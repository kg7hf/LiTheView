// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/application.hpp"

#include "lithe/painter.hpp"
#include "lithe/render_config.hpp"

#include <cstdint>
#include <limits>

namespace lithe
{

namespace
{

std::int32_t
transitionDistance(std::int32_t extent, std::uint32_t elapsedMs, std::uint32_t durationMs) noexcept
{
    if (extent <= 0 || durationMs == 0U)
    {
        return 0;
    }
    const std::uint32_t boundedElapsed = elapsedMs < durationMs ? elapsedMs : durationMs;
    const std::int64_t distance = (std::int64_t{extent} * boundedElapsed) / durationMs;
    return distance > std::numeric_limits<std::int32_t>::max()
               ? std::numeric_limits<std::int32_t>::max()
               : static_cast<std::int32_t>(distance); // LITHEVIEW_CAST_BOUNDARY: range checked.
}

} // namespace

bool Application::push(ViewController& controller) noexcept
{
    return pushScene(controller);
}

bool Application::pop() noexcept
{
    if (mDepth == 0U)
    {
        return false;
    }
    return popLayer(mControllers[mDepth - 1U].layer, {});
}

bool Application::pushScene(ViewController& controller, SceneTransitionSpec transition) noexcept
{
    return pushLayer(controller, SceneLayer::scene, transition);
}

bool Application::pushOverlay(ViewController& controller, SceneTransitionSpec transition) noexcept
{
    return pushLayer(controller, SceneLayer::overlay, transition);
}

bool Application::popScene(SceneTransitionSpec transition) noexcept
{
    return popLayer(SceneLayer::scene, transition);
}

bool Application::popOverlay(SceneTransitionSpec transition) noexcept
{
    return popLayer(SceneLayer::overlay, transition);
}

bool Application::pushLayer(ViewController& controller,
                            SceneLayer layer,
                            SceneTransitionSpec transition) noexcept
{
    if (mTransition.active || mDepth >= mControllers.size() ||
        (layer == SceneLayer::overlay && mDepth == 0U) ||
        (layer == SceneLayer::scene && mDepth != 0U &&
         mControllers[mDepth - 1U].layer == SceneLayer::overlay))
    {
        return false;
    }
    for (std::size_t index = 0U; index < mDepth; ++index)
    {
        if (mControllers[index].controller == &controller)
        {
            return false;
        }
    }

    cancelInput();
    const std::size_t incomingIndex = mDepth;
    mControllers[mDepth++] = {&controller, layer};
    controller.activate();
    controller.rootView().invalidate();
    if (incomingIndex == 0U || !transition.animated())
    {
        return true;
    }

    mTransition = {
        transition.type, 0U, transition.durationMs, incomingIndex - 1U, incomingIndex, false, true};
    mControllers[mTransition.outgoingIndex].controller->rootView().invalidate();
    return true;
}

bool Application::popLayer(SceneLayer requiredLayer, SceneTransitionSpec transition) noexcept
{
    if (mTransition.active || mDepth <= 1U || mControllers[mDepth - 1U].layer != requiredLayer)
    {
        return false;
    }

    cancelInput();
    const std::size_t outgoingIndex = mDepth - 1U;
    const std::size_t incomingIndex = outgoingIndex - 1U;
    if (!transition.animated())
    {
        mControllers[outgoingIndex] = {};
        --mDepth;
        ViewController* const incoming = mControllers[incomingIndex].controller;
        incoming->activate();
        incoming->rootView().invalidate();
        return true;
    }

    mTransition = {
        transition.type, 0U, transition.durationMs, outgoingIndex, incomingIndex, true, true};
    mControllers[outgoingIndex].controller->rootView().invalidate();
    mControllers[incomingIndex].controller->rootView().invalidate();
    return true;
}

bool Application::cancelTransition() noexcept
{
    if (!mTransition.active)
    {
        return false;
    }

    cancelInput();
    if (!mTransition.popping)
    {
        mControllers[mTransition.incomingIndex] = {};
        --mDepth;
    }
    ViewController* const restored = mControllers[mTransition.outgoingIndex].controller;
    mTransition = {};
    restored->activate();
    // A transparent overlay may have been drawn at an intermediate translated
    // position. Redraw every now-visible layer so the opaque scene beneath it
    // removes that stale image before the restored state is composed.
    invalidate();
    return true;
}

void Application::completeTransition() noexcept
{
    if (!mTransition.active)
    {
        return;
    }
    const std::size_t incomingIndex = mTransition.incomingIndex;
    if (mTransition.popping)
    {
        mControllers[mTransition.outgoingIndex] = {};
        --mDepth;
    }
    ViewController* const incoming = mControllers[incomingIndex].controller;
    mTransition = {};
    incoming->activate();
    // Completing an overlay transition after an intermediate frame must
    // repaint the underlying scene as well as the overlay. Invalidating only
    // the transparent incoming root leaves the previous translated pixels in
    // the framebuffer.
    invalidate();
}

void Application::cancelInput() noexcept
{
    if (ViewController* controller = activeController())
    {
        controller->cancelPointerCapture();
    }
    mGestures.cancel();
}

ViewController* Application::activeController() noexcept
{
    return mDepth == 0U ? nullptr : mControllers[mDepth - 1U].controller;
}

bool Application::handleKey(Key key) noexcept
{
    ViewController* controller = activeController();
    return !mTransition.active && controller != nullptr && controller->handleKey(key);
}

bool Application::handleTextInput(char32_t codepoint) noexcept
{
    ViewController* controller = activeController();
    return !mTransition.active && controller != nullptr && controller->handleTextInput(codepoint);
}

bool Application::handlePointer(Point point) noexcept
{
    const bool pressed = handlePointer({point, PointerPhase::pressed, 0U, 0U});
    return pressed && handlePointer({point, PointerPhase::released, 0U, 0U});
}

bool Application::handlePointer(PointerEvent event) noexcept
{
    ViewController* const controller = activeController();
    if (mTransition.active || controller == nullptr ||
        (event.phase == PointerPhase::pressed && mViewport.valid() &&
         !mViewport.contains(event.position)))
    {
        return false;
    }

    const GestureRecognition recognition = mGestures.handlePointer(event);
    // Capture continues outside the visible surface, so a drag can finish at
    // the device boundary. The controller must not activate a rectangular
    // control through a clipped corner; gesture completion keeps its original
    // release event and coordinates.
    if (event.phase == PointerPhase::released && mViewport.valid() &&
        !mViewport.contains(event.position))
    {
        event.phase = PointerPhase::cancelled;
    }
    const bool pointerHandled = controller->handlePointer(event);
    const bool gestureHandled =
        recognition.hasEvent && controller->handleGesture(recognition.event);
    return pointerHandled || gestureHandled || recognition.tracking || recognition.hasEvent;
}

std::size_t Application::firstVisibleIndex(std::size_t topIndex) const noexcept
{
    std::size_t first = topIndex;
    while (first > 0U && mControllers[first].layer == SceneLayer::overlay)
    {
        --first;
    }
    return first;
}

bool Application::advanceVisibleScenes(std::uint32_t elapsedMs) noexcept
{
    if (mDepth == 0U)
    {
        return false;
    }
    bool changed = false;
    const std::size_t top = mDepth - 1U;
    const std::size_t first = firstVisibleIndex(top);
    for (std::size_t index = first; index <= top; ++index)
    {
        changed = mControllers[index].controller->rootView().advanceTime(elapsedMs) || changed;
    }
    return changed;
}

bool Application::advanceTime(std::uint32_t elapsedMs) noexcept
{
    bool changed = advanceVisibleScenes(elapsedMs);
    if (mTransition.active)
    {
        const std::uint32_t remaining = mTransition.durationMs - mTransition.elapsedMs;
        mTransition.elapsedMs += elapsedMs < remaining ? elapsedMs : remaining;
        changed = true;
        if (mTransition.elapsedMs >= mTransition.durationMs)
        {
            completeTransition();
        }
        return changed;
    }

    const GestureRecognition recognition = mGestures.advanceTime(elapsedMs);
    if (recognition.hasEvent)
    {
        if (ViewController* controller = activeController())
        {
            changed = controller->handleGesture(recognition.event) || changed;
        }
    }
    return changed;
}

bool Application::visibleScenesAnimating() const noexcept
{
    if (mDepth == 0U)
    {
        return false;
    }
    const std::size_t top = mDepth - 1U;
    const std::size_t first = firstVisibleIndex(top);
    for (std::size_t index = first; index <= top; ++index)
    {
        if (mControllers[index].controller->rootView().animationsRunning())
        {
            return true;
        }
    }
    return false;
}

bool Application::animationsRunning() const noexcept
{
    return mTransition.active || mGestures.clockRunning() || visibleScenesAnimating();
}

bool Application::render() noexcept
{
    return mDisplay != nullptr && render(*mDisplay);
}

bool Application::render(Display& display) noexcept
{
    Surface target = display.surface();
    if (activeController() == nullptr || !target.valid() || !supportsDestination(target.format))
    {
        return false;
    }
    if (renderSurface(target, mDamage, false))
    {
        display.present(mDamage.regions());
    }

    // A valid clean frame is a successful no-op, not a rendering failure.
    // Input events such as a captured move or release are allowed to request a
    // render even when they do not change any pixels.
    return true;
}

void Application::renderVisible(Painter& painter, std::size_t topIndex, bool force) noexcept
{
    const std::size_t first = firstVisibleIndex(topIndex);
    for (std::size_t index = first; index <= topIndex; ++index)
    {
        mControllers[index].controller->rootView().render(painter, {}, force);
    }
}

Size Application::sceneSize() const noexcept
{
    if (mViewport.valid())
    {
        return {mViewport.bounds.width, mViewport.bounds.height};
    }
    if (mDepth == 0U)
    {
        return {};
    }
    const Rect frame = mControllers[mDepth - 1U].controller->rootView().frameInRoot();
    return {frame.width, frame.height};
}

Point Application::transitionOffset(std::size_t index, Size frameSize) const noexcept
{
    const std::int32_t horizontal =
        transitionDistance(frameSize.width, mTransition.elapsedMs, mTransition.durationMs);
    const std::int32_t vertical =
        transitionDistance(frameSize.height, mTransition.elapsedMs, mTransition.durationMs);
    const bool outgoing = index == mTransition.outgoingIndex;
    const bool overlayTransition =
        mControllers[mTransition.outgoingIndex].layer == SceneLayer::overlay ||
        mControllers[mTransition.incomingIndex].layer == SceneLayer::overlay;
    if (overlayTransition &&
        ((!mTransition.popping && outgoing) || (mTransition.popping && !outgoing)))
    {
        return {};
    }

    switch (mTransition.type)
    {
    case SceneTransition::slideLeft:
        return {outgoing ? -horizontal : frameSize.width - horizontal, 0};
    case SceneTransition::slideRight:
        return {outgoing ? horizontal : -frameSize.width + horizontal, 0};
    case SceneTransition::slideUp:
        return {0, outgoing ? -vertical : frameSize.height - vertical};
    case SceneTransition::slideDown:
    case SceneTransition::dropAway:
        return {0, outgoing ? vertical : -frameSize.height + vertical};
    case SceneTransition::none:
        return {};
    }
    return {};
}

void Application::renderTransition(Painter& painter) noexcept
{
    const SceneEntry& outgoing = mControllers[mTransition.outgoingIndex];
    const SceneEntry& incoming = mControllers[mTransition.incomingIndex];
    const bool overlayTransition =
        outgoing.layer == SceneLayer::overlay || incoming.layer == SceneLayer::overlay;
    const Size frameSize = sceneSize();
    if (overlayTransition)
    {
        const std::size_t backgroundTop =
            mTransition.popping ? mTransition.incomingIndex : mTransition.outgoingIndex;
        renderVisible(painter, backgroundTop, true);
        const std::size_t movingIndex =
            mTransition.popping ? mTransition.outgoingIndex : mTransition.incomingIndex;
        mControllers[movingIndex].controller->rootView().render(
            painter, transitionOffset(movingIndex, frameSize), true);
        return;
    }

    incoming.controller->rootView().render(
        painter, transitionOffset(mTransition.incomingIndex, frameSize), true);
    outgoing.controller->rootView().render(
        painter, transitionOffset(mTransition.outgoingIndex, frameSize), true);
}

Viewport Application::effectiveViewport(Surface target) const noexcept
{
    return mViewport.valid() ? mViewport : Viewport::rectangle(target.bounds());
}

bool Application::renderSurface(Surface target, DamageTracker& damage, bool force) noexcept
{
    if (!supportsDestination(target.format))
    {
        return false;
    }
    if (activeController() == nullptr || !target.valid())
    {
        damage.clear();
        return false;
    }
    damage.clear();
    Painter painter(target, damage, effectiveViewport(target));
    if (mTransition.active)
    {
        renderTransition(painter);
    }
    else
    {
        renderVisible(painter, mDepth - 1U, force);
    }
    return !damage.empty();
}

void Application::invalidate() noexcept
{
    if (mDepth == 0U)
    {
        return;
    }
    const std::size_t top = mDepth - 1U;
    const std::size_t first = firstVisibleIndex(top);
    for (std::size_t index = first; index <= top; ++index)
    {
        mControllers[index].controller->rootView().invalidate();
    }
}

void Application::setViewport(Viewport viewport) noexcept
{
    mViewport = viewport.valid() ? viewport : Viewport{};
    invalidate();
}

} // namespace lithe
