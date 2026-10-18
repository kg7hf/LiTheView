# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param([Parameter(Mandatory)][string] $CompilerBin)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$compilerDirectory = (Resolve-Path -LiteralPath $CompilerBin).Path
if (!(Test-Path -LiteralPath (Join-Path $compilerDirectory 'gdb.exe') -PathType Leaf)) {
    throw 'Install GDB beside the selected MinGW compiler before debugging this example.'
}
if ((Split-Path $compilerDirectory -Leaf) -ine 'bin') { throw 'Expected the MinGW compiler in its toolchain bin directory.' }
$toolchainRoot = Split-Path $compilerDirectory -Parent
$applicationRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$linkDirectory = Join-Path $applicationRoot 'build/tool-links'
$link = [IO.Path]::GetFullPath((Join-Path $linkDirectory 'mingw'))
if (!$link.StartsWith($applicationRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Debugger link is outside the application.'
}
New-Item -ItemType Directory -Force -Path $linkDirectory | Out-Null
$item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
if ($item) {
    if ($item.LinkType -ne 'Junction') { throw "Refusing to replace a non-junction: $link" }
    if ([IO.Path]::GetFullPath($item.Target) -ieq $toolchainRoot) { return }
    # Remove only the junction itself, never traverse or delete compiler files.
    [IO.Directory]::Delete($link)
}
New-Item -ItemType Junction -Path $link -Target $toolchainRoot | Out-Null
