// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstdint>

namespace starter
{
// Platform adapters report the same raw device state. The UI task owns debounce.
[[nodiscard]] bool readUserButton() noexcept;
[[nodiscard]] std::uint32_t inputMilliseconds() noexcept;

class UserButton final
{
public:
    [[nodiscard]] constexpr bool pressed(bool raw, std::uint32_t now) noexcept
    {
        if (raw != mCandidate)
        {
            mCandidate = raw;
            mChangedAt = now;
        }
        // Unsigned elapsed time is valid across the millisecond counter wrap.
        if (mStable != mCandidate && now - mChangedAt >= kDebounceMs)
        {
            mStable = mCandidate;
            return mStable;
        }
        return false;
    }

private:
    static constexpr std::uint32_t kDebounceMs{20U};
    bool mCandidate{};
    bool mStable{};
    std::uint32_t mChangedAt{};
};
} // namespace starter
