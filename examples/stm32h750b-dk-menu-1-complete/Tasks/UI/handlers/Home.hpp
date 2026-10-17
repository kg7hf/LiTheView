// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// Handwritten Home form. Generate UI owns only Generated/.
// LiTheView handwritten form: form.home
#pragma once
#include "Application/navigation.hpp"
#include "Application/playback.hpp"
#include "../playback_controls.hpp"
namespace menu
{
class HomeMenuForm final : public HomePlaybackBase
{
public:
    explicit HomeMenuForm(Requests& requests) noexcept : mRequests{requests}
    {
    }
    [[nodiscard]] PlaybackState playbackState() const noexcept
    {
        return mPlayback.state();
    }
    void refreshPlayback() noexcept
    {
        if (auto* play = getPlayButton(); play != nullptr)
            play->show(mPlayback.state());
        if (auto* stop = getStopButton(); stop != nullptr)
            stop->show();
        if (auto* label = getPlaybackLabel(); label != nullptr)
            label->setText(mPlayback.text());
    }

protected:
    // <litheview-handlers>
    // litheview-handler component.main.play onPlayButtonClicked
    void onPlayButtonClicked() noexcept override
    {
        mPlayback.playPause();
        refreshPlayback();
    }
    // litheview-handler component.main.stop onStopButtonClicked
    void onStopButtonClicked() noexcept override
    {
        mPlayback.stop();
        refreshPlayback();
    }
    // litheview-handler component.main.record onRecordButtonClicked
    void onRecordButtonClicked() noexcept override
    { /* Recording is a later lesson. */
    }
    // litheview-handler component.main.settings onSettingsButtonClicked
    void onSettingsButtonClicked() noexcept override
    {
        mRequests.offer(Event::openSettings);
    }
    // </litheview-handlers>
    void onBack(lithe::ViewController&) noexcept override
    {
        mRequests.offer(Event::back);
    }

private:
    Requests& mRequests;
    Playback mPlayback;
};
} // namespace menu
