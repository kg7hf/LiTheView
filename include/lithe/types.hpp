// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/detail/arithmetic.hpp"
#include "lithe/detail/representation.hpp"

#include <cstdint>

namespace lithe
{

namespace detail
{

inline constexpr std::uint32_t kRgb565RedShift{11U};
inline constexpr std::uint32_t kRgb565GreenShift{5U};
inline constexpr std::uint32_t kRgb565RedBlueMask{0x1fU};
inline constexpr std::uint32_t kRgb565GreenMask{0x3fU};
inline constexpr std::uint32_t kRgb565RedBlueMaximum{31U};
inline constexpr std::uint32_t kRgb565GreenMaximum{63U};
inline constexpr std::uint16_t kRgb565RedStorageMask{0xf8U};
inline constexpr std::uint16_t kRgb565GreenStorageMask{0xfcU};
inline constexpr std::uint32_t kRgb565RedStorageShift{8U};
inline constexpr std::uint32_t kRgb565GreenStorageShift{3U};
inline constexpr std::uint32_t kRgb565BlueStorageShift{3U};
inline constexpr std::uint32_t kDefaultAccentRgb{0x2d6cdfU};
inline constexpr std::uint32_t kDefaultPressedBackgroundRgb{0x2457b3U};
inline constexpr std::uint32_t kDefaultDisabledBackgroundRgb{0xd0d4dcU};
inline constexpr std::uint32_t kDefaultDisabledForegroundRgb{0x6a6f78U};
// A state color may be omitted so handwritten styles retain the original
// accent-based focus policy. This transparent chroma is reserved for that
// internal "not specified" state; ordinary transparent black remains a valid
// explicit state color.
inline constexpr std::uint32_t kUnspecifiedStyleStateColor{0xffc35aa5U};

} // namespace detail

using Rgb565Pixel = std::uint16_t;
// Compatibility alias for the small-target default pixel storage.
using Pixel = Rgb565Pixel;
using ViewId = std::uint16_t;

struct Point
{
    std::int32_t x{0};
    std::int32_t y{0};
};

struct Size
{
    std::int32_t width{0};
    std::int32_t height{0};
};

struct Rect
{
    std::int32_t x{0};
    std::int32_t y{0};
    std::int32_t width{0};
    std::int32_t height{0};

    constexpr bool empty() const noexcept
    {
        return width <= 0 || height <= 0;
    }
    constexpr std::int32_t right() const noexcept
    {
        return detail::saturatingAdd(x, width);
    }
    constexpr std::int32_t bottom() const noexcept
    {
        return detail::saturatingAdd(y, height);
    }
    constexpr bool contains(Point point) const noexcept
    {
        return !empty() && point.x >= x && point.y >= y && point.x < right() && point.y < bottom();
    }
    constexpr Rect translated(Point offset) const noexcept
    {
        return {
            detail::saturatingAdd(x, offset.x), detail::saturatingAdd(y, offset.y), width, height};
    }
};

constexpr Rect intersect(Rect lhs, Rect rhs) noexcept
{
    const std::int32_t leftEdge = lhs.x > rhs.x ? lhs.x : rhs.x;
    const std::int32_t topEdge = lhs.y > rhs.y ? lhs.y : rhs.y;
    const std::int32_t rightEdge = lhs.right() < rhs.right() ? lhs.right() : rhs.right();
    const std::int32_t bottomEdge = lhs.bottom() < rhs.bottom() ? lhs.bottom() : rhs.bottom();
    if (rightEdge <= leftEdge || bottomEdge <= topEdge)
    {
        return {};
    }
    return {leftEdge,
            topEdge,
            detail::saturatingSubtract(rightEdge, leftEdge),
            detail::saturatingSubtract(bottomEdge, topEdge)};
}

constexpr Rect unite(Rect lhs, Rect rhs) noexcept
{
    if (lhs.empty())
    {
        return rhs;
    }
    if (rhs.empty())
    {
        return lhs;
    }
    const std::int32_t leftEdge = lhs.x < rhs.x ? lhs.x : rhs.x;
    const std::int32_t topEdge = lhs.y < rhs.y ? lhs.y : rhs.y;
    const std::int32_t rightEdge = lhs.right() > rhs.right() ? lhs.right() : rhs.right();
    const std::int32_t bottomEdge = lhs.bottom() > rhs.bottom() ? lhs.bottom() : rhs.bottom();
    return {leftEdge,
            topEdge,
            detail::saturatingSubtract(rightEdge, leftEdge),
            detail::saturatingSubtract(bottomEdge, topEdge)};
}

enum class ViewportShape : std::uint8_t
{
    rectangle,
    circle
};

// A viewport is expressed in logical display coordinates and is independent
// of the current full-frame or tiled Surface. Circular containment tests pixel
// centers in doubled integer coordinates, avoiding floating point and square
// roots in the rendering path.
struct Viewport
{
    Rect bounds{};
    ViewportShape shape{ViewportShape::rectangle};

    static constexpr Viewport rectangle(Rect viewportBounds) noexcept
    {
        return {viewportBounds, ViewportShape::rectangle};
    }

    static constexpr Viewport circle(Rect viewportBounds) noexcept
    {
        return {viewportBounds, ViewportShape::circle};
    }

    constexpr bool valid() const noexcept
    {
        return !bounds.empty() &&
               (shape == ViewportShape::rectangle || bounds.width == bounds.height);
    }

    constexpr bool contains(Point point) const noexcept
    {
        if (!valid() || !bounds.contains(point))
        {
            return false;
        }
        if (shape == ViewportShape::rectangle)
        {
            return true;
        }

        const std::int64_t doubledPointX = (std::int64_t{point.x} * 2) + 1;
        const std::int64_t doubledPointY = (std::int64_t{point.y} * 2) + 1;
        const std::int64_t doubledCenterX = (std::int64_t{bounds.x} * 2) + bounds.width;
        const std::int64_t doubledCenterY = (std::int64_t{bounds.y} * 2) + bounds.height;
        const std::int64_t radius = bounds.width;
        const std::int64_t deltaX = doubledPointX - doubledCenterX;
        const std::int64_t deltaY = doubledPointY - doubledCenterY;
        const std::int64_t radiusSquared = radius * radius;
        const std::int64_t deltaYSquared = deltaY * deltaY;
        return deltaYSquared <= radiusSquared &&
               (deltaX * deltaX) <= (radiusSquared - deltaYSquared);
    }
};

struct Color
{
    std::uint8_t red{0};
    std::uint8_t green{0};
    std::uint8_t blue{0};
    std::uint8_t alpha{detail::kMaximumChannelValue};

    static constexpr Color rgb(std::uint32_t value) noexcept
    {
        return {detail::lowByte(value >> detail::kRedByteShift),
                detail::lowByte(value >> detail::kGreenByteShift),
                detail::lowByte(value),
                detail::kMaximumChannelValue};
    }

    static constexpr Color rgba(std::uint32_t value) noexcept
    {
        return {detail::lowByte(value >> detail::kAlphaByteShift),
                detail::lowByte(value >> detail::kRedByteShift),
                detail::lowByte(value >> detail::kGreenByteShift),
                detail::lowByte(value)};
    }

    static constexpr Color argb(std::uint32_t value) noexcept
    {
        return {detail::lowByte(value >> detail::kRedByteShift),
                detail::lowByte(value >> detail::kGreenByteShift),
                detail::lowByte(value),
                detail::lowByte(value >> detail::kAlphaByteShift)};
    }

    static constexpr Color fromRgb565(Rgb565Pixel value) noexcept
    {
        return {detail::lowByte(((value >> detail::kRgb565RedShift) & detail::kRgb565RedBlueMask) *
                                detail::kMaximumChannelValue / detail::kRgb565RedBlueMaximum),
                detail::lowByte(((value >> detail::kRgb565GreenShift) & detail::kRgb565GreenMask) *
                                detail::kMaximumChannelValue / detail::kRgb565GreenMaximum),
                detail::lowByte((value & detail::kRgb565RedBlueMask) *
                                detail::kMaximumChannelValue / detail::kRgb565RedBlueMaximum),
                detail::kMaximumChannelValue};
    }

    static constexpr Color fromLegacyValue(std::uint32_t value) noexcept
    {
        return {detail::lowByte(value),
                detail::lowByte(value >> detail::kGreenByteShift),
                detail::lowByte(value >> detail::kRedByteShift),
                detail::lowByte(detail::kMaximumChannelValue -
                                detail::lowByte(value >> detail::kAlphaByteShift))};
    }

    static constexpr Color transparent() noexcept
    {
        return {0U, 0U, 0U, 0U};
    }

    constexpr Rgb565Pixel rgb565() const noexcept
    {
        Rgb565Pixel encoded = red;
        encoded &= detail::kRgb565RedStorageMask;
        encoded <<= detail::kRgb565RedStorageShift;
        Rgb565Pixel greenBits = green;
        greenBits &= detail::kRgb565GreenStorageMask;
        greenBits <<= detail::kRgb565GreenStorageShift;
        Rgb565Pixel blueBits = blue;
        blueBits >>= detail::kRgb565BlueStorageShift;
        return encoded | greenBits | blueBits;
    }

    constexpr std::uint32_t legacyValue() const noexcept
    {
        const std::uint32_t redChannel = red;
        const std::uint32_t greenChannel = green;
        const std::uint32_t blueChannel = blue;
        const std::uint32_t inverseAlpha = detail::kMaximumChannelValue - alpha;
        return redChannel | (greenChannel << detail::kGreenByteShift) |
               (blueChannel << detail::kRedByteShift) | (inverseAlpha << detail::kAlphaByteShift);
    }

    constexpr Color blendedOver(Color background) const noexcept
    {
        if (alpha == detail::kMaximumChannelValue)
        {
            return *this;
        }
        if (alpha == 0U)
        {
            return background;
        }
        const std::uint32_t foregroundWeight = alpha;
        const std::uint32_t inverseForeground = detail::kMaximumChannelValue - foregroundWeight;
        const std::uint32_t backgroundWeight =
            (background.alpha * inverseForeground) / detail::kMaximumChannelValue;
        const std::uint32_t outputAlpha = foregroundWeight + backgroundWeight;
        if (outputAlpha == 0U)
        {
            return transparent();
        }
        return {detail::lowByte((red * foregroundWeight + background.red * backgroundWeight) /
                                outputAlpha),
                detail::lowByte((green * foregroundWeight + background.green * backgroundWeight) /
                                outputAlpha),
                detail::lowByte((blue * foregroundWeight + background.blue * backgroundWeight) /
                                outputAlpha),
                detail::lowByte(outputAlpha)};
    }
};

enum class HorizontalAlign : std::uint8_t
{
    left,
    center,
    right
};
enum class VerticalAlign : std::uint8_t
{
    top,
    center,
    bottom
};

struct TextAlign
{
    HorizontalAlign horizontal{HorizontalAlign::left};
    VerticalAlign vertical{VerticalAlign::center};
};

constexpr bool isSpecifiedStyleStateColor(Color color) noexcept
{
    return color.legacyValue() != detail::kUnspecifiedStyleStateColor;
}

struct WidgetStateStyle
{
    Color focusedBackground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
    Color focusedForeground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
    Color pressedBackground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
    Color pressedForeground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
    Color disabledBackground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
    Color disabledForeground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
    // A latched toggle, checked checkbox, or selected radio button. Omitted
    // selected colors keep the normal pair, so existing styles render unchanged.
    Color selectedBackground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
    Color selectedForeground{Color::fromLegacyValue(detail::kUnspecifiedStyleStateColor)};
};

struct Style
{
    Color background{Color::transparent()};
    Color foreground{Color::rgb(detail::kRgbColorMask)};
    Color accent{Color::rgb(detail::kDefaultAccentRgb)};
    Color border{Color::transparent()};
    TextAlign textAlign{};
    std::uint8_t textScale{1};
    std::uint8_t borderWidth{0};
    // Optional state colors are borrowed. Their storage must outlive every View
    // that receives this Style.
    const WidgetStateStyle* states{nullptr};
};

// Enumerator values are stable; new states are appended.
enum class WidgetState : std::uint8_t
{
    normal,
    focused,
    pressed,
    disabled,
    selected
};

// Precedence is disabled, pressed, focused, selected, then normal. Focus wins
// over selection so keyboard and encoder users can always locate focus; a
// selected widget must therefore show its value by more than color alone.
constexpr WidgetState
resolveWidgetState(bool enabled, bool focused, bool pressed, bool selected = false) noexcept
{
    if (!enabled)
    {
        return WidgetState::disabled;
    }
    if (pressed)
    {
        return WidgetState::pressed;
    }
    if (focused)
    {
        return WidgetState::focused;
    }
    return selected ? WidgetState::selected : WidgetState::normal;
}

constexpr Style resolveWidgetStyle(Style base, WidgetState state) noexcept
{
    const WidgetStateStyle* states = base.states;
    const Color focusedBackground =
        states != nullptr && isSpecifiedStyleStateColor(states->focusedBackground)
            ? states->focusedBackground
            : base.accent;
    const Color focusedForeground =
        states != nullptr && isSpecifiedStyleStateColor(states->focusedForeground)
            ? states->focusedForeground
            : Color::rgb(detail::kRgbColorMask);
    switch (state)
    {
    case WidgetState::focused:
        base.background = focusedBackground;
        base.foreground = focusedForeground;
        break;
    case WidgetState::pressed:
        base.background = states != nullptr && isSpecifiedStyleStateColor(states->pressedBackground)
                              ? states->pressedBackground
                              : focusedBackground;
        base.foreground = states != nullptr && isSpecifiedStyleStateColor(states->pressedForeground)
                              ? states->pressedForeground
                              : focusedForeground;
        break;
    case WidgetState::disabled:
        base.background =
            states != nullptr && isSpecifiedStyleStateColor(states->disabledBackground)
                ? states->disabledBackground
                : Color::rgb(detail::kDefaultDisabledBackgroundRgb);
        base.foreground =
            states != nullptr && isSpecifiedStyleStateColor(states->disabledForeground)
                ? states->disabledForeground
                : Color::rgb(detail::kDefaultDisabledForegroundRgb);
        break;
    case WidgetState::selected:
        if (states != nullptr && isSpecifiedStyleStateColor(states->selectedBackground))
        {
            base.background = states->selectedBackground;
        }
        if (states != nullptr && isSpecifiedStyleStateColor(states->selectedForeground))
        {
            base.foreground = states->selectedForeground;
        }
        break;
    case WidgetState::normal:
    default:
        break;
    }
    return base;
}

enum class Key : std::uint8_t
{
    up,
    down,
    left,
    right,
    select,
    back
};

enum class PointerPhase : std::uint8_t
{
    pressed,
    moved,
    released,
    cancelled
};

struct PointerEvent
{
    Point position{};
    PointerPhase phase{PointerPhase::pressed};
    std::uint8_t contactId{0U};
    // Monotonic, wrapping caller time in milliseconds. A zero timestamp is
    // valid; the recognizer compares unsigned elapsed values.
    std::uint32_t timestampMs{0U};
};

} // namespace lithe
