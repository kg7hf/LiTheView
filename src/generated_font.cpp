// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/font.hpp"

#include <algorithm>
#include <cstddef>

namespace lithe
{

namespace
{

/*
 * Generated ROM font walkthrough
 * ------------------------------
 *
 * A generated font separates typography work from the embedded runtime. The
 * converter rasterizes an outline font once on a development machine; the target
 * receives ordinary const C++ tables. Measuring and drawing text then require no
 * font file, filesystem, parser, cache, heap allocation, or operating-system font
 * service.
 *
 * The implementation has two deliberately separate layers:
 *
 *   1. This file implements the reusable GeneratedFont reader. It knows how to find
 *      a Unicode code point, validate the corresponding byte range, and expose a
 *      borrowed Glyph to Painter.
 *   2. A generated translation unit, such as generated_sans_regular.cpp, owns one
 *      sorted record table, one flat coverage-byte table, its manifest and metrics,
 *      and a function returning a process-lifetime GeneratedFont.
 *
 * Keeping the reader in LitheView::Core and the checked tables in
 * LitheView::GeneratedFonts lets a small application link only the compact 5 by 7
 * font. A linker never needs to pull the generated 20 KiB payload into a product
 * that does not reference generatedSans() or generatedSansBold().
 *
 * Creating or replacing a generated face
 * ---------------------------------------
 *
 * Start with product decisions, not with the converter command:
 *
 *   - choose a font file whose license permits the intended distribution;
 *   - choose the pixel size actually needed by the target display;
 *   - bound the Unicode repertoire to the strings the product must display;
 *   - include U+003F for deterministic fallback and normally U+FFFD for invalid
 *     UTF-8 replacement;
 *   - choose Alpha1 for the smallest binary mask or Alpha8 for anti-aliasing; and
 *   - generate real regular/bold files when designed bold typography is required.
 *
 * The checked regular face is reproduced from PowerShell with:
 *
 *     .\tools\convert-font.ps1 `
 *       -InputFont assets\source\DejaVuSans.ttf `
 *       -OutputSource resources\fonts\generated_sans_regular.cpp `
 *       -OutputManifest resources\fonts\generated_sans_regular.json `
 *       -Symbol GeneratedSansRegular `
 *       -EntryPoint generatedSans `
 *       -PixelSize 14 `
 *       -FontStyle Regular `
 *       -Coverage Alpha8
 *
 * Use -Codepoints to supply a comma-separated set of individual values and ranges,
 * for example U+0020-U+007E,U+00B0,U+00E9,U+2026,U+FFFD. The bold face uses the bold
 * input file, GeneratedSansBold symbol, generatedSansBold entry point, and Bold
 * style. The source file is the compiled ROM resource; the JSON file is review and
 * provenance evidence. Do not edit either generated output by hand. Change the
 * explicit inputs and regenerate both outputs so source hash, repertoire, sizes,
 * checksum, and code remain synchronized.
 *
 * The converter performs these steps offline:
 *
 *   outline file -> selected Unicode code points -> GDI+ paths -> cropped coverage
 *   rectangles -> sorted GeneratedGlyphRecord array -> flat coverage byte array
 *   -> C++ translation unit plus JSON manifest
 *
 * GDI+ is a converter implementation detail, not a target dependency. Reproduction
 * is tied to the manifest's source SHA-256, not merely to a family name that could
 * resolve to a different file on another workstation.
 *
 * How the compact record and byte tables fit together
 * --------------------------------------------------
 *
 * All glyph pixels are concatenated into one coverage array. A record says where
 * one glyph begins and how to interpret its rectangle. For example, the checked
 * regular A currently has this record:
 *
 *     {char32_t{0x00000041U}, 2313U, 10U, 11U, 10U, 10U, 0, 2}
 *
 * Read the fields from left to right as:
 *
 *     code point       U+0041, LATIN CAPITAL LETTER A
 *     data offset      byte 2313 in the shared coverage array
 *     width/height     a 10 by 11 source-pixel rectangle
 *     row stride       10 bytes from one Alpha8 row to the next
 *     advance          move the text pen 10 pixels after drawing A
 *     X/Y offsets      draw the rectangle at pen + (0, 2)
 *
 * makeGlyph() therefore borrows coverage.subspan(2313, 10 * 11). The next record can
 * begin immediately after those 110 bytes; there is no pointer per glyph and no
 * runtime allocation. Records are sorted by code point so findRecord() can use
 * binary search. The fixed record is 16 bytes on every supported target: 32-bit code
 * point and offset, compact 8-bit dimensions/advance, signed 8-bit offsets, and
 * alignment. The converter fails instead of truncating a value that does not fit.
 *
 * Alpha8 stores one value from 0 through 255 for every source pixel. Zero leaves the
 * destination untouched, 255 applies the complete draw color, and intermediate
 * values apply proportional coverage. Alpha1 packs eight left-to-right pixels into
 * each byte, most-significant bit first; its minimum row stride is
 * (width + 7) / 8. The older rowMask format is reserved for the compact font.
 *
 * A zero stride is also a valid public shorthand for that minimum stride. The
 * converter emits explicit strides, but caller-authored tables may use zero. The
 * shared minimumGlyphStrideBytes() rule is consequently used by Glyph::valid(),
 * makeGlyph(), and Painter. Keeping one rule prevents the reader from slicing one
 * byte range while the renderer walks another.
 *
 * Coverage is shape, not color. Painter multiplies the draw Color alpha by the
 * glyph coverage, blends the resulting logical color, and asks PixelFormatTraits to
 * encode the target RGB565, RGB888, XRGB8888, or ARGB8888 channels. The same table
 * can render red, green, translucent blue, or any other per-run color. Multicolored
 * text uses separate runs; a genuinely multicolored glyph needs layered masks or a
 * color-bitmap resource.
 *
 * Runtime lookup and failure behavior
 * -----------------------------------
 *
 * Painter decodes UTF-8 into a char32_t before calling Font::glyph(). GeneratedFont
 * then performs the following bounded path:
 *
 *     requested code point
 *         -> lower_bound() in the sorted record span
 *         -> declared fallback code point when no exact record exists
 *         -> checked offset, stride, height, and byte-count arithmetic
 *         -> borrowed Glyph span
 *         -> Painter clipping, integer scaling, coverage blending, and pixel write
 *
 * Missing both the requested and fallback records returns false and clears the
 * result. makeGlyph() subtracts the offset only after proving it is within the
 * coverage span, proves stride * height fits by division before multiplying, and
 * asks Glyph::valid() to check the final descriptor. Corrupt or mismatched tables
 * therefore fail closed instead of exposing an out-of-bounds span.
 *
 * Font-level metrics are separate from glyph rectangles. lineHeight advances the
 * line origin; baseline records the design baseline; underlinePosition and
 * underlineThickness place a run-level underline. Each glyph's signed offsets place
 * its cropped rectangle relative to the line origin, which permits proportional
 * widths, side bearings, accents, and descenders without padding every bitmap to a
 * common cell.
 *
 * Using the checked faces
 * -----------------------
 *
 * Link the generated resource archive and keep a Font reference in the views that
 * need it:
 *
 *     target_link_libraries(my_application PRIVATE LitheView::GeneratedFonts)
 *
 *     const lithe::Font& regular = lithe::generatedSans();
 *     const lithe::Font& bold = lithe::generatedSansBold();
 *     const lithe::TextMetrics size =
 *         lithe::Painter::measureText("Status", regular, 1U);
 *     painter.drawText(frame, "Status", regular, lithe::Color::rgb(0x20c080U));
 *
 * A closed product configuration can select the backend without a runtime branch:
 *
 *     constexpr auto kBackend = lithe::BuiltInFontBackend::generatedSans;
 *     const lithe::Font& font = lithe::builtInFont<kBackend>();
 *
 * The example-only LITHEVIEW_DEMO_FONT_BACKEND CMake option selects the same template
 * path for LitheView's demos. It is not required by library consumers.
 *
 * Absorbing the pipeline into another project
 * -------------------------------------------
 *
 * A developer bringing this code into a unique target needs these pieces:
 *
 *   1. Compile font.hpp and generated_font.cpp with the portable LitheView core.
 *   2. Generate or copy the desired face translation units and compile them into a
 *      separate ROM/resource archive when optional dead stripping matters.
 *   3. Declare the generated entry points used by the application and link their
 *      translation units. The checked declarations are generatedSans() and
 *      generatedSansBold() in font.hpp.
 *   4. Keep GeneratedFontData, its record span, and its coverage span alive longer
 *      than every GeneratedFont and every view that borrows that font. Generated
 *      translation units satisfy this with namespace-scope const tables and a
 *      function-local static GeneratedFont.
 *   5. Review the manifest's license/source identity, repertoire, checksum, and ROM
 *      estimate, then measure the actual linked image on the target.
 *   6. Test required strings, fallback, clipping, framebuffer formats, and any tiled
 *      renderer; do not infer target cost or visual quality from a host screenshot.
 *
 * For a product-specific entry point, give the converter a unique -Symbol and
 * -EntryPoint, declare the resulting function with the product's resource
 * declarations, and return that Font reference to views. No registry or global font
 * manager is required. Large immutable tables remain application-owned resources.
 *
 * Regular and designed-bold files have independent metrics and coverage. That costs
 * more ROM but preserves the typeface designer's stroke and spacing choices.
 * GeneratedFont can also request Painter's one-pixel synthetic overstrike through
 * its constructor when a product consciously prefers smaller ROM to designed bold.
 * Integer scale expands every source pixel to a scale-by-scale block; it does not
 * re-rasterize the outline, so generate the intended base pixel size when typography
 * quality matters and reserve scaling for deliberate UI enlargement.
 *
 * This is intentionally a bounded renderer, not a desktop font engine. It does not
 * perform kerning, shaping, bidirectional layout, language-specific line breaking,
 * or fallback across font families. Products needing those features should add a
 * reviewed offline shaping/resource stage or another bounded Font implementation,
 * rather than introducing filesystem access or runtime allocation into this path.
 */
const GeneratedGlyphRecord* findRecord(const GeneratedFontData& data, char32_t codepoint) noexcept
{
    const auto match = std::ranges::lower_bound(data.glyphs,
                                                codepoint,
                                                {},
                                                [](const GeneratedGlyphRecord& record) noexcept
                                                {
                                                    return record.codepoint;
                                                });
    return match != data.glyphs.end() && match->codepoint == codepoint ? &*match : nullptr;
}

bool makeGlyph(const GeneratedFontData& data,
               const GeneratedGlyphRecord& record,
               Glyph& result) noexcept
{
    const std::size_t offset = record.dataOffset;
    const std::size_t stride =
        record.strideBytes == 0U
            ? detail::minimumGlyphStrideBytes(record.width, data.manifest.coverage)
            : record.strideBytes;
    const std::size_t height = record.height;
    if (offset > data.coverage.size())
    {
        result = {};
        return false;
    }
    const std::size_t remaining = data.coverage.size() - offset;
    if (height != 0U && stride > remaining / height)
    {
        result = {};
        return false;
    }
    const std::size_t byteCount = stride * height;
    if (byteCount > remaining)
    {
        result = {};
        return false;
    }
    result = {data.coverage.subspan(offset, byteCount),
              record.width,
              record.height,
              record.strideBytes,
              record.advance,
              record.xOffset,
              record.yOffset,
              data.manifest.coverage};
    return result.valid();
}

} // namespace

bool GeneratedFont::glyph(char32_t codepoint, Glyph& result) const noexcept
{
    const GeneratedGlyphRecord* record = findRecord(mData, codepoint);
    if (record == nullptr && codepoint != mData.fallbackCodepoint)
    {
        record = findRecord(mData, mData.fallbackCodepoint);
    }
    if (record == nullptr)
    {
        result = {};
        return false;
    }
    return makeGlyph(mData, *record, result);
}

std::uint8_t GeneratedFont::lineHeight() const noexcept
{
    return mData.lineHeight;
}

std::uint8_t GeneratedFont::baseline() const noexcept
{
    return mData.baseline;
}

std::uint8_t GeneratedFont::underlinePosition() const noexcept
{
    return mData.underlinePosition;
}

std::uint8_t GeneratedFont::underlineThickness() const noexcept
{
    return mData.underlineThickness;
}

bool GeneratedFont::syntheticBold() const noexcept
{
    return mSyntheticBold;
}

} // namespace lithe
