// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const vscode = require('vscode');
const path = require('node:path');
const crypto = require('node:crypto');
const { ServiceClient } = require('./lib/service-client');
const { resolveDotnet, listRuntimes } = require('./lib/dotnet');
const { resolveProject, readManifestForms, submoduleRoots, isInside, exists } = require('./lib/project-paths');
const { applyVersionedEdit, isCurrent } = require('./lib/document-version');
const { renderWebview } = require('./lib/webview');
const { openNewProject } = require('./lib/new-project-host');
const { planHandlerEdit, validateBindings } = require('./lib/form-handlers');
const { applySetupPlan, assertSetupSaved, openProjectSetup } = require('./lib/project-setup');

const VIEW_TYPE = 'litheview.formDesigner';

function configuration(uri) {
  const value = vscode.workspace.getConfiguration('litheview', uri);
  return Object.fromEntries(['libraryRoot', 'servicePath', 'previewWorkerPath', 'dotnetPath'].map(name => [name, value.get(name, '')]));
}

function assertSupported(uri) {
  if (!vscode.workspace.isTrusted) throw new Error('Trust this application workspace before running the designer service. You can still edit the form source.');
  // Windows shares preview frames through named memory; Linux and macOS receive them inline.
  if (!['win32', 'linux', 'darwin'].includes(process.platform) || vscode.env.remoteName || uri.scheme !== 'file') throw new Error('This designer supports local Windows, Linux, and macOS workspaces. Use View Form Source in this environment.');
}

class DesignerProvider {
  constructor(context, output, diagnostics) {
    this.context = context;
    this.output = output;
    this.diagnostics = diagnostics;
    this.states = new Map();
    this.configurationWrites = new Set();
    this.configurationRevisions = new Map();
    // `dotnet --list-runtimes` results for this session, by executable.
    this.runtimeLists = new Map();
    this.disposables = [
      vscode.workspace.onDidChangeTextDocument(event => {
        const state = this.states.get(event.document.uri.toString());
        if (state && event.contentChanges.length) this.schedule(state);
      }),
      vscode.workspace.onDidGrantWorkspaceTrust(() => {
        for (const state of this.states.values()) this.schedule(state);
      })
    ];
  }

  async resolveCustomTextEditor(document, panel) {
    const state = { document, panel, client: undefined, project: undefined, disposed: false, ready: false, scheduled: false, refreshing: false, editing: false, lastInspectedVersion: undefined, lastRenderedVersion: undefined, lastError: undefined };
    this.states.set(document.uri.toString(), state);
    panel.webview.options = { enableScripts: true, localResourceRoots: [vscode.Uri.joinPath(this.context.extensionUri, 'media')] };
    const scriptUri = panel.webview.asWebviewUri(vscode.Uri.joinPath(this.context.extensionUri, 'media', 'designer.js')).toString();
    const styleUri = panel.webview.asWebviewUri(vscode.Uri.joinPath(this.context.extensionUri, 'media', 'designer.css')).toString();
    panel.webview.html = renderWebview({ scriptUri, styleUri, cspSource: panel.webview.cspSource, nonce: crypto.randomBytes(24).toString('base64') });
    const messageSubscription = panel.webview.onDidReceiveMessage(message => {
      this.handleMessage(state, message).catch(error => this.showError(state, error));
    });
    panel.onDidDispose(() => {
      state.disposed = true;
      state.setup?.cancel();
      clearTimeout(state.timer);
      state.client?.dispose();
      messageSubscription.dispose();
      this.diagnostics.delete(document.uri);
      this.states.delete(document.uri.toString());
    });
  }

  post(state, message) {
    if (!state.disposed && state.ready) void state.panel.webview.postMessage(message);
  }

  showError(state, error) {
    if (state.disposed) return;
    const message = error instanceof Error ? error.message : String(error);
    state.lastError = message;
    this.output.appendLine(message);
    this.post(state, { type: 'error', message });
    this.post(state, { type: 'busy', busy: false });
  }

  async service(state) {
    assertSupported(state.document.uri);
    if (state.servicePromise) return state.servicePromise;
    if (state.client) return state.client;
    state.servicePromise = this.startService(state);
    return state.servicePromise;
  }

  async startService(state) {
    const workspace = vscode.workspace.getWorkspaceFolder(state.document.uri);
    if (!workspace) throw new Error('Open the application root as a VS Code workspace before opening its form.');
    const settings = configuration(state.document.uri);
    const project = await resolveProject(state.document.uri.fsPath, workspace.uri.fsPath, settings);
    const runtime = vscode.Uri.joinPath(this.context.extensionUri, 'runtime').fsPath;
    const bundledService = path.join(runtime, 'service', 'LitheView.Designer.Service.dll');
    const bundledWorker = path.join(runtime, 'native', process.platform === 'win32' ? 'litheview_designer_preview_worker.exe' : 'litheview_designer_preview_worker');
    // An installed extension owns its matching runtime. Explicit settings still
    // support service development; source checkouts fall back to prepared tools.
    if (!settings.servicePath && await exists(bundledService)) project.servicePath = bundledService;
    if (!settings.previewWorkerPath && await exists(bundledWorker)) project.previewWorkerPath = bundledWorker;
    if (!await exists(project.servicePath)) throw new Error(`Designer service is missing: ${project.servicePath}. Prepare the extension runtime or set litheview.servicePath.`);
    let dotnet;
    try {
      dotnet = await resolveDotnet({ configured: settings.dotnetPath, servicePath: project.servicePath }, {
        listRuntimes: candidate => {
          if (!this.runtimeLists.has(candidate)) this.runtimeLists.set(candidate, listRuntimes(candidate));
          return this.runtimeLists.get(candidate);
        }
      });
    } catch (error) {
      // Check again after the developer installs a runtime or changes the setting.
      this.runtimeLists.clear();
      throw error;
    }
    project.dotnetPath = dotnet.dotnetPath;
    this.output.appendLine(`Designer service: ${project.dotnetPath} ${project.servicePath}`);
    // Missing renderer is reported by render, so editing and validation remain usable.
    const client = new ServiceClient({ ...project, onLog: message => this.output.append(message) });
    if (state.disposed) { client.dispose(); throw new Error('Designer editor closed.'); }
    state.project = project;
    state.client = client;
    const hello = await client.start();
    state.capabilities = hello?.capabilities || [];
    if (!hello?.capabilities?.includes('controlEvents')) {
      client.dispose();
      throw new Error('This prepared designer service predates typed control events. Update the pinned LiTheView package and prepare its designer service, or select the service bundled with Designer 0.2.9.');
    }
    return client;
  }

  parameters(state) {
    return { manifestPath: state.project.manifestPath, formPath: state.document.uri.fsPath, xml: state.document.getText(), documentVersion: state.document.version };
  }

  schedule(state) {
    if (state.disposed || !state.ready) return;
    state.scheduled = true;
    clearTimeout(state.timer);
    state.timer = setTimeout(() => { void this.refresh(state); }, 120);
  }

  async refresh(state) {
    if (state.disposed || state.refreshing || !state.scheduled) return;
    if (this.configurationWrites.has(state.project?.manifestPath)) return;
    let manifest, revision;
    const currentConfiguration = () => !this.configurationWrites.has(manifest) &&
      this.configurationRevisions.get(manifest) === revision;
    state.refreshing = true;
    state.scheduled = false;
    this.post(state, { type: 'busy', busy: true });
    try {
      const client = await this.service(state);
      if (state.disposed) return;
      manifest = state.project.manifestPath;
      revision = this.configurationRevisions.get(manifest);
      if (!currentConfiguration()) { state.scheduled = true; return; }
      const params = this.parameters(state);
      const result = await client.request('inspect', params);
      if (!state.disposed && currentConfiguration() && isCurrent(state.document, params.documentVersion, result)) {
        state.lastError = undefined;
        state.lastInspectedVersion = params.documentVersion;
        this.updateDiagnostics(state, result.diagnostics || []);
        this.post(state, { type: 'state', ...result, ...(state.pendingSelection ? { selectedComponentId: state.pendingSelection } : {}) });
        state.pendingSelection = undefined;
        const invalid = (result.diagnostics || []).some(item => String(item.severity).toLowerCase() === 'error');
        if (!invalid) {
          const frame = await client.request('render', params);
          if (!state.disposed && currentConfiguration() && isCurrent(state.document, params.documentVersion, frame)) {
            state.lastRenderStatus = frame.status;
            if (frame.diagnostics?.length) this.updateDiagnostics(state, [...(result.diagnostics || []), ...frame.diagnostics]);
            this.post(state, { type: 'frame', ...frame });
            if (frame.status !== 'Ok' || typeof frame.rgbaBase64 !== 'string' || !frame.rgbaBase64 || frame.width <= 0 || frame.height <= 0) {
              throw new Error(`Native preview failed: ${frame.status || 'missing pixels'}. ${(frame.diagnostics || []).map(item => item.message).join('; ')}`);
            }
            state.lastRenderedVersion = params.documentVersion;
          }
        }
      }
    } catch (error) { if (currentConfiguration()) this.showError(state, error); }
    finally {
      state.refreshing = false;
      this.post(state, { type: 'busy', busy: false });
      if (state.scheduled && !state.disposed && !this.configurationWrites.has(state.project?.manifestPath)) this.schedule(state);
    }
  }

  updateDiagnostics(state, diagnostics) {
    const mapped = diagnostics.map(item => {
      const severityName = String(item.severity).toLowerCase();
      const severity = severityName === 'error' ? vscode.DiagnosticSeverity.Error : severityName === 'warning' ? vscode.DiagnosticSeverity.Warning : vscode.DiagnosticSeverity.Information;
      const diagnostic = new vscode.Diagnostic(new vscode.Range(0, 0, 0, 1), item.message || item.code || 'Invalid form', severity);
      diagnostic.code = item.code;
      diagnostic.source = 'LiTheView';
      return diagnostic;
    });
    this.diagnostics.set(state.document.uri, mapped);
  }

  async edit(state, version, operation) {
    assertSupported(state.document.uri);
    if (state.disposed || state.editing || !Number.isInteger(version) || version !== state.document.version) {
      this.schedule(state);
      return false;
    }
    if (!operation || typeof operation !== 'object') return false;
    state.editing = true;
    this.post(state, { type: 'busy', busy: true });
    try {
      const client = await this.service(state);
      if (version !== state.document.version || state.disposed) return false;
      const result = await client.request('edit', { ...this.parameters(state), operation });
      if (state.disposed) return false;
      const applied = await applyVersionedEdit(vscode, state.document, version, result);
      if (applied && result.selectedComponentId) state.pendingSelection = result.selectedComponentId;
      this.schedule(state);
      return applied;
    } finally { state.editing = false; this.post(state, { type: 'busy', busy: false }); }
  }

  async generate(state) {
    assertSupported(state.document.uri);
    if (state.disposed) return;
    if (state.editing) throw new Error('Finish the current designer edit or close Configure before Generate UI.');
    if (!await state.document.save()) throw new Error('Save the form before generating the UI.');
    const client = await this.service(state);
    await assertSetupSaved(vscode, state.project);
    const params = this.parameters(state);
    this.post(state, { type: 'busy', busy: true });
    try {
      const plan = await client.request('handlers', params);
      if (await exists(plan.handlerPath)) {
        const handlers = await vscode.workspace.openTextDocument(vscode.Uri.file(plan.handlerPath));
        validateBindings(handlers.getText(), plan);
      }
      const result = await client.request('export', params);
      this.updateDiagnostics(state, result.diagnostics || []);
      this.output.appendLine(`Generated UI: ${result.outputDirectory || state.project.applicationRoot}`);
      void vscode.window.showInformationMessage(`LiTheView UI generated: ${result.outputDirectory || 'complete'}`);
    } finally { this.post(state, { type: 'busy', busy: false }); }
  }

  async openHandler(state, componentId, version, eventId = 'clicked') {
    assertSupported(state.document.uri);
    if (state.disposed || state.editing || version !== state.document.version) return;
    state.editing = true;
    this.post(state, { type: 'busy', busy: true });
    try {
      const client = await this.service(state);
      const plan = await client.request('handlers', this.parameters(state));
      if (state.disposed || version !== state.document.version || plan.documentVersion !== version) return;
      if (!isInside(plan.handlerPath, state.project.applicationRoot)) throw new Error('The handler must remain in the application workspace.');
      const uri = vscode.Uri.file(plan.handlerPath);
      const present = await exists(plan.handlerPath);
      const document = present ? await vscode.workspace.openTextDocument(uri) : undefined;
      if (state.disposed || version !== state.document.version) return;
      const originalVersion = document?.version;
      const result = planHandlerEdit(document?.getText() ?? null, plan, componentId, eventId);
      if (result.changed) {
        const edit = new vscode.WorkspaceEdit();
        if (document) {
          if (document.version !== originalVersion) throw new Error('Handler source changed; retry Create/Open handler.');
          edit.replace(uri, new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length)), result.text);
        } else {
          edit.createFile(uri, { overwrite: false, ignoreIfExists: false });
          edit.insert(uri, new vscode.Position(0, 0), result.text);
        }
        if (!await vscode.workspace.applyEdit(edit)) throw new Error('The handler edit could not be applied.');
      }
      const opened = await vscode.workspace.openTextDocument(uri);
      const position = opened.positionAt(result.offset);
      await vscode.window.showTextDocument(opened, { preview: false, selection: new vscode.Range(position, position) });
    } finally { state.editing = false; this.post(state, { type: 'busy', busy: false }); }
  }

  async projectSetup(state) {
    assertSupported(state.document.uri);
    if (state.disposed || state.editing) return;
    state.editing = true;
    this.post(state, { type: 'busy', busy: true });
    const finished = () => {
      state.editing = false;
      state.setup = undefined;
      this.post(state, { type: 'busy', busy: false });
    };
    try {
      const client = await this.service(state);
      if (!state.capabilities.includes('project.setup')) throw new Error('Configure requires Designer 0.2.7 or later and its matching service.');
      await assertSetupSaved(vscode, state.project);
      const params = this.parameters(state);
      const current = await client.request('project.setup', params);
      if (state.disposed || state.document.version !== params.documentVersion) throw new Error('The form changed; reopen Configure.');
      state.setup = await openProjectSetup(vscode, this.context, state.project, current, async (setup, front) => {
        assertSupported(state.document.uri);
        await assertSetupSaved(vscode, state.project);
        const plan = await client.request('project.setup', { ...params, setup });
        const manifest = state.project.manifestPath;
        if (this.configurationWrites.has(manifest)) throw new Error('Another form is saving this project configuration. Retry Apply.');
        this.configurationWrites.add(manifest);
        this.configurationRevisions.set(manifest, (this.configurationRevisions.get(manifest) ?? 0) + 1);
        let changed;
        try {
          changed = await applySetupPlan(vscode, state.project, plan,
            () => !state.disposed && state.setup?.isOpen() && state.document.version === params.documentVersion && plan.documentVersion === params.documentVersion, front);
        } finally {
          this.configurationWrites.delete(manifest);
          for (const other of this.states.values()) {
            if (other.project?.manifestPath === manifest) { other.lastError = undefined; this.schedule(other); }
          }
        }
        void vscode.window.showInformationMessage(changed
          ? 'Project settings saved. Review the form, Generate UI, then build and debug with your existing application configurations.'
          : 'Project settings are unchanged.');
        return changed;
      }, finished);
      if (state.disposed) { state.setup.cancel(); return; }
      return state.setup;
    } catch (error) { finished(); throw error; }
  }

  async handleMessage(state, message) {
    if (!message || typeof message.type !== 'string' || state.disposed) return;
    switch (message.type) {
      case 'ready': state.ready = true; this.schedule(state); break;
      case 'edit': await this.edit(state, message.documentVersion ?? message.version, message.operation); break;
      case 'generate': await this.generate(state); break;
      case 'handler': await this.openHandler(state, message.componentId, message.documentVersion, message.eventId); break;
      case 'setup': await this.projectSetup(state); break;
      case 'source': await vscode.commands.executeCommand('vscode.openWith', state.document.uri, 'default'); break;
    }
  }

  current(uri) {
    if (uri) return this.states.get(typeof uri === 'string' ? uri : uri.toString());
    return [...this.states.values()].find(state => state.panel.active) || this.states.get(vscode.window.activeTextEditor?.document.uri.toString());
  }

  dispose() {
    for (const state of this.states.values()) {
      state.disposed = true;
      state.setup?.cancel();
      clearTimeout(state.timer);
      state.client?.dispose();
    }
    for (const disposable of this.disposables) disposable.dispose();
    this.states.clear();
  }
}

async function chooseForm(output) {
  const folders = vscode.workspace.workspaceFolders || [];
  if (!folders.length) throw new Error('Open your application root in VS Code first.');
  const found = new Map();
  for (const folder of folders) {
    if (folder.uri.scheme !== 'file') continue;
    const modules = await submoduleRoots(folder.uri.fsPath);
    const manifests = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, '**/project.litheview.json'), '**/{build,.git,node_modules,Generated,generated}/**');
    for (const manifest of manifests) {
      if (modules.some(module => isInside(manifest.fsPath, module))) continue;
      try {
        for (const form of await readManifestForms(manifest.fsPath)) {
          if (!isInside(form, folder.uri.fsPath) || modules.some(module => isInside(form, module)) || !await exists(form)) continue;
          found.set(form, { label: path.basename(form), description: path.relative(folder.uri.fsPath, form), detail: folder.name, uri: vscode.Uri.file(form) });
        }
      } catch (error) { output.appendLine(`Skipping ${manifest.fsPath}: ${error.message}`); }
    }
  }
  if (!found.size) throw new Error('No application forms were found. The form must be listed in an application project.litheview.json.');
  const choices = [...found.values()];
  return choices.length === 1 ? choices[0].uri : (await vscode.window.showQuickPick(choices, { placeHolder: 'Choose the application form to edit' }))?.uri;
}

function activate(context) {
  const output = vscode.window.createOutputChannel('LiTheView Designer');
  const diagnostics = vscode.languages.createDiagnosticCollection('litheview');
  const provider = new DesignerProvider(context, output, diagnostics);
  let projectWizard;
  const guarded = action => async (...args) => {
    try { return await action(...args); }
    catch (error) { output.appendLine(error.message); void vscode.window.showErrorMessage(error.message); }
  };
  const requireState = uri => {
    const state = provider.current(uri);
    if (!state) throw new Error('Open a LiTheView form in the designer first.');
    return state;
  };
  context.subscriptions.push(output, diagnostics, provider,
    vscode.commands.registerCommand('litheview.addUi', guarded(async () => {
      if (projectWizard?.isOpen()) throw new Error('Finish or close the open Add LiTheView UI tab first.');
      projectWizard = await openNewProject(vscode, context, output, configuration);
    })),
    vscode.window.registerCustomEditorProvider(VIEW_TYPE, provider, { webviewOptions: { retainContextWhenHidden: true }, supportsMultipleEditorsPerDocument: false }),
    vscode.commands.registerCommand('litheview.openDesigner', guarded(async () => { const uri = await chooseForm(output); if (uri) await vscode.commands.executeCommand('vscode.openWith', uri, VIEW_TYPE); })),
    vscode.commands.registerCommand('litheview.generateUi', guarded(async uri => provider.generate(requireState(uri)))),
    vscode.commands.registerCommand('litheview.projectSetup', guarded(async uri => provider.projectSetup(requireState(uri)))),
    vscode.commands.registerCommand('litheview.viewSource', guarded(async uri => vscode.commands.executeCommand('vscode.openWith', requireState(uri).document.uri, 'default'))),
  );
  // Also used by the integration test running in an actual VS Code host.
  return {
    // Exercise the same submitted wizard data in the actual VS Code integration host.
    async submitNewProject(values) {
      if (!projectWizard?.isOpen()) throw new Error('Open Add LiTheView UI first.');
      return projectWizard.submit(values);
    },
    getDocumentState(uri) {
      const state = provider.current(uri);
      return state ? { version: state.document.version, lastInspectedVersion: state.lastInspectedVersion, lastRenderedVersion: state.lastRenderedVersion, lastRenderStatus: state.lastRenderStatus, hasPanel: !state.disposed, lastError: state.lastError } : undefined;
    },
    async editDocument(uri, documentVersion, operation) { return provider.edit(requireState(uri), documentVersion, operation); },
    async projectSetup(uri) { return provider.projectSetup(requireState(uri)); },
    async openHandler(uri, componentId, documentVersion, eventId) { return provider.openHandler(requireState(uri), componentId, documentVersion, eventId); }
  };
}

module.exports = { activate };
