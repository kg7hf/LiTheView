// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/telemetry_views.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <optional>
#include <span>

namespace
{
int failures{};
void check(bool condition, const char* message)
{
    if (!condition)
    {
        std::printf("FAIL: %s\n", message);
        ++failures;
    }
}

void axis()
{
    constexpr std::array<lithe::AxisTick, 3U> ticks{{{0U, "LOW"},
                                                     {32768U, "MID"},
                                                     {65535U, "HIGH"}}};
    constexpr std::array<lithe::AxisTick, 2U> unsorted{{{32768U, "A"}, {1U, "B"}}};
    constexpr std::array<lithe::AxisTick, lithe::AxisView::kMaximumTicks + 1U> excessive{};
    lithe::Style style{};
    style.background = lithe::Color::rgb(0U);
    style.foreground = lithe::Color::rgb(0xffffffU);
    lithe::AxisView view{{0, 0, 32, 16}, lithe::font5x7(), style};
    check(view.setTicks(ticks), "accept ordered caller-owned scale ticks");
    check(!view.setTicks(unsorted) && !view.setTicks(excessive) &&
              view.ticks().data() == ticks.data(),
          "reject invalid tick arrays without changing the published scale");
    std::array<lithe::Rgb565Pixel, 32U * 16U> pixels{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{
        lithe::Surface{lithe::PixelSurface<lithe::PixelFormat::rgb565>{pixels, 32, 16, 32}},
        damage};
    view.render(painter);
    check(pixels[3U * 32U] == style.foreground.rgb565() &&
              pixels[3U * 32U + 31U] == style.foreground.rgb565(),
          "horizontal scale ticks align to both plot endpoints");
    view.setOrientation(lithe::AxisOrientation::vertical);
    pixels.fill(0U);
    view.render(painter);
    check(pixels[15U * 32U + 28U] == style.foreground.rgb565() &&
              pixels[28U] == style.foreground.rgb565(),
          "vertical scale maps minimum to bottom and maximum to top");
}

void markerRange()
{
    constexpr std::array<lithe::OverlayRange, 1U> ranges{{
        {16384U, 49151U, lithe::Color::rgba(0x2080ff80U)}}};
    constexpr std::array<lithe::OverlayRange, 1U> invalidRanges{{
        {49151U, 16384U, lithe::Color::rgb(0xffffffU)}}};
    constexpr std::array<lithe::OverlayMarker, 1U> markers{{
        {32768U, lithe::Color::rgb(0xffc000U), "MID"}}};
    constexpr std::array<lithe::OverlayMarker, 2U> duplicateMarkers{{
        {32768U, lithe::Color::rgb(0xffffffU), "A"},
        {32768U, lithe::Color::rgb(0xffffffU), "B"}}};
    lithe::MarkerRangeView view{{0, 0, 16, 8}};
    check(view.setRanges(ranges) && view.setMarkers(markers),
          "accept borrowed overlay ranges and markers");
    check(!view.setRanges(invalidRanges) && !view.setMarkers(duplicateMarkers) &&
              view.ranges().data() == ranges.data() && view.markers().data() == markers.data(),
          "invalid overlay data leaves the published snapshot intact");
    const auto base = lithe::Color::rgb(0x000020U).rgb565();
    std::array<lithe::Rgb565Pixel, 16U * 8U> pixels{};
    pixels.fill(base);
    lithe::DamageTracker damage{};
    lithe::Painter painter{
        lithe::Surface{lithe::PixelSurface<lithe::PixelFormat::rgb565>{pixels, 16, 8, 16}},
        damage};
    view.render(painter);
    check(pixels[7U * 16U + 1U] == base && pixels[7U * 16U + 4U] != base &&
              pixels[7U * 16U + 7U] == markers[0U].color.rgb565(),
          "range blends only inside bounds and marker reaches the far edge");
}

void meter()
{
    constexpr std::array<lithe::AxisTick, 3U> ticks{{
        {0U, "-120"}, {32768U, "-60"}, {65535U, "0"}}};
    constexpr std::array<lithe::MeterZone, 2U> zones{{
        {0U, 32768U, lithe::Color::rgb(0x00ff00U)},
        {32768U, 65535U, lithe::Color::rgb(0xff0000U)}}};
    constexpr std::array<lithe::MeterZone, 1U> invalidZones{{
        {50000U, 10000U, lithe::Color::rgb(0xffffffU)}}};
    lithe::Style style{};
    style.background = lithe::Color::rgb(0U);
    style.foreground = lithe::Color::rgb(0xffffffU);
    lithe::MeterView view{{0, 0, 20, 20}, lithe::font5x7(), style};
    check(view.setScale(-120, 0, ticks) && view.setZones(zones),
          "accept integer calibration and color zones");
    check(!view.setScale(0, 0, ticks) && !view.setZones(invalidZones) &&
              view.minimum() == -120 && view.zones().data() == zones.data(),
          "invalid meter settings leave calibration intact");
    view.setReading(-60);
    std::array<lithe::Rgb565Pixel, 20U * 20U> pixels{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{
        lithe::Surface{lithe::PixelSurface<lithe::PixelFormat::rgb565>{pixels, 20, 20, 20}},
        damage};
    view.render(painter);
    check(pixels[2U * 20U + 5U] == zones[0U].color.rgb565() &&
              pixels[2U * 20U + 15U] == style.background.rgb565(),
          "half-scale reading fills only the calibrated first half");
    view.setReading(20);
    pixels.fill(0U);
    view.render(painter);
    check(view.reading() == 20 && pixels[2U * 20U + 15U] == zones[1U].color.rgb565(),
          "overrange reading remains available while the meter saturates visually");
}

void plot()
{
    constexpr std::array<std::uint16_t, 2U> values{0U, 255U};
    std::array<lithe::Rgb565Pixel, 32U> pixels{};
    lithe::Style style{};
    style.background = lithe::Color::rgb(0U);
    style.foreground = lithe::Color::rgb(0x00ff00U);
    lithe::PlotView view{{0, 0, 8, 4}, style};
    check(view.setSamples(values), "accept borrowed plot snapshot");
    check(!view.setRange(5U, 5U) && view.minimum() == 0U,
          "reject invalid plot range without mutation");
    constexpr std::array<std::uint16_t, lithe::PlotView::kMaximumSamples + 1U> oversized{};
    check(!view.setSamples(oversized) && view.samples().data() == values.data(),
          "reject excessive plot work without changing the snapshot");
    lithe::DamageTracker damage{};
    lithe::Painter painter{
        lithe::Surface{lithe::PixelSurface<lithe::PixelFormat::rgb565>{pixels, 8, 4, 8}}, damage};
    view.render(painter);
    check(pixels[3U * 8U] == style.foreground.rgb565() &&
              pixels[7U] == style.foreground.rgb565(),
          "trace reaches both normalized endpoints");
    view.setMode(lithe::PlotMode::points);
    pixels.fill(0U);
    view.render(painter);
    check(view.mode() == lithe::PlotMode::points && pixels[1U * 8U + 4U] == 0U,
          "points mode does not connect distant samples");
    check(!view.setGrid(17U, 2U, lithe::Color::rgb(0xff0000U)) &&
              view.setGrid(2U, 2U, lithe::Color::rgb(0xff0000U)) &&
              view.setCursor(0U, lithe::Color::rgb(0x0000ffU)),
          "plot grid and cursor have bounded settings");
    pixels.fill(0U);
    view.render(painter);
    check(pixels[1U * 8U + 3U] == lithe::Color::rgb(0xff0000U).rgb565() &&
              pixels[1U * 8U] == lithe::Color::rgb(0x0000ffU).rgb565(),
          "plot grid and tuning cursor render as separate overlays");
    check(view.setCursor(std::nullopt, lithe::Color::rgb(0x0000ffU)),
          "cursor can be cleared using the public optional API");
    pixels.fill(0U);
    view.render(painter);
    check(pixels[1U * 8U] != lithe::Color::rgb(0x0000ffU).rgb565(),
          "cleared cursor no longer paints");
}

void secondaryTrace()
{
    constexpr std::array<std::uint16_t, 2U> live{0U, 0U};
    constexpr std::array<std::uint16_t, 2U> reference{255U, 255U};
    constexpr std::array<std::uint16_t, lithe::PlotView::kMaximumSamples + 1U> oversized{};
    const auto green = lithe::Color::rgb(0x00ff00U);
    const auto amber = lithe::Color::rgb(0xffaa00U);
    lithe::Style style{};
    style.background = lithe::Color::rgb(0U);
    style.foreground = green;
    lithe::PlotView view{{0, 0, 8, 4}, style};
    check(view.setSamples(live) && view.setSecondarySamples(reference, amber),
          "two caller-owned traces can share a plot");
    check(!view.setSecondarySamples(oversized, green) &&
              view.secondarySamples().data() == reference.data(),
          "oversized secondary trace leaves the published snapshot intact");
    std::array<lithe::Rgb565Pixel, 32U> pixels{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{
        lithe::Surface{lithe::PixelSurface<lithe::PixelFormat::rgb565>{pixels, 8, 4, 8}}, damage};
    view.render(painter);
    check(pixels[0U] == amber.rgb565() && pixels[3U * 8U] == green.rgb565(),
          "secondary trace and live trace render at independent levels");
    check(view.setSecondarySamples(std::span{reference}.first(1U), amber),
          "secondary trace may have its own sample count");
    pixels.fill(0U);
    view.render(painter);
    check(pixels[0U] == amber.rgb565() && pixels[7U] != amber.rgb565(),
          "single-sample reference uses its own horizontal extent");
    check(view.setSecondarySamples({}, amber), "empty snapshot clears secondary trace");
    pixels.fill(0U);
    view.render(painter);
    check(pixels[0U] != amber.rgb565() && pixels[3U * 8U] == green.rgb565(),
          "clearing the secondary trace keeps the primary visible");
}

void waterfall()
{
    static_assert(lithe::WaterfallView::requiredBytes(3, 2) == 4U);
    std::array<std::uint8_t, 4U> storage{0xa5U, 0xa5U, 0xa5U, 0xa5U};
    std::array<lithe::Color, lithe::WaterfallView::kPaletteSize> palette{};
    palette.fill(lithe::Color::rgb(0U));
    palette[1U] = lithe::Color::rgb(0x00ff00U);
    palette[15U] = lithe::Color::rgb(0xffffffU);
    lithe::Style style{};
    style.background = lithe::Color::rgb(0U);
    lithe::WaterfallView view{{0, 0, 3, 2}, storage, 3, 2, palette, style};
    check(view.validStorage(), "packed history accepts exact caller storage");
    check(!view.setPalette(std::span{palette}.first(15U)) && view.validStorage(),
          "reject incomplete palette without changing valid storage");
    constexpr std::array<std::uint8_t, 3U> first{0U, 0x1fU, 0xffU};
    constexpr std::array<std::uint8_t, 3U> second{0xffU, 0x10U, 0U};
    constexpr std::array<std::uint8_t, 3U> third{0x20U, 0x30U, 0x40U};
    constexpr std::array<std::uint8_t, 2U> shortRow{1U, 2U};
    check(!view.pushRow(shortRow) && view.rowCount() == 0,
          "reject short row without changing history");
    check(view.pushRow(first) && view.pushRow(second), "ingest two completed rows");
    check(view.sampleAt(0, 0) == 15U && view.sampleAt(0, 1) == 1U &&
              view.sampleAt(1, 2) == 15U,
          "newest row is at top and four-bit values round down");
    std::array<lithe::Rgb565Pixel, 6U> pixels{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{
        lithe::Surface{lithe::PixelSurface<lithe::PixelFormat::rgb565>{pixels, 3, 2, 3}}, damage};
    view.render(painter);
    check(pixels[0U] == palette[15U].rgb565() &&
              pixels[1U] == palette[1U].rgb565() &&
              pixels[5U] == palette[15U].rgb565(),
          "native destination receives newest and older palette rows");
    check(view.pushRow(third) && view.rowCount() == 2 &&
              view.sampleAt(0, 0) == 2U && view.sampleAt(1, 0) == 15U,
          "ring overwrites oldest row at fixed capacity");
    check(storage[1U] == 0x40U,
          "odd-width row padding is canonical and isolated from the next row");
    check(!view.sampleAt(2, 0) && !view.sampleAt(0, -1),
          "out-of-range history access fails closed");
    view.clear();
    check(view.rowCount() == 0 && !view.sampleAt(0, 0),
          "clear discards logical history without allocating");
    lithe::WaterfallView invalid{{0, 0, 3, 2}, std::span{storage}.first(3U), 3, 2, palette};
    check(!invalid.validStorage() && !invalid.pushRow(first),
          "insufficient storage cannot accept samples");
    lithe::WaterfallView generated{{0, 0, 3, 2}, style};
    check(!generated.validStorage() && !generated.pushRow(first),
          "designer-created waterfall stays unbound until the application supplies storage");
    check(!generated.bindStorage(std::span{storage}.first(3U), 3, 2, palette) &&
              !generated.validStorage(),
          "failed application binding does not partially configure the view");
    check(generated.bindStorage(storage, 3, 2, palette) && generated.validStorage() &&
              generated.pushRow(first) && generated.sampleAt(0, 1) == 1U,
          "typed generated accessor can bind and publish caller-owned history");
}

void nativePackedDestination()
{
    std::array<std::uint8_t, 2U> history{};
    std::array<lithe::Color, lithe::WaterfallView::kPaletteSize> palette{};
    palette.fill(lithe::Color::rgb(0U));
    palette[5U] = lithe::Color::rgb(0x550000U);
    palette[10U] = lithe::Color::rgb(0xaa0000U);
    palette[15U] = lithe::Color::rgb(0xff0000U);
    lithe::Style style{};
    style.background = lithe::Color::rgb(0U);
    lithe::WaterfallView view{{0, 0, 2, 2}, history, 2, 2, palette, style};
    constexpr std::array<std::uint8_t, 2U> older{0xa0U, 0xf0U};
    constexpr std::array<std::uint8_t, 2U> newest{0U, 0x50U};
    check(view.pushRow(older) && view.pushRow(newest), "native source rows accepted");
    std::array<std::byte, 1U> pixels{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{{pixels, lithe::PixelFormat::r2Packed2x2, 2, 2, 1U}, damage};
    view.render(painter);
    check(std::to_integer<std::uint8_t>(pixels[0U]) == 0x72U,
          "waterfall paints direct native 2x2 packed destination without RGB staging");
}

void siblingInvalidation()
{
    std::array<lithe::Rgb565Pixel, 8U> pixels{};
    lithe::Style rootStyle{};
    rootStyle.background = lithe::Color::rgb(0U);
    lithe::View root{{0, 0, 8, 1}, rootStyle};
    lithe::Style transparent{};
    transparent.background = lithe::Color::transparent();
    lithe::View first{{0, 0, 2, 1}, transparent};
    lithe::Style secondStyle{};
    secondStyle.background = lithe::Color::rgb(0x0000ffU);
    lithe::View second{{5, 0, 2, 1}, secondStyle};
    check(root.addSubview(first) && root.addSubview(second), "two views fit in root");
    lithe::DamageTracker damage{};
    lithe::Painter painter{
        lithe::Surface{lithe::PixelSurface<lithe::PixelFormat::rgb565>{pixels, 8, 1, 8}}, damage};
    root.render(painter);
    first.invalidate(); // Root is already dirty in a restricted left-hand region.
    secondStyle.background = lithe::Color::rgb(0xff0000U);
    second.setStyle(secondStyle);
    root.render(painter);
    check(pixels[5U] == secondStyle.background.rgb565(),
          "opaque sibling invalidation expands an existing parent dirty region");
}
} // namespace

int main()
{
    axis();
    markerRange();
    meter();
    plot();
    secondaryTrace();
    waterfall();
    nativePackedDestination();
    siblingInvalidation();
    return failures == 0 ? 0 : 1;
}
