// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/layout_validation.hpp"
#include "lithe/render_config.hpp"
#include "product_config.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <limits>
#include <optional>

namespace lithe::simulator
{

// The form's own layout type builds only the widget kinds it uses.
using Views = Form::Layout;
inline constexpr auto kDisplay = Configuration::kDefinition;
inline constexpr auto kInput = Configuration::kInput;
static_assert(validateDisplayDefinition(kDisplay).has_value());
static_assert(kDisplay.format == PixelFormat::rgb565 || kDisplay.format == PixelFormat::rgb888 ||
                  kDisplay.format == PixelFormat::xrgb8888 ||
                  kDisplay.format == PixelFormat::argb8888,
              "This simulator supports RGB destinations; packed displays need a native adapter.");
static_assert(
    []
    {
        for (const auto format : {PixelFormat::rgb565,
                                  PixelFormat::rgb888,
                                  PixelFormat::xrgb8888,
                                  PixelFormat::argb8888,
                                  PixelFormat::r2Packed2x2,
                                  PixelFormat::rrrr2222,
                                  PixelFormat::ryry2222})
        {
            if (supportsDestination(format) != (format == kDisplay.format))
            {
                return false;
            }
        }
        return true;
    }(),
    "Install a Core package compiled for exactly the selected destination.");

inline constexpr std::int32_t kWidth{kDisplay.logicalSize.width};
inline constexpr std::int32_t kHeight{kDisplay.logicalSize.height};
inline constexpr std::size_t kWidthCount{kWidth};
inline constexpr std::size_t kHeightCount{kHeight};
inline constexpr std::size_t kPixelBytes{bytesPerPixel(kDisplay.format)};
static_assert(kWidthCount <= std::numeric_limits<std::size_t>::max() / kPixelBytes);
inline constexpr std::size_t kStrideBytes{kWidthCount * kPixelBytes};
inline constexpr auto kFrameLayout =
    storageLayout(kDisplay.format, kDisplay.logicalSize, kStrideBytes);
static_assert(kFrameLayout.has_value(), "The generated framebuffer size must be representable.");
inline constexpr std::size_t kFrameBytes{kFrameLayout->usedBytes};
inline constexpr std::size_t kPixelCount{kFrameBytes / kPixelBytes};
inline constexpr std::int32_t kTileHeight{std::min(kHeight, std::int32_t{4})};
inline constexpr auto kTileLayout =
    storageLayout(kDisplay.format, {kWidth, kTileHeight}, kStrideBytes);
static_assert(kTileLayout.has_value());
inline constexpr std::size_t kTileBytes{kTileLayout->usedBytes};
inline constexpr std::size_t kTileCount{(kHeightCount + 3U) / 4U};

constexpr std::optional<std::size_t> mutableTextBytes() noexcept
{
    std::size_t bytes = 0U;
    for (std::size_t index = 0U; index < Form::kBindings.size(); ++index)
    {
        const auto& binding = Form::kBindings[index];
        if (binding.name.empty() || binding.capacityBytes == 0U ||
            binding.capacityBytes > std::numeric_limits<std::size_t>::max() - bytes)
        {
            return std::nullopt;
        }
        for (std::size_t previous = 0U; previous < index; ++previous)
        {
            if (Form::kBindings[previous].name == binding.name)
            {
                return std::nullopt;
            }
        }
        bytes += binding.capacityBytes;
    }
    return bytes;
}
inline constexpr auto kTextStorage = mutableTextBytes();
static_assert(kTextStorage.has_value(),
              "Mutable bindings need unique names and bounded capacities.");
inline constexpr std::size_t kTextBytes{kTextStorage.value_or(0U)};

} // namespace lithe::simulator
