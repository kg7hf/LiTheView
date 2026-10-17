// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Application/ui.hpp"
#include "Devices/display_contract.hpp"
#include "Devices/geometry.hpp"
#include "lithe/display.hpp"

#include <cstddef>
#include <span>

namespace starter
{
// The board has this physical panel and framebuffer configuration. A different
// choice in the designer must get a corresponding platform adapter, not a cast.
inline constexpr std::size_t pixelCount{390U * 390U};
inline constexpr std::size_t framebufferBytes{pixelCount * sizeof(lithe::Rgb888Pixel)};

class Display final : public lithe::Display
{
public:
    [[nodiscard]] std::expected<void, ui::Error> initialize() noexcept;
    [[nodiscard]] lithe::Surface surface() noexcept override;
    void present(std::span<const lithe::Rect> damage) noexcept override;

private:
    std::span<lithe::Rgb888Pixel> mPixels;
    std::span<lithe::Rgb888Pixel> mScanoutPixels;
};
} // namespace starter
