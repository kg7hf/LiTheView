// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "round_demo.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <span>

namespace
{

using namespace lithe;

constexpr std::int32_t kExtent{240};
constexpr std::int32_t kTileWidth{73};
constexpr std::int32_t kTileHeight{11};

template <PixelFormat Format> class FrameDisplay final : public Display
{
public:
    using Storage = typename PixelFormatTraits<Format>::Storage;

    Surface surface() noexcept override
    {
        return Surface{PixelSurface<Format>{pixels, kExtent, kExtent, kExtent}};
    }

    void present([[maybe_unused]] std::span<const Rect> regions) noexcept override
    {
    }

    std::span<const std::byte> bytes() const noexcept
    {
        return std::as_bytes(std::span{pixels});
    }

    std::array<Storage, 57600U> pixels{};
};

class ComparingTiles final : public TileSink
{
public:
    explicit ComparingTiles(Surface expected) noexcept : mExpected(expected)
    {
    }

    bool beginFrame(Size size, PixelFormat format) noexcept override
    {
        return size.width == kExtent && size.height == kExtent && format == mExpected.format;
    }

    bool writeTile(Rect destination, PixelBufferView pixels) noexcept override
    {
        if (!pixels.valid() || destination.x != mNextX || destination.y != mNextY ||
            destination.width != std::min(kTileWidth, kExtent - mNextX) ||
            destination.height != std::min(kTileHeight, kExtent - mNextY) ||
            pixels.width != destination.width || pixels.height != destination.height ||
            pixels.format != mExpected.format)
        {
            return false;
        }
        const std::size_t pixelBytes = bytesPerPixel(pixels.format);
        const std::size_t rowBytes = detail::nonnegativeSize(pixels.width) * pixelBytes;
        const std::size_t firstRow = detail::nonnegativeSize(destination.y);
        const std::size_t firstColumn = detail::nonnegativeSize(destination.x) * pixelBytes;
        for (std::size_t row = 0U; row < detail::nonnegativeSize(pixels.height); ++row)
        {
            const auto actual = pixels.data.subspan(row * pixels.strideBytes, rowBytes);
            const auto expected = mExpected.data.subspan(
                (firstRow + row) * mExpected.strideBytes + firstColumn, rowBytes);
            if (!std::ranges::equal(actual, expected))
            {
                return false;
            }
        }
        ++tiles;
        mNextX += destination.width;
        if (mNextX == kExtent)
        {
            mNextX = 0;
            mNextY += destination.height;
        }
        return true;
    }

    bool endFrame(bool complete) noexcept override
    {
        completed = complete && mNextX == 0 && mNextY == kExtent && tiles == 88U;
        return completed;
    }

    std::size_t tiles{0U};
    bool completed{false};

private:
    Surface mExpected;
    std::int32_t mNextX{0};
    std::int32_t mNextY{0};
};

enum class ReplayAction : std::uint8_t
{
    pointer,
    advance,
    back
};

struct ReplayStep
{
    ReplayAction action{ReplayAction::pointer};
    PointerEvent pointer{};
    std::uint32_t elapsedMs{0U};
    std::size_t depth{1U};
    bool transition{false};
    bool handled{true};
    bool overlayContent{false};
};

// The same logical coordinates, contact IDs, timestamps and explicit time steps
// are replayed into independent scenes. No wall clock or hardware is involved.
constexpr std::array<ReplayStep, 23U> kReplay{{
    {ReplayAction::pointer, {{0, 0}, PointerPhase::pressed, 1U, 0U}, 0U, 1U, false, false},
    {ReplayAction::pointer, {{0, 0}, PointerPhase::released, 1U, 10U}, 0U, 1U, false, false},
    {ReplayAction::pointer, {{120, 162}, PointerPhase::pressed, 2U, 100U}},
    {ReplayAction::pointer, {{120, 162}, PointerPhase::released, 2U, 140U}, 0U, 2U, true},
    {ReplayAction::advance, {}, 120U, 2U, true},
    {ReplayAction::advance, {}, 120U, 2U, false, true, true},
    {ReplayAction::pointer, {{120, 170}, PointerPhase::pressed, 3U, 400U}, 0U, 2U},
    {ReplayAction::pointer, {{120, 170}, PointerPhase::released, 3U, 440U}, 0U, 2U, true},
    {ReplayAction::advance, {}, 120U, 2U, true},
    {ReplayAction::advance, {}, 120U},
    {ReplayAction::pointer, {{180, 120}, PointerPhase::pressed, 4U, 700U}},
    {ReplayAction::pointer, {{100, 120}, PointerPhase::moved, 4U, 750U}},
    {ReplayAction::pointer, {{50, 120}, PointerPhase::released, 4U, 800U}, 0U, 2U, true},
    {ReplayAction::advance, {}, 120U, 2U, true},
    {ReplayAction::advance, {}, 120U, 2U},
    {ReplayAction::pointer, {{82, 86}, PointerPhase::pressed, 5U, 1100U}, 0U, 2U},
    {ReplayAction::pointer, {{82, 86}, PointerPhase::released, 5U, 1150U}, 0U, 2U},
    {ReplayAction::pointer, {{82, 86}, PointerPhase::pressed, 6U, 1200U}, 0U, 2U},
    {ReplayAction::pointer, {{98, 94}, PointerPhase::moved, 6U, 1240U}, 0U, 2U},
    {ReplayAction::pointer, {{112, 104}, PointerPhase::moved, 6U, 1270U}, 0U, 2U},
    {ReplayAction::pointer, {{112, 104}, PointerPhase::released, 6U, 1300U}, 0U, 2U},
    {ReplayAction::back, {}, 0U, 2U, true},
    {ReplayAction::advance, {}, 240U},
}};

bool apply(demo::RoundDemo& scene, ReplayStep step) noexcept
{
    bool handled = false;
    switch (step.action)
    {
    case ReplayAction::pointer:
        handled = scene.handlePointer(step.pointer);
        break;
    case ReplayAction::advance:
        handled = scene.advanceTime(step.elapsedMs);
        break;
    case ReplayAction::back:
        handled = scene.handleKey(Key::back);
        break;
    }
    return handled == step.handled && scene.sceneDepth() == step.depth &&
           scene.transitionRunning() == step.transition;
}

template <PixelFormat Format> bool clippedPixelsAreBlack(std::span<const std::byte> bytes) noexcept
{
    constexpr std::size_t kPixelBytes{PixelFormatTraits<Format>::kBytesPerPixel};
    for (std::int32_t y = 0; y < kExtent; ++y)
    {
        for (std::int32_t x = 0; x < kExtent; ++x)
        {
            if (demo::RoundDemo::kViewport.contains({x, y}))
            {
                continue;
            }
            const auto offset =
                (detail::nonnegativeSize(y) * 240U + detail::nonnegativeSize(x)) * kPixelBytes;
            for (std::size_t channel = 0U; channel < kPixelBytes; ++channel)
            {
                const auto black = Format == PixelFormat::xrgb8888 && channel == 3U
                                       ? std::byte{0xffU}
                                       : std::byte{};
                if (bytes[offset + channel] != black)
                {
                    return false;
                }
            }
        }
    }
    return true;
}

bool regionHasContent(Surface frame, Rect region, Point background) noexcept
{
    const std::size_t pixelBytes = bytesPerPixel(frame.format);
    const auto backgroundPixel =
        frame.data.subspan(detail::nonnegativeSize(background.y) * frame.strideBytes +
                               detail::nonnegativeSize(background.x) * pixelBytes,
                           pixelBytes);
    for (std::int32_t y = region.y; y < region.bottom(); ++y)
    {
        for (std::int32_t x = region.x; x < region.right(); ++x)
        {
            const auto pixel = frame.data.subspan(detail::nonnegativeSize(y) * frame.strideBytes +
                                                      detail::nonnegativeSize(x) * pixelBytes,
                                                  pixelBytes);
            if (!std::ranges::equal(pixel, backgroundPixel))
            {
                return true;
            }
        }
    }
    return false;
}

bool overlayHasContent(Surface frame, Rect panel) noexcept
{
    const Point background{panel.x + 5, panel.y + 34};
    for (std::int32_t line = 0; line < 5; ++line)
    {
        if (!regionHasContent(
                frame, {panel.x + 14, panel.y + 34 + line * 13, panel.width - 28, 13}, background))
        {
            return false;
        }
    }
    return regionHasContent(
               frame, {panel.x + 20, panel.y + 12, panel.width - 40, 20}, background) &&
           regionHasContent(frame, {panel.x + 34, panel.y + 110, 100, 26}, background);
}

template <PixelFormat Format> bool replay() noexcept
{
    FrameDisplay<Format> display{};
    FrameDisplay<Format> repeatedDisplay{};
    demo::RoundDemo full{};
    demo::RoundDemo repeated{};
    demo::RoundDemo tiled{};
    if (!full.initialize() || !repeated.initialize() || !tiled.initialize())
    {
        return false;
    }
    using Storage = typename PixelFormatTraits<Format>::Storage;
    std::array<Storage, 803U> tilePixels{};
    const Surface workspace{PixelSurface<Format>{tilePixels, kTileWidth, kTileHeight, kTileWidth}};
    auto compareFrame = [&]()
    {
        if (!full.render(display) || !repeated.render(repeatedDisplay) ||
            !std::ranges::equal(display.bytes(), repeatedDisplay.bytes()) ||
            !clippedPixelsAreBlack<Format>(display.bytes()))
        {
            return false;
        }
        ComparingTiles sink{display.surface()};
        const auto result = tiled.renderTiled(sink, workspace);
        return result.ok() && result.tileCount == 88U && result.pixelsTransferred == 57600U &&
               sink.completed;
    };
    const auto placement = demo::RoundDemo::overlayPlacement();
    if (!placement || !placement->usedFallback || placement->frame.x != 36 ||
        placement->frame.y != 47 || placement->frame.width != 168 ||
        placement->frame.height != 146 || !compareFrame())
    {
        return false;
    }
    for (std::size_t index = 0U; index < kReplay.size(); ++index)
    {
        const ReplayStep step = kReplay[index];
        if (!apply(full, step) || !apply(repeated, step) || !apply(tiled, step) ||
            !compareFrame() ||
            (step.overlayContent && !overlayHasContent(display.surface(), placement->frame)))
        {
            std::printf("FAIL: round replay step %zu\n", index);
            return false;
        }
    }
    return true;
}

} // namespace

int main()
{
    const auto& definition = demo::display_definitions::kRound240;
    const auto rejected = placeSurfacePopup(definition, {20, 74, 200, 146});
    const auto oversized =
        placeSurfacePopup(definition, {20, 74, 200, 146}, SurfacePopupFallback::centeredSafeArea);
    if (rejected || rejected.error() != SurfaceLayoutError::popupCannotFit || oversized ||
        oversized.error() != SurfaceLayoutError::popupCannotFit)
    {
        std::puts("FAIL: round anchored popup requires an explicit smaller fallback");
        return 1;
    }
    if (!replay<PixelFormat::rgb565>() || !replay<PixelFormat::xrgb8888>())
    {
        return 1;
    }
    std::puts("PASS: round replay, 24 frames x 2 RGB formats, exact full-frame/tiled bytes");
    return 0;
}
