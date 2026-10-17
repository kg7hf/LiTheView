# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

param(
    [Parameter(Mandatory = $true)]
    [string]$InputSpriteSheet,

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
    [ValidateSet('True', 'False')]
    [string]$Tintable,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Alpha1', 'Alpha8', 'Indexed4', 'Rgb565', 'Rgb888', 'Xrgb8888', 'Argb8888')]
    [string]$Format,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 16383)]
    [int]$SheetX,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 16383)]
    [int]$SheetY,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 16384)]
    [int]$SourceFrameWidth,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 16384)]
    [int]$SourceFrameHeight,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 256)]
    [int]$Columns,

    [Parameter(Mandatory = $true)]
    [ValidateRange(2, 256)]
    [int]$FrameCount,

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

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int]$FrameIntervalMilliseconds,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 255)]
    [int]$RepeatIndex,

    [Parameter(Mandatory = $true)]
    [ValidateSet('True', 'False')]
    [string]$Autoplay,

    [string]$PaletteArgb = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

trap
{
    Write-Error (
        'Animation conversion failed at line {0}: {1}' -f
            $_.InvocationInfo.ScriptLineNumber,
            $_.Exception.Message)
    exit 1
}

$converterId = 'litheview.converter.animation.gdiplus'
$converterVersion = '1.0.0'
$maximumPayloadBytes = 64MB

function Get-Sha256([string]$Path)
{
    $stream = [System.IO.File]::OpenRead($Path)
    try
    {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try
        {
            return -join ($algorithm.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') })
        }
        finally
        {
            $algorithm.Dispose()
        }
    }
    finally
    {
        $stream.Dispose()
    }
}

function Get-Fnv1a32([byte[]]$Bytes)
{
    [uint64]$hash = 2166136261
    foreach ($value in $Bytes)
    {
        $hash = (($hash -bxor [uint64]$value) * [uint64]16777619) -band [uint64]4294967295
    }
    return ('0x{0:x8}' -f [uint32]$hash)
}

function Format-ByteRows([byte[]]$Bytes)
{
    $rows = [System.Collections.Generic.List[string]]::new()
    for ($offset = 0; $offset -lt $Bytes.Length; $offset += 4)
    {
        $last = [Math]::Min($offset + 4, $Bytes.Length)
        $values = for ($index = $offset; $index -lt $last; ++$index)
        {
            'std::byte{{0x{0:x2}U}}' -f $Bytes[$index]
        }
        $rows.Add(('    {0},' -f ($values -join ', ')))
    }
    return $rows -join "`n"
}

function Get-LowerFormatName([string]$Value)
{
    return $Value.Substring(0, 1).ToLowerInvariant() + $Value.Substring(1)
}

function Test-ByteArraysEqual([byte[]]$Left, [byte[]]$Right)
{
    if ($Left.Length -ne $Right.Length)
    {
        return $false
    }
    for ($index = 0; $index -lt $Left.Length; ++$index)
    {
        if ($Left[$index] -ne $Right[$index])
        {
            return $false
        }
    }
    return $true
}

if (-not [System.IO.File]::Exists($InputSpriteSheet))
{
    throw "Input sprite sheet does not exist: $InputSpriteSheet"
}
if ($RepeatIndex -ge $FrameCount)
{
    throw "Repeat index $RepeatIndex does not identify one of $FrameCount frames."
}
if ($Tintable -eq 'True' -and $Format -notin @('Alpha1', 'Alpha8'))
{
    throw 'Tintable animations require Alpha1 or Alpha8 coverage frames.'
}
if ($Dither -ne 'None' -and $Format -notin @('Indexed4', 'Rgb565'))
{
    throw 'Ordered dithering is defined only for Indexed4 and Rgb565 output.'
}

$paletteEntries = [System.Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($PaletteArgb))
{
    foreach ($entry in $PaletteArgb.Split(',', [System.StringSplitOptions]::RemoveEmptyEntries))
    {
        $canonical = $entry.Trim().TrimStart('#').ToLowerInvariant()
        if ($canonical -notmatch '^[0-9a-f]{8}$')
        {
            throw "Invalid ARGB palette entry: $entry"
        }
        $paletteEntries.Add($canonical)
    }
}
if ($Format -eq 'Indexed4' -and ($paletteEntries.Count -lt 1 -or $paletteEntries.Count -gt 16))
{
    throw 'Indexed4 output requires 1 through 16 explicit AARRGGBB palette entries.'
}
if ($Format -ne 'Indexed4' -and $paletteEntries.Count -ne 0)
{
    throw 'A palette is valid only for Indexed4 output.'
}
$canonicalPalette = $paletteEntries -join ','
$canonicalTint = $TintArgb.TrimStart('#').ToLowerInvariant()
$canonicalMatte = $MatteArgb.TrimStart('#').ToLowerInvariant()

$sourceImage = [System.Drawing.Image]::FromFile($InputSpriteSheet)
$temporaryDirectory = [System.IO.Path]::Combine(
    [System.IO.Path]::GetTempPath(),
    'litheview-animation-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
try
{
    $rows = [int][Math]::Ceiling($FrameCount / [double]$Columns)
    $requiredWidth = $Columns * $SourceFrameWidth
    $requiredHeight = $rows * $SourceFrameHeight
    if (($SheetX + $requiredWidth) -gt $sourceImage.Width -or
        ($SheetY + $requiredHeight) -gt $sourceImage.Height)
    {
        throw (
            "The $Columns-column, $rows-row frame grid at ($SheetX,$SheetY) exceeds the " +
            "$($sourceImage.Width)-by-$($sourceImage.Height) sprite sheet.")
    }

    $allPixels = [System.Collections.Generic.List[byte]]::new()
    [byte[]]$sharedPalette = @()
    $stride = 0
    $framePixelBytes = 0
    $powerShell = (Get-Command powershell.exe -CommandType Application -ErrorAction Stop).Source
    $imageConverter = [System.IO.Path]::Combine($PSScriptRoot, 'convert-image.ps1')
    for ($frameIndex = 0; $frameIndex -lt $FrameCount; ++$frameIndex)
    {
        $sourceColumn = $frameIndex % $Columns
        $sourceRow = [int][Math]::Floor($frameIndex / [double]$Columns)
        $framePath = [System.IO.Path]::Combine($temporaryDirectory, "frame-$frameIndex.png")
        $frameBitmap = [System.Drawing.Bitmap]::new(
            $SourceFrameWidth,
            $SourceFrameHeight,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($frameBitmap)
            try
            {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $destination = [System.Drawing.Rectangle]::new(
                    0, 0, $SourceFrameWidth, $SourceFrameHeight)
                $source = [System.Drawing.Rectangle]::new(
                    $SheetX + ($sourceColumn * $SourceFrameWidth),
                    $SheetY + ($sourceRow * $SourceFrameHeight),
                    $SourceFrameWidth,
                    $SourceFrameHeight)
                $graphics.DrawImage(
                    $sourceImage,
                    $destination,
                    $source,
                    [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally
            {
                $graphics.Dispose()
            }
            $frameBitmap.Save($framePath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $frameBitmap.Dispose()
        }

        $frameSource = [System.IO.Path]::Combine($temporaryDirectory, "frame-$frameIndex.cpp")
        $frameMetrics = [System.IO.Path]::Combine($temporaryDirectory, "frame-$frameIndex.json")
        $framePixels = [System.IO.Path]::Combine($temporaryDirectory, "frame-$frameIndex.bin")
        $framePalette = [System.IO.Path]::Combine($temporaryDirectory, "palette-$frameIndex.bin")
        $imageKind = if ($Tintable -eq 'True') { 'Icon' } else { 'Image' }
        $arguments = @(
            '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $imageConverter,
            '-InputImage', $framePath,
            '-OutputSource', $frameSource,
            '-OutputManifest', $frameMetrics,
            '-Symbol', 'AnimationFrame',
            '-EntryPoint', 'animationFrame',
            '-Kind', $imageKind,
            '-Tintable', $Tintable,
            '-Format', $Format,
            '-CropX', '0',
            '-CropY', '0',
            '-CropWidth', $SourceFrameWidth.ToString([Globalization.CultureInfo]::InvariantCulture),
            '-CropHeight', $SourceFrameHeight.ToString([Globalization.CultureInfo]::InvariantCulture),
            '-OutputWidth', $OutputWidth.ToString([Globalization.CultureInfo]::InvariantCulture),
            '-OutputHeight', $OutputHeight.ToString([Globalization.CultureInfo]::InvariantCulture),
            '-ScaleMode', $ScaleMode,
            '-AlphaMode', $AlphaMode,
            '-AlphaThreshold', $AlphaThreshold.ToString([Globalization.CultureInfo]::InvariantCulture),
            '-Dither', $Dither,
            '-TintArgb', $canonicalTint,
            '-MatteArgb', $canonicalMatte,
            '-OutputPixels', $framePixels,
            '-OutputPalette', $framePalette)
        # Windows PowerShell drops empty native arguments. Omit this optional value
        # when there is no palette so the next switch cannot be consumed as its value.
        if ($canonicalPalette.Length -ne 0)
        {
            $arguments += @('-PaletteArgb', $canonicalPalette)
        }
        & $powerShell @arguments | Out-Null
        if ($LASTEXITCODE -ne 0)
        {
            throw "Image conversion failed for animation frame $frameIndex."
        }

        $metrics = Get-Content -LiteralPath $frameMetrics -Raw | ConvertFrom-Json
        [byte[]]$pixels = [System.IO.File]::ReadAllBytes($framePixels)
        [byte[]]$palette = [System.IO.File]::ReadAllBytes($framePalette)
        if ($frameIndex -eq 0)
        {
            $stride = [int]$metrics.stride_bytes
            $framePixelBytes = $pixels.Length
            $sharedPalette = $palette
        }
        elseif ($pixels.Length -ne $framePixelBytes -or
            -not (Test-ByteArraysEqual $sharedPalette $palette))
        {
            throw "Frame $frameIndex does not reproduce the shared representation contract."
        }
        $allPixels.AddRange($pixels)
    }

    [byte[]]$pixelBytes = $allPixels.ToArray()
    $representationBytes = $pixelBytes.Length + $sharedPalette.Length
    if ($representationBytes -le 0 -or $representationBytes -gt $maximumPayloadBytes)
    {
        throw "Generated animation representation is $representationBytes bytes; limit is $maximumPayloadBytes."
    }

    $paletteRows = ''
    $paletteArgument = '{}'
    if ($paletteEntries.Count -gt 0)
    {
        $colors = foreach ($entry in $paletteEntries)
        {
            "    Color::argb(0x$($entry)U),"
        }
        $paletteRows = @"
inline constexpr std::array<Color, $($paletteEntries.Count)> k${Symbol}Palette{
$($colors -join "`n")
};
"@
        $paletteArgument = "k${Symbol}Palette"
    }

    $bitmapRows = [System.Collections.Generic.List[string]]::new()
    for ($frameIndex = 0; $frameIndex -lt $FrameCount; ++$frameIndex)
    {
        $offset = $frameIndex * $framePixelBytes
        $bitmapRows.Add(@"
    Bitmap{std::span<const std::byte>{k${Symbol}Pixels}.subspan($offset`U, $framePixelBytes`U),
           $OutputWidth,
           $OutputHeight,
           $stride`U,
           BitmapFormat::$(Get-LowerFormatName $Format),
           Color::argb(0x${canonicalTint}U),
           $paletteArgument},
"@)
    }
    $autoplayLiteral = if ($Autoplay -eq 'True') { 'true' } else { 'false' }
    $sourceText = @"
#include "lithe/painter.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <span>

namespace lithe
{

namespace
{

/*
 * Generated by $converterId $converterVersion from a reviewed, hash-locked sprite
 * sheet. Frames are extracted in row-major order from the declared grid, converted through the same
 * deterministic Bitmap packer used by single images, and concatenated without runtime decoding,
 * allocation, or frame construction. Every Bitmap borrows immutable storage below.
 *
 * Frame timing is kept separate from pixels because AnimatedImageView is caller-clocked. The
 * accessor functions expose the reviewed interval, repeat index, and autoplay default so an
 * application can wire them into its form or provider without copying the frame payload.
 *
 * A typical application declares std::span<const Bitmap> $EntryPoint() noexcept, calls it once
 * while configuring its ResourceProvider, and maps stable names such as $($EntryPoint)-f0 through
 * $($EntryPoint)-f$($FrameCount - 1) to the corresponding borrowed descriptors. AnimatedImageView
 * receives those same names in frame order. The returned span and every Bitmap remain valid for the
 * program lifetime because the descriptors, pixel bytes, and optional palette all have immutable
 * static storage in this translation unit. Do not copy pixels just to rename a frame.
 *
 * The interval is a default, not a hidden timer. Feed elapsed monotonic time through the normal
 * LitheView application path; pause, resume, manual stepping, and product-specific scheduling stay
 * application-owned. Repeat index selects the frame used after the last frame (usually zero, but a
 * nonzero intro-to-loop sequence is supported). Autoplay is likewise authoring metadata for the
 * caller to honor or override.
 *
 * Regenerate this file rather than editing its tables. The resource manifest locks the source,
 * grid, conversion, timing, checksums, and byte counts. Linker/storage placement remains an
 * application decision and intentionally does not appear in this visual resource.
 */
// Generated table packing is deterministic and intentionally excluded from mechanical reflow.
// clang-format off
inline constexpr std::array<std::byte, $($pixelBytes.Length)> k${Symbol}Pixels{
$(Format-ByteRows $pixelBytes)
};

$paletteRows
inline constexpr std::array<Bitmap, $FrameCount> k${Symbol}Frames{{
$($bitmapRows -join "`n")}};
// clang-format on

static_assert(
    []() constexpr noexcept
    {
        for (const Bitmap& frame : k${Symbol}Frames)
        {
            if (!frame.valid())
            {
                return false;
            }
        }
        return true;
    }());

} // namespace

std::span<const Bitmap> $EntryPoint() noexcept
{
    return k${Symbol}Frames;
}

std::uint16_t ${EntryPoint}IntervalMilliseconds() noexcept
{
    return $FrameIntervalMilliseconds`U;
}

std::size_t ${EntryPoint}RepeatIndex() noexcept
{
    return $RepeatIndex`U;
}

bool ${EntryPoint}Autoplay() noexcept
{
    return $autoplayLiteral;
}

} // namespace lithe
"@

    $sourceDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutputSource))
    $manifestDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutputManifest))
    [System.IO.Directory]::CreateDirectory($sourceDirectory) | Out-Null
    [System.IO.Directory]::CreateDirectory($manifestDirectory) | Out-Null
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($OutputSource, ($sourceText -replace "`r`n", "`n"), $utf8)

    [byte[]]$combined = [byte[]]::new($representationBytes)
    [Array]::Copy($pixelBytes, 0, $combined, 0, $pixelBytes.Length)
    [Array]::Copy($sharedPalette, 0, $combined, $pixelBytes.Length, $sharedPalette.Length)
    $metrics = [ordered]@{
        schema_version = 1
        converter_id = $converterId
        converter_version = $converterVersion
        tintable = $Tintable -eq 'True'
        format = (Get-LowerFormatName $Format).ToLowerInvariant()
        source_width = $sourceImage.Width
        source_height = $sourceImage.Height
        sheet_x = $SheetX
        sheet_y = $SheetY
        source_frame_width = $SourceFrameWidth
        source_frame_height = $SourceFrameHeight
        columns = $Columns
        frame_count = $FrameCount
        output_width = $OutputWidth
        output_height = $OutputHeight
        stride_bytes = $stride
        scale_mode = $ScaleMode
        alpha_mode = $AlphaMode
        alpha_threshold = $AlphaThreshold
        dither = $Dither
        tint_argb = $canonicalTint
        matte_argb = $canonicalMatte
        palette_argb = $canonicalPalette
        frame_interval_milliseconds = $FrameIntervalMilliseconds
        repeat_index = $RepeatIndex
        autoplay = $Autoplay -eq 'True'
        symbol = $Symbol
        entry_point = $EntryPoint
        frame_pixel_bytes = $framePixelBytes
        pixel_bytes = $pixelBytes.Length
        palette_bytes = $sharedPalette.Length
        estimated_rom_bytes = $representationBytes
        pixel_checksum_fnv1a32 = Get-Fnv1a32 $pixelBytes
        palette_checksum_fnv1a32 = Get-Fnv1a32 $sharedPalette
        data_checksum_fnv1a32 = Get-Fnv1a32 $combined
        source_sha256 = Get-Sha256 $InputSpriteSheet
    }
    [System.IO.File]::WriteAllText(
        $OutputManifest,
        (($metrics | ConvertTo-Json -Depth 4) -replace "`r`n", "`n") + "`n",
        $utf8)

    Write-Output 'CONVERTED'
    Write-Output "kind: animation"
    Write-Output "format: $($metrics.format)"
    Write-Output "frames: $FrameCount"
    Write-Output "frame-dimensions: $OutputWidth x $OutputHeight"
    Write-Output "pixel-bytes: $($metrics.pixel_bytes)"
    Write-Output "palette-bytes: $($metrics.palette_bytes)"
    Write-Output "estimated-rom-bytes: $representationBytes"
    Write-Output "data-checksum-fnv1a32: $($metrics.data_checksum_fnv1a32)"
}
finally
{
    $sourceImage.Dispose()
    if ([System.IO.Directory]::Exists($temporaryDirectory))
    {
        [System.IO.Directory]::Delete($temporaryDirectory, $true)
    }
}

exit 0
