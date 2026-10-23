// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/font.hpp"
#include "lithe/painter.hpp"

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdio>

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

void verifyFace(const lithe::GeneratedFont& font, std::uint8_t size,
                std::uint32_t checksum)
{
    const auto& manifest = font.manifest();
    check(manifest.pixelSize == size && manifest.glyphCount == 99U &&
              manifest.coverage == lithe::GlyphCoverage::alpha8 &&
              manifest.dataChecksum == checksum,
          "generated face has the reviewed size, repertoire and coverage");
    lithe::Glyph glyph{};
    lithe::Glyph fallback{};
    check(font.glyph(U'W', glyph) && glyph.valid() && glyph.data.size() > 0U,
          "generated face exposes a drawable glyph");
    check(font.glyph(U'\U0001f642', fallback) && fallback.valid(),
          "generated face has a bounded missing-glyph fallback");
}
} // namespace

int main()
{
    using namespace lithe;
    check(&builtInFont<BuiltInFontBackend::generatedSans20>() == &generatedSans20() &&
              &builtInBoldFont<BuiltInFontBackend::generatedSans20>() == &generatedSans20Bold() &&
              &builtInFont<BuiltInFontBackend::generatedSans28>() == &generatedSans28() &&
              &builtInBoldFont<BuiltInFontBackend::generatedSans28>() == &generatedSans28Bold(),
          "each optional font size selects its own regular and bold resource");
    verifyFace(generatedSans20(), 20U, 0x862bb2fcU);
    verifyFace(generatedSans20Bold(), 20U, 0x18ec1ebaU);
    verifyFace(generatedSans28(), 28U, 0xc994b49fU);
    verifyFace(generatedSans28Bold(), 28U, 0xdfa5273fU);

    const TextMetrics small = Painter::measureText("012345", generatedSans20(), 1U);
    const TextMetrics large = Painter::measureText("012345", generatedSans28(), 1U);
    check(small.width > 0 && large.width > small.width &&
              large.height > small.height,
          "font sizes produce distinct measured geometry without runtime scaling");

    std::array<Rgb565Pixel, 180U * 40U> pixels{};
    DamageTracker damage{};
    Painter painter{Surface{PixelSurface<PixelFormat::rgb565>{pixels, 180, 40, 180}}, damage};
    painter.drawText({0, 0, 180, 40}, "012345", generatedSans28(),
                     Color::rgb(0xffffffU));
    const bool drew = std::ranges::any_of(pixels, [](Rgb565Pixel pixel) { return pixel != 0U; });
    check(drew, "large generated font paints RGB565 pixels");

    return failures == 0 ? 0 : 1;
}
