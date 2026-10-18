// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const path = require('node:path');
const crypto = require('node:crypto');
const { ServiceClient } = require('./service-client');
const { resolveDotnet } = require('./dotnet');
const { exists, submoduleRoots, defaultPreviewWorker } = require('./project-paths');
const { creationRequest, renderNewProject } = require('./new-project');
const { readGlassJson } = require('./segment-setup');

async function isLibrary(directory) {
  return await exists(path.join(directory, 'include/lithe/application.hpp')) && await exists(path.join(directory, 'schema/widgets.json'));
}

async function chooseLibrary(vscode, folder, settings) {
  if (settings.libraryRoot) {
    const library = path.resolve(folder.uri.fsPath, settings.libraryRoot);
    if (!await isLibrary(library)) throw new Error(`Initialize the LiTheView submodule or correct litheview.libraryRoot: ${library}`);
    return library;
  }
  const candidates = [...new Set([
    ...await submoduleRoots(folder.uri.fsPath),
    path.join(folder.uri.fsPath, 'Middlewares/Third_Party/LiTheView'),
    path.join(folder.uri.fsPath, 'external/LiTheView')
  ])];
  const libraries = [];
  for (const directory of candidates) if (await isLibrary(directory)) libraries.push(directory);
  if (libraries.length === 1) return libraries[0];
  if (libraries.length > 1) {
    const choice = await vscode.window.showQuickPick([
      ...libraries.map(directory => ({ label: path.relative(folder.uri.fsPath, directory), directory })),
      { label: 'Browse for LiTheView…' }
    ], { placeHolder: 'Choose the LiTheView checkout used by this application' });
    if (!choice) return undefined;
    if (choice.directory) return choice.directory;
  }
  const selected = await vscode.window.showOpenDialog({ canSelectFolders: true, canSelectFiles: false, canSelectMany: false,
    defaultUri: folder.uri, openLabel: 'Use LiTheView', title: 'Select the initialized LiTheView submodule folder' });
  if (!selected?.length) return undefined;
  if (!await isLibrary(selected[0].fsPath)) throw new Error('Initialize LiTheView in the selected folder first. Expected include/lithe/application.hpp and schema/widgets.json.');
  return selected[0].fsPath;
}

async function openNewProject(vscode, context, output, configuration) {
  const folders = (vscode.workspace.workspaceFolders || []).filter(folder => folder.uri.scheme === 'file');
  if (!folders.length) throw new Error('Open your application root in VS Code first.');
  if (!vscode.workspace.isTrusted || vscode.env.remoteName) throw new Error('Add LiTheView UI requires a trusted local application workspace.');
  const folder = folders.length === 1 ? folders[0] : (await vscode.window.showQuickPick(
    folders.map(folder => ({ label: folder.name, description: folder.uri.fsPath, folder })),
    { placeHolder: 'Choose the application root' }))?.folder;
  if (!folder) return;
  const settings = configuration(folder.uri);
  const libraryRoot = await chooseLibrary(vscode, folder, settings);
  if (!libraryRoot) return;
  const runtime = vscode.Uri.joinPath(context.extensionUri, 'runtime').fsPath;
  const bundledService = path.join(runtime, 'service/LitheView.Designer.Service.dll');
  // Creation is a new capability. Prefer this extension's bundled service to an older
  // prepared service in a pinned dependency, while respecting explicit development overrides.
  const servicePath = settings.servicePath ? path.resolve(libraryRoot, settings.servicePath)
    : await exists(bundledService) ? bundledService : path.join(libraryRoot, 'build/vscode-designer/service/LitheView.Designer.Service.dll');
  if (!await exists(servicePath)) throw new Error('Prepare/install LiTheView Designer first; no designer service was found.');
  const preparedWorker = path.join(libraryRoot, defaultPreviewWorker(process.platform));
  const previewWorkerPath = settings.previewWorkerPath ? path.resolve(libraryRoot, settings.previewWorkerPath)
    : await exists(preparedWorker) ? preparedWorker : path.join(runtime, 'native', process.platform === 'win32'
      ? 'litheview_designer_preview_worker.exe' : 'litheview_designer_preview_worker');
  const { dotnetPath } = await resolveDotnet({ configured: settings.dotnetPath, servicePath });
  const client = new ServiceClient({ applicationRoot: folder.uri.fsPath, libraryRoot, servicePath, previewWorkerPath,
    dotnetPath, onLog: message => output.append(message) });
  let panel;
  try {
    const hello = await client.start();
    if (!hello.capabilities?.includes('project.create')) throw new Error('This prepared designer service predates Add LiTheView UI. Update the extension or run tools/prepare-vscode-designer.ps1 in its library.');
    const options = await client.request('project.options', {});
    panel = vscode.window.createWebviewPanel('litheview.newProject', 'Add LiTheView UI', vscode.ViewColumn.Active,
      { enableScripts: true, retainContextWhenHidden: true, localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')] });
    const asset = name => panel.webview.asWebviewUri(vscode.Uri.joinPath(context.extensionUri, 'media', name)).toString();
    panel.webview.html = renderNewProject({ scriptUri: asset('new-project.js'), styleUri: asset('new-project.css'), glassScriptUri: asset('glass-preview.js'),
      cspSource: panel.webview.cspSource, nonce: crypto.randomBytes(24).toString('base64') });
    let creating = false, disposed = false;
    const importGlass = async file => {
      if (disposed || creating) return;
      if (!vscode.workspace.isTrusted) throw new Error('Trust this workspace before importing glass.');
      if (!hello.capabilities?.includes('project.createSegmented')) throw new Error('Update the prepared designer service for segmented New Project support.');
      const glass = await client.request('project.segmentGlass', { segmentGlass: JSON.parse(await readGlassJson(file)) });
      if (!disposed) await panel.webview.postMessage({ type: 'glass', glass });
      return glass;
    };
    const handleMessage = async message => {
      if (disposed || !message || typeof message.type !== 'string') return;
      if (message.type === 'ready') {
        await panel.webview.postMessage({ type: 'initialize', ...options, applicationRoot: folder.uri.fsPath, libraryRoot });
      } else if (message.type === 'cancel' && !creating) panel.dispose();
      else if (message.type === 'importGlass' && !creating) {
        try {
          const selected = await vscode.window.showOpenDialog({ canSelectMany: false, filters: { 'Glass JSON': ['json'] } });
          if (selected?.length) await importGlass(selected[0].fsPath);
        } catch (error) { if (!disposed) await panel.webview.postMessage({ type: 'error', message: error.message }); }
      }
      else if (message.type === 'create' && !creating) {
        creating = true;
        try {
          if (!vscode.workspace.isTrusted) throw new Error('Trust this workspace before creating the UI.');
          const request = creationRequest(message.values, folder.uri.fsPath, options.surfaces, options.nativeDisplays);
          if ((request.nativeDisplayId || request.customNativeDisplay) && !hello.capabilities?.includes('project.createNative')) {
            throw new Error('Update LiTheView Designer to create native packed displays.');
          }
          if (request.segmentGlass && !hello.capabilities?.includes('project.createSegmented')) throw new Error('Update the prepared designer service for segmented New Project support.');
          await panel.webview.postMessage({ type: 'busy', busy: true });
          const result = await client.request('project.create', { project: request });
          output.appendLine(`Created LiTheView UI: ${result.manifestPath}`);
          for (const diagnostic of result.diagnostics || []) output.appendLine(`${diagnostic.severity}: ${diagnostic.message}`);
          panel.dispose();
          await vscode.commands.executeCommand('vscode.openWith', vscode.Uri.file(result.formPath), 'litheview.formDesigner');
          void vscode.window.showInformationMessage('UI created. Design your form, save and Generate UI, then build and debug your application.');
          return result;
        } catch (error) {
          output.appendLine(error.message);
          if (!disposed) await panel.webview.postMessage({ type: 'error', message: error.message });
          else void vscode.window.showErrorMessage(error.message);
        } finally {
          creating = false;
          if (disposed) client.dispose();
        }
      }
    };
    const subscription = panel.webview.onDidReceiveMessage(handleMessage);
    panel.onDidDispose(() => { disposed = true; subscription.dispose(); if (!creating) client.dispose(); });
    context.subscriptions.push(panel);
    return { submit: values => handleMessage({ type: 'create', values }), importGlass, cancel: () => handleMessage({ type: 'cancel' }), isOpen: () => !disposed };
  } catch (error) { client.dispose(); panel?.dispose(); throw error; }
}

module.exports = { chooseLibrary, openNewProject };
