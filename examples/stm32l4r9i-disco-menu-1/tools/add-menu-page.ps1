# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Settings', 'Storage', 'Volume', 'Load')][string]$Name)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ui = Join-Path (Split-Path $PSScriptRoot -Parent) 'Tasks/UI'
$manifestPath = Join-Path $ui 'project.litheview.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$target = Join-Path $ui "forms/$Name.litheview.xml"
$relative = "forms/$Name.litheview.xml"
if ((Test-Path -LiteralPath $target) -or ($manifest.forms -contains $relative)) { throw 'Screen already exists; no files were changed.' }
$definition = Get-Content -LiteralPath "$ui/profiles/display-definition.json" -Raw | ConvertFrom-Json
if ($definition.geometry.width -ne 390 -or $definition.geometry.height -ne 390 -or $definition.geometry.shape -ne 'circle' -or $definition.format -ne 'rgb888') { throw 'Define the 390 x 390 round RGB888 display first.' }
$profile = [System.Security.SecurityElement]::Escape($manifest.surfaceProfile.id)
$id = $Name.ToLowerInvariant()
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<!--litheview-form schemaVersion="1" id="form.$id" displayName="$Name" surfaceProfileId="$profile"-->
<!--litheview-component id="component.$id.root" name="rootPanel"-->
<panel frame="0,0,390,390" backgroundColor="0x00303030" borderWidth="0" />
"@
# This command-line equivalent of Add screen shares the existing profiles and output settings.
[IO.File]::WriteAllText($target, $xml)
$manifest.forms = @($manifest.forms) + $relative
$manifest | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Host "Added $relative to Tasks/UI/project.litheview.json. Open it, design, save and Generate UI."
