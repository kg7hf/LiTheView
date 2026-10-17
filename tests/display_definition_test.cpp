// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/display_definition.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <limits>
#include <span>
#include <string_view>
#include <type_traits>

namespace
{

using lithe::DisplayAddressSpace;
using lithe::DisplayDefinition;
using lithe::DisplayDefinitionError;
using lithe::NativeChannel;
using lithe::NativeChannelModel;
using lithe::NativeField;
using lithe::PixelFormat;
using lithe::ViewportShape;

constexpr DisplayDefinition kRgb565{"h750-rgb565",
                                    {480, 272},
                                    ViewportShape::rectangle,
                                    {0, 0, 480, 272},
                                    PixelFormat::rgb565,
                                    DisplayAddressSpace::pixels};
constexpr DisplayDefinition kXrgb8888{"h750-xrgb8888",
                                      {480, 272},
                                      ViewportShape::rectangle,
                                      {0, 0, 480, 272},
                                      PixelFormat::xrgb8888,
                                      DisplayAddressSpace::pixels};
constexpr DisplayDefinition kRound{"gc9a01-240-round",
                                   {240, 240},
                                   ViewportShape::circle,
                                   {36, 36, 168, 168},
                                   PixelFormat::rgb565,
                                   DisplayAddressSpace::pixels};
constexpr DisplayDefinition kR2{"software-red-emitters",
                                {608, 512},
                                ViewportShape::rectangle,
                                {0, 0, 608, 512},
                                PixelFormat::r2Packed2x2,
                                DisplayAddressSpace::emitters};
constexpr DisplayDefinition kRedCell{"software-red-cells",
                                     {304, 256},
                                     ViewportShape::rectangle,
                                     {0, 0, 304, 256},
                                     PixelFormat::rrrr2222,
                                     DisplayAddressSpace::cells};
constexpr DisplayDefinition kDualCell{"software-red-yellow-cells",
                                      {304, 256},
                                      ViewportShape::rectangle,
                                      {0, 0, 304, 256},
                                      PixelFormat::ryry2222,
                                      DisplayAddressSpace::cells};
constexpr std::array kDefinitions{kRgb565, kXrgb8888, kRound, kR2, kRedCell, kDualCell};

static_assert(std::is_trivially_copyable_v<DisplayDefinition>);
static_assert(std::is_trivially_copyable_v<lithe::NativeDescriptor>);
static_assert(std::is_trivially_copyable_v<lithe::LogicalInputConfiguration>);

constexpr bool hasError(const DisplayDefinition& definition, DisplayDefinitionError error) noexcept
{
    const auto validation = lithe::validateDisplayDefinition(definition);
    const auto viewport = lithe::displayViewport(definition);
    return !validation && validation.error() == error && !viewport && viewport.error() == error;
}

constexpr DisplayDefinition withId(std::string_view id) noexcept
{
    auto result = kRgb565;
    result.id = id;
    return result;
}

constexpr DisplayDefinition
geometry(lithe::Size size,
         ViewportShape shape,
         lithe::Rect safeArea,
         PixelFormat format = PixelFormat::rgb565,
         DisplayAddressSpace addressSpace = DisplayAddressSpace::pixels) noexcept
{
    return {"geometry", size, shape, safeArea, format, addressSpace};
}

constexpr bool validDefinitions() noexcept
{
    for (const auto& definition : kDefinitions)
    {
        const auto result = lithe::validateDisplayDefinition(definition);
        const auto viewport = lithe::displayViewport(definition);
        if (!result || !viewport || viewport->bounds.x != 0 || viewport->bounds.y != 0 ||
            viewport->bounds.width != definition.logicalSize.width ||
            viewport->bounds.height != definition.logicalSize.height ||
            viewport->shape != definition.shape)
        {
            return false;
        }
    }
    return true;
}
static_assert(validDefinitions());

// The definition header is independent of render_config.hpp and compiled
// writers. These format descriptions are valid without linking any Core archive.
static_assert(lithe::validateDisplayDefinition(
    geometry({9, 7}, ViewportShape::rectangle, {0, 0, 9, 7}, PixelFormat::rgb888)));
static_assert(lithe::validateDisplayDefinition(
    geometry({9, 7}, ViewportShape::rectangle, {0, 0, 9, 7}, PixelFormat::argb8888)));

constexpr auto kLongId = []
{
    std::array<char, 129U> characters{};
    characters.fill('a');
    return characters;
}();
static_assert(lithe::validateDisplayDefinition(withId("a")));
static_assert(lithe::validateDisplayDefinition(withId("0")));
static_assert(lithe::validateDisplayDefinition(withId("litheview.surface.rgb565-1")));
static_assert(lithe::validateDisplayDefinition(withId({kLongId.data(), 128U})));
static_assert(hasError(withId({kLongId.data(), 129U}), DisplayDefinitionError::invalidId));
static_assert(hasError(withId(""), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("Uppercase"), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("under_score"), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("bad space"), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("-start"), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("end."), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("two..dots"), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("mixed.-separators"), DisplayDefinitionError::invalidId));
static_assert(hasError(withId(std::string_view{"a\0b", 3U}), DisplayDefinitionError::invalidId));
static_assert(hasError(withId("caf\xc3\xa9"), DisplayDefinitionError::invalidId));

static_assert(hasError(geometry({0, 1}, ViewportShape::rectangle, {0, 0, 1, 1}),
                       DisplayDefinitionError::invalidExtent));
static_assert(hasError(geometry({1, -1}, ViewportShape::rectangle, {0, 0, 1, 1}),
                       DisplayDefinitionError::invalidExtent));
static_assert(hasError(geometry({4, 4}, ViewportShape{255U}, {1, 1, 2, 2}),
                       DisplayDefinitionError::invalidShape));
static_assert(hasError(geometry({4, 6}, ViewportShape::circle, {1, 1, 2, 2}),
                       DisplayDefinitionError::invalidShape));
static_assert(hasError(geometry({4, 4}, ViewportShape::rectangle, {}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(geometry({4, 4}, ViewportShape::rectangle, {-1, 0, 2, 2}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(geometry({4, 4}, ViewportShape::rectangle, {0, -1, 2, 2}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(geometry({4, 4}, ViewportShape::rectangle, {3, 0, 2, 2}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(geometry({4, 4}, ViewportShape::rectangle, {0, 3, 2, 2}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(geometry({4, 4}, ViewportShape::rectangle, {0, 0, -1, 2}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(geometry({4, 4}, ViewportShape::circle, {0, 0, 4, 4}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(
    lithe::validateDisplayDefinition(geometry({3, 3}, ViewportShape::circle, {0, 0, 3, 3})));
static_assert(
    lithe::validateDisplayDefinition(geometry({4, 4}, ViewportShape::circle, {0, 1, 4, 2})));
static_assert(lithe::validateDisplayDefinition(
    geometry({240, 240}, ViewportShape::circle, {35, 35, 170, 170})));
static_assert(hasError(geometry({240, 240}, ViewportShape::circle, {34, 34, 172, 172}),
                       DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(geometry({4, 4}, ViewportShape::rectangle, {0, 0, 4, 4}, PixelFormat{255U}),
                       DisplayDefinitionError::unsupportedFormat));
static_assert(hasError(geometry({4, 4},
                                ViewportShape::rectangle,
                                {0, 0, 4, 4},
                                PixelFormat::rgb565,
                                DisplayAddressSpace{255U}),
                       DisplayDefinitionError::invalidAddressSpace));
static_assert(hasError(geometry({4, 4},
                                ViewportShape::rectangle,
                                {0, 0, 4, 4},
                                PixelFormat::rgb565,
                                DisplayAddressSpace::cells),
                       DisplayDefinitionError::invalidAddressSpace));
static_assert(hasError(geometry({4, 4},
                                ViewportShape::rectangle,
                                {0, 0, 4, 4},
                                PixelFormat::r2Packed2x2,
                                DisplayAddressSpace::pixels),
                       DisplayDefinitionError::invalidAddressSpace));
static_assert(hasError(geometry({4, 4},
                                ViewportShape::rectangle,
                                {0, 0, 4, 4},
                                PixelFormat::ryry2222,
                                DisplayAddressSpace::emitters),
                       DisplayDefinitionError::invalidAddressSpace));
static_assert(hasError(geometry({3, 4},
                                ViewportShape::rectangle,
                                {0, 0, 3, 4},
                                PixelFormat::r2Packed2x2,
                                DisplayAddressSpace::emitters),
                       DisplayDefinitionError::invalidStorage));
static_assert(hasError(geometry({4, 3},
                                ViewportShape::rectangle,
                                {0, 0, 4, 3},
                                PixelFormat::r2Packed2x2,
                                DisplayAddressSpace::emitters),
                       DisplayDefinitionError::invalidStorage));
static_assert(hasError(geometry({3, 3},
                                ViewportShape::circle,
                                {0, 0, 3, 3},
                                PixelFormat::r2Packed2x2,
                                DisplayAddressSpace::emitters),
                       DisplayDefinitionError::invalidStorage));
static_assert(lithe::validateDisplayDefinition(geometry({4, 4},
                                                        ViewportShape::circle,
                                                        {1, 1, 2, 2},
                                                        PixelFormat::r2Packed2x2,
                                                        DisplayAddressSpace::emitters)));
static_assert(lithe::validateDisplayDefinition(geometry({3, 3},
                                                        ViewportShape::circle,
                                                        {0, 0, 3, 3},
                                                        PixelFormat::rrrr2222,
                                                        DisplayAddressSpace::cells)));

constexpr auto kMaximumCoordinate = std::numeric_limits<std::int32_t>::max();
static_assert(hasError(
    geometry({kMaximumCoordinate, 1}, ViewportShape::rectangle, {kMaximumCoordinate - 1, 0, 2, 1}),
    DisplayDefinitionError::invalidSafeArea));
static_assert(hasError(
    geometry({1, kMaximumCoordinate}, ViewportShape::rectangle, {0, kMaximumCoordinate - 1, 1, 2}),
    DisplayDefinitionError::invalidSafeArea));
static_assert(
    hasError(geometry({4, 4},
                      ViewportShape::rectangle,
                      {std::numeric_limits<std::int32_t>::min(), 0, kMaximumCoordinate, 1}),
             DisplayDefinitionError::invalidSafeArea));
constexpr auto kWideDisplay = geometry({kMaximumCoordinate, 1},
                                       ViewportShape::rectangle,
                                       {kMaximumCoordinate - 1, 0, 1, 1},
                                       PixelFormat::xrgb8888);
static_assert(sizeof(std::size_t) == 4U
                  ? hasError(kWideDisplay, DisplayDefinitionError::invalidStorage)
                  : lithe::validateDisplayDefinition(kWideDisplay).has_value());
constexpr auto kLargeDisplay = geometry({65536, 65536},
                                        ViewportShape::rectangle,
                                        {0, 0, 65536, 65536},
                                        PixelFormat::rrrr2222,
                                        DisplayAddressSpace::cells);
static_assert(sizeof(std::size_t) == 4U
                  ? hasError(kLargeDisplay, DisplayDefinitionError::invalidStorage)
                  : lithe::validateDisplayDefinition(kLargeDisplay).has_value());
constexpr auto kLargestCircle = geometry({kMaximumCoordinate, kMaximumCoordinate},
                                         ViewportShape::circle,
                                         {kMaximumCoordinate / 2, kMaximumCoordinate / 2, 1, 1},
                                         PixelFormat::rrrr2222,
                                         DisplayAddressSpace::cells);
constexpr auto kLargestViewport = lithe::displayViewport(kLargestCircle);
static_assert(sizeof(std::size_t) == 4U
                  ? hasError(kLargestCircle, DisplayDefinitionError::invalidStorage)
                  : kLargestViewport &&
                        kLargestViewport->contains({kMaximumCoordinate / 2,
                                                    kMaximumCoordinate / 2}) &&
                        !kLargestViewport->contains({0, 0}));

constexpr bool descriptorsMatch() noexcept
{
    constexpr std::array formats{PixelFormat::rgb565,
                                 PixelFormat::rgb888,
                                 PixelFormat::xrgb8888,
                                 PixelFormat::argb8888,
                                 PixelFormat::r2Packed2x2,
                                 PixelFormat::rrrr2222,
                                 PixelFormat::ryry2222};
    constexpr std::array<std::array<NativeField, 4U>, 7U> fields{
        {{{{NativeChannel::red, 11U, 5U, {0, 0}},
           {NativeChannel::green, 5U, 6U, {0, 0}},
           {NativeChannel::blue, 0U, 5U, {0, 0}},
           {}}},
         {{{NativeChannel::red, 0U, 8U, {0, 0}},
           {NativeChannel::green, 8U, 8U, {0, 0}},
           {NativeChannel::blue, 16U, 8U, {0, 0}},
           {}}},
         {{{NativeChannel::red, 16U, 8U, {0, 0}},
           {NativeChannel::green, 8U, 8U, {0, 0}},
           {NativeChannel::blue, 0U, 8U, {0, 0}},
           {NativeChannel::unused, 24U, 8U, {0, 0}}}},
         {{{NativeChannel::red, 16U, 8U, {0, 0}},
           {NativeChannel::green, 8U, 8U, {0, 0}},
           {NativeChannel::blue, 0U, 8U, {0, 0}},
           {NativeChannel::alpha, 24U, 8U, {0, 0}}}},
         {{{NativeChannel::red, 2U, 2U, {0, 0}},
           {NativeChannel::red, 6U, 2U, {1, 0}},
           {NativeChannel::red, 0U, 2U, {0, 1}},
           {NativeChannel::red, 4U, 2U, {1, 1}}}},
         {{{NativeChannel::red, 2U, 2U, {0, 0}},
           {NativeChannel::red, 6U, 2U, {1, 0}},
           {NativeChannel::red, 0U, 2U, {0, 1}},
           {NativeChannel::red, 4U, 2U, {1, 1}}}},
         {{{NativeChannel::yellow, 2U, 2U, {0, 0}},
           {NativeChannel::red, 6U, 2U, {1, 0}},
           {NativeChannel::red, 0U, 2U, {0, 1}},
           {NativeChannel::yellow, 4U, 2U, {1, 1}}}}}};
    constexpr std::array<std::uint8_t, 7U> counts{3U, 3U, 4U, 4U, 4U, 4U, 4U};
    constexpr std::array<std::size_t, 7U> storageBytes{2U, 3U, 4U, 4U, 1U, 1U, 1U};
    for (std::size_t index = 0U; index < formats.size(); ++index)
    {
        const auto result = lithe::nativeDescriptor(formats[index]);
        const auto blockDimension = formats[index] == PixelFormat::r2Packed2x2 ? 2 : 1;
        const auto channelModel = index < 4U    ? NativeChannelModel::rgb
                                  : index == 6U ? NativeChannelModel::redYellow
                                                : NativeChannelModel::red;
        if (!result || result->fieldCount != counts[index] ||
            result->storageBlock.width != blockDimension ||
            result->storageBlock.height != blockDimension ||
            result->storageBlock.bytes != storageBytes[index] ||
            result->channelModel != channelModel ||
            result->storesDestinationAlpha != (formats[index] == PixelFormat::argb8888) ||
            !result->supportsSourceAlpha || !result->supportsSourceCoverage ||
            !result->supportsSourceOver)
        {
            return false;
        }
        for (std::size_t field = 0U; field < counts[index]; ++field)
        {
            const auto actual = result->fields[field];
            const auto expected = fields[index][field];
            if (actual.channel != expected.channel || actual.bitOffset != expected.bitOffset ||
                actual.bitWidth != expected.bitWidth || actual.position.x != expected.position.x ||
                actual.position.y != expected.position.y)
            {
                return false;
            }
        }
    }
    return true;
}
static_assert(descriptorsMatch());
constexpr auto kUnknownFormat = lithe::nativeDescriptor(PixelFormat{255U});
static_assert(!kUnknownFormat &&
              kUnknownFormat.error() == DisplayDefinitionError::unsupportedFormat);

constexpr bool diagnosticIdsMatch() noexcept
{
    constexpr std::array errors{DisplayDefinitionError::invalidId,
                                DisplayDefinitionError::invalidExtent,
                                DisplayDefinitionError::invalidShape,
                                DisplayDefinitionError::invalidSafeArea,
                                DisplayDefinitionError::unsupportedFormat,
                                DisplayDefinitionError::invalidAddressSpace,
                                DisplayDefinitionError::invalidStorage};
    constexpr std::array<std::string_view, 7U> ids{
        "LVDS2001", "LVDS2002", "LVDS2003", "LVDS2004", "LVDS2005", "LVDS2006", "LVDS2007"};
    for (std::size_t index = 0U; index < errors.size(); ++index)
    {
        if (lithe::diagnosticId(errors[index]) != ids[index])
        {
            return false;
        }
    }
    return lithe::diagnosticId(DisplayDefinitionError{255U}) == "LVDS2000";
}
static_assert(diagnosticIdsMatch());
static_assert(hasError(DisplayDefinition{}, DisplayDefinitionError::invalidId));

constexpr lithe::LogicalInputConfiguration kEncoderOnly{false, false, false, true, false};
constexpr lithe::LogicalInputConfiguration kTextOnly{false, false, true, false, false};
constexpr lithe::LogicalInputConfiguration kOutputOnly{};
static_assert(kEncoderOnly.encoder && !kEncoderOnly.keys && !kEncoderOnly.select);
static_assert(kTextOnly.text && !kTextOnly.keys && !kTextOnly.pointer);
static_assert(!kOutputOnly.pointer && !kOutputOnly.keys && !kOutputOnly.text &&
              !kOutputOnly.encoder && !kOutputOnly.select);

class Checks final
{
public:
    void expect(bool condition, const char* message) noexcept
    {
        if (!condition)
        {
            std::printf("FAIL: %s\n", message);
            ++failures;
        }
    }
    int failures{0};
};

void checkViewportIdentity(Checks& checks)
{
    const auto derived = lithe::displayViewport(kRound);
    const auto existing = lithe::Viewport::circle({0, 0, 240, 240});
    checks.expect(derived.has_value(), "round definition produces a viewport");
    if (!derived)
    {
        return;
    }
    for (std::int32_t y = -1; y <= 240; ++y)
    {
        for (std::int32_t x = -1; x <= 240; ++x)
        {
            checks.expect(derived->contains({x, y}) == existing.contains({x, y}),
                          "derived round geometry matches the existing renderer at every sample");
        }
    }
    for (std::int32_t y = 36; y < 204; ++y)
    {
        for (std::int32_t x = 36; x < 204; ++x)
        {
            checks.expect(derived->contains({x, y}), "every declared safe-area sample is visible");
        }
    }
}

void checkBorrowedIdentity(Checks& checks)
{
    std::array<char, 5U> id{'l', 'o', 'c', 'a', 'l'};
    auto definition = kRgb565;
    definition.id = std::string_view{id.data(), id.size()};
    checks.expect(lithe::validateDisplayDefinition(definition).has_value(),
                  "a definition accepts a live borrowed non-null-terminated identifier");
    id[0] = 'X';
    checks.expect(
        hasError(definition, DisplayDefinitionError::invalidId),
        "the identifier remains a caller-owned view rather than a hidden allocation/copy");
}

} // namespace

int main()
{
    Checks checks{};
    checks.expect(validDefinitions(), "all named display configurations validate without a target");
    checks.expect(descriptorsMatch(), "native descriptors match independent field/layout facts");
    checks.expect(diagnosticIdsMatch(),
                  "display diagnostics preserve their shared stable identities");
    checkViewportIdentity(checks);
    checkBorrowedIdentity(checks);
    return checks.failures == 0 ? 0 : 1;
}
