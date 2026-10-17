// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include <lithe/lithe.hpp>

int main()
{
    const lithe::GeneratedFont& font = lithe::generatedSans();
    lithe::Glyph glyph{};
    const lithe::TextMetrics metrics = lithe::Painter::measureText("Caf\xc3\xa9", font);
    const lithe::GeneratedFontManifest& manifest = font.manifest();
    return font.glyph(U'\u00e9', glyph) && glyph.valid() && metrics.validUtf8 &&
                   metrics.width > 0 && manifest.glyphCount == 99U
               ? 0
               : 1;
}
