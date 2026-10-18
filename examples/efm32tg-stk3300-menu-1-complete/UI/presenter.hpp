// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include "Application/menu.hpp"
#include "UI/glass.hpp"
// Standalone consumers of the completed presenter use its generated contract.
#ifndef TINY_HAS_UI
#define TINY_HAS_UI 1
#endif
#if TINY_HAS_UI
#include "UI/Generated/segmented/home.hpp"
#endif
namespace tiny
{
struct Presentation
{
    std::string_view label;
    unsigned value;
    unsigned volumePercent;
};
[[nodiscard]] inline Presentation present(const Menu& menu) noexcept
{
    const auto volume = menu.volume();
    switch (menu.page())
    {
    case Page::volume:
        return {"VOLUME", menu.draft(), menu.draft()};
    case Page::info:
        return {"INFO", 3300, volume};
    case Page::setup:
        return menu.selection() == 0 ? Presentation{"LIGHT", menu.light() ? 1U : 0U, volume}
                                     : Presentation{"INFO", 3300, volume};
    case Page::home:
        if (menu.selection() == 0)
            return {menu.playing() ? "PAUSE" : "PLAY", menu.playing() ? 1U : 0U, volume};
        if (menu.selection() == 1)
            return {"VOLUME", volume, volume};
        return {"SETUP", 0, volume};
    }
    return {"", 0, volume};
}
[[nodiscard]] inline std::optional<Frame> encodePresentation(const Presentation& view) noexcept
{
#if TINY_HAS_UI
    // Field names are the designer contract. Menu state and draft/save/cancel stay above it.
    return lithe_generated::screen_home::encode({.field_label = view.label,
                                                .field_value = view.value,
                                                .field_volumePercent = view.volumePercent});
#else
    // Before the authoring lesson, the application runs with all electrodes off.
    (void)view;
    return Frame{};
#endif
}
#if TINY_HAS_UI
static_assert(lithe_generated::screen_home::Frame::kSegmentCount == Frame::kSegmentCount);
#endif
} // namespace tiny
