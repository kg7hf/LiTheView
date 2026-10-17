// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
// Exercise the published round-menu lessons through the installed extension's
// actual custom-editor commands. Application builds remain application-owned.
const vscode = require('vscode');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function run() {
  const root = vscode.workspace.workspaceFolders[0].uri.fsPath;
  const checks = [];
  const taskEvents = [];
  const subscription = vscode.tasks.onDidEndTaskProcess(e => taskEvents.push(e.execution.task.name));
  const output = path.join(root, 'vscode-validation.json');
  const extension = vscode.extensions.getExtension('litheview.litheview-designer');
  let step = 0;
  function command(file, args, label) {
    let log;
    try { log = execFileSync(file, args, { cwd: root, encoding: 'utf8', timeout: 120000, maxBuffer: 16 * 1024 * 1024 }); }
    catch (e) { log = `${e.stdout || ''}\n${e.stderr || ''}`; require('node:fs').writeFileSync(path.join(root, `validation-${label}.log`), log); throw e; }
    require('node:fs').writeFileSync(path.join(root, `validation-${label}.log`), log);
  }
  function builds(label) {
    for (const preset of ['win32-debug', 'stm32-debug']) {
      command('cmake', ['--preset', preset], `${label}-${preset}-configure`);
      command('cmake', ['--build', '--preset', preset, '--parallel', '4'], `${label}-${preset}-build`);
    }
    command('ctest', ['--preset', 'win32-debug', '--output-on-failure'], `${label}-ctest`);
    checks.push(`${label}: same Windows and STM32 presets build; application tests pass`);
  }
  try {
    assert.ok(extension.extensionPath.includes(`${path.sep}extensions${path.sep}`), 'use installed VSIX');
    await assert.rejects(fs.access(path.join(root, 'Tasks/UI/project.litheview.json')));
    const tasks = await fs.readFile(path.join(root, '.vscode/tasks.json'), 'utf8');
    for (const platform of ['Win32', 'STM32']) command('pwsh', ['-NoProfile', '-File', 'tools/configure-tools.ps1', '-Platform', platform], `tools-${platform}`);
    builds('blank');
    const api = await extension.activate();
    await vscode.commands.executeCommand('litheview.addUi');
    const created = await api.submitNewProject({ name: 'Home Menu', projectDirectory: 'Tasks/UI',
      componentDirectory: 'Tasks/UI/Generated/{form}', logicalInput: 'touchKeys', surfaceId: 'custom',
      custom: { width: 390, height: 390, shape: 'circle', format: 'rgb888', transfer: 'rgb888Parallel',
        physicalWidth: 30.48, safeArea: '60,60,270,270', manufacturer: 'Govisionox Optoelectronics',
        model: 'IEG1120TB103GF-001 / MB1314',
        sourceUrl: 'https://www.st.com/resource/en/user_manual/um2271-discovery-kit-with-stm32l4r9ai-mcu-stmicroelectronics.pdf',
        sourceDocument: 'UM2271 Rev 4; MB1311 C-02 / MB1314 C-01' } });
    const old = vscode.Uri.file(created.formPath);
    const home = vscode.Uri.file(path.join(root, 'Tasks/UI/forms/Home.litheview.xml'));
    const manifestUri = vscode.Uri.file(path.join(root, 'Tasks/UI/project.litheview.json'));
    const manifestDoc = await vscode.workspace.openTextDocument(manifestUri);
    const rename = new vscode.WorkspaceEdit();
    rename.renameFile(old, home);
    rename.replace(manifestUri, new vscode.Range(manifestDoc.positionAt(0), manifestDoc.positionAt(manifestDoc.getText().length)), manifestDoc.getText().replace('forms/main.litheview.xml', 'forms/Home.litheview.xml'));
    assert.equal(await vscode.workspace.applyEdit(rename), true);
    await manifestDoc.save();
    const docs = new Map();
    async function open(uri) {
      await vscode.commands.executeCommand('vscode.openWith', uri, 'litheview.formDesigner');
      const doc = await vscode.workspace.openTextDocument(uri);
      docs.set(uri.fsPath, doc);
      return doc;
    }
    async function frame(uri) {
      const doc = docs.get(uri.fsPath);
      for (let n = 0; n < 300; ++n) {
        const state = api.getDocumentState(uri);
        if (state?.lastError) throw new Error(`${uri.fsPath}: ${state.lastError}`);
        if (state?.lastRenderedVersion === doc.version && state?.lastRenderStatus === 'Ok') return;
        await delay(100);
      }
      throw new Error(`No native frame: ${JSON.stringify(api.getDocumentState(uri))}`);
    }
    async function edit(uri, operation) {
      assert.equal(await api.editDocument(uri, docs.get(uri.fsPath).version, operation), true, JSON.stringify(operation));
    }
    function id(uri, name) {
      const match = [...docs.get(uri.fsPath).getText().matchAll(/litheview-component id="([^"]+)" name="([^"]+)"/g)].find(m => m[2] === name);
      assert.ok(match, `component ${name}`); return match[1];
    }
    async function property(uri, name, propertyId, value) { await edit(uri, { kind: 'property', componentId: id(uri, name), propertyId, value }); }
    async function generate(uri, page) {
      await docs.get(uri.fsPath).save();
      await frame(uri);
      const target = path.join(root, `Tasks/UI/Generated/${page}/component.json`);
      const before = await fs.stat(target).then(s => s.mtimeMs, () => 0);
      await vscode.commands.executeCommand('litheview.generateUi', uri);
      const after = await fs.stat(target);
      assert.ok(after.mtimeMs > before, `Generate ${page} must publish, not silently reject its handlers`);
      checks.push(`Generate ${page} succeeds with current handler bindings (${++step})`);
    }
    async function add(uri, type, name, coords, text, scale = 2) {
      const before = new Set([...docs.get(uri.fsPath).getText().matchAll(/litheview-component id="([^"]+)"/g)].map(m => m[1]));
      const [x, y, width, height] = coords;
      await edit(uri, { kind: 'add', parentId: id(uri, 'rootPanel'), widgetId: `lithe.widget.${type}`, frame: { x, y, width, height } });
      const added = [...docs.get(uri.fsPath).getText().matchAll(/litheview-component id="([^"]+)"/g)].map(m => m[1]).find(v => !before.has(v));
      await edit(uri, { kind: 'rename', componentId: added, name });
      if (text !== undefined) await edit(uri, { kind: 'text', componentId: added, text });
      if (type === 'label' || type === 'button') {
        await property(uri, name, 'foreground-color', '#00ffffff');
        await property(uri, name, 'text-scale', scale);
      }
      if (type === 'button') await property(uri, name, 'background-color', '#00805020');
    }
    await open(home);
    await property(home, 'rootPanel', 'background-color', '#00303030');
    await generate(home, 'Home');
    builds('panel');
    async function standard(uri, title, buttons) {
      await property(uri, 'rootPanel', 'background-color', '#00303030');
      await add(uri, 'label', 'titleLabel', [90,60,240,40], title, 3);
      for (let i=0; i<buttons.length; ++i) await add(uri, 'button', buttons[i]+'Button', [95,130+65*i,200,48], buttons[i].toUpperCase());
    }
    await standard(home, 'HOME', ['play','record','settings']);
    await generate(home, 'Home');
    builds('home-buttons');
    const screens = { Home: home };
    for (const [name, buttons] of [['Settings',['volume','storage','back']], ['Storage',['save','load','back']]]) {
      const uri = await api.addScreen(home, name); screens[name] = uri;
      await open(uri); await standard(uri, name.toUpperCase(), buttons); await generate(uri, name);
    }
    command('pwsh', ['-NoProfile', '-File', 'tools/connect-menu-handlers.ps1'], 'connect-navigation');
    for (const [name, uri] of Object.entries(screens)) await generate(uri, name);
    builds('navigation');
    const volume = await api.addScreen(home, 'Volume'); screens.Volume = volume; await open(volume);
    await property(volume, 'rootPanel', 'background-color', '#00303030');
    for (const [type, name, rect, text, scale] of [
      ['label','titleLabel',[90,60,240,40],'VOLUME',3],['label','levelLabel',[153,108,84,30],'50%',2],
      ['progress','volumeProgress',[85,153,220,28]],['button','minusButton',[95,201,85,44],'-',3],
      ['button','plusButton',[210,201,85,44],'+',3],['button','backButton',[95,260,200,44],'BACK',2],
      ['label','hintLabel',[95,316,200,16],'LEFT/RIGHT: LEVEL',1]]) await add(volume,type,name,rect,text,scale);
    await property(volume,'levelLabel','background-color','#00303030');
    await property(volume,'volumeProgress','value',50);
    await generate(volume,'Volume');
    command('pwsh',['-NoProfile','-File','tools/connect-menu-handlers.ps1','-Volume'],'connect-volume');
    await generate(volume,'Volume'); builds('volume');
    await open(home);
    await edit(home,{kind:'frame',componentId:id(home,'playButton'),frame:{x:65,y:130,width:160,height:48}});
    await add(home,'label','playbackLabel',[95,108,200,18],'STOPPED',1);
    await property(home,'playbackLabel','background-color','#00303030');
    await add(home,'button','stopButton',[235,130,95,48],'STOP');
    for (let i=0; i<3; ++i) await edit(home,{kind:'order',componentId:id(home,'stopButton'),order:'sendBackward'});
    await generate(home,'Home');
    command('pwsh',['-NoProfile','-File','tools/connect-menu-handlers.ps1','-Playback'],'connect-playback');
    await generate(home,'Home'); builds('playback');
    const load = await api.addScreen(home,'Load'); screens.Load=load; await open(load);
    await property(load,'rootPanel','background-color','#00303030');
    for (const [type,name,rect,text,scale] of [
      ['label','titleLabel',[90,50,240,40],'LOAD',3],['panel','entryList',[95,120,200,108]],
      ['label','loadedLabel',[95,248,200,18],'LOADED: NONE',1],['button','loadButton',[95,278,95,40],'LOAD',2],
      ['button','backButton',[200,278,95,40],'BACK',2]]) await add(load,type,name,rect,text,scale);
    await property(load,'loadedLabel','background-color','#00303030');
    await generate(load,'Load');
    command('pwsh',['-NoProfile','-File','tools/connect-menu-handlers.ps1','-Load'],'connect-load');
    for(const [name,uri] of Object.entries(screens)) { await open(uri); await generate(uri,name); }
    assert.equal((await api.refreshScreens(home)).length,5);
    builds('load');
    assert.equal(await fs.readFile(path.join(root,'.vscode/tasks.json'),'utf8'),tasks);
    assert.deepEqual(taskEvents,[],'designer does not execute build/run tasks');
    await fs.writeFile(output,JSON.stringify({passed:true,checks,taskEvents,extensionPath:extension.extensionPath,
      scope:'Blank application through custom circle wizard, visual edits, Add screen, native previews, Generate UI, lesson handlers and both CMake targets. No board access or manual mouse walkthrough.'},null,2));
  } catch(error) {
    await fs.writeFile(output,JSON.stringify({passed:false,checks,taskEvents,error:String(error.stack||error)},null,2)); throw error;
  } finally { subscription.dispose(); }
}
module.exports={run};
