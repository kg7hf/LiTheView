// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';
// Host-only board artwork and mouse hit regions. Coordinates remain in source-image pixels.
window.litheviewFrontPanel = (() => {
  let panel = null, image = null, selected = 'display', drag = null;
  const byId = id => document.getElementById(id);
  const canvas = () => byId('front-canvas');
  const bounds = () => selected === 'display' ? panel?.display : panel?.buttons.find(button => button.id === selected)?.bounds;
  function draw() {
    if (!image?.complete || !image.naturalWidth || !panel) return;
    const context = canvas().getContext('2d');
    context.drawImage(image, 0, 0);
    for (const item of [{ id: 'display', bounds: panel.display }, ...panel.buttons]) {
      const box = item.bounds;
      context.strokeStyle = item.id === selected ? '#00d0ff' : '#ffb52e';
      context.lineWidth = Math.max(2, image.width / 350);
      context.strokeRect(box.x, box.y, box.width, box.height);
      context.fillStyle = context.strokeStyle;
      context.font = `${Math.max(16, image.width / 60)}px sans-serif`;
      context.fillText(item.id, box.x + 4, Math.max(24, box.y - 5));
    }
    const box = bounds();
    for (const key of ['x', 'y', 'width', 'height']) byId(`front-${key}`).value = box?.[key] ?? 0;
  }
  function regions() {
    const select = byId('front-region');
    select.replaceChildren();
    for (const item of [{ id: 'display' }, ...(panel?.buttons ?? [])]) {
      const option = document.createElement('option'); option.value = item.id;
      option.textContent = item.id === 'display' ? 'LCD display area' : `Push-button: ${item.id}`;
      select.append(option);
    }
    select.value = selected;
    byId('front-remove').disabled = selected === 'display';
  }
  function show() {
    byId('front-content').hidden = !panel;
    if (!panel) return;
    byId('front-enabled').checked = panel.enabled;
    byId('front-window-width').value = panel.windowWidth;
    byId('front-image-name').textContent = `${panel.image.path} · ${panel.image.width} × ${panel.image.height} source pixels`;
    regions(); draw();
  }
  function load(value, source) {
    panel = value ? structuredClone(value) : null;
    selected = 'display';
    if (source) {
      if (!panel) panel = { schemaVersion: 1, enabled: true, windowWidth: Math.min(960, source.width),
        image: {}, display: { x: 0, y: 0, width: source.width, height: source.height }, buttons: [] };
      panel.image = { path: source.path, width: source.width, height: source.height, sha256: source.sha256 };
      image = new Image();
      const selectedImage = image;
      image.onload = () => { if (image !== selectedImage) return; canvas().width = image.naturalWidth; canvas().height = image.naturalHeight; show(); };
      image.onerror = () => { if (image === selectedImage) byId('status').textContent = 'The PNG could not be decoded. Choose another image.'; };
      image.src = source.dataUrl;
    } else { image = null; show(); }
  }
  function connect(vscode) {
    byId('front-browse').addEventListener('click', () => vscode.postMessage({ type: 'front-image' }));
    byId('front-enabled').addEventListener('change', event => { if (panel) panel.enabled = event.target.checked; });
    byId('front-window-width').addEventListener('change', event => { if (panel) panel.windowWidth = Number(event.target.value); });
    byId('front-region').addEventListener('change', event => { selected = event.target.value; regions(); draw(); });
    byId('front-add').addEventListener('click', () => {
      const id = byId('front-button-id').value.trim();
      if (!panel || !/^[A-Za-z][A-Za-z0-9_]{0,47}$/.test(id) || id === 'display' || panel.buttons.some(button => button.id === id) || panel.buttons.length >= 16) {
        byId('status').textContent = 'Use a unique button name (up to 16 buttons).'; return;
      }
      panel.buttons.push({ id, bounds: { x: 0, y: 0, width: 1, height: 1 } });
      selected = id; regions(); draw();
    });
    byId('front-remove').addEventListener('click', () => { if (panel && selected !== 'display') { panel.buttons = panel.buttons.filter(button => button.id !== selected); selected = 'display'; regions(); draw(); } });
    for (const key of ['x', 'y', 'width', 'height']) byId(`front-${key}`).addEventListener('change', event => { const box = bounds(); if (box) { box[key] = Number(event.target.value); draw(); } });
    const point = event => {
      const rect = canvas().getBoundingClientRect();
      return { x: Math.max(0, Math.min(canvas().width - 1, Math.floor((event.clientX - rect.left) * canvas().width / rect.width))),
        y: Math.max(0, Math.min(canvas().height - 1, Math.floor((event.clientY - rect.top) * canvas().height / rect.height))) };
    };
    canvas().addEventListener('pointerdown', event => { if (event.button === 0 && panel && image && !byId('fields').disabled) { drag = point(event); canvas().setPointerCapture(event.pointerId); } });
    canvas().addEventListener('pointermove', event => {
      if (!drag || byId('fields').disabled) return;
      const end = point(event), box = bounds();
      Object.assign(box, { x: Math.min(drag.x, end.x), y: Math.min(drag.y, end.y), width: Math.abs(end.x - drag.x) + 1, height: Math.abs(end.y - drag.y) + 1 }); draw();
    });
    const finish = () => { drag = null; };
    canvas().addEventListener('pointerup', finish); canvas().addEventListener('pointercancel', finish); canvas().addEventListener('lostpointercapture', finish);
  }
  return { connect, load, value: () => panel };
})();
