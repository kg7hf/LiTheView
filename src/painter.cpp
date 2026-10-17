// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/painter.hpp"

#include "lithe/detail/number.hpp"
#include "lithe/render_config.hpp"

#include <cstdint>
#include <limits>

namespace lithe
{

namespace
{

template <PixelFormat Format>
std::size_t surfaceOffset(const Surface& surface, std::int32_t x, std::int32_t y) noexcept
{
    const auto row = detail::nonnegativeSize(y - surface.origin.y);
    const auto column = detail::nonnegativeSize(x - surface.origin.x);
    using Traits = PixelFormatTraits<Format>;
    constexpr auto kBlockWidth = detail::nonnegativeSize(Traits::kBlockWidth);
    constexpr auto kBlockHeight = detail::nonnegativeSize(Traits::kBlockHeight);
    return ((row / kBlockHeight) * surface.strideBytes) +
           ((column / kBlockWidth) * Traits::kBytesPerBlock);
}

template <PixelFormat Format>
void writePixelAtOffset(Surface& surface,
                        std::size_t offset,
                        Color color,
                        std::size_t fieldIndex = 0U) noexcept
{
    using Traits = PixelFormatTraits<Format>;
    if constexpr (Format == PixelFormat::rrrr2222 || Format == PixelFormat::ryry2222)
    {
        Traits::compose(surface.data, offset, color);
    }
    else if constexpr (Traits::kBlockWidth == 1 && Traits::kBlockHeight == 1)
    {
        if (color.alpha != 255U)
        {
            color = color.blendedOver(Traits::read(surface.data, offset));
        }
        Traits::write(surface.data, offset, color);
    }
    else
    {
        if (color.alpha != 255U)
        {
            color = color.blendedOver(Traits::read(surface.data, offset, fieldIndex));
        }
        Traits::write(surface.data, offset, fieldIndex, color);
    }
}

template <PixelFormat Format>
void putPixel(Surface& surface,
              Rect clip,
              Viewport viewport,
              std::int32_t x,
              std::int32_t y,
              Color color) noexcept
{
    if (!clip.contains({x, y}) || !viewport.contains({x, y}) || color.alpha == 0U)
    {
        return;
    }
    using Traits = PixelFormatTraits<Format>;
    constexpr auto kBlockWidth = detail::nonnegativeSize(Traits::kBlockWidth);
    constexpr auto kBlockHeight = detail::nonnegativeSize(Traits::kBlockHeight);
    const auto column = detail::nonnegativeSize(x - surface.origin.x);
    const auto row = detail::nonnegativeSize(y - surface.origin.y);
    const auto fieldIndex = (row % kBlockHeight) * kBlockWidth + (column % kBlockWidth);
    writePixelAtOffset<Format>(surface, surfaceOffset<Format>(surface, x, y), color, fieldIndex);
}

template <typename Operation> void dispatchSurface(Surface& surface, Operation operation) noexcept
{
    switch (surface.format)
    {
    case PixelFormat::rgb565:
        if constexpr (render_config::kRgb565Enabled)
        {
            operation.template operator()<PixelFormat::rgb565>();
        }
        break;
    case PixelFormat::rgb888:
        if constexpr (render_config::kRgb888Enabled)
        {
            operation.template operator()<PixelFormat::rgb888>();
        }
        break;
    case PixelFormat::xrgb8888:
        if constexpr (render_config::kXrgb8888Enabled)
        {
            operation.template operator()<PixelFormat::xrgb8888>();
        }
        break;
    case PixelFormat::argb8888:
        if constexpr (render_config::kArgb8888Enabled)
        {
            operation.template operator()<PixelFormat::argb8888>();
        }
        break;
    case PixelFormat::r2Packed2x2:
        if constexpr (render_config::kR2Packed2x2Enabled)
        {
            operation.template operator()<PixelFormat::r2Packed2x2>();
        }
        break;
    case PixelFormat::rrrr2222:
        if constexpr (render_config::kRrrr2222Enabled)
        {
            operation.template operator()<PixelFormat::rrrr2222>();
        }
        break;
    case PixelFormat::ryry2222:
        if constexpr (render_config::kRyry2222Enabled)
        {
            operation.template operator()<PixelFormat::ryry2222>();
        }
        break;
    }
}

struct BitmapSample
{
    Color color{};
    bool covered{false};
};

template <BitmapFormat Format>
BitmapSample sampleBitmap(const Bitmap& bitmap,
                          std::size_t rowOffset,
                          std::size_t sourceColumn,
                          std::size_t rowWithinBlock) noexcept
{
    if constexpr (Format == BitmapFormat::rgb565)
    {
        return {PixelFormatTraits<PixelFormat::rgb565>::read(
                    bitmap.data,
                    rowOffset +
                        (sourceColumn * PixelFormatTraits<PixelFormat::rgb565>::kBytesPerPixel)),
                true};
    }
    else if constexpr (Format == BitmapFormat::rgb888)
    {
        return {PixelFormatTraits<PixelFormat::rgb888>::read(
                    bitmap.data,
                    rowOffset +
                        (sourceColumn * PixelFormatTraits<PixelFormat::rgb888>::kBytesPerPixel)),
                true};
    }
    else if constexpr (Format == BitmapFormat::xrgb8888)
    {
        return {PixelFormatTraits<PixelFormat::xrgb8888>::read(
                    bitmap.data,
                    rowOffset +
                        (sourceColumn * PixelFormatTraits<PixelFormat::xrgb8888>::kBytesPerPixel)),
                true};
    }
    else if constexpr (Format == BitmapFormat::argb8888)
    {
        return {PixelFormatTraits<PixelFormat::argb8888>::read(
                    bitmap.data,
                    rowOffset +
                        (sourceColumn * PixelFormatTraits<PixelFormat::argb8888>::kBytesPerPixel)),
                true};
    }
    else if constexpr (Format == BitmapFormat::r2Packed2x2)
    {
        return {PixelFormatTraits<PixelFormat::r2Packed2x2>::read(bitmap.data,
                                                                  rowOffset + sourceColumn / 2U,
                                                                  rowWithinBlock * 2U +
                                                                      sourceColumn % 2U),
                true};
    }
    else if constexpr (Format == BitmapFormat::indexed4)
    {
        const std::uint8_t packed =
            std::to_integer<std::uint8_t>(bitmap.data[rowOffset + (sourceColumn / 2U)]);
        const std::uint8_t paletteIndexByte = (sourceColumn % 2U) == 0U
                                                  ? detail::lowByte(std::uint32_t{packed} >> 4U)
                                                  : detail::lowByte(std::uint32_t{packed} & 0x0fU);
        const std::size_t paletteIndex{paletteIndexByte};
        if (paletteIndex >= bitmap.palette.size())
        {
            return {};
        }
        const Color color = bitmap.palette[paletteIndex];
        return {color, color.alpha != 0U};
    }
    else if constexpr (Format == BitmapFormat::alpha1)
    {
        const std::uint8_t bits =
            std::to_integer<std::uint8_t>(bitmap.data[rowOffset + (sourceColumn / 8U)]);
        const bool covered = (bits & (0x80U >> (sourceColumn % 8U))) != 0U;
        return {bitmap.tint, covered};
    }
    else
    {
        const std::uint32_t coverage =
            std::to_integer<std::uint8_t>(bitmap.data[rowOffset + sourceColumn]);
        Color color = bitmap.tint;
        color.alpha = detail::lowByte((color.alpha * coverage) / 255U);
        return {color, coverage != 0U};
    }
}

template <PixelFormat SurfaceFormat, BitmapFormat SourceFormat>
void drawBitmapPixels(Surface& surface,
                      Rect clip,
                      Viewport viewport,
                      Point origin,
                      const Bitmap& bitmap,
                      Rect visible,
                      std::size_t stride,
                      std::uint8_t scale) noexcept
{
    for (std::int32_t y = visible.y; y < visible.bottom(); ++y)
    {
        const std::int32_t sourceY = (y - origin.y) / scale;
        constexpr std::size_t kBlockHeight = SourceFormat == BitmapFormat::r2Packed2x2 ? 2U : 1U;
        const auto sourceRow = detail::nonnegativeSize(sourceY);
        const auto sourceRowOffset = (sourceRow / kBlockHeight) * stride;
        for (std::int32_t x = visible.x; x < visible.right(); ++x)
        {
            const std::int32_t sourceX = (x - origin.x) / scale;
            const BitmapSample sample = sampleBitmap<SourceFormat>(bitmap,
                                                                   sourceRowOffset,
                                                                   detail::nonnegativeSize(sourceX),
                                                                   sourceRow % kBlockHeight);
            if (sample.covered)
            {
                putPixel<SurfaceFormat>(surface, clip, viewport, x, y, sample.color);
            }
        }
    }
}

template <PixelFormat SurfaceFormat>
void dispatchBitmap(Surface& surface,
                    Rect clip,
                    Viewport viewport,
                    Point origin,
                    const Bitmap& bitmap,
                    Rect visible,
                    std::size_t stride,
                    std::uint8_t scale) noexcept
{
    switch (bitmap.format)
    {
    case BitmapFormat::rgb565:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::rgb565>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    case BitmapFormat::rgb888:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::rgb888>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    case BitmapFormat::xrgb8888:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::xrgb8888>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    case BitmapFormat::argb8888:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::argb8888>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    case BitmapFormat::indexed4:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::indexed4>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    case BitmapFormat::alpha1:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::alpha1>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    case BitmapFormat::alpha8:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::alpha8>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    case BitmapFormat::r2Packed2x2:
        drawBitmapPixels<SurfaceFormat, BitmapFormat::r2Packed2x2>(
            surface, clip, viewport, origin, bitmap, visible, stride, scale);
        break;
    }
}

struct DecodedCharacter
{
    char32_t codepoint{0};
    std::size_t beginOffset{0U};
    std::size_t endOffset{0U};
    bool valid{true};
};

DecodedCharacter decodeNext(std::string_view text, std::size_t& offset) noexcept
{
    DecodedCharacter result{};
    result.beginOffset = offset;
    result.endOffset = offset;
    if (offset >= text.size())
    {
        return result;
    }
    const auto first = detail::codeUnit(text[offset]);
    if (first == detail::codeUnit('&'))
    {
        const std::size_t entityStart = offset + 1U;
        const std::size_t semicolon = text.find(';', entityStart);
        if (semicolon != std::string_view::npos && semicolon - entityStart <= 10U)
        {
            const std::string_view entity = text.substr(entityStart, semicolon - entityStart);
            char32_t decoded = 0;
            if (entity == "amp")
            {
                decoded = U'&';
            }
            else if (entity == "lt")
            {
                decoded = U'<';
            }
            else if (entity == "gt")
            {
                decoded = U'>';
            }
            else if (entity == "quot")
            {
                decoded = U'"';
            }
            else if (entity == "apos")
            {
                decoded = U'\'';
            }
            else if (!entity.empty() && entity[0] == '#')
            {
                std::string_view digits = entity.substr(1U);
                std::uint8_t base = 10U;
                if (!digits.empty() && (digits.front() == 'x' || digits.front() == 'X'))
                {
                    base = 16U;
                    digits.remove_prefix(1U);
                }
                std::uint32_t value = 0U;
                const bool valid = detail::parseUnsigned(digits, value, base);
                if (valid && value <= 0x10ffffU && (value < 0xd800U || value > 0xdfffU))
                {
                    decoded = value;
                }
            }
            if (decoded != 0)
            {
                offset = semicolon + 1U;
                result.codepoint = decoded;
                result.endOffset = offset;
                return result;
            }
        }
        ++offset;
        result.codepoint = U'&';
        result.endOffset = offset;
        return result;
    }
    if (first < 0x80U)
    {
        ++offset;
        result.codepoint = first;
        result.endOffset = offset;
        return result;
    }
    std::uint8_t remaining = 0;
    char32_t value = 0;
    char32_t minimum = 0;
    if (first >= 0xc2U && first <= 0xdfU)
    {
        remaining = 1U;
        value = first & 0x1fU;
        minimum = 0x80U;
    }
    else if (first >= 0xe0U && first <= 0xefU)
    {
        remaining = 2U;
        value = first & 0x0fU;
        minimum = 0x800U;
    }
    else if (first >= 0xf0U && first <= 0xf4U)
    {
        remaining = 3U;
        value = first & 0x07U;
        minimum = 0x10000U;
    }
    else
    {
        ++offset;
        result.codepoint = U'\ufffd';
        result.endOffset = offset;
        result.valid = false;
        return result;
    }
    std::size_t cursor = offset + 1U;
    for (std::uint8_t index = 0U; index < remaining; ++index)
    {
        if (cursor >= text.size())
        {
            ++offset;
            result.codepoint = U'\ufffd';
            result.endOffset = offset;
            result.valid = false;
            return result;
        }
        const auto next = detail::codeUnit(text[cursor]);
        if ((next & 0xc0U) != 0x80U)
        {
            ++offset;
            result.codepoint = U'\ufffd';
            result.endOffset = offset;
            result.valid = false;
            return result;
        }
        value = (value << 6U) | (next & 0x3fU);
        ++cursor;
    }
    if (value < minimum || value > 0x10ffffU || (value >= 0xd800U && value <= 0xdfffU))
    {
        ++offset;
        result.codepoint = U'\ufffd';
        result.endOffset = offset;
        result.valid = false;
        return result;
    }
    offset = cursor;
    result.codepoint = value;
    result.endOffset = offset;
    return result;
}

std::int32_t scaledAdvance(const Font& font, char32_t codepoint, std::uint8_t scale) noexcept
{
    Glyph glyph{};
    if (!font.glyph(codepoint, glyph))
    {
        return 0;
    }
    const std::int32_t advance = glyph.advance;
    return detail::saturatingMultiply(advance, scale);
}

struct LineLayout
{
    std::size_t beginOffset{0U};
    std::size_t endOffset{0U};
    std::size_t nextOffset{0U};
    std::int32_t width{0};
    bool hardBreak{false};
    bool stoppedOnInvalidUtf8{false};
};

std::size_t skipAsciiWrapSpace(std::string_view text, std::size_t offset) noexcept
{
    while (offset < text.size() && (text[offset] == ' ' || text[offset] == '\t'))
    {
        ++offset;
    }
    return offset;
}

LineLayout layoutLine(std::string_view text,
                      std::size_t beginOffset,
                      const Font& font,
                      std::int32_t maximumWidth,
                      const TextLayoutOptions& options) noexcept
{
    LineLayout line{};
    line.beginOffset = beginOffset;
    line.endOffset = beginOffset;
    line.nextOffset = beginOffset;
    if (beginOffset >= text.size())
    {
        return line;
    }

    std::size_t offset = beginOffset;
    std::size_t lastBreakOffset = std::string_view::npos;
    std::size_t afterLastBreak = std::string_view::npos;
    std::int32_t widthBeforeBreak = 0;
    while (offset < text.size())
    {
        const std::size_t characterOffset = offset;
        const DecodedCharacter character = decodeNext(text, offset);
        if (!character.valid && options.invalidUtf8 == InvalidUtf8Policy::stop)
        {
            line.endOffset = characterOffset;
            line.nextOffset = text.size();
            line.stoppedOnInvalidUtf8 = true;
            return line;
        }
        if (character.codepoint == U'\r' || character.codepoint == U'\n')
        {
            line.endOffset = characterOffset;
            if (character.codepoint == U'\r' && offset < text.size() && text[offset] == '\n')
            {
                ++offset;
            }
            line.nextOffset = offset;
            line.hardBreak = true;
            return line;
        }

        const bool isWrapSpace = character.codepoint == U' ' || character.codepoint == U'\t';
        if (isWrapSpace)
        {
            lastBreakOffset = characterOffset;
            afterLastBreak = offset;
            widthBeforeBreak = line.width;
        }
        const std::int32_t advance = scaledAdvance(font, character.codepoint, options.scale);
        const std::int32_t nextWidth = detail::saturatingAdd(line.width, advance);
        const bool wraps = options.wrap != TextWrap::none && nextWidth > maximumWidth;
        if (wraps && characterOffset != beginOffset)
        {
            if (options.wrap == TextWrap::word && lastBreakOffset != std::string_view::npos &&
                lastBreakOffset != beginOffset)
            {
                line.endOffset = lastBreakOffset;
                line.nextOffset = skipAsciiWrapSpace(text, afterLastBreak);
                line.width = widthBeforeBreak;
                return line;
            }
            line.endOffset = characterOffset;
            line.nextOffset = characterOffset;
            return line;
        }

        line.width = nextWidth;
        line.endOffset = offset;
        line.nextOffset = offset;
        if (wraps)
        {
            return line;
        }
    }
    return line;
}

bool hasFollowingLine(const LineLayout& line, std::string_view text) noexcept
{
    return line.hardBreak || line.nextOffset < text.size();
}

bool validUtf8(std::string_view text) noexcept
{
    std::size_t offset = 0U;
    bool valid = true;
    while (offset < text.size())
    {
        valid = decodeNext(text, offset).valid && valid;
    }
    return valid;
}

struct EllipsisPlan
{
    std::size_t endOffset{0U};
    std::int32_t width{0};
    bool drawEllipsis{false};
};

EllipsisPlan makeEllipsisPlan(const LineLayout& line,
                              bool forceEllipsis,
                              std::string_view text,
                              const Font& font,
                              std::int32_t maximumWidth,
                              const TextLayoutOptions& options) noexcept
{
    if (!forceEllipsis && line.width <= maximumWidth)
    {
        return {line.endOffset, line.width, false};
    }
    const std::int32_t ellipsisWidth = scaledAdvance(font, U'\u2026', options.scale);
    if (ellipsisWidth <= 0 || ellipsisWidth > maximumWidth)
    {
        return {line.beginOffset, 0, false};
    }
    const std::int32_t availableWidth = maximumWidth - ellipsisWidth;
    std::size_t offset = line.beginOffset;
    std::size_t fittedEnd = line.beginOffset;
    std::int32_t fittedWidth = 0;
    while (offset < line.endOffset)
    {
        const DecodedCharacter character = decodeNext(text.substr(0U, line.endOffset), offset);
        if (!character.valid && options.invalidUtf8 == InvalidUtf8Policy::stop)
        {
            break;
        }
        const std::int32_t advance = scaledAdvance(font, character.codepoint, options.scale);
        const std::int32_t nextWidth = detail::saturatingAdd(fittedWidth, advance);
        if (nextWidth > availableWidth)
        {
            break;
        }
        fittedWidth = nextWidth;
        fittedEnd = offset;
    }
    return {fittedEnd, detail::saturatingAdd(fittedWidth, ellipsisWidth), true};
}

struct LayoutSummary
{
    TextMetrics metrics{};
    std::int32_t lineHeight{0};
    std::int32_t lineSpacing{0};
};

LayoutSummary calculateLayout(std::string_view text,
                              const Font& font,
                              Size bounds,
                              TextLayoutOptions options) noexcept
{
    LayoutSummary summary{};
    summary.metrics.validUtf8 = validUtf8(text);
    if (options.scale == 0U)
    {
        options.scale = 1U;
    }
    const std::int32_t fontLineHeight = font.lineHeight();
    summary.lineHeight = detail::saturatingMultiply(fontLineHeight, options.scale);
    summary.lineSpacing = detail::saturatingMultiply(options.lineSpacing, options.scale);
    if (bounds.width <= 0 || bounds.height <= 0 || summary.lineHeight <= 0)
    {
        summary.metrics.truncated = !text.empty();
        return summary;
    }

    std::size_t offset = 0U;
    bool moreLines = true;
    while (moreLines)
    {
        if (summary.metrics.lineCount == std::numeric_limits<std::uint16_t>::max())
        {
            summary.metrics.truncated = true;
            break;
        }
        if (options.maxLines != 0U && summary.metrics.lineCount >= options.maxLines)
        {
            summary.metrics.truncated = true;
            break;
        }
        const std::int32_t nextHeight =
            summary.metrics.lineCount == 0U
                ? summary.lineHeight
                : detail::saturatingAdd(
                      summary.metrics.height,
                      detail::saturatingAdd(summary.lineSpacing, summary.lineHeight));
        if (summary.metrics.lineCount != 0U && nextHeight > bounds.height)
        {
            summary.metrics.truncated = true;
            break;
        }

        const LineLayout line = layoutLine(text, offset, font, bounds.width, options);
        ++summary.metrics.lineCount;
        summary.metrics.height = nextHeight;
        summary.metrics.truncated = summary.metrics.truncated || line.width > bounds.width ||
                                    nextHeight > bounds.height || line.stoppedOnInvalidUtf8;
        moreLines = hasFollowingLine(line, text) && !line.stoppedOnInvalidUtf8;
        offset = line.nextOffset;
        if (moreLines && summary.metrics.height >= bounds.height)
        {
            summary.metrics.truncated = true;
            break;
        }
    }

    offset = 0U;
    for (std::uint16_t lineIndex = 0U; lineIndex < summary.metrics.lineCount; ++lineIndex)
    {
        const LineLayout line = layoutLine(text, offset, font, bounds.width, options);
        const bool finalVisibleLine = lineIndex + 1U == summary.metrics.lineCount;
        const bool forceEllipsis =
            options.overflow == TextOverflow::ellipsis &&
            (line.width > bounds.width || (finalVisibleLine && summary.metrics.truncated));
        const EllipsisPlan plan =
            makeEllipsisPlan(line, forceEllipsis, text, font, bounds.width, options);
        std::int32_t visibleWidth = line.width < bounds.width ? line.width : bounds.width;
        if (options.overflow == TextOverflow::ellipsis)
        {
            visibleWidth = plan.width;
        }
        summary.metrics.width =
            summary.metrics.width > visibleWidth ? summary.metrics.width : visibleWidth;
        offset = line.nextOffset;
    }
    return summary;
}

std::uint8_t glyphCoverage(const Glyph& glyph, std::uint8_t row, std::uint8_t column) noexcept
{
    const std::size_t stride = glyph.strideBytes == 0U
                                   ? detail::minimumGlyphStrideBytes(glyph.width, glyph.coverage)
                                   : glyph.strideBytes;
    const std::size_t rowIndex = row;
    const std::size_t rowOffset = rowIndex * stride;
    if (glyph.coverage == GlyphCoverage::rowMask)
    {
        const std::uint8_t bits = std::to_integer<std::uint8_t>(glyph.data[rowOffset]);
        return (bits & (1U << (glyph.width - 1U - column))) == 0U ? 0U : 255U;
    }
    if (glyph.coverage == GlyphCoverage::alpha1)
    {
        const std::size_t byteOffset = rowOffset + (column / 8U);
        const std::uint8_t bits = std::to_integer<std::uint8_t>(glyph.data[byteOffset]);
        return (bits & (0x80U >> (column % 8U))) == 0U ? 0U : 255U;
    }
    return std::to_integer<std::uint8_t>(glyph.data[rowOffset + column]);
}

template <PixelFormat Format>
void drawGlyph(Surface& surface,
               Rect clip,
               Viewport viewport,
               Point origin,
               const Glyph& glyph,
               Color color,
               std::uint8_t scale,
               bool syntheticBold) noexcept
{
    const std::int32_t glyphX =
        detail::saturatingAdd(origin.x, detail::saturatingMultiply(glyph.xOffset, scale));
    const std::int32_t glyphY =
        detail::saturatingAdd(origin.y, detail::saturatingMultiply(glyph.yOffset, scale));
    for (std::uint8_t row = 0U; row < glyph.height; ++row)
    {
        for (std::uint8_t column = 0U; column < glyph.width; ++column)
        {
            const std::uint8_t coverage = glyphCoverage(glyph, row, column);
            if (coverage == 0U)
            {
                continue;
            }
            Color pixelColor = color;
            pixelColor.alpha = detail::lowByte((color.alpha * coverage) / 255U);
            for (std::uint8_t scaleY = 0U; scaleY < scale; ++scaleY)
            {
                for (std::uint8_t scaleX = 0U; scaleX < scale; ++scaleX)
                {
                    const std::int32_t pixelX = detail::saturatingAdd(
                        glyphX,
                        detail::saturatingAdd(detail::saturatingMultiply(column, scale), scaleX));
                    const std::int32_t pixelY = detail::saturatingAdd(
                        glyphY,
                        detail::saturatingAdd(detail::saturatingMultiply(row, scale), scaleY));
                    putPixel<Format>(surface, clip, viewport, pixelX, pixelY, pixelColor);
                    if (syntheticBold)
                    {
                        putPixel<Format>(surface,
                                         clip,
                                         viewport,
                                         detail::saturatingAdd(pixelX, 1),
                                         pixelY,
                                         pixelColor);
                    }
                }
            }
        }
    }
}

} // namespace

Painter::Painter(Surface surface, DamageTracker& damage, Viewport viewport) noexcept
    : mSurface(surface), mDamage(damage),
      mViewport(viewport.valid() ? viewport : Viewport::rectangle(surface.bounds())),
      mClip(valid() ? intersect(surface.bounds(), mViewport.bounds) : Rect{})
{
}

bool Painter::valid() const noexcept
{
    return mSurface.valid() && supportsDestination(mSurface.format);
}

void Painter::setClip(Rect clip) noexcept
{
    mClip = valid() ? intersect(intersect(mSurface.bounds(), mViewport.bounds), clip) : Rect{};
}

void Painter::fillRect(Rect rect, Color color) noexcept
{
    rect = intersect(rect, mClip);
    if (rect.empty() || color.alpha == 0U)
    {
        return;
    }
    bool drewPixels = false;
    dispatchSurface(mSurface,
                    [&]<PixelFormat Format>() noexcept
                    {
                        using Traits = PixelFormatTraits<Format>;
                        for (std::int32_t y = rect.y; y < rect.bottom(); ++y)
                        {
                            std::int32_t left = rect.x;
                            std::int32_t right = rect.right();
                            while (left < right && !mViewport.contains({left, y}))
                            {
                                ++left;
                            }
                            while (right > left && !mViewport.contains({right - 1, y}))
                            {
                                --right;
                            }
                            if (left >= right)
                            {
                                continue;
                            }
                            drewPixels = true;
                            if constexpr (Traits::kBlockWidth == 1 && Traits::kBlockHeight == 1)
                            {
                                std::size_t offset = surfaceOffset<Format>(mSurface, left, y);
                                for (std::int32_t x = left; x < right; ++x)
                                {
                                    writePixelAtOffset<Format>(mSurface, offset, color);
                                    offset += Traits::kBytesPerBlock;
                                }
                            }
                            else
                            {
                                for (std::int32_t x = left; x < right; ++x)
                                {
                                    putPixel<Format>(mSurface, mClip, mViewport, x, y, color);
                                }
                            }
                        }
                    });
    if (drewPixels)
    {
        mDamage.add(rect);
    }
}

void Painter::fillCircle(Point center, std::int32_t radius, Color color) noexcept
{
    if (radius <= 0 || color.alpha == 0U)
    {
        return;
    }
    const std::int32_t diameter = detail::saturatingMultiply(radius, 2);
    const std::int32_t extent = detail::saturatingAdd(diameter, 1);
    const Rect bounds{detail::saturatingSubtract(center.x, radius),
                      detail::saturatingSubtract(center.y, radius),
                      extent,
                      extent};
    const Rect visible = intersect(bounds, mClip);
    if (visible.empty())
    {
        return;
    }
    const std::int64_t wideRadius = radius;
    const std::int64_t radiusSquared = wideRadius * wideRadius;
    dispatchSurface(mSurface,
                    [&]<PixelFormat Format>() noexcept
                    {
                        for (std::int32_t y = visible.y; y < visible.bottom(); ++y)
                        {
                            const std::int64_t wideY = y;
                            const std::int64_t dy = wideY - center.y;
                            for (std::int32_t x = visible.x; x < visible.right(); ++x)
                            {
                                const std::int64_t wideX = x;
                                const std::int64_t dx = wideX - center.x;
                                if (dx <= wideRadius && dx >= -wideRadius && dy <= wideRadius &&
                                    dy >= -wideRadius && dx * dx + dy * dy <= radiusSquared)
                                {
                                    putPixel<Format>(mSurface, mClip, mViewport, x, y, color);
                                }
                            }
                        }
                    });
    mDamage.add(visible);
}

void Painter::strokeRect(Rect rect, Color color, std::uint8_t width) noexcept
{
    if (width == 0U || rect.empty())
    {
        return;
    }
    const std::int32_t thickness = width;
    const std::int32_t verticalLength =
        detail::saturatingSubtract(rect.height, detail::saturatingMultiply(thickness, 2));
    const std::int32_t insetY = detail::saturatingAdd(rect.y, thickness);
    fillRect({rect.x, rect.y, rect.width, thickness}, color);
    fillRect({rect.x, detail::saturatingSubtract(rect.bottom(), thickness), rect.width, thickness},
             color);
    fillRect({rect.x, insetY, thickness, verticalLength}, color);
    fillRect(
        {detail::saturatingSubtract(rect.right(), thickness), insetY, thickness, verticalLength},
        color);
}

void Painter::drawLine(Point from, Point to, Color color) noexcept
{
    if (mClip.empty())
    {
        return;
    }
    const std::int64_t wideFromX = from.x;
    const std::int64_t wideToX = to.x;
    const std::int64_t dx = from.x < to.x ? wideToX - wideFromX : wideFromX - wideToX;
    const std::int32_t sx = from.x < to.x ? 1 : -1;
    const std::int64_t wideFromY = from.y;
    const std::int64_t wideToY = to.y;
    const std::int64_t dyAbsolute = from.y < to.y ? wideToY - wideFromY : wideFromY - wideToY;
    const std::int64_t dy = -dyAbsolute;
    const std::int32_t sy = from.y < to.y ? 1 : -1;
    const std::int64_t error = dx + dy;
    dispatchSurface(mSurface,
                    [&]<PixelFormat Format>() noexcept
                    {
                        std::int64_t lineError = error;
                        Point point = from;
                        for (;;)
                        {
                            putPixel<Format>(mSurface, mClip, mViewport, point.x, point.y, color);
                            if (point.x == to.x && point.y == to.y)
                            {
                                break;
                            }
                            const std::int64_t doubled = lineError * 2;
                            if (doubled >= dy)
                            {
                                lineError += dy;
                                point.x += sx;
                            }
                            if (doubled <= dx)
                            {
                                lineError += dx;
                                point.y += sy;
                            }
                        }
                    });
    const std::int32_t damageWidth = detail::saturatingAdd(
        detail::saturatingSubtract(from.x < to.x ? to.x : from.x, from.x < to.x ? from.x : to.x),
        1);
    const std::int32_t damageHeight = detail::saturatingAdd(
        detail::saturatingSubtract(from.y < to.y ? to.y : from.y, from.y < to.y ? from.y : to.y),
        1);
    mDamage.add(intersect(
        {from.x < to.x ? from.x : to.x, from.y < to.y ? from.y : to.y, damageWidth, damageHeight},
        mClip));
}

void Painter::drawBitmap(Point origin, const Bitmap& bitmap, std::uint8_t scale) noexcept
{
    if (!bitmap.valid() || scale == 0U)
    {
        return;
    }
    const Rect destination{origin.x,
                           origin.y,
                           detail::saturatingMultiply(bitmap.width, scale),
                           detail::saturatingMultiply(bitmap.height, scale)};
    const Rect visible = intersect(destination, mClip);
    if (visible.empty())
    {
        return;
    }
    const std::size_t defaultStride = bitmap.minimumStrideBytes();
    const std::size_t stride = bitmap.strideBytes == 0U ? defaultStride : bitmap.strideBytes;

    dispatchSurface(mSurface,
                    [&]<PixelFormat Format>() noexcept
                    {
                        dispatchBitmap<Format>(
                            mSurface, mClip, mViewport, origin, bitmap, visible, stride, scale);
                    });
    mDamage.add(visible);
}

TextMetrics
Painter::measureText(std::string_view text, const Font& font, std::uint8_t scale) noexcept
{
    TextLayoutOptions options{};
    options.scale = scale;
    return measureTextLayout(
        text,
        font,
        {std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::max()},
        options);
}

TextMetrics Painter::measureTextLayout(std::string_view text,
                                       const Font& font,
                                       Size bounds,
                                       TextLayoutOptions options) noexcept
{
    return calculateLayout(text, font, bounds, options).metrics;
}

void Painter::drawText(Rect rect,
                       std::string_view text,
                       const Font& font,
                       Color color,
                       TextAlign align,
                       std::uint8_t scale) noexcept
{
    TextLayoutOptions options{};
    options.align = align;
    options.scale = scale;
    drawTextLayout(rect, text, font, color, options);
}

void Painter::drawTextLayout(Rect rect,
                             std::string_view text,
                             const Font& font,
                             Color color,
                             TextLayoutOptions options) noexcept
{
    const Rect visible = intersect(rect, mClip);
    if (visible.empty() || text.empty() || color.alpha == 0U)
    {
        return;
    }
    if (options.scale == 0U)
    {
        options.scale = 1U;
    }
    const LayoutSummary summary = calculateLayout(text, font, {rect.width, rect.height}, options);
    const TextMetrics metrics = summary.metrics;
    if (metrics.lineCount == 0U)
    {
        return;
    }
    std::int32_t y = rect.y;
    if (options.align.vertical == VerticalAlign::center)
    {
        y = detail::saturatingAdd(y, detail::saturatingSubtract(rect.height, metrics.height) / 2);
    }
    else if (options.align.vertical == VerticalAlign::bottom)
    {
        y = detail::saturatingAdd(y, detail::saturatingSubtract(rect.height, metrics.height));
    }
    dispatchSurface(
        mSurface,
        [&]<PixelFormat Format>() noexcept
        {
            std::size_t lineOffset = 0U;
            for (std::uint16_t lineIndex = 0U; lineIndex < metrics.lineCount; ++lineIndex)
            {
                const LineLayout line = layoutLine(text, lineOffset, font, rect.width, options);
                const bool finalVisibleLine = lineIndex + 1U == metrics.lineCount;
                const bool forceEllipsis =
                    options.overflow == TextOverflow::ellipsis &&
                    (line.width > rect.width || (finalVisibleLine && metrics.truncated));
                const EllipsisPlan plan =
                    options.overflow == TextOverflow::ellipsis
                        ? makeEllipsisPlan(line, forceEllipsis, text, font, rect.width, options)
                        : EllipsisPlan{line.endOffset,
                                       line.width < rect.width ? line.width : rect.width,
                                       false};
                std::int32_t lineX = rect.x;
                if (options.align.horizontal == HorizontalAlign::center)
                {
                    lineX = detail::saturatingAdd(
                        lineX, detail::saturatingSubtract(rect.width, plan.width) / 2);
                }
                else if (options.align.horizontal == HorizontalAlign::right)
                {
                    lineX = detail::saturatingAdd(
                        lineX, detail::saturatingSubtract(rect.width, plan.width));
                }

                std::int32_t penX = lineX;
                std::size_t characterOffset = line.beginOffset;
                while (characterOffset < plan.endOffset)
                {
                    const DecodedCharacter character =
                        decodeNext(text.substr(0U, plan.endOffset), characterOffset);
                    if (!character.valid && options.invalidUtf8 == InvalidUtf8Policy::stop)
                    {
                        break;
                    }
                    Glyph glyph{};
                    if (font.glyph(character.codepoint, glyph))
                    {
                        drawGlyph<Format>(mSurface,
                                          visible,
                                          mViewport,
                                          {penX, y},
                                          glyph,
                                          color,
                                          options.scale,
                                          font.syntheticBold());
                        penX = detail::saturatingAdd(
                            penX, detail::saturatingMultiply(glyph.advance, options.scale));
                    }
                }
                if (plan.drawEllipsis)
                {
                    Glyph glyph{};
                    if (font.glyph(U'\u2026', glyph))
                    {
                        drawGlyph<Format>(mSurface,
                                          visible,
                                          mViewport,
                                          {penX, y},
                                          glyph,
                                          color,
                                          options.scale,
                                          font.syntheticBold());
                    }
                }
                if (options.underline && plan.width > 0)
                {
                    const std::int32_t underlineY = detail::saturatingAdd(
                        y, detail::saturatingMultiply(font.underlinePosition(), options.scale));
                    const std::int32_t thickness =
                        detail::saturatingMultiply(font.underlineThickness(), options.scale);
                    for (std::int32_t underlineRow = 0; underlineRow < thickness; ++underlineRow)
                    {
                        for (std::int32_t column = 0; column < plan.width; ++column)
                        {
                            putPixel<Format>(mSurface,
                                             visible,
                                             mViewport,
                                             detail::saturatingAdd(lineX, column),
                                             detail::saturatingAdd(underlineY, underlineRow),
                                             color);
                        }
                    }
                }
                lineOffset = line.nextOffset;
                y = detail::saturatingAdd(
                    y, detail::saturatingAdd(summary.lineHeight, summary.lineSpacing));
            }
        });
    mDamage.add(visible);
}

} // namespace lithe
