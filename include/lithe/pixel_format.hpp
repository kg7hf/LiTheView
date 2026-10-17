// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/types.hpp"

#include <cstddef>
#include <cstdint>
#include <span>

namespace lithe
{

// Multibyte formats use little-endian byte order. RGB888 is stored R, G, B;
// XRGB8888 and ARGB8888 are stored B, G, R, X/A so their little-endian words
// are 0xXXRRGGBB and 0xAARRGGBB respectively.
enum class PixelFormat : std::uint8_t
{
    rgb565,
    rgb888,
    xrgb8888,
    argb8888,
    r2Packed2x2,
    rrrr2222,
    ryry2222
};

struct Rgb888Pixel
{
    std::uint8_t red{0U};
    std::uint8_t green{0U};
    std::uint8_t blue{0U};
};

struct Xrgb8888Pixel
{
    std::uint8_t blue{0U};
    std::uint8_t green{0U};
    std::uint8_t red{0U};
    std::uint8_t unused{detail::kMaximumChannelValue};
};

struct Argb8888Pixel
{
    std::uint8_t blue{0U};
    std::uint8_t green{0U};
    std::uint8_t red{0U};
    std::uint8_t alpha{0U};
};

static_assert(sizeof(Rgb888Pixel) == 3U);
static_assert(sizeof(Xrgb8888Pixel) == 4U);
static_assert(sizeof(Argb8888Pixel) == 4U);

template <PixelFormat Format> struct PixelFormatTraits;

template <> struct PixelFormatTraits<PixelFormat::rgb565>
{
    using Storage = Rgb565Pixel;
    static constexpr std::size_t kBytesPerPixel{2U};
    static constexpr std::int32_t kBlockWidth{1};
    static constexpr std::int32_t kBlockHeight{1};
    static constexpr std::size_t kBytesPerBlock{kBytesPerPixel};
    static constexpr bool kPreservesAlpha{false};

    static constexpr Color read(std::span<const std::byte> data, std::size_t offset) noexcept
    {
        const auto low = std::to_integer<std::uint16_t>(data[offset]);
        const auto high = std::to_integer<std::uint16_t>(data[offset + 1U]);
        Rgb565Pixel value = high;
        value <<= detail::kBitsPerByte;
        value |= low;
        return Color::fromRgb565(value);
    }

    static constexpr void write(std::span<std::byte> data, std::size_t offset, Color color) noexcept
    {
        const Rgb565Pixel encoded = color.rgb565();
        data[offset] = std::byte{detail::lowByte(encoded)};
        data[offset + 1U] = std::byte{detail::lowByte(encoded >> detail::kBitsPerByte)};
    }
};

template <> struct PixelFormatTraits<PixelFormat::rgb888>
{
    using Storage = Rgb888Pixel;
    static constexpr std::size_t kBytesPerPixel{3U};
    static constexpr std::int32_t kBlockWidth{1};
    static constexpr std::int32_t kBlockHeight{1};
    static constexpr std::size_t kBytesPerBlock{kBytesPerPixel};
    static constexpr bool kPreservesAlpha{false};

    static constexpr Color read(std::span<const std::byte> data, std::size_t offset) noexcept
    {
        return {std::to_integer<std::uint8_t>(data[offset]),
                std::to_integer<std::uint8_t>(data[offset + 1U]),
                std::to_integer<std::uint8_t>(data[offset + 2U]),
                detail::kMaximumChannelValue};
    }

    static constexpr void write(std::span<std::byte> data, std::size_t offset, Color color) noexcept
    {
        data[offset] = std::byte{color.red};
        data[offset + 1U] = std::byte{color.green};
        data[offset + 2U] = std::byte{color.blue};
    }
};

template <> struct PixelFormatTraits<PixelFormat::xrgb8888>
{
    using Storage = Xrgb8888Pixel;
    static constexpr std::size_t kBytesPerPixel{4U};
    static constexpr std::int32_t kBlockWidth{1};
    static constexpr std::int32_t kBlockHeight{1};
    static constexpr std::size_t kBytesPerBlock{kBytesPerPixel};
    static constexpr bool kPreservesAlpha{false};

    static constexpr Color read(std::span<const std::byte> data, std::size_t offset) noexcept
    {
        return {std::to_integer<std::uint8_t>(data[offset + 2U]),
                std::to_integer<std::uint8_t>(data[offset + 1U]),
                std::to_integer<std::uint8_t>(data[offset]),
                detail::kMaximumChannelValue};
    }

    static constexpr void write(std::span<std::byte> data, std::size_t offset, Color color) noexcept
    {
        data[offset] = std::byte{color.blue};
        data[offset + 1U] = std::byte{color.green};
        data[offset + 2U] = std::byte{color.red};
        data[offset + 3U] = std::byte{detail::kMaximumChannelValue};
    }
};

template <> struct PixelFormatTraits<PixelFormat::argb8888>
{
    using Storage = Argb8888Pixel;
    static constexpr std::size_t kBytesPerPixel{4U};
    static constexpr std::int32_t kBlockWidth{1};
    static constexpr std::int32_t kBlockHeight{1};
    static constexpr std::size_t kBytesPerBlock{kBytesPerPixel};
    static constexpr bool kPreservesAlpha{true};

    static constexpr Color read(std::span<const std::byte> data, std::size_t offset) noexcept
    {
        return {std::to_integer<std::uint8_t>(data[offset + 2U]),
                std::to_integer<std::uint8_t>(data[offset + 1U]),
                std::to_integer<std::uint8_t>(data[offset]),
                std::to_integer<std::uint8_t>(data[offset + 3U])};
    }

    static constexpr void write(std::span<std::byte> data, std::size_t offset, Color color) noexcept
    {
        data[offset] = std::byte{color.blue};
        data[offset + 1U] = std::byte{color.green};
        data[offset + 2U] = std::byte{color.red};
        data[offset + 3U] = std::byte{color.alpha};
    }
};

template <> struct PixelFormatTraits<PixelFormat::r2Packed2x2>
{
    using Storage = std::byte;
    static constexpr std::int32_t kBlockWidth{2};
    static constexpr std::int32_t kBlockHeight{2};
    static constexpr std::size_t kBytesPerBlock{1U};
    static constexpr bool kPreservesAlpha{false};

    // A byte contains four independent red samples: TL bits 3:2, TR 7:6,
    // BL 1:0, BR 5:4. The caller supplies an in-range byte offset and a
    // row-major field index in [0, 3], just as RGB traits require a valid offset.
    static constexpr Color
    read(std::span<const std::byte> data, std::size_t offset, std::size_t fieldIndex) noexcept
    {
        const std::size_t shift = (fieldIndex % 2U) * 4U + (fieldIndex < 2U ? 2U : 0U);
        const std::uint32_t level = (std::to_integer<std::uint32_t>(data[offset]) >> shift) & 3U;
        return {detail::lowByte(level * 85U), 0U, 0U, detail::kMaximumChannelValue};
    }

    static constexpr void write(std::span<std::byte> data,
                                std::size_t offset,
                                std::size_t fieldIndex,
                                Color color) noexcept
    {
        const std::size_t shift = (fieldIndex % 2U) * 4U + (fieldIndex < 2U ? 2U : 0U);
        const std::uint32_t mask = 3U << shift;
        const std::uint32_t level = (std::uint32_t{color.red} * 3U + 127U) / 255U;
        const std::uint32_t previous = std::to_integer<std::uint32_t>(data[offset]);
        data[offset] = std::byte{detail::lowByte((previous & ~mask) | (level << shift))};
    }
};

namespace detail
{

// One logical cell contains four independently stored emitter levels. This
// software packing contract does not identify or certify a physical panel.
template <bool RedYellow> struct NativeCellTraits
{
    using Storage = std::byte;
    static constexpr std::size_t kBytesPerPixel{1U};
    static constexpr std::int32_t kBlockWidth{1};
    static constexpr std::int32_t kBlockHeight{1};
    static constexpr std::size_t kBytesPerBlock{1U};
    static constexpr bool kPreservesAlpha{false};

    // The caller supplies an in-range byte offset and a row-major emitter index
    // in [0, 3]: TL bits 3:2, TR 7:6, BL 1:0, BR 5:4. RYRY assigns yellow to
    // TL/BR and red to TR/BL. Each returned emitter is opaque; there is no
    // clamped whole-cell RGB readback because it would lose independent levels.
    static constexpr Color readEmitter(std::span<const std::byte> data,
                                       std::size_t offset,
                                       std::size_t fieldIndex) noexcept
    {
        Color color = PixelFormatTraits<PixelFormat::r2Packed2x2>::read(data, offset, fieldIndex);
        if constexpr (RedYellow)
        {
            color.green = fieldIndex == 0U || fieldIndex == 3U ? color.red : 0U;
        }
        return color;
    }

    // As with the RGB traits, write encodes an opaque color. compose applies
    // coverage to each existing emitter before quantizing it independently.
    static constexpr void write(std::span<std::byte> data, std::size_t offset, Color color) noexcept
    {
        const std::uint32_t red = (redComponent(color) * 3U + 127U) / 255U;
        const std::uint32_t yellow = (yellowComponent(color) * 3U + 127U) / 255U;
        data[offset] = std::byte{lowByte(red * 0x41U + yellow * 0x14U)};
    }

    static constexpr void
    compose(std::span<std::byte> data, std::size_t offset, Color color) noexcept
    {
        if (color.alpha == 0U)
        {
            return;
        }
        if (color.alpha == 255U)
        {
            write(data, offset, color);
            return;
        }
        const std::uint32_t foregroundWeight = color.alpha;
        const std::uint32_t backgroundWeight = 255U - foregroundWeight;
        const std::uint32_t previous = std::to_integer<std::uint32_t>(data[offset]);
        std::uint32_t encoded = 0U;
        for (std::size_t field = 0U; field < 4U; ++field)
        {
            const std::size_t shift = (field % 2U) * 4U + (field < 2U ? 2U : 0U);
            const std::uint32_t background = ((previous >> shift) & 3U) * 85U;
            const std::uint32_t foreground =
                field == 0U || field == 3U ? yellowComponent(color) : redComponent(color);
            // Match Color::blendedOver's truncation for an opaque background,
            // then apply the native nearest-level quantization.
            const std::uint32_t blended =
                (foreground * foregroundWeight + background * backgroundWeight) / 255U;
            const std::uint32_t level = (blended * 3U + 127U) / 255U;
            encoded |= level << shift;
        }
        data[offset] = std::byte{lowByte(encoded)};
    }

private:
    static constexpr std::uint32_t yellowComponent(Color color) noexcept
    {
        if constexpr (RedYellow)
        {
            return color.red < color.green ? color.red : color.green;
        }
        return color.red;
    }

    static constexpr std::uint32_t redComponent(Color color) noexcept
    {
        if constexpr (RedYellow)
        {
            return std::uint32_t{color.red} - yellowComponent(color);
        }
        return color.red;
    }
};

} // namespace detail

template <> struct PixelFormatTraits<PixelFormat::rrrr2222> : detail::NativeCellTraits<false>
{
};

template <> struct PixelFormatTraits<PixelFormat::ryry2222> : detail::NativeCellTraits<true>
{
};

struct StorageBlock
{
    std::int32_t width{0};
    std::int32_t height{0};
    std::size_t bytes{0U};
};

// Zero fields denote an unknown format. storageLayout() reports that condition
// explicitly; packed formats have a valid block even without integral bytes per pixel.
constexpr StorageBlock storageBlock(PixelFormat format) noexcept
{
    switch (format)
    {
    case PixelFormat::rgb565:
        return {PixelFormatTraits<PixelFormat::rgb565>::kBlockWidth,
                PixelFormatTraits<PixelFormat::rgb565>::kBlockHeight,
                PixelFormatTraits<PixelFormat::rgb565>::kBytesPerBlock};
    case PixelFormat::rgb888:
        return {PixelFormatTraits<PixelFormat::rgb888>::kBlockWidth,
                PixelFormatTraits<PixelFormat::rgb888>::kBlockHeight,
                PixelFormatTraits<PixelFormat::rgb888>::kBytesPerBlock};
    case PixelFormat::xrgb8888:
        return {PixelFormatTraits<PixelFormat::xrgb8888>::kBlockWidth,
                PixelFormatTraits<PixelFormat::xrgb8888>::kBlockHeight,
                PixelFormatTraits<PixelFormat::xrgb8888>::kBytesPerBlock};
    case PixelFormat::argb8888:
        return {PixelFormatTraits<PixelFormat::argb8888>::kBlockWidth,
                PixelFormatTraits<PixelFormat::argb8888>::kBlockHeight,
                PixelFormatTraits<PixelFormat::argb8888>::kBytesPerBlock};
    case PixelFormat::r2Packed2x2:
        return {PixelFormatTraits<PixelFormat::r2Packed2x2>::kBlockWidth,
                PixelFormatTraits<PixelFormat::r2Packed2x2>::kBlockHeight,
                PixelFormatTraits<PixelFormat::r2Packed2x2>::kBytesPerBlock};
    case PixelFormat::rrrr2222:
        return {PixelFormatTraits<PixelFormat::rrrr2222>::kBlockWidth,
                PixelFormatTraits<PixelFormat::rrrr2222>::kBlockHeight,
                PixelFormatTraits<PixelFormat::rrrr2222>::kBytesPerBlock};
    case PixelFormat::ryry2222:
        return {PixelFormatTraits<PixelFormat::ryry2222>::kBlockWidth,
                PixelFormatTraits<PixelFormat::ryry2222>::kBlockHeight,
                PixelFormatTraits<PixelFormat::ryry2222>::kBytesPerBlock};
    }
    return {};
}

// Compatibility helper for formats with an integral number of bytes per pixel.
// Packed R2 returns zero; use storageLayout() for authoritative storage validation.
constexpr std::size_t bytesPerPixel(PixelFormat format) noexcept
{
    switch (format)
    {
    case PixelFormat::rgb565:
        return PixelFormatTraits<PixelFormat::rgb565>::kBytesPerPixel;
    case PixelFormat::rgb888:
        return PixelFormatTraits<PixelFormat::rgb888>::kBytesPerPixel;
    case PixelFormat::xrgb8888:
        return PixelFormatTraits<PixelFormat::xrgb8888>::kBytesPerPixel;
    case PixelFormat::argb8888:
        return PixelFormatTraits<PixelFormat::argb8888>::kBytesPerPixel;
    case PixelFormat::r2Packed2x2:
        return 0U;
    case PixelFormat::rrrr2222:
        return PixelFormatTraits<PixelFormat::rrrr2222>::kBytesPerPixel;
    case PixelFormat::ryry2222:
        return PixelFormatTraits<PixelFormat::ryry2222>::kBytesPerPixel;
    }
    return 0U;
}

constexpr bool preservesAlpha(PixelFormat format) noexcept
{
    return format == PixelFormat::argb8888;
}

} // namespace lithe
