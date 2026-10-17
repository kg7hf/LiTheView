// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const vscode = require('vscode');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');

const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(predicate, description, timeout = 30000, describeFailure) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    if (await predicate()) return;
    await delay(100);
  }
  throw new Error(`Timed out: ${description}${describeFailure ? `; ${describeFailure()}` : ''}`);
}

async function run() {
  const root = vscode.workspace.workspaceFolders[0].uri.fsPath;
  const output = path.join(root, 'vscode-validation.json');
  const checks = [];
  const taskEvents = [];
  const taskSubscription = vscode.tasks.onDidEndTaskProcess(event => taskEvents.push({ name: event.execution.task.name, exitCode: event.exitCode }));
  const record = name => checks.push(name);
  try {
    const extension = vscode.extensions.getExtension('litheview.litheview-designer');
    assert.ok(extension, 'extension must be installed in this test host');
    let packaged;
    try { packaged = JSON.parse((await fs.readFile(path.join(root, 'packaged-test.json'), 'utf8')).replace(/^\uFEFF/, '')); }
    catch (error) { if (error.code !== 'ENOENT') throw error; }
    if (packaged) {
      const settings = vscode.workspace.getConfiguration('litheview', vscode.workspace.workspaceFolders[0].uri);
      assert.equal(settings.get('servicePath'), '');
      assert.equal(settings.get('previewWorkerPath'), '');
      assert.ok(extension.extensionPath.toLowerCase().includes(`${path.sep}extensions${path.sep}`));
      for (const entry of (process.env.PATH || '').split(path.delimiter).filter(Boolean)) {
        for (const dll of ['libstdc++-6.dll', 'libgcc_s_seh-1.dll', 'libwinpthread-1.dll']) {
          await assert.rejects(fs.access(path.join(entry, dll)), { code: 'ENOENT' });
        }
      }
      if (packaged.sourceOnly) {
        await assert.rejects(fs.access(path.join(extension.extensionPath, 'runtime')), { code: 'ENOENT' });
        await fs.access(path.join(packaged.libraryRoot, 'build/vscode-designer/service/LitheView.Designer.Service.dll'));
        await fs.access(path.join(packaged.libraryRoot, 'build/windows-mingw-debug/litheview_designer_preview_worker.exe'));
        for (const dll of ['libstdc++-6.dll', 'libgcc_s_seh-1.dll', 'libwinpthread-1.dll']) {
          await fs.access(path.join(packaged.libraryRoot, 'build/windows-mingw-debug', dll));
        }
        record('Installed source-only VSIX uses locally prepared tools without overrides or MinGW on PATH');
      } else {
        await fs.access(path.join(extension.extensionPath, 'runtime/service/LitheView.Designer.Service.dll'));
        await fs.access(path.join(extension.extensionPath, 'runtime/native/litheview_designer_preview_worker.exe'));
        await assert.rejects(fs.access(path.join(packaged.libraryRoot, 'build')), { code: 'ENOENT' });
        for (const entry of (process.env.PATH || '').split(path.delimiter).filter(Boolean)) {
          await assert.rejects(fs.access(path.join(entry, 'libstdc++-6.dll')), { code: 'ENOENT' });
        }
        record('Installed VSIX uses bundled runtime without library builds or MinGW on PATH');
      }
    }
    const api = await extension.activate();
    for (const command of ['litheview.runUiStarter', 'litheview.runSimulator', 'litheview.buildFirmware', 'litheview.activeProjectManifest']) {
      assert.equal((await vscode.commands.getCommands()).includes(command), false);
    }
    const uri = vscode.Uri.file(path.join(root, 'Tasks/UI/forms/main.litheview.xml'));
    // Reproduce opening a saved file with mixed endings, then immediately clicking Generate.
    // A clean VS Code document normalizes its EOLs without rewriting the on-disk source.
    const sourceLf = (await fs.readFile(uri.fsPath, 'utf8')).replace(/\r\n|\r/g, '\n');
    const mixedEndings = sourceLf.replace('</panel>\n', '</panel>\r\n');
    assert.notEqual(mixedEndings, sourceLf, 'fixture needs a final CRLF among LF lines');
    await fs.writeFile(uri.fsPath, mixedEndings);
    const document = await vscode.workspace.openTextDocument(uri);
    const original = document.getText();
    const waitForFrame = description => until(() => {
      const state = api.getDocumentState(uri);
      if (state?.lastError) throw new Error(`${description}: ${state.lastError}`);
      return state?.lastRenderedVersion === document.version && state?.lastRenderStatus === 'Ok';
    }, description, 30000, () => JSON.stringify(api.getDocumentState(uri)));
    await vscode.commands.executeCommand('vscode.openWith', uri, 'litheview.formDesigner');
    await waitForFrame('native frame in the real custom editor');
    // Add screen shares one manifest/display; it must not scaffold another task/project.
    const settingsUriAdded = await api.addScreen(uri, 'Settings');
    await until(() => api.getDocumentState(settingsUriAdded)?.lastRenderStatus === 'Ok', 'added screen native preview', 30000);
    await assert.rejects(() => api.addScreen(settingsUriAdded, 'settings'), /exists|conflict/i);
    const sharedManifest = JSON.parse(await fs.readFile(path.join(root, 'Tasks/UI/project.litheview.json'), 'utf8'));
    assert.deepEqual([...sharedManifest.forms].sort(), ['forms/Settings.litheview.xml', 'forms/main.litheview.xml']);
    const settingsDocument = await vscode.workspace.openTextDocument(settingsUriAdded);
    assert.equal(await api.editDocument(settingsUriAdded, settingsDocument.version,
      { kind: 'add', parentId: 'component.settings.root', widgetId: 'lithe.widget.button', frame: { x: 100, y: 100, width: 80, height: 24 } }), true);
    const settingsButton = [...settingsDocument.getText().matchAll(/litheview-component id="([^"]+)"/g)].map(item => item[1]).find(id => id !== 'component.settings.root');
    assert.ok(settingsButton);
    await settingsDocument.save();
    await api.openHandler(settingsUriAdded, settingsButton, settingsDocument.version);
    const settingsHandler = await vscode.workspace.openTextDocument(vscode.Uri.file(path.join(root, 'Tasks/UI/handlers/Settings.hpp')));
    assert.match(settingsHandler.getText(), /class SettingsForm : public lithe::generated::Settings::Instance<>/);
    await settingsHandler.save();
    await vscode.commands.executeCommand('litheview.generateUi', settingsUriAdded);
    await until(async () => {
      try { await fs.access(path.join(root, 'Tasks/UI/Generated/Settings/form.hpp')); return true; }
      catch { return false; }
    }, 'Generate added screen in its own directory');
    assert.equal(await fs.access(path.join(root, 'Tasks/Settings')).then(() => true, () => false), false);
    await api.openScreen(settingsUriAdded, 'Tasks/UI/forms/main.litheview.xml');
    const screens = await api.refreshScreens(uri);
    assert.equal(screens.length, 2);
    const settingsScreen = screens.find(screen => screen.label === 'Settings');
    assert.ok(settingsScreen);
    const pending = new vscode.WorkspaceEdit();
    pending.insert(uri, document.positionAt(document.getText().length), '\n<!-- unsaved navigator check -->');
    await vscode.workspace.applyEdit(pending);
    assert.equal(document.isDirty, true);
    await api.openScreen(uri, settingsScreen.id);
    const settingsUri = vscode.Uri.file(settingsScreen.formPath);
    await until(() => api.getDocumentState(settingsUri)?.lastRenderStatus === 'Ok', 'second screen native preview', 30000);
    assert.equal(document.isDirty, true);
    assert.ok(document.getText().includes('unsaved navigator check'));
    await api.openScreen(settingsUri, screens.find(screen => screen.id === 'Tasks/UI/forms/main.litheview.xml').id);
    await vscode.commands.executeCommand('undo');
    assert.equal(document.getText(), original);
    await waitForFrame('original screen after navigation and undo');
    record('Add screen updates one UI project, generates independent output and preserves other screens and undo');

    record('Custom editor opens from application root and renders native pixels');

    assert.equal(document.isDirty, false);
    assert.notEqual(document.getText(), mixedEndings, 'editor should normalize mixed line endings');
    await vscode.commands.executeCommand('litheview.generateUi', uri);
    await until(async () => {
      try { await fs.access(path.join(root, 'Tasks/UI/Generated/form.hpp')); return true; }
      catch (error) { if (error.code === 'ENOENT') return false; throw error; }
    }, 'Generate on an unchanged, saved form with mixed line endings');
    assert.equal(document.isDirty, false);
    assert.equal(await fs.readFile(uri.fsPath, 'utf8'), mixedEndings);
    record('Generate accepts unchanged editor-normalized line endings without rewriting the form');

    const componentIds = xml => [...xml.matchAll(/litheview-component id="([^"]+)"/g)].map(match => match[1]);
    const beforeIds = new Set(componentIds(document.getText()));
    const rootComponent = /litheview-component id="([^"]+)" name="rootPanel"/.exec(original)?.[1];
    assert.ok(rootComponent, 'tutorial root component exists');
    assert.equal(await api.editDocument(uri, document.version,
      { kind: 'add', parentId: rootComponent, widgetId: 'lithe.widget.button', frame: { x: 100, y: 100, width: 80, height: 24 } }), true);
    const added = componentIds(document.getText()).find(id => !beforeIds.has(id));
    assert.ok(added, 'palette add must create a component');
    assert.match(document.getText(), /frame="100,100,80,24"/);
    await waitForFrame('native frame after adding a button');
    assert.equal(await api.editDocument(uri, document.version,
      { kind: 'frame', componentId: added, frame: { x: 105, y: 110, width: 82, height: 25 } }), true);
    assert.match(document.getText(), /frame="105,110,82,25"/);
    await waitForFrame('native frame after moving and resizing a button');
    assert.equal(await api.editDocument(uri, document.version, { kind: 'delete', componentId: added }), true);
    assert.ok(!componentIds(document.getText()).includes(added));
    await waitForFrame('native frame after deleting a button');
    record('Palette add, move, resize and delete update the document and native frame');

    // The same checked edit path is invoked by the webview's property editor.
    const component = /litheview-component id="([^"]+)" name="activateButton"/.exec(original)?.[1];
    assert.ok(component, 'tutorial activate component exists');
    const beforeVersion = document.version;
    assert.equal(await api.editDocument(uri, beforeVersion,
      { kind: 'text', componentId: component, text: 'VS CODE EDIT' }), true);
    await until(() => document.getText().includes('VS CODE EDIT'), 'visual edit to text document');
    assert.ok(document.isDirty);
    await document.save();
    assert.match(await fs.readFile(uri.fsPath, 'utf8'), /VS CODE EDIT/);
    record('Visual edit updates VS Code dirty state and normal save');
    assert.equal(await api.editDocument(uri, beforeVersion,
      { kind: 'text', componentId: component, text: 'STALE EDIT' }), false);
    assert.ok(!document.getText().includes('STALE EDIT'));
    record('Stale visual edits cannot overwrite a newer document');

    await vscode.commands.executeCommand('vscode.openWith', uri, 'default');
    await vscode.commands.executeCommand('undo');
    await until(() => !document.getText().includes('VS CODE EDIT'), 'normal VS Code undo');
    record('Visual edit participates in VS Code undo after source reopen');
    await document.save();

    // Editing-parity operations from the inspector, theme panel, and multi-selection use the
    // same checked edit path; the fixture is restored afterwards for the remaining checks.
    const beforeParity = document.getText();
    const parityEdit = async operation => assert.equal(await api.editDocument(uri, document.version, operation), true,
      `editing-parity operation ${operation.kind} must apply`);
    await parityEdit({ kind: 'rename', componentId: component, name: 'goButton' });
    assert.match(document.getText(), /name="goButton"/);
    const componentCount = () => componentIds(document.getText()).length;
    const beforeDuplicate = componentCount();
    await parityEdit({ kind: 'duplicate', componentIds: [component] });
    assert.equal(componentCount(), beforeDuplicate + 1);
    await parityEdit({ kind: 'order', componentId: component, order: 'bringToFront' });
    await parityEdit({ kind: 'themeColor', tokenId: 'theme.integration', color: '#000088ff' });
    assert.match(document.getText(), /id="theme\.integration"/);
    await parityEdit({ kind: 'action', componentId: component, widgetActionId: 'activate',
      newAction: { id: 'action.integration', runtimeId: 900, symbol: 'Integration' } });
    assert.match(document.getText(), /id="action\.integration"/);
    await waitForFrame('native frame after editing-parity operations');
    const restoreParity = new vscode.WorkspaceEdit();
    restoreParity.replace(uri, new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length)), beforeParity);
    assert.equal(await vscode.workspace.applyEdit(restoreParity), true);
    await document.save();
    await waitForFrame('native frame after restoring the fixture');
    record('Rename, duplicate, order, theme token and action edits apply through the checked path');

    const fullRange = () => new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length));
    const sourceEdit = new vscode.WorkspaceEdit();
    sourceEdit.replace(uri, fullRange(), document.getText().replace('text="ACTIVATE"', 'text="SOURCE EDIT"'));
    assert.equal(await vscode.workspace.applyEdit(sourceEdit), true);
    assert.match(document.getText(), /SOURCE EDIT/, 'source edit must actually change the activate button');
    await document.save();
    await vscode.commands.executeCommand('vscode.openWith', uri, 'litheview.formDesigner');
    await waitForFrame('source edit reflected in native visual editor');
    record('Source changes and visual editor share the same document');

    const valid = document.getText();
    const malformed = new vscode.WorkspaceEdit();
    malformed.replace(uri, fullRange(), '<panel broken=');
    await vscode.workspace.applyEdit(malformed);
    await until(() => vscode.languages.getDiagnostics(uri).some(item => item.severity === vscode.DiagnosticSeverity.Error),
      'malformed XML diagnostics');
    assert.equal(document.getText(), '<panel broken=');
    record('Malformed source remains untouched and is diagnosed');
    const restore = new vscode.WorkspaceEdit();
    restore.replace(uri, fullRange(), valid);
    await vscode.workspace.applyEdit(restore);
    await document.save();
    await waitForFrame('native preview recovers after invalid XML');

    const hooks = path.join(root, 'Tasks/UI/Generated/application.hpp');
    await fs.mkdir(path.dirname(hooks), { recursive: true });
    const sentinel = '// Handwritten integration sentinel: generation must preserve this file.\n';
    await fs.writeFile(hooks, sentinel);
    await vscode.commands.executeCommand('litheview.generateUi', uri);
    await until(async () => {
      try { return (await fs.readFile(path.join(root, 'Tasks/UI/Generated/form.hpp'), 'utf8')).includes('SOURCE EDIT'); }
      catch { return false; }
    }, 'generation from editor command');
    assert.equal(await fs.readFile(hooks, 'utf8'), sentinel);
    const componentManifest = JSON.parse(await fs.readFile(path.join(root, 'Tasks/UI/Generated/component.json'), 'utf8'));
    assert.equal(componentManifest.generationStatus, 'complete');
    assert.equal(componentManifest.targetStatus, 'unevaluated');
    for (const retired of ['win32.cpp', 'headless.cpp', 'runtime.hpp', '.vscode/launch.json']) {
      await assert.rejects(fs.access(path.join(root, 'Tasks/UI/Generated', retired)), { code: 'ENOENT' });
    }
    record('Generate UI exports a portable component, preserves user files and owns no application build');

    await api.openHandler(uri, component, document.version);
    const handlerUri = vscode.Uri.file(path.join(root, 'Tasks/UI/form_handlers.hpp'));
    const handlerDocument = await vscode.workspace.openTextDocument(handlerUri);
    assert.match(handlerDocument.getText(), /void onActivateButtonClicked\(\) noexcept override/);
    const handlerEdit = new vscode.WorkspaceEdit();
    handlerEdit.replace(handlerUri, new vscode.Range(handlerDocument.positionAt(0),
      handlerDocument.positionAt(handlerDocument.getText().length)),
      handlerDocument.getText().replace('// Add application behavior here.', '// Keep this application behavior exactly.'));
    assert.equal(await vscode.workspace.applyEdit(handlerEdit), true);
    await handlerDocument.save();
    const handlerBytes = await fs.readFile(handlerUri.fsPath, 'utf8');
    await api.openHandler(uri, component, document.version);
    assert.equal(handlerDocument.getText(), handlerBytes);
    await vscode.commands.executeCommand('litheview.generateUi', uri);
    assert.equal(await fs.readFile(handlerUri.fsPath, 'utf8'), handlerBytes);
    record('Create/Open click handler creates a typed override and preserves its body through reopen and generation');

    // Exercise several events on one control, along with an independent checkbox.
    for (const [widgetId, eventIds] of [
      ['lithe.widget.checkbox', ['checkedChanged']],
      ['lithe.widget.drop-down', ['selectionChanged', 'editCommitted', 'editCancelled']],
      ['lithe.widget.wheel-picker', ['selectionChanged', 'editCommitted', 'editCancelled']],
      ['lithe.widget.text-field', ['textChanged', 'editCommitted', 'editCancelled']]
    ]) {
      const before = new Set(componentIds(document.getText()));
      assert.equal(await api.editDocument(uri, document.version, { kind: 'add', parentId: rootComponent,
        widgetId, frame: { x: 40, y: 40, width: 100, height: 80 } }), true);
      const id = componentIds(document.getText()).find(value => !before.has(value));
      assert.ok(id);
      await document.save();
      for (const eventId of eventIds) {
        await api.openHandler(uri, id, document.version, eventId);
        assert.ok(handlerDocument.getText().includes(`// litheview-handler ${id} ${eventId} `));
        await handlerDocument.save();
        const source = handlerDocument.getText();
        await api.openHandler(uri, id, document.version, eventId);
        assert.equal(handlerDocument.getText(), source);
      }
    }
    const eventHandlers = handlerDocument.getText();
    assert.ok(eventHandlers.includes('// Keep this application behavior exactly.'));
    await vscode.commands.executeCommand('litheview.generateUi', uri);
    assert.equal(await fs.readFile(handlerUri.fsPath, 'utf8'), eventHandlers);
    assert.match(await fs.readFile(path.join(root, 'Tasks/UI/Generated/form.hpp'), 'utf8'), /lithe::SelectionEdit event/);
    assert.match(eventHandlers, /lithe::TextChange event/);
    assert.match(eventHandlers, /lithe::TextEdit event/);
    record('Typed checkbox, drop-down, wheel and text events create/reopen/save/regenerate without changing existing handler bodies');

    assert.deepEqual(taskEvents, [], 'editing and generation must not launch application tasks');
    const originalTasks = await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8');
    await vscode.commands.executeCommand('litheview.addUi');
    await vscode.commands.executeCommand('workbench.action.closeActiveEditor');
    await assert.rejects(fs.access(path.join(root, 'NewUI/project.litheview.json')), { code: 'ENOENT' });
    record('Closing Add UI before Create leaves the application untouched');
    await vscode.commands.executeCommand('litheview.addUi');
    const created = await api.submitNewProject({ name: 'Blank Display', projectDirectory: 'NewUI',
      componentDirectory: 'NewUI/Generated', buildDirectory: 'out/blank-display', logicalInput: 'outputOnly',
      surfaceId: 'custom', custom: { width: 480, height: 272, shape: 'rectangle', format: 'rgb565',
        transfer: 'rgb888Parallel', physicalWidth: 95.04, physicalHeight: 53.86, safeArea: '0,0,480,272',
        manufacturer: 'Rocktech Displays Limited', model: 'RK043FN48H-CT672B',
        sourceUrl: 'https://www.rocktech.com.hk/lcd-product/rk043fn48h-ct672b/',
        sourceDocument: 'Rocktech RK043FN48H-CT672B product specification page' } });
    assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), originalTasks);
    const createdUri = vscode.Uri.file(created.formPath);
    await until(() => api.getDocumentState(createdUri)?.lastRenderStatus === 'Ok', 'new blank form native preview');
    const blankXml = await fs.readFile(created.formPath, 'utf8');
    assert.doesNotMatch(blankXml, /borderWidth=/);
    assert.equal((blankXml.match(/<panel /g) || []).length, 1);
    assert.doesNotMatch(blankXml, /<(?:label|button) /);
    await assert.rejects(fs.access(path.join(root, 'NewUI/Generated')), { code: 'ENOENT' });
    const newDocument = await vscode.workspace.openTextDocument(createdUri);
    for (const [propertyId, value] of [['background-color', '#00303030'], ['border-color', '#00ffffff'], ['border-width', 2]]) {
      assert.equal(await api.editDocument(createdUri, newDocument.version,
        { kind: 'property', componentId: 'component.main.root', propertyId, value }), true);
    }
    await newDocument.save();
    await vscode.commands.executeCommand('litheview.generateUi', createdUri);
    await until(async () => {
      try { await fs.access(path.join(root, 'NewUI/Generated/form.hpp')); return true; }
      catch (error) { if (error.code === 'ENOENT') return false; throw error; }
    }, 'generate the developer-edited custom-display form');
    await until(() => api.getDocumentState(createdUri)?.lastRenderedVersion === newDocument.version &&
      api.getDocumentState(createdUri)?.lastRenderStatus === 'Ok', 'native preview after adding the border');
    assert.match(newDocument.getText(), /borderWidth="2"/);
    assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), originalTasks);
    record('Custom display opens blank; property edits, save and Generate create the bordered form without changing application tasks');

    const definitionBeforeInput = await fs.readFile(path.join(root, 'NewUI/profiles/display-definition.json'), 'utf8');
    const formBeforeInput = await fs.readFile(created.formPath, 'utf8');
    const setup = await api.projectSetup(createdUri);
    setup.values.logicalInput = 'touch';
    assert.equal(await setup.submit(setup.values), true);
    assert.equal(JSON.parse(await fs.readFile(path.join(root, 'NewUI/profiles/logical-input.json'), 'utf8')).pointer, true);
    const repeatedSetup = await api.projectSetup(createdUri);
    assert.equal(repeatedSetup.values.input.pointer, true);
    assert.equal(await repeatedSetup.submit(repeatedSetup.values), false);
    await vscode.commands.executeCommand('litheview.generateUi', createdUri);
    assert.equal(JSON.parse(await fs.readFile(path.join(root, 'NewUI/Generated/logical-input.json'), 'utf8')).pointer, true);
    assert.equal(await fs.readFile(created.formPath, 'utf8'), formBeforeInput);
    assert.equal(await fs.readFile(path.join(root, 'NewUI/profiles/display-definition.json'), 'utf8'), definitionBeforeInput);
    assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), originalTasks);
    record('Existing output-only form gains Touch input and regenerates through a paired saved edit without recreating its UI');

    const frontSetup = await api.projectSetup(createdUri);
    const frontImage = await frontSetup.chooseImage('board-front.png');
    const { dataUrl, ...frontMetadata } = frontImage;
    assert.match(dataUrl, /^data:image\/png;base64,/);
    frontSetup.values.frontPanel = { schemaVersion: 1, enabled: true, image: frontMetadata, windowWidth: 905,
      display: { x: 232, y: 166, width: 1386, height: 812 },
      buttons: [{ id: 'userButton', bounds: { x: 344, y: 1140, width: 84, height: 80 } }] };
    assert.equal(await frontSetup.submit(frontSetup.values), true);
    const frontPath = path.join(root, 'NewUI/project.litheview.front-panel.json');
    const savedFront = await fs.readFile(frontPath, 'utf8');
    assert.deepEqual(JSON.parse(savedFront), frontSetup.values.frontPanel);
    const reopenFront = await api.projectSetup(createdUri);
    assert.deepEqual(reopenFront.values.frontPanel, frontSetup.values.frontPanel);
    assert.equal(await reopenFront.submit(reopenFront.values), false);
    const cancelFront = await api.projectSetup(createdUri);
    cancelFront.values.frontPanel.buttons[0].bounds.x++;
    cancelFront.cancel();
    assert.equal(await fs.readFile(frontPath, 'utf8'), savedFront);
    assert.equal(await fs.readFile(created.formPath, 'utf8'), formBeforeInput);
    assert.equal(await fs.readFile(path.join(root, 'NewUI/profiles/display-definition.json'), 'utf8'), definitionBeforeInput);
    assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), originalTasks);
    record('Configure selects a PNG, saves and reopens a host-only front view and named button; no-op and cancel preserve source and application tasks');

    const cancelledSetup = await api.projectSetup(createdUri);
    cancelledSetup.values.custom.width = 999;
    cancelledSetup.cancel();
    assert.equal(await fs.readFile(path.join(root, 'NewUI/profiles/display-definition.json'), 'utf8'), definitionBeforeInput);
    const displaySetup = await api.projectSetup(createdUri);
    assert.equal(displaySetup.values.custom.format, 'rgb565');
    assert.equal(displaySetup.values.custom.width, 480);
    displaySetup.values.custom.format = 'xrgb8888';
    assert.equal(await displaySetup.submit(displaySetup.values), true);
    await vscode.commands.executeCommand('litheview.generateUi', createdUri);
    assert.equal(JSON.parse(await fs.readFile(path.join(root, 'NewUI/Generated/display-definition.json'), 'utf8')).format, 'xrgb8888');
    assert.equal(JSON.parse(await fs.readFile(path.join(root, 'NewUI/Generated/logical-input.json'), 'utf8')).pointer, true);
    assert.equal(await fs.readFile(created.formPath, 'utf8'), formBeforeInput);
    assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), originalTasks);
    record('Project setup reopens saved RGB display/input, cancellation preserves files, and a storage-format change regenerates without rewriting form or tasks');

    for (const [name, format, width, height] of [
      ['synthetic-r2', 'r2Packed2x2', 512, 384],
      ['synthetic-red-cell', 'rrrr2222', 256, 192],
      ['synthetic-red-yellow', 'ryry2222', 256, 192]
    ]) {
      for (const custom of [false, true]) {
        const folder = `${name}-${custom ? 'custom' : 'catalog'}`;
        await vscode.commands.executeCommand('litheview.addUi');
        const packed = await api.submitNewProject({ name: `Native ${folder}`, projectDirectory: folder,
          componentDirectory: `${folder}/Generated UI`, logicalInput: 'encoderSelect',
          surfaceId: custom ? 'custom' : `native:litheview.definition.software.${name}`,
          custom: custom ? { format, width: 120, height: 80, shape: 'rectangle',
            sourceReference: 'Integration panel specification, revision 1' } : undefined });
        const packedUri = vscode.Uri.file(packed.formPath);
        const packedDocument = await vscode.workspace.openTextDocument(packedUri);
        const waitPacked = () => until(() => {
          const state = api.getDocumentState(packedUri);
          if (state?.lastError) throw new Error(`${folder}: ${state.lastError}`);
          return state?.lastRenderedVersion === packedDocument.version && state?.lastRenderStatus === 'Ok';
        }, `${folder} native preview`);
        await waitPacked();
        const packedSetup = await api.projectSetup(packedUri);
        assert.equal(packedSetup.values.custom.format, format);
        assert.equal(packedSetup.values.custom.width, custom ? 120 : width);
        assert.equal(packedSetup.values.input.encoder, true);
        assert.equal(await packedSetup.submit(packedSetup.values), false);
        const packedInputSetup = await api.projectSetup(packedUri);
        packedInputSetup.values.logicalInput = 'custom';
        packedInputSetup.values.input.pointer = true;
        assert.equal(await packedInputSetup.submit(packedInputSetup.values), true);
        const projectManifest = JSON.parse(await fs.readFile(packed.manifestPath, 'utf8'));
        assert.equal(projectManifest.schemaVersion, 3);
        assert.equal(projectManifest.surfaceProfile, undefined);
        const definition = JSON.parse(await fs.readFile(path.join(root, folder, 'profiles/display-definition.json'), 'utf8'));
        assert.equal(definition.format, format);
        assert.equal(definition.geometry.width, custom ? 120 : width);
        assert.equal(definition.geometry.height, custom ? 80 : height);
        assert.equal((packedDocument.getText().match(/<panel /g) || []).length, 1);
        await assert.rejects(fs.access(path.join(root, folder, 'Generated UI')), { code: 'ENOENT' });
        assert.equal(await api.editDocument(packedUri, packedDocument.version, { kind: 'add',
          parentId: 'component.main.root', widgetId: 'lithe.widget.button', frame: { x: 8, y: 8, width: 80, height: 24 } }), true);
        await packedDocument.save();
        await waitPacked();
        await vscode.commands.executeCommand('litheview.generateUi', packedUri);
        const generated = path.join(root, folder, 'Generated UI');
        await until(async () => {
          try { await fs.access(path.join(generated, 'form.hpp')); return true; }
          catch (error) { if (error.code === 'ENOENT') return false; throw error; }
        }, `${folder} native generation`);
        const component = JSON.parse(await fs.readFile(path.join(generated, 'component.json'), 'utf8'));
        assert.equal(component.targetStatus, 'unevaluated');
        await api.openHandler(packedUri, 'component.button.1', packedDocument.version);
        const handlerUri = vscode.Uri.file(path.join(root, folder, 'form_handlers.hpp'));
        const handler = await vscode.workspace.openTextDocument(handlerUri);
        assert.match(handler.getText(), /void onButton1Clicked\(\) noexcept override/);
        const handlerEdit = new vscode.WorkspaceEdit();
        handlerEdit.replace(handlerUri, new vscode.Range(handler.positionAt(0), handler.positionAt(handler.getText().length)),
          handler.getText().replace('// Add application behavior here.', '// Keep native panel behavior.'));
        assert.equal(await vscode.workspace.applyEdit(handlerEdit), true);
        await handler.save();
        const bytes = await fs.readFile(handlerUri.fsPath, 'utf8');
        await vscode.commands.executeCommand('litheview.generateUi', packedUri);
        assert.equal(await fs.readFile(handlerUri.fsPath, 'utf8'), bytes);
        assert.equal(await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8'), originalTasks);
        record(`${folder}: wizard creates blank native form, preview/edit/save/generate and typed handler preservation pass`);
      }
    }
    assert.deepEqual(taskEvents, [], 'native project creation and generation must not launch application tasks');
    await fs.writeFile(output, JSON.stringify({ passed: true, checks, taskEvents, extensionPath: extension.extensionPath, scope:
      'Real VS Code custom editor, native preview, edit/save/undo/source/diagnostics/generation without application task execution. No hardware.' }, null, 2));
  } catch (error) {
    await fs.writeFile(output, JSON.stringify({ passed: false, checks, taskEvents, error: String(error.stack || error) }, null, 2));
    throw error;
  } finally {
    taskSubscription.dispose();
  }
}

module.exports = { run };
