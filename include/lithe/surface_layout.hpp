// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/display_definition.hpp"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <expected>
#include <limits>
#include <optional>
#include <span>
#include <string_view>

namespace lithe
{

enum class SurfacePlacementPolicy : std::uint8_t
{
    safeArea,
    visibleShape,
    clipToViewport
};

// These are application operations, not the union of every input handler a
// widget implements. Encoder rotation can navigate but cannot activate alone.
struct SurfaceInputRequirements
{
    bool activation{false};
    bool navigation{false};
    bool text{false};
};

enum class [[nodiscard]] SurfaceLayoutError : std::uint8_t
{
    invalidDefinition,
    invalidPolicy,
    invalidGeometry,
    outsideSafeArea,
    outsideVisibleShape,
    fullyClipped,
    invalidHierarchy,
    unreachablePlacement,
    missingActivation,
    missingNavigation,
    missingText,
    popupCannotFit
};

[[nodiscard]] constexpr std::string_view diagnosticId(SurfaceLayoutError error) noexcept
{
    switch (error)
    {
    case SurfaceLayoutError::invalidDefinition:
        return "LVSL3001";
    case SurfaceLayoutError::invalidPolicy:
        return "LVSL3002";
    case SurfaceLayoutError::invalidGeometry:
        return "LVSL3003";
    case SurfaceLayoutError::outsideSafeArea:
        return "LVSL3004";
    case SurfaceLayoutError::outsideVisibleShape:
        return "LVSL3005";
    case SurfaceLayoutError::fullyClipped:
        return "LVSL3006";
    case SurfaceLayoutError::invalidHierarchy:
        return "LVSL3007";
    case SurfaceLayoutError::unreachablePlacement:
        return "LVSL3008";
    case SurfaceLayoutError::missingActivation:
        return "LVSL3009";
    case SurfaceLayoutError::missingNavigation:
        return "LVSL3010";
    case SurfaceLayoutError::missingText:
        return "LVSL3011";
    case SurfaceLayoutError::popupCannotFit:
        return "LVSL3012";
    }
    return "LVSL3000";
}

namespace detail
{

constexpr bool surfaceCoordinate(std::int64_t value) noexcept
{
    return value >= std::numeric_limits<std::int32_t>::min() &&
           value <= std::numeric_limits<std::int32_t>::max();
}

constexpr bool surfaceRectangle(Rect rectangle) noexcept
{
    const std::int64_t x = rectangle.x;
    const std::int64_t y = rectangle.y;
    return !rectangle.empty() && surfaceCoordinate(x + rectangle.width) &&
           surfaceCoordinate(y + rectangle.height);
}

constexpr bool surfacePolicy(SurfacePlacementPolicy policy) noexcept
{
    return policy == SurfacePlacementPolicy::safeArea ||
           policy == SurfacePlacementPolicy::visibleShape ||
           policy == SurfacePlacementPolicy::clipToViewport;
}

// Both rectangles have already passed checked exclusive-edge arithmetic.
constexpr bool surfaceContains(Rect outer, Rect inner) noexcept
{
    return inner.x >= outer.x && inner.y >= outer.y && inner.right() <= outer.right() &&
           inner.bottom() <= outer.bottom();
}

constexpr bool surfaceContains(Viewport viewport, Rect rectangle) noexcept
{
    return viewport.contains({rectangle.x, rectangle.y}) &&
           viewport.contains({rectangle.right() - 1, rectangle.y}) &&
           viewport.contains({rectangle.x, rectangle.bottom() - 1}) &&
           viewport.contains({rectangle.right() - 1, rectangle.bottom() - 1});
}

// A circle's closest pixel center proves existence of a visible sample. This
// also handles a rectangle surrounding the circle with all four corners out.
constexpr bool surfaceIntersects(Viewport viewport, Rect rectangle) noexcept
{
    const Point nearest{
        std::clamp(
            viewport.bounds.x + viewport.bounds.width / 2, rectangle.x, rectangle.right() - 1),
        std::clamp(
            viewport.bounds.y + viewport.bounds.height / 2, rectangle.y, rectangle.bottom() - 1)};
    return viewport.contains(nearest);
}

constexpr std::expected<void, SurfaceLayoutError> surfacePlacement(
    const DisplayDefinition& definition, Rect rectangle, SurfacePlacementPolicy policy) noexcept
{
    if (!surfacePolicy(policy))
    {
        return std::unexpected{SurfaceLayoutError::invalidPolicy};
    }
    if (!surfaceRectangle(rectangle))
    {
        return std::unexpected{SurfaceLayoutError::invalidGeometry};
    }
    const Viewport viewport{{0, 0, definition.logicalSize.width, definition.logicalSize.height},
                            definition.shape};
    if (policy == SurfacePlacementPolicy::safeArea &&
        !surfaceContains(definition.safeArea, rectangle))
    {
        return std::unexpected{SurfaceLayoutError::outsideSafeArea};
    }
    if (policy == SurfacePlacementPolicy::visibleShape && !surfaceContains(viewport, rectangle))
    {
        return std::unexpected{SurfaceLayoutError::outsideVisibleShape};
    }
    if (policy == SurfacePlacementPolicy::clipToViewport && !surfaceIntersects(viewport, rectangle))
    {
        return std::unexpected{SurfaceLayoutError::fullyClipped};
    }
    return {};
}

} // namespace detail

// Validation never moves or rescales a supplied ordinary control. Destination
// writer selection and MCU budgets do not participate in surface compatibility.
[[nodiscard]] constexpr std::expected<void, SurfaceLayoutError>
validateSurfacePlacement(const DisplayDefinition& definition,
                         Rect rectangle,
                         SurfacePlacementPolicy policy = SurfacePlacementPolicy::safeArea) noexcept
{
    if (!validateDisplayDefinition(definition))
    {
        return std::unexpected{SurfaceLayoutError::invalidDefinition};
    }
    return detail::surfacePlacement(definition, rectangle, policy);
}

[[nodiscard]] constexpr std::expected<void, SurfaceLayoutError>
validateSurfaceInput(LogicalInputConfiguration available,
                     SurfaceInputRequirements required) noexcept
{
    if (required.activation && !(available.pointer || available.keys || available.select))
    {
        return std::unexpected{SurfaceLayoutError::missingActivation};
    }
    if (required.navigation && !(available.pointer || available.keys || available.encoder))
    {
        return std::unexpected{SurfaceLayoutError::missingNavigation};
    }
    if (required.text && !available.text)
    {
        return std::unexpected{SurfaceLayoutError::missingText};
    }
    return {};
}

enum class SurfacePopupFallback : std::uint8_t
{
    none,
    centeredSafeArea
};

struct SurfacePopupPlacement
{
    Rect frame{};
    bool usedFallback{false};
};

[[nodiscard]] constexpr std::expected<SurfacePopupPlacement, SurfaceLayoutError>
placeSurfacePopup(const DisplayDefinition& definition,
                  Rect anchored,
                  SurfacePopupFallback fallback = SurfacePopupFallback::none,
                  std::optional<Size> centeredFallbackSize = std::nullopt) noexcept
{
    if (!validateDisplayDefinition(definition))
    {
        return std::unexpected{SurfaceLayoutError::invalidDefinition};
    }
    if ((fallback != SurfacePopupFallback::none &&
         fallback != SurfacePopupFallback::centeredSafeArea) ||
        (fallback == SurfacePopupFallback::none && centeredFallbackSize))
    {
        return std::unexpected{SurfaceLayoutError::invalidPolicy};
    }
    if (!detail::surfaceRectangle(anchored) ||
        (centeredFallbackSize &&
         (centeredFallbackSize->width <= 0 || centeredFallbackSize->height <= 0)))
    {
        return std::unexpected{SurfaceLayoutError::invalidGeometry};
    }
    const Rect safe = definition.safeArea;
    if (detail::surfaceContains(safe, anchored))
    {
        return SurfacePopupPlacement{anchored, false};
    }
    const Size fallbackSize = centeredFallbackSize.value_or(Size{anchored.width, anchored.height});
    if (fallback == SurfacePopupFallback::none || fallbackSize.width > safe.width ||
        fallbackSize.height > safe.height)
    {
        return std::unexpected{SurfaceLayoutError::popupCannotFit};
    }
    return SurfacePopupPlacement{{safe.x + (safe.width - fallbackSize.width) / 2,
                                  safe.y + (safe.height - fallbackSize.height) / 2,
                                  fallbackSize.width,
                                  fallbackSize.height},
                                 true};
}

// A minimal borrowed-layout projection. Frames are parent-relative. The caller
// selects policy from its widget/application semantics; root/backdrop frames
// normally use clipToViewport, ordinary controls use safeArea. Hidden/disabled
// nodes retain their geometry obligations because those states are mutable.
struct SurfaceLayoutNode
{
    std::optional<std::size_t> parent{};
    Rect frame{};
    SurfacePlacementPolicy placement{SurfacePlacementPolicy::safeArea};
    bool scrollable{false};
    std::int32_t contentHeight{0};
};

struct SurfaceLayoutFailure
{
    SurfaceLayoutError error{SurfaceLayoutError::invalidHierarchy};
    std::size_t nodeIndex{0U};
};

namespace detail
{

template <typename NodeAt>
constexpr std::int64_t
surfaceContentHeight(std::size_t index, std::size_t count, const NodeAt& nodeAt) noexcept
{
    const SurfaceLayoutNode parent = nodeAt(index);
    std::int64_t height = std::max(parent.frame.height, parent.contentHeight);
    for (std::size_t child = index + 1U; child < count; ++child)
    {
        const SurfaceLayoutNode candidate = nodeAt(child);
        if (candidate.parent == index)
        {
            height = std::max(height, std::int64_t{candidate.frame.bottom()});
        }
    }
    return height;
}

template <typename NodeAt>
constexpr std::expected<void, SurfaceLayoutError>
surfaceNodePlacement(const DisplayDefinition& definition,
                     std::size_t index,
                     std::size_t count,
                     const NodeAt& nodeAt) noexcept
{
    const SurfaceLayoutNode node = nodeAt(index);
    const bool clipped = node.placement == SurfacePlacementPolicy::clipToViewport;
    const std::int64_t width = node.frame.width;
    const std::int64_t height = node.frame.height;
    std::int64_t left = node.frame.x;
    std::int64_t right = clipped ? std::int64_t{node.frame.right()} - 1 : left;
    std::int64_t low = node.frame.y;
    std::int64_t high = clipped ? std::int64_t{node.frame.bottom()} - 1 : low;
    auto parentIndex = node.parent;
    while (parentIndex)
    {
        const SurfaceLayoutNode parent = nodeAt(*parentIndex);
        const std::int64_t parentWidth = parent.frame.width;
        const std::int64_t parentHeight = parent.frame.height;
        const std::int64_t contentHeight =
            parent.scrollable ? surfaceContentHeight(*parentIndex, count, nodeAt) : parentHeight;
        if (clipped)
        {
            left = std::max(left, std::int64_t{0});
            right = std::min(right, parentWidth - 1);
            low = std::max(low, std::int64_t{0});
            high = std::min(high, contentHeight - 1);
        }
        else
        {
            if (left < 0 || left + width > parentWidth)
            {
                return std::unexpected{SurfaceLayoutError::unreachablePlacement};
            }
            low = std::max(low, std::int64_t{0});
            high = std::min(high, contentHeight - height);
        }
        if (left > right || low > high)
        {
            return std::unexpected{clipped ? SurfaceLayoutError::fullyClipped
                                           : SurfaceLayoutError::unreachablePlacement};
        }
        if (parent.scrollable)
        {
            low = std::max(std::int64_t{0}, low - (contentHeight - parentHeight));
            high = std::min(high, parentHeight - (clipped ? 1 : height));
            if (low > high)
            {
                return std::unexpected{clipped ? SurfaceLayoutError::fullyClipped
                                               : SurfaceLayoutError::unreachablePlacement};
            }
        }
        left += parent.frame.x;
        right += parent.frame.x;
        low += parent.frame.y;
        high += parent.frame.y;
        const std::int64_t lastX = clipped ? right + 1 : left + width;
        const std::int64_t lastY = high + (clipped ? 1 : height);
        if (!surfaceCoordinate(left) || !surfaceCoordinate(lastX) || !surfaceCoordinate(low) ||
            !surfaceCoordinate(lastY))
        {
            return std::unexpected{SurfaceLayoutError::invalidGeometry};
        }
        parentIndex = parent.parent;
    }
    if (clipped)
    {
        return surfacePlacement(definition,
                                {saturatingInt32(left),
                                 saturatingInt32(low),
                                 saturatingInt32(right - left + 1),
                                 saturatingInt32(high - low + 1)},
                                node.placement);
    }
    if (node.placement == SurfacePlacementPolicy::safeArea)
    {
        low = std::max(low, std::int64_t{definition.safeArea.y});
        high = std::min(high, std::int64_t{definition.safeArea.bottom()} - height);
        if (low > high)
        {
            return std::unexpected{SurfaceLayoutError::outsideSafeArea};
        }
    }
    // Of all reachable vertical positions this minimizes the farthest corner's
    // distance from the circle center. Rectangle viewports use the same path.
    const std::int64_t centered = (std::int64_t{definition.logicalSize.height} - height) / 2;
    const std::int64_t top = std::clamp(centered, low, high);
    return surfacePlacement(
        definition,
        {saturatingInt32(left), saturatingInt32(top), node.frame.width, node.frame.height},
        node.placement);
}

} // namespace detail

// nodeAt(index) must return a stable SurfaceLayoutNode value, without side
// effects, for 0 <= index < count. Nothing is retained. Parents precede children
// and exactly one root occupies index zero; the iterative walks cannot cycle.
// Scroll compatibility means reachable at some valid offset, not visible at
// the initial offset. Full controls must fit every ancestor viewport. Decor
// explicitly allowing clipping needs only one reachable visible pixel.
template <typename NodeAt>
[[nodiscard]] constexpr std::expected<void, SurfaceLayoutFailure> validateSurfaceLayout(
    const DisplayDefinition& definition, std::size_t count, const NodeAt& nodeAt) noexcept
{
    if (!validateDisplayDefinition(definition))
    {
        return std::unexpected{SurfaceLayoutFailure{SurfaceLayoutError::invalidDefinition, 0U}};
    }
    if (count == 0U)
    {
        return std::unexpected{SurfaceLayoutFailure{SurfaceLayoutError::invalidHierarchy, 0U}};
    }
    for (std::size_t index = 0U; index < count; ++index)
    {
        const SurfaceLayoutNode node = nodeAt(index);
        if ((index == 0U && node.parent) ||
            (index != 0U && (!node.parent || *node.parent >= index)))
        {
            return std::unexpected{
                SurfaceLayoutFailure{SurfaceLayoutError::invalidHierarchy, index}};
        }
        if (!detail::surfacePolicy(node.placement))
        {
            return std::unexpected{SurfaceLayoutFailure{SurfaceLayoutError::invalidPolicy, index}};
        }
        if (!detail::surfaceRectangle(node.frame))
        {
            return std::unexpected{
                SurfaceLayoutFailure{SurfaceLayoutError::invalidGeometry, index}};
        }
    }
    for (std::size_t index = 0U; index < count; ++index)
    {
        const auto result = detail::surfaceNodePlacement(definition, index, count, nodeAt);
        if (!result)
        {
            return std::unexpected{SurfaceLayoutFailure{result.error(), index}};
        }
    }
    return {};
}

[[nodiscard]] constexpr std::expected<void, SurfaceLayoutFailure>
validateSurfaceLayout(const DisplayDefinition& definition,
                      std::span<const SurfaceLayoutNode> nodes) noexcept
{
    return validateSurfaceLayout(definition,
                                 nodes.size(),
                                 [&nodes](std::size_t index) noexcept
                                 {
                                     return nodes[index];
                                 });
}

} // namespace lithe
