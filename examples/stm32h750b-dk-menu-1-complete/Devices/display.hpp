// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Application/ui.hpp"
#include "Tasks/UI/Generated/Home/display.hpp"
#include "lithe/display.hpp"

#include <cstddef>
#include <span>

namespace starter
{
// The board has this physical panel and framebuffer configuration. A different
// choice in the designer must get a corresponding platform adapter, not a cast.
inline constexpr std::int32_t width{480};
inline constexpr std::int32_t height{272};
inline constexpr std::size_t pixelCount{480U * 272U};
inline constexpr std::size_t framebufferBytes{pixelCount * sizeof(lithe::Rgb565Pixel)};
static_assert(lithe::generated::assets::HomeMenu::DisplayConfiguration::kDefinition.logicalSize.width == width);
static_assert(lithe::generated::assets::HomeMenu::DisplayConfiguration::kDefinition.logicalSize.height == height);
static_assert(lithe::generated::assets::HomeMenu::DisplayConfiguration::kFormat == lithe::PixelFormat::rgb565);

class Display final : public lithe::Display
{
public:
    [[nodiscard]] std::expected<void, ui::Error> initialize() noexcept;
    [[nodiscard]] lithe::Surface surface() noexcept override;
    void present(std::span<const lithe::Rect> damage) noexcept override;

private:
    std::span<lithe::Rgb565Pixel> mPixels;
    std::span<lithe::Rgb565Pixel> mScanoutPixels;
};
} // namespace starter
