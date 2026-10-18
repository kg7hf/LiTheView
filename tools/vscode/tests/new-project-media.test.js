// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('wizard switches to fixed glass, previews its fields and submits custom input without pixel settings', () => {
  const messages = [], listeners = {}, nodes = new Map();
  const node = id => {
    if (!nodes.has(id)) nodes.set(id, { value: '', hidden: false, disabled: false, checked: false, options: [], handlers: {},
      addEventListener(kind, action) { this.handlers[kind] = action; }, replaceChildren() { this.options = []; },
      append(child) { this.options.push(child); }, getContext() { return { resetTransform() {}, clearRect() {}, scale() {}, beginPath() {}, lineTo() {}, moveTo() {}, closePath() {}, fill() {} }; } });
    return nodes.get(id);
  };
  const names = ['name', 'projectDirectory', 'componentDirectory', 'sourceReference', 'width', 'height', 'logicalInput', 'pointer', 'keys', 'text', 'encoder', 'select'];
  const form = node('project'); form.elements = Object.fromEntries(names.map(n => [n, node(n)]));
  form.elements.displayKind = node('display-kind'); form.elements.surfaceId = node('surface');
  node('format').value = 'rgb565'; node('display-kind').value = 'pixel';
  const customInput = node('custom-input-option'); node('logicalInput').querySelector = () => customInput;
  node('logicalInput').value = 'outputOnly';
  const customFields = ['width', 'height', 'sourceReference'].map(n => { node(n).name = n; return node(n); });
  const document = { getElementById: node, createElement: () => ({ value: '', textContent: '' }),
    querySelectorAll: selector => selector.startsWith('#custom') ? customFields : [] };
  const context = { document, window: { addEventListener: (event, fn) => { listeners[event] = fn; } },
    acquireVsCodeApi: () => ({ postMessage: message => messages.push(message) }),
    FormData: class { constructor() { return new Map(Object.entries(form.elements).filter(([, n]) => !n.disabled).map(([k, n]) => [k, n.value])); } } };
  vm.createContext(context);
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../media/glass-preview.js'), 'utf8'), context);
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../media/new-project.js'), 'utf8'), context);
  listeners.message({ data: { type: 'initialize', surfaces: [], applicationRoot: '/app', libraryRoot: '/app/external/LiTheView' } });
  assert.equal(node('custom').hidden, false);
  node('display-kind').value = 'segmented'; node('display-kind').handlers.change();
  assert.equal(node('pixel-display').hidden, true);
  assert.equal(node('width').disabled, true);
  assert.equal(customInput.hidden, false);
  node('glass-import').handlers.click(); assert.equal(messages.at(-1).type, 'importGlass');
  const glass = { id: '<not-markup>', segmentCount: 3, viewWidth: 10, viewHeight: 10,
    shapes: [{ id: 0, polygon: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }] }],
    texts: [], numbers: [], icons: [{ id: 'status', segments: [0] }], groups: [] };
  listeners.message({ data: { type: 'glass', glass } });
  assert.equal(node('glass-preview').hidden, false);
  assert.match(node('glass-summary').textContent, /<not-markup>: 10 × 10 layout; 3 logical segments/);
  assert.match(node('glass-fields').textContent, /status: icon/);
  node('logicalInput').value = 'custom'; node('logicalInput').handlers.change();
  node('keys').checked = true; node('encoder').checked = true; node('select').checked = true;
  form.handlers.submit({ preventDefault() {} });
  const submitted = JSON.parse(JSON.stringify(messages.at(-1)));
  assert.equal(submitted.type, 'create'); assert.equal(submitted.values.displayKind, 'segmented');
  assert.equal(submitted.values.surfaceId, undefined);
  assert.deepEqual(JSON.parse(submitted.values.glass), glass);
  assert.deepEqual(submitted.values.input, { pointer: false, keys: true, text: false, encoder: true, select: true });
  node('display-kind').value = 'pixel'; node('display-kind').handlers.change();
  assert.equal(node('logicalInput').value, 'outputOnly'); assert.equal(node('width').disabled, false);
});
