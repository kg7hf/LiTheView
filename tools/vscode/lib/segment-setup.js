// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
async function readGlassJson(file) {
  const handle = await fs.open(file, 'r');
  try {
    if (!(await handle.stat()).isFile()) throw new Error('Choose a regular glass JSON file.');
    const bytes = Buffer.alloc(1024 * 1024 + 1);
    let total = 0;
    while (total < bytes.length) { const { bytesRead } = await handle.read(bytes, total, bytes.length - total, null); if (!bytesRead) break; total += bytesRead; }
    if (total > 1024 * 1024) throw new Error('Glass JSON exceeds 1 MiB.');
    return bytes.subarray(0, total).toString('utf8');
  } finally { await handle.close(); }
}
const keys = ['pointer', 'keys', 'text', 'encoder', 'select'];
function segmentSetupRequest(current, values) {
  if (!values || !keys.every(key => typeof values.input?.[key] === 'boolean')) throw new Error('Choose logical input capabilities.');
  if (typeof values.glass !== 'string' || Buffer.byteLength(values.glass, 'utf8') > 1024 * 1024) throw new Error('Glass JSON exceeds 1 MiB.');
  return { snapshot: current.snapshot, segmentGlass: JSON.parse(values.glass), input: Object.fromEntries(keys.map(k => [k, values.input[k]])) };
}
function renderSegmentSetup(asset, csp, nonce) {
  return `<!DOCTYPE html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${csp}; script-src 'nonce-${nonce}';"><link rel="stylesheet" href="${asset('new-project.css')}"></head>
<body><main><h1>Configure LiTheView UI</h1>
<p>Review this project's segmented display and input. Apply settings, Generate UI, then build and debug with your existing application configurations.</p>
<section><h2>Display</h2><p>Segmented display — fixed glass</p>
<button id="import" type="button">Import glass JSON…</button>
<p id="glass-summary" role="status"></p>
<canvas id="glass-preview" width="600" height="300" aria-label="Glass geometry with all segments on" hidden></canvas>
<p class="hint">All segments are shown to identify the glass. Your form and application decide which are on.</p>
<h2>Available fields</h2><p id="glass-fields" class="hint"></p>
<input id="glass" type="hidden">
<p class="hint">Import a different definition to change the glass. MCU wiring remains in your application.</p></section>
<section><h2>Input</h2><p>Custom logical capabilities</p>
<fieldset class="grid"><legend class="hint">Choose the input events your application supplies.</legend>${keys.map(key => `<label class="check"><input id="${key}" type="checkbox">${({pointer:'Pointer / touch', keys:'Navigation keys', text:'Text input', encoder:'Rotary encoder', select:'Select button'})[key]}</label>`).join('')}</fieldset>
<p class="hint">PC mouse and keyboard stand in for the devices. These settings do not assign GPIOs or change your menu FSM.</p></section>
<section><h2>Save configuration</h2><p>Apply validates every screen against the glass and preserves compatible bindings. Then Generate UI and build as usual.</p>
<p id="error" role="alert"></p><button id="apply" type="button">Apply</button><button id="cancel" class="secondary" type="button">Cancel</button></section></main>
<script nonce="${nonce}" src="${asset('glass-preview.js')}"></script>
<script nonce="${nonce}" src="${asset('segment-setup.js')}"></script></body></html>`;
}
async function openSegmentSetup(vscode, context, current, apply, finished, validateGlass) {
  if (!current?.segmentGlass || typeof current.segmentGlass !== 'object' || Array.isArray(current.segmentGlass) ||
      !keys.every(key => typeof current.input?.[key] === 'boolean')) {
    throw new Error('Incomplete segmented display configuration. Update the LiTheView extension and its designer service, then reopen Configure.');
  }
  const values = { glass: JSON.stringify(current.segmentGlass, null, 2), input: current.input };
  const panel = vscode.window.createWebviewPanel('litheview.segmentSetup', 'LiTheView: Configure', vscode.ViewColumn.Active,
    { enableScripts: true, retainContextWhenHidden: true, localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')] });
  const asset = name => panel.webview.asWebviewUri(vscode.Uri.joinPath(context.extensionUri, 'media', name)).toString();
  panel.webview.html = renderSegmentSetup(asset, panel.webview.cspSource, crypto.randomBytes(24).toString('base64'));
  let disposed = false, busy = false;
  const handle = async message => {
    if (!message || disposed) return;
    if (message.type === 'cancel') { panel.dispose(); return; }
    if (busy) return;
    try {
      if (message.type === 'ready') await panel.webview.postMessage({ type: 'initialize', values });
      if (message.type === 'import') {
        busy = true;
        await panel.webview.postMessage({ type: 'busy', busy: true });
        const files = await vscode.window.showOpenDialog({ canSelectMany: false, filters: { 'Glass JSON': ['json'] } });
        if (files?.length) {
          if (!validateGlass) throw new Error('Update the designer service to validate imported glass.');
          const validated = await validateGlass(JSON.parse(await readGlassJson(files[0].fsPath)));
          const glass = JSON.stringify(validated);
          if (!disposed) await panel.webview.postMessage({ type: 'glass', glass });
        }
      }
      if (message.type === 'apply') {
        busy = true;
        await panel.webview.postMessage({ type: 'busy', busy: true });
        const changed = await apply(segmentSetupRequest(current, message.values));
        panel.dispose(); return changed;
      }
    } catch (error) { if (!disposed) await panel.webview.postMessage({ type: 'error', message: error.message }); }
    finally {
      if (busy) {
        busy = false;
        if (disposed) finished();
        else await panel.webview.postMessage({ type: 'busy', busy: false });
      }
    }
  };
  const subscription = panel.webview.onDidReceiveMessage(handle);
  panel.onDidDispose(() => { disposed = true; subscription.dispose(); if (!busy) finished(); });
  context.subscriptions.push(panel);
  return { values, submit: next => handle({ type: 'apply', values: next }), cancel: () => panel.dispose(), isOpen: () => !disposed };
}
module.exports = { readGlassJson, segmentSetupRequest, renderSegmentSetup, openSegmentSetup };
