// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const vscode = require('vscode');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
async function run() {
  const root = vscode.workspace.workspaceFolders[0].uri.fsPath;
  const extension = vscode.extensions.getExtension('litheview.litheview-designer');
  const checks = [], output = path.join(root, 'vscode-validation.json');
  try {
    const api = await extension.activate();
    const uri = vscode.Uri.file(path.join(root, 'Tasks/UI/forms/home.litheview.xml'));
    const document = await vscode.workspace.openTextDocument(uri);
    const tasks = await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8');
    const frame = async target => {
      const doc = await vscode.workspace.openTextDocument(target);
      for (let n = 0; n < 200; n++) {
        const state = api.getDocumentState(target);
        if (state?.lastError) throw new Error(state.lastError);
        if (state?.lastRenderedVersion === doc.version && state.lastRenderStatus === 'Ok') return;
        await new Promise(resolve => setTimeout(resolve, 100));
      }
      throw new Error('No segmented preview frame: ' + JSON.stringify(api.getDocumentState(target)));
    };
    // Start the copied project with a blank form, then author through the plug-in.
    const blank = document.getText().replace(/\s*<bind\b[^>]*\/>/g, '');
    const edit = new vscode.WorkspaceEdit();
    edit.replace(uri, new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length)), blank);
    assert.equal(await vscode.workspace.applyEdit(edit), true);
    await document.save();
    await vscode.commands.executeCommand('vscode.openWith', uri, 'litheview.formDesigner');
    await frame(uri); checks.push('blank segmented form opens and native preview returns in real extension host');
    for (const [field, name, sample] of [['label', 'label', 'PLAY'], ['value', 'value', '0']]) {
      assert.equal(await api.editDocument(uri, document.version, { kind: 'segmentBinding', componentId: field, name, text: sample }), true);
      await frame(uri);
    }
    assert.equal(await api.editDocument(uri, document.version, { kind: 'segmentBinding', componentId: 'volumeRing', name: 'volumePercent', text: '75' }), true);
    await frame(uri);
    assert.match(document.getText(), /preview="75"/);
    await document.save();
    await vscode.commands.executeCommand('litheview.generateUi', uri);
    assert.match(await fs.readFile(path.join(root, 'Tasks/UI/Generated/home.hpp'), 'utf8'), /field_volumePercent/);
    checks.push('field edit, save and Generate UI succeed without a handler file');
    const volume = await api.addScreen(uri, 'volume');
    await vscode.commands.executeCommand('vscode.openWith', volume, 'litheview.formDesigner');
    await frame(volume);
    assert.equal((await api.refreshScreens(volume)).length, 2);
    checks.push('Add screen creates a second segmented form in the same project');
    await vscode.commands.executeCommand('vscode.openWith', uri, 'litheview.formDesigner');
    const setup = await api.projectSetup(uri);
    const glass = JSON.parse(setup.values.glass); glass.viewWidth++;
    setup.values.glass = JSON.stringify(glass); setup.values.input.pointer = true;
    assert.equal(await setup.submit(setup.values), true);
    await frame(uri);
    const manifest = JSON.parse(await fs.readFile(path.join(root, 'Tasks/UI/project.litheview.json'), 'utf8'));
    for (const form of manifest.forms) assert.ok((await fs.readFile(path.join(root, 'Tasks/UI', form), 'utf8')).includes(manifest.segmentGlass.sha256));
    checks.push('Configure updates glass and logical input, relocks both forms and refreshes preview');
    await vscode.commands.executeCommand('litheview.generateUi', uri);
    assert.ok(await fs.stat(path.join(root, 'Tasks/UI/Generated/volume.hpp')));
    assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), tasks);
    checks.push('multi-form generation leaves application build tasks untouched');
    await fs.writeFile(output, JSON.stringify({ passed: true, extensionPath: extension.extensionPath, checks }, null, 2));
  } catch (error) {
    await fs.writeFile(output, JSON.stringify({ passed: false, checks, error: error.stack }, null, 2));
    throw error;
  }
}
module.exports = { run };
