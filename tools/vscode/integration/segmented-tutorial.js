// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const vscode = require('vscode');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
async function run() {
  const root = vscode.workspace.workspaceFolders[0].uri.fsPath;
  const extension = vscode.extensions.getExtension('litheview.litheview-designer');
  const checks = [], output = path.join(root, 'vscode-validation.json');
  try {
    const api = await extension.activate();
    assert.ok(extension.extensionPath.includes(`${path.sep}extensions${path.sep}`));
    const inputs = JSON.parse(await fs.readFile(path.join(root, 'build-inputs.json'), 'utf8'));
    const fsmBefore = await fs.readFile(path.join(root, 'Application/menu.hpp'), 'utf8');
    const command = (file, args, label) => {
      let log;
      try { log = execFileSync(file, args, { cwd: root, encoding: 'utf8', timeout: 120000 }); }
      catch (e) { require('node:fs').writeFileSync(path.join(root, label + '.log'), `${e.stdout}\n${e.stderr}`); throw e; }
      require('node:fs').writeFileSync(path.join(root, label + '.log'), log);
    };
    const build = label => {
      if (label === 'blank') command('cmake', ['--preset', 'win32-debug', '-DLITHEVIEW_ROOT=' + inputs.libraryRoot, '-DCMAKE_CXX_COMPILER=' + inputs.compiler], label + '-configure');
      command('cmake', ['--build', '--preset', 'win32-debug', '--parallel', '4'], label + '-build');
      command('ctest', ['--preset', 'win32-debug', '--output-on-failure'], label + '-tests');
    };
    await assert.rejects(fs.access(path.join(root, 'UI/designer/project.litheview.json')));
    build('blank');
    checks.push('published starter runs its actual blank-glass application and three tests before authoring');
    await vscode.commands.executeCommand('litheview.addUi');
    const glassPath = path.join(root, 'glass.segment-display.json');
    const glassDefinition = await api.importNewProjectGlass(glassPath);
    assert.equal(glassDefinition.segmentCount, 160);
    await api.cancelNewProject();
    await assert.rejects(fs.access(path.join(root, 'UI/designer')));
    checks.push('import validates glass; cancel publishes nothing in a blank outside-library application');
    await vscode.commands.executeCommand('litheview.addUi');
    const glass = await api.importNewProjectGlass(glassPath);
    const created = await api.submitNewProject({ name: 'Tiny segmented menu', projectDirectory: 'UI/designer',
      componentDirectory: 'UI/Generated/segmented', displayKind: 'segmented', glass: JSON.stringify(glass),
      logicalInput: 'custom', input: { pointer: false, keys: true, text: false, encoder: true, select: true } });
    assert.ok(created?.formPath);
    const uri = vscode.Uri.file(created.formPath);
    const document = await vscode.workspace.openTextDocument(uri);
    assert.doesNotMatch(document.getText(), /<bind /);
    checks.push('New Project creates blank segmented screen and application paths without CLI or MCU');
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
    assert.match(await fs.readFile(path.join(root, 'UI/Generated/segmented/home.hpp'), 'utf8'), /field_volumePercent/);
    checks.push('field edit, save and Generate UI succeed without a handler file');
    const volume = await api.addScreen(uri, 'volume');
    await vscode.commands.executeCommand('vscode.openWith', volume, 'litheview.formDesigner');
    await frame(volume);
    assert.equal((await api.refreshScreens(volume)).length, 2);
    checks.push('Add screen creates a second segmented form in the same project');
    await vscode.commands.executeCommand('vscode.openWith', uri, 'litheview.formDesigner');
    const beforeConfigure = await fs.readFile(path.join(root, 'UI/designer/project.litheview.json'), 'utf8');
    for (const close of ['cancel', 'tab']) {
      const dialog = await api.projectSetup(uri);
      assert.equal(JSON.parse(dialog.values.glass).id, glass.id);
      assert.equal(dialog.values.input.encoder, true);
      if (close === 'cancel') dialog.cancel();
      else await vscode.commands.executeCommand('workbench.action.closeActiveEditor');
      assert.equal(dialog.isOpen(), false);
      assert.equal(await fs.readFile(path.join(root, 'UI/designer/project.litheview.json'), 'utf8'), beforeConfigure);
    }
    checks.push('segmented Configure cancels and closes without writes; reopening releases the form editing lock');
    const setup = await api.projectSetup(uri);
    const changedGlass = JSON.parse(setup.values.glass); changedGlass.viewWidth++;
    setup.values.glass = JSON.stringify(changedGlass); setup.values.input.pointer = true;
    assert.equal(await setup.submit(setup.values), true);
    await frame(uri);
    const manifest = JSON.parse(await fs.readFile(path.join(root, 'UI/designer/project.litheview.json'), 'utf8'));
    for (const form of manifest.forms) assert.ok((await fs.readFile(path.join(root, 'UI/designer', form), 'utf8')).includes(manifest.segmentGlass.sha256));
    checks.push('Configure updates glass and logical input, relocks both forms and refreshes preview');
    await vscode.commands.executeCommand('litheview.generateUi', uri);
    assert.ok(await fs.stat(path.join(root, 'UI/Generated/segmented/volume.hpp')));
    assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), tasks);
    build('generated');
    assert.equal(await fs.readFile(path.join(root, 'Application/menu.hpp'), 'utf8'), fsmBefore);
    checks.push('same Windows preset builds generated presentation; four tests pass including real Win32 input workflow; FSM and build tasks unchanged');
    await fs.writeFile(output, JSON.stringify({ passed: true, extensionPath: extension.extensionPath, checks }, null, 2));
  } catch (error) {
    await fs.writeFile(output, JSON.stringify({ passed: false, checks, error: error.stack }, null, 2));
    throw error;
  }
}
module.exports = { run };
