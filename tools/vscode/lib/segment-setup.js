// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const keys = ['pointer', 'keys', 'text', 'encoder', 'select'];
function segmentSetupRequest(current, values) {
  if (!values || !keys.every(key => typeof values.input?.[key] === 'boolean')) throw new Error('Choose logical input capabilities.');
  if (typeof values.glass !== 'string' || Buffer.byteLength(values.glass, 'utf8') > 1024 * 1024) throw new Error('Glass JSON exceeds 1 MiB.');
  return { snapshot: current.snapshot, segmentGlass: JSON.parse(values.glass), input: Object.fromEntries(keys.map(k => [k, values.input[k]])) };
}
function renderSegmentSetup(asset, csp, nonce) {
  return `<!DOCTYPE html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${csp}; script-src 'nonce-${nonce}';"><link rel="stylesheet" href="${asset('new-project.css')}"></head>
<body><main><h1>Configure segmented display</h1><p>The glass defines fixed geometry and named fields. MCU wiring and menu behavior remain in your application.</p>
<button id="import" type="button">Import glass JSON</button><label for="glass">Glass definition JSON</label><textarea id="glass" rows="22" spellcheck="false"></textarea>
<fieldset><legend>Logical input capabilities</legend>${keys.map(key => `<label><input id="${key}" type="checkbox">${key}</label>`).join('')}</fieldset>
<p>Applying validates every screen and updates their glass locks together. Existing bindings must still be valid.</p>
<p id="error" role="alert"></p><button id="apply" type="button">Apply</button><button id="cancel" type="button">Cancel</button></main>
<script nonce="${nonce}" src="${asset('segment-setup.js')}"></script></body></html>`;
}
async function openSegmentSetup(vscode, context, current, apply, finished) {
  const panel = vscode.window.createWebviewPanel('litheview.segmentSetup', 'LiTheView: Configure', vscode.ViewColumn.Active,
    { enableScripts: true, retainContextWhenHidden: true, localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')] });
  const asset = name => panel.webview.asWebviewUri(vscode.Uri.joinPath(context.extensionUri, 'media', name)).toString();
  panel.webview.html = renderSegmentSetup(asset, panel.webview.cspSource, crypto.randomBytes(24).toString('base64'));
  let disposed = false, busy = false;
  const values = { glass: JSON.stringify(current.segmentGlass, null, 2), input: current.input };
  const handle = async message => {
    if (!message || disposed || busy) return;
    try {
      if (message.type === 'ready') await panel.webview.postMessage({ type: 'initialize', values });
      if (message.type === 'cancel') panel.dispose();
      if (message.type === 'import') {
        const files = await vscode.window.showOpenDialog({ canSelectMany: false, filters: { 'Glass JSON': ['json'] } });
        if (files?.length) {
          if ((await fs.stat(files[0].fsPath)).size > 1024 * 1024) throw new Error('Glass JSON exceeds 1 MiB.');
          const glass = await fs.readFile(files[0].fsPath, 'utf8');
          if (!disposed) await panel.webview.postMessage({ type: 'glass', glass });
        }
      }
      if (message.type === 'apply') {
        busy = true;
        await panel.webview.postMessage({ type: 'busy' });
        const changed = await apply(segmentSetupRequest(current, message.values));
        panel.dispose(); return changed;
      }
    } catch (error) { if (!disposed) await panel.webview.postMessage({ type: 'error', message: error.message }); }
    finally { if (busy) { busy = false; if (disposed) finished(); } }
  };
  const subscription = panel.webview.onDidReceiveMessage(handle);
  panel.onDidDispose(() => { disposed = true; subscription.dispose(); if (!busy) finished(); });
  context.subscriptions.push(panel);
  return { values, submit: next => handle({ type: 'apply', values: next }), cancel: () => panel.dispose(), isOpen: () => !disposed };
}
module.exports = { segmentSetupRequest, renderSegmentSetup, openSegmentSetup };
