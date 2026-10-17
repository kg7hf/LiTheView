// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/layout.hpp"
#include "lithe/surface_layout.hpp"

#include <expected>
#include <span>
#include <variant>

namespace lithe
{

// This is a placement default, not an inference about required input devices.
// Applications can explicitly allow a control outside the conservative safe
// rectangle with visibleShape, or allow intentional drawing to be clipped.
[[nodiscard]] constexpr SurfacePlacementPolicy defaultSurfacePlacement(const ViewSpec& spec,
                                                                       std::size_t index) noexcept
{
    if (index == 0U)
    {
        return SurfacePlacementPolicy::clipToViewport;
    }
    if (spec.focusable > 0)
    {
        return SurfacePlacementPolicy::safeArea;
    }
    switch (spec.kind)
    {
    case ViewKind::button:
    case ViewKind::checkbox:
    case ViewKind::radio:
    case ViewKind::textField:
    case ViewKind::picker:
    case ViewKind::softkey:
    case ViewKind::tabBar:
    case ViewKind::tabView:
    case ViewKind::wheelPicker:
    case ViewKind::dropDown:
    case ViewKind::scroll:
    case ViewKind::table:
        return SurfacePlacementPolicy::safeArea;
    default:
        return SurfacePlacementPolicy::clipToViewport;
    }
}

// The same borrowed ViewSpec sequence can come from handwritten C++, generated
// C++, or XmlLayoutParser. Validation does not construct widgets or retain data.
[[nodiscard]] constexpr std::expected<void, SurfaceLayoutFailure>
validateLayoutSurface(const DisplayDefinition& definition,
                      std::span<const ViewSpec> specs,
                      LogicalInputConfiguration input = {},
                      SurfaceInputRequirements requirements = {},
                      std::span<const SurfacePlacementPolicy> placement = {}) noexcept
{
    if (!placement.empty() && placement.size() != specs.size())
    {
        return std::unexpected{SurfaceLayoutFailure{SurfaceLayoutError::invalidPolicy, 0U}};
    }
    const auto geometry = validateSurfaceLayout(
        definition,
        specs.size(),
        [&specs, &placement](std::size_t index) noexcept
        {
            const ViewSpec& spec = specs[index];
            return SurfaceLayoutNode{spec.parent,
                                     spec.frame,
                                     placement.empty() ? defaultSurfacePlacement(spec, index)
                                                       : placement[index],
                                     spec.kind == ViewKind::scroll || spec.kind == ViewKind::table,
                                     spec.contentHeight};
        });
    if (!geometry)
    {
        return geometry;
    }
    const auto interaction = validateSurfaceInput(input, requirements);
    if (!interaction)
    {
        return std::unexpected{SurfaceLayoutFailure{interaction.error(), 0U}};
    }
    return {};
}

using SurfaceLayoutBuildFailure = std::variant<SurfaceLayoutFailure, LayoutResult>;

// A surface failure leaves an already constructed layout intact. Construction
// errors retain StaticLayout::build's existing clearing/lifetime semantics.
template <std::size_t Capacity, typename Widgets>
[[nodiscard]] std::expected<void, SurfaceLayoutBuildFailure>
buildSurfaceLayout(StaticLayout<Capacity, Widgets>& layout,
                   const DisplayDefinition& definition,
                   std::span<const ViewSpec> specs,
                   LogicalInputConfiguration input = {},
                   SurfaceInputRequirements requirements = {},
                   std::span<const SurfacePlacementPolicy> placement = {},
                   const Font& defaultFont = font5x7(),
                   const ResourceProvider* resources = nullptr,
                   std::span<const std::string_view> animationFrames = {}) noexcept
{
    const auto compatible =
        validateLayoutSurface(definition, specs, input, requirements, placement);
    if (!compatible)
    {
        return std::unexpected{SurfaceLayoutBuildFailure{compatible.error()}};
    }
    const LayoutResult built = layout.build(specs, defaultFont, resources, animationFrames);
    if (!built)
    {
        return std::unexpected{SurfaceLayoutBuildFailure{built}};
    }
    return {};
}

} // namespace lithe
