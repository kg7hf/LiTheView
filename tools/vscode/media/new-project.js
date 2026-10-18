// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';
(() => {
  const vscode = acquireVsCodeApi();
  const form = document.getElementById('project');
  const surface = document.getElementById('surface');
  const status = document.getElementById('status');
  let surfaces = [];
  let setup = false;
  let glass = null;
  const displayKind = document.getElementById('display-kind');
  const format = document.getElementById('format');
  if (window.litheviewFrontPanel) window.litheviewFrontPanel.connect(vscode);
  function updateFormat(custom) {
    const packed = ['r2Packed2x2', 'rrrr2222', 'ryry2222'].includes(format.value);
    const cells = packed && format.value !== 'r2Packed2x2';
    for (const label of document.querySelectorAll('.rgb-field')) {
      label.hidden = packed;
      for (const input of label.querySelectorAll('input, select')) input.disabled = !custom || packed;
    }
    document.getElementById('native-reference').hidden = !packed;
    form.elements.sourceReference.disabled = !custom || !packed;
    const units = cells ? 'cells' : packed ? 'emitters' : 'pixels';
    document.getElementById('width-label').textContent = `Width (${units})`;
    document.getElementById('height-label').textContent = `Height (${units})`;
    const step = packed && !cells ? '2' : '1';
    for (const input of [form.elements.width, form.elements.height]) {
      input.min = step; input.step = step; input.max = packed ? '4096' : '16384';
    }
    document.getElementById('format-description').textContent = cells
      ? 'Each logical cell contains four 2-bit emitters in one byte. A 256 × 192 cell form presents 512 × 384 emitters. Enter cell dimensions; changing format does not resize your entries.'
      : packed ? 'Each form coordinate addresses one 2-bit red emitter. Four emitters in a 2 × 2 block occupy one byte; both dimensions must be even. A 512 × 384 form needs 49,152 bytes of native storage.'
      : 'Choose the framebuffer storage and panel transfer formats independently.';
  }
  function updateSurface() {
    const segmented = displayKind?.value === 'segmented';
    document.getElementById('pixel-display').hidden = segmented;
    if (displayKind) document.getElementById('segmented').hidden = !segmented;
    surface.disabled = segmented;
    const custom = !segmented && surface.value === 'custom';
    document.getElementById('custom').hidden = !custom;
    for (const input of document.querySelectorAll('#custom input, #custom select')) input.disabled = !custom;
    const selected = surfaces.find(item => item.id === surface.value);
    document.getElementById('surface-description').textContent = selected
      ? `${selected.width} × ${selected.height} ${selected.addressSpace || 'pixels'} · ${selected.format} · ${selected.shape}. This is a fixed display configuration, independent of the MCU.`
      : 'Define your geometry using a supported native storage format. New pixel encodings require library support.';
    updateFormat(custom);
    const customInput = form.elements.logicalInput.querySelector('option[value=custom]');
    customInput.hidden = !setup && !segmented;
    if (customInput.hidden && form.elements.logicalInput.value === 'custom') form.elements.logicalInput.value = 'outputOnly';
    updateInput();
  }
  displayKind?.addEventListener('change', updateSurface);
  document.getElementById('glass-import')?.addEventListener('click', () => vscode.postMessage({ type: 'importGlass' }));
  surface.addEventListener('change', updateSurface);
  format.addEventListener('change', updateSurface);
  function updateInput() {
    document.getElementById('input-capabilities').hidden = form.elements.logicalInput.value !== 'custom';
  }
  form.elements.logicalInput.addEventListener('change', updateInput);
  document.getElementById('cancel').addEventListener('click', () => vscode.postMessage({ type: 'cancel' }));
  form.addEventListener('submit', event => {
    event.preventDefault();
    const values = Object.fromEntries(new FormData(form));
    const custom = {};
    for (const input of document.querySelectorAll('#custom input, #custom select')) custom[input.name] = input.value;
    const input = (setup || values.logicalInput === 'custom') ? Object.fromEntries(['pointer', 'keys', 'text', 'encoder', 'select'].map(key => [key, form.elements[key].checked])) : undefined;
    vscode.postMessage({ type: setup ? 'apply' : 'create', values: {
      name: values.name, projectDirectory: values.projectDirectory, componentDirectory: values.componentDirectory,
      surfaceId: values.surfaceId, logicalInput: values.logicalInput, custom, input,
      displayKind: displayKind?.value, glass: glass ? JSON.stringify(glass) : undefined,
      frontPanel: setup ? window.litheviewFrontPanel.value() : undefined
    } });
  });
  window.addEventListener('message', event => {
    const message = event.data;
    if (message.type === 'initialize') {
      surfaces = [...message.surfaces, ...(message.nativeDisplays || []).map(item => ({ ...item, id: `native:${item.id}` }))];
      surface.replaceChildren();
      for (const item of [{ id: 'custom', name: 'Custom display' }, ...surfaces.map(item => ({ id: item.id, name: item.name }))]) {
        const option = document.createElement('option'); option.value = item.id; option.textContent = item.name; surface.append(option);
      }
      surface.value = 'custom';
      setup = message.setup === true;
      if (setup) {
        window.litheviewFrontPanel.load(message.frontPanel?.value, message.frontPanel?.image);
        form.elements.logicalInput.value = message.values.logicalInput;
        for (const [key, value] of Object.entries(message.values.custom)) form.elements[key].value = value ?? '';
        for (const key of ['pointer', 'keys', 'text', 'encoder', 'select']) form.elements[key].checked = message.values.input[key];
        for (const option of [...format.options]) {
          if (['r2Packed2x2', 'rrrr2222', 'ryry2222'].includes(option.value) !== message.packed) option.remove();
        }
      }
      document.getElementById('location').textContent = `Application: ${message.applicationRoot}\nLiTheView: ${message.libraryRoot}`;
      document.getElementById('fields').disabled = false;
      updateSurface();
      updateInput();
      if (message.frontPanel?.warning) status.textContent = message.frontPanel.warning;
    }
    if (message.type === 'glass') {
      glass = message.glass;
      window.litheviewGlassPreview.render(glass);
      status.textContent = 'Glass validated. Choose input and create the blank form.';
    }
    if (message.type === 'front-image') window.litheviewFrontPanel.load(null, message.image);
    if (message.type === 'busy') {
      document.getElementById('fields').disabled = message.busy;
      status.textContent = message.busy ? (setup ? 'Validating and saving project settings…' : 'Validating the display and creating the UI…') : '';
    }
    if (message.type === 'error') { document.getElementById('fields').disabled = false; status.textContent = message.message; }
  });
  vscode.postMessage({ type: 'ready' });
})();
