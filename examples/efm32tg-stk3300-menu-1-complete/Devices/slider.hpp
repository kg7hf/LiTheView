// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include "Application/menu.hpp"

#include <cstdint>
#include <optional>

namespace tiny
{
// Normalized 0..1000 contact position. First contact establishes an anchor;
// movement of 100 units emits one step. No held-touch auto-repeat in this lesson.
class Slider
{
public:
    [[nodiscard]] std::optional<Event> sample(std::optional<std::uint16_t> position) noexcept
    {
        if (!position || *position > 1000)
        {
            mAnchor.reset();
            return {};
        }
        if (!mAnchor)
        {
            mAnchor = position;
            return {};
        }
        const int current = *position;
        const int anchor = *mAnchor;
        const int distance = current - anchor;
        if (distance >= 100 || distance <= -100)
        {
            mAnchor = position;
            return distance > 0 ? Event::next : Event::previous;
        }
        return {};
    }
    void release() noexcept
    {
        mAnchor.reset();
    }

private:
    std::optional<std::uint16_t> mAnchor;
};
} // namespace tiny
