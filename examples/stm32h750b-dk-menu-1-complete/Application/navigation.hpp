// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <cstdint>
#include <optional>
namespace menu
{
enum class Page : std::uint8_t
{
    home,
    settings,
    storage,
    volume,
    load
};
enum class Event : std::uint8_t
{
    openSettings,
    openStorage,
    openVolume,
    openLoad,
    back
};
class Navigation final
{
public:
    [[nodiscard]] Page page() const noexcept
    {
        return mPage;
    }
    [[nodiscard]] std::optional<Page> destination(Event event) const noexcept
    {
        if (mPage == Page::home && event == Event::openSettings)
        {
            return Page::settings;
        }
        if (mPage == Page::settings && event == Event::openStorage)
        {
            return Page::storage;
        }
        if (mPage == Page::settings && event == Event::openVolume)
        {
            return Page::volume;
        }
        if (mPage == Page::storage && event == Event::openLoad)
        {
            return Page::load;
        }
        if (mPage == Page::load && event == Event::back)
        {
            return Page::storage;
        }
        if ((mPage == Page::storage || mPage == Page::volume) && event == Event::back)
        {
            return Page::settings;
        }
        if (mPage == Page::settings && event == Event::back)
        {
            return Page::home;
        }
        return std::nullopt;
    }
    void commit(Page page) noexcept
    {
        mPage = page;
    }

private:
    Page mPage{Page::home};
};
// One synchronous control dispatch may request one transition. No allocation,
// queue ownership transfer, or form destruction occurs inside a callback.
class Requests final
{
public:
    void offer(Event event) noexcept
    {
        if (mPending)
        {
            mOverflow = true;
            return;
        }
        mPending = event;
    }
    [[nodiscard]] bool overflowed() const noexcept
    {
        return mOverflow;
    }
    [[nodiscard]] std::optional<Event> take() noexcept
    {
        const auto result = mPending;
        mPending.reset();
        return result;
    }

private:
    std::optional<Event> mPending;
    bool mOverflow{};
};
} // namespace menu
