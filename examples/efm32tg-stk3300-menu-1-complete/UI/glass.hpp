// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include "lithe/segment_frame.hpp"

#include <array>
#include <optional>
#include <string_view>

namespace tiny
{
using Frame = lithe::SegmentFrame<160>;
// Glass COM/S coordinates from t0011_1.00 page 26 (BRD2100A A03).
// Glass COM0 is wired to MCU COM7. This is NOT a register image.
constexpr std::size_t electrode(std::size_t common, std::size_t segment) noexcept
{
    return common * 20U + segment;
}
// A03 ring, clockwise from twelve o'clock: A7, A0, A1, ... A6.
// These are glass coordinates; lcdRows reverses COM7 onto MCU COM0.
inline constexpr std::array<std::size_t, 8> kVolumeRing{electrode(7, 0),
                                                        electrode(7, 7),
                                                        electrode(7, 6),
                                                        electrode(7, 5),
                                                        electrode(7, 4),
                                                        electrode(7, 3),
                                                        electrode(7, 2),
                                                        electrode(7, 1)};

inline constexpr std::string_view kStrokes{"ABCDEFGHJKMNPQ"};
constexpr std::array<std::size_t, 14> characterMap(std::size_t position) noexcept
{
    const auto left = position * 2U + 1U;
    const auto right = left + 1U;
    const auto offset = position >= 4U ? 1U : 0U;
    return {electrode(6U + offset, left),
            electrode(6, right),
            electrode(2, right),
            electrode(0, right),
            electrode(offset, left),
            electrode(4U + offset, left),
            electrode(3U + offset, left),
            electrode(5U + offset, left),
            electrode(4, right),
            electrode(5, right),
            electrode(3, right),
            electrode(1, right),
            electrode(2U + offset, left),
            electrode(1U + offset, left)};
}
inline constexpr auto kCharacters = []
{
    std::array<std::array<std::size_t, 14>, 7> result{};
    for (std::size_t i = 0; i < result.size(); ++i)
        result[i] = characterMap(i);
    return result;
}();
inline constexpr auto kDigits = []
{
    std::array<std::array<std::size_t, 7>, 4> result{};
    constexpr std::array<std::size_t, 7> common{0, 2, 5, 6, 4, 1, 3};
    for (std::size_t i = 0; i < 4; ++i)
        for (std::size_t j = 0; j < 7; ++j)
            result[i][j] = electrode(common[j], 18U - i);
    return result;
}();
constexpr bool validMapping() noexcept
{
    std::array<bool, 160> used{};
    for (const auto& cell : kCharacters)
        for (const auto id : cell)
        {
            if (id >= used.size() || used[id])
                return false;
            used[id] = true;
        }
    for (const auto& cell : kDigits)
        for (const auto id : cell)
        {
            if (id >= used.size() || used[id])
                return false;
            used[id] = true;
        }
    for (const auto id : kVolumeRing)
    {
        if (id >= used.size() || used[id])
            return false;
        used[id] = true;
    }
    return true;
}
static_assert(validMapping());
static_assert(sizeof(Frame) == 20);

} // namespace tiny
