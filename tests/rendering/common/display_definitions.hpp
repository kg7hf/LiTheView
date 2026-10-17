// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/display_definition.hpp"

namespace lithe::demo::display_definitions
{

// Handwritten counterparts of profiles/display-definitions/*.json. These
// identities describe compiled software contracts; native emitter positions do
// not claim physical panel verification. Drivers and logical input are separate.
inline constexpr DisplayDefinition kH750Rgb565{"litheview.definition.software.rk043fn48h-rgb565",
                                               {480, 272},
                                               ViewportShape::rectangle,
                                               {0, 0, 480, 272},
                                               PixelFormat::rgb565,
                                               DisplayAddressSpace::pixels};
inline constexpr DisplayDefinition kH750Xrgb8888{
    "litheview.definition.software.rk043fn48h-xrgb8888",
    {480, 272},
    ViewportShape::rectangle,
    {0, 0, 480, 272},
    PixelFormat::xrgb8888,
    DisplayAddressSpace::pixels};
inline constexpr DisplayDefinition kRound240{"litheview.definition.software.round-240-rgb565",
                                             {240, 240},
                                             ViewportShape::circle,
                                             {36, 36, 168, 168},
                                             PixelFormat::rgb565,
                                             DisplayAddressSpace::pixels};
static_assert(validateDisplayDefinition(kH750Rgb565));
static_assert(validateDisplayDefinition(kH750Xrgb8888));
static_assert(validateDisplayDefinition(kRound240));

// Both RGB products use the same scene geometry. Their destination encoding
// and selected resources remain separate compile-time product choices.
static_assert(kH750Rgb565.logicalSize.width == kH750Xrgb8888.logicalSize.width &&
              kH750Rgb565.logicalSize.height == kH750Xrgb8888.logicalSize.height &&
              kH750Rgb565.shape == kH750Xrgb8888.shape &&
              kH750Rgb565.addressSpace == kH750Xrgb8888.addressSpace &&
              kH750Rgb565.safeArea.x == kH750Xrgb8888.safeArea.x &&
              kH750Rgb565.safeArea.y == kH750Xrgb8888.safeArea.y &&
              kH750Rgb565.safeArea.width == kH750Xrgb8888.safeArea.width &&
              kH750Rgb565.safeArea.height == kH750Xrgb8888.safeArea.height);
inline constexpr Viewport kStandardViewport{*displayViewport(kH750Rgb565)};

} // namespace lithe::demo::display_definitions
