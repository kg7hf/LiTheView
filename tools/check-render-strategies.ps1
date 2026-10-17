# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Rgb565Prefix,
    [Parameter(Mandatory)]
    [string] $Xrgb8888Prefix,
    [Parameter(Mandatory)]
    [string] $R2Prefix,
    [string] $BuildRoot = (Join-Path $PSScriptRoot '../build/render-strategies'),
    [ValidateSet('MinGW Makefiles', 'Ninja')]
    [string] $Generator = $(if ($env:OS -eq 'Windows_NT') { 'MinGW Makefiles' } else { 'Ninja' }),
    # Optional trailing parameters preserve named and positional three-format
    # invocations. Each supplied cell prefix adds both strategy consumers.
    [string] $Rrrr2222Prefix,
    [string] $Ryry2222Prefix
)

$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../tests/strategy_consumer')).Path
New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null
$strategyBuildRoot = (Resolve-Path -LiteralPath $BuildRoot).Path
$cmakeCommand = (Get-Command cmake -ErrorAction Stop).Source
$nmCommand = Get-Command nm -ErrorAction SilentlyContinue

function Invoke-RecordedCommand {
    param([string] $Program, [string[]] $ArgumentList, [string] $LogPath)
    $output = & $Program @ArgumentList 2>&1
    $exitCode = $LASTEXITCODE
    $text = $output -join [Environment]::NewLine
    Set-Content -LiteralPath $LogPath -Value $text -Encoding utf8
    if ($exitCode -ne 0) {
        throw "Command exited with $exitCode; retained output: $LogPath"
    }
    return $text
}

# These hashes describe the independently specified fixture bytes, not a prior
# renderer capture. Every consumer also verifies its own native-byte oracle.
$formats = @(
    @{
        Name = 'RGB565'; Prefix = $Rgb565Prefix; FrameBytes = 128; TileBytes = 32
        Sha256 = '88C41049081B92510F44C3B200412D666E8B9F5D2F610767F353AF70959660BE'
    },
    @{
        Name = 'XRGB8888'; Prefix = $Xrgb8888Prefix; FrameBytes = 256; TileBytes = 64
        Sha256 = 'B317FB6DA6B90626EC94242EE9D10C2868AF6110364422625EC862C6988E76F5'
    },
    @{
        Name = 'R2_PACKED2X2'; Prefix = $R2Prefix; FrameBytes = 16; TileBytes = 4
        Sha256 = '2CB7A7CE11E2309E24CFFEAF15EB26AF25A7E4CE0F15983957F0523BB5407DED'
    }
)
if ($Rrrr2222Prefix) {
    $formats += @{
        Name = 'RRRR2222'; Prefix = $Rrrr2222Prefix; FrameBytes = 64; TileBytes = 16
        Sha256 = '0D2E30296907AEF5A5F36F98B66C5970CB7C87DC9B712C2E5BE51EE3908030C4'
    }
}
if ($Ryry2222Prefix) {
    $formats += @{
        Name = 'RYRY2222'; Prefix = $Ryry2222Prefix; FrameBytes = 64; TileBytes = 16
        Sha256 = 'FEA496FB2251BD5990444D5A751971A6C51F1C890CAFDF7800EAD8A7B64AED05'
    }
}
$results = @()
foreach ($format in $formats) {
    $prefix = (Resolve-Path -LiteralPath $format.Prefix).Path
    $packageDirectory = Join-Path $prefix 'lib/cmake/LitheView'
    if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory 'LitheViewConfig.cmake'))) {
        throw "Installed LitheView package not found below $prefix"
    }
    $frames = @{}
    foreach ($strategy in @('FULL_FRAME', 'TILED')) {
        $name = $format.Name.ToLowerInvariant() + '-' + $strategy.ToLowerInvariant()
        $buildDirectory = Join-Path $strategyBuildRoot $name
        New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null
        Write-Host "Checking $($format.Name) $strategy against $prefix"
        $configureArguments = @(
            '-S', $sourceRoot, '-B', $buildDirectory, '-G', $Generator,
            '-DCMAKE_BUILD_TYPE=Release', '-DCMAKE_EXPORT_COMPILE_COMMANDS=ON',
            "-DLitheView_DIR=$packageDirectory",
            "-DEXPECTED_DESTINATION_FORMAT=$($format.Name)", "-DSTRATEGY=$strategy"
        )
        $buildArguments = @('--build', $buildDirectory, '--config', 'Release', '--parallel', '2')
        $null = Invoke-RecordedCommand $cmakeCommand $configureArguments (Join-Path $buildDirectory 'configure.log')
        $null = Invoke-RecordedCommand $cmakeCommand $buildArguments (Join-Path $buildDirectory 'build.log')
        $executable = @(
            (Join-Path $buildDirectory 'litheview_strategy_consumer.exe'),
            (Join-Path $buildDirectory 'litheview_strategy_consumer')
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
        if (-not $executable) {
            throw "Strategy executable was not produced in $buildDirectory"
        }
        $framePath = Join-Path $buildDirectory 'native-frame.bin'
        $metricsText = Invoke-RecordedCommand $executable @($framePath) (Join-Path $buildDirectory 'metrics.json')
        $metrics = $metricsText | ConvertFrom-Json
        $expectedWorkspace = if ($strategy -eq 'TILED') { $format.TileBytes } else { $format.FrameBytes }
        $expectedTiles = if ($strategy -eq 'TILED') { 4 } else { 0 }
        if ($metrics.format -ne $format.Name -or $metrics.strategy -ne $strategy -or
            $metrics.frame_bytes -ne $format.FrameBytes -or
            $metrics.workspace_bytes -ne $expectedWorkspace -or
            $metrics.tile_count -ne $expectedTiles -or -not $metrics.complete) {
            throw "Unexpected strategy metrics in $buildDirectory"
        }
        $bytes = [System.IO.File]::ReadAllBytes($framePath)
        $hash = (Get-FileHash -LiteralPath $framePath -Algorithm SHA256).Hash
        if ($bytes.Length -ne $format.FrameBytes -or $hash -ne $format.Sha256) {
            throw "Native output does not match the independent length/hash for $name"
        }
        $frames[$strategy] = $bytes
        $omissionCheck = 'unavailable: nm not found'
        if ($nmCommand) {
            $symbols = Invoke-RecordedCommand $nmCommand.Source @('-C', $executable) (Join-Path $buildDirectory 'symbols.txt')
            $hasTiledRenderer = $symbols -match 'lithe::TiledRenderer::render\('
            if (($strategy -eq 'FULL_FRAME' -and $hasTiledRenderer) -or
                ($strategy -eq 'TILED' -and -not $hasTiledRenderer)) {
                throw "Unexpected TiledRenderer linkage in $name"
            }
            if ($strategy -eq 'TILED' -and $symbols -match 'FrameDisplay') {
                throw "The tiled fixture linked a full-frame display implementation: $name"
            }
            $omissionCheck = 'passed'
        }
        $results += [pscustomobject]@{
            Format = $format.Name; Strategy = $strategy; InstalledPrefix = $prefix
            Executable = $executable; NativeFrame = $framePath
            FrameBytes = $bytes.Length; WorkspaceBytes = $metrics.workspace_bytes
            TileCount = $metrics.tile_count; Sha256 = $hash; SymbolCheck = $omissionCheck
            ConfigureArguments = $configureArguments; BuildArguments = $buildArguments
        }
    }
    for ($index = 0; $index -lt $format.FrameBytes; ++$index) {
        if ($frames['FULL_FRAME'][$index] -ne $frames['TILED'][$index]) {
            throw "Full-frame and tiled output differ for $($format.Name) at byte $index"
        }
    }
}
$reportPath = Join-Path $strategyBuildRoot 'strategy-conformance.json'
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host "All $($results.Count) installed strategy consumers passed. Evidence: $reportPath"
if (-not $nmCommand) {
    Write-Warning 'Native bytes and strategy selection passed; binary symbol omission was not checked because nm was unavailable.'
}
