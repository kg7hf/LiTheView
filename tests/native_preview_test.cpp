// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "native_row_preview.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <span>
#include <string_view>

namespace
{

class Checks final
{
public:
    void expect(bool condition, std::string_view description)
    {
        if (!condition)
        {
            std::cerr << "FAIL: " << description << '\n';
            ++failures;
        }
    }
    int failures{0};
};

constexpr lithe::Xrgb8888Pixel kGuard{17U, 29U, 43U, 59U};

constexpr bool samePixel(lithe::Xrgb8888Pixel left, lithe::Xrgb8888Pixel right) noexcept
{
    return left.blue == right.blue && left.green == right.green && left.red == right.red &&
           left.unused == right.unused;
}

// Independent byte oracle: the software contract locates the fields at TL=2,
// TR=6, BL=0, BR=4. Yellow occupies TL/BR only in the RYRY presentation.
// These tests validate that contract, not the optical layout of a physical panel.
template <lithe::PixelFormat Format>
constexpr bool
expectedPixel(lithe::Xrgb8888Pixel actual, std::uint32_t level, bool yellowPosition) noexcept
{
    constexpr std::array<std::uint8_t, 4U> intensities{0U, 85U, 170U, 255U};
    const auto intensity = intensities[level];
    const std::uint8_t green =
        Format == lithe::PixelFormat::ryry2222 && yellowPosition ? intensity : std::uint8_t{};
    return samePixel(actual, {0U, green, intensity, 255U});
}

template <lithe::PixelFormat Format>
bool rowMatches(std::span<const std::byte> row,
                std::span<const lithe::Xrgb8888Pixel> pixels) noexcept
{
    for (std::size_t cell = 0U; cell < row.size(); ++cell)
    {
        const auto value = std::to_integer<std::uint32_t>(row[cell]);
        const auto top = cell * 2U;
        const auto bottom = row.size() * 2U + top;
        if (!expectedPixel<Format>(pixels[top], (value >> 2U) & 3U, true) ||
            !expectedPixel<Format>(pixels[top + 1U], (value >> 6U) & 3U, false) ||
            !expectedPixel<Format>(pixels[bottom], value & 3U, false) ||
            !expectedPixel<Format>(pixels[bottom + 1U], (value >> 4U) & 3U, true))
        {
            return false;
        }
    }
    return true;
}

template <lithe::PixelFormat Format, std::size_t Cells> void checkLiteralPositions(Checks& checks)
{
    static_assert(Cells >= 6U);
    constexpr std::size_t kWidth{Cells * 2U};
    constexpr std::size_t kPixels{Cells * 4U};
    std::array<std::byte, Cells> row{};
    std::array<lithe::Xrgb8888Pixel, kPixels + 2U> storage{};
    storage.fill(kGuard);
    auto pixels = std::span{storage}.subspan(1U, kPixels);
    row[0U] = std::byte{0x72U};
    row[1U] = std::byte{0x0cU};
    row[2U] = std::byte{0xc0U};
    row[3U] = std::byte{0x03U};
    row[4U] = std::byte{0x30U};
    row.back() = std::byte{0x8dU};
    checks.expect(lithe::examples::decodeNativeRow<Format>(row, pixels),
                  "a complete native row decodes into two physical presentation rows");
    checks.expect(expectedPixel<Format>(pixels[0U], 0U, true) &&
                      expectedPixel<Format>(pixels[1U], 1U, false) &&
                      expectedPixel<Format>(pixels[kWidth], 2U, false) &&
                      expectedPixel<Format>(pixels[kWidth + 1U], 3U, true) &&
                      expectedPixel<Format>(pixels[kWidth - 2U], 3U, true) &&
                      expectedPixel<Format>(pixels[kWidth - 1U], 2U, false) &&
                      expectedPixel<Format>(pixels[kPixels - 2U], 1U, false) &&
                      expectedPixel<Format>(pixels[kPixels - 1U], 0U, true),
                  "literal 0x72 and 0x8d preserve all four levels at both row endpoints");
    checks.expect(rowMatches<Format>(row, pixels),
                  "isolated TL, TR, BL and BR emitters retain their positions and primaries");
    checks.expect(samePixel(storage.front(), kGuard) && samePixel(storage.back(), kGuard),
                  "valid decoding preserves the storage guards around the exact destination");
}

template <lithe::PixelFormat Format, std::size_t Cells> void checkEveryByte(Checks& checks)
{
    std::array<std::byte, Cells> row{};
    std::array<lithe::Xrgb8888Pixel, Cells * 4U> pixels{};
    bool everyByteMatches = true;
    for (std::uint32_t value = 0U; value < 256U; ++value)
    {
        row.fill(std::byte{lithe::detail::lowByte(value)});
        const bool decoded = lithe::examples::decodeNativeRow<Format>(row, pixels);
        everyByteMatches = decoded && rowMatches<Format>(row, pixels) && everyByteMatches;
    }
    checks.expect(everyByteMatches,
                  "all 256 stored bytes decode independently at every cell in the row");
}

template <lithe::PixelFormat Format, std::size_t Cells> void checkInvalidSpans(Checks& checks)
{
    constexpr std::size_t kPixels{Cells * 4U};
    std::array<std::byte, Cells + 1U> source{};
    source.fill(std::byte{0xffU});
    std::array<lithe::Xrgb8888Pixel, kPixels + 3U> storage{};
    constexpr std::array<std::size_t, 4U> sourceSizes{0U, Cells - 1U, Cells, Cells + 1U};
    constexpr std::array<std::size_t, 4U> destinationSizes{0U, kPixels - 1U, kPixels, kPixels + 1U};
    for (const auto sourceSize : sourceSizes)
    {
        for (const auto destinationSize : destinationSizes)
        {
            if (sourceSize == Cells && destinationSize == kPixels)
            {
                continue;
            }
            storage.fill(kGuard);
            const bool result = lithe::examples::decodeNativeRow<Format>(
                std::span{source}.first(sourceSize),
                std::span{storage}.subspan(1U, destinationSize));
            checks.expect(!result && std::ranges::all_of(storage,
                                                         [](auto pixel)
                                                         {
                                                             return samePixel(pixel, kGuard);
                                                         }),
                          "invalid source or destination extent fails before any write");
        }
    }
}

template <lithe::PixelFormat Format> void checkFormat(Checks& checks)
{
    // Include a single block, odd block counts and a larger synthetic row.
    checkLiteralPositions<Format, 7U>(checks);
    checkLiteralPositions<Format, 31U>(checks);
    checkLiteralPositions<Format, 256U>(checks);
    checkEveryByte<Format, 1U>(checks);
    checkEveryByte<Format, 7U>(checks);
    checkEveryByte<Format, 31U>(checks);
    checkEveryByte<Format, 256U>(checks);
    checkInvalidSpans<Format, 1U>(checks);
    checkInvalidSpans<Format, 7U>(checks);
    checkInvalidSpans<Format, 256U>(checks);
}

} // namespace

int main()
{
    Checks checks{};
    checkFormat<lithe::PixelFormat::r2Packed2x2>(checks);
    checkFormat<lithe::PixelFormat::rrrr2222>(checks);
    checkFormat<lithe::PixelFormat::ryry2222>(checks);
    return checks.failures == 0 ? 0 : 1;
}
