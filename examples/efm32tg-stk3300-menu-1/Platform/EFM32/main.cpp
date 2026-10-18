// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "Devices/button.hpp"
#include "Devices/capacitive_position.hpp"
#include "Devices/slider.hpp"
#include "Platform/EFM32/board.h"
#include "Platform/EFM32/lcd_rows.hpp"
#include "UI/presenter.hpp"

// Debugger evidence only; no ISR accesses these foreground-owned counters.
struct Diagnostics
{
    std::uint32_t ready{}, ticks{}, events{}, frames{}, sliderFaults{}, calibrated{};
    std::uint32_t samples[4]{};
};
extern "C"
{
    volatile Diagnostics tiny_gecko_status{};
}

int main()
{
    if (!board_init())
    {
        tiny_gecko_status.ready = 0xFFFFFFFFU;
        for (;;)
            board_sleep();
    }
    tiny::Menu menu;
    tiny::Button pb0, pb1;
    tiny::Slider slider;
    tiny::CapacitivePosition position;
    tiny::Frame displayed;
    bool first = true;
    auto dispatch = [&](tiny::Event event)
    {
        const auto effect = menu.dispatch(event);
        if (effect == tiny::Effect::lightChanged)
            board_led(menu.light());
        tiny_gecko_status.events = tiny_gecko_status.events + 1U;
    };
    tiny_gecko_status.ready = 1;
    for (;;)
    {
        const auto now = board_milliseconds();
        tiny_gecko_status.ticks = now;
        if (pb0.sample(board_pb0(), now))
            dispatch(tiny::Event::select);
        if (pb1.sample(board_pb1(), now))
            dispatch(tiny::Event::back);
        tiny::CapacitivePosition::Counts counts{};
        const auto result = board_slider(counts.data());
        if (result < 0)
        {
            slider.release();
            tiny_gecko_status.sliderFaults = tiny_gecko_status.sliderFaults + 1U;
        }
        else if (result > 0)
        {
            for (unsigned i = 0; i < 4; ++i)
                tiny_gecko_status.samples[i] = counts[i];
            if (const auto event = slider.sample(position.sample(counts)))
                dispatch(*event);
            tiny_gecko_status.calibrated = position.calibrated() ? 1U : 0U;
        }
        const auto view = tiny::present(menu);
        if (const auto frame = tiny::encodePresentation(view))
        {
            if (first || *frame != displayed)
            {
                const auto rows = tiny::lcdRows(*frame);
                if (board_display(rows.data()))
                {
                    displayed = *frame;
                    first = false;
                    tiny_gecko_status.frames = tiny_gecko_status.frames + 1U;
                }
            }
        }
        board_sleep();
    }
}
