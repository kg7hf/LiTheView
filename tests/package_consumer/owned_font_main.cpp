// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include <lithe/lithe.hpp>

#include <array>

namespace
{
// A consumer owns both the glyph data and its lifetime. No bundled font is used.
constexpr std::array<lithe::GeneratedGlyphRecord, 1U> kGlyphs{
    lithe::GeneratedGlyphRecord{U'A', 0U, 1U, 1U, 1U, 2U, 0, 0}};
constexpr std::array<std::byte, 1U> kCoverage{std::byte{0x80}};
constexpr lithe::GeneratedFontData kFontData{
    kGlyphs, kCoverage, {.coverage = lithe::GlyphCoverage::alpha1}, U'A', 2U, 1U, 1U, 1U};
} // namespace

int main()
{
    const lithe::GeneratedFont font{kFontData};
    std::array<lithe::ViewSpec, 2U> specs{};
    specs[0].frame = {0, 0, 8, 8};
    specs[1].parent = 0U;
    specs[1].kind = lithe::ViewKind::label;
    specs[1].frame = {0, 0, 8, 8};
    specs[1].text = "A";
    lithe::StaticLayout<2U> layout;
    if (!layout.build(specs, font) || layout.root() == nullptr)
    {
        return 1;
    }
    std::array<lithe::Pixel, 64U> pixels{};
    lithe::DamageTracker damage;
    lithe::Painter painter{{pixels, 8, 8, 8}, damage};
    painter.drawText({0, 0, 8, 8}, "A", font, lithe::Color::rgb(0xff0000U),
                     {lithe::HorizontalAlign::left, lithe::VerticalAlign::top});
    return pixels.front() == lithe::Color::rgb(0xff0000U).rgb565() && !damage.regions().empty() ? 0 : 2;
}
