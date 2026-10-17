// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "configuration.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <lithe/lithe.hpp>
#include <span>

namespace strategy_fixture
{

constexpr std::array kFormats{lithe::PixelFormat::rgb565,
                              lithe::PixelFormat::rgb888,
                              lithe::PixelFormat::xrgb8888,
                              lithe::PixelFormat::argb8888,
                              lithe::PixelFormat::r2Packed2x2,
                              lithe::PixelFormat::rrrr2222,
                              lithe::PixelFormat::ryry2222};
static_assert(lithe::supportsDestination(kFormat));
static_assert(std::ranges::count_if(kFormats, lithe::supportsDestination) == 1);

inline constexpr auto kViewport = lithe::Viewport::rectangle({0, 0, 8, 8});
inline constexpr std::size_t kFrameBytes = kFormat == lithe::PixelFormat::rgb565        ? 128U
                                           : kFormat == lithe::PixelFormat::xrgb8888    ? 256U
                                           : kFormat == lithe::PixelFormat::r2Packed2x2 ? 16U
                                                                                        : 64U;
inline constexpr std::size_t kRowBytes = kFormat == lithe::PixelFormat::rgb565        ? 16U
                                         : kFormat == lithe::PixelFormat::xrgb8888    ? 32U
                                         : kFormat == lithe::PixelFormat::r2Packed2x2 ? 4U
                                                                                      : 8U;
inline constexpr std::size_t kTileBytes = kFrameBytes / 4U;
inline constexpr auto kLayout = lithe::storageLayout(kFormat, {8, 8}, kRowBytes);
static_assert(kLayout && kLayout->usedBytes == kFrameBytes);

class PatternView final : public lithe::View
{
public:
    PatternView() noexcept : View(kViewport.bounds)
    {
    }

protected:
    void drawSelf(lithe::Painter& painter, lithe::Rect frame) noexcept override
    {
        painter.fillRect(frame, lithe::Color::rgb(0xff0000U));
        painter.fillRect({frame.x, frame.y, 4, 8}, lithe::Color::rgb(0x550000U));
        painter.drawLine(
            {frame.x, frame.y}, {frame.x + 7, frame.y + 7}, lithe::Color::rgb(0xaa0000U));
    }
};

constexpr std::array<std::byte, kFrameBytes> expectedFrame() noexcept
{
    std::array<std::byte, kFrameBytes> result{};
    if constexpr (kFormat == lithe::PixelFormat::r2Packed2x2)
    {
        // Independent native block oracle: left half level one, right half
        // level three, diagonal level two. No renderer encoding helper is used.
        constexpr std::array<std::uint8_t, 16U> packed{0x69U,
                                                       0x55U,
                                                       0xffU,
                                                       0xffU,
                                                       0x55U,
                                                       0x69U,
                                                       0xffU,
                                                       0xffU,
                                                       0x55U,
                                                       0x55U,
                                                       0xebU,
                                                       0xffU,
                                                       0x55U,
                                                       0x55U,
                                                       0xffU,
                                                       0xebU};
        for (std::size_t index = 0U; index < packed.size(); ++index)
        {
            result[index] = std::byte{packed[index]};
        }
    }
    else
    {
        // Explicit byte patterns for red levels 85, 170, and 255. RGB565
        // quantizes these to words 0x5000, 0xa800, and 0xf800 respectively.
        constexpr std::array<std::uint8_t, 3U> rgb565High{0x50U, 0xa8U, 0xf8U};
        constexpr std::array<std::uint8_t, 3U> redChannels{0x55U, 0xaaU, 0xffU};
        // Literal native cell bytes for the same three red levels. RRRR
        // repeats red in all fields; RYRY uses only its TR and BL red fields.
        constexpr std::array<std::uint8_t, 3U> redCellBytes{0x55U, 0xaaU, 0xffU};
        constexpr std::array<std::uint8_t, 3U> dualCellBytes{0x41U, 0x82U, 0xc3U};
        for (std::size_t y = 0U; y < 8U; ++y)
        {
            for (std::size_t x = 0U; x < 8U; ++x)
            {
                const std::size_t level = x == y ? 1U : x < 4U ? 0U : 2U;
                if constexpr (kFormat == lithe::PixelFormat::rgb565)
                {
                    result[y * 16U + x * 2U + 1U] = std::byte{rgb565High[level]};
                }
                else if constexpr (kFormat == lithe::PixelFormat::xrgb8888)
                {
                    result[y * 32U + x * 4U + 2U] = std::byte{redChannels[level]};
                    result[y * 32U + x * 4U + 3U] = std::byte{0xffU};
                }
                else if constexpr (kFormat == lithe::PixelFormat::rrrr2222)
                {
                    result[y * 8U + x] = std::byte{redCellBytes[level]};
                }
                else
                {
                    result[y * 8U + x] = std::byte{dualCellBytes[level]};
                }
            }
        }
    }
    return result;
}

inline constexpr auto kExpectedFrame = expectedFrame();

inline bool matchesOracle(std::span<const std::byte> bytes, std::size_t offset = 0U) noexcept
{
    return offset <= kExpectedFrame.size() && bytes.size() <= kExpectedFrame.size() - offset &&
           std::ranges::equal(bytes, std::span{kExpectedFrame}.subspan(offset, bytes.size()));
}

inline bool writeBytes(std::FILE* output, std::span<const std::byte> bytes) noexcept
{
    for (const auto byte : bytes)
    {
        if (std::fputc(std::to_integer<int>(byte), output) == EOF)
        {
            return false;
        }
    }
    return true;
}

inline int fail(const char* message) noexcept
{
    std::fprintf(stderr, "%s\n", message);
    return 1;
}

inline void report(std::size_t workspaceBytes, std::size_t tiles) noexcept
{
    std::printf("{\"format\":\"%s\",\"strategy\":\"%s\",\"frame_bytes\":%zu,"
                "\"workspace_bytes\":%zu,\"tile_count\":%zu,\"complete\":true}\n",
                std::span{kFormatName}.data(),
                std::span{kStrategyName}.data(),
                kFrameBytes,
                workspaceBytes,
                tiles);
}

} // namespace strategy_fixture
