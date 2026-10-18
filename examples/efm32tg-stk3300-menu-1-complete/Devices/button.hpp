// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include <cstdint>
namespace tiny
{
// Emit once on a debounced release. A button held at boot must first be released.
class Button
{
public:
    [[nodiscard]] bool sample(bool pressed, std::uint32_t now) noexcept
    {
        if (!mInitialized || pressed != mCandidate)
        {
            mInitialized = true;
            mCandidate = pressed;
            mSince = now;
        }
        if (now - mSince < 20U)
            return false;
        if (!mReady)
        {
            mReady = !pressed;
            return false;
        }
        if (pressed == mStable)
            return false;
        mStable = pressed;
        return !pressed;
    }

private:
    std::uint32_t mSince{};
    bool mInitialized{}, mCandidate{}, mStable{}, mReady{};
};
} // namespace tiny
