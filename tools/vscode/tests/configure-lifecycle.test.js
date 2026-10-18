// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { openProjectSetup } = require('../lib/project-setup');

function harness() {
  const panels = [], context = { extensionUri: 'extension', subscriptions: [] };
  const vscode = { ViewColumn: { Active: 1 }, Uri: { joinPath: (...parts) => parts.join('/') }, window: {
    createWebviewPanel(kind) {
      const panel = { kind, disposed: false, messages: [],
        webview: { cspSource: 'vscode-resource:', asWebviewUri: value => value,
          onDidReceiveMessage(handler) { panel.receive = handler; return { dispose() {} }; },
          async postMessage(message) { panel.messages.push(message); panel.toMedia?.({ data: message }); return true; } },
        onDidDispose(handler) { panel.closed = handler; },
        dispose() { if (!panel.disposed) { panel.disposed = true; panel.closed(); } } };
      panels.push(panel); return panel;
    }
  } };
  return { panels, context, vscode };
}
const current = { displayKind: 'segmented', snapshot: 'lock',
  segmentGlass: { id: 'tinyGeckoA03', segmentCount: 160, viewWidth: 870, viewHeight: 330, texts: [], numbers: [], icons: [], groups: [], shapes: [{ polygon: [{x:0,y:0},{x:10,y:0},{x:0,y:10}] }] },
  input: { pointer: false, keys: true, text: false, encoder: true, select: true } };

function media(panel) {
  const nodes = new Map(), pending = [];
  const element = id => {
    assert.ok(panel.webview.html.includes(`id="${id}"`), `Missing element ${id}`);
    if (!nodes.has(id)) nodes.set(id, { value: '', checked: false, disabled: false, textContent: '', width: 600, height: 300, fills: 0,
      getContext() { const node = this; return { resetTransform() {}, clearRect() {}, scale() {}, beginPath() {}, lineTo() {}, moveTo() {}, closePath() {}, fill() { node.fills++; } }; } });
    return nodes.get(id);
  };
  const context = vm.createContext({
    document: { getElementById: element },
    window: { addEventListener(event, handler) { assert.equal(event, 'message'); panel.toMedia = handler; } },
    acquireVsCodeApi: () => ({ postMessage(message) { pending.push(panel.receive(message)); } })
  });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../media/glass-preview.js'), 'utf8'), context);
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../media/segment-setup.js'), 'utf8'), context);
  return { element, async flush() { await Promise.all(pending.splice(0)); } };
}

test('segmented Configure initializes the correct panel; real Cancel and Apply messages complete its lifecycle', async () => {
  for (const action of ['cancel', 'apply', 'close']) {
    const h = harness(); let finished = 0, writes = 0;
    const setup = await openProjectSetup(h.vscode, h.context, {}, current, async request => {
      writes++; assert.equal(request.snapshot, 'lock'); assert.deepEqual(request.segmentGlass, current.segmentGlass);
      assert.deepEqual(request.input, current.input); return true;
    }, () => finished++);
    const panel = h.panels[0]; assert.equal(panel.kind, 'litheview.segmentSetup');
    assert.doesNotMatch(panel.webview.html, /PC front view|id="width"/);
    const view = media(panel); await view.flush();
    assert.equal(JSON.parse(view.element('glass').value).id, 'tinyGeckoA03');
    assert.match(view.element('glass-summary').textContent, /160 logical segments; 20 bytes/);
    assert.equal(view.element('glass-preview').fills, 1);
    for (const [key, value] of Object.entries(current.input)) assert.equal(view.element(key).checked, value);
    if (action === 'close') panel.dispose(); else view.element(action).onclick();
    await view.flush(); assert.equal(setup.isOpen(), false); assert.equal(finished, 1);
    assert.equal(writes, action === 'apply' ? 1 : 0);
  }
});

test('failed Apply keeps segmented Configure usable for correction or Cancel', async () => {
  const h = harness(); let writes = 0, finished = 0;
  const setup = await openProjectSetup(h.vscode, h.context, {}, current, async () => {
    writes++; throw new Error('A field still uses that electrode');
  }, () => finished++);
  const view = media(h.panels[0]); await view.flush();
  view.element('apply').onclick(); await view.flush();
  assert.equal(writes, 1); assert.equal(setup.isOpen(), true); assert.equal(finished, 0);
  assert.equal(view.element('apply').disabled, false);
  assert.match(view.element('error').textContent, /electrode/);
  view.element('cancel').onclick(); await view.flush();
  assert.equal(setup.isOpen(), false); assert.equal(finished, 1);
});

test('invalid setup responses fail before creating an orphan Configure tab', async () => {
  const h = harness();
  for (const value of [undefined, {}, { ...current, displayKind: 'unknown' },
    { ...current, segmentGlass: undefined }, { ...current, input: {} }]) {
    await assert.rejects(openProjectSetup(h.vscode, h.context, {}, value, () => {}, () => {}), /Incomplete.*configuration/);
    assert.equal(h.panels.length, 0);
  }
});

test('Configure validates imported glass before replacing the preview and recovers from invalid input', async t => {
  const os = require('node:os');
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'lithe-configure-glass-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const file = path.join(directory, 'glass.json');
  const h = harness(); h.vscode.window.showOpenDialog = async () => [{ fsPath: file }];
  let validations = 0;
  const replacement = { ...current.segmentGlass, id: 'replacement' };
  await openProjectSetup(h.vscode, h.context, {}, current, () => {}, () => {}, async glass => {
    validations++; assert.deepEqual(glass, replacement); return replacement;
  });
  const panel = h.panels[0], view = media(panel); await view.flush();
  fs.writeFileSync(file, '{invalid');
  view.element('import').onclick(); await view.flush();
  assert.equal(validations, 0);
  assert.match(view.element('glass-summary').textContent, /tinyGeckoA03/);
  assert.notEqual(view.element('error').textContent, '');
  assert.equal(view.element('apply').disabled, false);
  fs.writeFileSync(file, JSON.stringify(replacement));
  view.element('import').onclick(); await view.flush();
  assert.equal(validations, 1);
  assert.match(view.element('glass-summary').textContent, /replacement/);
  assert.equal(view.element('error').textContent, '');
  assert.equal(view.element('glass-preview').fills, 2);
  panel.dispose();
});
