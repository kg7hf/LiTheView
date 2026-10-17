# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$cache = Get-Content -LiteralPath (Join-Path $build 'CMakeCache.txt')
function CacheValue([string]$Name) {
    $entry = @($cache | Where-Object { $_ -match "^${Name}:[^=]+=" })
    if ($entry.Count -ne 1) { throw "Missing or ambiguous $Name in CMake cache." }
    $entry[0].Substring($entry[0].IndexOf('=') + 1)
}
$app = [IO.Path]::GetFullPath((CacheValue 'CMAKE_HOME_DIRECTORY'))
$compiler = CacheValue 'CMAKE_CXX_COMPILER'
$db = @(Get-Content -LiteralPath (Join-Path $build 'compile_commands.json') -Raw | ConvertFrom-Json)
$owned = @($db | Where-Object {
    $relative = [IO.Path]::GetRelativePath($app, $_.file).Replace('\', '/')
    $relative -match '^(Application|Controls|Devices|Platform|Tasks|Tests)/' -and
    $relative -notmatch '/Generated/' -and $relative -like '*.cpp'
})
if ($owned.Count -eq 0) { throw 'No application-owned C++ translation units in this build.' }
$analysis = Join-Path $build 'application-analysis'
New-Item -ItemType Directory -Force -Path $analysis | Out-Null
$owned | ConvertTo-Json -AsArray -Depth 12 | Set-Content -LiteralPath (Join-Path $analysis 'compile_commands.json') -Encoding utf8
$files = @($owned | ForEach-Object file | Sort-Object -Unique)
$files | Set-Content -LiteralPath (Join-Path $analysis 'scope.txt') -Encoding utf8
# Clang cannot infer GCC's i686 or Arm C++ headers from every Windows toolchain.
# Ask the selected compiler instead of assuming a particular installation path.
$originalPath = $env:PATH
try {
    $env:PATH = (Split-Path $compiler -Parent) + [IO.Path]::PathSeparator + $env:PATH
    $target = (& $compiler -dumpmachine).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Compiler target lookup failed.' }
    $listing = '' | & $compiler -E -x c++ -v - 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Compiler include lookup failed.' }
    $extra = @("--extra-arg=--target=$target")
    $collect = $false
    foreach ($line in $listing) {
        $text = $line.ToString()
        if ($text -eq '#include <...> search starts here:') { $collect = $true; continue }
        if ($text -eq 'End of search list.') { $collect = $false }
        if ($collect) {
            $directory = [IO.Path]::GetFullPath($text.Trim()).Replace('\', '/')
            # Use Clang's own intrinsic headers, not GCC-specific builtin declarations.
            if ($directory -match '/lib/gcc/[^/]+/[^/]+/include(-fixed)?$') { continue }
            $extra += '--extra-arg=-isystem'; $extra += '--extra-arg=' + $directory
        }
    }
    if ($extra.Count -le 1) { throw 'Compiler returned no system include paths.' }
    & cppcheck "--project=$analysis/compile_commands.json" --enable=warning,portability `
        --check-level=exhaustive --std=c++23 --inline-suppr --error-exitcode=1 `
        --suppress=missingIncludeSystem --suppress=missingInclude 2>&1 |
        Tee-Object -FilePath (Join-Path $analysis 'cppcheck.log')
    if ($LASTEXITCODE -ne 0) { throw "Application Cppcheck failed; see $analysis" }
    # Only first-party application headers; the dependency's own gate checks Core.
    $prefix = [regex]::Escape($app.Replace('\', '/'))
    $filter = "^$prefix/(Application|Controls|Devices|Platform|Tasks|Tests)/"
    & clang-tidy --quiet "--config-file=$PSScriptRoot/clang-tidy-critical.yaml" `
        "--header-filter=$filter" -p $build @extra @files 2>&1 |
        Tee-Object -FilePath (Join-Path $analysis 'clang-tidy.log')
    if ($LASTEXITCODE -ne 0) { throw "Application Clang-Tidy failed; see $analysis" }
} finally { $env:PATH = $originalPath }
Write-Host "Application analysis passed: $($files.Count) C++ translation units; scope retained in $analysis"
