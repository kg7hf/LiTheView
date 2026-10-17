# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputFont,

    [Parameter(Mandatory = $true)]
    [string]$OutputSource,

    [Parameter(Mandatory = $true)]
    [string]$OutputManifest,

    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string]$Symbol = 'GeneratedSans',

    [ValidatePattern('^[a-z][A-Za-z0-9]*$')]
    [string]$EntryPoint = 'generatedSans',

    [ValidateRange(4, 64)]
    [int]$PixelSize = 14,

    [ValidateSet('Regular', 'Bold')]
    [string]$FontStyle = 'Regular',

    [string]$StyleLabel = '',

    [ValidateRange(0, 1000)]
    [int]$Weight = 0,

    [ValidateSet('Alpha1', 'Alpha8')]
    [string]$Coverage = 'Alpha8',

    [string]$Codepoints = 'U+0020-U+007E,U+00B0,U+00E9,U+2026,U+FFFD'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertFrom-CodepointToken
{
    param([Parameter(Mandatory = $true)][string]$Token)

    $trimmed = $Token.Trim()
    if ($trimmed -notmatch '^U\+([0-9A-Fa-f]{4,6})(?:-U\+([0-9A-Fa-f]{4,6}))?$')
    {
        throw "Invalid code-point token '$Token'. Use U+0020 or U+0020-U+007E."
    }
    $first = [Convert]::ToInt32($Matches[1], 16)
    $last = if ($Matches[2]) { [Convert]::ToInt32($Matches[2], 16) } else { $first }
    if ($first -gt $last -or $last -gt 0x10ffff)
    {
        throw "Invalid code-point range '$Token'."
    }
    if (($first -le 0xdfff -and $last -ge 0xd800))
    {
        throw "Code-point range '$Token' includes a UTF-16 surrogate."
    }
    return $first..$last
}

function Format-ByteArray
{
    param([Parameter(Mandatory = $true)][System.Collections.Generic.List[byte]]$Bytes)

    $lines = [System.Collections.Generic.List[string]]::new()
    for ($offset = 0; $offset -lt $Bytes.Count; $offset += 12)
    {
        $last = [Math]::Min($offset + 11, $Bytes.Count - 1)
        $values = for ($index = $offset; $index -le $last; ++$index)
        {
            '0x{0:x2}U' -f $Bytes[$index]
        }
        $lines.Add('    ' + ($values -join ', ') + ',')
    }
    return $lines -join "`n"
}

function Format-Codepoint
{
    param([Parameter(Mandatory = $true)][int]$Codepoint)

    $digits = if ($Codepoint -le 0xffff) { 'X4' } else { 'X6' }
    return 'U+{0}' -f $Codepoint.ToString($digits, [Globalization.CultureInfo]::InvariantCulture)
}

function Format-CodepointSet
{
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.SortedSet[int]]$Values
    )

    $ranges = [System.Collections.Generic.List[string]]::new()
    $first = -1
    $last = -1
    foreach ($value in $Values)
    {
        if ($first -lt 0)
        {
            $first = $value
            $last = $value
            continue
        }
        if ($value -eq $last + 1)
        {
            $last = $value
            continue
        }
        $firstText = Format-Codepoint -Codepoint $first
        $ranges.Add($(if ($first -eq $last)
                {
                    $firstText
                }
                else
                {
                    '{0}-{1}' -f $firstText, (Format-Codepoint -Codepoint $last)
                }))
        $first = $value
        $last = $value
    }
    if ($first -ge 0)
    {
        $firstText = Format-Codepoint -Codepoint $first
        $ranges.Add($(if ($first -eq $last)
                {
                    $firstText
                }
                else
                {
                    '{0}-{1}' -f $firstText, (Format-Codepoint -Codepoint $last)
                }))
    }
    return $ranges -join ','
}

function Get-Fnv1a32
{
    param([Parameter(Mandatory = $true)][System.Collections.Generic.List[byte]]$Bytes)

    [uint32]$hash = 2166136261
    foreach ($value in $Bytes)
    {
        $hash = $hash -bxor $value
        $hash = [uint32](([uint64]$hash * 16777619) -band 0xffffffffL)
    }
    return $hash
}

function Get-Sha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        $digest = $sha256.ComputeHash($stream)
        return -join ($digest | ForEach-Object {
                $_.ToString('x2', [Globalization.CultureInfo]::InvariantCulture)
            })
    }
    finally
    {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

function ConvertTo-CppStringContents
{
    param([Parameter(Mandatory = $true)][string]$Value)

    foreach ($character in $Value.ToCharArray())
    {
        if ([int]$character -lt 0x20 -and $character -notin "`t", "`r", "`n")
        {
            throw 'Font metadata contains a control character that cannot be emitted safely.'
        }
    }
    return $Value.Replace('\', '\\').Replace('"', '\"').Replace("`t", '\t').Replace(
        "`r", '\r').Replace("`n", '\n')
}

$resolvedInput = (Resolve-Path -LiteralPath $InputFont).Path
$sourceOutput = [System.IO.Path]::GetFullPath($OutputSource)
$manifestOutput = [System.IO.Path]::GetFullPath($OutputManifest)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($sourceOutput)) | Out-Null
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($manifestOutput)) |
    Out-Null

$selectedCodepoints = [System.Collections.Generic.SortedSet[int]]::new()
foreach ($token in $Codepoints.Split(','))
{
    foreach ($codepoint in ConvertFrom-CodepointToken -Token $token)
    {
        [void]$selectedCodepoints.Add($codepoint)
    }
}
if (-not $selectedCodepoints.Contains([int][char]'?'))
{
    throw 'The selected repertoire must contain U+003F for deterministic missing-glyph fallback.'
}
if ($selectedCodepoints.Count -gt [uint16]::MaxValue)
{
    throw 'The selected repertoire exceeds the 65,535-glyph manifest capacity.'
}
$canonicalCodepoints = Format-CodepointSet -Values $selectedCodepoints

Add-Type -AssemblyName System.Drawing
$fontCollection = [System.Drawing.Text.PrivateFontCollection]::new()
$fontCollection.AddFontFile($resolvedInput)
if ($fontCollection.Families.Count -ne 1)
{
    throw 'The input must expose exactly one font family.'
}
$fontFamily = $fontCollection.Families[0]
$familyName = $fontFamily.Name
$styleName = if ([string]::IsNullOrWhiteSpace($StyleLabel)) { $FontStyle } else { $StyleLabel }
$resolvedWeight = if ($Weight -eq 0)
{
    if ($FontStyle -eq 'Bold') { 700 } else { 400 }
}
else
{
    $Weight
}
$familyLiteral = ConvertTo-CppStringContents -Value $familyName
$styleLiteral = ConvertTo-CppStringContents -Value $styleName
$repertoireLiteral = ConvertTo-CppStringContents -Value $canonicalCodepoints
$drawingStyle = [System.Drawing.FontStyle][Enum]::Parse(
    [System.Drawing.FontStyle], $FontStyle)
if (-not $fontFamily.IsStyleAvailable($drawingStyle))
{
    throw "The '$FontStyle' style is not available in '$resolvedInput'."
}

$format = [System.Drawing.StringFormat]::GenericTypographic.Clone()
$format.FormatFlags = $format.FormatFlags -bor
    [System.Drawing.StringFormatFlags]::MeasureTrailingSpaces
$font = [System.Drawing.Font]::new(
    $fontFamily,
    [single]$PixelSize,
    $drawingStyle,
    [System.Drawing.GraphicsUnit]::Pixel)
$measurementBitmap = [System.Drawing.Bitmap]::new(4, 4)
$measurementGraphics = [System.Drawing.Graphics]::FromImage($measurementBitmap)
$measurementGraphics.PageUnit = [System.Drawing.GraphicsUnit]::Pixel

$emHeight = $fontFamily.GetEmHeight($drawingStyle)
$cellAscent = $fontFamily.GetCellAscent($drawingStyle)
$lineSpacingDesign = $fontFamily.GetLineSpacing($drawingStyle)
$baseline = [Math]::Ceiling($PixelSize * $cellAscent / $emHeight)
$lineHeight = [Math]::Ceiling($PixelSize * $lineSpacingDesign / $emHeight)
$underlinePosition = [Math]::Min($lineHeight - 1, $baseline + 1)
$underlineThickness = [Math]::Max(1, [Math]::Round($PixelSize / 12))
$baseline = [int]$baseline
$lineHeight = [int]$lineHeight
$underlinePosition = [int]$underlinePosition
$underlineThickness = [int]$underlineThickness

$coverageBytes = [System.Collections.Generic.List[byte]]::new()
$records = [System.Collections.Generic.List[object]]::new()
try
{
    foreach ($codepoint in $selectedCodepoints)
    {
        $text = [char]::ConvertFromUtf32($codepoint)
        $advance = [Math]::Ceiling(
            $measurementGraphics.MeasureString($text, $font, [int]::MaxValue, $format).Width)
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try
        {
            $path.AddString(
                $text,
                $fontFamily,
                [int]$drawingStyle,
                [single]$PixelSize,
                [System.Drawing.PointF]::new(0.0, 0.0),
                $format)
            $bounds = $path.GetBounds()
            $left = if ($path.PointCount -eq 0) { 0 } else { [Math]::Floor($bounds.Left) }
            $top = if ($path.PointCount -eq 0) { 0 } else { [Math]::Floor($bounds.Top) }
            $right = if ($path.PointCount -eq 0) { 0 } else { [Math]::Ceiling($bounds.Right) }
            $bottom = if ($path.PointCount -eq 0) { 0 } else { [Math]::Ceiling($bounds.Bottom) }
            $width = [int]($right - $left)
            $height = [int]($bottom - $top)
            if ($width -gt 255 -or $height -gt 255 -or $advance -gt 255 -or
                $left -lt -128 -or $left -gt 127 -or $top -lt -128 -or $top -gt 127)
            {
                throw ('Glyph U+{0:X4} exceeds the compact generated-font record limits.' -f
                    $codepoint)
            }

            $offset = $coverageBytes.Count
            $stride = if ($Coverage -eq 'Alpha1')
            {
                [int][Math]::Ceiling($width / 8)
            }
            else
            {
                $width
            }
            if ($width -gt 0 -and $height -gt 0)
            {
                $bitmap = [System.Drawing.Bitmap]::new(
                    $width,
                    $height,
                    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                try
                {
                    $graphics.Clear([System.Drawing.Color]::Transparent)
                    $graphics.SmoothingMode =
                        [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
                    $graphics.PixelOffsetMode =
                        [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                    $graphics.TranslateTransform([single]-$left, [single]-$top)
                    $graphics.FillPath([System.Drawing.Brushes]::White, $path)

                    for ($y = 0; $y -lt $height; ++$y)
                    {
                        if ($Coverage -eq 'Alpha1')
                        {
                            for ($byteColumn = 0; $byteColumn -lt $stride; ++$byteColumn)
                            {
                                [byte]$packed = 0
                                for ($bit = 0; $bit -lt 8; ++$bit)
                                {
                                    $x = ($byteColumn * 8) + $bit
                                    if ($x -lt $width -and $bitmap.GetPixel($x, $y).A -ge 128)
                                    {
                                        $packed = $packed -bor (0x80 -shr $bit)
                                    }
                                }
                                $coverageBytes.Add($packed)
                            }
                        }
                        else
                        {
                            for ($x = 0; $x -lt $width; ++$x)
                            {
                                $coverageBytes.Add($bitmap.GetPixel($x, $y).A)
                            }
                        }
                    }
                }
                finally
                {
                    $graphics.Dispose()
                    $bitmap.Dispose()
                }
            }
            $records.Add([pscustomobject]@{
                    Codepoint = $codepoint
                    Offset = $offset
                    Width = $width
                    Height = $height
                    Stride = $stride
                    Advance = [int]$advance
                    XOffset = [int]$left
                    YOffset = [int]$top
                })
        }
        finally
        {
            $path.Dispose()
        }
    }
}
finally
{
    $measurementGraphics.Dispose()
    $measurementBitmap.Dispose()
    $font.Dispose()
    $format.Dispose()
    $fontCollection.Dispose()
}

$sourceSha256 = Get-Sha256 -Path $resolvedInput
$checksum = Get-Fnv1a32 -Bytes $coverageBytes
$coverageEnumerator = if ($Coverage -eq 'Alpha1') { 'alpha1' } else { 'alpha8' }
$recordLines = foreach ($record in $records)
{
    '    {{char32_t{{0x{0:x8}U}}, {1}U, {2}U, {3}U, {4}U, {5}U, {6}, {7}}},' -f
        $record.Codepoint,
        $record.Offset,
        $record.Width,
        $record.Height,
        $record.Stride,
        $record.Advance,
        $record.XOffset,
        $record.YOffset
}
$byteLines = Format-ByteArray -Bytes $coverageBytes
$source = @"
// Generated by tools/convert-font.ps1. Do not edit by hand.
// See src/generated_font.cpp for the data contract and integration walkthrough.
#include "lithe/font.hpp"

#include <array>
#include <cstdint>
#include <span>

namespace lithe
{

namespace
{

// Generated table packing is deterministic and intentionally excluded from mechanical reflow.
// clang-format off
constexpr auto k${Symbol}Coverage = std::to_array<std::uint8_t>({
$byteLines
});

constexpr auto k${Symbol}Glyphs = std::to_array<GeneratedGlyphRecord>({
$($recordLines -join "`n")
});
// clang-format on

const GeneratedFontData k${Symbol}Data{
    std::span<const GeneratedGlyphRecord>{k${Symbol}Glyphs},
    std::as_bytes(std::span{k${Symbol}Coverage}),
    {"$familyLiteral",
     "$styleLiteral",
     "$repertoireLiteral",
     "$sourceSha256",
     0x$($checksum.ToString('x8'))U,
     $($coverageBytes.Count)U,
     $(($records.Count) * 16)U,
     $(($coverageBytes.Count) + (($records.Count) * 16))U,
     $($records.Count)U,
     $($PixelSize)U,
     GlyphCoverage::$coverageEnumerator},
    U'?',
    $($lineHeight)U,
    $($baseline)U,
    $($underlinePosition)U,
    $($underlineThickness)U};

} // namespace

const GeneratedFont& $EntryPoint() noexcept
{
    static const GeneratedFont font{k${Symbol}Data};
    return font;
}

} // namespace lithe
"@
$source = $source.Replace("`r`n", "`n").Replace("`r", "`n")
[System.IO.File]::WriteAllText($sourceOutput, $source, [System.Text.UTF8Encoding]::new($false))

$manifest = [ordered]@{
    schema_version = 2
    converter_id = 'litheview.converter.font.gdiplus'
    converter_version = '2.0.0'
    family = $familyName
    style = $styleName
    raster_style = $FontStyle
    weight = $resolvedWeight
    pixel_size = $PixelSize
    coverage = $Coverage.ToLowerInvariant()
    repertoire = $canonicalCodepoints
    symbol = $Symbol
    entry_point = $EntryPoint
    glyph_count = $records.Count
    coverage_bytes = $coverageBytes.Count
    record_bytes = $records.Count * 16
    estimated_rom_bytes = $coverageBytes.Count + ($records.Count * 16)
    data_checksum_fnv1a32 = '0x{0:x8}' -f $checksum
    source_sha256 = $sourceSha256
    fallback = 'U+003F'
    line_height = $lineHeight
    baseline = $baseline
    underline_position = $underlinePosition
    underline_thickness = $underlineThickness
}
$manifestJson = ($manifest | ConvertTo-Json -Depth 3).Replace("`r`n", "`n").Replace("`r", "`n")
[System.IO.File]::WriteAllText(
    $manifestOutput,
    $manifestJson + "`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Host ('Generated {0} glyphs, {1} coverage bytes, FNV-1a 0x{2:x8}.' -f
    $records.Count,
    $coverageBytes.Count,
    $checksum)
