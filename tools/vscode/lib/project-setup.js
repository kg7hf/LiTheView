// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { isInside, samePath } = require('./project-paths');
const { customSurface, customNativeDisplay, renderNewProject } = require('./new-project');
const { frontPanelPath, readFrontPanel, frontPanelEdit, chooseFrontImage } = require('./front-panel');
const normalize = text => text.replace(/^\uFEFF/, '').replace(/\r\n|\r/g, '\n');
const keys = ['pointer', 'keys', 'text', 'encoder', 'select'];
const inputs = {
  outputOnly: { pointer: false, keys: false, text: false, encoder: false, select: false },
  touch: { pointer: true, keys: false, text: false, encoder: false, select: false },
  touchKeys: { pointer: true, keys: true, text: false, encoder: false, select: true },
  encoderSelect: { pointer: false, keys: false, text: false, encoder: true, select: true }
};

function configurationPaths(project) {
  return ['logical-input.json', 'display-definition.json', 'display-surface.json']
    .map(name => path.join(path.dirname(project.manifestPath), 'profiles', name)).concat(project.manifestPath);
}

async function assertSetupSaved(vscode, project) {
  const paths = [...configurationPaths(project), frontPanelPath(project)];
  if (vscode.workspace.textDocuments.some(document => document.isDirty && paths.some(file => samePath(document.uri.fsPath, file)))) {
    throw new Error('Save the project manifest and display/input profiles before Configure or Generate UI.');
  }
}

function setupValues(current) {
  const surface = current.displaySurface, definition = current.displayDefinition;
  const geometry = definition.geometry, safe = geometry.safeArea;
  const custom = { width: geometry.width, height: geometry.height, shape: geometry.shape, format: definition.format,
    safeArea: [safe.x, safe.y, safe.width, safe.height].join(','), sourceReference: definition.source.reference };
  if (surface) Object.assign(custom, {
    physicalWidth: surface.physicalSize.diameterMillimeters ?? surface.physicalSize.widthMillimeters,
    physicalHeight: surface.physicalSize.heightMillimeters, transfer: surface.nativeDisplay.transferFormat,
    manufacturer: surface.provenance.manufacturer, model: surface.provenance.model,
    sourceUrl: surface.provenance.sourceUrl, sourceDocument: surface.provenance.sourceDocument
  });
  const logicalInput = Object.keys(inputs).find(choice => keys.every(key => inputs[choice][key] === current.input[key])) ?? 'custom';
  return { custom, logicalInput, input: Object.fromEntries(keys.map(key => [key, current.input[key]])) };
}

function setupRequest(values, current) {
  const input = values?.logicalInput === 'custom' ? values.input : inputs[values?.logicalInput];
  if (!input || !keys.every(key => typeof input[key] === 'boolean')) throw new Error('Choose the logical input capabilities.');
  const request = { snapshot: current.snapshot, input: Object.fromEntries(keys.map(key => [key, input[key]])) };
  if (current.displaySurface) request.customSurface = {
    ...customSurface(values.custom, current.displaySurface.id, current.displaySurface.displayName),
    id: current.displaySurface.id, compatibilityFamily: current.displaySurface.compatibilityFamily
  };
  else {
    if (!['r2Packed2x2', 'rrrr2222', 'ryry2222'].includes(values.custom?.format)) throw new Error('Choose a packed storage format.');
    request.customNativeDisplay = customNativeDisplay(values.custom);
  }
  return request;
}

async function applySetupPlan(vscode, project, plan, isCurrent, frontEdit) {
  const allowed = configurationPaths(project);
  const expected = plan.displaySurface ? allowed : allowed.filter(file => !file.endsWith('display-surface.json'));
  if (!Array.isArray(plan.documents) || plan.documents.length !== expected.length || !Array.isArray(plan.edits) ||
      (plan.edits.length > 0 && (plan.edits.length < 2 || !samePath(plan.edits.at(-1).path, project.manifestPath)))) {
    throw new Error('Invalid project setup edit plan.');
  }
  const documents = [], seen = new Set();
  const root = await fs.realpath(project.applicationRoot);
  for (let index = 0; index < plan.documents.length; index++) {
    const source = plan.documents[index];
    if (typeof source.path !== 'string' || !samePath(source.path, expected[index]) ||
        typeof source.before !== 'string' || source.before.length > 1024 * 1024 ||
        !isInside(await fs.realpath(source.path), root)) throw new Error('Invalid project setup document.');
    const document = await vscode.workspace.openTextDocument(vscode.Uri.file(source.path));
    if (document.isDirty || normalize(document.getText()) !== normalize(source.before) ||
        normalize(await fs.readFile(source.path, 'utf8')) !== normalize(source.before)) {
      throw new Error('Project settings changed; save your edits and reopen Configure.');
    }
    documents.push({ document, version: document.version, source });
  }
  const edits = plan.edits.map(edit => {
    const item = documents.find(item => typeof edit.path === 'string' && samePath(item.source.path, edit.path));
    if (!item || seen.has(item) || edit.before !== item.source.before || typeof edit.after !== 'string' || edit.after.length > 1024 * 1024) {
      throw new Error('Invalid project setup edit path or content.');
    }
    seen.add(item);
    return { ...item, edit };
  });
  if (frontEdit) {
    if (!samePath(frontEdit.path, frontPanelPath(project)) ||
        (frontEdit.before !== null && typeof frontEdit.before !== 'string') ||
        (frontEdit.after === null && frontEdit.before !== null) ||
        (frontEdit.after !== null && (typeof frontEdit.after !== 'string' || frontEdit.after.length > 65536))) throw new Error('Invalid front-view edit.');
    const parent = await fs.realpath(path.dirname(frontEdit.path));
    if (!isInside(parent, root)) throw new Error('The front-view settings must stay inside the application.');
    let disk = null;
    try {
      if (!isInside(await fs.realpath(frontEdit.path), root)) throw new Error('Invalid front-view settings path.');
      disk = await fs.readFile(frontEdit.path, 'utf8');
    } catch (error) { if (error.code !== 'ENOENT') throw error; }
    if (disk !== frontEdit.before) throw new Error('Front-view settings changed; reopen Configure.');
    const document = disk === null ? undefined : await vscode.workspace.openTextDocument(vscode.Uri.file(frontEdit.path));
    if (document && (document.isDirty || normalize(document.getText()) !== normalize(disk))) throw new Error('Save the front-view settings and reopen Configure.');
    const item = { document, version: document?.version, source: frontEdit, edit: frontEdit };
    documents.push(item);
    if (frontEdit.before !== frontEdit.after) edits.unshift(item);
  }
  if (!isCurrent() || documents.some(item => item.document && (item.document.version !== item.version || item.document.isDirty))) {
    throw new Error('The form or project settings changed; reopen Configure.');
  }
  if (!edits.length) return false;
  const update = new vscode.WorkspaceEdit();
  for (const { document, edit } of edits) {
    if (document) update.replace(document.uri, new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length)), edit.after);
    else {
      const uri = vscode.Uri.file(edit.path);
      update.createFile(uri, { overwrite: false, ignoreIfExists: false });
      update.insert(uri, new vscode.Position(0, 0), edit.after);
    }
  }
  if (!await vscode.workspace.applyEdit(update)) throw new Error('The project setup edit could not be applied.');
  for (const item of edits) {
    const { edit } = item;
    const document = item.document ?? await vscode.workspace.openTextDocument(vscode.Uri.file(edit.path));
    if (normalize(document.getText()) !== normalize(edit.after) || !await document.save()) {
      throw new Error('Configure is not fully saved. Save all display/input profiles and the project manifest before Generate UI.');
    }
  }
  return true;
}

async function openProjectSetup(vscode, context, project, current, apply, finished) {
  const frontPanel = await readFrontPanel(project);
  const panel = vscode.window.createWebviewPanel('litheview.projectSetup', 'LiTheView: Configure', vscode.ViewColumn.Active,
    { enableScripts: true, retainContextWhenHidden: true, localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')] });
  const asset = name => panel.webview.asWebviewUri(vscode.Uri.joinPath(context.extensionUri, 'media', name)).toString();
  panel.webview.html = renderNewProject({ scriptUri: asset('new-project.js'), styleUri: asset('new-project.css'),
    frontScriptUri: asset('front-panel.js'), cspSource: panel.webview.cspSource, nonce: crypto.randomBytes(24).toString('base64'), setup: true });
  let disposed = false, applying = false;
  const handleMessage = async message => {
    if (disposed || !message || applying) return;
    if (message.type === 'ready') await panel.webview.postMessage({ type: 'initialize', setup: true,
      packed: !current.displaySurface, values: setupValues(current), surfaces: [], frontPanel,
      applicationRoot: project.applicationRoot, libraryRoot: project.libraryRoot });
    else if (message.type === 'cancel') panel.dispose();
    else if (message.type === 'front-image') {
      try {
        const image = await chooseFrontImage(vscode, project, message.relative);
        if (image && !disposed) await panel.webview.postMessage({ type: 'front-image', image });
        return image;
      } catch (error) { if (!disposed) await panel.webview.postMessage({ type: 'error', message: error.message }); }
    }
    else if (message.type === 'apply') {
      applying = true;
      try {
        await panel.webview.postMessage({ type: 'busy', busy: true });
        const front = await frontPanelEdit(project, frontPanel, message.values.frontPanel);
        const changed = await apply(setupRequest(message.values, current), front);
        panel.dispose();
        return changed;
      } catch (error) {
        if (!disposed) await panel.webview.postMessage({ type: 'error', message: error.message });
        else void vscode.window.showErrorMessage(error.message);
      } finally { applying = false; if (disposed) finished(); }
    }
  };
  const subscription = panel.webview.onDidReceiveMessage(handleMessage);
  panel.onDidDispose(() => { disposed = true; subscription.dispose(); if (!applying) finished(); });
  context.subscriptions.push(panel);
  return { values: { ...setupValues(current), frontPanel: frontPanel.value }, submit: values => handleMessage({ type: 'apply', values }),
    chooseImage: relative => handleMessage({ type: 'front-image', relative }),
    cancel: () => panel.dispose(), isOpen: () => !disposed };
}

module.exports = { setupValues, setupRequest, assertSetupSaved, applySetupPlan, openProjectSetup };
