# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

param(
    [Parameter(Mandatory = $true)]
    [string]$InputImage,

    [Parameter(Mandatory = $true)]
    [string]$OutputSource,

    [Parameter(Mandatory = $true)]
    [string]$OutputManifest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9_]{0,127}$')]
    [string]$Symbol,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z][A-Za-z0-9_]{0,127}$')]
    [string]$EntryPoint,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Image', 'Icon')]
    [string]$Kind,

    [Parameter(Mandatory = $true)]
    [ValidateSet('True', 'False')]
    [string]$Tintable,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Alpha1', 'Alpha8', 'Indexed4', 'Rgb565', 'Rgb888', 'Xrgb8888', 'Argb8888', 'R2Packed2x2')]
    [string]$Format,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 16383)]
    [int]$CropX,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 16383)]
    [int]$CropY,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 16384)]
    [int]$CropWidth,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 16384)]
    [int]$CropHeight,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 4096)]
    [int]$OutputWidth,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 4096)]
    [int]$OutputHeight,

    [Parameter(Mandatory = $true)]
    [ValidateSet('NearestNeighbor', 'Bilinear')]
    [string]$ScaleMode,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Preserve', 'Threshold', 'Opaque')]
    [string]$AlphaMode,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 255)]
    [int]$AlphaThreshold,

    [Parameter(Mandatory = $true)]
    [ValidateSet('None', 'Ordered4x4')]
    [string]$Dither,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^#?[0-9A-Fa-f]{8}$')]
    [string]$TintArgb,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^#?[0-9A-Fa-f]{8}$')]
    [string]$MatteArgb,

    [string]$PaletteArgb = '',

    [string]$OutputPixels = '',

    [string]$OutputPalette = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

trap
{
    Write-Error (
        'Image conversion failed at line {0}: {1}' -f
            $_.InvocationInfo.ScriptLineNumber,
            $_.Exception.Message)
    exit 1
}

$converterId = 'litheview.converter.image.gdiplus'
$converterVersion = '1.0.0'
$maximumPayloadBytes = 64MB

function Get-Sha256([string]$Path)
{
    $stream = [System.IO.File]::OpenRead($Path)
    try
    {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try
        {
            return [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        }
        finally
        {
            $sha.Dispose()
        }
    }
    finally
    {
        $stream.Dispose()
    }
}

function Get-Fnv1a32([byte[]]$Bytes)
{
    [uint64]$checksum = 2166136261
    foreach ($value in $Bytes)
    {
        $checksum = (($checksum -bxor [uint64]$value) * [uint64]0x01000193) -band [uint64]4294967295
    }
    return ('0x{0:x8}' -f $checksum)
}

function Convert-HexColor([string]$Text)
{
    $hex = $Text.TrimStart('#')
    return [System.Drawing.Color]::FromArgb(
        [Convert]::ToInt32($hex.Substring(0, 2), 16),
        [Convert]::ToInt32($hex.Substring(2, 2), 16),
        [Convert]::ToInt32($hex.Substring(4, 2), 16),
        [Convert]::ToInt32($hex.Substring(6, 2), 16))
}

function Convert-ColorHex([System.Drawing.Color]$Color)
{
    return ('{0:x2}{1:x2}{2:x2}{3:x2}' -f $Color.A, $Color.R, $Color.G, $Color.B)
}

function Limit-Byte([int]$Value)
{
    return [Math]::Min(255, [Math]::Max(0, $Value))
}

function Blend-Channel([int]$Foreground, [int]$Background, [int]$Alpha)
{
    return [int]((($Foreground * $Alpha) + ($Background * (255 - $Alpha)) + 127) / 255)
}

function Normalize-Color(
    [System.Drawing.Color]$Color,
    [System.Drawing.Color]$Matte,
    [bool]$DestinationPreservesAlpha)
{
    $alpha = [int]$Color.A
    if ($AlphaMode -eq 'Threshold')
    {
        $alpha = if ($alpha -ge $AlphaThreshold) { 255 } else { 0 }
    }

    if ($AlphaMode -eq 'Opaque' -or -not $DestinationPreservesAlpha)
    {
        $red = Blend-Channel $Color.R $Matte.R $alpha
        $green = Blend-Channel $Color.G $Matte.G $alpha
        $blue = Blend-Channel $Color.B $Matte.B $alpha
        return [System.Drawing.Color]::FromArgb(255, $red, $green, $blue)
    }

    return [System.Drawing.Color]::FromArgb($alpha, $Color.R, $Color.G, $Color.B)
}

function Get-SampledColor(
    [System.Drawing.Bitmap]$Bitmap,
    [int]$DestinationX,
    [int]$DestinationY)
{
    if ($ScaleMode -eq 'NearestNeighbor')
    {
        $sourceX = $CropX + [Math]::Min(
            $CropWidth - 1,
            [int][Math]::Floor((($DestinationX + 0.5) * $CropWidth) / $OutputWidth))
        $sourceY = $CropY + [Math]::Min(
            $CropHeight - 1,
            [int][Math]::Floor((($DestinationY + 0.5) * $CropHeight) / $OutputHeight))
        return $Bitmap.GetPixel($sourceX, $sourceY)
    }

    $sourceXF = $CropX + ((($DestinationX + 0.5) * $CropWidth) / $OutputWidth) - 0.5
    $sourceYF = $CropY + ((($DestinationY + 0.5) * $CropHeight) / $OutputHeight) - 0.5
    $x0 = [Math]::Max($CropX, [Math]::Min($CropX + $CropWidth - 1, [int][Math]::Floor($sourceXF)))
    $y0 = [Math]::Max($CropY, [Math]::Min($CropY + $CropHeight - 1, [int][Math]::Floor($sourceYF)))
    $x1 = [Math]::Min($CropX + $CropWidth - 1, $x0 + 1)
    $y1 = [Math]::Min($CropY + $CropHeight - 1, $y0 + 1)
    $fractionX = [Math]::Max(0.0, [Math]::Min(1.0, $sourceXF - [Math]::Floor($sourceXF)))
    $fractionY = [Math]::Max(0.0, [Math]::Min(1.0, $sourceYF - [Math]::Floor($sourceYF)))
    $c00 = $Bitmap.GetPixel($x0, $y0)
    $c10 = $Bitmap.GetPixel($x1, $y0)
    $c01 = $Bitmap.GetPixel($x0, $y1)
    $c11 = $Bitmap.GetPixel($x1, $y1)
    $topA = ($c00.A * (1.0 - $fractionX)) + ($c10.A * $fractionX)
    $topR = ($c00.R * (1.0 - $fractionX)) + ($c10.R * $fractionX)
    $topG = ($c00.G * (1.0 - $fractionX)) + ($c10.G * $fractionX)
    $topB = ($c00.B * (1.0 - $fractionX)) + ($c10.B * $fractionX)
    $bottomA = ($c01.A * (1.0 - $fractionX)) + ($c11.A * $fractionX)
    $bottomR = ($c01.R * (1.0 - $fractionX)) + ($c11.R * $fractionX)
    $bottomG = ($c01.G * (1.0 - $fractionX)) + ($c11.G * $fractionX)
    $bottomB = ($c01.B * (1.0 - $fractionX)) + ($c11.B * $fractionX)
    return [System.Drawing.Color]::FromArgb(
        [int][Math]::Round(($topA * (1.0 - $fractionY)) + ($bottomA * $fractionY)),
        [int][Math]::Round(($topR * (1.0 - $fractionY)) + ($bottomR * $fractionY)),
        [int][Math]::Round(($topG * (1.0 - $fractionY)) + ($bottomG * $fractionY)),
        [int][Math]::Round(($topB * (1.0 - $fractionY)) + ($bottomB * $fractionY)))
}

function Get-DitheredColor([System.Drawing.Color]$Color, [int]$X, [int]$Y)
{
    if ($Dither -eq 'None')
    {
        return $Color
    }

    $matrix = @(0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
    $bias = (($matrix[(($Y % 4) * 4) + ($X % 4)] * 2) - 15) * 3
    return [System.Drawing.Color]::FromArgb(
        $Color.A,
        (Limit-Byte ($Color.R + $bias)),
        (Limit-Byte ($Color.G + $bias)),
        (Limit-Byte ($Color.B + $bias)))
}

function Get-NearestPaletteIndex(
    [System.Drawing.Color]$Color,
    [System.Collections.Generic.List[System.Drawing.Color]]$Palette)
{
    $bestIndex = 0
    [long]$bestDistance = [long]::MaxValue
    for ($index = 0; $index -lt $Palette.Count; ++$index)
    {
        $candidate = $Palette[$index]
        $deltaA = [long]$Color.A - $candidate.A
        $deltaR = [long]$Color.R - $candidate.R
        $deltaG = [long]$Color.G - $candidate.G
        $deltaB = [long]$Color.B - $candidate.B
        $distance = (4 * $deltaA * $deltaA) + ($deltaR * $deltaR) +
            ($deltaG * $deltaG) + ($deltaB * $deltaB)
        if ($distance -lt $bestDistance)
        {
            $bestIndex = $index
            $bestDistance = $distance
        }
    }
    return $bestIndex
}

function Format-ByteRows([byte[]]$Bytes)
{
    $lines = [System.Collections.Generic.List[string]]::new()
    for ($offset = 0; $offset -lt $Bytes.Length; $offset += 4)
    {
        $count = [Math]::Min(4, $Bytes.Length - $offset)
        $values = for ($index = 0; $index -lt $count; ++$index)
        {
            'std::byte{{0x{0:x2}U}}' -f $Bytes[$offset + $index]
        }
        $lines.Add('    ' + ($values -join ', ') + ',')
    }
    return $lines -join "`n"
}

if (-not [System.IO.File]::Exists($InputImage))
{
    throw "Input image does not exist: $InputImage"
}

$extension = [System.IO.Path]::GetExtension($InputImage).ToLowerInvariant()
if ($extension -notin @('.png', '.bmp', '.jpg', '.jpeg', '.gif', '.tif', '.tiff'))
{
    throw 'The deterministic GDI+ converter accepts PNG, BMP, JPEG, GIF, or TIFF raster sources.'
}

if ($Dither -ne 'None' -and $Format -notin @('Indexed4', 'Rgb565'))
{
    throw 'Ordered dithering is defined only for Indexed4 and Rgb565 output.'
}

if ($Format -eq 'R2Packed2x2' -and (($OutputWidth % 2) -ne 0 -or ($OutputHeight % 2) -ne 0))
{
    throw 'Native R2 output requires even logical width and height for complete 2-by-2 blocks.'
}

$palette = [System.Collections.Generic.List[System.Drawing.Color]]::new()
if (-not [string]::IsNullOrWhiteSpace($PaletteArgb))
{
    foreach ($entry in $PaletteArgb.Split(',', [System.StringSplitOptions]::RemoveEmptyEntries))
    {
        $trimmed = $entry.Trim()
        if ($trimmed -notmatch '^#?[0-9A-Fa-f]{8}$')
        {
            throw "Invalid ARGB palette entry: $trimmed"
        }
        $palette.Add((Convert-HexColor $trimmed))
    }
}
if ($Format -eq 'Indexed4' -and ($palette.Count -lt 1 -or $palette.Count -gt 16))
{
    throw 'Indexed4 output requires 1 through 16 explicit AARRGGBB palette entries.'
}
if ($Format -ne 'Indexed4' -and $palette.Count -ne 0)
{
    throw 'A palette is valid only for Indexed4 output.'
}

$sourceImage = [System.Drawing.Image]::FromFile($InputImage)
$sourceBitmap = $null
try
{
    if (($CropX + $CropWidth) -gt $sourceImage.Width -or
        ($CropY + $CropHeight) -gt $sourceImage.Height)
    {
        throw "Crop rectangle exceeds the $($sourceImage.Width)-by-$($sourceImage.Height) source image."
    }

    $sourceBitmap = [System.Drawing.Bitmap]::new(
        $sourceImage.Width,
        $sourceImage.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sourceGraphics = [System.Drawing.Graphics]::FromImage($sourceBitmap)
    try
    {
        $sourceGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $sourceGraphics.DrawImageUnscaled($sourceImage, 0, 0)
    }
    finally
    {
        $sourceGraphics.Dispose()
    }

    $stride = switch ($Format)
    {
        'Alpha1' { [int][Math]::Ceiling($OutputWidth / 8.0); break }
        'Alpha8' { $OutputWidth; break }
        'Indexed4' { [int][Math]::Ceiling($OutputWidth / 2.0); break }
        'Rgb565' { $OutputWidth * 2; break }
        'Rgb888' { $OutputWidth * 3; break }
        'Xrgb8888' { $OutputWidth * 4; break }
        'Argb8888' { $OutputWidth * 4; break }
        'R2Packed2x2' { $OutputWidth / 2; break }
    }
    $storageRows = if ($Format -eq 'R2Packed2x2') { $OutputHeight / 2 } else { $OutputHeight }
    $payloadLength = [long]$stride * $storageRows
    if ($payloadLength -le 0 -or $payloadLength -gt $maximumPayloadBytes)
    {
        throw "Generated payload is $payloadLength bytes; the converter limit is $maximumPayloadBytes bytes."
    }
    $payload = [byte[]]::new([int]$payloadLength)
    $matte = Convert-HexColor $MatteArgb
    $destinationPreservesAlpha = $Format -in @('Alpha1', 'Alpha8', 'Indexed4', 'Argb8888')
    for ($y = 0; $y -lt $OutputHeight; ++$y)
    {
        $rowOffset = $y * $stride
        for ($x = 0; $x -lt $OutputWidth; ++$x)
        {
            $sample = Get-SampledColor $sourceBitmap $x $y
            $color = Normalize-Color $sample $matte $destinationPreservesAlpha
            if ($Dither -ne 'None')
            {
                $color = Get-DitheredColor $color $x $y
            }
            switch ($Format)
            {
                'R2Packed2x2'
                {
                    # One byte per 2x2 block: TL bits3:2, TR7:6, BL1:0, BR5:4.
                    $blockOffset = [int][Math]::Floor($y / 2.0) * $stride +
                        [int][Math]::Floor($x / 2.0)
                    $shift = (($x % 2) * 4) + $(if (($y % 2) -eq 0) { 2 } else { 0 })
                    $level = [int][Math]::Floor((([int]$color.R * 3) + 127) / 255.0)
                    $payload[$blockOffset] = $payload[$blockOffset] -bor [byte]($level -shl $shift)
                    break
                }
                'Alpha1'
                {
                    if ($color.A -ge $AlphaThreshold)
                    {
                        $byteOffset = $rowOffset + [int][Math]::Floor($x / 8.0)
                        $payload[$byteOffset] = $payload[$byteOffset] -bor (0x80 -shr ($x % 8))
                    }
                    break
                }
                'Alpha8'
                {
                    $payload[$rowOffset + $x] = $color.A
                    break
                }
                'Indexed4'
                {
                    $paletteIndex = Get-NearestPaletteIndex $color $palette
                    $offset = $rowOffset + [int][Math]::Floor($x / 2.0)
                    if (($x % 2) -eq 0)
                    {
                        $payload[$offset] = [byte]($paletteIndex -shl 4)
                    }
                    else
                    {
                        $payload[$offset] = $payload[$offset] -bor [byte]$paletteIndex
                    }
                    break
                }
                'Rgb565'
                {
                    [uint16]$encoded = (($color.R -band 0xf8) -shl 8) -bor
                        (($color.G -band 0xfc) -shl 3) -bor ($color.B -shr 3)
                    $offset = $rowOffset + ($x * 2)
                    $payload[$offset] = [byte]($encoded -band 0xff)
                    $payload[$offset + 1] = [byte]($encoded -shr 8)
                    break
                }
                'Rgb888'
                {
                    $offset = $rowOffset + ($x * 3)
                    $payload[$offset] = $color.R
                    $payload[$offset + 1] = $color.G
                    $payload[$offset + 2] = $color.B
                    break
                }
                'Xrgb8888'
                {
                    $offset = $rowOffset + ($x * 4)
                    $payload[$offset] = $color.B
                    $payload[$offset + 1] = $color.G
                    $payload[$offset + 2] = $color.R
                    $payload[$offset + 3] = 255
                    break
                }
                'Argb8888'
                {
                    $offset = $rowOffset + ($x * 4)
                    $payload[$offset] = $color.B
                    $payload[$offset + 1] = $color.G
                    $payload[$offset + 2] = $color.R
                    $payload[$offset + 3] = $color.A
                    break
                }
            }
        }
    }

    $formatName = $Format.Substring(0, 1).ToLowerInvariant() + $Format.Substring(1)
    $paletteRows = ''
    $paletteArgument = '{}'
    if ($palette.Count -gt 0)
    {
        $paletteValues = foreach ($color in $palette)
        {
            '    Color::argb(0x{0}U),' -f (Convert-ColorHex $color)
        }
        $paletteRows = @"
inline constexpr std::array<Color, $($palette.Count)> k${Symbol}Palette{
$($paletteValues -join "`n")
};
"@
        $paletteArgument = "k${Symbol}Palette"
    }
    $tintHex = (Convert-ColorHex (Convert-HexColor $TintArgb))
    $sourceText = @"
#include "lithe/painter.hpp"

#include <array>
#include <cstddef>

namespace lithe
{

namespace
{

/*
 * Generated by $converterId $converterVersion from a reviewed, hash-locked source image.
 * The desktop converter already decoded, cropped, scaled, alpha-processed, quantized, and packed
 * the pixels. Embedded code borrows this immutable storage directly; it performs no file I/O,
 * image decoding, allocation, or startup conversion.
 *
 * Format notes:
 * - Alpha1/Alpha8 store coverage only. Bitmap::tint supplies color at draw time, so the same icon
 *   can be reused for normal, focused, pressed, disabled, or themed states without duplicating the
 *   mask bytes.
 * - Indexed4 stores two high-nibble-first palette indexes per byte. Palette entry zero has no
 *   implicit meaning; transparency comes from that entry's Color alpha.
 * - RGB565 and the direct-color formats are already in LitheView's native Bitmap byte order.
 *
 * The resource manifest records source/output hashes and every conversion option. Regenerate this
 * file instead of editing the arrays. Linker placement and provider wiring remain application
 * decisions and intentionally do not appear in this generated visual resource.
 */
// Generated table packing is deterministic and intentionally excluded from mechanical reflow.
// clang-format off
inline constexpr std::array<std::byte, $($payload.Length)> k${Symbol}Pixels{
$(Format-ByteRows $payload)
};

$paletteRows
inline constexpr Bitmap k${Symbol}Bitmap{
    k${Symbol}Pixels,
    $OutputWidth,
    $OutputHeight,
    $stride`U,
    BitmapFormat::$formatName,
    Color::argb(0x${tintHex}U),
    $paletteArgument};
// clang-format on

static_assert(k${Symbol}Bitmap.valid());

} // namespace

const Bitmap& $EntryPoint() noexcept
{
    return k${Symbol}Bitmap;
}

} // namespace lithe
"@

    $sourceDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutputSource))
    $manifestDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutputManifest))
    [System.IO.Directory]::CreateDirectory($sourceDirectory) | Out-Null
    [System.IO.Directory]::CreateDirectory($manifestDirectory) | Out-Null
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($OutputSource, ($sourceText -replace "`r`n", "`n"), $utf8)

    $paletteBytes = [byte[]]::new($palette.Count * 4)
    for ($index = 0; $index -lt $palette.Count; ++$index)
    {
        $paletteBytes[($index * 4)] = $palette[$index].R
        $paletteBytes[($index * 4) + 1] = $palette[$index].G
        $paletteBytes[($index * 4) + 2] = $palette[$index].B
        $paletteBytes[($index * 4) + 3] = $palette[$index].A
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputPixels))
    {
        $pixelPath = [System.IO.Path]::GetFullPath($OutputPixels)
        [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($pixelPath)) | Out-Null
        [System.IO.File]::WriteAllBytes($pixelPath, $payload)
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputPalette))
    {
        $palettePath = [System.IO.Path]::GetFullPath($OutputPalette)
        [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($palettePath)) | Out-Null
        [System.IO.File]::WriteAllBytes($palettePath, $paletteBytes)
    }
    $combined = [byte[]]::new($payload.Length + $paletteBytes.Length)
    [Array]::Copy($payload, 0, $combined, 0, $payload.Length)
    [Array]::Copy($paletteBytes, 0, $combined, $payload.Length, $paletteBytes.Length)
    $canonicalPalette = ($palette | ForEach-Object { Convert-ColorHex $_ }) -join ','
    $metrics = [ordered]@{
        schema_version = 1
        converter_id = $converterId
        converter_version = $converterVersion
        kind = $Kind.ToLowerInvariant()
        tintable = $Tintable -eq 'True'
        format = $formatName.ToLowerInvariant()
        source_width = $sourceBitmap.Width
        source_height = $sourceBitmap.Height
        crop_x = $CropX
        crop_y = $CropY
        crop_width = $CropWidth
        crop_height = $CropHeight
        output_width = $OutputWidth
        output_height = $OutputHeight
        stride_bytes = $stride
        scale_mode = $ScaleMode
        alpha_mode = $AlphaMode
        alpha_threshold = $AlphaThreshold
        dither = $Dither
        tint_argb = $tintHex
        matte_argb = (Convert-ColorHex $matte)
        palette_argb = $canonicalPalette
        symbol = $Symbol
        entry_point = $EntryPoint
        pixel_bytes = $payload.Length
        palette_bytes = $paletteBytes.Length
        estimated_rom_bytes = $combined.Length
        pixel_checksum_fnv1a32 = (Get-Fnv1a32 $payload)
        palette_checksum_fnv1a32 = (Get-Fnv1a32 $paletteBytes)
        data_checksum_fnv1a32 = (Get-Fnv1a32 $combined)
        source_sha256 = (Get-Sha256 $InputImage)
    }
    $manifestText = ($metrics | ConvertTo-Json -Depth 4) + "`n"
    [System.IO.File]::WriteAllText($OutputManifest, ($manifestText -replace "`r`n", "`n"), $utf8)

    Write-Output 'CONVERTED'
    Write-Output "kind: $($metrics.kind)"
    Write-Output "format: $($metrics.format)"
    Write-Output "dimensions: $OutputWidth x $OutputHeight"
    Write-Output "pixel-bytes: $($metrics.pixel_bytes)"
    Write-Output "palette-bytes: $($metrics.palette_bytes)"
    Write-Output "estimated-rom-bytes: $($metrics.estimated_rom_bytes)"
    Write-Output "data-checksum-fnv1a32: $($metrics.data_checksum_fnv1a32)"
}
finally
{
    if ($null -ne $sourceBitmap)
    {
        $sourceBitmap.Dispose()
    }
    $sourceImage.Dispose()
}
