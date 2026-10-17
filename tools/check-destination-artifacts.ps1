# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

<#
.SYNOPSIS
Checks fixed-destination writer exclusion and optional standard-example resources.
.DESCRIPTION
Uses defined, demangled GNU nm symbols. Only destination-specialized writer helpers
count as writer evidence; bitmap/font readers are intentionally unrestricted. A
stripped or fully inlined image without recognizable writer evidence fails closed.
Resource checks apply to the final standard example executable/ELF, not an archive
of unreferenced alternatives. No hardware is accessed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CoreArchive,

    [Parameter(Mandatory = $true)]
    [ValidateSet('RGB565', 'RGB888', 'XRGB8888', 'ARGB8888', 'R2_PACKED2X2', 'RRRR2222', 'RYRY2222')]
    [string]$DestinationFormat,

    [string]$Nm = 'nm',

    [string]$ExampleArtifact,

    [ValidateSet('Auto', 'CompactAlpha1', 'CompactIndexed4', 'GeneratedIndexed4')]
    [string]$ResourceChoice = 'Auto',

    [string]$JsonOutput
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-Artifact([string]$Path) {
    $item = Get-Item -LiteralPath $Path
    if ($item.PSIsContainer) {
        throw "Expected an artifact file: $Path"
    }
    return $item.FullName
}

$nmCommand = Get-Command $Nm -CommandType Application -ErrorAction Stop | Select-Object -First 1
$nmPath = $nmCommand.Source
$nmVersion = @(& $nmPath --version 2>&1)
if ($LASTEXITCODE -ne 0 -or $nmVersion.Count -eq 0) {
    throw "Could not determine nm version: $nmPath"
}

function Get-DefinedSymbols([string]$Path) {
    $output = @(& $nmPath --defined-only --demangle $Path 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "nm failed for ${Path}: $($output -join [Environment]::NewLine)"
    }
    foreach ($line in $output) {
        if ([string]$line -match '^\s*(?<Address>[0-9a-fA-F]+)\s+(?<Type>[A-Za-z?])\s+(?<Name>.+)$') {
            [pscustomobject]@{
                Address = $Matches.Address
                Type = $Matches.Type
                Name = $Matches.Name
            }
        }
    }
}

$corePath = Resolve-Artifact $CoreArchive
$symbols = @(Get-DefinedSymbols $corePath)
$violations = [System.Collections.Generic.List[string]]::new()
$writers = [System.Collections.Generic.List[object]]::new()
$unrecognized = [System.Collections.Generic.List[string]]::new()
# Numeric values are the public PixelFormat enumerators. Named demangling is
# accepted too; an unknown value or spelling is not treated as exclusion proof.
$formatByValue = @{ '0' = 'RGB565'; '1' = 'RGB888'; '2' = 'XRGB8888'; '3' = 'ARGB8888'; '4' = 'R2_PACKED2X2'; '5' = 'RRRR2222'; '6' = 'RYRY2222' }
$writerPattern = '\blithe::.*::(?<Helper>putPixel|writePixelAtOffset|drawGlyph|drawBitmapPixels|dispatchBitmap)<(?<Destination>[^,>]+)'
foreach ($symbol in $symbols) {
    if ($symbol.Type -cnotmatch '^[tTwW]$' -or $symbol.Name -notmatch $writerPattern) {
        continue
    }
    $helper = $Matches.Helper
    $destination = $Matches.Destination.Trim()
    $format = $null
    if ($destination -match '^\(lithe::PixelFormat\)\s*(?<Value>[0-9]+)$') {
        $format = $formatByValue[$Matches.Value]
    }
    elseif ($destination -match '^(?:lithe::)?PixelFormat::(?<Named>rgb565|rgb888|xrgb8888|argb8888|r2Packed2x2|rrrr2222|ryry2222)$') {
        $format = if ($Matches.Named -eq 'r2Packed2x2') { 'R2_PACKED2X2' } else { $Matches.Named.ToUpperInvariant() }
    }
    if ($null -eq $format) {
        $unrecognized.Add($symbol.Name)
        continue
    }
    $writers.Add([pscustomobject]@{ Helper = $helper; Format = $format; Symbol = $symbol.Name })
}
if ($unrecognized.Count -ne 0) {
    $violations.Add('Unrecognized destination specialization: writer exclusion is unproven.')
}
if ($writers.Count -eq 0) {
    $violations.Add('No recognizable defined writer specializations; stripped or inlined symbols cannot prove exclusion.')
}
$selected = @($writers | Where-Object { $_.Format -eq $DestinationFormat })
$unexpected = @($writers | Where-Object { $_.Format -ne $DestinationFormat })
if ($selected.Count -eq 0) {
    $violations.Add("No defined $DestinationFormat writer specialization was found.")
}
if ($unexpected.Count -ne 0) {
    $unexpectedFormats = @($unexpected.Format | Sort-Object -Unique) -join ', '
    $violations.Add("Unselected destination writers remain: $unexpectedFormats")
}

$resourceEvidence = $null
if ($ExampleArtifact) {
    $examplePath = Resolve-Artifact $ExampleArtifact
    if ($ResourceChoice -eq 'Auto') {
        switch ($DestinationFormat) {
            'RGB565' { $ResourceChoice = 'CompactAlpha1' }
            'XRGB8888' { $ResourceChoice = 'GeneratedIndexed4' }
            default { throw 'Select ResourceChoice explicitly for this destination format.' }
        }
    }
    $exampleSymbols = @(Get-DefinedSymbols $examplePath)
    $names = @($exampleSymbols.Name)
    $compact = @($names | Where-Object { $_ -match '^lithe::font5x7\(\)$' })
    $generatedRegular = @($names | Where-Object { $_ -match '^lithe::generatedSans\(\)$' })
    $generatedBold = @($names | Where-Object { $_ -match '^lithe::generatedSansBold\(\)$' })
    $generatedFamily = @($names | Where-Object { $_ -match 'generatedSans' })
    $alpha1 = @($names | Where-Object { $_ -eq 'lithe::demo::assets::kGallopBits' })
    $indexed4 = @($names | Where-Object { $_ -eq 'lithe::demo::assets::kGallopIndexed4Bits' })
    if ($ResourceChoice -eq 'CompactAlpha1') {
        if ($compact.Count -eq 0 -or $alpha1.Count -eq 0) {
            $violations.Add('Compact/alpha1 example must define font5x7() and kGallopBits.')
        }
        if ($generatedFamily.Count -ne 0 -or $indexed4.Count -ne 0) {
            $violations.Add('Compact/alpha1 example retains generatedSans resources or kGallopIndexed4Bits.')
        }
    }
    elseif ($ResourceChoice -eq 'CompactIndexed4') {
        if ($compact.Count -eq 0 -or $indexed4.Count -eq 0) {
            $violations.Add('Compact/indexed4 example must define font5x7() and kGallopIndexed4Bits.')
        }
        if ($generatedFamily.Count -ne 0 -or $alpha1.Count -ne 0) {
            $violations.Add('Compact/indexed4 example retains generatedSans resources or kGallopBits.')
        }
    }
    else {
        if ($generatedRegular.Count -eq 0 -or $generatedBold.Count -eq 0 -or $indexed4.Count -eq 0) {
            $violations.Add('Generated/indexed4 example must define generatedSans(), generatedSansBold(), and kGallopIndexed4Bits.')
        }
        if ($alpha1.Count -ne 0) {
            $violations.Add('Generated/indexed4 example retains kGallopBits.')
        }
    }
    $resourceEvidence = [ordered]@{
        Artifact = $examplePath
        SHA256 = (Get-FileHash -LiteralPath $examplePath -Algorithm SHA256).Hash
        Choice = $ResourceChoice
        CompactFontPresent = $compact.Count -ne 0
        GeneratedRegularPresent = $generatedRegular.Count -ne 0
        GeneratedBoldPresent = $generatedBold.Count -ne 0
        Alpha1PayloadPresent = $alpha1.Count -ne 0
        Indexed4PayloadPresent = $indexed4.Count -ne 0
        Symbols = @($names | Where-Object { $_ -match 'generatedSans|font5x7|kGallop(?:Indexed4)?Bits' })
        Note = 'Compact font retention is permitted for generated/indexed4 consumers; its actual presence is reported.'
    }
}
elseif ($ResourceChoice -ne 'Auto') {
    throw 'ResourceChoice requires ExampleArtifact.'
}

$evidence = [ordered]@{
    Passed = $violations.Count -eq 0
    CheckedAtUtc = [DateTime]::UtcNow.ToString('o')
    CoreArchive = $corePath
    CoreSHA256 = (Get-FileHash -LiteralPath $corePath -Algorithm SHA256).Hash
    DestinationFormat = $DestinationFormat.ToUpperInvariant()
    Nm = $nmPath
    NmVersion = [string]$nmVersion[0]
    NmArguments = @('--defined-only', '--demangle')
    WriterSpecializations = @($writers.ToArray())
    UnrecognizedWriterSpecializations = @($unrecognized.ToArray())
    Resources = $resourceEvidence
    Violations = @($violations.ToArray())
    Scope = 'Defined destination writer specializations in the archive, plus optional selected resources in the final standard example image. Source bitmap/font readers are unrestricted.'
}
if ($JsonOutput) {
    $evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $JsonOutput -Encoding utf8
}
if ($violations.Count -ne 0) {
    throw ($violations -join [Environment]::NewLine)
}
Write-Output "PASS: $DestinationFormat; $($selected.Count) defined writer specializations; no unselected writers."
if ($null -ne $resourceEvidence) {
    Write-Output "PASS: $ResourceChoice resources; compact font present: $($resourceEvidence.CompactFontPresent)."
}
