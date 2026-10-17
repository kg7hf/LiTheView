// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

// The service never owns a document. A response may only affect the exact
// VS Code document version from which its request was made.
function isCurrent(document, requestedVersion, result) {
  return !document.isClosed && document.version === requestedVersion && result?.documentVersion === requestedVersion;
}

async function applyVersionedEdit(vscode, document, requestedVersion, result) {
  if (!isCurrent(document, requestedVersion, result) || typeof result.xml !== 'string') return false;
  if (result.xml === document.getText()) return true;
  const edit = new vscode.WorkspaceEdit();
  edit.replace(document.uri, new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length)), result.xml);
  return vscode.workspace.applyEdit(edit);
}

module.exports = { applyVersionedEdit, isCurrent };
