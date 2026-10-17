# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug',
    [string] $Dotnet = 'dotnet',
    [string] $ProjectDirectory = '',
    [string] $Surface = 'profiles/display-surfaces/stm32h750b-dk-rk043fn48h-rgb565.json',
    [string] $InputConfiguration = 'profiles/logical-inputs/encoder-select.json'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if ($env:OS -ne 'Windows_NT') { throw 'This runner builds the Windows simulator. The exported headless CMake project is portable.' }
$configurationName = $Configuration.ToLowerInvariant()
$outputRoot = Join-Path $repositoryRoot "build/designer-tutorial-$configurationName"
if (!$ProjectDirectory) { $ProjectDirectory = Join-Path $outputRoot 'project' }
$project = [IO.Path]::GetFullPath($ProjectDirectory, $repositoryRoot)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$validationPath = Join-Path $outputRoot 'validation.json'
[ordered]@{ Passed = $false; Status = 'incomplete' } | ConvertTo-Json |
    Set-Content -LiteralPath $validationPath -Encoding utf8
$artifacts = Join-Path $outputRoot 'managed'
$dotnetCommand = (Get-Command $Dotnet -ErrorAction Stop).Source
if (!$env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $outputRoot 'dotnet-home' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

function Invoke-Step {
    param([string[]] $Arguments, [string] $Name)
    $log = Join-Path $outputRoot "$Name.log"
    & $dotnetCommand @Arguments *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 40 | Write-Host
        throw "Designer tutorial step '$Name' failed; see $log"
    }
    Write-Output "PASS $Name"
}

# Honor the repository's pinned SDK even when invoked from another directory.
Push-Location (Join-Path $repositoryRoot 'tools/designer')
try {
    Invoke-Step @('build', 'src/LitheView.Designer.Cli/LitheView.Designer.Cli.csproj',
        '-c', $Configuration, '--artifacts-path', $artifacts, '--disable-build-servers',
        '-m:1', '-p:UseSharedCompilation=false') 'build-tools'
} finally { Pop-Location }
$cli = Join-Path $artifacts "bin/LitheView.Designer.Cli/$configurationName/LitheView.Designer.Cli.dll"
$catalog = Join-Path $repositoryRoot 'profiles/display-surfaces'
$widgets = Join-Path $repositoryRoot 'schema/widgets.json'
$manifest = Join-Path $project 'project.litheview.json'
if (!(Test-Path -LiteralPath $manifest)) {
    Invoke-Step @($cli, 'simulator', 'create-tutorial', $project,
        [IO.Path]::GetFullPath($Surface, $repositoryRoot),
        [IO.Path]::GetFullPath($InputConfiguration, $repositoryRoot), $catalog, $widgets) 'create-project'
} else {
    Write-Output "Using existing project and its display/input snapshots: $manifest"
    Write-Output 'To exercise different display/input choices, supply a new -ProjectDirectory.'
}
Invoke-Step @($cli, 'simulator', 'paths', $manifest, $repositoryRoot) 'resolve-paths'
$paths = Get-Content -LiteralPath (Join-Path $outputRoot 'resolve-paths.log') -Raw | ConvertFrom-Json
Invoke-Step @($cli, 'simulator', 'export-project', $manifest, $repositoryRoot) 'export'
Invoke-Step @($cli, 'simulator', 'build-project', $manifest, $repositoryRoot, $Configuration) 'build-simulation'
$buildResult = Join-Path $paths.buildDirectory "$configurationName/build-result.json"
$result = Get-Content -LiteralPath $buildResult -Raw | ConvertFrom-Json
if (!$result.passed) { throw 'The simulator build did not publish a successful result.' }
[ordered]@{
    Passed = $true; Project = $manifest; BuildResult = $buildResult
    Executable = $result.executable
    ExecutableSha256 = $result.executableSha256
    RunnerSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Scope = 'Designer project creation/reopen, export, fixed-format Core build, installed consumer build and CTest. No interactive GUI execution or hardware qualification.'
} | ConvertTo-Json | Set-Content -LiteralPath $validationPath -Encoding utf8
Write-Output "Designer tutorial passed. Start the simulator: $($result.executable)"
