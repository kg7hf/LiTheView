// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/gesture.hpp"

#include "lithe/detail/arithmetic.hpp"

#include <cstdint>
#include <limits>

namespace lithe
{

namespace
{

constexpr std::int64_t absolute(std::int64_t value) noexcept
{
    return value < 0 ? -value : value;
}

constexpr bool reachedDistance(Point origin, Point position, std::int32_t distance) noexcept
{
    const std::int64_t wideDistance = distance;
    const std::int64_t deltaX = absolute(std::int64_t{position.x} - origin.x);
    const std::int64_t deltaY = absolute(std::int64_t{position.y} - origin.y);
    if (deltaX >= wideDistance || deltaY >= wideDistance)
    {
        return true;
    }
    return (deltaX * deltaX) + (deltaY * deltaY) >= (wideDistance * wideDistance);
}

constexpr bool exceedsDistance(Point origin, Point position, std::int32_t distance) noexcept
{
    const std::int64_t wideDistance = distance;
    const std::int64_t deltaX = absolute(std::int64_t{position.x} - origin.x);
    const std::int64_t deltaY = absolute(std::int64_t{position.y} - origin.y);
    if (deltaX > wideDistance || deltaY > wideDistance)
    {
        return true;
    }
    return (deltaX * deltaX) + (deltaY * deltaY) > (wideDistance * wideDistance);
}

} // namespace

void GestureRecognizer::reset() noexcept
{
    mOrigin = {};
    mPosition = {};
    mPressTimestampMs = 0U;
    mElapsedMs = 0U;
    mContactId = 0U;
    mTracking = false;
    mDragging = false;
    mLongPressEmitted = false;
}

void GestureRecognizer::synchronizeElapsed(std::uint32_t timestampMs) noexcept
{
    const std::uint32_t timestampElapsed = timestampMs - mPressTimestampMs;
    constexpr std::uint32_t kMaximumForwardDelta = std::numeric_limits<std::uint32_t>::max() / 2U;
    if (timestampElapsed <= kMaximumForwardDelta && timestampElapsed > mElapsedMs)
    {
        mElapsedMs = timestampElapsed;
    }
}

GestureEvent GestureRecognizer::makeEvent(GestureType type) const noexcept
{
    return {type,
            mOrigin,
            mPosition,
            {detail::saturatingSubtract(mPosition.x, mOrigin.x),
             detail::saturatingSubtract(mPosition.y, mOrigin.y)},
            mElapsedMs,
            mContactId};
}

GestureRecognition GestureRecognizer::handlePointer(PointerEvent event) noexcept
{
    if (!mThresholds.valid())
    {
        reset();
        return {};
    }

    if (event.phase == PointerPhase::pressed)
    {
        if (mTracking)
        {
            return {{}, false, true};
        }
        reset();
        mOrigin = event.position;
        mPosition = event.position;
        mPressTimestampMs = event.timestampMs;
        mContactId = event.contactId;
        mTracking = true;
        return {{}, false, true};
    }

    if (!mTracking || event.contactId != mContactId)
    {
        return {{}, false, mTracking};
    }

    mPosition = event.position;
    synchronizeElapsed(event.timestampMs);
    if (event.phase == PointerPhase::cancelled)
    {
        const GestureEvent cancelled = makeEvent(GestureType::cancelled);
        reset();
        return {cancelled, true, false};
    }

    if (event.phase == PointerPhase::moved)
    {
        if (!mDragging && !mLongPressEmitted &&
            reachedDistance(mOrigin, mPosition, mThresholds.dragDistance))
        {
            mDragging = true;
            return {makeEvent(GestureType::dragStarted), true, true};
        }
        if (mDragging)
        {
            return {makeEvent(GestureType::dragged), true, true};
        }
        return {{}, false, true};
    }

    if (event.phase != PointerPhase::released)
    {
        return {{}, false, true};
    }

    GestureRecognition result{};
    if (mLongPressEmitted)
    {
        reset();
        return result;
    }

    const std::int64_t deltaX = std::int64_t{mPosition.x} - mOrigin.x;
    const std::int64_t deltaY = std::int64_t{mPosition.y} - mOrigin.y;
    const std::int64_t absoluteX = absolute(deltaX);
    const std::int64_t absoluteY = absolute(deltaY);
    const bool swipe = mElapsedMs <= mThresholds.maximumSwipeMs &&
                       reachedDistance(mOrigin, mPosition, mThresholds.swipeDistance);
    if (swipe)
    {
        if (absoluteX >= absoluteY)
        {
            result.event = makeEvent(deltaX < 0 ? GestureType::swipeLeft : GestureType::swipeRight);
        }
        else
        {
            result.event = makeEvent(deltaY < 0 ? GestureType::swipeUp : GestureType::swipeDown);
        }
        result.hasEvent = true;
    }
    else if (mDragging)
    {
        result.event = makeEvent(GestureType::dragEnded);
        result.hasEvent = true;
    }
    else if (mElapsedMs <= mThresholds.maximumTapMs &&
             !exceedsDistance(mOrigin, mPosition, mThresholds.tapSlop))
    {
        result.event = makeEvent(GestureType::tap);
        result.hasEvent = true;
    }
    reset();
    return result;
}

GestureRecognition GestureRecognizer::advanceTime(std::uint32_t elapsedMs) noexcept
{
    if (!clockRunning())
    {
        return {{}, false, mTracking};
    }
    const std::uint32_t remaining = std::numeric_limits<std::uint32_t>::max() - mElapsedMs;
    mElapsedMs += elapsedMs < remaining ? elapsedMs : remaining;
    if (mElapsedMs < mThresholds.longPressMs ||
        exceedsDistance(mOrigin, mPosition, mThresholds.tapSlop))
    {
        return {{}, false, true};
    }
    mLongPressEmitted = true;
    return {makeEvent(GestureType::longPress), true, true};
}

GestureRecognition GestureRecognizer::cancel() noexcept
{
    if (!mTracking)
    {
        return {};
    }
    const GestureEvent cancelled = makeEvent(GestureType::cancelled);
    reset();
    return {cancelled, true, false};
}

} // namespace lithe
