// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/display.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <limits>
#include <span>
#include <utility>

namespace
{

using lithe::PixelFormat;
using lithe::StorageError;
using R2 = lithe::PixelFormatTraits<PixelFormat::r2Packed2x2>;

constexpr bool hasError(const std::expected<lithe::StorageLayout, StorageError>& result,
                        StorageError error) noexcept
{
    return !result && result.error() == error;
}

constexpr auto kMaximumSize = std::numeric_limits<std::size_t>::max();
constexpr auto kRgb565 = lithe::storageLayout(PixelFormat::rgb565, {480, 272}, 960U);
constexpr auto kXrgb8888 = lithe::storageLayout(PixelFormat::xrgb8888, {480, 272}, 1920U);
constexpr auto kPacked = lithe::storageLayout(PixelFormat::r2Packed2x2, {608, 512}, 304U);
static_assert(kRgb565 && kRgb565->usedBytes == 261120U);
static_assert(kXrgb8888 && kXrgb8888->usedBytes == 522240U);
static_assert(kPacked && kPacked->rowBytes == 304U && kPacked->rowCount == 256U &&
              kPacked->strideBytes == 304U && kPacked->usedBytes == 77824U);
static_assert(std::to_underlying(PixelFormat::r2Packed2x2) == 4U);
static_assert(lithe::bytesPerPixel(PixelFormat::r2Packed2x2) == 0U &&
              lithe::storageBlock(PixelFormat::r2Packed2x2).bytes == 1U);

static_assert(hasError(lithe::storageLayout(PixelFormat{255U}, {2, 2}, 2U),
                       StorageError::unsupportedFormat));
static_assert(hasError(lithe::storageLayout(PixelFormat::rgb565, {0, 2}, 4U),
                       StorageError::invalidExtent));
static_assert(hasError(lithe::storageLayout(PixelFormat::rgb565, {2, -1}, 4U),
                       StorageError::invalidExtent));
static_assert(hasError(lithe::storageLayout(PixelFormat::r2Packed2x2, {3, 2}, 2U),
                       StorageError::misalignedExtent));
static_assert(hasError(lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 3}, 2U),
                       StorageError::misalignedExtent));
static_assert(hasError(lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 2}, 1U, {-1, -2}),
                       StorageError::misalignedOrigin));
static_assert(hasError(lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 2}, 1U, {2, 3}),
                       StorageError::misalignedOrigin));
static_assert(hasError(lithe::storageLayout(PixelFormat::rgb888, {3, 2}, 8U),
                       StorageError::invalidStride));
static_assert(hasError(lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 2}, 0U),
                       StorageError::invalidStride));
static_assert(hasError(lithe::storageLayout(PixelFormat::rgb565, {1, 3}, kMaximumSize),
                       StorageError::arithmeticOverflow));
static_assert(hasError(lithe::storageLayout(PixelFormat::rgb565, {1, 2}, kMaximumSize),
                       StorageError::arithmeticOverflow));
static_assert(hasError(lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 6}, kMaximumSize),
                       StorageError::arithmeticOverflow));
static_assert(hasError(lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 4}, kMaximumSize),
                       StorageError::arithmeticOverflow));
constexpr auto kMaximumCoordinate = std::numeric_limits<std::int32_t>::max();
static_assert(lithe::storageLayout(
    PixelFormat::rgb565, {2, 2}, 4U, {kMaximumCoordinate - 2, kMaximumCoordinate - 2}));
static_assert(lithe::storageLayout(
    PixelFormat::r2Packed2x2, {2, 2}, 1U, {kMaximumCoordinate - 3, kMaximumCoordinate - 3}));
static_assert(
    hasError(lithe::storageLayout(PixelFormat::rgb565, {2, 2}, 4U, {kMaximumCoordinate - 1, 0}),
             StorageError::arithmeticOverflow));
static_assert(
    hasError(lithe::storageLayout(PixelFormat::rgb565, {2, 2}, 4U, {0, kMaximumCoordinate - 1}),
             StorageError::arithmeticOverflow));
static_assert(hasError(
    lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 2}, 1U, {kMaximumCoordinate - 1, 0}),
    StorageError::arithmeticOverflow));
static_assert(hasError(
    lithe::storageLayout(PixelFormat::r2Packed2x2, {2, 2}, 1U, {0, kMaximumCoordinate - 1}),
    StorageError::arithmeticOverflow));

// Compiling this file on Arm exercises minimum-stride multiplication overflow
// that the wider host size_t can represent. No oversized buffer is constructed.
constexpr auto kWideRow = lithe::storageLayout(
    PixelFormat::xrgb8888, {std::numeric_limits<std::int32_t>::max(), 1}, kMaximumSize);
static_assert(sizeof(std::size_t) == 4U ? hasError(kWideRow, StorageError::arithmeticOverflow)
                                        : kWideRow.has_value());

constexpr bool packedBindings() noexcept
{
    std::array<std::byte, 7U> storage{};
    const lithe::Surface surface{storage, PixelFormat::r2Packed2x2, 4, 4, 5U, {-6, -4}};
    const lithe::PixelBufferView view{storage, PixelFormat::r2Packed2x2, 4, 4, 5U};
    const auto result = surface.layout();
    const auto viewed = view.layout();
    if (!surface.valid() || !view.valid() || !result || !viewed || result->rowBytes != 2U ||
        result->rowCount != 2U || result->usedBytes != 7U || surface.usedBytes() != 7U ||
        view.usedBytes() != 7U)
    {
        return false;
    }
    const lithe::Surface shortSurface{
        std::span{storage}.first(6U), PixelFormat::r2Packed2x2, 4, 4, 5U};
    const lithe::PixelBufferView shortView{
        std::span{storage}.first(6U), PixelFormat::r2Packed2x2, 4, 4, 5U};
    return hasError(shortSurface.layout(), StorageError::insufficientStorage) &&
           hasError(shortView.layout(), StorageError::insufficientStorage) &&
           !shortSurface.valid() && !shortView.valid() && shortSurface.usedBytes() == 0U &&
           shortView.usedBytes() == 0U;
}
static_assert(packedBindings());

constexpr bool packedGolden() noexcept
{
    std::array<std::byte, 1U> storage{};
    R2::write(storage, 0U, 0U, {0U, 0U, 0U, 255U});
    R2::write(storage, 0U, 1U, {85U, 0U, 0U, 255U});
    R2::write(storage, 0U, 2U, {170U, 0U, 0U, 255U});
    R2::write(storage, 0U, 3U, {255U, 0U, 0U, 255U});
    return storage[0] == std::byte{0x72} && R2::read(storage, 0U, 0U).red == 0U &&
           R2::read(storage, 0U, 1U).red == 85U && R2::read(storage, 0U, 2U).red == 170U &&
           R2::read(storage, 0U, 3U).red == 255U;
}
static_assert(packedGolden());


class Checks final
{
public:
    void expect(bool condition, const char* description) noexcept
    {
        if (!condition)
        {
            std::printf("FAIL: %s\n", description);
            ++failures;
        }
    }

    int failures{0};
};

template <PixelFormat Format> void checkRgbSurface(Checks& checks)
{
    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;
    std::array<Storage, 5U> storage{};
    const lithe::PixelSurface<Format> typed{storage, 2, 2, 3, {-3, 7}};
    const lithe::Surface surface{typed};
    const lithe::PixelBufferView view{surface.data, Format, 2, 2, surface.strideBytes};
    const auto layout = surface.layout();
    checks.expect(typed.valid() && surface.valid() && view.valid() && layout &&
                      layout->rowBytes == 2U * sizeof(Storage) && layout->rowCount == 2U &&
                      layout->strideBytes == 3U * sizeof(Storage) &&
                      layout->usedBytes == 5U * sizeof(Storage) &&
                      view.usedBytes() == 5U * sizeof(Storage),
                  "typed RGB surfaces retain padded stride and exact final-row capacity");
    const lithe::PixelSurface<Format> shortTyped{std::span{storage}.first(4U), 2, 2, 3};
    const lithe::Surface shortSurface{shortTyped};
    checks.expect(!shortTyped.valid() &&
                      hasError(shortSurface.layout(), StorageError::insufficientStorage),
                  "typed and byte-bound RGB storage reject the same missing final pixel");
    const lithe::PixelSurface<Format> negativeStride{storage, 2, 2, -1};
    checks.expect(!negativeStride.valid() && !lithe::Surface{negativeStride}.valid(),
                  "negative pixel strides fail closed in both RGB entry points");
}

void checkPackedFields(Checks& checks)
{
    constexpr std::array<std::size_t, 4U> shifts{2U, 6U, 0U, 4U};
    constexpr std::array<std::uint8_t, 4U> reds{0U, 85U, 170U, 255U};
    for (std::uint32_t original = 0U; original < 256U; ++original)
    {
        for (std::size_t field = 0U; field < 4U; ++field)
        {
            for (std::uint32_t level = 0U; level < 4U; ++level)
            {
                std::array<std::byte, 3U> storage{
                    std::byte{0xa5}, std::byte{lithe::detail::lowByte(original)}, std::byte{0x5a}};
                R2::write(storage, 1U, field, {reds[level], 241U, 157U, 23U});
                const std::uint32_t mask = 3U << shifts[field];
                const auto expected = (original & ~mask) | (level << shifts[field]);
                const auto decoded = R2::read(storage, 1U, field);
                checks.expect(std::to_integer<std::uint32_t>(storage[1]) == expected &&
                                  storage[0] == std::byte{0xa5} && storage[2] == std::byte{0x5a},
                              "a packed field write preserves every other field and byte");
                checks.expect(decoded.red == reds[level] && decoded.green == 0U &&
                                  decoded.blue == 0U && decoded.alpha == 255U,
                              "packed fields decode to opaque independent red levels");
            }
        }
    }
    // The expected ranges specify nearest-level quantization without copying
    // the implementation's multiply/divide expression.
    for (std::uint32_t red = 0U; red < 256U; ++red)
    {
        const std::uint32_t expected = red < 43U ? 0U : red < 128U ? 1U : red < 213U ? 2U : 3U;
        std::array<std::byte, 1U> storage{};
        R2::write(storage, 0U, 2U, {lithe::detail::lowByte(red), 0U, 0U, 255U});
        checks.expect(std::to_integer<std::uint32_t>(storage[0]) == expected,
                      "all red values follow the declared nearest-level thresholds");
    }
}

} // namespace

int main()
{
    Checks checks{};
    checkRgbSurface<PixelFormat::rgb565>(checks);
    checkRgbSurface<PixelFormat::rgb888>(checks);
    checkRgbSurface<PixelFormat::xrgb8888>(checks);
    checkRgbSurface<PixelFormat::argb8888>(checks);
    checks.expect(packedBindings() && packedGolden(),
                  "packed constexpr contracts also hold at runtime");
    const auto singleRow = lithe::storageLayout(PixelFormat::rgb565, {2, 1}, kMaximumSize);
    checks.expect(singleRow && singleRow->usedBytes == 4U,
                  "unused final-row stride does not require an allocation or overflow");
    const auto minimumOrigin = lithe::storageLayout(
        PixelFormat::r2Packed2x2,
        {2, 2},
        1U,
        {std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::min()});
    checks.expect(minimumOrigin && minimumOrigin->usedBytes == 1U,
                  "negative block-aligned origins including INT32_MIN remain valid");
    checkPackedFields(checks);
    return checks.failures == 0 ? 0 : 1;
}
