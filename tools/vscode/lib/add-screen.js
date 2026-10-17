// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
'use strict';
const fs = require('node:fs/promises');
const path = require('node:path');
const { isDeepStrictEqual } = require('node:util');
const { samePath, isInside } = require('./project-paths');
const normalize = text => text.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n');
// VS Code owns the multi-file source edit and undo. No generated files are touched.
async function applyScreenPlan(vscode, project, plan) {
  const directory = path.dirname(project.manifestPath);
  if (!plan || !samePath(plan.manifestPath, project.manifestPath) ||
      !samePath(path.dirname(plan.formPath), path.join(directory, 'forms')) ||
      !/^[A-Za-z][A-Za-z0-9_]{0,63}\.litheview\.xml$/.test(path.basename(plan.formPath)) ||
      [plan.before, plan.after, plan.xml].some(text => typeof text !== 'string' || text.length > 1024 * 1024))
    throw new Error('Invalid Add screen plan.');
  const root = await fs.realpath(project.applicationRoot);
  for (const folder of [directory, path.dirname(plan.formPath)]) {
    if (!isInside(await fs.realpath(folder), root)) throw new Error('Screen sources must stay inside the application.');
  }
  try { await fs.lstat(plan.formPath); throw new Error('The screen file already exists.'); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
  const uri = vscode.Uri.file(plan.manifestPath);
  const manifest = await vscode.workspace.openTextDocument(uri);
  const version = manifest.version;
  if (manifest.isDirty || normalize(manifest.getText()) !== normalize(plan.before) ||
      normalize(await fs.readFile(plan.manifestPath, 'utf8')) !== normalize(plan.before))
    throw new Error('The project changed. Save it and retry Add screen.');
  const after = JSON.parse(plan.after);
  const before = JSON.parse(plan.before);
  const relative = path.relative(directory, plan.formPath).split(path.sep).join('/');
  if (JSON.stringify([...after.forms].sort()) !== JSON.stringify([...before.forms, relative].sort()) ||
      Object.keys(before).length !== Object.keys(after).length ||
      Object.keys(before).some(key => key !== 'forms' && !isDeepStrictEqual(before[key], after[key])))
    throw new Error('Add screen may only append a form to the current project.');
  // No await between this final editor-version check and submitting the WorkspaceEdit.
  if (manifest.version !== version || manifest.isDirty || normalize(manifest.getText()) !== normalize(plan.before))
    throw new Error('The project changed. Save it and retry Add screen.');
  const update = new vscode.WorkspaceEdit();
  const form = vscode.Uri.file(plan.formPath);
  update.createFile(form, { overwrite: false, ignoreIfExists: false });
  update.insert(form, new vscode.Position(0, 0), plan.xml);
  update.replace(uri, new vscode.Range(manifest.positionAt(0), manifest.positionAt(manifest.getText().length)), plan.after);
  if (!await vscode.workspace.applyEdit(update)) throw new Error('The screen edit could not be applied.');
  const created = await vscode.workspace.openTextDocument(form);
  if (normalize(created.getText()) !== normalize(plan.xml) || !await created.save() ||
      normalize(manifest.getText()) !== normalize(plan.after) || !await manifest.save())
    throw new Error('Screen creation is not fully saved. Save the form and project manifest before generating.');
  return form;
}
module.exports = { applyScreenPlan };
