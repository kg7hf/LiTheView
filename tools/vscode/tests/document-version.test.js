// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const { applyVersionedEdit, isCurrent } = require('../lib/document-version');

function fixture() {
  const applied = [];
  const document = { uri: 'file:///form.litheview.xml', version: 3, isClosed: false, getText: () => '<old/>', positionAt: offset => offset };
  class WorkspaceEdit { replace(...args) { this.replacement = args; } }
  class Range { constructor(start, end) { this.start = start; this.end = end; } }
  return { applied, document, vscode: { WorkspaceEdit, Range, workspace: { applyEdit: async edit => { applied.push(edit); return true; } } } };
}

test('service edit becomes one VS Code workspace edit with source version', async () => {
  const f = fixture();
  assert.equal(await applyVersionedEdit(f.vscode, f.document, 3, { documentVersion: 3, xml: '<new/>' }), true);
  assert.equal(f.applied.length, 1);
  assert.equal(f.applied[0].replacement[2], '<new/>');
});

test('typing while service works discards stale XML without modifying document', async () => {
  const f = fixture(); f.document.version = 4;
  assert.equal(await applyVersionedEdit(f.vscode, f.document, 3, { documentVersion: 3, xml: '<stale/>' }), false);
  assert.equal(f.applied.length, 0);
});

test('mismatched service version, closed document and invalid XML result are rejected', async () => {
  const f = fixture();
  assert.equal(isCurrent(f.document, 3, { documentVersion: 2 }), false);
  assert.equal(await applyVersionedEdit(f.vscode, f.document, 3, { documentVersion: 3 }), false);
  f.document.isClosed = true;
  assert.equal(isCurrent(f.document, 3, { documentVersion: 3 }), false);
  assert.equal(f.applied.length, 0);
});

test('unchanged XML does not insert a needless undo entry', async () => {
  const f = fixture();
  assert.equal(await applyVersionedEdit(f.vscode, f.document, 3, { documentVersion: 3, xml: '<old/>' }), true);
  assert.equal(f.applied.length, 0);
});
