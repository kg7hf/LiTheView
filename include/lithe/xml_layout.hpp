// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/layout.hpp"

#include <cstddef>
#include <cstdint>
#include <span>
#include <string_view>

namespace lithe
{

enum class XmlLayoutError : std::uint8_t
{
    none,
    empty,
    syntax,
    capacity,
    nestingTooDeep,
    unbalancedElement,
    unsupportedElement,
    unsupportedClass,
    unsupportedAttribute,
    animationCapacity,
    stateStyleCapacity,
    missingClass,
    invalidAttribute
};

struct XmlParseOptions
{
    constexpr XmlParseOptions() noexcept = default;

    constexpr explicit XmlParseOptions(bool rejectUnknownAttributes,
                                       std::span<std::string_view> animationFrames = {},
                                       std::span<WidgetStateStyle> stateStyles = {}) noexcept
        : rejectUnknownAttributes{rejectUnknownAttributes}, animationFrames{animationFrames},
          stateStyles{stateStyles}
    {
    }

    bool rejectUnknownAttributes{false};
    std::span<std::string_view> animationFrames;
    // A parsed Style borrows an entry at the matching ViewSpec index when XML
    // contains explicit state colors. This storage must outlive the layout.
    std::span<WidgetStateStyle> stateStyles;
};

struct XmlLayoutResult
{
    XmlLayoutError error{XmlLayoutError::none};
    std::size_t offset{0};
    std::size_t nodeCount{0};
    std::size_t animationFrameCount{0};
    constexpr bool ok() const noexcept
    {
        return error == XmlLayoutError::none;
    }
    constexpr explicit operator bool() const noexcept
    {
        return ok();
    }
};

// Parses the legacy LCDViewGUI object dialect directly from caller-owned memory.
// The parser performs no allocation and no I/O. String fields in ViewSpec refer
// to the input XML, so the XML storage must outlive the built layout.
class XmlLayoutParser
{
public:
    static XmlLayoutResult
    parse(std::string_view xml, std::span<ViewSpec> output, XmlParseOptions options = {}) noexcept;
};

} // namespace lithe
