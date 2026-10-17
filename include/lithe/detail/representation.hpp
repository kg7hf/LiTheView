// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/detail/constants.hpp"

#include <bit>
#include <cstdint>

namespace lithe::detail
{

// These are representation boundaries, not application-domain conversions.
// Each narrowing input is masked before the single audited cast.
constexpr std::uint8_t lowByte(std::uint32_t value) noexcept
{
    // LITHEVIEW_CAST_BOUNDARY: masked packed value to one byte.
    return static_cast<std::uint8_t>(value & kByteMask);
}

constexpr std::uint8_t codeUnit(char value) noexcept
{
    // LITHEVIEW_CAST_BOUNDARY: preserve the object representation of a UTF-8 code unit.
    return std::bit_cast<std::uint8_t>(value);
}

constexpr char utf8CodeUnit(char32_t value) noexcept
{
    const std::uint32_t scalar = value;
    // LITHEVIEW_CAST_BOUNDARY: preserve the low UTF-8 byte in possibly signed char storage.
    return std::bit_cast<char>(lowByte(scalar));
}

} // namespace lithe::detail
