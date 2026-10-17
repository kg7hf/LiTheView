// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { isInside } = require('./project-paths');

function frontPanelPath(project) { return project.manifestPath.replace(/\.json$/i, '.front-panel.json'); }
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

async function imageFile(project, relative) {
  if (typeof relative !== 'string' || !relative || path.isAbsolute(relative) || /[\x00-\x1f]/.test(relative)) throw new Error('Choose a PNG inside the application folder.');
  const file = path.resolve(project.applicationRoot, relative);
  if (!isInside(file, project.applicationRoot) || !isInside(await fs.realpath(file), await fs.realpath(project.applicationRoot))) throw new Error('The board image must stay inside the application.');
  const stat = await fs.stat(file);
  if (!stat.isFile() || stat.size > 16 * 1024 * 1024) throw new Error('Use a PNG smaller than 16 MiB.');
  const bytes = await fs.readFile(file);
  if (bytes.length > 16 * 1024 * 1024 || bytes.length < 33 || !bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])) || bytes.toString('ascii', 12, 16) !== 'IHDR') throw new Error('The front view must be a PNG image smaller than 16 MiB.');
  const width = bytes.readUInt32BE(16), height = bytes.readUInt32BE(20);
  if (width < 1 || height < 1 || width > 8192 || height > 8192 || width * height > 16777216) throw new Error('Use an image up to 8192 pixels per side and 16 megapixels.');
  return { path: path.relative(project.applicationRoot, file).replace(/\\/g, '/'), width, height, sha256: hash(bytes),
    dataUrl: `data:image/png;base64,${bytes.toString('base64')}` };
}

function rectangle(value, image, label) {
  if (!value || !['x', 'y', 'width', 'height'].every(key => Number.isInteger(value[key])) ||
      value.x < 0 || value.y < 0 || value.width < 1 || value.height < 1 ||
      value.x + value.width > image.width || value.y + value.height > image.height) throw new Error(`${label} must fit inside the image, in original image pixels.`);
  return { x: value.x, y: value.y, width: value.width, height: value.height };
}
function overlap(a, b) { return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height; }

function validateFrontPanel(value) {
  if (!value || value.schemaVersion !== 1 || typeof value.enabled !== 'boolean' ||
      !Number.isInteger(value.windowWidth) || value.windowWidth < 160 || value.windowWidth > 4096) throw new Error('Invalid front-view configuration or window width (160–4096 pixels).');
  const image = value.image;
  if (!image || typeof image.path !== 'string' || !/^[a-f0-9]{64}$/.test(image.sha256) ||
      ![image.width, image.height].every(n => Number.isInteger(n) && n > 0 && n <= 8192) ||
      image.width * image.height > 16777216 || value.windowWidth * image.height / image.width > 4096) throw new Error('Invalid front-view image metadata or window height.');
  const display = rectangle(value.display, image, 'Display area');
  const windowHeight = Math.round(value.windowWidth * image.height / image.width);
  if (windowHeight < 1) throw new Error('The front-view window must have a positive height.');
  if (!Array.isArray(value.buttons) || value.buttons.length > 16) throw new Error('Use at most 16 physical push-buttons.');
  const ids = new Set();
  const buttons = value.buttons.map(button => {
    if (!button || typeof button.id !== 'string' || !/^[A-Za-z][A-Za-z0-9_]{0,47}$/.test(button.id) || button.id === 'display' || ids.has(button.id)) throw new Error('Each push-button needs a unique name using letters, digits or underscore; display is reserved.');
    ids.add(button.id);
    return { id: button.id, bounds: rectangle(button.bounds, image, `Button ${button.id}`) };
  });
  for (let index = 0; index < buttons.length; index++) {
    if (overlap(display, buttons[index].bounds) || buttons.slice(0, index).some(other => overlap(other.bounds, buttons[index].bounds))) throw new Error('Display and push-button areas must not overlap.');
  }
  for (const box of [display, ...buttons.map(button => button.bounds)]) {
    if (Math.floor((box.x + box.width) * value.windowWidth / image.width) <= Math.floor(box.x * value.windowWidth / image.width) ||
        Math.floor((box.y + box.height) * windowHeight / image.height) <= Math.floor(box.y * windowHeight / image.height)) throw new Error('An area disappears at this window width. Enlarge the area or the window.');
  }
  return { schemaVersion: 1, enabled: value.enabled, image: { path: image.path, width: image.width, height: image.height, sha256: image.sha256 },
    windowWidth: value.windowWidth, display, buttons };
}

async function readFrontPanel(project) {
  const file = frontPanelPath(project);
  let before = null;
  try {
    if (!isInside(await fs.realpath(file), await fs.realpath(project.applicationRoot)) || (await fs.stat(file)).size > 65536) throw new Error('Invalid front-view settings path or size.');
    before = await fs.readFile(file, 'utf8');
  } catch (error) { if (error.code !== 'ENOENT') throw error; }
  if (before === null) return { path: file, before, value: null };
  const value = validateFrontPanel(JSON.parse(before.replace(/^\uFEFF/, '')));
  let image;
  try { image = await imageFile(project, value.image.path); }
  catch (error) { return { path: file, before, value, warning: `${error.message} Choose the front-view PNG again to repair it.` }; }
  // Show the current image; applying it deliberately updates the saved image lock.
  return { path: file, before, value, image };
}

async function frontPanelEdit(project, original, value) {
  if (value === null || value === undefined) return { path: original.path, before: original.before, after: original.before };
  const validated = validateFrontPanel(value);
  const image = await imageFile(project, validated.image.path);
  if (image.sha256 !== validated.image.sha256 || image.width !== validated.image.width || image.height !== validated.image.height) throw new Error('The board image changed. Choose it again before Apply.');
  const after = original.value && JSON.stringify(validated) === JSON.stringify(original.value) ? original.before : JSON.stringify(validated, null, 2) + '\n';
  return { path: original.path, before: original.before, after };
}

async function chooseFrontImage(vscode, project, relative) {
  let file = relative;
  if (file === undefined) {
    const selected = await vscode.window.showOpenDialog({ canSelectFiles: true, canSelectFolders: false, canSelectMany: false,
      defaultUri: vscode.Uri.file(project.applicationRoot), filters: { 'Board front view': ['png'] }, openLabel: 'Use front view' });
    if (!selected?.length) return;
    file = path.relative(project.applicationRoot, selected[0].fsPath);
  }
  return imageFile(project, file);
}

module.exports = { frontPanelPath, readFrontPanel, frontPanelEdit, chooseFrontImage, validateFrontPanel };
