// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/lithe.hpp"

#include <array>
#include <cstdio>

namespace
{

constexpr lithe::DisplayDefinition kRound{"test.round",
                                          {240, 240},
                                          lithe::ViewportShape::circle,
                                          {36, 36, 168, 168},
                                          lithe::PixelFormat::rgb565,
                                          lithe::DisplayAddressSpace::pixels};
constexpr lithe::LogicalInputConfiguration kEncoder{false, false, false, true, true};
constexpr lithe::SurfaceInputRequirements kRequirements{true, true, false};

constexpr std::array<lithe::ViewSpec, 2U> fixture() noexcept
{
    std::array<lithe::ViewSpec, 2U> specs{};
    specs[0].frame = {0, 0, 240, 240};
    specs[1].parent = 0U;
    specs[1].kind = lithe::ViewKind::button;
    specs[1].frame = {80, 80, 80, 30};
    specs[1].text = "OK";
    return specs;
}

constexpr auto kSpecs = fixture();
static_assert(lithe::validateLayoutSurface(kRound, kSpecs, kEncoder, kRequirements));
constexpr auto kMissingInput = lithe::validateLayoutSurface(kRound, kSpecs, {}, kRequirements);
static_assert(!kMissingInput &&
              kMissingInput.error().error == lithe::SurfaceLayoutError::missingActivation);

bool checkXmlParity() noexcept
{
    constexpr std::string_view xml =
        "<panel frame=\"0,0,240,240\"><button frame=\"80,80,80,30\" text=\"OK\"/></panel>";
    std::array<lithe::ViewSpec, 2U> parsed{};
    const auto read = lithe::XmlLayoutParser::parse(xml, parsed);
    if (!read || read.nodeCount != parsed.size() ||
        !lithe::validateLayoutSurface(kRound, parsed, kEncoder, kRequirements))
    {
        return false;
    }
    auto written = kSpecs;
    parsed[1].frame = {100, 10, 20, 20};
    written[1].frame = parsed[1].frame;
    const auto parsedResult = lithe::validateLayoutSurface(kRound, parsed);
    const auto writtenResult = lithe::validateLayoutSurface(kRound, written);
    if (parsedResult || writtenResult ||
        parsedResult.error().nodeIndex != writtenResult.error().nodeIndex ||
        lithe::diagnosticId(parsedResult.error().error) != "LVSL3004" ||
        parsedResult.error().error != writtenResult.error().error)
    {
        return false;
    }
    constexpr std::array policies{lithe::SurfacePlacementPolicy::clipToViewport,
                                  lithe::SurfacePlacementPolicy::visibleShape};
    return lithe::validateLayoutSurface(kRound, parsed, {}, {}, policies).has_value() &&
           lithe::validateLayoutSurface(kRound, written, {}, {}, policies).has_value();
}

bool checkConstruction() noexcept
{
    lithe::StaticLayout<2U> layout;
    if (!lithe::buildSurfaceLayout(layout, kRound, kSpecs, kEncoder, kRequirements))
    {
        return false;
    }
    lithe::View* originalRoot = layout.root();
    auto invalid = kSpecs;
    invalid[1].frame = {0, 0, 20, 20};
    const auto rejected = lithe::buildSurfaceLayout(layout, kRound, invalid);
    if (rejected || layout.root() != originalRoot || originalRoot->subviewCount() != 1U)
    {
        return false;
    }
    const auto* surface = std::get_if<lithe::SurfaceLayoutFailure>(&rejected.error());
    if (surface == nullptr || surface->error != lithe::SurfaceLayoutError::outsideSafeArea)
    {
        return false;
    }
    lithe::StaticLayout<1U> tooSmall;
    const auto capacity = lithe::buildSurfaceLayout(tooSmall, kRound, kSpecs);
    if (capacity)
    {
        return false;
    }
    const auto* construction = std::get_if<lithe::LayoutResult>(&capacity.error());
    return construction != nullptr && construction->error == lithe::LayoutError::capacity;
}

} // namespace

int main()
{
    if (!checkXmlParity() || !checkConstruction())
    {
        std::puts("Surface layout adapter or XML parity failed.");
        return 1;
    }
    return 0;
}
