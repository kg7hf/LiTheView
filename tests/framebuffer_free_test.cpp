// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/lithe.hpp"

#include <array>
#include <cstddef>
#include <iostream>
#include <span>
#include <string_view>

namespace
{

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

constexpr lithe::Size kSize{8, 8};
constexpr lithe::Viewport kRound{lithe::Viewport::circle({0, 0, 8, 8})};
constexpr lithe::Viewport kRectangle{lithe::Viewport::rectangle({0, 0, 8, 8})};
static_assert(kRound.valid() && kRectangle.valid());

template <lithe::PixelFormat Format> class FrameDisplay final : public lithe::Display
{
public:
    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;

    lithe::Surface surface() noexcept override
    {
        return invalid ? lithe::Surface{}
                       : lithe::Surface{lithe::PixelSurface<Format>{
                             pixels, kSize.width, kSize.height, kSize.width}};
    }
    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        presentations += regions.empty() ? 0U : 1U;
    }

    std::array<Storage, 64U> pixels{};
    std::size_t presentations{0U};
    bool invalid{false};
};

// Independent byte comparison: no painter or format reader is used to assemble
// the tile reference. The sink also records the presentation lifecycle.
class ComparingSink final : public lithe::TileSink
{
public:
    explicit ComparingSink(lithe::Surface expected) noexcept : mExpected(expected)
    {
    }
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        ++begins;
        return size.width == kSize.width && size.height == kSize.height &&
               format == mExpected.format;
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        ++writes;
        if (writes == failWrite || !pixels.valid() || pixels.format != mExpected.format ||
            destination.width != pixels.width || destination.height != pixels.height ||
            destination.x < 0 || destination.y < 0 || destination.right() > kSize.width ||
            destination.bottom() > kSize.height)
        {
            return false;
        }
        const auto pixelBytes = lithe::bytesPerPixel(pixels.format);
        const auto xBytes = lithe::detail::nonnegativeSize(destination.x) * pixelBytes;
        const auto firstRow = lithe::detail::nonnegativeSize(destination.y);
        const auto rowBytes = lithe::detail::nonnegativeSize(pixels.width) * pixelBytes;
        for (std::size_t y = 0U; y < lithe::detail::nonnegativeSize(pixels.height); ++y)
        {
            for (std::size_t x = 0U; x < rowBytes; ++x)
            {
                if (pixels.data[y * pixels.strideBytes + x] !=
                    mExpected.data[(firstRow + y) * mExpected.strideBytes + xBytes + x])
                {
                    return false;
                }
            }
        }
        return true;
    }
    bool endFrame(bool complete) noexcept override
    {
        ++ends;
        completed = complete;
        return true;
    }

    std::size_t begins{0U};
    std::size_t writes{0U};
    std::size_t ends{0U};
    std::size_t failWrite{0U};
    bool completed{false};

private:
    lithe::Surface mExpected;
};

lithe::Style background(lithe::Color color) noexcept
{
    lithe::Style style{};
    style.background = color;
    return style;
}

template <lithe::PixelFormat Format> void checkRendering(Checks& checks, lithe::Viewport viewport)
{
    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;
    using Traits = lithe::PixelFormatTraits<Format>;
    const auto red = lithe::Color::rgb(0xff0000U);
    FrameDisplay<Format> display{};
    lithe::View root{{0, 0, 8, 8}, background(red)};
    lithe::ViewController controller{root};
    lithe::Application application{viewport};
    checks.expect(!application.render() && !application.render(display),
                  "application without a controller cannot render or present");
    checks.expect(application.pushScene(controller) && !application.render() &&
                      application.render(display),
                  "geometry-only application borrows a display without binding it");
    checks.expect(display.presentations == 1U && application.render(display) &&
                      display.presentations == 1U && !application.render(),
                  "clean frame succeeds without presentation; display binding remains absent");
    const auto bytes = std::as_bytes(std::span{display.pixels});
    checks.expect(Traits::read(bytes, 36U * Traits::kBytesPerPixel).red == 255U &&
                      Traits::read(bytes, 0U).red == (viewport.contains({0, 0}) ? 255U : 0U),
                  "explicit geometry controls center and corner coverage");

    lithe::View tiledRoot{{0, 0, 8, 8}, background(red)};
    lithe::ViewController tiledController{tiledRoot};
    lithe::Application tiledApplication{viewport};
    checks.expect(tiledApplication.pushScene(tiledController), "tiled scene initializes");
    std::array<Storage, 9U> tilePixels{};
    const lithe::Surface workspace{lithe::PixelSurface<Format>{tilePixels, 3, 3, 3}};
    ComparingSink sink{display.surface()};
    const auto result = lithe::TiledRenderer::render(tiledApplication, kSize, workspace, sink);
    checks.expect(result.ok() && result.tileCount == 9U && result.pixelsTransferred == 64U &&
                      sink.begins == 1U && sink.ends == 1U && sink.completed,
                  "independent framebuffer-free scene matches full frame, including edge tiles");

    ComparingSink failedSink{display.surface()};
    failedSink.failWrite = 2U;
    const auto failed =
        lithe::TiledRenderer::render(tiledApplication, kSize, workspace, failedSink);
    checks.expect(failed.error == lithe::TileRenderError::sinkWriteFailed &&
                      failed.tileCount == 1U && failedSink.ends == 1U && !failedSink.completed,
                  "sink failure reports partial output and closes an incomplete frame");
    ComparingSink recoveredSink{display.surface()};
    checks.expect(
        lithe::TiledRenderer::render(tiledApplication, kSize, workspace, recoveredSink).ok() &&
            recoveredSink.completed,
        "a subsequent complete frame recovers after sink failure");

    lithe::Application emptyApplication{viewport};
    ComparingSink unusedSink{display.surface()};
    const auto noController =
        lithe::TiledRenderer::render(emptyApplication, kSize, workspace, unusedSink);
    const auto badWorkspace = lithe::TiledRenderer::render(tiledApplication, kSize, {}, unusedSink);
    checks.expect(noController.error == lithe::TileRenderError::noActiveController &&
                      badWorkspace.error == lithe::TileRenderError::invalidArgument &&
                      unusedSink.begins == 0U,
                  "invalid input fails before starting presentation");

    application.invalidate();
    display.invalid = true;
    checks.expect(!application.render(display) && display.presentations == 1U,
                  "invalid full-frame output does not present");
    display.invalid = false;
    checks.expect(application.render(display) && display.presentations == 2U,
                  "failed output preserves pending damage for a valid display");

    // A deliberately larger incoming root proves transition distance comes
    // from display geometry, not its scene content dimensions.
    lithe::View incoming{{0, 0, 12, 12}, background(lithe::Color::rgb(0x0000ffU))};
    lithe::ViewController incomingController{incoming};
    checks.expect(
        application.pushScene(incomingController, {lithe::SceneTransition::slideLeft, 100U}) &&
            application.advanceTime(50U) && application.render(display),
        "framebuffer-free application advances a scene transition");
    checks.expect(Traits::read(bytes, 34U * Traits::kBytesPerPixel).red == 255U &&
                      Traits::read(bytes, 37U * Traits::kBytesPerPixel).blue == 255U,
                  "halfway transition uses the eight-pixel display width");
}

class Actions final : public lithe::ControllerDelegate
{
public:
    void onAction([[maybe_unused]] lithe::ViewController& controller,
                  [[maybe_unused]] lithe::ViewId action) noexcept override
    {
        ++count;
    }
    void onBack([[maybe_unused]] lithe::ViewController& controller) noexcept override
    {
    }
    std::size_t count{0U};
};

void checkInput(Checks& checks)
{
    lithe::ButtonView button{{0, 0, 8, 8}, lithe::font5x7(), ""};
    Actions actions{};
    lithe::ViewController controller{button, actions};
    lithe::Application application{kRound};
    checks.expect(application.pushScene(controller), "input scene initializes without display");
    checks.expect(!application.handlePointer({{0, 0}, lithe::PointerPhase::pressed, 1U, 10U}),
                  "round corner rejects input before any surface exists");
    checks.expect(application.handlePointer({{4, 4}, lithe::PointerPhase::pressed, 1U, 20U}) &&
                      application.handlePointer({{4, 4}, lithe::PointerPhase::released, 1U, 30U}) &&
                      actions.count == 1U,
                  "normalized pointer input activates the same controller without a display");
}

void checkDisplayLifetime(Checks& checks)
{
    FrameDisplay<lithe::PixelFormat::rgb565> boundDisplay{};
    lithe::View root{kRectangle.bounds, background(lithe::Color::rgb(0xff0000U))};
    lithe::ViewController controller{root};
    lithe::Application application{boundDisplay, kRectangle};
    checks.expect(application.pushScene(controller) && application.render(),
                  "compatibility constructor binds the persistent display");
    {
        FrameDisplay<lithe::PixelFormat::rgb565> temporaryDisplay{};
        application.invalidate();
        checks.expect(application.render(temporaryDisplay) && temporaryDisplay.presentations == 1U,
                      "render can borrow a temporary output for one call");
    }
    application.invalidate();
    checks.expect(application.render() && boundDisplay.presentations == 2U,
                  "borrowing a display does not replace the persistent binding");
}

} // namespace

int main()
{
    Checks checks{};
    checkRendering<lithe::PixelFormat::rgb565>(checks, kRectangle);
    checkRendering<lithe::PixelFormat::rgb565>(checks, kRound);
    checkRendering<lithe::PixelFormat::xrgb8888>(checks, kRectangle);
    checkRendering<lithe::PixelFormat::xrgb8888>(checks, kRound);
    checkInput(checks);
    checkDisplayLifetime(checks);
    return checks.failures == 0 ? 0 : 1;
}
