// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstdint>
#include <optional>
#include <string_view>

namespace starter::host
{
struct Area final
{
    std::int32_t x{};
    std::int32_t y{};
    std::int32_t width{};
    std::int32_t height{};

    [[nodiscard]] constexpr bool contains(std::int32_t px, std::int32_t py) const noexcept
    {
        return px >= x && py >= y && px - x < width && py - y < height;
    }
};

struct NamedButton final
{
    std::string_view id;
    Area bounds;
};

struct DisplayPoint final
{
    std::int32_t x{};
    std::int32_t y{};
};

// All dimensions are validated at configuration (image <= 8192, window <= 4096).
// The same integer transform drives painting and hit testing at every zoom.
struct FrontPanelTransform final
{
    std::int32_t imageWidth{};
    std::int32_t imageHeight{};
    std::int32_t windowWidth{};
    std::int32_t windowHeight{};

    [[nodiscard]] constexpr Area toWindow(Area source) const noexcept
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return {};
        }
        const auto left = source.x * windowWidth / imageWidth;
        const auto top = source.y * windowHeight / imageHeight;
        return {left, top, (source.x + source.width) * windowWidth / imageWidth - left,
                (source.y + source.height) * windowHeight / imageHeight - top};
    }

    [[nodiscard]] constexpr std::optional<DisplayPoint> displayPoint(
        Area source, std::int32_t x, std::int32_t y, std::int32_t width, std::int32_t height) const noexcept
    {
        const auto destination = toWindow(source);
        if (!destination.contains(x, y) || destination.width <= 0 || destination.height <= 0)
        {
            return std::nullopt;
        }
        return DisplayPoint{(x - destination.x) * width / destination.width,
                            (y - destination.y) * height / destination.height};
    }
};
} // namespace starter::host
