// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/lithe.hpp"

#include <cstdint>

namespace lithe::stm32u545::detail
{

template <std::int32_t Width, std::int32_t Height> class CrcSink final : public TileSink
{
public:
    bool beginFrame(Size frameSize, PixelFormat format) noexcept override
    {
        mCrc = 0xffffffffU;
        mExpectedY = 0;
        mValid =
            frameSize.width == Width && frameSize.height == Height && format == PixelFormat::rgb565;
        return mValid;
    }

    bool writeTile(Rect destination, PixelBufferView pixels) noexcept override
    {
        if (!mValid || destination.x != 0 || destination.y != mExpectedY ||
            destination.width != Width || destination.height <= 0 ||
            pixels.format != PixelFormat::rgb565 || pixels.width != destination.width ||
            pixels.height != destination.height || !pixels.valid())
        {
            mValid = false;
            return false;
        }
        for (std::int32_t row = 0; row < destination.height; ++row)
        {
            for (std::int32_t column = 0; column < destination.width; ++column)
            {
                const auto offset = (lithe::detail::nonnegativeSize(row) * pixels.strideBytes) +
                                    (lithe::detail::nonnegativeSize(column) * sizeof(Pixel));
                update(std::to_integer<std::uint8_t>(pixels.data[offset]));
                update(std::to_integer<std::uint8_t>(pixels.data[offset + 1U]));
            }
        }
        mExpectedY += destination.height;
        return true;
    }

    bool endFrame(bool complete) noexcept override
    {
        mValid = mValid && complete && mExpectedY == Height;
        return mValid;
    }

    std::uint32_t value() const noexcept
    {
        return mCrc ^ 0xffffffffU;
    }

private:
    void update(std::uint8_t byte) noexcept
    {
        mCrc ^= byte;
        for (std::uint8_t bit = 0U; bit < 8U; ++bit)
        {
            const std::uint32_t mask = 0U - (mCrc & 1U);
            mCrc = (mCrc >> 1U) ^ (0xedb88320U & mask);
        }
    }

    std::uint32_t mCrc{0xffffffffU};
    std::int32_t mExpectedY{0};
    bool mValid{false};
};

} // namespace lithe::stm32u545::detail
