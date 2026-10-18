// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include "UI/glass.hpp"

// Accepted handwritten baseline. Test-only: production uses the generated encoder.
namespace tiny::reference
{
// Nearest eighth, half-up. Zero is empty; 100 fills all eight pieces.
[[nodiscard]] inline bool setVolumeRing(Frame& frame, unsigned percent) noexcept
{
    if (percent > 100U)
        return false;
    const auto count = (percent * 8U + 50U) / 100U;
    for (std::size_t i = 0; i < kVolumeRing.size(); ++i)
        (void)frame.set(kVolumeRing[i], i < count);
    return true;
}
// Deliberately limited, authored glyph set. No raster font or vendor font table.
[[nodiscard]] constexpr std::optional<std::string_view> glyph(char value) noexcept
{
    switch (value)
    {
    case ' ':
        return "";
    case 'A':
        return "ABCEFGM";
    case 'E':
        return "ADEFGM";
    case 'F':
        return "AEFGM";
    case 'G':
        return "ACDEFM";
    case 'H':
        return "BCEFGM";
    case 'I':
        return "ADJP";
    case 'L':
        return "DEF";
    case 'N':
        return "BCEFHN";
    case 'O':
        return "ABCDEF";
    case 'P':
        return "ABEFGM";
    case 'S':
        return "ACDFGM";
    case 'T':
        return "AJP";
    case 'U':
        return "BCDEF";
    case 'V':
        return "EFKQ";
    case 'Y':
        return "HKP";
    case 'M':
        return "BCEFHK";
    default:
        return std::nullopt;
    }
}
inline constexpr std::array<std::string_view, 10> kNumbers{
    "ABCDEF", "BC", "ABDEG", "ABCDG", "BCFG", "ACDFG", "ACDEFG", "ABC", "ABCDEFG", "ABCDFG"};
[[nodiscard]] inline std::optional<Frame> encode(std::string_view label, unsigned value) noexcept
{
    if (label.size() > 7 || value > 9999)
        return {};
    Frame result{};
    for (std::size_t position = 0; position < label.size(); ++position)
    {
        const auto strokes = glyph(label[position]);
        if (!strokes)
            return {};
        for (const char stroke : *strokes)
        {
            const auto index = kStrokes.find(stroke);
            if (index == std::string_view::npos || !result.set(kCharacters[position][index]))
                return {};
        }
    }
    for (std::size_t remaining = 4; remaining > 0; --remaining)
    {
        const auto digit = value % 10U;
        for (const char stroke : kNumbers[digit])
            if (!result.set(kDigits[remaining - 1U][std::string_view{"ABCDEFG"}.find(stroke)]))
                return {};
        value /= 10U;
        if (value == 0)
            break;
    }
    return result;
}
} // namespace tiny::reference
