# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$fixture = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath = Join-Path $fixture 'assets/gallop_source.png'
$expectedSourceHash = '71656C0AF13F37460E70D9D674867EA6787FEF02767EE7C461089D9926D47344'
if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $expectedSourceHash) {
    throw 'The retained source image hash changed.'
}

$image = [System.Drawing.Bitmap]::new($sourcePath)
try {
    $redLevels = [byte[]]::new(96 * 72)
    $alpha = [byte[]]::new(12 * 72)
    for ($y = 0; $y -lt 72; ++$y) {
        for ($x = 0; $x -lt 96; ++$x) {
            # Integer-center nearest sampling of source crop0,0,384,512.
            $sourceX = [int][Math]::Floor(((2 * $x + 1) * 384) / 192.0)
            $sourceY = [int][Math]::Floor(((2 * $y + 1) * 512) / 144.0)
            $color = $image.GetPixel($sourceX, $sourceY)
            # Existing GDI+ converter uses a white matte and its documented
            # round-to-even channel normalization before destination quantization.
            $red = [int][Math]::Round(
                (($color.R * $color.A) + (255 * (255 - $color.A)) + 127) / 255.0,
                [MidpointRounding]::ToEven)
            $redLevels[$y * 96 + $x] = if ($red -lt 43) { 0 } elseif ($red -lt 128) { 1 }
                elseif ($red -lt 213) { 2 } else { 3 }
            if ($color.A -ge 128) {
                $index = $y * 12 + [int][Math]::Floor($x / 8.0)
                $alpha[$index] = $alpha[$index] -bor (0x80 -shr ($x % 8))
            }
        }
    }
    $native = [byte[]]::new(48 * 36)
    for ($row = 0; $row -lt 36; ++$row) {
        for ($column = 0; $column -lt 48; ++$column) {
            $top = (2 * $row) * 96 + 2 * $column
            # Independent positional weights: TL4, TR64, BL1, BR16.
            $native[$row * 48 + $column] = 4 * $redLevels[$top] + 64 * $redLevels[$top + 1] +
                $redLevels[$top + 96] + 16 * $redLevels[$top + 97]
        }
    }
    foreach ($item in @(@('r2', $native), @('alpha1', $alpha))) {
        $actual = [System.IO.File]::ReadAllBytes((Join-Path $fixture "generated/resources/gallop-$($item[0]).bin"))
        $expected = $item[1]
        if ($actual.Length -ne $expected.Length) { throw "Unexpected $($item[0]) payload size." }
        for ($index = 0; $index -lt $actual.Length; ++$index) {
            if ($actual[$index] -ne $expected[$index]) {
                throw "Independent $($item[0]) packing oracle differs at byte $index."
            }
        }
        Write-Output "$($item[0]): $($actual.Length) bytes match the independent retained-source oracle."
    }
}
finally {
    $image.Dispose()
}
