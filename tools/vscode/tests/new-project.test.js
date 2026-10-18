// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { creationRequest, customSurface, renderNewProject } = require('../lib/new-project');

const root = path.resolve('test application');
const surfaces = [{ id: 'test.h750', name: 'H750 RGB565' }];
const values = { name: 'First light', projectDirectory: 'Tasks/UI', componentDirectory: 'Tasks/UI/Generated',
  buildDirectory: 'out/simulator', surfaceId: 'test.h750', logicalInput: 'outputOnly' };
const custom = { width: 240, height: 240, shape: 'circle', format: 'rgb565', transfer: 'rgb888Parallel',
  physicalWidth: 32, manufacturer: 'Fixture', model: 'Round', sourceUrl: 'https://example.test/display', sourceDocument: 'Fixture revision 1' };

test('creation paths use the selected application root and select input independently', () => {
  const request = creationRequest(values, root, surfaces);
  assert.equal(request.projectDirectory, path.join(root, 'Tasks/UI'));
  assert.equal(request.applicationRoot, root);
  assert.equal(request.logicalInput, 'outputOnly');
  assert.equal(request.projectId, 'litheview.project.first-light');
  assert.equal(request.componentDirectory, 'Tasks/UI/Generated');
  assert.equal(request.customSurface, undefined);
  assert.equal(creationRequest({ ...values, buildDirectory: undefined }, root, surfaces).buildDirectory, 'build');
});

test('invalid wizard input never becomes a creation request', () => {
  for (const change of [{ projectDirectory: '../outside' }, { name: '---' }, { surfaceId: 'absent' },
    { logicalInput: 'magic' }, { componentDirectory: '' }, { buildDirectory: '\n' }]) {
    assert.throws(() => creationRequest({ ...values, ...change }, root, surfaces));
  }
});

test('custom display keeps framebuffer and panel transfer formats independent', () => {
  const profile = customSurface(custom, 'test.custom', 'Round display');
  assert.equal(profile.painterSurface.storageBitsPerPixel, 16);
  assert.equal(profile.nativeDisplay.colorDepthBits, 24);
  assert.equal(profile.nativeDisplay.transferFormat, 'rgb888Parallel');
  assert.equal(profile.physicalSize.diameterMillimeters, 32);
  assert.deepEqual(profile.inputCapabilities, []);
  const safe = profile.safeArea;
  assert.ok(Math.hypot(safe.width / 2, safe.height / 2) < 120, 'border box fits inside the visible circle');
  assert.equal(profile.provenance.sourceSha256, null, 'no invented evidence hash');
});

test('custom geometry, preview bounds, formats and provenance must be explicit', () => {
  for (const change of [{ width: 0 }, { height: 239 }, { width: 8192, height: 8192 }, { format: 'mono' },
    { transfer: 'SPI' }, { physicalWidth: '' }, { manufacturer: '' }, { safeArea: '0,0,10' }]) {
    assert.throws(() => customSurface({ ...custom, ...change }, 'test.custom', 'Round'));
  }
  const request = creationRequest({ ...values, surfaceId: 'custom', custom }, root, surfaces);
  assert.equal(request.surfaceId, undefined);
  assert.equal(request.customSurface.coordinateSystem.rotationDegrees, 0);
});

test('wizard is local, uses a restrictive CSP, and creates no markup from project data', () => {
  const html = renderNewProject({ scriptUri: 'local/script.js', styleUri: 'local/style.css', cspSource: 'webview:', nonce: 'abc' });
  assert.match(html, /default-src &#39;none&#39;/);
  assert.match(html, /nonce="abc"/);
  assert.match(html, /Create UI/);
  assert.match(html, /Output only/);
  assert.match(html, /blank canvas/);
  assert.doesNotMatch(html, /name="buildDirectory"|Run UI Starter/);
  assert.doesNotMatch(html, /(?:src|href)="https?:\/\//);
});

test('native wizard uses explicit emitter or cell extents and separate input and paths', () => {
  for (const format of ['r2Packed2x2', 'rrrr2222', 'ryry2222']) {
    const request = creationRequest({ ...values, surfaceId: 'custom', logicalInput: 'encoderSelect', custom: {
      format, width: 24, height: 16, shape: 'rectangle', sourceReference: 'Panel spec revision 1'
    } }, root, surfaces);
    assert.equal(request.customSurface, undefined);
    assert.equal(request.surfaceId, undefined);
    assert.deepEqual(request.customNativeDisplay, { format, width: 24, height: 16, shape: 'rectangle',
      sourceReference: 'Panel spec revision 1', safeArea: undefined });
    assert.equal(request.logicalInput, 'encoderSelect');
    assert.equal(request.componentDirectory, 'Tasks/UI/Generated');
  }
  const request = creationRequest({ ...values, surfaceId: 'native:test.cells' }, root, surfaces, [{ id: 'test.cells' }]);
  assert.equal(request.nativeDisplayId, 'test.cells');
  assert.equal(request.surfaceId, undefined);
  assert.throws(() => creationRequest({ ...values, surfaceId: 'native:unknown' }, root, surfaces));
});

test('native creation rejects incomplete blocks and presentation sizes before requesting publication', () => {
  const native = { format: 'r2Packed2x2', width: 24, height: 16, shape: 'rectangle', sourceReference: 'Panel spec' };
  for (const change of [{ width: 23 }, { height: 15 }, { width: 0 }, { shape: 'circle' }, { sourceReference: '' },
    { width: 4096, height: 4096, format: 'rrrr2222' }, { safeArea: '0,0,24' }]) {
    assert.throws(() => creationRequest({ ...values, surfaceId: 'custom', custom: { ...native, ...change } }, root, surfaces));
  }
});


test('segmented creation is independent of raster catalog and requires bounded glass and explicit input', () => {
  const next = { ...values, displayKind: 'segmented', surfaceId: undefined, glass: '{"schemaVersion":1}', logicalInput: 'custom',
    input: { pointer: false, keys: true, text: false, encoder: true, select: true } };
  const request = creationRequest(next, root, []);
  assert.deepEqual(request.segmentGlass, { schemaVersion: 1 });
  assert.deepEqual(request.input, next.input);
  assert.equal(request.surfaceId, undefined);
  assert.equal(request.customSurface, undefined);
  assert.equal(request.componentDirectory, values.componentDirectory);
  for (const change of [{ glass: undefined }, { glass: '{' }, { glass: ' '.repeat(1048577) }, { input: {} }, { displayKind: 'unknown' }]) {
    assert.throws(() => creationRequest({ ...next, ...change }, root, []));
  }
  const html = renderNewProject({ scriptUri: 'script', styleUri: 'style', cspSource: 'webview:', nonce: 'abc' });
  assert.match(html, /Segmented display/);
  assert.match(html, /Import glass JSON/);
  assert.match(html, /glass-preview/);
});
