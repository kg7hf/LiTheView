// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/lithe.hpp"

#include <span>

namespace lithe::examples
{

// A storage-block row expands to two presentation rows, with four emitters per
// byte. Geometry comes from the spans; no panel dimensions are implied.
template <PixelFormat Format>
[[nodiscard]] constexpr bool decodeNativeRow(std::span<const std::byte> row,
                                             std::span<Xrgb8888Pixel> destination) noexcept
{
    static_assert(Format == PixelFormat::r2Packed2x2 || Format == PixelFormat::rrrr2222 ||
                  Format == PixelFormat::ryry2222);
    // Divide before comparing so even an oversized span cannot overflow a product.
    if (row.empty() || destination.size() % 4U != 0U || row.size() != destination.size() / 4U)
    {
        return false;
    }
    const std::size_t presentationWidth = destination.size() / 2U;
    for (std::size_t cell = 0U; cell < row.size(); ++cell)
    {
        for (std::size_t field = 0U; field < 4U; ++field)
        {
            const Color color = [&]()
            {
                if constexpr (Format == PixelFormat::r2Packed2x2)
                {
                    return PixelFormatTraits<Format>::read(row, cell, field);
                }
                else
                {
                    return PixelFormatTraits<Format>::readEmitter(row, cell, field);
                }
            }();
            const std::size_t offset = (field / 2U) * presentationWidth + cell * 2U + field % 2U;
            destination[offset] = {color.blue, color.green, color.red, 255U};
        }
    }
    return true;
}

} // namespace lithe::examples
