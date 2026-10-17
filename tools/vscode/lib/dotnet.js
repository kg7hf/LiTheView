// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const fs = require('node:fs/promises');
const path = require('node:path');
const { execFile } = require('node:child_process');

/** The shared framework the framework-dependent service DLL requires, from its runtimeconfig.json. */
async function requiredFramework(servicePath, readFile = fs.readFile) {
  try {
    const config = JSON.parse(await readFile(servicePath.replace(/\.dll$/i, '.runtimeconfig.json'), 'utf8'));
    const options = config?.runtimeOptions || {};
    const frameworks = options.framework ? [options.framework] : Array.isArray(options.frameworks) ? options.frameworks : [];
    const core = frameworks.find(item => item?.name === 'Microsoft.NETCore.App') || frameworks[0];
    const match = /^(\d+)\.(\d+)/.exec(String(core?.version || ''));
    return core?.name && match ? { name: core.name, version: core.version, major: Number(match[1]), minor: Number(match[2]) } : null;
  } catch {
    return null;
  }
}

/** Parses `dotnet --list-runtimes` lines such as "Microsoft.NETCore.App 10.0.12 [C:\Program Files\dotnet\shared\...]". */
function parseRuntimes(text) {
  return String(text ?? '').split(/\r?\n/)
    .map(line => /^(\S+)\s+(\d+)\.(\d+)\.(\S+)\s+\[/.exec(line.trim()))
    .filter(Boolean)
    .map(match => ({ name: match[1], major: Number(match[2]), minor: Number(match[3]), version: `${match[2]}.${match[3]}.${match[4]}` }));
}

/** The default roll-forward policy accepts the same major version with an equal or later minor version. */
function satisfies(runtimes, framework) {
  return runtimes.some(runtime => runtime.name === framework.name && runtime.major === framework.major && runtime.minor >= framework.minor);
}

/**
 * Where to look when litheview.dotnetPath is left at its default: DOTNET_ROOT, the per-user
 * install (%USERPROFILE%\.dotnet), PATH, then the machine-wide install.
 */
function candidates(env, platform) {
  const paths = platform === 'win32' ? path.win32 : path.posix;
  const executable = platform === 'win32' ? 'dotnet.exe' : 'dotnet';
  const home = platform === 'win32' ? env.USERPROFILE : env.HOME;
  const list = [];
  if (env.DOTNET_ROOT) list.push(paths.join(env.DOTNET_ROOT, executable));
  if (home) list.push(paths.join(home, '.dotnet', executable));
  list.push('dotnet');
  if (platform === 'win32' && env.ProgramFiles) list.push(paths.join(env.ProgramFiles, 'dotnet', executable));
  return [...new Set(list)];
}

function listRuntimes(dotnet) {
  return new Promise(resolve => {
    execFile(dotnet, ['--list-runtimes'], { windowsHide: true, shell: false, timeout: 15000 },
      (error, stdout) => resolve(error ? null : parseRuntimes(stdout)));
  });
}

function describe(candidate, runtimes, framework) {
  if (!runtimes) return `${candidate} (not found or did not start)`;
  const versions = runtimes.filter(runtime => runtime.name === framework.name).map(runtime => runtime.version);
  return `${candidate} (${versions.length ? `has ${versions.join(', ')}` : `no ${framework.name}`})`;
}

/**
 * Chooses the dotnet executable that can run the designer service. An explicit setting is checked
 * but never replaced; the default "dotnet" searches the usual install locations. Throws an
 * actionable error naming what was checked when none has the required runtime.
 */
async function resolveDotnet({ configured, servicePath, env = process.env, platform = process.platform }, dependencies = {}) {
  const explicit = typeof configured === 'string' && configured.trim() !== '' && configured.trim() !== 'dotnet';
  const fallback = explicit ? configured.trim() : 'dotnet';
  // A self-contained service executable carries its own runtime.
  if (!/\.dll$/i.test(String(servicePath))) return { dotnetPath: fallback, framework: null };
  const framework = await requiredFramework(servicePath, dependencies.readFile);
  if (!framework) return { dotnetPath: fallback, framework: null };
  const run = dependencies.listRuntimes || listRuntimes;
  const checked = [];
  for (const candidate of explicit ? [fallback] : candidates(env, platform)) {
    const runtimes = await run(candidate);
    if (runtimes && satisfies(runtimes, framework)) return { dotnetPath: candidate, framework };
    checked.push(describe(candidate, runtimes, framework));
  }
  const needed = `the .NET ${framework.major} runtime (${framework.name} ${framework.major}.${framework.minor} or a later ${framework.major}.x)`;
  throw new Error(explicit
    ? `The LiTheView designer service needs ${needed}, but litheview.dotnetPath does not provide it: ${checked.join('; ')}. ` +
      'Point the setting at a dotnet executable that has it, or install that runtime.'
    : `The LiTheView designer service needs ${needed}. Checked: ${checked.join('; ')}. ` +
      `Install the .NET ${framework.major} runtime, or set litheview.dotnetPath to a dotnet executable that has it.`);
}

module.exports = { requiredFramework, parseRuntimes, satisfies, candidates, listRuntimes, resolveDotnet };
