// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const fs = require('node:fs/promises');
const path = require('node:path');

function samePath(first, second, platform = process.platform) {
  const a = path.resolve(first);
  const b = path.resolve(second);
  return platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b;
}

function isInside(file, directory) {
  const relative = path.relative(directory, file);
  return relative === '' || (!path.isAbsolute(relative) && relative !== '..' && !relative.startsWith(`..${path.sep}`));
}

async function readJson(file) {
  const stat = await fs.stat(file);
  if (stat.size > 1024 * 1024) throw new Error(`Project file exceeds 1 MiB: ${file}`);
  return JSON.parse((await fs.readFile(file, 'utf8')).replace(/^\uFEFF/, ''));
}

async function exists(file) {
  try { await fs.access(file); return true; } catch { return false; }
}

async function findManifest(formPath, workspaceRoot) {
  let directory = path.dirname(path.resolve(formPath));
  const root = path.resolve(workspaceRoot);
  if (!isInside(formPath, root)) throw new Error('The form must belong to an open application workspace.');
  while (isInside(directory, root)) {
    const candidate = path.join(directory, 'project.litheview.json');
    if (await exists(candidate)) {
      const manifest = await readJson(candidate);
      if (Array.isArray(manifest.forms) && manifest.forms.some(form => typeof form === 'string' && samePath(path.resolve(directory, form), formPath))) return candidate;
    }
    if (samePath(directory, root)) break;
    directory = path.dirname(directory);
  }
  throw new Error('No ancestor project.litheview.json lists this form. Add the form to its application project manifest.');
}

async function findLibraryRoot(directory) {
  let candidate = path.resolve(directory);
  while (true) {
    if (await exists(path.join(candidate, 'tools', 'designer', 'src', 'LitheView.Designer.Core', 'LitheView.Designer.Core.csproj'))) return candidate;
    const parent = path.dirname(candidate);
    if (parent === candidate) return undefined;
    candidate = parent;
  }
}

// The prepare task builds the worker with windows-mingw-debug on Windows and host-debug elsewhere.
function defaultPreviewWorker(platform) {
  return platform === 'win32'
    ? 'build/windows-mingw-debug/litheview_designer_preview_worker.exe'
    : 'build/host-debug/litheview_designer_preview_worker';
}

async function resolveProject(formPath, workspaceRoot, overrides = {}) {
  const manifestPath = await findManifest(formPath, workspaceRoot);
  const directory = path.dirname(manifestPath);
  const settingsPath = manifestPath.replace(/\.json$/i, '.paths.json');
  const settings = await exists(settingsPath) ? await readJson(settingsPath) : undefined;
  if (settings && settings.schemaVersion !== 1) throw new Error('project.litheview.paths.json requires schemaVersion 1.');
  if (settings && ['applicationRoot', 'libraryRoot', 'componentDirectory', 'buildDirectory'].some(key => typeof settings[key] !== 'string' || !settings[key].trim())) {
    throw new Error('The project paths sidecar requires applicationRoot, libraryRoot, componentDirectory and buildDirectory.');
  }
  const applicationRoot = path.resolve(directory, settings?.applicationRoot || '.');
  const libraryRoot = overrides.libraryRoot
    ? path.resolve(workspaceRoot, overrides.libraryRoot)
    : settings ? path.resolve(applicationRoot, settings.libraryRoot) : await findLibraryRoot(directory);
  if (!libraryRoot) throw new Error('Set libraryRoot in project.litheview.paths.json or the LiTheView workspace settings.');
  const servicePath = path.resolve(libraryRoot, overrides.servicePath || 'build/vscode-designer/service/LitheView.Designer.Service.dll');
  const previewWorkerPath = path.resolve(libraryRoot, overrides.previewWorkerPath || defaultPreviewWorker(overrides.platform || process.platform));
  return { manifestPath, formPath: path.resolve(formPath), applicationRoot, libraryRoot, servicePath, previewWorkerPath, dotnetPath: overrides.dotnetPath || 'dotnet' };
}

async function readManifestForms(manifestPath) {
  const manifest = await readJson(manifestPath);
  if (!Array.isArray(manifest.forms)) return [];
  const directory = path.dirname(manifestPath);
  return manifest.forms.filter(form => typeof form === 'string').map(form => path.resolve(directory, form));
}

async function submoduleRoots(workspaceRoot) {
  try {
    const text = await fs.readFile(path.join(workspaceRoot, '.gitmodules'), 'utf8');
    return Array.from(text.matchAll(/^\s*path\s*=\s*(.+?)\s*$/gm), match => path.resolve(workspaceRoot, match[1].replace(/^"|"$/g, '')));
  } catch (error) {
    if (error.code === 'ENOENT') return [];
    throw error;
  }
}

module.exports = { defaultPreviewWorker, exists, findManifest, isInside, readManifestForms, resolveProject, samePath, submoduleRoots };
