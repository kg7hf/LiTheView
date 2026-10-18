// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "Application/menu.hpp"
#include "Devices/slider.hpp"

#include <array>
#include <cstdio>
namespace
{
int failures{};
void check(bool value, const char* name)
{
    if (!value)
    {
        std::printf("FAIL: %s\n", name);
        ++failures;
    }
}
} // namespace
int main()
{
    using tiny::Event;
    tiny::Menu menu;
    check(menu.dispatch(Event::select) == tiny::Effect::playbackChanged && menu.playing(),
          "play effect");
    (void)menu.dispatch(Event::next);
    (void)menu.dispatch(Event::select);
    check(menu.page() == tiny::Page::volume, "enter volume");
    for (int i = 0; i < 200; ++i)
        (void)menu.dispatch(Event::next);
    check(menu.draft() == 100 && menu.volume() == 50, "saturate draft without commit");
    (void)menu.dispatch(Event::back);
    check(menu.volume() == 50 && menu.selection() == 1, "cancel retains parent selection");
    (void)menu.dispatch(Event::select);
    for (int i = 0; i < 200; ++i)
        (void)menu.dispatch(Event::previous);
    check(menu.draft() == 0, "lower bound");
    check(menu.dispatch(Event::select) == tiny::Effect::volumeChanged && menu.volume() == 0,
          "commit effect");
    (void)menu.dispatch(Event::next);
    (void)menu.dispatch(Event::select);
    check(menu.dispatch(Event::select) == tiny::Effect::lightChanged && menu.light(),
          "light effect");
    (void)menu.dispatch(Event::next);
    (void)menu.dispatch(Event::select);
    check(menu.page() == tiny::Page::info, "info");
    (void)menu.dispatch(Event::next);
    check(menu.page() == tiny::Page::info, "info ignores navigation");
    (void)menu.dispatch(Event::back);
    check(menu.selection() == 1, "setup selection retained");
    (void)menu.dispatch(Event::back);
    check(menu.selection() == 2 && menu.playing(), "home retained");
    (void)menu.dispatch(Event::next);
    check(menu.selection() == 0, "wrap next");
    (void)menu.dispatch(Event::previous);
    check(menu.selection() == 2, "wrap previous");
    tiny::Slider slider;
    check(!slider.sample(400), "first contact anchors");
    check(!slider.sample(490), "jitter below threshold");
    check(slider.sample(500) == Event::next, "positive step");
    check(!slider.sample(500), "stationary contact no repeat");
    check(slider.sample(400) == Event::previous, "negative step");
    slider.release();
    check(!slider.sample(900), "release resets");
    check(!slider.sample(1001) && !slider.sample(100), "invalid sample resets");
    // Exhaustive event sequences to depth eight, without recursive traversal.
    for (unsigned sequence = 0; sequence < 65536; ++sequence)
    {
        tiny::Menu state;
        auto code = sequence;
        for (unsigned i = 0; i < 8; ++i)
        {
            constexpr std::array events{Event::previous, Event::next, Event::select, Event::back};
            (void)state.dispatch(events[code % 4U]);
            code /= 4U;
            check(state.volume() <= 100 && state.draft() <= 100, "bounded values");
            check(state.selection() < (state.page() == tiny::Page::setup ? 2 : 3),
                  "bounded selection");
        }
    }
    std::printf("FSM/input checks: %d failures\n", failures);
    return failures == 0 ? 0 : 1;
}
