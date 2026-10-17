// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/config.hpp"
#include "lithe/detail/arithmetic.hpp"
#include "lithe/detail/index.hpp"
#include "lithe/pixel_format.hpp"
#include "lithe/storage_layout.hpp"

#include <array>
#include <cstddef>
#include <span>

namespace lithe
{

template <PixelFormat Format> struct PixelSurface
{
    static_assert(
        PixelFormatTraits<Format>::kBlockWidth == 1 && PixelFormatTraits<Format>::kBlockHeight == 1,
        "PixelSurface requires 1x1 pixels; use Surface with byte storage for packed formats");
    using Storage = typename PixelFormatTraits<Format>::Storage;

    std::span<Storage> pixels;
    std::int32_t width{0};
    std::int32_t height{0};
    std::int32_t stridePixels{0};
    Point origin{};

    constexpr Rect bounds() const noexcept
    {
        return {origin.x, origin.y, width, height};
    }

    constexpr bool valid() const noexcept
    {
        std::size_t stride = 0U;
        std::size_t capacity = 0U;
        if (!detail::checkedMultiply(detail::nonnegativeSize(stridePixels),
                                     PixelFormatTraits<Format>::kBytesPerBlock,
                                     stride) ||
            !detail::checkedMultiply(pixels.size(), sizeof(Storage), capacity))
        {
            return false;
        }
        const auto layout = storageLayout(Format, {width, height}, stride, origin);
        return layout && layout->usedBytes <= capacity;
    }
};

using Rgb565Surface = PixelSurface<PixelFormat::rgb565>;
using Rgb888Surface = PixelSurface<PixelFormat::rgb888>;
using Xrgb8888Surface = PixelSurface<PixelFormat::xrgb8888>;
using Argb8888Surface = PixelSurface<PixelFormat::argb8888>;

struct Surface
{
    std::span<std::byte> data;
    PixelFormat format{PixelFormat::rgb565};
    std::int32_t width{0};
    std::int32_t height{0};
    std::size_t strideBytes{0U};
    Point origin{};

    constexpr Surface() noexcept = default;

    Surface(std::span<Pixel> pixels,
            std::int32_t surfaceWidth,
            std::int32_t surfaceHeight,
            std::int32_t stridePixels,
            Point surfaceOrigin = {}) noexcept
        : data(std::as_writable_bytes(pixels)), width(surfaceWidth), height(surfaceHeight),
          strideBytes(pixelStrideBytes(stridePixels, sizeof(Pixel))), origin(surfaceOrigin)
    {
    }

    template <PixelFormat Format>
    explicit Surface(PixelSurface<Format> surface) noexcept
        : data(std::as_writable_bytes(surface.pixels)), format(Format), width(surface.width),
          height(surface.height),
          strideBytes(
              pixelStrideBytes(surface.stridePixels, PixelFormatTraits<Format>::kBytesPerBlock)),
          origin(surface.origin)
    {
    }

    constexpr Surface(std::span<std::byte> storage,
                      PixelFormat pixelFormat,
                      std::int32_t surfaceWidth,
                      std::int32_t surfaceHeight,
                      std::size_t rowStrideBytes,
                      Point surfaceOrigin = {}) noexcept
        : data(storage), format(pixelFormat), width(surfaceWidth), height(surfaceHeight),
          strideBytes(rowStrideBytes), origin(surfaceOrigin)
    {
    }

    constexpr Rect bounds() const noexcept
    {
        return {origin.x, origin.y, width, height};
    }
    [[nodiscard]] constexpr std::expected<StorageLayout, StorageError> layout() const noexcept
    {
        const auto result = storageLayout(format, {width, height}, strideBytes, origin);
        if (!result)
        {
            return result;
        }
        if (result->usedBytes > data.size())
        {
            return std::unexpected{StorageError::insufficientStorage};
        }
        return result;
    }

    constexpr bool valid() const noexcept
    {
        return layout().has_value();
    }

    constexpr std::size_t usedBytes() const noexcept
    {
        const auto result = layout();
        return result ? result->usedBytes : 0U;
    }

private:
    static constexpr std::size_t pixelStrideBytes(std::int32_t stridePixels,
                                                  std::size_t pixelBytes) noexcept
    {
        std::size_t result = 0U;
        return detail::checkedMultiply(detail::nonnegativeSize(stridePixels), pixelBytes, result)
                   ? result
                   : 0U;
    }
};

struct PixelBufferView
{
    std::span<const std::byte> data;
    PixelFormat format{PixelFormat::rgb565};
    std::int32_t width{0};
    std::int32_t height{0};
    std::size_t strideBytes{0U};

    [[nodiscard]] constexpr std::expected<StorageLayout, StorageError> layout() const noexcept
    {
        const auto result = storageLayout(format, {width, height}, strideBytes);
        if (!result)
        {
            return result;
        }
        if (result->usedBytes > data.size())
        {
            return std::unexpected{StorageError::insufficientStorage};
        }
        return result;
    }

    constexpr bool valid() const noexcept
    {
        return layout().has_value();
    }

    constexpr std::size_t usedBytes() const noexcept
    {
        const auto result = layout();
        return result ? result->usedBytes : 0U;
    }
};

class Display
{
public:
    Display() = default;
    virtual ~Display() = default;

    Display(const Display&) = delete;
    Display& operator=(const Display&) = delete;
    Display(Display&&) = delete;
    Display& operator=(Display&&) = delete;

    virtual Surface surface() noexcept = 0;
    virtual void present(std::span<const Rect> regions) noexcept = 0;
};

class DamageTracker
{
public:
    void clear() noexcept
    {
        mCount = 0;
    }
    void add(Rect region) noexcept;

    std::span<const Rect> regions() const noexcept
    {
        return std::span<const Rect>{mRegions}.first(mCount);
    }
    std::size_t size() const noexcept
    {
        return mCount;
    }
    bool empty() const noexcept
    {
        return mCount == 0;
    }

private:
    std::array<Rect, config::kMaxDamageRegions> mRegions{};
    std::size_t mCount{0};
};

} // namespace lithe
