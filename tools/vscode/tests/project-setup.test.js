// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { applySetupPlan, assertSetupSaved, setupValues, setupRequest } = require('../lib/project-setup');

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'lithe-setup-'));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const project = { applicationRoot: root, manifestPath: path.join(root, 'project.litheview.json') };
  const paths = ['logical-input.json', 'display-definition.json', 'display-surface.json'].map(name => path.join(root, 'profiles', name)).concat(project.manifestPath);
  await fs.mkdir(path.dirname(paths[0]));
  const documents = paths.map(file => ({ uri: { fsPath: file }, version: 1, isDirty: false, text: '{}\r\n',
    getText() { return this.text; }, positionAt(offset) { return offset; },
    async save() { await fs.writeFile(file, this.text); this.isDirty = false; return true; } }));
  for (const document of documents) await document.save();
  let applied = 0;
  const vscode = { Uri: { file: fsPath => ({ fsPath }) }, Range: class {},
    WorkspaceEdit: class { edits = []; replace(uri, range, text) { this.edits.push({ uri, text }); } },
    workspace: { textDocuments: documents,
      async openTextDocument(uri) { return documents.find(item => item.uri.fsPath === uri.fsPath); },
      async applyEdit(edit) { applied++; for (const item of edit.edits) { const document = documents.find(doc => doc.uri.fsPath === item.uri.fsPath); document.text = item.text; document.version++; document.isDirty = true; } return true; }
    } };
  const plan = { documentVersion: 1, displaySurface: {}, documents: paths.map(file => ({ path: file, before: '{}\n' })),
    edits: [paths[0], paths[3]].map(file => ({ path: file, before: '{}\n', after: '{"updated":true}' })) };
  return { vscode, project, plan, documents, applied: () => applied };
}

test('setup reads all configuration preconditions, edits only changed files and saves manifest last', async t => {
  const f = await fixture(t);
  assert.equal(await applySetupPlan(f.vscode, f.project, { ...f.plan, edits: [] }, () => true), false);
  assert.equal(await applySetupPlan(f.vscode, f.project, f.plan, () => true), true);
  assert.equal(f.applied(), 1);
  assert.equal(await fs.readFile(f.documents[0].uri.fsPath, 'utf8'), '{"updated":true}');
  assert.equal(await fs.readFile(f.documents[1].uri.fsPath, 'utf8'), '{}\r\n');
  assert.equal(await fs.readFile(f.documents[3].uri.fsPath, 'utf8'), '{"updated":true}');
});

test('dirty or externally changed untouched profiles also block setup and generation', async t => {
  const f = await fixture(t);
  f.documents[1].isDirty = true;
  await assert.rejects(assertSetupSaved(f.vscode, f.project), /Save the project/);
  await assert.rejects(applySetupPlan(f.vscode, f.project, f.plan, () => true), /changed/);
  f.documents[1].isDirty = false;
  await fs.writeFile(f.documents[1].uri.fsPath, 'external edit');
  await assert.rejects(applySetupPlan(f.vscode, f.project, f.plan, () => true), /changed/);
  await f.documents[1].save();
  await assert.rejects(applySetupPlan(f.vscode, f.project, f.plan, () => false), /changed/);
  assert.equal(f.applied(), 0);
});

test('unexpected paths, duplicate writes, incomplete reads and missing manifest update cannot be applied', async t => {
  const f = await fixture(t);
  for (const change of [{ documents: f.plan.documents.slice(1) }, { edits: [f.plan.edits[0]] },
    { edits: [f.plan.edits[0], f.plan.edits[0], f.plan.edits[1]] },
    { edits: [{ ...f.plan.edits[0], path: path.join(f.project.applicationRoot, 'form_handlers.hpp') }, f.plan.edits[1]] }]) {
    await assert.rejects(applySetupPlan(f.vscode, f.project, { ...f.plan, ...change }, () => true), /Invalid/);
  }
  assert.equal(f.applied(), 0);
});

test('failed manifest save leaves a recoverable dirty document and blocks generation', async t => {
  const f = await fixture(t);
  f.documents[3].save = async () => false;
  await assert.rejects(applySetupPlan(f.vscode, f.project, f.plan, () => true), /not fully saved/);
  await assert.rejects(assertSetupSaved(f.vscode, f.project), /Save the project/);
});

test('a stale front-view read or escaped destination prevents every configuration write', async t => {
  const f = await fixture(t);
  const file = f.project.manifestPath.replace(/\.json$/, '.front-panel.json');
  await fs.writeFile(file, 'external front-view edit');
  await assert.rejects(applySetupPlan(f.vscode, f.project, f.plan, () => true,
    { path: file, before: null, after: '{}' }), /Front-view settings changed/);
  await assert.rejects(applySetupPlan(f.vscode, f.project, f.plan, () => true,
    { path: path.join(f.project.applicationRoot, 'unrelated.json'), before: null, after: '{}' }), /Invalid front-view edit/);
  assert.equal(f.applied(), 0);
});

test('UTF-8 source BOM is compatible with the service and VS Code text view', async t => {
  const f = await fixture(t);
  await fs.writeFile(f.documents[1].uri.fsPath, '\uFEFF{}\r\n');
  assert.equal(await applySetupPlan(f.vscode, f.project, f.plan, () => true), true);
});

test('setup prepopulates native units and preserves arbitrary input combinations without choosing a preset', () => {
  const current = { snapshot: 'current', input: { pointer: true, keys: false, text: true, encoder: true, select: false },
    displayDefinition: { format: 'rrrr2222', geometry: { width: 256, height: 192, shape: 'rectangle',
      safeArea: { x: 10, y: 10, width: 284, height: 236 } }, source: { reference: 'My panel specification' } } };
  const values = setupValues(current);
  assert.equal(values.custom.width, 256);
  assert.equal(values.custom.safeArea, '10,10,284,236');
  assert.deepEqual(values.input, current.input);
  const request = setupRequest(values, current);
  assert.equal(request.snapshot, 'current');
  assert.equal(request.customNativeDisplay.width, 256);
  assert.equal(values.logicalInput, 'custom');
  assert.deepEqual(request.input, current.input);
  assert.throws(() => setupRequest({ ...values, input: { pointer: true } }, current), /capabilities/);
  assert.throws(() => setupRequest({ ...values, custom: { ...values.custom, format: 'rgb565' } }, current), /packed/);
});
