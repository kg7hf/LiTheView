# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param(
    [ValidateSet('win32-debug', 'stm32-debug')]
    [string]$Preset = 'win32-debug',
    [ValidateSet('Build', 'Test', 'Run')][string]$Action = 'Build'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. "$PSScriptRoot/tool-helpers.ps1"
$application = Split-Path $PSScriptRoot -Parent
$savedPath = $env:PATH
Push-Location $application
try {
    $cmake = Resolve-Tool 'cmake.exe' $env:STARTER_CMAKE @(
        "$env:LOCALAPPDATA/stm32cube/bundles/cmake/*/bin/cmake.exe",
        "$env:USERPROFILE/.mcuxpressotools/cmake-*/bin/cmake.exe")
    if ($Preset.StartsWith('stm32')) {
        if ($Action -ne 'Build') { throw 'Test and Run are host actions. Use the explicit ST-LINK launcher for hardware.' }

    }
    # Both platform presets build the current application, including generated UI
    # when present. Generation is explicit; the application build needs no .NET SDK.
    Invoke-Checked $cmake @('--preset', $Preset)
    Invoke-Checked $cmake @('--build', '--preset', $Preset, '--parallel')
    if ($Action -eq 'Test') {
        Invoke-Checked (Join-Path (Split-Path $cmake) 'ctest.exe') @('--preset', $Preset)
    } elseif ($Action -eq 'Run') {
        Invoke-Checked "./build/$Preset/starter.exe" @()
    }
} finally { Pop-Location; $env:PATH = $savedPath }
