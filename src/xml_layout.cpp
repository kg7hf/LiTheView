// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/xml_layout.hpp"

#include "lithe/detail/number.hpp"

#include <array>
#include <bit>
#include <concepts>
#include <cstdint>
#include <limits>
#include <optional>

namespace lithe
{

namespace
{

constexpr std::size_t kMaxDepth = 32U;
constexpr std::uint16_t kMinimumTabHeight = 8U;
constexpr std::uint16_t kMaximumTabHeight = 255U;
constexpr std::uint8_t kMinimumWheelRowHeight = 12U;

bool isNameStart(char value) noexcept
{
    return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') || value == '_';
}

bool isNameChar(char value) noexcept
{
    return isNameStart(value) || (value >= '0' && value <= '9') || value == '-' || value == ':';
}

void skipWhitespace(std::string_view input, std::size_t& offset) noexcept
{
    while (offset < input.size())
    {
        const char value = input[offset];
        if (value != ' ' && value != '\t' && value != '\r' && value != '\n')
        {
            break;
        }
        ++offset;
    }
}

bool consume(std::string_view input, std::size_t& offset, std::string_view token) noexcept
{
    if (offset > input.size() || token.size() > input.size() - offset ||
        input.substr(offset, token.size()) != token)
    {
        return false;
    }
    offset += token.size();
    return true;
}

bool parseName(std::string_view input, std::size_t& offset, std::string_view& name) noexcept
{
    if (offset >= input.size() || !isNameStart(input[offset]))
    {
        return false;
    }
    const std::size_t start = offset++;
    while (offset < input.size() && isNameChar(input[offset]))
    {
        ++offset;
    }
    name = input.substr(start, offset - start);
    return true;
}

template <std::unsigned_integral Integer>
bool parseUnsigned(std::string_view input, Integer& result) noexcept
{
    if (input.empty())
    {
        return false;
    }
    if (input.front() == '+')
    {
        input.remove_prefix(1U);
    }
    std::uint8_t base = 10U;
    if (input.size() > 2U && input[0] == '0' && (input[1] == 'x' || input[1] == 'X'))
    {
        base = 16U;
        input.remove_prefix(2U);
    }
    if (input.empty())
    {
        return false;
    }
    return detail::parseUnsigned(input, result, base);
}

bool parseInteger(std::string_view input, std::int32_t& result) noexcept
{
    if (input.empty())
    {
        return false;
    }
    if (input.front() == '+')
    {
        input.remove_prefix(1U);
    }
    if (input.empty())
    {
        return false;
    }
    const bool negative = input.front() == '-';
    if (negative)
    {
        input.remove_prefix(1U);
    }
    std::uint8_t base = 10U;
    if (input.size() > 2U && input[0] == '0' && (input[1] == 'x' || input[1] == 'X'))
    {
        base = 16U;
        input.remove_prefix(2U);
    }
    if (input.empty())
    {
        return false;
    }
    return detail::parseSignedMagnitude(input, negative, result, base);
}

bool parseBool(std::string_view value, bool& result) noexcept
{
    if (value == "1" || value == "true" || value == "TRUE")
    {
        result = true;
        return true;
    }
    if (value == "0" || value == "false" || value == "FALSE")
    {
        result = false;
        return true;
    }
    return false;
}

bool animationImageIndex(std::string_view name, std::size_t& index) noexcept
{
    constexpr std::string_view prefix = "animationImage";
    if (name.size() <= prefix.size() || name.substr(0U, prefix.size()) != prefix)
    {
        return false;
    }
    return parseUnsigned(name.substr(prefix.size()), index);
}

bool parseFrame(std::string_view value, Rect& frame) noexcept
{
    std::array<std::int32_t, 4> fields{};
    std::size_t start = 0U;
    for (std::size_t index = 0; index < 4U; ++index)
    {
        while (start < value.size() && (value[start] == ' ' || value[start] == '\t'))
        {
            ++start;
        }
        const std::size_t comma = index == 3U ? value.size() : value.find(',', start);
        if (comma == std::string_view::npos)
        {
            return false;
        }
        std::size_t end = comma;
        while (end > start && (value[end - 1U] == ' ' || value[end - 1U] == '\t'))
        {
            --end;
        }
        if (!parseInteger(value.substr(start, end - start), fields[index]))
        {
            return false;
        }
        start = comma + 1U;
    }
    frame = {fields[0], fields[1], fields[2], fields[3]};
    return !frame.empty();
}

bool parseLegacyColor(std::string_view value, Color& color) noexcept
{
    std::uint32_t encoded = 0U;
    if (!value.empty() && value[0] == '-')
    {
        std::int32_t signedValue = 0;
        if (!parseInteger(value, signedValue))
        {
            return false;
        }
        // LITHEVIEW_CAST_BOUNDARY: legacy XML colors use the signed value's raw 32-bit pattern.
        encoded = std::bit_cast<std::uint32_t>(signedValue);
    }
    else if (!parseUnsigned(value, encoded))
    {
        return false;
    }
    color = Color::fromLegacyValue(encoded);
    return true;
}

using StateColorMember = Color WidgetStateStyle::*;

std::optional<StateColorMember> stateColorMember(std::string_view name) noexcept
{
    if (name == "focusedBackgroundColor")
    {
        return &WidgetStateStyle::focusedBackground;
    }
    if (name == "focusedForegroundColor")
    {
        return &WidgetStateStyle::focusedForeground;
    }
    if (name == "pressedBackgroundColor")
    {
        return &WidgetStateStyle::pressedBackground;
    }
    if (name == "pressedForegroundColor")
    {
        return &WidgetStateStyle::pressedForeground;
    }
    if (name == "disabledBackgroundColor")
    {
        return &WidgetStateStyle::disabledBackground;
    }
    if (name == "disabledForegroundColor")
    {
        return &WidgetStateStyle::disabledForeground;
    }
    if (name == "selectedBackgroundColor")
    {
        return &WidgetStateStyle::selectedBackground;
    }
    if (name == "selectedForegroundColor")
    {
        return &WidgetStateStyle::selectedForeground;
    }
    return std::nullopt;
}

// State colors are stored in the caller-owned entry for this node. The reserved
// omitted-state value is rejected so an explicit attribute always takes effect.
bool applyStateColor(std::string_view value,
                     StateColorMember member,
                     ViewSpec& spec,
                     WidgetStateStyle* stateStyle,
                     XmlLayoutError& error) noexcept
{
    if (stateStyle == nullptr)
    {
        error = XmlLayoutError::stateStyleCapacity;
        return false;
    }
    Color& color = stateStyle->*member;
    if (!parseLegacyColor(value, color) || !isSpecifiedStyleStateColor(color))
    {
        error = XmlLayoutError::invalidAttribute;
        return false;
    }
    spec.style.states = stateStyle;
    return true;
}

bool parseButtonBehavior(std::string_view value, ButtonBehavior& behavior) noexcept
{
    if (value == "momentary")
    {
        behavior = ButtonBehavior::momentary;
        return true;
    }
    if (value == "toggle")
    {
        behavior = ButtonBehavior::toggle;
        return true;
    }
    return false;
}

// Strict documents may not carry state that the node kind cannot represent:
// only a toggle button latches, and only buttons have a behavior.
bool strictButtonStateValid(const ViewSpec& spec) noexcept
{
    if (spec.kind != ViewKind::button)
    {
        return spec.buttonBehavior == ButtonBehavior::momentary;
    }
    return !spec.checked || spec.buttonBehavior == ButtonBehavior::toggle;
}

std::size_t countDelimitedItems(std::string_view titles) noexcept
{
    if (titles.empty())
    {
        return 0U;
    }
    std::size_t count = 1U;
    for (const char character : titles)
    {
        if (character == '|')
        {
            ++count;
        }
    }
    return count;
}

// Strict tab widgets name one through TabBarView::kMaximumTabs tabs and select
// one of them; only a tab view has a strip height.
bool strictTabStateValid(const ViewSpec& spec) noexcept
{
    if (spec.kind != ViewKind::tabBar && spec.kind != ViewKind::tabView)
    {
        return spec.tabHeight == detail::kDefaultTabHeightPixels;
    }
    const std::size_t count = countDelimitedItems(spec.text);
    return count != 0U && count <= TabBarView::kMaximumTabs && spec.selectedIndex < count &&
           (spec.kind == ViewKind::tabView || spec.tabHeight == detail::kDefaultTabHeightPixels);
}

// Wheel pickers and drop-downs require a selected item. A list may be empty until
// its application supplies borrowed rows; all three expose a row height.
bool strictWheelStateValid(const ViewSpec& spec) noexcept
{
    if (spec.kind != ViewKind::wheelPicker && spec.kind != ViewKind::dropDown && spec.kind != ViewKind::list)
    {
        return spec.rowHeight == detail::kDefaultWheelRowHeightPixels;
    }
    const std::size_t count = countDelimitedItems(spec.text);
    if (spec.kind == ViewKind::list)
    {
        return count <= ListView::kMaximumItems &&
               (count == 0U ? spec.selectedIndex == 0U : spec.selectedIndex < count);
    }
    return count != 0U && count <= WheelPickerView::kMaximumItems && spec.selectedIndex < count;
}

bool setKind(std::string_view className, ViewSpec& spec) noexcept
{
    if (className == "LCDVIEW")
    {
        spec.kind = ViewKind::panel;
    }
    else if (className == "LCDSCROLLVIEW")
    {
        spec.kind = ViewKind::scroll;
    }
    else if (className == "LCDLISTVIEW")
    {
        spec.kind = ViewKind::list;
    }
    else if (className == "LCDPLOTVIEW")
    {
        spec.kind = ViewKind::plot;
    }
    else if (className == "LCDWATERFALLVIEW")
    {
        spec.kind = ViewKind::waterfall;
    }
    else if (className == "LCDAXISVIEW")
    {
        spec.kind = ViewKind::axis;
    }
    else if (className == "LCDMARKERRANGEVIEW")
    {
        spec.kind = ViewKind::markerRange;
    }
    else if (className == "LCDMETERVIEW")
    {
        spec.kind = ViewKind::meter;
    }
    else if (className == "LCDTABLEVIEW")
    {
        spec.kind = ViewKind::table;
    }
    else if (className == "LCDLABELVIEW" || className == "LCDTEXTVIEW")
    {
        spec.kind = ViewKind::label;
    }
    else if (className == "LCDBUTTONVIEW")
    {
        spec.kind = ViewKind::button;
    }
    else if (className == "LCDPROGRESSVIEW")
    {
        spec.kind = ViewKind::progress;
    }
    else if (className == "LCDIMAGEVIEW")
    {
        spec.kind = ViewKind::image;
    }
    else if (className == "LCDICONVIEW")
    {
        spec.kind = ViewKind::icon;
    }
    else if (className == "LCDLINEVIEW")
    {
        spec.kind = ViewKind::line;
    }
    else if (className == "LCDPICKERVIEW")
    {
        spec.kind = ViewKind::picker;
    }
    else if (className == "LCDCHECKBOXVIEW")
    {
        spec.kind = ViewKind::checkbox;
    }
    else if (className == "LCDRADIOBUTTONVIEW")
    {
        spec.kind = ViewKind::radio;
    }
    else if (className == "LCDTEXTFIELDVIEW")
    {
        spec.kind = ViewKind::textField;
    }
    else if (className == "LCDSOFTKEYVIEW")
    {
        spec.kind = ViewKind::softkey;
    }
    else if (className == "LCDTABBARVIEW")
    {
        spec.kind = ViewKind::tabBar;
    }
    else if (className == "LCDTABVIEW")
    {
        spec.kind = ViewKind::tabView;
    }
    else if (className == "LCDDROPDOWNVIEW")
    {
        spec.kind = ViewKind::dropDown;
    }
    else if (className == "LCDWHEELPICKERVIEW")
    {
        spec.kind = ViewKind::wheelPicker;
    }
    else
    {
        return false;
    }
    return true;
}

bool setElementKind(std::string_view element, ViewSpec& spec) noexcept
{
    if (element == "view" || element == "panel")
    {
        spec.kind = ViewKind::panel;
    }
    else if (element == "label")
    {
        spec.kind = ViewKind::label;
    }
    else if (element == "button")
    {
        spec.kind = ViewKind::button;
    }
    else if (element == "progress")
    {
        spec.kind = ViewKind::progress;
    }
    else if (element == "image")
    {
        spec.kind = ViewKind::image;
    }
    else if (element == "icon")
    {
        spec.kind = ViewKind::icon;
    }
    else if (element == "line")
    {
        spec.kind = ViewKind::line;
    }
    else if (element == "scroll")
    {
        spec.kind = ViewKind::scroll;
    }
    else if (element == "list")
    {
        spec.kind = ViewKind::list;
    }
    else if (element == "plot")
    {
        spec.kind = ViewKind::plot;
    }
    else if (element == "waterfall")
    {
        spec.kind = ViewKind::waterfall;
    }
    else if (element == "axis")
    {
        spec.kind = ViewKind::axis;
    }
    else if (element == "marker-range")
    {
        spec.kind = ViewKind::markerRange;
    }
    else if (element == "meter")
    {
        spec.kind = ViewKind::meter;
    }
    else if (element == "table")
    {
        spec.kind = ViewKind::table;
    }
    else if (element == "picker")
    {
        spec.kind = ViewKind::picker;
    }
    else if (element == "checkbox")
    {
        spec.kind = ViewKind::checkbox;
    }
    else if (element == "radio")
    {
        spec.kind = ViewKind::radio;
    }
    else if (element == "textfield" || element == "text-field")
    {
        spec.kind = ViewKind::textField;
    }
    else if (element == "softkey")
    {
        spec.kind = ViewKind::softkey;
    }
    else if (element == "tabbar" || element == "tab-bar")
    {
        spec.kind = ViewKind::tabBar;
    }
    else if (element == "tabview" || element == "tab-view")
    {
        spec.kind = ViewKind::tabView;
    }
    else if (element == "dropdown" || element == "drop-down")
    {
        spec.kind = ViewKind::dropDown;
    }
    else if (element == "wheelpicker" || element == "wheel-picker")
    {
        spec.kind = ViewKind::wheelPicker;
    }
    else
    {
        return false;
    }
    return true;
}

bool applyAttribute(std::string_view name,
                    std::string_view value,
                    ViewSpec& spec,
                    WidgetStateStyle* stateStyle,
                    bool& classSeen,
                    bool rejectUnknown,
                    XmlLayoutError& error) noexcept
{
    bool boolean = false;
    if (name == "className")
    {
        classSeen = true;
        if (!setKind(value, spec))
        {
            error = XmlLayoutError::unsupportedClass;
            return false;
        }
    }
    else if (name == "frame")
    {
        if (!parseFrame(value, spec.frame))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "runtimeID" || name == "id")
    {
        if (!parseUnsigned(value, spec.id))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "backgroundColor")
    {
        if (!parseLegacyColor(value, spec.style.background))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "textColor" || name == "foregroundColor")
    {
        if (!parseLegacyColor(value, spec.style.foreground))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "accentColor")
    {
        if (!parseLegacyColor(value, spec.style.accent))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "borderColor")
    {
        if (!parseLegacyColor(value, spec.style.border))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (const auto member = stateColorMember(name))
    {
        if (!applyStateColor(value, *member, spec, stateStyle, error))
        {
            return false;
        }
    }
    else if (name == "behavior")
    {
        if (!parseButtonBehavior(value, spec.buttonBehavior))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "textAlignmentFlags")
    {
        std::uint8_t flags = 0U;
        if (!parseUnsigned(value, flags) || flags > 15U)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        switch (flags & 3U)
        {
        case 1U:
            spec.style.textAlign.horizontal = HorizontalAlign::right;
            break;
        case 2U:
            spec.style.textAlign.horizontal = HorizontalAlign::center;
            break;
        default:
            spec.style.textAlign.horizontal = HorizontalAlign::left;
            break;
        }
        switch ((flags >> 2U) & 3U)
        {
        case 1U:
            spec.style.textAlign.vertical = VerticalAlign::bottom;
            break;
        case 3U:
            spec.style.textAlign.vertical = VerticalAlign::center;
            break;
        default:
            spec.style.textAlign.vertical = VerticalAlign::top;
            break;
        }
    }
    else if (name == "textScale")
    {
        if (!parseUnsigned(value, spec.style.textScale) || spec.style.textScale < 1U ||
            spec.style.textScale > 8U)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "borderWidth")
    {
        if (!parseUnsigned(value, spec.style.borderWidth) || spec.style.borderWidth > 16U)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "text" || name == "items" || name == "left_text" || name == "leftText")
    {
        spec.text = value;
    }
    else if (name == "detail" || name == "right_text" || name == "rightText" ||
             name == "placeholder")
    {
        spec.detail = value;
    }
    else if (name == "imageName" || name == "resource" || name == "binding" || name == "icon")
    {
        spec.resource = value;
    }
    else if (name == "font")
    {
        spec.font = value;
    }
    else if (name == "value")
    {
        if (!parseInteger(value, spec.value) || spec.value < 0 || spec.value > 100)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "animationInterval")
    {
        if (!parseUnsigned(value, spec.animationIntervalMs) || spec.animationIntervalMs < 1U)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "repeatIndex")
    {
        if (!parseUnsigned(value, spec.animationRepeatIndex))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "autoplay")
    {
        if (!parseBool(value, boolean))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        spec.animationAutoplay = boolean;
    }
    else if (name == "leftAction")
    {
        if (!parseUnsigned(value, spec.leftAction))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "rightAction")
    {
        if (!parseUnsigned(value, spec.rightAction))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "contentHeight")
    {
        if (!parseInteger(value, spec.contentHeight) || spec.contentHeight < 0)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "scrollOffset")
    {
        if (!parseInteger(value, spec.scrollOffset) || spec.scrollOffset < 0)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "scrollStep")
    {
        if (!parseInteger(value, spec.scrollStep) || spec.scrollStep < 1)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "selectedIndex")
    {
        if (!parseUnsigned(value, spec.selectedIndex) || spec.selectedIndex > 255U)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "tabHeight")
    {
        if (!parseUnsigned(value, spec.tabHeight) || spec.tabHeight < kMinimumTabHeight ||
            spec.tabHeight > kMaximumTabHeight)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "rowHeight")
    {
        if (!parseUnsigned(value, spec.rowHeight) || spec.rowHeight < kMinimumWheelRowHeight)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "checked" || name == "selected")
    {
        if (!parseBool(value, boolean))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        spec.checked = boolean;
    }
    else if (name == "maxLength")
    {
        if (!parseUnsigned(value, spec.maxLength) || spec.maxLength < 1U)
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
    }
    else if (name == "wrap")
    {
        if (!parseBool(value, boolean))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        spec.wraps = boolean;
    }
    else if (name == "showsIndicator")
    {
        if (!parseBool(value, boolean))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        spec.showsIndicator = boolean;
    }
    else if (name == "hide")
    {
        if (!parseBool(value, boolean))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        spec.visible = !boolean;
    }
    else if (name == "enabled")
    {
        if (!parseBool(value, boolean))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        spec.enabled = boolean;
    }
    else if (name == "canFocus")
    {
        if (!parseBool(value, boolean))
        {
            error = XmlLayoutError::invalidAttribute;
            return false;
        }
        spec.focusable = boolean ? 1 : 0;
    }
    else if (rejectUnknown)
    {
        error = XmlLayoutError::unsupportedAttribute;
        return false;
    }
    // Unknown attributes are deliberately ignored for legacy XML compatibility
    // (wireframeID, textids and product-specific metadata).
    return true;
}

} // namespace

XmlLayoutResult XmlLayoutParser::parse(std::string_view xml,
                                       std::span<ViewSpec> output,
                                       XmlParseOptions options) noexcept
{
    if (xml.empty() || output.empty())
    {
        return {XmlLayoutError::empty, 0U, 0U};
    }

    std::array<std::uint16_t, kMaxDepth> parents{};
    std::array<std::string_view, kMaxDepth> elements{};
    std::size_t depth = 0U;
    std::size_t count = 0U;
    std::size_t animationFrameCount = 0U;
    std::size_t offset = 0U;

    while (offset < xml.size())
    {
        skipWhitespace(xml, offset);
        if (offset == xml.size())
        {
            break;
        }
        if (consume(xml, offset, "<!--"))
        {
            const std::size_t end = xml.find("-->", offset);
            if (end == std::string_view::npos)
            {
                return {XmlLayoutError::syntax, offset, count};
            }
            offset = end + 3U;
            continue;
        }
        if (consume(xml, offset, "<?"))
        {
            const std::size_t end = xml.find("?>", offset);
            if (end == std::string_view::npos)
            {
                return {XmlLayoutError::syntax, offset, count};
            }
            offset = end + 2U;
            continue;
        }
        if (!consume(xml, offset, "<"))
        {
            return {XmlLayoutError::syntax, offset, count};
        }
        if (consume(xml, offset, "/"))
        {
            std::string_view closingName;
            if (!parseName(xml, offset, closingName))
            {
                return {XmlLayoutError::syntax, offset, count};
            }
            skipWhitespace(xml, offset);
            if (!consume(xml, offset, ">") || depth == 0U || elements[depth - 1U] != closingName)
            {
                return {XmlLayoutError::unbalancedElement, offset, count};
            }
            --depth;
            continue;
        }

        if (count >= output.size())
        {
            return {XmlLayoutError::capacity, offset, count};
        }
        std::string_view element;
        if (!parseName(xml, offset, element))
        {
            return {XmlLayoutError::syntax, offset, count};
        }
        ViewSpec spec{};
        spec.xmlEncodedText = true;
        if (depth != 0U)
        {
            spec.parent = parents[depth - 1U];
        }
        bool classSeen = element != "object";
        if (element != "object" && !setElementKind(element, spec))
        {
            return {XmlLayoutError::unsupportedElement, offset, count};
        }

        bool selfClosing = false;
        for (;;)
        {
            skipWhitespace(xml, offset);
            if (consume(xml, offset, "/>"))
            {
                selfClosing = true;
                break;
            }
            if (consume(xml, offset, ">"))
            {
                break;
            }
            std::string_view name;
            if (!parseName(xml, offset, name))
            {
                return {XmlLayoutError::syntax, offset, count};
            }
            skipWhitespace(xml, offset);
            if (!consume(xml, offset, "="))
            {
                return {XmlLayoutError::syntax, offset, count};
            }
            skipWhitespace(xml, offset);
            if (offset >= xml.size() || (xml[offset] != '\'' && xml[offset] != '"'))
            {
                return {XmlLayoutError::syntax, offset, count};
            }
            const char quote = xml[offset++];
            const std::size_t valueStart = offset;
            const std::size_t valueEnd = xml.find(quote, valueStart);
            if (valueEnd == std::string_view::npos)
            {
                return {XmlLayoutError::syntax, offset, count};
            }
            const std::string_view value = xml.substr(valueStart, valueEnd - valueStart);
            offset = valueEnd + 1U;
            std::size_t animationIndex = 0U;
            if (animationImageIndex(name, animationIndex))
            {
                if (animationIndex != spec.animationFrameCount)
                {
                    return {
                        XmlLayoutError::invalidAttribute, valueStart, count, animationFrameCount};
                }
                if (animationFrameCount >= options.animationFrames.size())
                {
                    return {
                        XmlLayoutError::animationCapacity, valueStart, count, animationFrameCount};
                }
                if (spec.animationFrameCount == 0U)
                {
                    if (!detail::assignIfRepresentable(animationFrameCount,
                                                       spec.animationFrameOffset))
                    {
                        return {XmlLayoutError::animationCapacity,
                                valueStart,
                                count,
                                animationFrameCount};
                    }
                }
                options.animationFrames[animationFrameCount++] = value;
                ++spec.animationFrameCount;
                continue;
            }
            XmlLayoutError attributeError = XmlLayoutError::none;
            WidgetStateStyle* stateStyle =
                count < options.stateStyles.size() ? &options.stateStyles[count] : nullptr;
            if (!applyAttribute(name,
                                value,
                                spec,
                                stateStyle,
                                classSeen,
                                options.rejectUnknownAttributes,
                                attributeError))
            {
                return {attributeError, valueStart, count};
            }
        }

        if (!classSeen)
        {
            return {XmlLayoutError::missingClass, offset, count};
        }
        if (options.rejectUnknownAttributes &&
            (!strictButtonStateValid(spec) || !strictTabStateValid(spec) ||
             !strictWheelStateValid(spec)))
        {
            return {XmlLayoutError::invalidAttribute, offset, count};
        }
        if (spec.animationFrameCount > 0U)
        {
            if (spec.kind != ViewKind::image ||
                spec.animationRepeatIndex >= spec.animationFrameCount)
            {
                return {XmlLayoutError::invalidAttribute, offset, count, animationFrameCount};
            }
            spec.kind = ViewKind::animatedImage;
        }
        output[count] = spec;
        if (!selfClosing)
        {
            if (depth >= parents.size())
            {
                return {XmlLayoutError::nestingTooDeep, offset, count};
            }
            if (!detail::assignIfRepresentable(count, parents[depth]))
            {
                return {XmlLayoutError::capacity, offset, count};
            }
            ++depth;
            elements[depth - 1U] = element;
        }
        ++count;
    }

    if (depth != 0U)
    {
        return {XmlLayoutError::unbalancedElement, offset, count};
    }
    return count == 0U ? XmlLayoutResult{XmlLayoutError::empty, 0U, 0U, animationFrameCount}
                       : XmlLayoutResult{XmlLayoutError::none, offset, count, animationFrameCount};
}

} // namespace lithe
