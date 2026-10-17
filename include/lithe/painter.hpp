// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/display.hpp"
#include "lithe/font.hpp"

#include <cstddef>
#include <cstdint>
#include <span>
#include <string_view>

namespace lithe
{

enum class BitmapFormat : std::uint8_t
{
    rgb565,
    rgb888,
    xrgb8888,
    argb8888,
    indexed4,
    alpha1,
    alpha8,
    r2Packed2x2
};

struct Bitmap
{
    // Multibyte pixels are stored least-significant byte first and may be unaligned.
    std::span<const std::byte> data;
    // Packed R2 uses logical sample dimensions, both even, and strideBytes
    // advances a 2-pixel-high storage row. It decodes opaque red samples and
    // ignores tint, just as the RGB bitmap formats do.
    std::int32_t width{0};
    std::int32_t height{0};
    std::size_t strideBytes{0U};
    BitmapFormat format{BitmapFormat::rgb565};
    Color tint{Color::rgb(detail::kRgbColorMask)};
    // Indexed formats borrow their immutable palette for the Bitmap lifetime.
    // Index zero is not special to the format; transparency is expressed by
    // the alpha value of the selected Color.
    // Explicitly initialize the trailing aggregate member so partial Bitmap initializers remain
    // warning-free under -Wmissing-field-initializers.
    // NOLINTNEXTLINE(readability-redundant-member-init)
    std::span<const Color> palette{};

    constexpr std::size_t minimumStrideBytes() const noexcept
    {
        std::size_t minimumStride = 0U;
        switch (format)
        {
        case BitmapFormat::rgb565:
            return detail::checkedMultiply(
                       detail::nonnegativeSize(width), sizeof(Pixel), minimumStride)
                       ? minimumStride
                       : 0U;
        case BitmapFormat::rgb888:
            return detail::checkedMultiply(
                       detail::nonnegativeSize(width), sizeof(Rgb888Pixel), minimumStride)
                       ? minimumStride
                       : 0U;
        case BitmapFormat::xrgb8888:
        case BitmapFormat::argb8888:
            return detail::checkedMultiply(
                       detail::nonnegativeSize(width), sizeof(std::uint32_t), minimumStride)
                       ? minimumStride
                       : 0U;
        case BitmapFormat::indexed4:
            return (detail::nonnegativeSize(width) + 1U) / detail::kIndexed4PixelsPerByte;
        case BitmapFormat::alpha1:
            return (detail::nonnegativeSize(width) + detail::kAlpha1RoundingBits) /
                   detail::kBitsPerByte;
        case BitmapFormat::alpha8:
            return detail::nonnegativeSize(width);
        case BitmapFormat::r2Packed2x2:
            return width % 2 == 0 ? detail::nonnegativeSize(width) / 2U : 0U;
        }
        return 0U;
    }

    constexpr bool valid() const noexcept
    {
        if (width <= 0 || height <= 0 || (format == BitmapFormat::r2Packed2x2 && height % 2 != 0))
        {
            return false;
        }
        const std::size_t minimumStride = minimumStrideBytes();
        const std::size_t actualStride = strideBytes == 0U ? minimumStride : strideBytes;
        const std::int32_t rows = format == BitmapFormat::r2Packed2x2 ? height / 2 : height;
        const std::size_t precedingRows = detail::nonnegativeSize(rows - 1);
        const bool paletteValid =
            format != BitmapFormat::indexed4 ||
            (!palette.empty() && palette.size() <= detail::kMaximumIndexed4PaletteEntries);
        return paletteValid && minimumStride != 0U && actualStride >= minimumStride &&
               data.size() >= minimumStride &&
               precedingRows <= (data.size() - minimumStride) / actualStride;
    }
};

struct TextMetrics
{
    std::int32_t width{0};
    std::int32_t height{0};
    std::uint16_t lineCount{0U};
    bool truncated{false};
    bool validUtf8{true};
};

enum class TextWrap : std::uint8_t
{
    none,
    word,
    character
};

enum class TextOverflow : std::uint8_t
{
    clip,
    ellipsis
};

enum class InvalidUtf8Policy : std::uint8_t
{
    replacementCharacter,
    stop
};

struct TextLayoutOptions
{
    TextAlign align{};
    TextWrap wrap{TextWrap::none};
    TextOverflow overflow{TextOverflow::clip};
    InvalidUtf8Policy invalidUtf8{InvalidUtf8Policy::replacementCharacter};
    std::uint16_t maxLines{0U};
    std::uint8_t scale{1U};
    std::uint8_t lineSpacing{0U};
    bool underline{false};
};

class Painter
{
public:
    Painter(Surface surface, DamageTracker& damage, Viewport viewport = {}) noexcept;

    // Storage validity and this build's destination writer are independent of
    // the current clip. Invalid painters remain nondrawable after setClip().
    [[nodiscard]] bool valid() const noexcept;
    Surface surface() const noexcept
    {
        return mSurface;
    }
    Rect clip() const noexcept
    {
        return mClip;
    }
    Viewport viewport() const noexcept
    {
        return mViewport;
    }
    void setClip(Rect clip) noexcept;

    void fillRect(Rect rect, Color color) noexcept;
    void fillCircle(Point center, std::int32_t radius, Color color) noexcept;
    void strokeRect(Rect rect, Color color, std::uint8_t width = 1U) noexcept;
    void drawLine(Point from, Point to, Color color) noexcept;
    // Integer nearest-neighbor scaling expands each source pixel into a
    // scale-by-scale destination block without allocating a resized bitmap.
    // A zero scale is invalid and draws nothing.
    void drawBitmap(Point origin, const Bitmap& bitmap, std::uint8_t scale = 1U) noexcept;
    void drawText(Rect rect,
                  std::string_view text,
                  const Font& font,
                  Color color,
                  TextAlign align = {},
                  std::uint8_t scale = 1U) noexcept;
    // Layout borrows text and font storage for the call, performs no allocation, clips to rect,
    // and reports invalid UTF-8/truncation through the matching measureTextLayout() result.
    void drawTextLayout(Rect rect,
                        std::string_view text,
                        const Font& font,
                        Color color,
                        TextLayoutOptions options = {}) noexcept;
    static TextMetrics
    measureText(std::string_view text, const Font& font, std::uint8_t scale = 1U) noexcept;
    static TextMetrics measureTextLayout(std::string_view text,
                                         const Font& font,
                                         Size bounds,
                                         TextLayoutOptions options = {}) noexcept;

private:
    Surface mSurface;
    DamageTracker& mDamage;
    Viewport mViewport{};
    Rect mClip{};
};

} // namespace lithe
