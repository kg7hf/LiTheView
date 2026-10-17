# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $CliAssembly,
    [Parameter(Mandatory)][string] $Rgb565Prefix,
    [Parameter(Mandatory)][string] $Xrgb8888Prefix,
    [Parameter(Mandatory)][string] $R2Prefix,
    [Parameter(Mandatory)][string] $Rrrr2222Prefix,
    [Parameter(Mandatory)][string] $Ryry2222Prefix,
    [string] $Dotnet = 'dotnet',
    [string] $BuildRoot = (Join-Path $PSScriptRoot '../build/display-definitions'),
    [ValidateSet('MinGW Makefiles', 'Ninja')]
    [string] $Generator = $(if ($env:OS -eq 'Windows_NT') { 'MinGW Makefiles' } else { 'Ninja' })
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$assembly = (Resolve-Path -LiteralPath $CliAssembly).Path
$dotnetCommand = (Get-Command $Dotnet -ErrorAction Stop).Source
$cmakeCommand = (Get-Command cmake -ErrorAction Stop).Source
$ctestCommand = (Get-Command ctest -ErrorAction Stop).Source
New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $BuildRoot).Path

function Invoke-Recorded {
    param([string] $Program, [string[]] $Arguments, [string] $Log, [int] $ExpectedExit = 0)
    $output = & $Program @Arguments 2>&1
    $code = $LASTEXITCODE
    $output | Set-Content -LiteralPath $Log -Encoding utf8
    if ($code -ne $ExpectedExit) {
        throw "Expected exit $ExpectedExit, got $code; retained output: $Log"
    }
}

function Get-ExportHashes {
    param([string] $Directory)
    $items = Get-ChildItem -LiteralPath $Directory -File -Recurse | Sort-Object FullName
    return @($items | ForEach-Object {
        [ordered]@{
            Path = [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\', '/')
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
}

$products = @(
    @{ Name = 'rk043fn48h-rgb565'; Prefix = $Rgb565Prefix; Input = 'pointer-keys-text' },
    @{ Name = 'rk043fn48h-xrgb8888'; Prefix = $Xrgb8888Prefix; Input = 'pointer-keys-text' },
    @{ Name = 'round-240-rgb565'; Prefix = $Rgb565Prefix; Input = 'pointer-keys-text' },
    @{ Name = 'synthetic-r2'; Prefix = $R2Prefix; Input = 'encoder-select' },
    @{ Name = 'synthetic-red-cell'; Prefix = $Rrrr2222Prefix; Input = 'encoder-select' },
    @{ Name = 'synthetic-red-yellow'; Prefix = $Ryry2222Prefix; Input = 'encoder-select' }
)
$results = @()
foreach ($product in $products) {
    $name = $product.Name
    $prefix = (Resolve-Path -LiteralPath $product.Prefix).Path
    $directory = Join-Path $outputRoot $name
    $generated = Join-Path $directory 'generated'
    $repeated = Join-Path $directory 'repeated'
    $consumer = Join-Path $directory 'consumer'
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $commonArguments = @(
        $assembly, 'display', 'export-cpp',
        (Join-Path $repositoryRoot "tests/rendering/display-model-generated/$name.litheview.xml"),
        (Join-Path $repositoryRoot 'schema/widgets.json'),
        (Join-Path $repositoryRoot "profiles/display-definitions/$name.json"),
        (Join-Path $repositoryRoot "profiles/logical-inputs/$($product.Input).json")
    )
    Invoke-Recorded $dotnetCommand ($commonArguments + @($generated, 'resources/gallop.resource.json')) (Join-Path $directory 'export.log')
    Invoke-Recorded $dotnetCommand ($commonArguments + @($repeated, 'resources/gallop.resource.json')) (Join-Path $directory 'repeat.log')
    $hashes = Get-ExportHashes $generated
    if (($hashes | ConvertTo-Json -Depth 4 -Compress) -cne ((Get-ExportHashes $repeated) | ConvertTo-Json -Depth 4 -Compress)) {
        throw "Nondeterministic export for $name"
    }
    $report = Get-Content -LiteralPath (Join-Path $generated 'report.json') -Raw | ConvertFrom-Json
    if ($report.exportStatus -ne 'complete' -or $report.surfaceStatus -ne 'passed' -or $report.targetStatus -ne 'unevaluated') {
        throw "Unexpected validation stages for $name"
    }
    Invoke-Recorded $cmakeCommand @(
        '-S', (Join-Path $repositoryRoot 'tests/display_definition_consumer'), '-B', $consumer,
        '-G', $Generator, '-DCMAKE_BUILD_TYPE=Release', '-DCMAKE_EXPORT_COMPILE_COMMANDS=ON',
        "-DCMAKE_PREFIX_PATH=$prefix", "-DGENERATED_DIRECTORY=$generated", "-DDISPLAY_EXAMPLE=$name"
    ) (Join-Path $directory 'configure.log')
    Invoke-Recorded $cmakeCommand @('--build', $consumer, '--parallel', '2') (Join-Path $directory 'build.log')
    Invoke-Recorded $ctestCommand @('--test-dir', $consumer, '--output-on-failure') (Join-Path $directory 'test.log')
    $frame = Get-Item -LiteralPath (Join-Path $consumer 'frame.bin')
    $results += [ordered]@{
        Name = $name; SurfaceStatus = 'passed'; TargetStatus = 'unevaluated'
        RepeatedExportMatches = $true; HandwrittenReplayMatches = $true
        NativeFrameBytes = $frame.Length
        NativeFrameSha256 = (Get-FileHash -LiteralPath $frame.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        ExportedFiles = $hashes
    }
    Write-Host "PASS $name; $($frame.Length) native bytes; target unevaluated"
}

# A surface lock mismatch must fail before producing a new export.
$negative = Join-Path $outputRoot 'wrong-surface'
New-Item -ItemType Directory -Force -Path $negative | Out-Null
Invoke-Recorded $dotnetCommand @(
    $assembly, 'display', 'check',
    (Join-Path $repositoryRoot 'tests/rendering/display-model-generated/rk043fn48h-rgb565.litheview.xml'),
    (Join-Path $repositoryRoot 'schema/widgets.json'),
    (Join-Path $repositoryRoot 'profiles/display-definitions/rk043fn48h-xrgb8888.json'),
    (Join-Path $repositoryRoot 'profiles/logical-inputs/pointer-keys-text.json'),
    'resources/gallop.resource.json'
) (Join-Path $negative 'check.log') 1
if ((Get-Content -LiteralPath (Join-Path $negative 'check.log') -Raw) -notmatch 'LVFS1001') {
    throw 'Wrong surface did not report the surface-lock diagnostic.'
}

# A generated RGB565 definition cannot silently use an XRGB8888-only library.
$wrongBuild = Join-Path $outputRoot 'wrong-library'
New-Item -ItemType Directory -Force -Path $wrongBuild | Out-Null
Invoke-Recorded $cmakeCommand @(
    '-S', (Join-Path $repositoryRoot 'tests/display_definition_consumer'), '-B', $wrongBuild,
    '-G', $Generator, '-DCMAKE_BUILD_TYPE=Release',
    "-DCMAKE_PREFIX_PATH=$((Resolve-Path -LiteralPath $Xrgb8888Prefix).Path)",
    "-DGENERATED_DIRECTORY=$(Join-Path $outputRoot 'rk043fn48h-rgb565/generated')",
    '-DDISPLAY_EXAMPLE=rk043fn48h-rgb565'
) (Join-Path $wrongBuild 'configure.log')
$failedOutput = & $cmakeCommand --build $wrongBuild --parallel 2 2>&1
$failedExit = $LASTEXITCODE
$failedOutput | Set-Content -LiteralPath (Join-Path $wrongBuild 'build.log') -Encoding utf8
if ($failedExit -eq 0 -or ($failedOutput -join "`n") -notmatch 'must enable the selected destination format') {
    throw 'Wrong library did not fail at the generated destination-format assertion.'
}
[ordered]@{
    SchemaVersion = 1
    Products = $results
    WrongSurfaceRejected = $true
    WrongLibraryRejected = $true
    CliAssemblySha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'validation.json') -Encoding utf8
Write-Host 'PASS deterministic exports, six native replays, and both negative configuration checks.'
exit 0
