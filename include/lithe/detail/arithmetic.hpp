// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstdint>
#include <limits>

namespace lithe::detail
{

constexpr std::int32_t saturatingInt32(std::int64_t value) noexcept
{
    constexpr std::int64_t minimum = std::numeric_limits<std::int32_t>::min();
    constexpr std::int64_t maximum = std::numeric_limits<std::int32_t>::max();
    if (value < minimum)
    {
        return std::numeric_limits<std::int32_t>::min();
    }
    if (value > maximum)
    {
        return std::numeric_limits<std::int32_t>::max();
    }
    // LITHEVIEW_CAST_BOUNDARY: range-checked wide arithmetic returns to GUI geometry storage.
    return static_cast<std::int32_t>(value);
}

constexpr std::int32_t saturatingAdd(std::int32_t left, std::int32_t right) noexcept
{
    constexpr std::int32_t minimum = std::numeric_limits<std::int32_t>::min();
    constexpr std::int32_t maximum = std::numeric_limits<std::int32_t>::max();
    if (right > 0 && left > maximum - right)
    {
        return maximum;
    }
    if (right < 0 && left < minimum - right)
    {
        return minimum;
    }
    return left + right;
}

constexpr std::int32_t saturatingSubtract(std::int32_t minuend, std::int32_t subtrahend) noexcept
{
    constexpr std::int32_t minimum = std::numeric_limits<std::int32_t>::min();
    constexpr std::int32_t maximum = std::numeric_limits<std::int32_t>::max();
    if (subtrahend > 0 && minuend < minimum + subtrahend)
    {
        return minimum;
    }
    if (subtrahend < 0 && minuend > maximum + subtrahend)
    {
        return maximum;
    }
    return minuend - subtrahend;
}

constexpr std::int32_t saturatingMultiply(std::int32_t left, std::int32_t right) noexcept
{
    const std::int64_t wideLeft = left;
    const std::int64_t wideRight = right;
    return saturatingInt32(wideLeft * wideRight);
}

constexpr std::int32_t
multiplyDivide(std::int32_t left, std::int32_t right, std::int32_t divisor) noexcept
{
    if (divisor == 0)
    {
        return 0;
    }
    const std::int64_t wideLeft = left;
    const std::int64_t wideRight = right;
    const std::int64_t wideDivisor = divisor;
    return saturatingInt32((wideLeft * wideRight) / wideDivisor);
}

} // namespace lithe::detail
