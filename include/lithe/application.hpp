// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/config.hpp"
#include "lithe/controller.hpp"
#include "lithe/display.hpp"

#include <array>
#include <cstddef>
#include <cstdint>

namespace lithe
{

namespace detail
{

inline constexpr std::uint32_t kDefaultSceneTransitionMilliseconds{220U};

} // namespace detail

enum class SceneLayer : std::uint8_t
{
    scene,
    overlay
};

enum class SceneTransition : std::uint8_t
{
    none,
    slideLeft,
    slideRight,
    slideUp,
    slideDown,
    dropAway
};

struct SceneTransitionSpec
{
    SceneTransition type{SceneTransition::none};
    // Duration is measured in caller-supplied milliseconds. The application
    // owns no timer and advances only through Application::advanceTime().
    std::uint32_t durationMs{detail::kDefaultSceneTransitionMilliseconds};

    constexpr bool animated() const noexcept
    {
        return type != SceneTransition::none && durationMs != 0U;
    }
};

class Application
{
public:
    // Geometry is copied; controllers, views, resources, and an optional bound
    // display remain caller-owned. A null display supports renderSurface() and
    // TiledRenderer without a framebuffer or a dummy output adapter. A display
    // passed to render(Display&) is borrowed only for that call.
    explicit Application(Viewport viewport,
                         Display* display = nullptr,
                         GestureThresholds gestureThresholds = {}) noexcept
        : mDisplay(display), mGestures(gestureThresholds), mViewport(viewport)
    {
    }

    // Compatibility entry point for a bound full-frame display. Invalid or
    // empty viewport bounds default to the complete target surface.
    explicit Application(Display& display,
                         Viewport viewport = {},
                         GestureThresholds gestureThresholds = {}) noexcept
        : Application(viewport, &display, gestureThresholds)
    {
    }

    // Legacy scene-only navigation. These are equivalent to an immediate
    // pushScene()/popScene() and remain for source compatibility.
    bool push(ViewController& controller) noexcept;
    bool pop() noexcept;

    // Scenes and overlays share config::kMaxControllerDepth fixed-capacity
    // storage. A controller must outlive the Application entry that borrows it.
    // Navigation fails closed on duplicate controllers, layer mismatches,
    // capacity exhaustion, or a transition already in progress.
    bool pushScene(ViewController& controller, SceneTransitionSpec transition = {}) noexcept;
    bool pushOverlay(ViewController& controller, SceneTransitionSpec transition = {}) noexcept;
    bool popScene(SceneTransitionSpec transition = {}) noexcept;
    bool popOverlay(SceneTransitionSpec transition = {}) noexcept;

    // Cancelling an animated push removes the incoming layer. Cancelling an
    // animated pop restores the outgoing layer. Both outcomes invalidate the
    // viewport so the next render deterministically redraws the stable state.
    bool cancelTransition() noexcept;
    bool transitionRunning() const noexcept
    {
        return mTransition.active;
    }
    std::size_t controllerDepth() const noexcept
    {
        return mDepth;
    }
    ViewController* activeController() noexcept;

    bool handleKey(Key key) noexcept;
    bool handleTextInput(char32_t codepoint) noexcept;
    // Input is rejected while a scene transition is active. Starting any
    // navigation operation first cancels pointer capture and gesture tracking.
    // Presses must start inside a valid viewport. Captured movement continues
    // outside; an outside release completes gestures but cancels control activation.
    bool handlePointer(PointerEvent event) noexcept;
    bool handlePointer(Point point) noexcept;
    bool advanceTime(std::uint32_t elapsedMs) noexcept;
    bool animationsRunning() const noexcept;
    // Returns false without a bound display. A successful clean frame returns
    // true without a presentation. Neither overload retains a target surface.
    // Damage state is shared across calls; invalidate before rendering into
    // replacement storage that does not contain the previous frame.
    bool render() noexcept;
    [[nodiscard]] bool render(Display& display) noexcept;
    // Returns true when at least one bounded damage region was produced. A
    // valid render clipped entirely outside a circular viewport returns false;
    // this is not a rendering error.
    bool renderSurface(Surface target, DamageTracker& damage, bool force = true) noexcept;
    void invalidate() noexcept;
    void setViewport(Viewport viewport) noexcept;
    Viewport viewport() const noexcept
    {
        return mViewport;
    }

private:
    struct SceneEntry
    {
        ViewController* controller{nullptr};
        SceneLayer layer{SceneLayer::scene};
    };

    struct TransitionState
    {
        SceneTransition type{SceneTransition::none};
        std::uint32_t elapsedMs{0U};
        std::uint32_t durationMs{0U};
        std::size_t outgoingIndex{0U};
        std::size_t incomingIndex{0U};
        bool popping{false};
        bool active{false};
    };

    bool pushLayer(ViewController& controller,
                   SceneLayer layer,
                   SceneTransitionSpec transition) noexcept;
    bool popLayer(SceneLayer requiredLayer, SceneTransitionSpec transition) noexcept;
    void cancelInput() noexcept;
    void completeTransition() noexcept;
    std::size_t firstVisibleIndex(std::size_t topIndex) const noexcept;
    bool advanceVisibleScenes(std::uint32_t elapsedMs) noexcept;
    bool visibleScenesAnimating() const noexcept;
    void renderVisible(Painter& painter, std::size_t topIndex, bool force) noexcept;
    void renderTransition(Painter& painter) noexcept;
    Point transitionOffset(std::size_t index, Size frameSize) const noexcept;
    Size sceneSize() const noexcept;
    Viewport effectiveViewport(Surface target) const noexcept;

    // Like the original reference, this optional binding cannot be reassigned.
    Display* const mDisplay{nullptr};
    DamageTracker mDamage{};
    GestureRecognizer mGestures;
    std::array<SceneEntry, config::kMaxControllerDepth> mControllers{};
    TransitionState mTransition{};
    Viewport mViewport{};
    std::size_t mDepth{0};
};

} // namespace lithe
