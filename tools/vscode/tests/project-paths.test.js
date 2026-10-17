// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { resolveProject, findManifest, readManifestForms, submoduleRoots, defaultPreviewWorker } = require('../lib/project-paths');

async function setup(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'litheview-host-'));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const project = path.join(root, 'Application with spaces');
  const ui = path.join(project, 'Tasks', 'UI');
  const form = path.join(ui, 'forms', 'main.litheview.xml');
  const library = path.join(project, 'custom', 'third party', 'LiTheView');
  const manifest = path.join(ui, 'project.litheview.json');
  await fs.mkdir(path.dirname(form), { recursive: true });
  await fs.writeFile(form, '<form/>');
  await fs.writeFile(manifest, JSON.stringify({ forms: ['forms/main.litheview.xml'] }));
  await fs.writeFile(path.join(ui, 'project.litheview.paths.json'), JSON.stringify({ schemaVersion: 1, applicationRoot: '../..', libraryRoot: 'custom/third party/LiTheView', componentDirectory: 'Tasks/UI/Generated', buildDirectory: 'build/ui' }));
  return { root, project, ui, form, library, manifest };
}

test('application paths are resolved against the sidecar rather than cwd', async t => {
  const f = await setup(t);
  const result = await resolveProject(f.form, f.project);
  assert.equal(result.applicationRoot, f.project);
  assert.equal(result.libraryRoot, f.library);
  assert.equal(result.servicePath, path.join(f.library, 'build', 'vscode-designer', 'service', 'LitheView.Designer.Service.dll'));
  assert.equal(result.manifestPath, f.manifest);
});

test('nearest manifest must list the form', async t => {
  const f = await setup(t);
  await fs.writeFile(path.join(f.ui, 'forms', 'project.litheview.json'), JSON.stringify({ forms: ['unrelated.xml'] }));
  assert.equal(await findManifest(f.form, f.project), f.manifest);
  await fs.writeFile(f.manifest, JSON.stringify({ forms: [] }));
  await assert.rejects(findManifest(f.form, f.project), /lists this form/);
});

test('explicit overrides are anchored to workspace and library', async t => {
  const f = await setup(t);
  const result = await resolveProject(f.form, f.project, { libraryRoot: '../Other library', servicePath: 'artifacts/service.dll', previewWorkerPath: 'artifacts/worker.exe', dotnetPath: 'custom-dotnet' });
  assert.equal(result.libraryRoot, path.resolve(f.project, '../Other library'));
  assert.equal(result.servicePath, path.join(result.libraryRoot, 'artifacts', 'service.dll'));
  assert.equal(result.dotnetPath, 'custom-dotnet');
});

test('malformed sidecar fails rather than silently using defaults', async t => {
  const f = await setup(t);
  await fs.writeFile(path.join(f.ui, 'project.litheview.paths.json'), '{');
  await assert.rejects(resolveProject(f.form, f.project), SyntaxError);
});

test('picker membership and submodule roots come from application metadata', async t => {
  const f = await setup(t);
  await fs.writeFile(path.join(f.project, '.gitmodules'), '[submodule "display"]\n path = custom/third party/LiTheView\n url = example\n');
  assert.deepEqual(await readManifestForms(f.manifest), [f.form]);
  assert.deepEqual(await submoduleRoots(f.project), [f.library]);
});

test('a form outside the application workspace is rejected', async t => {
  const f = await setup(t);
  await assert.rejects(findManifest(f.form, path.join(f.root, 'other')), /open application workspace/);
});

test('the default preview worker follows the platform build preset', () => {
  assert.equal(defaultPreviewWorker('win32'), 'build/windows-mingw-debug/litheview_designer_preview_worker.exe');
  assert.equal(defaultPreviewWorker('linux'), 'build/host-debug/litheview_designer_preview_worker');
  assert.equal(defaultPreviewWorker('darwin'), 'build/host-debug/litheview_designer_preview_worker');
});
