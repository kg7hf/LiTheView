# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Settings', 'Storage', 'Volume', 'Load')][string]$Name)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$app = Split-Path $PSScriptRoot -Parent
$homeProject = Join-Path $app 'Tasks/UI'
$destination = Join-Path $app "Tasks/$Name"
if (Test-Path -LiteralPath $destination) { throw "$destination already exists; no files were changed." }
$manifest = Get-Content -LiteralPath "$homeProject/project.litheview.json" -Raw | ConvertFrom-Json
$paths = Get-Content -LiteralPath "$homeProject/project.litheview.paths.json" -Raw | ConvertFrom-Json
$definition = Get-Content -LiteralPath "$homeProject/profiles/display-definition.json" -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 2 -or $definition.geometry.width -ne 390 -or $definition.geometry.height -ne 390 -or $definition.geometry.shape -ne 'circle' -or $definition.format -ne 'rgb888') {
    throw 'Finish the 390 x 390 circular RGB888 Home display definition first.'
}
if ($manifest.resourceManifests.Count -ne 0 -or $manifest.extensions.Count -ne 0) { throw 'This lesson scaffold expects the resource-free Home project.' }
# The display/input profile IDs and their manifest hashes remain shared. Only the
# new page identity, form and output path change. No generated files are copied.
$manifest.id = "$($manifest.id).$($Name.ToLowerInvariant())"
$manifest.displayName = "$Name Menu"
$manifest.forms = @('forms/main.litheview.xml')
$paths.componentDirectory = "Tasks/$Name/Generated"
New-Item -ItemType Directory -Path "$destination/forms" -Force | Out-Null
Copy-Item -LiteralPath "$homeProject/profiles" -Destination "$destination/profiles" -Recurse
$manifest | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath "$destination/project.litheview.json" -Encoding utf8
$paths | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath "$destination/project.litheview.paths.json" -Encoding utf8
$profile = [System.Security.SecurityElement]::Escape($manifest.surfaceProfile.id)
@"
<?xml version="1.0" encoding="utf-8"?>
<!--litheview-form schemaVersion="1" id="form.main" displayName="$Name Menu" surfaceProfileId="$profile"-->
<!--litheview-component id="component.main.root" name="rootPanel"-->
<panel frame="0,0,390,390" backgroundColor="0x00303030" borderWidth="0" />
"@ | Set-Content -LiteralPath "$destination/forms/main.litheview.xml" -Encoding utf8
Write-Host "Created $destination. Open its form in the designer, add the controls, save and Generate UI."
