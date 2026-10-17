// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/storage_layout.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <expected>
#include <string_view>

namespace lithe
{

enum class DisplayAddressSpace : std::uint8_t
{
    pixels,
    cells,
    emitters
};

// A compiled display configuration borrows its identifier. That storage must
// outlive every use of the definition; string literals are suitable for static
// configurations. This value owns no pixels, input device, driver, or target.
struct DisplayDefinition
{
    std::string_view id{};
    Size logicalSize{};
    ViewportShape shape{ViewportShape::rectangle};
    Rect safeArea{};
    PixelFormat format{PixelFormat::rgb565};
    DisplayAddressSpace addressSpace{DisplayAddressSpace::pixels};
};

// Logical input is declared separately from the display and from physical
// adapters. Each capability is independent: text may arrive without key events,
// and an encoder/select adapter need not expose keyboard events. An empty set
// is a valid output-only application configuration.
struct LogicalInputConfiguration
{
    bool pointer{false};
    bool keys{false};
    bool text{false};
    bool encoder{false};
    bool select{false};
};

enum class NativeChannelModel : std::uint8_t
{
    rgb,
    red,
    redYellow
};

enum class NativeChannel : std::uint8_t
{
    red,
    green,
    blue,
    alpha,
    yellow,
    unused
};

struct NativeField
{
    NativeChannel channel{NativeChannel::unused};
    std::uint8_t bitOffset{0U};
    std::uint8_t bitWidth{0U};
    // Position in the format's native sample/emitter grid, not an application
    // coordinate. RGB fields share (0,0); packed fields occupy a 2x2 grid even
    // when one logical cell addresses all four emitters together.
    Point position{};
};

// Derived facts about an implemented format, never instructions for an
// arbitrary new packing scheme. Multibyte bit offsets use little-endian order.
// The fixed native emitter grid is a software contract, not panel verification.
struct NativeDescriptor
{
    StorageBlock storageBlock{};
    NativeChannelModel channelModel{NativeChannelModel::rgb};
    std::array<NativeField, 4U> fields{};
    std::uint8_t fieldCount{0U};
    bool storesDestinationAlpha{false};
    bool supportsSourceAlpha{false};
    bool supportsSourceCoverage{false};
    bool supportsSourceOver{false};
};

enum class [[nodiscard]] DisplayDefinitionError : std::uint8_t
{
    invalidId,
    invalidExtent,
    invalidShape,
    invalidSafeArea,
    unsupportedFormat,
    invalidAddressSpace,
    invalidStorage
};

// Stable identities shared with host validation. LVDS2000 is reserved for an
// unknown error value; ordinary validation only produces LVDS2001..LVDS2007.
[[nodiscard]] constexpr std::string_view diagnosticId(DisplayDefinitionError error) noexcept
{
    switch (error)
    {
    case DisplayDefinitionError::invalidId:
        return "LVDS2001";
    case DisplayDefinitionError::invalidExtent:
        return "LVDS2002";
    case DisplayDefinitionError::invalidShape:
        return "LVDS2003";
    case DisplayDefinitionError::invalidSafeArea:
        return "LVDS2004";
    case DisplayDefinitionError::unsupportedFormat:
        return "LVDS2005";
    case DisplayDefinitionError::invalidAddressSpace:
        return "LVDS2006";
    case DisplayDefinitionError::invalidStorage:
        return "LVDS2007";
    }
    return "LVDS2000";
}

[[nodiscard]] constexpr std::expected<NativeDescriptor, DisplayDefinitionError>
nativeDescriptor(PixelFormat format) noexcept
{
    NativeDescriptor result{};
    result.storageBlock = storageBlock(format);
    result.supportsSourceAlpha = true;
    result.supportsSourceCoverage = true;
    result.supportsSourceOver = true;
    switch (format)
    {
    case PixelFormat::rgb565:
        result.fields = {{{NativeChannel::red, 11U, 5U, {}},
                          {NativeChannel::green, 5U, 6U, {}},
                          {NativeChannel::blue, 0U, 5U, {}},
                          {}}};
        result.fieldCount = 3U;
        return result;
    case PixelFormat::rgb888:
        result.fields = {{{NativeChannel::red, 0U, 8U, {}},
                          {NativeChannel::green, 8U, 8U, {}},
                          {NativeChannel::blue, 16U, 8U, {}},
                          {}}};
        result.fieldCount = 3U;
        return result;
    case PixelFormat::xrgb8888:
    case PixelFormat::argb8888:
        result.storesDestinationAlpha = format == PixelFormat::argb8888;
        result.fields = {
            {{NativeChannel::red, 16U, 8U, {}},
             {NativeChannel::green, 8U, 8U, {}},
             {NativeChannel::blue, 0U, 8U, {}},
             {result.storesDestinationAlpha ? NativeChannel::alpha : NativeChannel::unused,
              24U,
              8U,
              {}}}};
        result.fieldCount = 4U;
        return result;
    case PixelFormat::r2Packed2x2:
    case PixelFormat::rrrr2222:
    case PixelFormat::ryry2222:
        result.channelModel = format == PixelFormat::ryry2222 ? NativeChannelModel::redYellow
                                                              : NativeChannelModel::red;
        const NativeChannel diagonal =
            format == PixelFormat::ryry2222 ? NativeChannel::yellow : NativeChannel::red;
        result.fields = {{{diagonal, 2U, 2U, {0, 0}},
                          {NativeChannel::red, 6U, 2U, {1, 0}},
                          {NativeChannel::red, 0U, 2U, {0, 1}},
                          {diagonal, 4U, 2U, {1, 1}}}};
        result.fieldCount = 4U;
        return result;
    }
    return std::unexpected{DisplayDefinitionError::unsupportedFormat};
}

namespace detail
{

[[nodiscard]] constexpr bool validDisplayId(std::string_view id) noexcept
{
    // Match the existing host profile identity rule, with no locale dependency:
    // ^[a-z0-9]+(?:[.-][a-z0-9]+)*$, 1..128 ASCII characters.
    if (id.empty() || id.size() > 128U)
    {
        return false;
    }
    bool previousSeparator = true;
    for (const char character : id)
    {
        if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
        {
            previousSeparator = false;
        }
        else if ((character == '.' || character == '-') && !previousSeparator)
        {
            previousSeparator = true;
        }
        else
        {
            return false;
        }
    }
    return !previousSeparator;
}

[[nodiscard]] constexpr bool safeAreaFits(const DisplayDefinition& definition) noexcept
{
    const Rect area = definition.safeArea;
    const Size size = definition.logicalSize;
    if (area.empty() || area.x < 0 || area.y < 0 || area.x > size.width || area.y > size.height ||
        area.width > size.width - area.x || area.height > size.height - area.y)
    {
        return false;
    }
    // Containment above proves representable edges before any edge arithmetic.
    // A rectangle/circle is convex, so its four corner pixel centers suffice.
    // Reuse the actual rendering/hit-testing rule rather than a second circle formula.
    const Viewport viewport{{0, 0, size.width, size.height}, definition.shape};
    const std::int32_t right = area.right() - 1;
    const std::int32_t bottom = area.bottom() - 1;
    return viewport.contains({area.x, area.y}) && viewport.contains({right, area.y}) &&
           viewport.contains({area.x, bottom}) && viewport.contains({right, bottom});
}

} // namespace detail

// Structural compatibility is independent of the selected destination writer,
// framebuffer strategy, physical input, or MCU. Diagnostics have deterministic
// priority in ID order. Storage checks prove representable complete blocks using
// the same checked layout calculation as Surface; they impose no memory budget.
[[nodiscard]] constexpr std::expected<void, DisplayDefinitionError>
validateDisplayDefinition(const DisplayDefinition& definition) noexcept
{
    if (!detail::validDisplayId(definition.id))
    {
        return std::unexpected{DisplayDefinitionError::invalidId};
    }
    if (definition.logicalSize.width <= 0 || definition.logicalSize.height <= 0)
    {
        return std::unexpected{DisplayDefinitionError::invalidExtent};
    }
    if ((definition.shape != ViewportShape::rectangle &&
         definition.shape != ViewportShape::circle) ||
        (definition.shape == ViewportShape::circle &&
         definition.logicalSize.width != definition.logicalSize.height))
    {
        return std::unexpected{DisplayDefinitionError::invalidShape};
    }
    if (!detail::safeAreaFits(definition))
    {
        return std::unexpected{DisplayDefinitionError::invalidSafeArea};
    }
    const auto descriptor = nativeDescriptor(definition.format);
    if (!descriptor)
    {
        return std::unexpected{descriptor.error()};
    }
    const auto expectedSpace =
        definition.format == PixelFormat::r2Packed2x2 ? DisplayAddressSpace::emitters
        : definition.format == PixelFormat::rrrr2222 || definition.format == PixelFormat::ryry2222
            ? DisplayAddressSpace::cells
            : DisplayAddressSpace::pixels;
    if (definition.addressSpace != expectedSpace)
    {
        return std::unexpected{DisplayDefinitionError::invalidAddressSpace};
    }
    const StorageBlock block = descriptor->storageBlock;
    if (definition.logicalSize.width % block.width != 0 ||
        definition.logicalSize.height % block.height != 0)
    {
        return std::unexpected{DisplayDefinitionError::invalidStorage};
    }
    std::size_t strideBytes = 0U;
    const auto columns = detail::nonnegativeSize(definition.logicalSize.width / block.width);
    if (!detail::checkedMultiply(columns, block.bytes, strideBytes) ||
        !storageLayout(definition.format, definition.logicalSize, strideBytes))
    {
        return std::unexpected{DisplayDefinitionError::invalidStorage};
    }
    return {};
}

[[nodiscard]] constexpr std::expected<Viewport, DisplayDefinitionError>
displayViewport(const DisplayDefinition& definition) noexcept
{
    const auto valid = validateDisplayDefinition(definition);
    if (!valid)
    {
        return std::unexpected{valid.error()};
    }
    return Viewport{{0, 0, definition.logicalSize.width, definition.logicalSize.height},
                    definition.shape};
}

} // namespace lithe
