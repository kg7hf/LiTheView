// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const path = require('node:path');
const { isInside } = require('./project-paths');

function projectId(name) {
  const slug = String(name).toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  if (!slug) throw new Error('Use at least one letter or digit in the UI name.');
  return `litheview.project.${slug}`;
}

function required(value, label) {
  if (typeof value !== 'string' || !value.trim() || /[\x00-\x1f]/.test(value)) throw new Error(`${label} is required.`);
  return value.trim();
}

function customSurface(value, id, name) {
  if (!value || typeof value !== 'object') throw new Error('Enter the custom display specifications.');
  const width = Number(value.width), height = Number(value.height);
  if (![width, height].every(n => Number.isInteger(n) && n >= 1 && n <= 16384) || width * height > 4194304) {
    throw new Error('Use dimensions from 1 to 16384 with at most 4,194,304 pixels for native preview.');
  }
  if (!['rectangle', 'square', 'circle'].includes(value.shape)) throw new Error('Choose a supported display shape.');
  if (value.shape !== 'rectangle' && width !== height) throw new Error('Square and circular displays require equal pixel dimensions.');
  const formats = { rgb565: [16, 16], rgb888: [24, 24], xrgb8888: [24, 32], argb8888: [32, 32] };
  const bits = formats[value.format];
  if (!bits) throw new Error('Choose a supported RGB framebuffer format.');
  const transfers = { rgb565: [16, 16], rgb888Parallel: [24, 24], argb8888: [32, 32] };
  const native = transfers[value.transfer];
  if (!native) throw new Error('Choose the display transfer format from its specification.');
  const physicalWidth = Number(value.physicalWidth), physicalHeight = Number(value.physicalHeight);
  const circle = value.shape === 'circle';
  if (![physicalWidth, ...(circle ? [] : [physicalHeight])].every(n => Number.isFinite(n) && n > 0 && n <= 2000)) {
    throw new Error('Enter the active display dimensions in millimeters (greater than zero, at most 2000).');
  }
  const inset = circle ? Math.ceil((width - Math.floor(width / Math.SQRT2)) / 2) : 0;
  let safeArea = { x: inset, y: inset, width: width - 2 * inset, height: height - 2 * inset };
  if (value.safeArea?.trim()) {
    const values = value.safeArea.split(',').map(part => Number(part.trim()));
    if (values.length !== 4 || !values.every(Number.isInteger)) throw new Error('Safe area uses x,y,width,height in pixels.');
    safeArea = Object.fromEntries(['x', 'y', 'width', 'height'].map((key, index) => [key, values[index]]));
  }
  return {
    $schema: 'https://litheview.dev/schema/display-surface.schema.json', schemaVersion: 1,
    id: `${id}.surface`, displayName: name, compatibilityFamily: `${id}.display-family`, shape: value.shape,
    physicalSize: circle ? { diameterMillimeters: physicalWidth } : { widthMillimeters: physicalWidth, heightMillimeters: physicalHeight },
    pixelSize: { width, height }, coordinateSystem: { origin: 'topLeft', rotationDegrees: 0 },
    painterSurface: { format: value.format, colorDepthBits: bits[0], storageBitsPerPixel: bits[1] },
    nativeDisplay: { colorModel: 'rgb', colorDepthBits: native[0], transferUnitBits: native[1], transferFormat: value.transfer },
    safeArea, inputCapabilities: [],
    provenance: {
      manufacturer: required(value.manufacturer, 'Manufacturer'), model: required(value.model, 'Display model'),
      sourceUrl: required(value.sourceUrl, 'Datasheet HTTPS URL'), sourceDocument: required(value.sourceDocument, 'Datasheet title/revision'),
      sourceSha256: null, notes: 'Declared by the application developer in VS Code; hardware verification is separate.'
    }
  };
}

// Paths shown in the wizard are relative to the chosen application workspace.
// The shared service resolves them again and checks protected inputs, links and file ownership.
function customNativeDisplay(value) {
  const width = Number(value.width), height = Number(value.height);
  const scale = value.format === 'r2Packed2x2' ? 1 : 2;
  if (![width, height].every(n => Number.isInteger(n) && n > 0 && n <= 4096) || width * height * scale * scale > 4194304) {
    throw new Error('Use logical dimensions up to 4096 with at most 4,194,304 presented emitters.');
  }
  if (value.format === 'r2Packed2x2' && (width % 2 || height % 2)) throw new Error('R2 stores 2 × 2 emitters per byte; width and height must be even.');
  if (!['rectangle', 'square', 'circle'].includes(value.shape) || (value.shape !== 'rectangle' && width !== height)) {
    throw new Error('Choose a display shape; square and circular displays need equal logical dimensions.');
  }
  let safeArea;
  if (value.safeArea?.trim()) {
    const parts = value.safeArea.split(',').map(part => Number(part.trim()));
    if (parts.length !== 4 || !parts.every(Number.isInteger)) throw new Error('Safe area uses x,y,width,height in logical coordinates.');
    safeArea = Object.fromEntries(['x', 'y', 'width', 'height'].map((key, index) => [key, parts[index]]));
  }
  return { format: value.format, width, height, shape: value.shape, safeArea,
    sourceReference: required(value.sourceReference, 'Display specification reference') };
}

function creationRequest(values, applicationRoot, surfaces, nativeDisplays = []) {
  if (!values || typeof values !== 'object') throw new Error('Enter the UI project settings.');
  const name = required(values.name, 'UI name');
  const id = projectId(name);
  const directory = path.resolve(applicationRoot, required(values.projectDirectory, 'UI source folder'));
  if (!isInside(directory, applicationRoot)) throw new Error('The UI source folder must be inside this application workspace.');
  const request = {
    applicationRoot, projectDirectory: directory, displayName: name, projectId: id,
    logicalInput: values.logicalInput,
    componentDirectory: required(values.componentDirectory, 'Generated component folder'),
    buildDirectory: required(values.buildDirectory ?? 'build', 'Build folder')
  };
  if (!['outputOnly', 'touch', 'touchKeys', 'encoderSelect'].includes(values.logicalInput)) throw new Error('Choose a logical input configuration.');
  if (values.surfaceId === 'custom') {
    if (['r2Packed2x2', 'rrrr2222', 'ryry2222'].includes(values.custom?.format)) {
      request.customNativeDisplay = customNativeDisplay(values.custom);
    } else request.customSurface = customSurface(values.custom, id, name + ' display');
  } else if (values.surfaceId?.startsWith('native:')) {
    const nativeId = values.surfaceId.slice('native:'.length);
    if (!nativeDisplays.some(display => display.id === nativeId)) throw new Error('Choose a native display from the current catalog.');
    request.nativeDisplayId = nativeId;
  }
  else {
    if (!surfaces.some(surface => surface.id === values.surfaceId)) throw new Error('Choose a display from the current catalog.');
    request.surfaceId = values.surfaceId;
  }
  return request;
}

function renderNewProject({ scriptUri, styleUri, cspSource, nonce, setup = false, frontScriptUri = '' }) {
  const escape = value => String(value).replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch]);
  return `<!doctype html><html lang="en"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="${escape(`default-src 'none'; img-src data:; style-src ${cspSource}; script-src 'nonce-${nonce}'; base-uri 'none'; form-action 'none';`)}">
<link rel="stylesheet" href="${escape(styleUri)}"><title>${setup ? 'Configure LiTheView UI' : 'Add LiTheView UI'}</title></head><body>
<main><h1>${setup ? 'Configure LiTheView UI' : 'Add LiTheView UI'}</h1>
<p>${setup ? 'Review or change this project’s display and logical input. Apply settings, Generate UI, then build and debug with your existing application configurations.' : 'Define your display and start with a blank canvas. Design the form here, then build and debug your application on the PC and hardware.'}</p>
<p id="location" class="hint"></p>
<form id="project"><fieldset id="fields" disabled>
<section ${setup ? 'hidden' : ''}><h2>1. Application paths</h2><div class="grid">
<label>UI name<input name="name" value="Control Panel" required maxlength="100"></label>
<label>UI source folder<input name="projectDirectory" value="Tasks/UI" required></label>
<label>Generated component folder<input name="componentDirectory" value="Tasks/UI/Generated" required></label>
</div><p class="hint">Folders are relative to your application root. Existing application files are preserved; conflicting UI files stop creation.</p></section>
<section><h2>${setup ? 'Display' : '2. Display'}</h2><label ${setup ? 'hidden' : ''}>Display configuration<select name="surfaceId" id="surface"></select></label>
<p id="surface-description" class="hint"></p>
<div id="custom" hidden><div class="grid">
<label>Shape<select name="shape"><option value="rectangle">Rectangle</option><option value="square">Square</option><option value="circle">Circle</option></select></label>
<label>Native storage format<select name="format" id="format"><option value="rgb565">RGB565 (16 bits)</option><option value="rgb888">RGB888 (24 bits)</option><option value="xrgb8888">XRGB8888 (32 bits)</option><option value="argb8888">ARGB8888 (32 bits)</option><option value="r2Packed2x2">R2 — individual red emitters (2 bits)</option><option value="rrrr2222">RRRR2222 — red 2 × 2 cells (8 bits)</option><option value="ryry2222">RYRY2222 — red/yellow 2 × 2 cells (8 bits)</option></select></label>
<label><span id="width-label">Width (pixels)</span><input name="width" type="number" value="480" min="1" max="16384"></label>
<label><span id="height-label">Height (pixels)</span><input name="height" type="number" value="272" min="1" max="16384"></label>
<label class="rgb-field">Active width / circle diameter (mm)<input name="physicalWidth" type="number" min="0.001" max="2000" step="any"></label>
<label class="rgb-field">Active height (mm, rectangles/squares)<input name="physicalHeight" type="number" min="0.001" max="2000" step="any"></label>
<label class="rgb-field">Panel transfer format<select name="transfer"><option value="rgb565">RGB565</option><option value="rgb888Parallel">RGB888 parallel</option><option value="argb8888">ARGB8888</option></select></label>
<label>Safe area (optional x,y,width,height)<input name="safeArea" placeholder="Default: full rectangle or inset square for a circle"></label>
<label class="rgb-field">Manufacturer<input name="manufacturer"></label><label class="rgb-field">Display model<input name="model"></label>
<label class="rgb-field">Datasheet HTTPS URL<input name="sourceUrl" type="url" placeholder="https://..."></label>
<label class="rgb-field">Datasheet title / revision<input name="sourceDocument" maxlength="240"></label>
<label id="native-reference" hidden>Display specification reference<input name="sourceReference" maxlength="1024" placeholder="Manufacturer, model, datasheet revision and page or document path"></label>
</div><p id="format-description" class="hint"></p><p class="hint">Coordinates start at the top left, with zero rotation. Safe area uses the same logical coordinates as the form. Declare specifications from your display documentation. These fields do not configure pins, buses, clocks or the MCU.</p></div></section>
<section><h2>${setup ? 'Input' : '3. Input'}</h2><label>Application input<select name="logicalInput"><option value="outputOnly">Output only — first display check</option><option value="touch">Touch</option><option value="touchKeys">Touch + keys</option><option value="encoderSelect">Encoder + select button</option>${setup ? '<option value="custom">Custom capabilities</option>' : ''}</select></label>
${setup ? `<div class="grid" id="input-capabilities" hidden>
<label class="check"><input type="checkbox" name="pointer">Touch / pointer (PC mouse)</label>
<label class="check"><input type="checkbox" name="keys">Navigation keys</label>
<label class="check"><input type="checkbox" name="text">Text input</label>
<label class="check"><input type="checkbox" name="encoder">Rotary encoder</label>
<label class="check"><input type="checkbox" name="select">Select button</label>
</div>` : ''}
<p class="hint">PC mouse and keyboard input stand in for the selected devices. Hardware drivers are connected later.</p></section>
${setup ? `<section><h2>PC front view (optional)</h2>
<p>Use a board or enclosure image for your application's simulator. Mark the LCD area and the physical push-buttons that the PC mouse should operate.</p>
<button type="button" id="front-browse" class="secondary">Choose front-view PNG…</button>
<div id="front-content" hidden>
<p id="front-image-name" class="hint"></p>
<label class="check"><input type="checkbox" id="front-enabled">Show front view in the PC application</label>
<div class="grid">
<label>PC window width (pixels)<input id="front-window-width" type="number" min="160" max="4096"></label>
<label>Area to mark<select id="front-region"></select></label>
<label>Physical button name<input id="front-button-id" value="userButton" maxlength="48"></label>
<div><button type="button" id="front-add" class="secondary">Add push-button</button><button type="button" id="front-remove" class="secondary">Remove selected button</button></div>
</div>
<p class="hint">Select an area, then drag its rectangle on the image. Coordinates below are original image pixels, independent of the preview zoom.</p>
<canvas id="front-canvas" aria-label="Draw LCD and physical push-button areas on the front image"></canvas>
<div class="grid front-coordinates">
<label>X<input id="front-x" type="number" min="0"></label><label>Y<input id="front-y" type="number" min="0"></label>
<label>Width<input id="front-width" type="number" min="1"></label><label>Height<input id="front-height" type="number" min="1"></label>
</div>
<p class="hint">The photo and hit regions are PC-only. Your application maps a button name to its typed device input and supplies the matching GPIO adapter on hardware. These buttons are independent of touch and UI focus/select.</p>
</div></section>` : ''}
<section><h2>${setup ? 'Save configuration' : '4. Create the blank form'}</h2>
${setup ? `<p>Apply updates the display and input settings for this UI project. Return to your form, Generate UI, then build and debug as usual.</p><p class="hint">Form coordinates stay as authored. After changing display dimensions, adjust the root panel and controls to fit. Assets and application adapters must match the selected display.</p>` : `<p>Creates editable form, display/input profiles and path settings. Add your controls and properties, save, then use Generate UI to produce the C++ component. Build and debug with your application's existing configurations.</p>`}
<button type="submit">${setup ? 'Apply' : 'Create UI'}</button><button type="button" id="cancel" class="secondary">Cancel</button></section>
</fieldset></form><p id="status" role="status" aria-live="polite"></p></main>
${setup ? `<script nonce="${escape(nonce)}" src="${escape(frontScriptUri)}"></script>` : ''}
<script nonce="${escape(nonce)}" src="${escape(scriptUri)}"></script></body></html>`;
}

module.exports = { projectId, customSurface, customNativeDisplay, creationRequest, renderNewProject };
