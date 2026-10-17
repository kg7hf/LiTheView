# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.2
[CmdletBinding()]
param([switch]$Volume, [switch]$Load, [switch]$Playback)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$app = Split-Path $PSScriptRoot -Parent
$writes = @()
if (@($Volume, $Load, $Playback).Where({ $_ }).Count -gt 1) { throw 'Connect one additional lesson at a time.' }
$pages = @('Home', 'Settings', 'Storage')
if ($Volume -or $Load) {
    foreach ($parent in $pages) {
        if (!(Test-Path -LiteralPath "$app/Tasks/UI/handlers/$parent.hpp")) {
            throw 'Connect the three-page menu before adding this page.'
        }
    }
    $pages = @($(if ($Load) { 'Load' } else { 'Volume' }))
}
if ($Playback) { $pages = @('Home') }
foreach ($page in $pages) {
    $target = Join-Path $app "Tasks/UI/handlers/$page.hpp"
    if (!$Playback -and (Test-Path -LiteralPath $target)) { throw "$target exists. Keep your handwritten code; no files were changed." }
    $manifest = Get-Content -LiteralPath "$app/Tasks/UI/project.litheview.json" -Raw | ConvertFrom-Json
    $relative = "forms/$page.litheview.xml"
    if ($manifest.forms -notcontains $relative) { throw "Add $page to the UI project first." }
    $form = Join-Path "$app/Tasks/UI" $relative
    $xml = Get-Content -LiteralPath $form -Raw
    $generated = Get-Content -LiteralPath "$app/Tasks/UI/Generated/$page/form.hpp" -Raw
    $lesson = if ($page -eq 'Home') { 'UI' } else { $page }
    $template = if ($Playback) { "$app/Lessons/Playback/form_handlers.hpp" } else { "$app/Lessons/Handlers/$lesson/form_handlers.hpp" }
    $text = Get-Content -LiteralPath $template -Raw
    $formId = [regex]::Match($xml, 'litheview-form schemaVersion="1" id="([^"]+)"').Groups[1].Value
    if (!$formId) { throw 'Form identity is missing.' }
    $text = $text.Replace('handwritten form: form.main', "handwritten form: $formId")
    foreach ($hook in [regex]::Matches($text, 'litheview-handler (component.main.([a-z]+)) (on[A-Za-z]+ButtonClicked)')) {
        $name = $hook.Groups[2].Value + 'Button'
        $callback = $hook.Groups[3].Value
        $component = [regex]::Match($xml, '<!--litheview-component id="([^"]+)" name="' + [regex]::Escape($name) + '"-->')
        if (-not $component.Success -or -not $generated.Contains("virtual void $callback()")) {
            throw "$page needs a button named $name and a current Generate UI before connecting handlers. No files were changed."
        }
        $text = $text.Replace("litheview-handler $($hook.Groups[1].Value) ", "litheview-handler $($component.Groups[1].Value) ")
    }
    if ($page -eq 'Volume') {
        foreach ($accessor in @('getLevelLabel', 'getVolumeProgress')) {
            if (!$generated.Contains("$accessor()")) {
                throw "Volume needs levelLabel and volumeProgress; Generate UI before connecting. No files were changed."
            }
        }
    }
    if ($page -eq 'Load') {
        foreach ($accessor in @('getEntryList', 'getLoadedLabel')) {
            if (!$generated.Contains("$accessor()")) { throw "Load needs entryList and loadedLabel. Generate UI first." }
        }
        if (!$generated.Contains('EntryListType')) { throw 'Generate the named entryList panel first.' }
    }
    $writes += @{ Path = $target; Text = $text }
}
if ($Playback) {
    $homeHandler = "$app/Tasks/UI/handlers/Home.hpp"
    $backup = "$app/Tasks/UI/handlers/Home.before-playback.hpp"
    $controls = "$app/Tasks/UI/playback_controls.hpp"
    if (!(Test-Path -LiteralPath $homeHandler) -or (Test-Path -LiteralPath $backup) -or (Test-Path -LiteralPath $controls)) {
        throw 'Playback requires the existing Home handler and no prior playback backup/controls. Merge your changes manually if playback is already connected.'
    }
    Copy-Item -LiteralPath $homeHandler -Destination $backup
    Copy-Item -LiteralPath "$app/Lessons/Playback/playback_controls.hpp" -Destination $controls
}
# Validate every page before publishing any handwritten files. This command never
# regenerates a form. Only -Playback replaces Home after retaining its backup.
New-Item -ItemType Directory -Path "$app/Tasks/UI/handlers" -Force | Out-Null
foreach ($write in $writes) { [IO.File]::WriteAllText($write.Path, $write.Text) }
Write-Host "Connected $($pages -join ", "). Review Tasks/UI/handlers/*.hpp, then build the same preset."
