# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param(
    [string] $Dotnet = 'dotnet',
    [string] $CMake = 'cmake',
    [switch] $SkipNative
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Windows builds the MinGW worker, which shares frames through named memory; Linux and macOS
# build host-debug, whose worker returns frames inline.
$preset = if ($IsWindows) { 'windows-mingw-debug' } else { 'host-debug' }
$workerName = if ($IsWindows) { 'litheview_designer_preview_worker.exe' } else { 'litheview_designer_preview_worker' }
$output = Join-Path $repository 'build/vscode-designer'
$service = Join-Path $output 'service'
$artifacts = Join-Path $output 'managed'
$dotnetCommand = Get-Command $Dotnet -CommandType Application -ErrorAction Stop |
    Select-Object -First 1 -ExpandProperty Source
New-Item -ItemType Directory -Force -Path $output | Out-Null
$originalHome = $env:DOTNET_CLI_HOME
$originalTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$originalCertificate = $env:DOTNET_GENERATE_ASPNET_CERTIFICATE
$originalLogo = $env:DOTNET_NOLOGO
$env:DOTNET_CLI_HOME = Join-Path $output 'dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
try {
    # global.json selects the same SDK as the standalone designer.
    Push-Location (Join-Path $repository 'tools/designer')
    try {
        & $dotnetCommand publish src/LitheView.Designer.Service/LitheView.Designer.Service.csproj `
            -c Release -o $service --artifacts-path $artifacts --disable-build-servers `
            -m:1 -p:UseSharedCompilation=false -p:UseAppHost=false 2>&1 |
            Tee-Object -FilePath (Join-Path $output 'publish.log')
        if ($LASTEXITCODE -ne 0) { throw 'Designer service publication failed.' }
    } finally { Pop-Location }
    if (!$SkipNative) {
        Push-Location $repository
        try {
            & $CMake --preset $preset 2>&1 |
                Tee-Object -FilePath (Join-Path $output 'native-configure.log')
            if ($LASTEXITCODE -ne 0) { throw 'Native preview configuration failed.' }
            & $CMake --build --preset $preset --target litheview_designer_preview_worker --parallel 2 2>&1 |
                Tee-Object -FilePath (Join-Path $output 'native-build.log')
            if ($LASTEXITCODE -ne 0) { throw 'Native preview build failed.' }
        } finally { Pop-Location }
    }
    $worker = Join-Path $repository "build/$preset/$workerName"
    if (!(Test-Path -LiteralPath $worker -PathType Leaf)) { throw 'Native worker is missing; run without -SkipNative.' }
    if ($IsWindows) {
        # Local preparation must work when VS Code has no compiler on PATH.
        # Resolve the runtimes from the compiler that actually built this worker.
        $cache = Get-Content -LiteralPath (Join-Path $repository "build/$preset/CMakeCache.txt")
        $compilerEntry = $cache | Where-Object { $_ -match '^CMAKE_CXX_COMPILER:(FILEPATH|STRING)=' } | Select-Object -First 1
        if (!$compilerEntry) { throw 'Cannot resolve the preview worker compiler from CMakeCache.txt.' }
        $compiler = $compilerEntry -replace '^CMAKE_CXX_COMPILER:(FILEPATH|STRING)=', ''
        $runtimeFiles = foreach ($name in @('libgcc_s_seh-1.dll', 'libstdc++-6.dll', 'libwinpthread-1.dll')) {
            $resolved = Join-Path (Split-Path -Parent $compiler) $name
            if (!(Test-Path -LiteralPath $resolved -PathType Leaf)) {
                $resolved = & $compiler "-print-file-name=$name"
                if ($LASTEXITCODE -ne 0) { throw "Worker compiler runtime lookup failed for $name." }
            }
            if (!$resolved -or $resolved -eq $name -or
                !(Test-Path -LiteralPath $resolved -PathType Leaf)) {
                throw "The worker compiler cannot locate $name. Repair that compiler installation and prepare again."
            }
            Get-Item -LiteralPath $resolved
        }
        foreach ($runtimeFile in $runtimeFiles) {
            Copy-Item -LiteralPath $runtimeFile.FullName -Destination (Join-Path (Split-Path -Parent $worker) $runtimeFile.Name) -Force
        }
        # These are local build outputs. Binary redistribution remains subject to
        # the separate notice/provenance checks in package-vscode-designer.ps1.
    }
    [ordered]@{
        protocolVersion = 1
        service = Join-Path $service 'LitheView.Designer.Service.dll'
        worker = $worker
        widgetCatalogSha256 = (Get-FileHash -LiteralPath (Join-Path $repository 'schema/widgets.json')).Hash.ToLowerInvariant()
        scope = 'Prepared desktop designer service and native preview; no firmware or hardware action.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'prepared.json') -Encoding utf8
    Write-Output "Prepared designer service: $service"
} finally {
    $env:DOTNET_CLI_HOME = $originalHome
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $originalTelemetry
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = $originalCertificate
    $env:DOTNET_NOLOGO = $originalLogo
}
