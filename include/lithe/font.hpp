// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/detail/constants.hpp"

#include <cstddef>
#include <cstdint>
#include <span>
#include <string_view>

namespace lithe
{

enum class GlyphCoverage : std::uint8_t
{
    rowMask,
    alpha1,
    alpha8
};

namespace detail
{

constexpr std::size_t minimumGlyphStrideBytes(std::uint8_t width, GlyphCoverage coverage) noexcept
{
    const std::size_t pixelWidth = width;
    if (coverage == GlyphCoverage::rowMask)
    {
        return 1U;
    }
    if (coverage == GlyphCoverage::alpha1)
    {
        return (pixelWidth + detail::kAlpha1RoundingBits) / detail::kBitsPerByte;
    }
    return pixelWidth;
}

} // namespace detail

struct Glyph
{
    // Borrowed coverage remains valid only as long as the supplying Font and its ROM tables.
    // alpha1 is MSB-first within each row; rowMask uses the compact 5x7 low-bit convention.
    // A zero strideBytes selects minimumGlyphStrideBytes(width, coverage).
    std::span<const std::byte> data;
    std::uint8_t width{0};
    std::uint8_t height{0};
    std::uint8_t strideBytes{0};
    std::uint8_t advance{0};
    std::int8_t xOffset{0};
    std::int8_t yOffset{0};
    GlyphCoverage coverage{GlyphCoverage::rowMask};

    constexpr bool valid() const noexcept
    {
        if (advance == 0U)
        {
            return false;
        }
        if (width == 0U || height == 0U)
        {
            return data.empty();
        }
        const std::size_t pixelWidth = width;
        if (coverage == GlyphCoverage::rowMask && pixelWidth > detail::kBitsPerByte)
        {
            return false;
        }
        const std::size_t minimumStride = detail::minimumGlyphStrideBytes(width, coverage);
        const std::size_t actualStride = strideBytes == 0U ? minimumStride : strideBytes;
        const std::size_t pixelHeight = height;
        return actualStride >= minimumStride && data.size() >= actualStride * pixelHeight;
    }
};

struct GeneratedGlyphRecord
{
    // A zero strideBytes selects the same coverage-dependent minimum as Glyph.
    char32_t codepoint{0};
    std::uint32_t dataOffset{0U};
    std::uint8_t width{0U};
    std::uint8_t height{0U};
    std::uint8_t strideBytes{0U};
    std::uint8_t advance{0U};
    std::int8_t xOffset{0};
    std::int8_t yOffset{0};
};

inline constexpr std::size_t kGeneratedGlyphRecordSizeBytes{16U};
static_assert(sizeof(GeneratedGlyphRecord) == kGeneratedGlyphRecordSizeBytes);

struct GeneratedFontManifest
{
    std::string_view family;
    std::string_view style;
    std::string_view repertoire;
    std::string_view sourceSha256;
    std::uint32_t dataChecksum{0U};
    std::uint32_t coverageBytes{0U};
    std::uint32_t recordBytes{0U};
    std::uint32_t estimatedRomBytes{0U};
    std::uint16_t glyphCount{0U};
    std::uint8_t pixelSize{0U};
    GlyphCoverage coverage{GlyphCoverage::alpha1};
};

struct GeneratedFontData
{
    // Glyph records are sorted by code point and borrow the immutable coverage storage.
    std::span<const GeneratedGlyphRecord> glyphs;
    std::span<const std::byte> coverage;
    GeneratedFontManifest manifest;
    char32_t fallbackCodepoint{U'?'};
    std::uint8_t lineHeight{0U};
    std::uint8_t baseline{0U};
    std::uint8_t underlinePosition{0U};
    std::uint8_t underlineThickness{1U};
};

class Font
{
public:
    Font() = default;
    virtual ~Font() = default;

    Font(const Font&) = delete;
    Font& operator=(const Font&) = delete;
    Font(Font&&) = delete;
    Font& operator=(Font&&) = delete;

    virtual bool glyph(char32_t codepoint, Glyph& result) const noexcept = 0;
    virtual std::uint8_t lineHeight() const noexcept = 0;
    virtual std::uint8_t baseline() const noexcept
    {
        const std::uint8_t height = lineHeight();
        std::uint8_t baselineValue = height;
        if (baselineValue != 0U)
        {
            --baselineValue;
        }
        return baselineValue;
    }
    virtual std::uint8_t underlinePosition() const noexcept
    {
        return baseline();
    }
    virtual std::uint8_t underlineThickness() const noexcept
    {
        return 1U;
    }
    virtual bool syntheticBold() const noexcept
    {
        return false;
    }
};

class GeneratedFont final : public Font
{
public:
    explicit constexpr GeneratedFont(const GeneratedFontData& data,
                                     bool syntheticBold = false) noexcept
        : mData(data), mSyntheticBold(syntheticBold)
    {
    }

    bool glyph(char32_t codepoint, Glyph& result) const noexcept override;
    std::uint8_t lineHeight() const noexcept override;
    std::uint8_t baseline() const noexcept override;
    std::uint8_t underlinePosition() const noexcept override;
    std::uint8_t underlineThickness() const noexcept override;
    bool syntheticBold() const noexcept override;

    const GeneratedFontManifest& manifest() const noexcept
    {
        return mData.manifest;
    }

private:
    const GeneratedFontData& mData;
    bool mSyntheticBold{false};
};

// Optional bundled resources: link LitheView::CompactFont for font5x7/Bold, or
// LitheView::GeneratedFonts for generatedSans/Bold. Core contains no font payload.
// An application may instead supply its own Font to the layout and controls.
const Font& font5x7() noexcept;
const Font& font5x7Bold() noexcept;
const GeneratedFont& generatedSans() noexcept;
const GeneratedFont& generatedSansBold() noexcept;

enum class BuiltInFontBackend : std::uint8_t
{
    compact5x7,
    generatedSans
};

template <BuiltInFontBackend Backend> const Font& builtInFont() noexcept
{
    if constexpr (Backend == BuiltInFontBackend::compact5x7)
    {
        return font5x7();
    }
    else
    {
        return generatedSans();
    }
}

template <BuiltInFontBackend Backend> const Font& builtInBoldFont() noexcept
{
    if constexpr (Backend == BuiltInFontBackend::compact5x7)
    {
        return font5x7Bold();
    }
    else
    {
        return generatedSansBold();
    }
}

} // namespace lithe
