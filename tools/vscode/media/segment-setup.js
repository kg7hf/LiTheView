// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
(() => {
  'use strict';
  const vscode = acquireVsCodeApi(), keys = ['pointer', 'keys', 'text', 'encoder', 'select'];
  const element = id => document.getElementById(id);
  window.addEventListener('message', ({ data }) => {
    if (data.type === 'initialize') { element('glass').value = data.values.glass; for (const key of keys) element(key).checked = data.values.input[key]; }
    if (data.type === 'glass') element('glass').value = data.glass;
    if (data.type === 'busy') element('apply').disabled = true;
    if (data.type === 'error') { element('error').textContent = data.message; element('apply').disabled = false; }
  });
  element('import').onclick = () => vscode.postMessage({ type: 'import' });
  element('cancel').onclick = () => vscode.postMessage({ type: 'cancel' });
  element('apply').onclick = () => vscode.postMessage({ type: 'apply', values: {
    glass: element('glass').value, input: Object.fromEntries(keys.map(key => [key, element(key).checked])) } });
  vscode.postMessage({ type: 'ready' });
})();
