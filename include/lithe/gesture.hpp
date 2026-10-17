// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/types.hpp"

#include <cstdint>

namespace lithe
{

namespace detail
{

inline constexpr std::int32_t kDefaultTapSlopPixels{8};
inline constexpr std::int32_t kDefaultDragDistancePixels{10};
inline constexpr std::int32_t kDefaultSwipeDistancePixels{48};
inline constexpr std::uint32_t kDefaultMaximumTapMilliseconds{500U};
inline constexpr std::uint32_t kDefaultLongPressMilliseconds{600U};
inline constexpr std::uint32_t kDefaultMaximumSwipeMilliseconds{800U};

} // namespace detail

enum class GestureType : std::uint8_t
{
    tap,
    longPress,
    dragStarted,
    dragged,
    dragEnded,
    swipeLeft,
    swipeRight,
    swipeUp,
    swipeDown,
    cancelled
};

struct GestureEvent
{
    GestureType type{GestureType::tap};
    Point origin{};
    Point position{};
    Point delta{};
    std::uint32_t durationMs{0U};
    std::uint8_t contactId{0U};
};

struct GestureThresholds
{
    // Distances are logical pixels. Durations are unsigned milliseconds.
    std::int32_t tapSlop{detail::kDefaultTapSlopPixels};
    std::int32_t dragDistance{detail::kDefaultDragDistancePixels};
    std::int32_t swipeDistance{detail::kDefaultSwipeDistancePixels};
    std::uint32_t maximumTapMs{detail::kDefaultMaximumTapMilliseconds};
    std::uint32_t longPressMs{detail::kDefaultLongPressMilliseconds};
    std::uint32_t maximumSwipeMs{detail::kDefaultMaximumSwipeMilliseconds};

    constexpr bool valid() const noexcept
    {
        return tapSlop >= 0 && dragDistance > 0 && swipeDistance > 0 && maximumTapMs > 0U &&
               longPressMs > 0U && maximumSwipeMs > 0U;
    }
};

struct GestureRecognition
{
    GestureEvent event{};
    bool hasEvent{false};
    bool tracking{false};

    constexpr explicit operator bool() const noexcept
    {
        return hasEvent;
    }
};

// GestureRecognizer is a single-contact, allocation-free state machine. It
// owns no clock. Pointer timestamps provide deterministic event-to-event
// timing, while advanceTime() lets an application-owned timer recognize a
// stationary long press without polling when no contact is active. Invalid
// thresholds fail closed: input is rejected and no tracking state is retained.
// While a contact is tracked, presses and events from every other contact ID
// are consumed without replacing or mutating the original contact state.
class GestureRecognizer final
{
public:
    explicit constexpr GestureRecognizer(GestureThresholds thresholds = {}) noexcept
        : mThresholds(thresholds)
    {
    }

    GestureRecognition handlePointer(PointerEvent event) noexcept;
    GestureRecognition advanceTime(std::uint32_t elapsedMs) noexcept;
    GestureRecognition cancel() noexcept;

    constexpr bool tracking() const noexcept
    {
        return mTracking;
    }

    constexpr bool clockRunning() const noexcept
    {
        return mTracking && !mDragging && !mLongPressEmitted;
    }

    constexpr GestureThresholds thresholds() const noexcept
    {
        return mThresholds;
    }

private:
    void reset() noexcept;
    void synchronizeElapsed(std::uint32_t timestampMs) noexcept;
    GestureEvent makeEvent(GestureType type) const noexcept;

    GestureThresholds mThresholds{};
    Point mOrigin{};
    Point mPosition{};
    std::uint32_t mPressTimestampMs{0U};
    std::uint32_t mElapsedMs{0U};
    std::uint8_t mContactId{0U};
    bool mTracking{false};
    bool mDragging{false};
    bool mLongPressEmitted{false};
};

} // namespace lithe
