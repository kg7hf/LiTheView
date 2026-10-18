// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { segmentSetupRequest, renderSegmentSetup } = require('../lib/segment-setup');
const { applySetupPlan, assertSetupSaved } = require('../lib/project-setup');

test('segmented Configure transmits a definition and capabilities without raster placeholders', () => {
  const input = { pointer: false, keys: true, text: false, encoder: true, select: true };
  assert.deepEqual(segmentSetupRequest({ snapshot: 'lock' }, { glass: '{"id":"customGlass"}', input }),
    { snapshot: 'lock', segmentGlass: { id: 'customGlass' }, input });
  assert.throws(() => segmentSetupRequest({}, { glass: '{}', input: {} }), /capabilities/);
  assert.throws(() => segmentSetupRequest({}, { glass: 'invalid', input }), SyntaxError);
  assert.throws(() => segmentSetupRequest({}, { glass: 'x'.repeat(1024 * 1024 + 1), input }), /1 MiB/);
  const html = renderSegmentSetup(name => name, 'vscode-resource:', 'nonce');
  assert.match(html, /default-src 'none'/);
  assert.match(html, /script-src 'nonce-nonce'/);
  assert.ok(!html.includes('onclick='));
});

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'lithe-segment-setup-'));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const manifest = { schemaVersion: 4, forms: ['forms/home.litheview.xml', 'forms/volume.litheview.xml'] };
  const project = { applicationRoot: root, manifestPath: path.join(root, 'project.litheview.json') };
  const files = ['profiles/logical-input.json', 'profiles/glass.segment-display.json', ...manifest.forms, 'project.litheview.json'];
  const docs = files.map(relative => ({ uri: { fsPath: path.join(root, relative) }, version: 1, isDirty: false,
    text: relative === 'project.litheview.json' ? JSON.stringify(manifest) : 'before', getText() { return this.text; }, positionAt(i) { return i; },
    async save() { await fs.writeFile(this.uri.fsPath, this.text); this.isDirty = false; return true; } }));
  for (const doc of docs) { await fs.mkdir(path.dirname(doc.uri.fsPath), { recursive: true }); await doc.save(); }
  let writes = 0;
  const vscode = { Uri: { file: fsPath => ({ fsPath }) }, Range: class {},
    WorkspaceEdit: class { edits = []; replace(uri, range, text) { this.edits.push({ uri, text }); } },
    workspace: { textDocuments: docs, async openTextDocument(uri) { return docs.find(d => d.uri.fsPath === uri.fsPath); },
      async applyEdit(edit) { writes++; for (const entry of edit.edits) { const doc = docs.find(d => d.uri.fsPath === entry.uri.fsPath); doc.text = entry.text; doc.version++; doc.isDirty = true; } return true; } } };
  const plan = { displayKind: 'segmented', documents: docs.map(d => ({ path: d.uri.fsPath, before: d.text })),
    edits: [docs[1], docs[2], docs[3], docs[4]].map(d => ({ path: d.uri.fsPath, before: d.text, after: 'updated' })) };
  return { project, docs, vscode, plan, writes: () => writes };
}

test('segmented Configure treats every form as a precondition and applies one WorkspaceEdit', async t => {
  const f = await fixture(t);
  f.docs[3].isDirty = true;
  await assert.rejects(applySetupPlan(f.vscode, f.project, f.plan, () => true), /changed/);
  assert.equal(f.writes(), 0);
  f.docs[3].isDirty = false;
  await assert.rejects(applySetupPlan(f.vscode, f.project, { ...f.plan, documents: f.plan.documents.slice(1) }, () => true), /Invalid/);
  await assert.rejects(applySetupPlan(f.vscode, f.project, { ...f.plan, edits: [{ ...f.plan.edits[0], path: path.join(f.project.applicationRoot, 'Application/fsm.hpp') }, f.plan.edits.at(-1)] }, () => true), /Invalid/);
  assert.equal(await applySetupPlan(f.vscode, f.project, f.plan, () => true), true);
  assert.equal(f.writes(), 1);
});

test('dirty segmented glass is rejected before Configure or generation', async t => {
  const f = await fixture(t); f.docs[1].isDirty = true;
  await assert.rejects(assertSetupSaved(f.vscode, f.project), /Save the project/);
});
