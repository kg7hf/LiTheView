// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/lithe.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <limits>
#include <span>
#include <string_view>

namespace
{

constexpr auto kRed = lithe::Color::rgb(0xff0000U);
constexpr auto kGuard = std::byte{0xa5U};
constexpr lithe::Size kFrameSize{5, 3};
constexpr auto kViewport = lithe::Viewport::rectangle({0, 0, 5, 3});
constexpr std::array kFormats{lithe::PixelFormat::rrrr2222, lithe::PixelFormat::ryry2222};
static_assert(lithe::supportsDestination(kFormats[0]) && lithe::supportsDestination(kFormats[1]));

class Checks final
{
public:
    void expect(bool condition, std::string_view description)
    {
        if (!condition)
        {
            std::cerr << "FAIL: " << description << '\n';
            ++failures;
        }
    }

    int failures{0};
};

template <std::size_t Count>
bool equalBytes(std::span<const std::byte> actual,
                const std::array<std::uint8_t, Count>& expected) noexcept
{
    if (actual.size() != expected.size())
    {
        return false;
    }
    for (std::size_t index = 0U; index < expected.size(); ++index)
    {
        if (std::to_integer<std::uint8_t>(actual[index]) != expected[index])
        {
            return false;
        }
    }
    return true;
}

template <lithe::PixelFormat Format> void checkTraits(Checks& checks)
{
    using Traits = lithe::PixelFormatTraits<Format>;
    constexpr bool kRedYellow = Format == lithe::PixelFormat::ryry2222;
    static_assert(Traits::kBlockWidth == 1 && Traits::kBlockHeight == 1 &&
                  Traits::kBytesPerBlock == 1U && !Traits::kPreservesAlpha);
    constexpr auto layout = lithe::storageLayout(Format, {3, 3}, 5U, {-1, -3});
    static_assert(layout.has_value() && layout->rowBytes == 3U && layout->rowCount == 3U &&
                  layout->usedBytes == 13U && lithe::bytesPerPixel(Format) == 1U);

    std::array<std::byte, 3U> bytes{kGuard, std::byte{0x72U}, kGuard};
    constexpr std::array<std::uint8_t, 4U> intensities{0U, 85U, 170U, 255U};
    for (std::size_t field = 0U; field < intensities.size(); ++field)
    {
        const auto color = Traits::readEmitter(bytes, 1U, field);
        const auto green = kRedYellow && (field == 0U || field == 3U) ? intensities[field] : 0U;
        checks.expect(color.red == intensities[field] && color.green == green && color.blue == 0U &&
                          color.alpha == 255U,
                      "0x72 reads TL/TR/BL/BR levels 0/1/2/3 with the emitter's actual color");
    }
    constexpr std::array<std::uint8_t, 4U> isolated{0x0cU, 0xc0U, 0x03U, 0x30U};
    for (std::size_t lit = 0U; lit < isolated.size(); ++lit)
    {
        bytes[1] = std::byte{isolated[lit]};
        for (std::size_t field = 0U; field < isolated.size(); ++field)
        {
            checks.expect(Traits::readEmitter(bytes, 1U, field).red == (field == lit ? 255U : 0U),
                          "each stored field decodes independently from the other three emitters");
        }
    }

    for (std::uint32_t value = 0U; value <= 255U; ++value)
    {
        bytes[1] = std::byte{lithe::detail::lowByte(value)};
        Traits::compose(bytes, 1U, {123U, 61U, 212U, 0U});
        checks.expect(std::to_integer<std::uint32_t>(bytes[1]) == value,
                      "alpha-zero composition preserves all 256 native cell encodings exactly");
    }
    bytes[1] = std::byte{0x72U};
    Traits::compose(bytes, 1U, {0U, 0U, 0U, 128U});
    checks.expect(bytes[1] == std::byte{0x11U},
                  "half-covered black retains unequal 0/1/2/3 levels as 0/0/1/1");
    bytes[1] = std::byte{0xffU};
    Traits::compose(bytes, 1U, {0U, 0U, 0U, 128U});
    checks.expect(bytes[1] == std::byte{0x55U},
                  "half-covered black over four full emitters yields four level-one emitters");
    checks.expect(bytes.front() == kGuard && bytes.back() == kGuard,
                  "trait read/write operations stay at their supplied byte offset");

    constexpr std::array<std::uint8_t, 8U> redValues{0U, 42U, 43U, 127U, 128U, 212U, 213U, 255U};
    constexpr std::array<std::uint8_t, 8U> redCell{
        0U, 0U, 0x55U, 0x55U, 0xaaU, 0xaaU, 0xffU, 0xffU};
    constexpr std::array<std::uint8_t, 8U> dualCell{
        0U, 0U, 0x41U, 0x41U, 0x82U, 0x82U, 0xc3U, 0xc3U};
    for (std::size_t sample = 0U; sample < redValues.size(); ++sample)
    {
        Traits::write(bytes, 1U, {redValues[sample], 0U, 255U, 255U});
        checks.expect(std::to_integer<std::uint8_t>(bytes[1]) ==
                          (kRedYellow ? dualCell[sample] : redCell[sample]),
                      "opaque red quantization rounds at 43/128/213 and ignores blue");
    }
    Traits::write(bytes, 1U, {255U, 170U, 0U, 255U});
    checks.expect(bytes[1] == (kRedYellow ? std::byte{0x69U} : std::byte{0xffU}),
                  "orange maps to red-one/yellow-two or four red-three emitters");
    Traits::write(bytes, 1U, {0U, 255U, 255U, 255U});
    checks.expect(bytes[1] == std::byte{}, "green and blue alone produce no emitter output");
    Traits::write(bytes, 1U, {255U, 255U, 255U, 255U});
    checks.expect(bytes[1] == (kRedYellow ? std::byte{0x3cU} : std::byte{0xffU}),
                  "white maps to yellow-only or all-red according to the selected cell format");
}

void checkPainterCoverageAndClip(Checks& checks, lithe::PixelFormat format)
{
    std::array<std::byte, 9U> bytes{};
    bytes.fill(kGuard);
    const lithe::Surface surface{std::span{bytes}.subspan(1U, 7U), format, 3, 2, 4U, {-3, 5}};
    lithe::DamageTracker damage{};
    lithe::Painter painter{surface, damage};
    checks.expect(painter.valid() && surface.usedBytes() == 7U,
                  "native cells accept odd extents/origins and exact last-row storage");
    painter.setClip({-2, 5, 1, 2});
    painter.fillRect(surface.bounds(), kRed);
    const auto red = format == lithe::PixelFormat::rrrr2222 ? 0xffU : 0xc3U;
    checks.expect(equalBytes(std::span{bytes},
                             std::array<std::uint8_t, 9U>{0xa5U,
                                                          0xa5U,
                                                          lithe::detail::lowByte(red),
                                                          0xa5U,
                                                          0xa5U,
                                                          0xa5U,
                                                          lithe::detail::lowByte(red),
                                                          0xa5U,
                                                          0xa5U}),
                  "logical cell clips preserve neighbors, row padding, and span guards");

    std::array<std::byte, 1U> cell{};
    lithe::DamageTracker cellDamage{};
    lithe::Painter cellPainter{{cell, format, 1, 1, 1U}, cellDamage};
    for (std::uint32_t value = 0U; value <= 255U; ++value)
    {
        cell[0] = std::byte{lithe::detail::lowByte(value)};
        cellDamage.clear();
        cellPainter.fillRect({0, 0, 1, 1}, {255U, 255U, 255U, 0U});
        checks.expect(std::to_integer<std::uint32_t>(cell[0]) == value && cellDamage.empty(),
                      "Painter's transparent fill preserves every cell byte and records no damage");
    }
    cell[0] = std::byte{0x72U};
    constexpr std::array mask{std::byte{0x80U}};
    const lithe::Bitmap alpha1{mask, 1, 1, 1U, lithe::BitmapFormat::alpha1, {0U, 0U, 0U, 128U}};
    cellPainter.drawBitmap({0, 0}, alpha1);
    checks.expect(cell[0] == std::byte{0x11U},
                  "alpha1 bitmap tint composes with unequal emitters without RGB reconstruction");
}

// Independent source fixture: each byte stores TL/TR/BL/BR, with one padding
// byte between the two block rows. Zero-valued samples are opaque black.
constexpr std::array kNativeBytes{
    std::byte{0x72U}, std::byte{0x1bU}, kGuard, std::byte{0xc3U}, std::byte{0x3cU}};
constexpr std::array<std::uint8_t, 16U> kNativeLevels{
    0U, 1U, 2U, 0U, 2U, 3U, 3U, 1U, 0U, 3U, 3U, 0U, 3U, 0U, 0U, 3U};
constexpr lithe::Bitmap kNativeBitmap{kNativeBytes, 4, 4, 3U, lithe::BitmapFormat::r2Packed2x2};
static_assert(kNativeBitmap.valid());
static_assert(kNativeBitmap.minimumStrideBytes() == 2U);

void checkNativeBitmapValidation(Checks& checks)
{
    auto bitmap = kNativeBitmap;
    bitmap.width = 3;
    checks.expect(!bitmap.valid(), "packed bitmap rejects incomplete horizontal blocks");
    bitmap = kNativeBitmap;
    bitmap.height = 3;
    checks.expect(!bitmap.valid(), "packed bitmap rejects incomplete vertical blocks");
    bitmap.height = 1;
    checks.expect(!bitmap.valid(), "packed bitmap rejects a single logical sample row");
    bitmap = kNativeBitmap;
    bitmap.strideBytes = 1U;
    checks.expect(!bitmap.valid(), "packed bitmap rejects overlapping block rows");
    bitmap = kNativeBitmap;
    bitmap.data = std::span{kNativeBytes}.first(4U);
    checks.expect(!bitmap.valid(), "packed bitmap requires the complete final active block row");
    bitmap = kNativeBitmap;
    bitmap.strideBytes = std::numeric_limits<std::size_t>::max();
    checks.expect(!bitmap.valid(), "packed bitmap rejects an unrepresentable required span");
    constexpr std::array tight{
        std::byte{0x72U}, std::byte{0x1bU}, std::byte{0xc3U}, std::byte{0x3cU}};
    const lithe::Bitmap tightBitmap{tight, 4, 4, 0U, lithe::BitmapFormat::r2Packed2x2};
    checks.expect(tightBitmap.valid(), "zero bitmap stride selects the minimum packed row stride");
}

void checkNativeBitmap(Checks& checks, lithe::PixelFormat format)
{
    const std::uint32_t levelUnit = format == lithe::PixelFormat::rrrr2222 ? 0x55U : 0x41U;
    std::array<std::byte, 16U> bytes{};
    bytes.fill(kGuard);
    lithe::DamageTracker damage{};
    lithe::Painter painter{{bytes, format, 4, 4, 4U}, damage};
    painter.drawBitmap({0, 0}, kNativeBitmap);
    for (std::size_t index = 0U; index < bytes.size(); ++index)
    {
        checks.expect(std::to_integer<std::uint32_t>(bytes[index]) ==
                          kNativeLevels[index] * levelUnit,
                      "native R2 bitmap samples decode directly into opaque destination cells");
    }

    std::array<std::byte, 36U> scaled{};
    scaled.fill(kGuard);
    lithe::DamageTracker scaledDamage{};
    lithe::Painter scaledPainter{{scaled, format, 6, 6, 6U}, scaledDamage};
    scaledPainter.setClip({0, 0, 5, 5});
    scaledPainter.drawBitmap({-1, -1}, kNativeBitmap, 2U);
    for (std::size_t row = 0U; row < 6U; ++row)
    {
        for (std::size_t column = 0U; column < 6U; ++column)
        {
            const auto actual = std::to_integer<std::uint32_t>(scaled[row * 6U + column]);
            const auto expected =
                row < 5U && column < 5U
                    ? kNativeLevels[((row + 1U) / 2U) * 4U + (column + 1U) / 2U] * levelUnit
                    : 0xa5U;
            checks.expect(
                actual == expected,
                "native bitmap scaling/clipping keeps source block and field offsets correct");
        }
    }
}

void checkOtherDestinations(Checks& checks)
{
    std::array<std::byte, 5U> r2Bytes{};
    r2Bytes.fill(kGuard);
    lithe::DamageTracker damage{};
    lithe::Painter packed{{r2Bytes, lithe::PixelFormat::r2Packed2x2, 4, 4, 3U}, damage};
    packed.drawBitmap({0, 0}, kNativeBitmap);
    checks.expect(
        r2Bytes == kNativeBytes,
        "native R2 bitmap round-trips its logical samples and preserves destination padding");
    std::array<lithe::Xrgb8888Pixel, 16U> rgbBytes{};
    lithe::DamageTracker rgbDamage{};
    lithe::Painter rgb{
        {std::as_writable_bytes(std::span{rgbBytes}), lithe::PixelFormat::xrgb8888, 4, 4, 16U},
        rgbDamage};
    rgb.drawBitmap({0, 0}, kNativeBitmap);
    for (std::size_t index = 0U; index < rgbBytes.size(); ++index)
    {
        checks.expect(rgbBytes[index].red == kNativeLevels[index] * 85U &&
                          rgbBytes[index].green == 0U && rgbBytes[index].blue == 0U &&
                          rgbBytes[index].unused == 255U,
                      "packed native source remains available to a conventional RGB destination");
    }
}

class FixtureFont final : public lithe::Font
{
public:
    bool glyph(char32_t codepoint, lithe::Glyph& result) const noexcept override
    {
        if (codepoint != U'X')
        {
            return false;
        }
        result = {mCoverage, 2U, 2U, 2U, 2U, 0, 0, lithe::GlyphCoverage::alpha8};
        return true;
    }
    std::uint8_t lineHeight() const noexcept override
    {
        return 2U;
    }

private:
    const std::array<std::byte, 4U> mCoverage{
        std::byte{0U}, std::byte{85U}, std::byte{170U}, std::byte{255U}};
};

void checkGlyphCoverage(Checks& checks, lithe::PixelFormat format)
{
    std::array<std::byte, 4U> bytes{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{{bytes, format, 2, 2, 2U}, damage};
    const FixtureFont font{};
    painter.drawText({0, 0, 2, 2}, "X", font, kRed);
    checks.expect(equalBytes(std::span{bytes},
                             format == lithe::PixelFormat::rrrr2222
                                 ? std::array<std::uint8_t, 4U>{0U, 0x55U, 0xaaU, 0xffU}
                                 : std::array<std::uint8_t, 4U>{0U, 0x41U, 0x82U, 0xc3U}),
                  "alpha8 glyph coverage renders four native cell intensity levels");
}

class PatternView final : public lithe::View
{
public:
    PatternView() noexcept : View(kViewport.bounds)
    {
    }

protected:
    void drawSelf(lithe::Painter& painter, lithe::Rect frame) noexcept override
    {
        painter.fillRect(frame, {255U, 170U, 0U, 255U});
        painter.fillRect({0, 0, 2, 3}, {85U, 0U, 0U, 255U});
        painter.drawLine({0, 0}, {2, 2}, lithe::Color::rgb(0xffffffU));
        painter.fillRect({4, 0, 1, 3}, {0U, 0U, 0U, 128U});
        constexpr std::array native{std::byte{0x72U}};
        const lithe::Bitmap bitmap{native, 2, 2, 1U, lithe::BitmapFormat::r2Packed2x2};
        painter.drawBitmap({2, 1}, bitmap);
    }
};

class FrameDisplay final : public lithe::Display
{
public:
    explicit FrameDisplay(lithe::PixelFormat format) noexcept : mFormat(format)
    {
    }
    lithe::Surface surface() noexcept override
    {
        return {bytes, mFormat, 5, 3, 5U};
    }
    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        presentations += regions.empty() ? 0U : 1U;
    }

    std::array<std::byte, 15U> bytes{};
    std::size_t presentations{0U};

private:
    lithe::PixelFormat mFormat;
};

class CapturingSink final : public lithe::TileSink
{
public:
    explicit CapturingSink(lithe::PixelFormat format) noexcept : mFormat(format)
    {
    }
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        return size.width == 5 && size.height == 3 && format == mFormat;
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        if (!pixels.valid() || pixels.format != mFormat || destination.width != pixels.width ||
            destination.height != pixels.height || destination.x < 0 || destination.y < 0 ||
            destination.right() > 5 || destination.bottom() > 3)
        {
            return false;
        }
        const auto rows = lithe::detail::nonnegativeSize(pixels.height);
        const auto columns = lithe::detail::nonnegativeSize(pixels.width);
        if (pixels.data.size() != (rows - 1U) * pixels.strideBytes + columns)
        {
            return false;
        }
        for (std::size_t row = 0U; row < rows; ++row)
        {
            for (std::size_t column = 0U; column < columns; ++column)
            {
                const auto index = (lithe::detail::nonnegativeSize(destination.y) + row) * 5U +
                                   lithe::detail::nonnegativeSize(destination.x) + column;
                if (seen[index])
                {
                    return false;
                }
                seen[index] = true;
                bytes[index] = pixels.data[row * pixels.strideBytes + column];
            }
            if (row + 1U < rows)
            {
                for (std::size_t padding = columns; padding < pixels.strideBytes; ++padding)
                {
                    if (pixels.data[row * pixels.strideBytes + padding] != std::byte{})
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
    bool endFrame(bool complete) noexcept override
    {
        completed = complete && std::ranges::all_of(seen,
                                                    [](bool value)
                                                    {
                                                        return value;
                                                    });
        return completed;
    }

    std::array<std::byte, 15U> bytes{};
    std::array<bool, 15U> seen{};
    bool completed{false};

private:
    lithe::PixelFormat mFormat;
};

void checkFullAndTiled(Checks& checks, lithe::PixelFormat format)
{
    // Independent logical-cell oracles for fills, line, coverage, and native bitmap.
    const auto expected = format == lithe::PixelFormat::rrrr2222
                              ? std::array<std::uint8_t, 15U>{0xffU,
                                                              0x55U,
                                                              0xffU,
                                                              0xffU,
                                                              0x55U,
                                                              0x55U,
                                                              0xffU,
                                                              0x00U,
                                                              0x55U,
                                                              0x55U,
                                                              0x55U,
                                                              0x55U,
                                                              0xaaU,
                                                              0xffU,
                                                              0x55U}
                              : std::array<std::uint8_t, 15U>{0x3cU,
                                                              0x41U,
                                                              0x69U,
                                                              0x69U,
                                                              0x14U,
                                                              0x41U,
                                                              0x3cU,
                                                              0x00U,
                                                              0x41U,
                                                              0x14U,
                                                              0x41U,
                                                              0x41U,
                                                              0x82U,
                                                              0xc3U,
                                                              0x14U};
    FrameDisplay display{format};
    PatternView fullView{};
    lithe::ViewController fullController{fullView};
    lithe::Application fullApplication{kViewport};
    checks.expect(fullApplication.pushScene(fullController) && fullApplication.render(display) &&
                      display.presentations == 1U && equalBytes(display.bytes, expected),
                  "full native cell frame matches the independent byte oracle");

    PatternView tileView{};
    lithe::ViewController tileController{tileView};
    lithe::Application tileApplication{kViewport};
    checks.expect(tileApplication.pushScene(tileController), "tiled native cell scene initializes");
    constexpr std::array<lithe::Size, 4U> tileSizes{{{1, 1}, {2, 2}, {3, 2}, {6, 4}}};
    for (const auto size : tileSizes)
    {
        std::array<std::byte, 26U> workspaceBytes{};
        workspaceBytes.fill(kGuard);
        const lithe::Surface workspace{std::span{workspaceBytes}.subspan(1U, 24U),
                                       format,
                                       size.width,
                                       size.height,
                                       6U,
                                       {7, -9}};
        CapturingSink sink{format};
        const auto result =
            lithe::TiledRenderer::render(tileApplication, kFrameSize, workspace, sink);
        checks.expect(result.ok() && result.pixelsTransferred == 15U && sink.completed &&
                          equalBytes(sink.bytes, expected) && sink.bytes == display.bytes,
                      "padded native tiles and odd edge tiles match every full-frame cell byte");
        checks.expect(workspaceBytes.front() == kGuard && workspaceBytes.back() == kGuard,
                      "tile clearing and rendering preserve caller span guards");
    }
}

} // namespace

int main()
{
    Checks checks{};
    checkTraits<lithe::PixelFormat::rrrr2222>(checks);
    checkTraits<lithe::PixelFormat::ryry2222>(checks);
    checkNativeBitmapValidation(checks);
    checkOtherDestinations(checks);
    for (const auto format : kFormats)
    {
        checkPainterCoverageAndClip(checks, format);
        checkNativeBitmap(checks, format);
        checkGlyphCoverage(checks, format);
        checkFullAndTiled(checks, format);
    }
    return checks.failures == 0 ? 0 : 1;
}
