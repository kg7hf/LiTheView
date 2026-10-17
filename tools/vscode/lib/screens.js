// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const fs = require('node:fs/promises');
const path = require('node:path');
const { isInside, samePath, submoduleRoots } = require('./project-paths');
const ignored = new Set(['.git', 'build', 'out', 'node_modules', 'generated', 'bin', 'obj']);
async function json(file) {
  if ((await fs.stat(file)).size > 1024 * 1024) throw new Error('Manifest exceeds 1 MiB.');
  return JSON.parse((await fs.readFile(file, 'utf8')).replace(/^\uFEFF/, ''));
}
// Discovery is metadata-only, bounded and independent of the native service.
async function discoverScreens(project, workspaceRoot) {
  const root = await fs.realpath(project.applicationRoot);
  const workspace = await fs.realpath(workspaceRoot);
  if (!isInside(root, workspace)) throw new Error('Open the application root to browse its screens.');
  const excluded = [...await submoduleRoots(root), project.libraryRoot]
    .filter(dir => dir && isInside(dir, root) && !samePath(dir, root));
  const screens = new Map();
  const inspected = new Set();
  const warnings = [];
  let visited = 0;
  async function manifest(file) {
    if (inspected.has(file)) return;
    inspected.add(file);
    try {
      const data = await json(file);
      if (!Array.isArray(data.forms)) return;
      const directory = path.dirname(file);
      const sidecar = file.replace(/\.json$/i, '.paths.json');
      let settings;
      try { settings = await json(sidecar); } catch (error) { if (error.code !== 'ENOENT') throw error; }
      const owner = settings ? await fs.realpath(path.resolve(directory, settings.applicationRoot)) : directory;
      if (!samePath(owner, root) && !samePath(file, project.manifestPath)) return;
      for (const form of data.forms) {
        try {
        if (typeof form !== 'string' || !form.endsWith('.litheview.xml')) continue;
        const candidate = path.resolve(directory, form);
        if (!isInside(candidate, root)) continue;
        const resolved = await fs.realpath(candidate);
        if (!isInside(resolved, root) || excluded.some(dir => dir && isInside(resolved, dir))) continue;
        if (!(await fs.stat(resolved)).isFile()) continue;
        const relative = path.relative(root, resolved).split(path.sep).join('/');
        const name = path.basename(resolved, '.litheview.xml');
        screens.set(resolved, { id: relative, label: name.toLowerCase() === 'main' && typeof data.displayName === 'string' ? data.displayName : name,
          detail: relative, formPath: resolved });
        } catch (error) { warnings.push(`${form}: ${error.message}`); }
      }
    } catch (error) { warnings.push(`${path.relative(root, file)}: ${error.message}`); }
  }
  async function walk(directory, depth) {
    if (depth > 16) { warnings.push('Screen search reached its folder-depth limit.'); return; }
    for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
      if (++visited > 20000) throw new Error('Screen search exceeded 20,000 entries; open the application folder.');
      const file = path.join(directory, entry.name);
      if (entry.isSymbolicLink()) continue;
      if (entry.isFile() && entry.name === 'project.litheview.json') await manifest(file);
      if (!entry.isDirectory() || ignored.has(entry.name.toLowerCase()) || entry.name.startsWith('.')) continue;
      if (excluded.some(dir => dir && isInside(file, dir))) continue;
      try { await fs.lstat(path.join(file, '.git')); continue; } catch (error) { if (error.code !== 'ENOENT') throw error; }
      await walk(file, depth + 1);
    }
  }
  await manifest(project.manifestPath);
  try { await walk(root, 0); } catch (error) { warnings.push(error.message); }
  return { screens: [...screens.values()].sort((a, b) => a.label.localeCompare(b.label) || a.detail.localeCompare(b.detail)), warnings };
}
module.exports = { discoverScreens };
