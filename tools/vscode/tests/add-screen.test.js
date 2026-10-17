// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const { applyScreenPlan } = require('../lib/add-screen');
for (const scenario of ['clean', 'bom', 'concurrent edit', 'concurrent save']) {
  test(`Add screen protects the source transaction: ${scenario}`, async t => {
    const root = await fs.mkdtemp(path.join(os.tmpdir(), 'litheview-add-screen-'));
    t.after(() => fs.rm(root, { recursive: true, force: true }));
    const directory = path.join(root, 'UI');
    await fs.mkdir(path.join(directory, 'forms'), { recursive: true });
    const manifestPath = path.join(directory, 'project.litheview.json');
    const formPath = path.join(directory, 'forms/Settings.litheview.xml');
    const before = JSON.stringify({ displayName: 'Original', forms: ['forms/Home.litheview.xml'] });
    const after = JSON.stringify({ displayName: 'Original', forms: ['forms/Home.litheview.xml', 'forms/Settings.litheview.xml'] });
    await fs.writeFile(manifestPath, (scenario === 'bom' ? '\uFEFF' : '') + before);
    const documents = new Map();
    function document(uri, text) {
      const result = { uri, text, version: 1, isDirty: false, getText() { return this.text; }, positionAt(offset) { return offset; },
        async save() { await fs.writeFile(uri.fsPath, this.text); this.isDirty = false; return true; } };
      documents.set(uri.fsPath, result); return result;
    }
    const manifest = document({ fsPath: manifestPath }, before);
    let applied = false;
    const vscode = {
      Uri: { file: fsPath => ({ fsPath }) }, Position: class {}, Range: class {},
      WorkspaceEdit: class {
        constructor() { this.operations = []; }
        createFile(uri) { this.operations.push(['create', uri]); }
        insert(uri, position, text) { this.operations.push(['insert', uri, text]); }
        replace(uri, range, text) { this.operations.push(['replace', uri, text]); }
      },
      workspace: {
        async openTextDocument(uri) { return documents.get(uri.fsPath) || document(uri, ''); },
        async applyEdit(edit) {
          applied = true;
          for (const [operation, uri, text] of edit.operations) {
            if (operation === 'create') document(uri, '');
            else { documents.get(uri.fsPath).text = text; documents.get(uri.fsPath).isDirty = true; }
          }
          return true;
        }
      }
    };
    const read = fs.readFile;
    t.after(() => { fs.readFile = read; });
    // Deliver an editor event while the real asynchronous disk comparison is pending.
    fs.readFile = async function(file, ...args) {
      const value = await read.call(this, file, ...args);
      if (file === manifestPath && scenario.startsWith('concurrent')) {
        manifest.text = before.replace('Original', 'Developer edit');
        manifest.version++;
        manifest.isDirty = scenario === 'concurrent edit';
      }
      return value;
    };
    const operation = applyScreenPlan(vscode, { applicationRoot: root, manifestPath }, { manifestPath, formPath, before, after, xml: '<panel/>' });
    if (scenario.startsWith('concurrent')) {
      await assert.rejects(operation, /project changed/i);
      assert.equal(applied, false);
      assert.match(manifest.text, /Developer edit/);
      await assert.rejects(fs.access(formPath), { code: 'ENOENT' });
    } else {
      await operation;
      assert.equal(applied, true);
      assert.equal(await read(manifestPath, 'utf8'), after);
      assert.equal(await read(formPath, 'utf8'), '<panel/>');
    }
  });
}
