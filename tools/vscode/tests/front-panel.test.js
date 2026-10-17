// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { frontPanelPath, readFrontPanel, chooseFrontImage, frontPanelEdit, validateFrontPanel } = require('../lib/front-panel');

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'lithe-front-'));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  await fs.mkdir(path.join(root, 'Tasks/UI'), { recursive: true });
  await fs.copyFile(path.resolve(__dirname, '../../../examples/stm32h750b-dk-starter-1/board-front.png'), path.join(root, 'board front.png'));
  const project = { applicationRoot: root, manifestPath: path.join(root, 'Tasks/UI/project.litheview.json') };
  const { dataUrl, ...image } = await chooseFrontImage({}, project, 'board front.png');
  assert.match(dataUrl, /^data:image\/png;base64,/);
  const value = { schemaVersion: 1, enabled: true, image, windowWidth: 905,
    display: { x: 232, y: 166, width: 1386, height: 812 },
    buttons: [{ id: 'userButton', bounds: { x: 344, y: 1140, width: 84, height: 80 } }] };
  return { project, value, root };
}

test('front-view edit is a proposal, uses application-relative paths and preserves unchanged bytes', async t => {
  const { project, value } = await fixture(t);
  const empty = await readFrontPanel(project);
  const edit = await frontPanelEdit(project, empty, value);
  assert.equal(edit.before, null);
  await assert.rejects(fs.access(edit.path), { code: 'ENOENT' });
  await fs.writeFile(edit.path, edit.after.replace(/\n/g, '\r\n'));
  const current = await readFrontPanel(project);
  assert.deepEqual(current.value, value);
  const unchanged = await frontPanelEdit(project, current, value);
  assert.equal(unchanged.after, current.before);
  const changed = structuredClone(value); changed.buttons[0].bounds.x++;
  assert.notEqual((await frontPanelEdit(project, current, changed)).after, current.before);
  assert.equal(await fs.readFile(edit.path, 'utf8'), current.before);
});

test('overlap, invalid coordinates, duplicate or reserved IDs and oversized windows are rejected', async t => {
  const { value } = await fixture(t);
  for (const mutate of [v => { v.display.width = 2000; }, v => { v.buttons[0].bounds.x = -1; },
    v => { v.buttons[0].bounds = v.display; }, v => { v.buttons.push(v.buttons[0]); },
    v => { v.buttons[0].id = 'display'; }, v => { v.buttons[0].id = undefined; },
    v => { v.windowWidth = 5000; }, v => { v.display.x = 0.5; }]) {
    const invalid = structuredClone(value); mutate(invalid);
    assert.throws(() => validateFrontPanel(invalid));
  }
});

test('image replacement must be selected again and a missing image leaves Configure repairable', async t => {
  const { project, value, root } = await fixture(t);
  const initial = await readFrontPanel(project);
  await fs.writeFile(frontPanelPath(project), (await frontPanelEdit(project, initial, value)).after);
  await fs.appendFile(path.join(root, value.image.path), Buffer.from([0]));
  await assert.rejects(frontPanelEdit(project, initial, value), /image changed/);
  await fs.unlink(path.join(root, value.image.path));
  const missing = await readFrontPanel(project);
  assert.match(missing.warning, /Choose the front-view PNG again/);
  assert.deepEqual(missing.value, value);
});

test('front-view image paths stay inside the application and non-PNG input is rejected', async t => {
  const { project, root } = await fixture(t);
  await assert.rejects(chooseFrontImage({}, project, '../escape.png'), /inside the application/);
  await assert.rejects(chooseFrontImage({}, project, path.join(root, 'board front.png')), /inside the application/);
  await fs.writeFile(path.join(root, 'not.png'), 'Not a PNG');
  await assert.rejects(chooseFrontImage({}, project, 'not.png'), /PNG image/);
});
