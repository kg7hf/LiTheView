# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $prefix = [string](& git rev-parse --show-prefix)
    if ($LASTEXITCODE -ne 0) { throw 'Use a Git checkout of this example.' }
    $root = (& git rev-parse --show-toplevel).Trim()
    # No --recursive: the library has its own examples and optional vendor trees.
    & git -C $root submodule update --init -- "$($prefix.Trim())Middlewares/Third_Party/LiTheView"
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the pinned LiTheView package.' }
} finally { Pop-Location }
