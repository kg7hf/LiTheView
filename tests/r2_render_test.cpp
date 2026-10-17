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

constexpr auto kFormat = lithe::PixelFormat::r2Packed2x2;
constexpr auto kRed = lithe::Color::rgb(0xff0000U);
constexpr auto kBlack = lithe::Color::rgb(0x000000U);
constexpr auto kGuard = std::byte{0xa5U};
constexpr lithe::Size kSize{8, 8};
constexpr auto kRectangle = lithe::Viewport::rectangle({0, 0, 8, 8});
constexpr auto kRound = lithe::Viewport::circle({0, 0, 8, 8});
static_assert(lithe::supportsDestination(kFormat));

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

void checkPacking(Checks& checks)
{
    std::array<std::byte, 7U> bytes{};
    bytes.fill(kGuard);
    const lithe::Surface surface{std::span{bytes}.subspan(1U, 5U), kFormat, 4, 4, 3U, {2, -2}};
    lithe::DamageTracker damage{};
    lithe::Painter painter{surface, damage};
    checks.expect(painter.valid() && surface.usedBytes() == 5U,
                  "R2 accepts aligned negative origins and an exact final active row");
    painter.fillRect(surface.bounds(), kBlack);
    painter.fillRect({2, -2, 1, 1}, kBlack);
    painter.fillRect({3, -2, 1, 1}, lithe::Color::rgb(0x550000U));
    painter.fillRect({2, -1, 1, 1}, lithe::Color::rgb(0xaa0000U));
    painter.fillRect({3, -1, 1, 1}, kRed);
    // Independent native byte oracle: TL=0, TR=1, BL=2, BR=3 is 0x72.
    checks.expect(equalBytes(std::span{bytes},
                             std::array<std::uint8_t, 7U>{0xa5U, 0x72U, 0U, 0xa5U, 0U, 0U, 0xa5U}),
                  "logical 2x2 samples pack to 0x72 without changing guards or row padding");

    constexpr std::array<lithe::Point, 4U> points{{{0, 0}, {1, 0}, {0, 1}, {1, 1}}};
    constexpr std::array<std::uint8_t, 4U> changed{0xadU, 0xe5U, 0xa7U, 0xb5U};
    std::array<std::byte, 1U> cell{};
    for (std::size_t field = 0U; field < points.size(); ++field)
    {
        cell[0] = kGuard;
        lithe::DamageTracker fieldDamage{};
        lithe::Painter fieldPainter{{cell, kFormat, 2, 2, 1U}, fieldDamage};
        fieldPainter.fillRect({points[field].x, points[field].y, 1, 1}, kRed);
        checks.expect(std::to_integer<std::uint8_t>(cell[0]) == changed[field],
                      "a single R2 field update preserves three nonzero neighbors");
    }

    cell[0] = std::byte{};
    lithe::DamageTracker clipDamage{};
    lithe::Painter clipped{{cell, kFormat, 2, 2, 1U}, clipDamage};
    clipped.setClip({1, 0, 1, 1});
    clipped.fillRect({0, 0, 2, 2}, kRed);
    checks.expect(cell[0] == std::byte{0xc0U},
                  "an unaligned logical clip updates only the selected packed field");
}

void checkQuantizationAndCoverage(Checks& checks)
{
    constexpr std::array<std::uint8_t, 8U> redValues{0U, 42U, 43U, 127U, 128U, 212U, 213U, 255U};
    constexpr std::array<std::uint8_t, 8U> expected{0U, 0U, 4U, 4U, 8U, 8U, 12U, 12U};
    std::array<std::byte, 1U> cell{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{{cell, kFormat, 2, 2, 1U}, damage};
    for (std::size_t sample = 0U; sample < redValues.size(); ++sample)
    {
        cell[0] = std::byte{};
        painter.fillRect({0, 0, 1, 1}, {redValues[sample], 255U, 255U, 255U});
        checks.expect(std::to_integer<std::uint8_t>(cell[0]) == expected[sample],
                      "red quantization rounds at 43, 128, and 213 and ignores green/blue");
    }

    cell[0] = kGuard;
    damage.clear();
    painter.fillRect({0, 0, 2, 2}, {255U, 0U, 0U, 0U});
    checks.expect(cell[0] == kGuard && damage.empty(), "zero coverage changes no fields or damage");
    cell[0] = std::byte{};
    painter.fillRect({0, 0, 1, 1}, {255U, 0U, 0U, 128U});
    checks.expect(cell[0] == std::byte{0x08U},
                  "half-covered red over black quantizes to level two");
    cell[0] = std::byte{0xffU};
    painter.fillRect({0, 0, 1, 1}, {0U, 0U, 0U, 128U});
    checks.expect(
        cell[0] == std::byte{0xf7U},
        "half-covered black over full red quantizes to level one and preserves neighbors");
    painter.fillRect({0, 0, 2, 2}, {85U, 255U, 255U, 255U});
    checks.expect(cell[0] == std::byte{0x55U},
                  "opaque writes replace every field with exact level one");
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
    // Independent coverage levels become TL=0, TR=1, BL=2, BR=3 over black.
    const std::array<std::byte, 4U> mCoverage{
        std::byte{0U}, std::byte{85U}, std::byte{170U}, std::byte{255U}};
};

void checkDrawingOperations(Checks& checks)
{
    std::array<std::byte, 4U> bytes{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{{bytes, kFormat, 4, 4, 2U}, damage};
    painter.drawLine({0, 0}, {3, 3}, kRed);
    checks.expect(equalBytes(std::span{bytes}, std::array<std::uint8_t, 4U>{0x3cU, 0U, 0U, 0x3cU}),
                  "a diagonal line crosses native block boundaries with exact packed bytes");
    bytes.fill(std::byte{});
    painter.strokeRect({0, 0, 4, 4}, kRed);
    checks.expect(
        equalBytes(std::span{bytes}, std::array<std::uint8_t, 4U>{0xcfU, 0xfcU, 0x3fU, 0xf3U}),
        "rectangle outlines preserve the four interior fields");
    bytes.fill(std::byte{});
    painter.fillCircle({1, 1}, 1, kRed);
    checks.expect(
        equalBytes(std::span{bytes}, std::array<std::uint8_t, 4U>{0xf3U, 0x03U, 0xc0U, 0U}),
        "unit circle fills the center and four axial neighbors");

    bytes.fill(std::byte{});
    constexpr std::array<std::byte, 2U> mask{std::byte{0x80U}, std::byte{0x40U}};
    const lithe::Bitmap alpha1{mask, 2, 2, 1U, lithe::BitmapFormat::alpha1, kRed};
    painter.drawBitmap({0, 0}, alpha1);
    checks.expect(bytes[0] == std::byte{0x3cU},
                  "alpha1 bitmap coverage preserves uncovered fields");

    bytes.fill(std::byte{});
    constexpr std::array<std::byte, 4U> coverage{
        std::byte{0U}, std::byte{85U}, std::byte{170U}, std::byte{255U}};
    const lithe::Bitmap alpha8{coverage, 2, 2, 2U, lithe::BitmapFormat::alpha8, kRed};
    painter.drawBitmap({0, 0}, alpha8);
    checks.expect(bytes[0] == std::byte{0x72U},
                  "alpha8 bitmap coverage has an independent 0x72 oracle");

    bytes.fill(std::byte{});
    constexpr std::array<std::byte, 2U> indexes{std::byte{0x01U}, std::byte{0x23U}};
    constexpr std::array<lithe::Color, 4U> palette{
        kBlack, lithe::Color::rgb(0x550000U), lithe::Color::rgb(0xaa0000U), kRed};
    const lithe::Bitmap indexed{indexes, 2, 2, 1U, lithe::BitmapFormat::indexed4, kRed, palette};
    painter.drawBitmap({0, 0}, indexed);
    checks.expect(bytes[0] == std::byte{0x72U},
                  "indexed4 resources render directly into packed R2");

    bytes.fill(std::byte{});
    const FixtureFont font{};
    painter.drawText({0, 0, 2, 2}, "X", font, kRed);
    checks.expect(bytes[0] == std::byte{0x72U},
                  "antialiased glyph coverage composes on an opaque packed destination");

    std::array<std::byte, 16U> compactText{};
    lithe::DamageTracker textDamage{};
    lithe::Painter textPainter{{compactText, kFormat, 8, 8, 4U}, textDamage};
    textPainter.drawText({0, 0, 8, 8}, "!", lithe::font5x7(), kRed);
    // Compact '!' has one five-pixel vertical stroke and an isolated dot.
    checks.expect(
        equalBytes(std::span{compactText},
                   std::array<std::uint8_t, 16U>{
                       0U, 0x0fU, 0U, 0U, 0U, 0x0fU, 0U, 0U, 0U, 0x0cU, 0U, 0U, 0U, 0x0cU, 0U, 0U}),
        "compact font5x7 text renders to exact native bytes without RGB staging");
}

class PatternView final : public lithe::View
{
public:
    PatternView() noexcept : View(kRectangle.bounds)
    {
    }

protected:
    void drawSelf(lithe::Painter& painter, lithe::Rect frame) noexcept override
    {
        painter.fillRect(frame, kRed);
        painter.fillRect({frame.x, frame.y, 4, 8}, lithe::Color::rgb(0x550000U));
        painter.drawLine(
            {frame.x, frame.y}, {frame.x + 7, frame.y + 7}, lithe::Color::rgb(0xaa0000U));
    }
};

// Oracles are manually specified 2x2 block bytes. The scene fills its left
// half at level one, right half at level three, and diagonal at level two.
constexpr std::array<std::uint8_t, 16U> kRectangleBytes{0x69U,
                                                        0x55U,
                                                        0xffU,
                                                        0xffU,
                                                        0x55U,
                                                        0x69U,
                                                        0xffU,
                                                        0xffU,
                                                        0x55U,
                                                        0x55U,
                                                        0xebU,
                                                        0xffU,
                                                        0x55U,
                                                        0x55U,
                                                        0xffU,
                                                        0xebU};
constexpr std::array<std::uint8_t, 16U> kRoundBytes{0x20U,
                                                    0x55U,
                                                    0xffU,
                                                    0x03U,
                                                    0x55U,
                                                    0x69U,
                                                    0xffU,
                                                    0xffU,
                                                    0x55U,
                                                    0x55U,
                                                    0xebU,
                                                    0xffU,
                                                    0x40U,
                                                    0x55U,
                                                    0xffU,
                                                    0x08U};

class FrameDisplay final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return {bytes, kFormat, 8, 8, 4U};
    }
    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        presentations += regions.empty() ? 0U : 1U;
    }

    std::array<std::byte, 16U> bytes{};
    std::size_t presentations{0U};
};

class CapturingSink final : public lithe::TileSink
{
public:
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        ++begins;
        return !failBegin && size.width == 8 && size.height == 8 && format == kFormat;
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        ++writes;
        if (writes == failWrite || !pixels.valid() || pixels.format != kFormat ||
            destination.width != pixels.width || destination.height != pixels.height ||
            destination.x < 0 || destination.y < 0 || destination.right() > 8 ||
            destination.bottom() > 8 || destination.x % 2 != 0 || destination.y % 2 != 0)
        {
            return false;
        }
        const auto rows = lithe::detail::nonnegativeSize(pixels.height / 2);
        const auto rowBytes = lithe::detail::nonnegativeSize(pixels.width / 2);
        const auto firstRow = lithe::detail::nonnegativeSize(destination.y / 2);
        const auto firstColumn = lithe::detail::nonnegativeSize(destination.x / 2);
        if (pixels.data.size() != (rows - 1U) * pixels.strideBytes + rowBytes)
        {
            return false;
        }
        for (std::size_t row = 0U; row < rows; ++row)
        {
            for (std::size_t column = 0U; column < rowBytes; ++column)
            {
                const auto destinationIndex = (firstRow + row) * 4U + firstColumn + column;
                if (seen[destinationIndex])
                {
                    return false;
                }
                seen[destinationIndex] = true;
                bytes[destinationIndex] = pixels.data[row * pixels.strideBytes + column];
            }
            if (row + 1U < rows)
            {
                for (std::size_t padding = rowBytes; padding < pixels.strideBytes; ++padding)
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
        ++ends;
        completed = complete;
        return !failEnd && (!complete || std::ranges::all_of(seen,
                                                             [](bool present)
                                                             {
                                                                 return present;
                                                             }));
    }

    std::array<std::byte, 16U> bytes{};
    std::array<bool, 16U> seen{};
    std::size_t begins{0U};
    std::size_t writes{0U};
    std::size_t ends{0U};
    std::size_t failWrite{0U};
    bool failBegin{false};
    bool failEnd{false};
    bool completed{false};
};

void checkFullAndTiled(Checks& checks,
                       lithe::Viewport viewport,
                       const std::array<std::uint8_t, 16U>& expected)
{
    FrameDisplay display{};
    PatternView fullView{};
    lithe::ViewController fullController{fullView};
    lithe::Application fullApplication{viewport};
    checks.expect(fullApplication.pushScene(fullController) && fullApplication.render(display) &&
                      display.presentations == 1U && equalBytes(std::span{display.bytes}, expected),
                  "full-frame packed output agrees with the independent shape-specific oracle");

    PatternView tileView{};
    lithe::ViewController tileController{tileView};
    lithe::Application tileApplication{viewport};
    checks.expect(tileApplication.pushScene(tileController), "independent tiled scene initializes");
    constexpr std::array<lithe::Size, 3U> tileSizes{{{2, 2}, {6, 4}, {8, 6}}};
    for (const auto tileSize : tileSizes)
    {
        std::array<std::byte, 17U> workspaceBytes{};
        workspaceBytes.fill(kGuard);
        const lithe::Surface workspace{std::span{workspaceBytes}.subspan(1U, 14U),
                                       kFormat,
                                       tileSize.width,
                                       tileSize.height,
                                       5U,
                                       {1, -3}};
        CapturingSink sink{};
        const auto result = lithe::TiledRenderer::render(tileApplication, kSize, workspace, sink);
        checks.expect(
            result.ok() && result.pixelsTransferred == 64U && sink.completed && sink.begins == 1U &&
                sink.ends == 1U && equalBytes(std::span{sink.bytes}, expected) &&
                sink.bytes == display.bytes,
            "independent padded tiles match native full-frame bytes, including edge tiles");
        checks.expect(workspaceBytes.front() == kGuard && workspaceBytes[15U] == kGuard &&
                          workspaceBytes.back() == kGuard,
                      "tiled clear and drawing stay inside the caller's bounded workspace");
    }
}

void checkFailures(Checks& checks)
{
    PatternView view{};
    lithe::ViewController controller{view};
    lithe::Application application{kRectangle};
    checks.expect(application.pushScene(controller), "failure fixture initializes");
    std::array<std::byte, 8U> bytes{};
    constexpr auto maximumStride = std::numeric_limits<std::size_t>::max();
    const std::array<lithe::Surface, 5U> invalidWorkspaces{
        lithe::Surface{bytes, kFormat, 3, 4, 2U},
        lithe::Surface{bytes, kFormat, 4, 3, 2U},
        lithe::Surface{std::span{bytes}.first(3U), kFormat, 4, 4, 2U},
        lithe::Surface{bytes, kFormat, 4, 4, 1U},
        lithe::Surface{bytes, kFormat, 4, 6, maximumStride}};
    for (const auto workspace : invalidWorkspaces)
    {
        CapturingSink sink{};
        const auto result = lithe::TiledRenderer::render(application, kSize, workspace, sink);
        checks.expect(result.error == lithe::TileRenderError::invalidArgument &&
                          sink.begins == 0U && sink.writes == 0U && sink.ends == 0U,
                      "invalid R2 extent, stride, span, or arithmetic fails before beginFrame");
        bytes.fill(kGuard);
        lithe::DamageTracker damage{};
        lithe::Painter painter{workspace, damage};
        painter.setClip({0, 0, 8, 8});
        painter.fillRect({0, 0, 8, 8}, kRed);
        checks.expect(!painter.valid() && damage.empty() &&
                          std::ranges::all_of(bytes,
                                              [](std::byte value)
                                              {
                                                  return value == kGuard;
                                              }),
                      "setClip cannot revive an invalid packed destination");
    }

    const lithe::Surface workspace{bytes, kFormat, 4, 4, 2U};
    constexpr std::array<lithe::Size, 3U> invalidFrames{{{7, 8}, {8, 7}, {0, 8}}};
    for (const auto size : invalidFrames)
    {
        CapturingSink sink{};
        const auto result = lithe::TiledRenderer::render(application, size, workspace, sink);
        checks.expect(result.error == lithe::TileRenderError::invalidArgument && sink.begins == 0U,
                      "incomplete frame blocks fail before output begins");
    }

    CapturingSink beginFailure{};
    beginFailure.failBegin = true;
    const auto beginResult =
        lithe::TiledRenderer::render(application, kSize, workspace, beginFailure);
    checks.expect(beginResult.error == lithe::TileRenderError::sinkBeginFailed &&
                      beginFailure.writes == 0U && beginFailure.ends == 0U,
                  "rejected beginFrame does not write or claim a completed frame");
    CapturingSink writeFailure{};
    writeFailure.failWrite = 2U;
    const auto writeResult =
        lithe::TiledRenderer::render(application, kSize, workspace, writeFailure);
    checks.expect(writeResult.error == lithe::TileRenderError::sinkWriteFailed &&
                      writeResult.tileCount == 1U && writeFailure.ends == 1U &&
                      !writeFailure.completed,
                  "failed tile submission closes the incomplete native frame");
    CapturingSink endFailure{};
    endFailure.failEnd = true;
    const auto endResult = lithe::TiledRenderer::render(application, kSize, workspace, endFailure);
    checks.expect(endResult.error == lithe::TileRenderError::sinkEndFailed,
                  "failed endFrame cannot report native frame success");
    CapturingSink recovered{};
    const auto recovery = lithe::TiledRenderer::render(application, kSize, workspace, recovered);
    checks.expect(recovery.ok() && recovered.completed &&
                      equalBytes(std::span{recovered.bytes}, kRectangleBytes),
                  "a fresh native submission recovers the complete deterministic frame");
}

} // namespace

int main()
{
    Checks checks{};
    checkPacking(checks);
    checkQuantizationAndCoverage(checks);
    checkDrawingOperations(checks);
    checkFullAndTiled(checks, kRectangle, kRectangleBytes);
    checkFullAndTiled(checks, kRound, kRoundBytes);
    checkFailures(checks);
    return checks.failures == 0 ? 0 : 1;
}
