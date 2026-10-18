// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include "UI/glass.hpp"
#include <cstdint>
namespace tiny
{
// PCB2100 A03 reverses glass COM0..7 onto controller COM7..0.
// Glass S0..19 connects directly to controller SEG0..19.
[[nodiscard]] constexpr std::array<std::uint32_t, 8> lcdRows(const Frame& frame) noexcept
{
    std::array<std::uint32_t, 8> rows{};
    for (unsigned common = 0; common < 8; ++common)
        for (unsigned segment = 0; segment < 20; ++segment)
            if (frame.test(common * 20U + segment))
                rows[7U - common] |= 1U << segment;
    return rows;
}
} // namespace tiny
