# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug',
    [ValidateSet('MinGW Makefiles', 'Ninja')]
    [string] $Generator = $(if ($env:OS -eq 'Windows_NT') { 'MinGW Makefiles' } else { 'Ninja' })
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $repositoryRoot "build/display-bringup-$($Configuration.ToLowerInvariant())"
$core = Join-Path $outputRoot 'core'
$prefix = Join-Path $outputRoot 'installed'
$consumer = Join-Path $outputRoot 'host'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
[ordered]@{ Passed = $false; Status = 'incomplete' } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $outputRoot 'validation.json') -Encoding utf8
$cmake = (Get-Command cmake -ErrorAction Stop).Source
$ctest = (Get-Command ctest -ErrorAction Stop).Source

function Invoke-Step {
    param([string] $Program, [string[]] $Arguments, [string] $Name)
    $log = Join-Path $outputRoot "$Name.log"
    & $Program @Arguments *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 35 | Write-Host
        throw "Bring-up tutorial step '$Name' failed; see $log"
    }
    Write-Output "PASS $Name"
}

Invoke-Step $cmake @(
    '--fresh', '-S', $repositoryRoot, '-B', $core, '-G', $Generator,
    "-DCMAKE_BUILD_TYPE=$Configuration", '-DCMAKE_EXPORT_COMPILE_COMMANDS=ON',
    '-DLITHEVIEW_DESTINATION_FORMAT=RGB565', '-DLITHEVIEW_BUILD_RENDERING_FIXTURES=OFF',
    '-DLITHEVIEW_BUILD_TESTS=OFF', '-DLITHEVIEW_BUILD_WIN32=OFF',
    '-DLITHEVIEW_BUILD_STM32U545_PROBE=OFF', '-DLITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF'
) 'configure-core'
Invoke-Step $cmake @('--build', $core, '--parallel', '2') 'build-core'
Invoke-Step $cmake @('--install', $core, '--prefix', $prefix) 'install-core'
Invoke-Step $cmake @(
    '--fresh', '-S', (Join-Path $repositoryRoot 'tests/rendering/display-bringup'), '-B', $consumer,
    '-G', $Generator, "-DCMAKE_BUILD_TYPE=$Configuration",
    '-DCMAKE_EXPORT_COMPILE_COMMANDS=ON', "-DCMAKE_PREFIX_PATH=$prefix"
) 'configure-application'
Invoke-Step $cmake @('--build', $consumer, '--parallel', '2') 'build-application'
Invoke-Step $ctest @('--test-dir', $consumer, '--output-on-failure') 'test-application'

$artifacts = @('initial.ppm', 'activated.ppm') | ForEach-Object {
    $path = Join-Path $consumer $_
    [ordered]@{ Path = $path; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$sources = @(
    'tests/rendering/display-bringup/product.hpp', 'tests/rendering/display-bringup/main.cpp',
    'tests/rendering/display-bringup/win32_main.cpp', 'tests/rendering/display-bringup/CMakeLists.txt',
    'tools/run-display-bringup.ps1'
) | ForEach-Object {
    [ordered]@{
        Path = $_
        Sha256 = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
[ordered]@{
    Passed = $true; Configuration = $Configuration; Destination = 'RGB565'
    FrameBytes = 24576; TileBytes = 1024; Artifacts = @($artifacts); Sources = @($sources)
    Scope = 'Host rendering/input/transfer-lifetime checks; simulator compiled on Windows. No GUI execution, panel driver or hardware measurement is claimed by this runner.'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'validation.json') -Encoding utf8
Write-Output "Bring-up tutorial passed. Open initial.ppm and activated.ppm in $consumer"
if ($env:OS -eq 'Windows_NT') {
    Write-Output "Start the interactive simulation: $consumer/litheview_display_bringup_simulator.exe"
}
exit 0
