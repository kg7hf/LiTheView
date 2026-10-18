// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include <cstdint>

namespace tiny
{
enum class Event : std::uint8_t
{
    previous,
    next,
    select,
    back
};
enum class Page : std::uint8_t
{
    home,
    setup,
    volume,
    info
};
enum class Effect : std::uint8_t
{
    none,
    playbackChanged,
    lightChanged,
    volumeChanged
};

// No display, operating system, time source or device dependency.
class Menu
{
public:
    [[nodiscard]] Effect dispatch(Event event) noexcept
    {
        if (mPage == Page::volume)
        {
            if (event == Event::previous && mDraft > 0)
                --mDraft;
            if (event == Event::next && mDraft < 100)
                ++mDraft;
            if (event == Event::back)
            {
                mDraft = mVolume;
                mPage = Page::home;
            }
            if (event == Event::select)
            {
                const bool changed = mVolume != mDraft;
                mVolume = mDraft;
                mPage = Page::home;
                return changed ? Effect::volumeChanged : Effect::none;
            }
            return Effect::none;
        }
        if (mPage == Page::info)
        {
            if (event == Event::back || event == Event::select)
                mPage = Page::setup;
            return Effect::none;
        }
        auto& index = mPage == Page::home ? mHomeIndex : mSetupIndex;
        const std::uint8_t last = mPage == Page::home ? 2 : 1;
        if (event == Event::next)
        {
            if (index == last)
                index = 0;
            else
                ++index;
        }
        if (event == Event::previous)
        {
            if (index == 0)
                index = last;
            else
                --index;
        }
        if (event == Event::back)
            mPage = Page::home;
        if (event != Event::select)
            return Effect::none;
        if (mPage == Page::setup)
        {
            if (index == 1)
            {
                mPage = Page::info;
                return Effect::none;
            }
            mLight = !mLight;
            return Effect::lightChanged;
        }
        if (index == 0)
        {
            mPlaying = !mPlaying;
            return Effect::playbackChanged;
        }
        if (index == 1)
        {
            mDraft = mVolume;
            mPage = Page::volume;
        }
        if (index == 2)
            mPage = Page::setup;
        return Effect::none;
    }
    [[nodiscard]] Page page() const noexcept
    {
        return mPage;
    }
    [[nodiscard]] std::uint8_t selection() const noexcept
    {
        return mPage == Page::setup ? mSetupIndex : mHomeIndex;
    }
    [[nodiscard]] std::uint8_t volume() const noexcept
    {
        return mVolume;
    }
    [[nodiscard]] std::uint8_t draft() const noexcept
    {
        return mDraft;
    }
    [[nodiscard]] bool playing() const noexcept
    {
        return mPlaying;
    }
    [[nodiscard]] bool light() const noexcept
    {
        return mLight;
    }

private:
    Page mPage{Page::home};
    std::uint8_t mHomeIndex{};
    std::uint8_t mSetupIndex{};
    std::uint8_t mVolume{50};
    std::uint8_t mDraft{50};
    bool mPlaying{};
    bool mLight{};
};
} // namespace tiny
