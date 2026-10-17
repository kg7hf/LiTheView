// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/surface_layout.hpp"

#include <array>
#include <charconv>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <limits>
#include <span>
#include <string>
#include <string_view>
#include <system_error>

namespace
{

using lithe::DisplayDefinition;
using lithe::Rect;
using lithe::SurfaceLayoutError;
using lithe::SurfaceLayoutNode;
using lithe::SurfacePlacementPolicy;

constexpr DisplayDefinition kRound{"round-layout",
                                   {240, 240},
                                   lithe::ViewportShape::circle,
                                   {36, 36, 168, 168},
                                   lithe::PixelFormat::rgb565,
                                   lithe::DisplayAddressSpace::pixels};
constexpr DisplayDefinition kRectangle{"rectangle-layout",
                                       {100, 100},
                                       lithe::ViewportShape::rectangle,
                                       {0, 0, 100, 100},
                                       lithe::PixelFormat::rgb565,
                                       lithe::DisplayAddressSpace::pixels};

constexpr bool
placementError(Rect frame, SurfacePlacementPolicy policy, SurfaceLayoutError expected)
{
    const auto result = lithe::validateSurfacePlacement(kRound, frame, policy);
    return !result && result.error() == expected;
}

static_assert(lithe::validateSurfacePlacement(kRound, {36, 36, 168, 168}));
static_assert(placementError({35, 36, 168, 168},
                             SurfacePlacementPolicy::safeArea,
                             SurfaceLayoutError::outsideSafeArea));
static_assert(lithe::validateSurfacePlacement(kRound,
                                              {0, 0, 240, 240},
                                              SurfacePlacementPolicy::clipToViewport));
static_assert(placementError({0, 0, 10, 10},
                             SurfacePlacementPolicy::clipToViewport,
                             SurfaceLayoutError::fullyClipped));
static_assert(placementError({std::numeric_limits<std::int32_t>::max(), 0, 1, 1},
                             SurfacePlacementPolicy::clipToViewport,
                             SurfaceLayoutError::invalidGeometry));
static_assert(placementError({0, 0, 1, 1},
                             SurfacePlacementPolicy{255U},
                             SurfaceLayoutError::invalidPolicy));
static_assert(lithe::validateSurfaceInput({}, {}));
static_assert(lithe::validateSurfaceInput({false, false, true, true, true}, {true, true, true}));

constexpr bool popupChecks()
{
    const auto anchored = lithe::placeSurfacePopup(kRound, {50, 60, 40, 30});
    const auto noFallback = lithe::placeSurfacePopup(kRound, {180, 180, 50, 40});
    const auto centered = lithe::placeSurfacePopup(
        kRound, {180, 180, 50, 40}, lithe::SurfacePopupFallback::centeredSafeArea);
    const auto explicitSize =
        lithe::placeSurfacePopup(kRound,
                                 {20, 47, 200, 146},
                                 lithe::SurfacePopupFallback::centeredSafeArea,
                                 lithe::Size{168, 146});
    const auto tooLarge = lithe::placeSurfacePopup(
        kRound, {0, 0, 169, 146}, lithe::SurfacePopupFallback::centeredSafeArea);
    const auto invalid =
        lithe::placeSurfacePopup(kRound, {50, 60, 40, 30}, lithe::SurfacePopupFallback{255U});
    const auto invalidSize = lithe::placeSurfacePopup(
        kRound, {50, 60, 40, 30}, lithe::SurfacePopupFallback::centeredSafeArea, lithe::Size{0, 1});
    const auto unusedSize = lithe::placeSurfacePopup(
        kRound, {50, 60, 40, 30}, lithe::SurfacePopupFallback::none, lithe::Size{1, 1});
    return anchored && !anchored->usedFallback && anchored->frame.x == 50 &&
           anchored->frame.y == 60 && !noFallback &&
           noFallback.error() == SurfaceLayoutError::popupCannotFit && centered &&
           centered->usedFallback && centered->frame.x == 95 && centered->frame.y == 100 &&
           centered->frame.width == 50 && centered->frame.height == 40 && explicitSize &&
           explicitSize->usedFallback && explicitSize->frame.x == 36 &&
           explicitSize->frame.y == 47 && explicitSize->frame.width == 168 &&
           explicitSize->frame.height == 146 && !tooLarge &&
           tooLarge.error() == SurfaceLayoutError::popupCannotFit && !invalid &&
           invalid.error() == SurfaceLayoutError::invalidPolicy && !invalidSize &&
           invalidSize.error() == SurfaceLayoutError::invalidGeometry && !unusedSize &&
           unusedSize.error() == SurfaceLayoutError::invalidPolicy;
}
static_assert(popupChecks());

constexpr bool hierarchyChecks()
{
    std::array<SurfaceLayoutNode, 3U> nodes{{
        {std::nullopt, {0, 0, 100, 100}, SurfacePlacementPolicy::clipToViewport},
        {0U, {10, 10, 80, 60}, SurfacePlacementPolicy::safeArea, true, 140},
        {1U, {5, 120, 50, 20}, SurfacePlacementPolicy::safeArea},
    }};
    if (!lithe::validateSurfaceLayout(kRectangle, nodes))
    {
        return false;
    }
    // An automatically fitted direct child remains reachable even when the
    // declared content height was smaller than its bottom edge.
    nodes[1].contentHeight = -1;
    if (!lithe::validateSurfaceLayout(kRectangle, nodes))
    {
        return false;
    }
    nodes[2].frame.width = 81;
    const auto tooWide = lithe::validateSurfaceLayout(kRectangle, nodes);
    if (tooWide || tooWide.error().error != SurfaceLayoutError::unreachablePlacement ||
        tooWide.error().nodeIndex != 2U)
    {
        return false;
    }
    nodes[2].frame.width = 50;
    nodes[2].frame.height = 61;
    const auto tooTall = lithe::validateSurfaceLayout(kRectangle, nodes);
    if (tooTall || tooTall.error().error != SurfaceLayoutError::unreachablePlacement)
    {
        return false;
    }
    nodes[2].placement = SurfacePlacementPolicy::clipToViewport;
    if (!lithe::validateSurfaceLayout(kRectangle, nodes))
    {
        return false;
    }
    nodes[2].frame.x = 80;
    const auto invisible = lithe::validateSurfaceLayout(kRectangle, nodes);
    if (invisible || invisible.error().error != SurfaceLayoutError::fullyClipped)
    {
        return false;
    }
    nodes[2].parent = 2U;
    const auto cycle = lithe::validateSurfaceLayout(kRectangle, nodes);
    const auto empty =
        lithe::validateSurfaceLayout(kRectangle, std::span<const SurfaceLayoutNode>{});
    return !cycle && cycle.error().error == SurfaceLayoutError::invalidHierarchy &&
           cycle.error().nodeIndex == 2U && !empty &&
           empty.error().error == SurfaceLayoutError::invalidHierarchy;
}
static_assert(hierarchyChecks());

constexpr bool nestedAndSafeScrollChecks()
{
    std::array<SurfaceLayoutNode, 4U> nodes{{
        {std::nullopt, {0, 0, 240, 240}, SurfacePlacementPolicy::clipToViewport},
        {0U, {36, 0, 168, 240}, SurfacePlacementPolicy::clipToViewport, true, 600},
        {1U, {0, 300, 168, 168}, SurfacePlacementPolicy::safeArea, true, 400},
        {2U, {4, 370, 160, 20}, SurfacePlacementPolicy::safeArea},
    }};
    // Choosing the first reachable offset would place the inner panel at y=0,
    // outside the safe area. The feasible interval includes y=36 instead.
    if (!lithe::validateSurfaceLayout(kRound, nodes))
    {
        return false;
    }
    nodes[3].frame.x = -1;
    const auto negativeX = lithe::validateSurfaceLayout(kRound, nodes);
    if (negativeX || negativeX.error().error != SurfaceLayoutError::unreachablePlacement)
    {
        return false;
    }
    nodes[3].frame.x = 4;
    nodes[3].frame.y = -1;
    const auto negativeY = lithe::validateSurfaceLayout(kRound, nodes);
    if (negativeY || negativeY.error().error != SurfaceLayoutError::unreachablePlacement)
    {
        return false;
    }
    nodes[3].frame.y = 370;
    nodes[2].scrollable = false;
    const auto fixedAncestor = lithe::validateSurfaceLayout(kRound, nodes);
    return !fixedAncestor &&
           fixedAncestor.error().error == SurfaceLayoutError::unreachablePlacement;
}
static_assert(nestedAndSafeScrollChecks());

struct Checks
{
    int failures{0};
    void expect(bool passed, std::string_view message)
    {
        if (!passed)
        {
            ++failures;
            std::fputs("FAIL: ", stderr);
            std::fwrite(message.data(), sizeof(char), message.size(), stderr);
            std::fputc('\n', stderr);
        }
    }
};

template <std::size_t Count>
bool split(std::string_view line, std::array<std::string_view, Count>& fields)
{
    for (std::size_t index = 0U; index < Count; ++index)
    {
        const auto comma = line.find(',');
        if (index + 1U == Count)
        {
            fields[index] = line;
            return comma == std::string_view::npos;
        }
        if (comma == std::string_view::npos)
        {
            return false;
        }
        fields[index] = line.substr(0U, comma);
        line.remove_prefix(comma + 1U);
    }
    return false;
}

bool number(std::string_view text, std::int32_t& value)
{
    const auto parsed = std::from_chars(text.data(), text.data() + text.size(), value);
    return parsed.ec == std::errc{} && parsed.ptr == text.data() + text.size();
}

template <std::size_t Count, typename CheckRow>
void fixture(Checks& checks, const char* name, std::size_t expectedRows, const CheckRow& checkRow)
{
    const auto path =
        std::filesystem::path{__FILE__}.parent_path() / "fixtures" / "display-model" / name;
    std::ifstream input{path};
    checks.expect(input.is_open(), "shared fixture file opens");
    std::string line{};
    std::size_t rows = 0U;
    while (std::getline(input, line))
    {
        if (!line.empty() && line.back() == '\r')
        {
            line.pop_back();
        }
        if (line.empty() || line.front() == '#')
        {
            continue;
        }
        std::array<std::string_view, Count> fields{};
        if (!split(line, fields))
        {
            checks.expect(false, "shared fixture has exact column count");
            continue;
        }
        ++rows;
        checkRow(fields);
    }
    checks.expect(rows == expectedRows, "shared fixture case count matches reviewed corpus");
}

void sharedFixtures(Checks& checks)
{
    fixture<14U>(
        checks,
        "surface-placement.csv",
        26U,
        [&checks](const auto& fields)
        {
            std::array<std::int32_t, 10U> values{};
            for (std::size_t index = 0U; index < values.size(); ++index)
            {
                if (!number(fields[index + 2U], values[index]))
                {
                    checks.expect(false, fields[0]);
                    return;
                }
            }
            const auto shape = fields[1] == "rectangle" ? lithe::ViewportShape::rectangle
                                                        : lithe::ViewportShape::circle;
            const DisplayDefinition definition{"fixture",
                                               {values[0], values[1]},
                                               shape,
                                               {values[2], values[3], values[4], values[5]},
                                               lithe::PixelFormat::rgb565,
                                               lithe::DisplayAddressSpace::pixels};
            const auto policy =
                fields[12] == "safeArea"         ? SurfacePlacementPolicy::safeArea
                : fields[12] == "visibleShape"   ? SurfacePlacementPolicy::visibleShape
                : fields[12] == "clipToViewport" ? SurfacePlacementPolicy::clipToViewport
                                                 : SurfacePlacementPolicy{255U};
            const auto result = lithe::validateSurfacePlacement(
                definition, {values[6], values[7], values[8], values[9]}, policy);
            checks.expect((result ? std::string_view{"OK"} : lithe::diagnosticId(result.error())) ==
                              fields[13],
                          fields[0]);
        });
    fixture<10U>(checks,
                 "logical-input.csv",
                 20U,
                 [&checks](const auto& fields)
                 {
                     std::array<bool, 8U> values{};
                     for (std::size_t index = 0U; index < values.size(); ++index)
                     {
                         if (fields[index + 1U] != "0" && fields[index + 1U] != "1")
                         {
                             checks.expect(false, fields[0]);
                             return;
                         }
                         values[index] = fields[index + 1U] == "1";
                     }
                     const auto result = lithe::validateSurfaceInput(
                         {values[0], values[1], values[2], values[3], values[4]},
                         {values[5], values[6], values[7]});
                     checks.expect((result ? std::string_view{"OK"}
                                           : lithe::diagnosticId(result.error())) == fields[9],
                                   fields[0]);
                 });
}

void sharedHierarchy(Checks& checks)
{
    std::array<SurfaceLayoutNode, 16U> nodes{};
    std::size_t count = 0U;
    std::size_t groups = 0U;
    std::string name{};
    std::string expected{};
    std::optional<std::size_t> errorNode{};
    const auto checkGroup = [&]()
    {
        if (name.empty())
        {
            return;
        }
        ++groups;
        const auto result = lithe::validateSurfaceLayout(
            kRound, std::span<const SurfaceLayoutNode>{nodes}.first(count));
        checks.expect((result ? std::string_view{"OK"}
                              : lithe::diagnosticId(result.error().error)) == expected,
                      name);
        checks.expect(result ? !errorNode : errorNode == result.error().nodeIndex, name);
    };
    const auto readIndex = [](std::string_view text, std::size_t& value)
    {
        const auto parsed = std::from_chars(text.data(), text.data() + text.size(), value);
        return parsed.ec == std::errc{} && parsed.ptr == text.data() + text.size();
    };
    fixture<12U>(
        checks,
        "surface-hierarchy.csv",
        54U,
        [&](const auto& fields)
        {
            if (name != fields[0])
            {
                checkGroup();
                name = fields[0];
                expected = fields[10];
                count = 0U;
                errorNode.reset();
                if (fields[11] != "-1")
                {
                    std::size_t parsedError = 0U;
                    checks.expect(readIndex(fields[11], parsedError), name);
                    errorNode = parsedError;
                }
            }
            std::size_t index = 0U;
            if (!readIndex(fields[1], index) || index != count || count >= nodes.size())
            {
                checks.expect(false, "hierarchy fixture ordered indices fit bounded storage");
                return;
            }
            SurfaceLayoutNode node{};
            if (fields[2] != "-1")
            {
                std::size_t parent = 0U;
                checks.expect(readIndex(fields[2], parent), name);
                node.parent = parent;
            }
            checks.expect(number(fields[3], node.frame.x) && number(fields[4], node.frame.y) &&
                              number(fields[5], node.frame.width) &&
                              number(fields[6], node.frame.height) &&
                              number(fields[9], node.contentHeight),
                          name);
            node.placement = fields[7] == "safeArea"       ? SurfacePlacementPolicy::safeArea
                             : fields[7] == "visibleShape" ? SurfacePlacementPolicy::visibleShape
                             : fields[7] == "clipToViewport"
                                 ? SurfacePlacementPolicy::clipToViewport
                                 : SurfacePlacementPolicy{255U};
            checks.expect(fields[8] == "0" || fields[8] == "1", name);
            node.scrollable = fields[8] == "1";
            nodes[count] = node;
            ++count;
        });
    checkGroup();
    checks.expect(groups == 21U, "shared hierarchy group count matches reviewed corpus");
}

// Independent small-domain oracle: enumerate actual legal offset pairs and
// test every painted pixel, rather than reproducing the interval algorithm.
void exhaustiveNestedScroll(Checks& checks)
{
    constexpr DisplayDefinition definition{"tiny-scroll",
                                           {9, 9},
                                           lithe::ViewportShape::circle,
                                           {2, 2, 5, 5},
                                           lithe::PixelFormat::rgb565,
                                           lithe::DisplayAddressSpace::pixels};
    constexpr auto viewport = lithe::Viewport::circle({0, 0, 9, 9});
    for (std::int32_t y = -1; y <= 7; ++y)
    {
        for (std::int32_t height = 1; height <= 5; ++height)
        {
            for (const auto policy : {SurfacePlacementPolicy::safeArea,
                                      SurfacePlacementPolicy::visibleShape,
                                      SurfacePlacementPolicy::clipToViewport})
            {
                const std::array<SurfaceLayoutNode, 4U> nodes{{
                    {std::nullopt, {0, 0, 9, 9}, SurfacePlacementPolicy::clipToViewport},
                    {0U, {2, 0, 5, 8}, SurfacePlacementPolicy::clipToViewport, true, 12},
                    {1U, {0, 5, 5, 4}, SurfacePlacementPolicy::clipToViewport, true, 8},
                    {2U, {0, y, 5, height}, policy},
                }};
                bool possible = false;
                const std::int32_t innerContent = std::max(std::int32_t{8}, y + height);
                for (std::int32_t outerOffset = 0; outerOffset <= 4; ++outerOffset)
                {
                    for (std::int32_t innerOffset = 0; innerOffset <= innerContent - 4;
                         ++innerOffset)
                    {
                        bool any = false;
                        bool all = true;
                        for (std::int32_t row = 0; row < height; ++row)
                        {
                            for (std::int32_t column = 0; column < 5; ++column)
                            {
                                const std::int32_t innerY = y + row - innerOffset;
                                const std::int32_t outerY = 5 + innerY - outerOffset;
                                const lithe::Point point{column + 2, outerY};
                                const bool ancestorVisible =
                                    innerY >= 0 && innerY < 4 && outerY >= 0 && outerY < 8;
                                const bool regionVisible =
                                    policy == SurfacePlacementPolicy::safeArea
                                        ? definition.safeArea.contains(point)
                                        : viewport.contains(point);
                                const bool visible = ancestorVisible && regionVisible;
                                any = any || visible;
                                all = all && visible;
                            }
                        }
                        possible = possible ||
                                   (policy == SurfacePlacementPolicy::clipToViewport ? any : all);
                    }
                }
                checks.expect(lithe::validateSurfaceLayout(definition, nodes).has_value() ==
                                  possible,
                              "nested scrolling agrees with exhaustive offsets and sample tests");
            }
        }
    }
}

void exhaustiveSmallCircles(Checks& checks)
{
    for (std::int32_t size = 1; size <= 9; ++size)
    {
        const DisplayDefinition definition{"small-circle",
                                           {size, size},
                                           lithe::ViewportShape::circle,
                                           {size / 2, size / 2, 1, 1},
                                           lithe::PixelFormat::rgb565,
                                           lithe::DisplayAddressSpace::pixels};
        const auto viewport = lithe::Viewport::circle({0, 0, size, size});
        for (std::int32_t y = -2; y <= size + 1; ++y)
        {
            for (std::int32_t x = -2; x <= size + 1; ++x)
            {
                for (std::int32_t height = 1; height <= 4; ++height)
                {
                    for (std::int32_t width = 1; width <= 4; ++width)
                    {
                        bool any = false;
                        bool all = true;
                        for (std::int32_t row = y; row < y + height; ++row)
                        {
                            for (std::int32_t column = x; column < x + width; ++column)
                            {
                                const bool visible = viewport.contains({column, row});
                                any = any || visible;
                                all = all && visible;
                            }
                        }
                        checks.expect(
                            lithe::validateSurfacePlacement(definition,
                                                            {x, y, width, height},
                                                            SurfacePlacementPolicy::clipToViewport)
                                    .has_value() == any,
                            "circle intersection agrees with individual pixel centers");
                        checks.expect(
                            lithe::validateSurfacePlacement(definition,
                                                            {x, y, width, height},
                                                            SurfacePlacementPolicy::visibleShape)
                                    .has_value() == all,
                            "circle containment agrees with individual pixel centers");
                    }
                }
            }
        }
    }
}

} // namespace

int main()
{
    Checks checks{};
    checks.expect(popupChecks(), "anchored and explicitly declared popup fallback");
    checks.expect(hierarchyChecks(), "ancestor clipping, scroll reachability and malformed trees");
    checks.expect(nestedAndSafeScrollChecks(), "nested scroll intervals include safe placements");
    checks.expect(lithe::diagnosticId(SurfaceLayoutError{255U}) == "LVSL3000",
                  "unknown errors have reserved diagnostic ID");
    sharedFixtures(checks);
    sharedHierarchy(checks);
    exhaustiveSmallCircles(checks);
    exhaustiveNestedScroll(checks);
    return checks.failures == 0 ? 0 : 1;
}
