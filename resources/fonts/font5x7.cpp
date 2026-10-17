// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// Original compact glyphs created by Paul R. Decker in 2012.
#include "lithe/font.hpp"

#include <algorithm>
#include <array>
#include <cstddef>

namespace lithe
{

namespace
{

inline constexpr std::uint8_t kGlyphWidth{5U};
inline constexpr std::uint8_t kGlyphHeight{7U};
inline constexpr std::uint8_t kGlyphStrideBytes{1U};
inline constexpr std::uint8_t kGlyphAdvance{6U};
inline constexpr std::uint8_t kLineHeight{8U};

struct GlyphRecord
{
    char32_t code;
    std::array<std::uint8_t, kGlyphHeight> rows;
};

/*
 * Encoding overview
 * -----------------
 *
 * This font stores each character as a row-major, one-bit stencil rather than a
 * colored bitmap. A glyph is five pixels wide and seven pixels high, so its visible
 * shape contains only 35 on/off decisions. Each of the seven row values uses its
 * low five bits: 1 means draw the text color and 0 means leave the destination
 * untouched. Bit 4 is the leftmost pixel and bit 0 is the rightmost pixel.
 *
 * For example, the table entry for A is:
 *
 *     {'A', {14, 17, 17, 31, 17, 17, 17}}
 *
 * Reading each decimal value as a five-bit binary row reveals the glyph:
 *
 *     decimal   binary   pixels
 *          14   01110    .###.
 *          17   10001    #...#
 *          17   10001    #...#
 *          31   11111    #####
 *          17   10001    #...#
 *          17   10001    #...#
 *          17   10001    #...#
 *
 * Painter::drawText examines one row at a time. For columns 0 through 4 it tests
 * bits 4 through 0 respectively:
 *
 *     column       0   1   2   3   4
 *     bit          4   3   2   1   0
 *     bit value   16   8   4   2   1
 *
 * The font contains no color or framebuffer-format data. It supplies only the
 * coverage mask; Painter supplies the requested color and writes it through the
 * selected RGB565, RGB888, XRGB8888, or ARGB8888 surface specialization.
 *
 * The complete text-rendering pipeline is therefore:
 *
 *     character code point
 *         -> format-neutral 5x7 coverage shape
 *         -> scale expansion into destination pixels
 *         -> optional one-pixel synthetic-bold overstrike
 *         -> caller-selected logical RGBA Color
 *         -> optional source-over blending
 *         -> PixelFormatTraits<Format>::write()
 *         -> target framebuffer channel layout
 *
 * An enabled mask bit is not a red, green, blue, or alpha channel. It means "apply
 * the draw call's logical Color here." The same A record can therefore be red in one
 * drawText() call, green in the next, translucent blue over existing pixels in a
 * third, and encoded into a different framebuffer format without changing kGlyphs.
 * PixelFormatTraits performs the final channel mapping: RGB565 quantizes the logical
 * color to 5/6/5 bits, RGB888 stores three eight-bit channels, XRGB8888 stores opaque
 * B/G/R/X bytes, and ARGB8888 stores B/G/R/A bytes. A disabled mask bit leaves the
 * destination untouched.
 *
 * One drawText() call applies one Color to its entire text run. Multicolored text uses
 * separate runs with different colors. A genuinely multicolored glyph needs multiple
 * masks/layers or a color bitmap; a one-bit font mask cannot choose a different color
 * for each enabled pixel by itself.
 *
 * A tightly packed 5x7 glyph needs only 35 bits, or 4.375 bytes. This representation
 * deliberately spends seven bytes per glyph--one byte per row--to keep lookup and
 * rendering simple and to avoid extracting rows across byte boundaries. The current
 * 52 records therefore contain 364 bytes of row payload. Four-byte code points and
 * record alignment bring kGlyphs to 624 bytes in the current Arm build; that measured
 * object size is not a public ABI guarantee.
 *
 * The table does not represent all ASCII or Unicode glyphs. It stores space, selected
 * punctuation, digits, uppercase A through Z, and underscore. Font5x7::glyph maps
 * lowercase ASCII to the corresponding uppercase record, so 26 lowercase inputs cost
 * no additional bitmap storage. An unsupported code point reuses the question-mark
 * glyph. With five bits in each of seven rows, the encoding could describe 2^35
 * different shapes, but the table stores only one chosen shape for each supported
 * code point.
 *
 * Adding distinct lowercase shapes requires no renderer change. Add records keyed by
 * lowercase code points, for example:
 *
 *     {'a', {0, 0, 14, 1, 15, 17, 15}}
 *     {'b', {16, 16, 30, 17, 17, 17, 30}}
 *
 * Then remove the lowercase-to-uppercase conversion at the start of
 * Font5x7::glyph(); otherwise lookup canonicalizes a through z before the new records
 * can match. Adding all 26 lowercase rows costs 182 bitmap bytes. With the current
 * aligned GlyphRecord layout it adds 312 bytes to the Arm table. Lowercase shapes such
 * as g, j, p, q, and y need an explicit baseline decision: a bring-up font can fit the
 * descender into the existing seven rows, while typographically accurate descenders
 * require taller or glyph-specific metrics.
 *
 * Every returned Glyph has width 5 and advance 6, providing one blank column between
 * characters. lineHeight() returns 8, providing one blank row between text lines.
 * Scaling requires no additional font data. Painter's nested scale loops expand each
 * enabled source pixel into a scale-by-scale block; at scale 2, one source pixel becomes
 * a 2x2 square, and both the six-pixel advance and eight-pixel line height double.
 *
 * font5x7Bold() also shares this table. After Painter draws an enabled pixel at (x, y),
 * synthetic bold draws the same color again at (x + 1, y). This horizontal overstrike
 * thickens every stem without a second glyph table or a color-specific bold table. Both
 * the original and overstrike pixels pass through the same alpha blending and channel
 * mapping. Bolding changes coverage geometry only; it remains independent of logical
 * color and destination pixel format. It can consume the otherwise blank advance
 * column, which is acceptable for this fixed-width bring-up font; a generated font can
 * instead provide independently designed bold metrics and bitmaps.
 *
 * This intentionally small bring-up font keeps the examples independent of a
 * filesystem and runtime allocation. Applications that need lowercase shapes,
 * anti-aliasing, proportional metrics, kerning, broader Unicode coverage, or richer
 * typography can supply another Font backed by generated flash or ROM data.
 */
constexpr auto kGlyphs = std::to_array<GlyphRecord>({
    {' ', {0, 0, 0, 0, 0, 0, 0}},        {'!', {4, 4, 4, 4, 4, 0, 4}},
    {'%', {17, 2, 4, 8, 17, 0, 0}},      {'(', {2, 4, 8, 8, 8, 4, 2}},
    {')', {8, 4, 2, 2, 2, 4, 8}},        {'+', {0, 4, 4, 31, 4, 4, 0}},
    {',', {0, 0, 0, 0, 0, 4, 8}},        {'-', {0, 0, 0, 31, 0, 0, 0}},
    {'.', {0, 0, 0, 0, 0, 0, 4}},        {'/', {1, 2, 4, 8, 16, 0, 0}},
    {'0', {14, 17, 19, 21, 25, 17, 14}}, {'1', {4, 12, 4, 4, 4, 4, 14}},
    {'2', {14, 17, 1, 2, 4, 8, 31}},     {'3', {30, 1, 1, 14, 1, 1, 30}},
    {'4', {2, 6, 10, 18, 31, 2, 2}},     {'5', {31, 16, 16, 30, 1, 1, 30}},
    {'6', {14, 16, 16, 30, 17, 17, 14}}, {'7', {31, 1, 2, 4, 8, 8, 8}},
    {'8', {14, 17, 17, 14, 17, 17, 14}}, {'9', {14, 17, 17, 15, 1, 1, 14}},
    {':', {0, 4, 0, 0, 4, 0, 0}},        {'<', {1, 2, 4, 8, 4, 2, 1}},
    {'=', {0, 0, 31, 0, 31, 0, 0}},      {'>', {16, 8, 4, 2, 4, 8, 16}},
    {'?', {14, 17, 1, 2, 4, 0, 4}},      {'A', {14, 17, 17, 31, 17, 17, 17}},
    {'B', {30, 17, 17, 30, 17, 17, 30}}, {'C', {14, 17, 16, 16, 16, 17, 14}},
    {'D', {30, 17, 17, 17, 17, 17, 30}}, {'E', {31, 16, 16, 30, 16, 16, 31}},
    {'F', {31, 16, 16, 30, 16, 16, 16}}, {'G', {14, 17, 16, 23, 17, 17, 14}},
    {'H', {17, 17, 17, 31, 17, 17, 17}}, {'I', {14, 4, 4, 4, 4, 4, 14}},
    {'J', {7, 2, 2, 2, 2, 18, 12}},      {'K', {17, 18, 20, 24, 20, 18, 17}},
    {'L', {16, 16, 16, 16, 16, 16, 31}}, {'M', {17, 27, 21, 21, 17, 17, 17}},
    {'N', {17, 25, 21, 19, 17, 17, 17}}, {'O', {14, 17, 17, 17, 17, 17, 14}},
    {'P', {30, 17, 17, 30, 16, 16, 16}}, {'Q', {14, 17, 17, 17, 21, 18, 13}},
    {'R', {30, 17, 17, 30, 20, 18, 17}}, {'S', {15, 16, 16, 14, 1, 1, 30}},
    {'T', {31, 4, 4, 4, 4, 4, 4}},       {'U', {17, 17, 17, 17, 17, 17, 14}},
    {'V', {17, 17, 17, 17, 17, 10, 4}},  {'W', {17, 17, 17, 21, 21, 21, 10}},
    {'X', {17, 17, 10, 4, 10, 17, 17}},  {'Y', {17, 17, 10, 4, 4, 4, 4}},
    {'Z', {31, 1, 2, 4, 8, 16, 31}},     {'_', {0, 0, 0, 0, 0, 0, 31}},
});

class Font5x7 final : public Font
{
public:
    explicit constexpr Font5x7(bool bold = false) noexcept : mBold(bold)
    {
    }

    bool glyph(char32_t codepoint, Glyph& result) const noexcept override
    {
        if (codepoint >= U'a' && codepoint <= U'z')
        {
            codepoint -= U'a' - U'A';
        }
        // The portable standard-library iterator is not guaranteed to be a pointer on MSVC.
        // NOLINTNEXTLINE(readability-qualified-auto)
        const auto match = std::ranges::find_if(kGlyphs,
                                                [codepoint](const GlyphRecord& record)
                                                {
                                                    return record.code == codepoint;
                                                });
        if (match != kGlyphs.end())
        {
            result = {std::as_bytes(std::span{match->rows}),
                      kGlyphWidth,
                      kGlyphHeight,
                      kGlyphStrideBytes,
                      kGlyphAdvance,
                      0,
                      0,
                      GlyphCoverage::rowMask};
            return true;
        }
        if (codepoint != U'?')
        {
            return glyph(U'?', result);
        }
        result = {};
        return false;
    }

    std::uint8_t lineHeight() const noexcept override
    {
        return kLineHeight;
    }
    bool syntheticBold() const noexcept override
    {
        return mBold;
    }

private:
    bool mBold{false};
};

} // namespace

const Font& font5x7() noexcept
{
    static const Font5x7 font{};
    return font;
}

const Font& font5x7Bold() noexcept
{
    static const Font5x7 font{true};
    return font;
}

} // namespace lithe
