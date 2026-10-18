// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <lithe/segment_frame.hpp>
#include <optional>
#include <span>

namespace lithe::designer::segments
{
inline constexpr std::uint32_t kMaximumDimension{1024};
inline constexpr std::uint32_t kMaximumSegments{4096};
inline constexpr std::uint32_t kMaximumFields{128};
inline constexpr std::uint32_t kMaximumCells{64};
inline constexpr std::uint32_t kMaximumNumberCells{9};
inline constexpr std::uint32_t kMaximumStrokes{32};
inline constexpr std::uint32_t kMaximumPoints{32};
inline constexpr std::uint32_t kMinimumPoints{3};
inline constexpr std::uint32_t kFirstCharacter{32};
inline constexpr std::uint32_t kLastCharacter{126};
inline constexpr std::uint32_t kGlyphCount{kLastCharacter - kFirstCharacter + 1};
inline constexpr std::uint32_t kMaximumGroupValue{1000000};
inline constexpr std::uint64_t kMaximumPolygonWork{64000000};
inline constexpr std::uint32_t kDecimalRadix{10};
inline constexpr std::uint32_t kDigitZero{48};
inline constexpr std::size_t kPixelBytes{4};
inline constexpr std::size_t kAlphaChannel{3};
inline constexpr std::size_t kBitsPerByte{8};
inline constexpr std::byte kBackground{230};
inline constexpr std::byte kOff{210};
inline constexpr std::byte kOn{25};
inline constexpr std::byte kOpaque{255};
// Values are protocol words, not C++ enum representations.
inline constexpr std::uint32_t kIcon{0};
inline constexpr std::uint32_t kGroup{1};
inline constexpr std::uint32_t kText{2};
inline constexpr std::uint32_t kNumber{3};
using Frame = SegmentFrame<kMaximumSegments>;

// Host-only expansion. Target presentation storage remains one bit per electrode.
struct Image
{
    std::uint32_t width{};
    std::uint32_t height{};
    std::span<const std::byte> bits;
    std::span<const std::byte> rgba;
};
class Reader
{
public:
    explicit Reader(std::span<const std::byte> bytes) noexcept : mBytes{bytes}
    {
    }
    std::uint32_t word() noexcept
    {
        if (mBytes.size() < sizeof(std::uint32_t))
        {
            mValid = false;
            return 0;
        }
        std::uint32_t value{};
        for (std::size_t index = 0; index < sizeof(value); ++index)
        {
            value |= std::to_integer<std::uint32_t>(mBytes[index]) << (index * kBitsPerByte);
        }
        mBytes = mBytes.subspan(sizeof(value));
        return value;
    }
    [[nodiscard]] bool valid() const noexcept
    {
        return mValid;
    }
    [[nodiscard]] bool done() const noexcept
    {
        return mValid && mBytes.empty();
    }

private:
    std::span<const std::byte> mBytes;
    bool mValid{true};
};
struct Extent
{
    std::uint32_t width{};
    std::uint32_t height{};
    std::uint32_t segments{};
    std::uint32_t fields{};
    std::uint32_t shapes{};
    [[nodiscard]] bool valid() const noexcept
    {
        return width > 0 && height > 0 && width <= kMaximumDimension &&
               height <= kMaximumDimension && segments > 0 && segments <= kMaximumSegments &&
               fields <= kMaximumFields && shapes <= segments;
    }
};
struct Field
{
    std::uint32_t kind{};
    std::uint32_t mappingCount{};
    std::uint32_t strokes{};
    std::uint32_t maximum{};
    std::uint32_t glyphCount{};
    std::uint32_t value{};
    std::uint32_t textCount{};
    std::array<std::uint32_t, kMaximumSegments> mapping{};
    std::array<std::uint32_t, kGlyphCount> glyphs{};
    std::array<bool, kGlyphCount> known{};
    std::array<std::uint32_t, kMaximumCells> text{};

    bool readMappings(Reader& reader, std::uint32_t count, Frame& writers) noexcept
    {
        for (std::uint32_t index = 0; index < mappingCount; ++index)
        {
            mapping[index] = reader.word();
            if (mapping[index] >= count || writers.test(mapping[index]) ||
                !writers.set(mapping[index]))
            {
                return false;
            }
        }
        return reader.valid();
    }
    bool readGlyphs(Reader& reader) noexcept
    {
        for (std::uint32_t index = 0; index < glyphCount; ++index)
        {
            const auto character = reader.word();
            const auto mask = reader.word();
            if (character < kFirstCharacter || character > kLastCharacter ||
                known[character - kFirstCharacter] || strokes == 0 || strokes > kMaximumStrokes ||
                (strokes < kMaximumStrokes && (mask >> strokes) != 0))
            {
                return false;
            }
            known[character - kFirstCharacter] = true;
            glyphs[character - kFirstCharacter] = mask;
        }
        return reader.valid();
    }
    bool read(Reader& reader, std::uint32_t count, Frame& writers) noexcept
    {
        kind = reader.word();
        mappingCount = reader.word();
        strokes = reader.word();
        maximum = reader.word();
        glyphCount = reader.word();
        value = reader.word();
        textCount = reader.word();
        if (kind > kNumber || mappingCount == 0 || mappingCount > kMaximumSegments ||
            glyphCount > kGlyphCount || textCount > kMaximumCells ||
            !readMappings(reader, count, writers) || !readGlyphs(reader))
        {
            return false;
        }
        for (std::uint32_t index = 0; index < textCount; ++index)
        {
            text[index] = reader.word();
        }
        return reader.valid();
    }
    [[nodiscard]] bool
    writeGlyph(Frame& frame, std::uint32_t cell, std::uint32_t character) const noexcept
    {
        if (character < kFirstCharacter || character > kLastCharacter ||
            !known[character - kFirstCharacter])
        {
            return false;
        }
        for (std::uint32_t stroke = 0; stroke < strokes; ++stroke)
        {
            if (!frame.set(mapping[(cell * strokes) + stroke],
                           (glyphs[character - kFirstCharacter] & (std::uint32_t{1} << stroke)) !=
                               0))
            {
                return false;
            }
        }
        return true;
    }
    [[nodiscard]] bool encodeGroup(Frame& frame) const noexcept
    {
        if (glyphCount != 0 || textCount != 0 || strokes != 0 || maximum == 0 ||
            maximum > kMaximumGroupValue || value > maximum || (kind == kIcon && maximum != 1))
        {
            return false;
        }
        const auto lit = (std::uint64_t{value} * mappingCount + maximum / 2U) / maximum;
        for (std::uint32_t index = 0; index < mappingCount; ++index)
        {
            if (!frame.set(mapping[index], index < lit))
            {
                return false;
            }
        }
        return true;
    }
    [[nodiscard]] bool encodeNumber(Frame& frame, std::uint32_t cells) const noexcept
    {
        std::uint32_t limit{1};
        for (std::uint32_t index = 0; index < cells; ++index)
        {
            limit *= kDecimalRadix;
        }
        if (value >= limit)
        {
            return false;
        }
        auto rest = value;
        for (auto remaining = cells; remaining > 0; --remaining)
        {
            if (!writeGlyph(frame, remaining - 1U, kDigitZero + (rest % kDecimalRadix)))
            {
                return false;
            }
            rest /= kDecimalRadix;
            if (rest == 0)
            {
                break;
            }
        }
        return true;
    }
    [[nodiscard]] bool encode(Frame& frame) const noexcept
    {
        if (kind <= kGroup)
        {
            return encodeGroup(frame);
        }
        if (strokes == 0 || strokes > kMaximumStrokes || mappingCount % strokes != 0)
        {
            return false;
        }
        const auto cells = mappingCount / strokes;
        if (cells > kMaximumCells || textCount > cells ||
            (kind == kNumber && (cells > kMaximumNumberCells || textCount != 0)))
        {
            return false;
        }
        if (kind == kNumber)
        {
            return encodeNumber(frame, cells);
        }
        for (std::uint32_t index = 0; index < textCount; ++index)
        {
            if (!writeGlyph(frame, index, text[index]))
            {
                return false;
            }
        }
        return true;
    }
};
struct Polygon
{
    std::uint32_t id{};
    std::uint32_t points{};
    std::array<std::int64_t, kMaximumPoints> xs{};
    std::array<std::int64_t, kMaximumPoints> ys{};
    std::uint32_t minX{};
    std::uint32_t minY{};
    std::uint32_t maxX{};
    std::uint32_t maxY{};
    bool read(Reader& reader, const Extent& extent, Frame& seen, std::uint64_t& work) noexcept
    {
        id = reader.word();
        points = reader.word();
        if (id >= extent.segments || seen.test(id) || !seen.set(id) || points < kMinimumPoints ||
            points > kMaximumPoints)
        {
            return false;
        }
        minX = extent.width;
        minY = extent.height;
        for (std::uint32_t index = 0; index < points; ++index)
        {
            const auto x = reader.word();
            const auto y = reader.word();
            if (x > extent.width || y > extent.height)
            {
                return false;
            }
            // Doubled coordinates allow exact integer pixel-center tests.
            xs[index] = std::int64_t{x} * 2;
            ys[index] = std::int64_t{y} * 2;
            minX = std::min(minX, x);
            maxX = std::max(maxX, x);
            minY = std::min(minY, y);
            maxY = std::max(maxY, y);
        }
        work += std::uint64_t{maxX - minX} * (maxY - minY) * points;
        return reader.valid() && work <= kMaximumPolygonWork;
    }
    [[nodiscard]] bool contains(std::uint32_t x, std::uint32_t y) const noexcept
    {
        const std::int64_t px = (std::int64_t{x} * 2) + 1;
        const std::int64_t py = (std::int64_t{y} * 2) + 1;
        bool inside{false};
        auto previous = points - 1U;
        for (std::uint32_t index = 0; index < points; ++index)
        {
            if ((ys[index] > py) != (ys[previous] > py))
            {
                const auto left = (px - xs[index]) * (ys[previous] - ys[index]);
                const auto right = (xs[previous] - xs[index]) * (py - ys[index]);
                if (ys[previous] > ys[index] ? left < right : left > right)
                {
                    inside = !inside;
                }
            }
            previous = index;
        }
        return inside;
    }
    void paint(std::span<std::byte> pixels, std::uint32_t width, std::byte color) const noexcept
    {
        for (auto y = minY; y < maxY; ++y)
        {
            for (auto x = minX; x < maxX; ++x)
            {
                if (contains(x, y))
                {
                    const auto offset = (std::size_t{y} * width + x) * kPixelBytes;
                    pixels[offset] = color;
                    pixels[offset + 1] = color;
                    pixels[offset + 2] = color;
                }
            }
        }
    }
};
// The returned spans borrow this renderer until its next request. The worker
// serializes requests; no storage or synchronization is imposed on the target API.
class Renderer
{
public:
    std::optional<Image> render(std::span<const std::byte> payload) noexcept
    {
        Reader reader{payload};
        const Extent extent{
            reader.word(), reader.word(), reader.word(), reader.word(), reader.word()};
        if (!extent.valid())
        {
            return {};
        }
        mFrame = {};
        Frame writers{};
        for (std::uint32_t index = 0; index < extent.fields; ++index)
        {
            Field field{};
            if (!field.read(reader, extent.segments, writers) || !field.encode(mFrame))
            {
                return {};
            }
        }
        const auto pixelBytes = std::size_t{extent.width} * extent.height * kPixelBytes;
        auto pixels = std::span{mPixels}.first(pixelBytes);
        std::fill(pixels.begin(), pixels.end(), kBackground);
        for (std::size_t index = kAlphaChannel; index < pixelBytes; index += kPixelBytes)
        {
            pixels[index] = kOpaque;
        }
        Frame seen{};
        std::uint64_t work{};
        for (std::uint32_t index = 0; index < extent.shapes; ++index)
        {
            Polygon polygon{};
            if (!polygon.read(reader, extent, seen, work))
            {
                return {};
            }
            polygon.paint(pixels, extent.width, mFrame.test(polygon.id) ? kOn : kOff);
        }
        for (std::uint32_t id = 0; id < extent.segments; ++id)
        {
            if (writers.test(id) && !seen.test(id))
            {
                return {};
            }
        }
        if (!reader.done())
        {
            return {};
        }
        return Image{extent.width,
                     extent.height,
                     mFrame.bytes().first((extent.segments + kBitsPerByte - 1) / kBitsPerByte),
                     pixels};
    }

private:
    std::array<std::byte, std::size_t{kMaximumDimension} * kMaximumDimension * kPixelBytes>
        mPixels{};
    Frame mFrame{};
};
} // namespace lithe::designer::segments
