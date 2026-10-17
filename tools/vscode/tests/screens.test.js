// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const { discoverScreens } = require('../lib/screens');
async function setup(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'litheview-screens-'));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  async function page(folder, names, applicationRoot = '../..') {
    const dir = path.join(root, folder);
    await fs.mkdir(path.join(dir, 'forms'), { recursive: true });
    for (const name of names) await fs.writeFile(path.join(dir, 'forms', name + '.litheview.xml'), '<panel/>');
    await fs.writeFile(path.join(dir, 'project.litheview.json'), JSON.stringify({ displayName: 'Legacy Home', forms: names.map(n => 'forms/' + n + '.litheview.xml') }));
    await fs.writeFile(path.join(dir, 'project.litheview.paths.json'), JSON.stringify({ applicationRoot }));
    return path.join(dir, 'project.litheview.json');
  }
  const manifestPath = await page('Tasks/UI', ['Home']);
  const project = { applicationRoot: root, libraryRoot: path.join(root, 'external/library'), manifestPath };
  return { root, page, project };
}
test('screens group sibling projects and multiple forms using application metadata', async t => {
  const f = await setup(t);
  await f.page('custom/Settings', ['Settings', 'Advanced']);
  const result = await discoverScreens(f.project, f.root);
  assert.deepEqual(result.screens.map(s => s.label), ['Advanced', 'Home', 'Settings']);
  assert.deepEqual(result.warnings, []);
  assert.equal(result.screens[0].detail, 'custom/Settings/forms/Advanced.litheview.xml');
});
test('dependencies, generated outputs, unrelated applications and unlisted XML stay out', async t => {
  const f = await setup(t);
  await f.page('external/library', ['Library']);
  await f.page('devices/submodule', ['Vendor']);
  await fs.writeFile(path.join(f.root, '.gitmodules'), '[submodule "device"]\n path = devices/submodule\n');
  await f.page('build/cache', ['Old']);
  await f.page('Tasks/Generated', ['Generated']);
  await f.page('other/application', ['Other'], '.');
  await fs.writeFile(path.join(f.root, 'Tasks/UI/forms/Unlisted.litheview.xml'), '<panel/>');
  assert.deepEqual((await discoverScreens(f.project, f.root)).screens.map(s => s.label), ['Home']);
});
test('legacy names remain compatible; refresh observes renames and malformed projects are isolated', async t => {
  const f = await setup(t);
  await f.page('Tasks/Legacy', ['main']);
  const bad = await f.page('Tasks/Bad', ['Bad']); await fs.writeFile(bad, '{');
  let result = await discoverScreens(f.project, f.root);
  assert.deepEqual(result.screens.map(s => s.label), ['Home', 'Legacy Home']);
  assert.equal(result.warnings.length, 1);
  await fs.rename(path.join(f.root, 'Tasks/UI/forms/Home.litheview.xml'), path.join(f.root, 'Tasks/UI/forms/New.litheview.xml'));
  await fs.writeFile(f.project.manifestPath, JSON.stringify({ forms: ['forms/New.litheview.xml'] }));
  result = await discoverScreens(f.project, f.root);
  assert.deepEqual(result.screens.map(s => s.label), ['Legacy Home', 'New']);
});
test('application roots above the open workspace are rejected', async t => {
  const f = await setup(t);
  await assert.rejects(discoverScreens(f.project, path.join(f.root, 'Tasks')), /Open the application root/);
});
test('out-of-root manifest paths cannot become navigation targets', async t => {
  const f = await setup(t);
  await fs.writeFile(f.project.manifestPath, JSON.stringify({ forms: ['../../../../outside.litheview.xml'] }));
  assert.deepEqual((await discoverScreens(f.project, f.root)).screens, []);
});

test('an external library containing the test application does not hide its screens', async t => {
  const f = await setup(t);
  f.project.libraryRoot = path.dirname(f.root);
  assert.deepEqual((await discoverScreens(f.project, f.root)).screens.map(s => s.label), ['Home']);
});

test('one missing form cannot hide the remaining registered screens', async t => {
  const f = await setup(t);
  await fs.writeFile(f.project.manifestPath, JSON.stringify({ forms: ['forms/Missing.litheview.xml', 'forms/Home.litheview.xml'] }));
  const result = await discoverScreens(f.project, f.root);
  assert.deepEqual(result.screens.map(s => s.label), ['Home']);
  assert.equal(result.warnings.length, 1);
  assert.match(result.warnings[0], /Missing/);
});

test('the active project is discovered even in a normally ignored folder', async t => {
  const f = await setup(t);
  f.project.manifestPath = await f.page('out/UI', ['Active']);
  const result = await discoverScreens(f.project, f.root);
  assert.deepEqual(result.screens.map(s => s.label), ['Active', 'Home']);
});
