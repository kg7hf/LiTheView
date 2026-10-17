# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.2
[CmdletBinding()]
param([switch]$Volume, [switch]$Load)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$app = Split-Path $PSScriptRoot -Parent
$writes = @()
if ($Volume -and $Load) { throw 'Connect one additional lesson at a time.' }
$pages = @('UI', 'Settings', 'Storage')
if ($Volume -or $Load) {
    foreach ($parent in $pages) {
        if (!(Test-Path -LiteralPath "$app/Tasks/$parent/form_handlers.hpp")) {
            throw 'Connect the three-page menu before adding this page.'
        }
    }
    $pages = @($(if ($Load) { 'Load' } else { 'Volume' }))
}
foreach ($page in $pages) {
    $target = Join-Path $app "Tasks/$page/form_handlers.hpp"
    if (Test-Path -LiteralPath $target) { throw "$target exists. Keep your handwritten code; no files were changed." }
    $xml = Get-Content -LiteralPath "$app/Tasks/$page/forms/main.litheview.xml" -Raw
    $generated = Get-Content -LiteralPath "$app/Tasks/$page/Generated/form.hpp" -Raw
    $text = Get-Content -LiteralPath "$app/Lessons/Handlers/$page/form_handlers.hpp" -Raw
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
# Validate every page before publishing any handwritten files. This command never
# regenerates a form and never replaces an existing application handler.
foreach ($write in $writes) { [IO.File]::WriteAllText($write.Path, $write.Text) }
Write-Host "Connected $($pages -join ", "). Review Tasks/*/form_handlers.hpp, then build the same preset."
