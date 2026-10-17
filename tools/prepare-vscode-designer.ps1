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
