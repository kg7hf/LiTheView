// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <cstdint>
#include <string_view>
namespace menu
{
enum class PlaybackState : std::uint8_t
{
    stopped,
    playing,
    paused
};
// Demonstration state owned by the application form, never by a button or
// driver.
class Playback final
{
public:
    [[nodiscard]] PlaybackState state() const noexcept
    {
        return mState;
    }
    void playPause() noexcept
    {
        mState = mState == PlaybackState::playing ? PlaybackState::paused : PlaybackState::playing;
    }
    void stop() noexcept
    {
        mState = PlaybackState::stopped;
    }
    [[nodiscard]] std::string_view text() const noexcept
    {
        switch (mState)
        {
        case PlaybackState::playing:
            return "PLAYING";
        case PlaybackState::paused:
            return "PAUSED";
        case PlaybackState::stopped:
            return "STOPPED";
        }
        return "STOPPED";
    }

private:
    PlaybackState mState{PlaybackState::stopped};
};
} // namespace menu
