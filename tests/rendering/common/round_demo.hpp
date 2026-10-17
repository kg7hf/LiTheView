// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "display_definitions.hpp"
#include "lithe/lithe.hpp"
#include "lithe/surface_layout.hpp"

#include <cstdint>
#include <expected>

namespace lithe::demo
{

enum class RoundSceneKind : std::uint8_t
{
    dashboard,
    gestureLab
};

class RoundSceneView final : public View
{
public:
    RoundSceneView(RoundSceneKind kind, const Font& regular, const Font& bold) noexcept;

    // The gesture-lab scene is a product-neutral recognizer demonstration. The caller
    // supplies recognized events; the view retains only the last bounded
    // position and event kind so the result is visible without owning input
    // policy, a clock, or an event queue.
    void setGestureFeedback(GestureEvent event) noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    static constexpr Point kInitialGesturePosition{120, 93};

    RoundSceneKind mKind{RoundSceneKind::dashboard};
    const Font& mRegular;
    const Font& mBold;
    Point mGesturePosition{kInitialGesturePosition};
    GestureType mLastGesture{GestureType::tap};
    bool mGestureReceived{false};
};

class RoundOverlayView final : public View
{
public:
    RoundOverlayView(const Font& regular, const Font& bold) noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    const Font& mRegular;
    const Font& mBold;
};

// RoundDemo is a controller-agnostic 240x240 acceptance example. A platform
// supplies either a full-frame Display or a synchronous TileSink; GC9A01/SPI,
// QSPI, DMA, backlight, and touch calibration remain application adapters.
class RoundDemo final : private ControllerDelegate
{
public:
    static constexpr std::int32_t kWidth{240};
    static constexpr std::int32_t kHeight{240};

    static constexpr Viewport kViewport{*displayViewport(display_definitions::kRound240)};
    static_assert(kViewport.valid());
    static_assert(kViewport.bounds.width == kWidth && kViewport.bounds.height == kHeight);
    // The caller declares logical input independently; a round surface does
    // not imply an integrated pointer, keys, or any other input device.

    // The same scene can use a tiled sink or a caller-supplied full-frame display.
    RoundDemo() noexcept : RoundDemo(nullptr)
    {
    }
    explicit RoundDemo(Display& display) noexcept : RoundDemo(&display)
    {
    }

    bool initialize() noexcept;
    // The anchored panel is deliberately wider than the safe area. The
    // declared compact centered alternative keeps its full content height;
    // callers can inspect a placement error before initialization fails.
    [[nodiscard]] static std::expected<SurfacePopupPlacement, SurfaceLayoutError>
    overlayPlacement() noexcept;
    bool render() noexcept
    {
        return mApplication.render();
    }
    [[nodiscard]] bool render(Display& display) noexcept
    {
        return mApplication.render(display);
    }
    TileRenderResult renderTiled(TileSink& sink, Surface workspace) noexcept
    {
        return TiledRenderer::render(
            mApplication, {kWidth, kHeight}, workspace, sink, Color::rgb(0x000000U));
    }
    bool handleKey(Key key) noexcept
    {
        return mApplication.handleKey(key);
    }
    static bool handleTextInput([[maybe_unused]] char32_t codepoint) noexcept
    {
        return false;
    }
    bool handlePointer(PointerEvent event) noexcept
    {
        return mApplication.handlePointer(event);
    }
    bool advanceTime(std::uint32_t elapsedMs) noexcept
    {
        return mApplication.advanceTime(elapsedMs);
    }
    bool animationsRunning() const noexcept
    {
        return mApplication.animationsRunning();
    }
    bool transitionRunning() const noexcept
    {
        return mApplication.transitionRunning();
    }
    std::size_t sceneDepth() const noexcept
    {
        return mApplication.controllerDepth();
    }
    Viewport viewport() const noexcept
    {
        return mApplication.viewport();
    }

private:
    explicit RoundDemo(Display* display) noexcept;

    void onAction(ViewController& controller, ViewId action) noexcept override;
    void onBack(ViewController& controller) noexcept override;
    bool onGesture(ViewController& controller, GestureEvent event) noexcept override;
    bool showGestureLab() noexcept;
    bool showOverlay() noexcept;
    bool dismissTop() noexcept;

    Application mApplication;
    RoundSceneView mDashboard;
    RoundSceneView mGestureLab;
    View mOverlayRoot;
    RoundOverlayView mOverlayPanel;
    ButtonView mMoreButton;
    ButtonView mGestureLabBackButton;
    ButtonView mCloseButton;
    ViewController mDashboardController;
    ViewController mGestureLabController;
    ViewController mOverlayController;
    bool mInitialized{false};
};

} // namespace lithe::demo
