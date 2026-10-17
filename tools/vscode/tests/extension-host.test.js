// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');

const extensionFile = path.join(__dirname, '..', 'extension.js');
const waitFor = async predicate => {
  const deadline = Date.now() + 2000;
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error('Timed out waiting for host state.');
    await new Promise(resolve => setTimeout(resolve, 5));
  }
};

function fixture(t, trusted = true) {
  const disposed = { dispose() {} };
  const calls = [];
  const messages = [];
  const diagnostics = new Map();
  const commands = new Map();
  const availableTasks = [];
  const executedTasks = [];
  const settings = {};
  const picks = [];
  let changeHandler;
  let messageHandler;
  let closeHandler;
  let provider;
  let serviceDisposals = 0;
  const uri = { scheme: 'file', fsPath: 'D:/app/Tasks/UI/forms/main.litheview.xml', toString() { return `file:///${this.fsPath}`; } };
  const document = { uri, version: 1, isClosed: false, xml: '<old/>', getText() { return this.xml; }, positionAt(offset) { return offset; }, async save() { calls.push({ method: 'save' }); return true; } };
  class WorkspaceEdit { replace(target, range, xml) { this.xml = xml; } }
  class Range {}
  class Diagnostic { constructor(range, message, severity) { this.message = message; this.severity = severity; } }
  const vscode = {
    env: {},
    Uri: { joinPath(root, ...segments) { return { fsPath: path.join(root.fsPath, ...segments), toString() { return this.fsPath; } }; } },
    Diagnostic, Range, WorkspaceEdit,
    DiagnosticSeverity: { Error: 0, Warning: 1, Information: 2 },
    window: {
      createOutputChannel: () => ({ ...disposed, append() {}, appendLine() {} }),
      registerCustomEditorProvider(type, instance) { provider = instance; return disposed; },
      showInformationMessage() {}, showErrorMessage() {},
      async showQuickPick(items) { picks.push(items); return items[0]; }
    },
    languages: { createDiagnosticCollection: () => ({ ...disposed, set(key, items) { diagnostics.set(key.toString(), items); }, delete(key) { diagnostics.delete(key.toString()); } }) },
    commands: { registerCommand(name, handler) { commands.set(name, handler); return disposed; }, executeCommand: async () => {} },
    TaskScope: { Workspace: 2 },
    tasks: { async fetchTasks() { return availableTasks; }, async executeTask(task) { executedTasks.push(task); } },
    workspace: {
      textDocuments: [],
      isTrusted: trusted,
      onDidChangeTextDocument(handler) { changeHandler = handler; return disposed; },
      onDidGrantWorkspaceTrust() { return disposed; },
      getConfiguration: () => ({ get: (name, fallback) => settings[name] ?? fallback }),
      getWorkspaceFolder: () => ({ uri: { fsPath: 'D:/app', toString: () => 'file:///D:/app' } }),
      async applyEdit(edit) { document.xml = edit.xml; document.version++; changeHandler({ document, contentChanges: [{}] }); return true; }
    }
  };
  class FakeService {
    async start() { calls.push({ method: 'start' }); return { capabilities: ['handlers', 'controlEvents'] }; }
    async request(method, params) {
      calls.push({ method, params });
      const result = { documentVersion: params.documentVersion, diagnostics: [], components: [], widgets: [], surface: { width: 1, height: 1 } };
      if (method === 'edit') return { ...result, xml: '<edited/>', selectedComponentId: 'button.new' };
      if (method === 'render') return { ...result, width: 1, height: 1, rgbaBase64: 'AAAAAA==', status: settings.renderStatus || 'Ok' };
      if (method === 'export') return { outputDirectory: 'D:/app/Generated', diagnostics: [] };
      if (method === 'handlers') return { ...result, handlerPath: 'D:/app/Tasks/UI/form_handlers.hpp', formId: 'form.main', include: 'Generated/product_config.hpp', handlers: [] };
      return result;
    }
    dispose() { serviceDisposals++; }
  }
  const module = { exports: {} };
  vm.runInNewContext(fs.readFileSync(extensionFile, 'utf8'), {
    module, exports: module.exports, process: { platform: 'win32' }, setTimeout, clearTimeout,
    require(name) {
      if (name === 'vscode') return vscode;
      if (name === './lib/service-client') return { ServiceClient: FakeService };
      if (name === './lib/project-paths') return {
        ...require('../lib/project-paths'), exists: async value => !value.endsWith('form_handlers.hpp'),
        resolveProject: async () => ({ manifestPath: 'D:/app/Tasks/UI/project.litheview.json', applicationRoot: 'D:/app', libraryRoot: 'D:/app/vendor/LiTheView', servicePath: 'D:/service.dll', previewWorkerPath: 'D:/worker.exe' })
      };
      return name.startsWith('.') ? require(path.resolve(path.dirname(extensionFile), name)) : require(name);
    }
  }, { filename: extensionFile });
  const context = { subscriptions: [], extensionUri: { fsPath: 'D:/extension' } };
  const api = module.exports.activate(context);
  const panel = {
    active: true,
    webview: {
      cspSource: 'https://unit.test',
      asWebviewUri: value => value,
      onDidReceiveMessage(handler) { messageHandler = handler; return disposed; },
      async postMessage(message) { messages.push(message); }
    },
    onDidDispose(handler) { closeHandler = handler; return disposed; }
  };
  const open = async () => { await provider.resolveCustomTextEditor(document, panel); messageHandler({ type: 'ready' }); };
  t.after(() => { closeHandler?.(); provider.dispose(); });
  return { api, uri, calls, messages, diagnostics, document, panel, open, commands, availableTasks, executedTasks, settings, picks, send: message => messageHandler(message), getServiceDisposals: () => serviceDisposals, close: () => closeHandler() };
}

test('untrusted workspace displays an explanation without starting a service', async t => {
  const f = fixture(t, false); await f.open();
  await waitFor(() => f.messages.some(message => message.type === 'error'));
  assert.match(f.api.getDocumentState(f.uri).lastError, /Trust this application workspace/);
  assert.equal(f.calls.length, 0);
  await assert.rejects(f.api.editDocument(f.uri, 1, { kind: 'text', id: 'a', text: 'b' }), /Trust/);
});

test('editor request uses current unsaved XML and applies one document change', async t => {
  const f = fixture(t); await f.open();
  await waitFor(() => f.api.getDocumentState(f.uri)?.lastRenderedVersion === 1);
  assert.equal(f.calls.find(call => call.method === 'inspect').params.xml, '<old/>');
  assert.equal(await f.api.editDocument(f.uri, 1, { kind: 'text', id: 'a', text: 'new' }), true);
  assert.equal(f.document.getText(), '<edited/>');
  assert.equal(f.document.version, 2);
  await waitFor(() => f.api.getDocumentState(f.uri)?.lastRenderedVersion === 2);
  assert.equal(f.messages.find(message => message.type === 'state' && message.documentVersion === 2).selectedComponentId, 'button.new');
  assert.equal(f.calls.filter(call => call.method === 'start').length, 1);
});

test('stale webview edits never reach the service', async t => {
  const f = fixture(t); await f.open();
  await waitFor(() => f.api.getDocumentState(f.uri)?.lastRenderedVersion === 1);
  assert.equal(await f.api.editDocument(f.uri, 0, { kind: 'delete', id: 'a' }), false);
  assert.equal(f.calls.some(call => call.method === 'edit'), false);
  assert.equal(f.document.getText(), '<old/>');
});

test('failed native frames never count as successful rendered document versions', async t => {
  const f = fixture(t); f.settings.renderStatus = 'InvalidDocument'; await f.open();
  await waitFor(() => f.api.getDocumentState(f.uri)?.lastError);
  assert.equal(f.api.getDocumentState(f.uri).lastRenderedVersion, undefined);
  assert.equal(f.api.getDocumentState(f.uri).lastRenderStatus, 'InvalidDocument');
  assert.match(f.api.getDocumentState(f.uri).lastError, /Native preview failed/);
});

test('generation saves before export and closing clears diagnostics and service', async t => {
  const f = fixture(t); await f.open();
  await waitFor(() => f.api.getDocumentState(f.uri)?.lastRenderedVersion === 1);
  f.send({ type: 'generate' });
  await waitFor(() => f.calls.some(call => call.method === 'export'));
  assert.ok(f.calls.findIndex(call => call.method === 'save') < f.calls.findIndex(call => call.method === 'export'));
  f.close();
  assert.equal(f.api.getDocumentState(f.uri), undefined);
  assert.equal(f.diagnostics.size, 0);
  assert.equal(f.getServiceDisposals(), 1);
});

test('designer has no build/run commands and ignores obsolete task messages', async t => {
  const f = fixture(t); await f.open();
  for (const command of ['litheview.runSimulator', 'litheview.buildFirmware', 'litheview.runUiStarter', 'litheview.activeProjectManifest']) {
    assert.equal(f.commands.has(command), false);
  }
  f.send({ type: 'runSimulator' });
  f.send({ type: 'buildFirmware' });
  assert.equal(f.executedTasks.length, 0);
});

test('Configure rejects an explicitly selected older service with no migration', async t => {
  const f = fixture(t); await f.open();
  await waitFor(() => f.api.getDocumentState(f.uri)?.lastRenderedVersion === 1);
  assert.ok(f.commands.has('litheview.projectSetup'));
  await assert.rejects(f.api.projectSetup(f.uri, 'touch'), /Designer 0.2.7/);
  assert.equal(f.calls.some(call => call.method === 'project.setup'), false);
});
