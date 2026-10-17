# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.2
[CmdletBinding()]
param([string]$CMake = '', [switch]$SkipStm32, [string]$LibraryRoot = '')
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repository 'examples/stm32h750b-dk-starter-1'
$completed = Join-Path $repository 'examples/stm32h750b-dk-starter-1-complete'
. "$source/tools/tool-helpers.ps1"
if (!$CMake) {
    $CMake = Resolve-Tool 'cmake.exe' $env:STARTER_CMAKE @(
        "$env:LOCALAPPDATA/stm32cube/bundles/cmake/*/bin/cmake.exe",
        "$env:USERPROFILE/.mcuxpressotools/cmake-*/bin/cmake.exe")
}
$run = Join-Path $repository "build/starter-progression/$([Guid]::NewGuid().ToString('N'))"
$application = Join-Path $run 'application with spaces'
New-Item -ItemType Directory -Path $application -Force | Out-Null

# Copy application sources, never the developer's local UI or nested dependencies.
$prefix = 'examples/stm32h750b-dk-starter-1/'
$entries = & git -C $repository ls-files --stage -- $prefix
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate starter sources.' }
foreach ($entry in $entries) {
    if ($entry.StartsWith('160000 ')) { continue }
    $relative = $entry.Substring($entry.IndexOf("`t") + 1).Substring($prefix.Length)
    if ($relative.StartsWith('Tasks/UI/')) { continue }
    if (!(Test-Path -LiteralPath (Join-Path $source $relative) -PathType Leaf)) { continue }
    $destination = Join-Path $application $relative
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destination
}
$newFiles = & git -C $repository ls-files --others --exclude-standard -- $prefix
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate new starter sources.' }
foreach ($entry in $newFiles) {
    $relative = $entry.Substring($prefix.Length)
    if ($relative.StartsWith('Tasks/UI/')) { continue }
    $destination = Join-Path $application $relative
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destination
}
# Test-only junctions avoid copying vendor trees. Distributed examples use real submodules.
foreach ($dependency in @('Drivers/CMSIS_6', 'Drivers/CMSIS/Device/ST/STM32H7xx',
    'Drivers/STM32H7xx_HAL_Driver', 'Middlewares/Third_Party/FreeRTOS',
    'Middlewares/Third_Party/FreeRTOS-Kernel', 'Middlewares/Third_Party/LiTheView')) {
    $target = Join-Path $source $dependency
    if ($LibraryRoot -and $dependency -eq 'Middlewares/Third_Party/LiTheView') {
        $target = (Resolve-Path -LiteralPath $LibraryRoot).Path
    }
    if (!(Test-Path -LiteralPath (Join-Path $target '.git'))) { throw "Initialize $target first." }
    $link = Join-Path $application $dependency
    New-Item -ItemType Directory -Path (Split-Path $link) -Force | Out-Null
    New-Item -ItemType Junction -Path $link -Target $target | Out-Null
}
function Invoke-Recorded([string]$Label, [string]$Executable, [string[]]$Arguments, [switch]$ExpectFailure) {
    $output = & $Executable @Arguments 2>&1
    $result = $LASTEXITCODE
    $output | Set-Content -LiteralPath (Join-Path $run "$Label.log") -Encoding utf8
    if (($result -eq 0) -eq $ExpectFailure.IsPresent) { throw "$Label returned $result; see $run" }
    return $output -join "`n"
}
$savedMingw = $env:STARTER_MINGW_BIN
$ctest = Join-Path (Split-Path $CMake) 'ctest.exe'
$presets = @('win32-debug')
if (!$SkipStm32) { $presets += 'stm32-debug' }
Push-Location $application
try {
    # Check fail-closed overrides before selecting a real toolchain in a spaced path.
    $setup = Join-Path $application 'tools/configure-tools.ps1'
    $pwsh = (Get-Command pwsh).Source
    $env:STARTER_MINGW_BIN = Join-Path $run 'missing compiler'
    $badTool = Invoke-Recorded 'invalid-compiler-override' $pwsh @('-NoProfile', '-File', $setup) -ExpectFailure
    if ($badTool -notmatch 'Missing tool override') { throw "Unexpected override diagnostic: $badTool" }
    $env:STARTER_MINGW_BIN = $savedMingw
    $null = Invoke-Recorded 'discover-host-tools' $pwsh @('-NoProfile', '-File', $setup, '-RequireDebugger')
    $selected = (Get-Item -LiteralPath (Join-Path $application 'build/tool-links/mingw')).Target
    $spacedTools = Join-Path $run 'compiler path with spaces'
    New-Item -ItemType Junction -Path $spacedTools -Target $selected | Out-Null
    $env:STARTER_MINGW_BIN = $spacedTools
    foreach ($preset in $presets) {
        # Also exercise migration from the previously cached blank-mode option.
        $null = Invoke-Recorded "configure-$preset" $CMake @('--preset', $preset, '-DSTARTER_WITH_UI=OFF')
        $null = Invoke-Recorded "blank-$preset" $CMake @('--build', '--preset', $preset)
    }
    $before = Invoke-Recorded 'blank-runtime' $ctest @('--preset', 'win32-debug', '--verbose')
    if ($before -notmatch 'PASS:.*frames=0') { throw "Blank application did not run: $before" }

    # These are the checked-in outputs of the completed lesson's Generate UI operation.
    Copy-Item -LiteralPath (Join-Path $completed 'Tasks/UI') -Destination (Join-Path $application 'Tasks/UI') -Recurse
    $header = Join-Path $application 'Tasks/UI/Generated/form.hpp'
    $heldHeader = Join-Path $run 'held-form.hpp'
    Move-Item -LiteralPath $header -Destination $heldHeader
    try {
        $partial = Invoke-Recorded 'partial-export' $CMake @('--build', '--preset', 'win32-debug') -ExpectFailure
        if ($partial -notmatch 'UI export is missing form.hpp') { throw "Partial export failed for an unexpected reason: $partial" }
    } finally { Move-Item -LiteralPath $heldHeader -Destination $header }

    # Crucial regression: no configure command, new preset or build directory after generation.
    foreach ($preset in $presets) {
        $null = Invoke-Recorded "generated-$preset" $CMake @('--build', '--preset', $preset)
    }
    $after = Invoke-Recorded 'generated-runtime' $ctest @('--preset', 'win32-debug', '--verbose')
    if ($after -notmatch 'PASS:.*frames=1') { throw "Generated UI was not integrated: $after" }
    $interactionLog = Join-Path $run 'interaction.log'
    $interaction = Start-Process -FilePath (Join-Path $application 'build/win32-debug/starter.exe') `
        -ArgumentList '--interaction-self-test' -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput $interactionLog -RedirectStandardError (Join-Path $run 'interaction-errors.log')
    if ($interaction.ExitCode -ne 0 -or (Get-Content -LiteralPath $interactionLog -Raw) -notmatch 'PASS: mouse') {
        throw "Named-handler interaction failed; see $interactionLog"
    }
    $null = Invoke-Recorded 'input-checks-build' $CMake @('--build', '--preset', 'win32-debug', '--target', 'starter_input_checks')
    $null = Invoke-Recorded 'input-checks' (Join-Path $application 'build/win32-debug/starter_input_checks.exe') @()
    $buttonLog = Join-Path $run 'physical-button.log'
    $button = Start-Process -FilePath (Join-Path $application 'build/win32-debug/starter.exe') `
        -ArgumentList '--button-self-test' -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput $buttonLog -RedirectStandardError (Join-Path $run 'physical-button-errors.log')
    if ($button.ExitCode -ne 0 -or (Get-Content -LiteralPath $buttonLog -Raw) -notmatch 'PASS: front-view') {
        throw "Physical-button interaction failed; see $buttonLog"
    }
    # A hand-edited sidecar must obey the same contract as the designer.
    $frontPath = Join-Path $application 'Tasks/UI/project.litheview.front-panel.json'
    $frontText = [IO.File]::ReadAllText($frontPath)
    try {
        foreach ($invalid in @('overlap', 'unmapped', 'changed-image')) {
            $front = $frontText | ConvertFrom-Json
            switch ($invalid) {
                'overlap' { $front.buttons[0].bounds = $front.display; $expected = 'must not overlap' }
                'unmapped' { $front.buttons[0].id = 'unconnectedButton'; $expected = 'No device adapter' }
                'changed-image' { $front.image.sha256 = '0' * 64; $expected = 'PNG changed' }
            }
            $front | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $frontPath -Encoding utf8NoBOM
            $failure = Invoke-Recorded "front-$invalid" $CMake @('--build', '--preset', 'win32-debug') -ExpectFailure
            if ($failure -notmatch $expected) { throw "Unexpected front-view diagnostic: $failure" }
        }
    } finally { [IO.File]::WriteAllText($frontPath, $frontText) }
    $null = Invoke-Recorded 'front-restored' $CMake @('--build', '--preset', 'win32-debug')
    [ordered]@{ passed = $true; application = $application; presets = $presets;
        blank = $before; generated = $after; partialExportRejected = $true;
        namedHandlerInteraction = $true; physicalButtonInteraction = $true; invalidFrontViewsRejected = $true;
        explicitlyReconfiguredAfterGeneration = $false; invalidCompilerOverrideRejected = $true; compilerPathWithSpaces = $true } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "PASS: same presets progress from blank to generated UI; partial export rejected. Evidence: $run"
} finally { Pop-Location; $env:STARTER_MINGW_BIN = $savedMingw }
