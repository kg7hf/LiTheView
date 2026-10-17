// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "expected_destination.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <iostream>
#include <lithe/lithe.hpp>
#include <string_view>

namespace
{

constexpr std::array kFormats{lithe::PixelFormat::rgb565,
                              lithe::PixelFormat::rgb888,
                              lithe::PixelFormat::xrgb8888,
                              lithe::PixelFormat::argb8888,
                              lithe::PixelFormat::r2Packed2x2,
                              lithe::PixelFormat::rrrr2222,
                              lithe::PixelFormat::ryry2222};
constexpr auto kViewport = lithe::Viewport::rectangle({0, 0, 4, 4});
constexpr auto kRed = lithe::Color::rgb(0xff0000U);
constexpr auto kGuard = std::byte{0xa5};
static_assert(lithe::supportsDestination(kExpectedDestination));
static_assert(std::ranges::count_if(kFormats, lithe::supportsDestination) == 1);

constexpr std::size_t rowBytes(lithe::PixelFormat format) noexcept
{
    return format == lithe::PixelFormat::r2Packed2x2 ? 2U : 4U * lithe::bytesPerPixel(format);
}

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

class FrameDisplay final : public lithe::Display
{
public:
    explicit FrameDisplay(lithe::PixelFormat format) noexcept : mFormat(format)
    {
        pixels.fill(kGuard);
    }
    lithe::Surface surface() noexcept override
    {
        return {pixels, mFormat, 4, 4, rowBytes(mFormat)};
    }
    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        presentations += regions.empty() ? 0U : 1U;
    }

    std::array<std::byte, 64U> pixels{};
    std::size_t presentations{0U};

private:
    lithe::PixelFormat mFormat;
};

// These byte patterns specify full red independently of the library's writers.
bool isRed(lithe::PixelBufferView pixels) noexcept
{
    if (!pixels.valid())
    {
        return false;
    }
    const auto pixelBytes = lithe::bytesPerPixel(pixels.format);
    std::array<std::byte, 4U> expected{};
    switch (pixels.format)
    {
    case lithe::PixelFormat::rgb565:
        expected = {std::byte{0x00}, std::byte{0xf8}, std::byte{}, std::byte{}};
        break;
    case lithe::PixelFormat::rgb888:
        expected = {std::byte{0xff}, std::byte{}, std::byte{}, std::byte{}};
        break;
    case lithe::PixelFormat::xrgb8888:
    case lithe::PixelFormat::argb8888:
        expected = {std::byte{}, std::byte{}, std::byte{0xff}, std::byte{0xff}};
        break;
    case lithe::PixelFormat::rrrr2222:
        expected = {std::byte{0xff}, std::byte{}, std::byte{}, std::byte{}};
        break;
    case lithe::PixelFormat::ryry2222:
        expected = {std::byte{0xc3}, std::byte{}, std::byte{}, std::byte{}};
        break;
    case lithe::PixelFormat::r2Packed2x2:
        for (std::int32_t y = 0; y < pixels.height / 2; ++y)
        {
            for (std::int32_t x = 0; x < pixels.width / 2; ++x)
            {
                if (pixels.data[lithe::detail::nonnegativeSize(y) * pixels.strideBytes +
                                lithe::detail::nonnegativeSize(x)] != std::byte{0xff})
                {
                    return false;
                }
            }
        }
        return true;
    }
    for (std::size_t y = 0U; y < lithe::detail::nonnegativeSize(pixels.height); ++y)
    {
        for (std::size_t x = 0U; x < lithe::detail::nonnegativeSize(pixels.width); ++x)
        {
            for (std::size_t channel = 0U; channel < pixelBytes; ++channel)
            {
                if (pixels.data[y * pixels.strideBytes + x * pixelBytes + channel] !=
                    expected[channel])
                {
                    return false;
                }
            }
        }
    }
    return true;
}

class RedSink final : public lithe::TileSink
{
public:
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        ++begins;
        return size.width == 4 && size.height == 4 && format == kExpectedDestination;
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        ++writes;
        const bool valid = destination.x == 0 && destination.y == nextRow &&
                           destination.width == 4 && destination.height == 2 &&
                           pixels.format == kExpectedDestination &&
                           pixels.width == destination.width &&
                           pixels.height == destination.height && isRed(pixels);
        nextRow += 2;
        return valid;
    }
    bool endFrame(bool complete) noexcept override
    {
        ++ends;
        return complete && nextRow == 4;
    }
    std::size_t begins{0U};
    std::size_t writes{0U};
    std::size_t ends{0U};
    std::int32_t nextRow{0};
};

void checkRejected(Checks& checks, lithe::Application& application, lithe::PixelFormat format)
{
    FrameDisplay rejected{format};
    const auto surface = rejected.surface();
    lithe::DamageTracker damage{};
    lithe::Painter painter{surface, damage};
    checks.expect(surface.valid() && !painter.valid(),
                  "structural validity is independent of compiled destination support");
    painter.setClip(surface.bounds());
    painter.fillRect(surface.bounds(), kRed);
    painter.drawLine({0, 0}, {3, 3}, kRed);
    painter.fillCircle({1, 1}, 1, kRed);
    painter.drawText(surface.bounds(), "A", lithe::font5x7(), kRed);
    checks.expect(damage.empty() && std::ranges::all_of(rejected.pixels,
                                                        [](std::byte pixel)
                                                        {
                                                            return pixel == kGuard;
                                                        }),
                  "unavailable painter stays inert after resetting its clip");
    checks.expect(!application.render(rejected) && rejected.presentations == 0U,
                  "unavailable display is rejected before presentation");
    damage.add({1, 1, 1, 1});
    checks.expect(!application.renderSurface(surface, damage, true) && damage.size() == 1U,
                  "unavailable surface preserves caller damage");
    RedSink sink{};
    const auto result = lithe::TiledRenderer::render(application, {4, 4}, surface, sink);
    checks.expect(result.error == lithe::TileRenderError::unsupportedFormat &&
                      result.tileCount == 0U && sink.begins == 0U && sink.writes == 0U &&
                      sink.ends == 0U,
                  "unavailable tiled destination fails before opening the sink");
}

} // namespace

int main()
{
    Checks checks{};
    lithe::Style style{};
    style.background = kRed;
    lithe::View root{kViewport.bounds, style};
    lithe::ViewController controller{root};
    lithe::Application application{kViewport};
    checks.expect(application.pushScene(controller), "scene initializes without output storage");
    for (const auto format : kFormats)
    {
        checks.expect(lithe::supportsDestination(format) == (format == kExpectedDestination),
                      "installed capability equals the independently required product format");
        if (format != kExpectedDestination)
        {
            checkRejected(checks, application, format);
        }
    }

    FrameDisplay accepted{kExpectedDestination};
    const auto surface = accepted.surface();
    checks.expect(application.render(accepted) && accepted.presentations == 1U &&
                      isRed({surface.data, surface.format, 4, 4, surface.strideBytes}),
                  "pending scene damage survives rejected outputs and renders exact red bytes");
    checks.expect(application.render(accepted) && accepted.presentations == 1U,
                  "clean supported render does not present twice");

    std::array<std::byte, 32U> workspace{};
    const lithe::Surface tile{
        workspace, kExpectedDestination, 4, 2, rowBytes(kExpectedDestination)};
    RedSink sink{};
    const auto tiled = lithe::TiledRenderer::render(application, {4, 4}, tile, sink);
    checks.expect(tiled.ok() && tiled.tileCount == 2U && tiled.pixelsTransferred == 16U &&
                      sink.begins == 1U && sink.writes == 2U && sink.ends == 1U,
                  "installed core renders and streams the selected format without a display");

    std::array<std::byte, 1U> shortStorage{kGuard};
    const lithe::Surface invalid{shortStorage, kExpectedDestination, 4, 4, surface.strideBytes};
    lithe::DamageTracker damage{};
    lithe::Painter invalidPainter{invalid, damage};
    invalidPainter.setClip({0, 0, 4, 4});
    invalidPainter.fillRect({0, 0, 4, 4}, kRed);
    checks.expect(!invalidPainter.valid() && shortStorage[0] == kGuard && damage.empty(),
                  "short storage stays rejected after setting the clip");
    return checks.failures == 0 ? 0 : 1;
}
