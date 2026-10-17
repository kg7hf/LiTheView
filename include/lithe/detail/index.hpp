// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstddef>
#include <cstdint>
#include <limits>

namespace lithe::detail
{

// Geometry is signed so clipping and off-screen coordinates are representable;
// contiguous storage is indexed by size_t. This function is the single audited
// transition between those domains and fails closed for negative input.
constexpr std::size_t nonnegativeSize(std::int32_t value) noexcept
{
    // LITHEVIEW_CAST_BOUNDARY: validated signed geometry to the standard index domain.
    return value < 0 ? 0U : static_cast<std::size_t>(value);
}

constexpr bool checkedMultiply(std::size_t left, std::size_t right, std::size_t& result) noexcept
{
    if (right != 0U && left > std::numeric_limits<std::size_t>::max() / right)
    {
        return false;
    }
    result = left * right;
    return true;
}

} // namespace lithe::detail
