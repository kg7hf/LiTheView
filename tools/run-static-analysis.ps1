# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [string] $BuildDirectory = 'build/static-analysis',
    [string] $ApplicationBuildDirectory = '',
    [string] $Generator = $(if ($env:OS -eq 'Windows_NT') { 'MinGW Makefiles' } else { 'Ninja' })
)

$ErrorActionPreference = 'Stop'
if ($ApplicationBuildDirectory) {
    & (Join-Path $PSScriptRoot 'analyze-application.ps1') -BuildDirectory $ApplicationBuildDirectory
    return
}
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$analysisBuild = Join-Path $repositoryRoot $BuildDirectory

function Assert-LastExitCode {
    param([Parameter(Mandatory)][string] $Description)
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

function Assert-CommandAvailable {
    param([Parameter(Mandatory)][string] $Name)
    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "LitheView static analysis requires '$Name' on PATH. See docs/CONFORMANCE.md."
    }
}

foreach ($requiredCommand in @('cmake', 'cppcheck', 'clang-tidy', 'git')) {
    Assert-CommandAvailable $requiredCommand
}

# --fresh discards CMake dependency metadata, but can leave old object files.
# Clean compiled outputs too so every gate checks this checkout's current headers.
Push-Location $repositoryRoot
try {
    & cmake --fresh -S . -B $analysisBuild -G $Generator `
        -DCMAKE_BUILD_TYPE=Debug `
        -DCMAKE_EXPORT_COMPILE_COMMANDS=ON `
        -DLITHEVIEW_BUILD_WIN32=OFF `
        -DLITHEVIEW_BUILD_TESTS=ON `
        -DLITHEVIEW_WARNINGS_AS_ERRORS=ON
    Assert-LastExitCode 'Static-analysis configuration'

    & cmake --build $analysisBuild --clean-first --parallel
    Assert-LastExitCode 'Static-analysis build'

    # Tracked and untracked files that .gitignore does not exclude, so new sources are analyzed too.
    $analysisFiles = @(
        git ls-files --cached --others --exclude-standard -- src tests |
            Where-Object {
                $_ -like '*.cpp' -and
                $_.Replace('\', '/') -notmatch '^tests/[^/]*consumer/' -and
                ($_.Replace('\', '/') -notmatch '^tests/rendering/' -or
                 $_.Replace('\', '/') -match '^tests/rendering/(common|stm32u545re-q)/') -and
                (Test-Path -LiteralPath $_)
            }
    )
    Assert-LastExitCode 'Source listing'
    # Preserve Cppcheck's complete test-tree coverage while excluding the newly
    # relocated host/asset fixtures that were never part of this critical gate.
    $cppcheckTestPaths = @(Get-ChildItem -LiteralPath tests | Where-Object {
        $_.Name -ne 'rendering' -and ($_.PSIsContainer -or $_.Extension -in @('.cpp', '.hpp'))
    } | Select-Object -ExpandProperty FullName)
    & cppcheck `
        --enable=warning,portability `
        --check-level=exhaustive `
        --std=c++23 `
        --language=c++ `
        --inline-suppr `
        --error-exitcode=1 `
        --suppress=missingIncludeSystem `
        --suppress=missingInclude `
        -Iinclude `
        "-I$analysisBuild/generated" `
        -Itests/rendering/common `
        -Itests/rendering/stm32u545re-q `
        include `
        src `
        tests/rendering/common `
        tests/rendering/stm32u545re-q `
        @cppcheckTestPaths
    Assert-LastExitCode 'Cppcheck critical profile'

    & clang-tidy `
        --quiet `
        --config-file=tools/clang-tidy-critical.yaml `
        -p $analysisBuild `
        @analysisFiles
    Assert-LastExitCode 'Clang-Tidy critical profile'
} finally {
    Pop-Location
}

Write-Host 'LitheView static-analysis gates passed.'
