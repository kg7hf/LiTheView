// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "demo.hpp"

#include <cstddef>
#include <string_view>

namespace lithe::examples::rgb565
{

// This product definition is shared by its host and board entry points.
// Hardware placement and transfer encoding belong to their display adapters.
struct Configuration final
{
    static constexpr DisplayDefinition kDefinition{demo::display_definitions::kH750Rgb565};
    static constexpr PixelFormat kFormat{kDefinition.format};
    static constexpr Viewport kViewport{*displayViewport(kDefinition)};
    static constexpr LogicalInputConfiguration kLogicalInput{true, true, true, false, true};
    static constexpr std::size_t kStrideBytes{960U};
    static constexpr std::size_t kFramebufferBytes{261120U};
    static constexpr BuiltInFontBackend kFontBackend{BuiltInFontBackend::compact5x7};
    static constexpr demo::GallopResourceFormat kAnimationFormat{
        demo::GallopResourceFormat::alpha1};
    // Retained v1 profile identity; kDefinition.id identifies the v2 contract.
    static constexpr std::string_view kSurfaceId{
        "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565"};
};

static_assert(Configuration::kViewport.valid());
static_assert(
    storageLayout(Configuration::kFormat, {480, 272}, Configuration::kStrideBytes).has_value());
static_assert(storageLayout(Configuration::kFormat, {480, 272}, Configuration::kStrideBytes)
                  ->usedBytes == Configuration::kFramebufferBytes);

} // namespace lithe::examples::rgb565
