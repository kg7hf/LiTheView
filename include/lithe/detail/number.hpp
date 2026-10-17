// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <concepts>
#include <cstdint>
#include <limits>
#include <string_view>

namespace lithe::detail
{

template <std::unsigned_integral Destination, std::unsigned_integral Source>
bool assignIfRepresentable(Source value, Destination& result) noexcept
{
    const std::uintmax_t wideValue = value;
    if (wideValue > std::numeric_limits<Destination>::max())
    {
        return false;
    }
    // LITHEVIEW_CAST_BOUNDARY: range-checked external value converted to its destination field
    // type.
    result = static_cast<Destination>(wideValue);
    return true;
}

constexpr std::uint8_t digitValue(char character) noexcept
{
    switch (character)
    {
    case '0':
        return 0U;
    case '1':
        return 1U;
    case '2':
        return 2U;
    case '3':
        return 3U;
    case '4':
        return 4U;
    case '5':
        return 5U;
    case '6':
        return 6U;
    case '7':
        return 7U;
    case '8':
        return 8U;
    case '9':
        return 9U;
    case 'a':
    case 'A':
        return 10U;
    case 'b':
    case 'B':
        return 11U;
    case 'c':
    case 'C':
        return 12U;
    case 'd':
    case 'D':
        return 13U;
    case 'e':
    case 'E':
        return 14U;
    case 'f':
    case 'F':
        return 15U;
    default:
        return 0xffU;
    }
}

template <std::unsigned_integral Integer>
bool parseUnsigned(std::string_view input, Integer& result, std::uint8_t base) noexcept
{
    if (input.empty() || (base != 10U && base != 16U))
    {
        return false;
    }

    std::uintmax_t parsed = 0U;
    const std::uintmax_t typedBase = base;
    const std::uintmax_t maximum = std::numeric_limits<Integer>::max();
    for (const char character : input)
    {
        const std::uint8_t digit = digitValue(character);
        if (digit >= base)
        {
            return false;
        }
        const std::uintmax_t typedDigit = digit;
        if (parsed > (maximum - typedDigit) / typedBase)
        {
            return false;
        }
        parsed = (parsed * typedBase) + typedDigit;
    }
    return assignIfRepresentable(parsed, result);
}

inline bool parseSignedMagnitude(std::string_view digits,
                                 bool negative,
                                 std::int32_t& result,
                                 std::uint8_t base) noexcept
{
    if (digits.empty() || (base != 10U && base != 16U))
    {
        return false;
    }

    const std::int32_t typedBase = base;
    const std::int32_t limit = negative ? std::numeric_limits<std::int32_t>::min()
                                        : -std::numeric_limits<std::int32_t>::max();
    const std::int32_t multiplicationLimit = limit / typedBase;
    std::int32_t parsed = 0;
    for (const char character : digits)
    {
        const std::uint8_t digit = digitValue(character);
        if (digit >= base)
        {
            return false;
        }
        const std::int32_t typedDigit = digit;
        if (parsed < multiplicationLimit)
        {
            return false;
        }
        parsed *= typedBase;
        if (parsed < limit + typedDigit)
        {
            return false;
        }
        parsed -= typedDigit;
    }
    result = negative ? parsed : -parsed;
    return true;
}

} // namespace lithe::detail
