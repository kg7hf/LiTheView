// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "demo.hpp"

#include <cstddef>
#include <string_view>

namespace lithe::examples::xrgb8888
{

// This product definition is shared by its host and board entry points.
// Hardware placement and transfer encoding belong to their display adapters.
struct Configuration final
{
    static constexpr DisplayDefinition kDefinition{demo::display_definitions::kH750Xrgb8888};
    static constexpr PixelFormat kFormat{kDefinition.format};
    static constexpr Viewport kViewport{*displayViewport(kDefinition)};
    static constexpr LogicalInputConfiguration kLogicalInput{true, true, true, false, true};
    static constexpr std::size_t kStrideBytes{1920U};
    static constexpr std::size_t kFramebufferBytes{522240U};
    static constexpr BuiltInFontBackend kFontBackend{BuiltInFontBackend::generatedSans};
    static constexpr demo::GallopResourceFormat kAnimationFormat{
        demo::GallopResourceFormat::indexed4};
    // Retained v1 profile identity; kDefinition.id identifies the v2 contract.
    static constexpr std::string_view kSurfaceId{
        "litheview.surface.stm32h750b-dk-rk043fn48h-xrgb8888"};
};

static_assert(Configuration::kViewport.valid());
static_assert(
    storageLayout(Configuration::kFormat, {480, 272}, Configuration::kStrideBytes).has_value());
static_assert(storageLayout(Configuration::kFormat, {480, 272}, Configuration::kStrideBytes)
                  ->usedBytes == Configuration::kFramebufferBytes);

} // namespace lithe::examples::xrgb8888
