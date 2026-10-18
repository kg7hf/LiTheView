// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "UI/presenter.hpp"
#include "Tests/reference_encoder.hpp"
#include "piece.hpp"
#include "status.hpp"
#include "tiny.hpp"

#include <array>
#include <cstdio>

namespace generated = lithe_generated::screen_presentation;
namespace status = lithe_generated::screen_status;
namespace piece = lithe_generated::screen_piece;
static_assert(sizeof(generated::Frame) == 20);
static_assert(sizeof(status::Frame) == 3);
static_assert(generated::encode({"PLAY", 0, 50}).has_value());

template <std::size_t Count> constexpr bool boundedFrame()
{
    lithe::SegmentFrame<Count> frame{};
    if (!frame.set(Count - 1U) || !frame.test(Count - 1U) || frame.set(Count) || frame.test(Count))
        return false;
    for (std::size_t id = 0; id + 1U < Count; ++id)
        if (frame.test(id))
            return false;
    return frame.set(Count - 1U, false) && !frame.test(Count - 1U);
}
static_assert(boundedFrame<1>() && boundedFrame<8>() && boundedFrame<9>() && boundedFrame<4096>());

bool compare(const tiny::Presentation& model)
{
    auto expected = tiny::reference::encode(model.label, model.value);
    if (expected && !tiny::reference::setVolumeRing(*expected, model.volumePercent))
        expected.reset();
    const auto actual = generated::encode({model.label, model.value, model.volumePercent});
    return expected == actual;
}

int main()
{
    // This independently specified oracle is not calculated from the glass JSON.
    constexpr std::array<unsigned, 8> thresholds{7, 19, 32, 44, 57, 69, 82, 94};
    constexpr std::array<std::size_t, 8> ids{140, 147, 146, 145, 144, 143, 142, 141};
    for (unsigned volume = 0; volume <= 100; ++volume)
    {
        const auto frame = generated::encode({"VOLUME", volume, volume});
        if (!frame || !compare({"VOLUME", volume, volume}))
            return 1;
        for (std::size_t i = 0; i < ids.size(); ++i)
            if (frame->test(ids[i]) != (volume >= thresholds[i]))
                return 2;
    }
    for (unsigned value = 0; value <= 9999; ++value)
        if (!compare({"INFO", value, 50}))
            return 3;
    constexpr std::string_view glyphs{" AEFGHILNOPSTUVYM"};
    for (char glyph : glyphs)
        if (!compare({std::string_view{&glyph, 1}, 0, 0}))
            return 4;
    if (generated::encode({"TOO LONG", 0, 0}) || generated::encode({"B", 0, 0}) ||
        generated::encode({"", 10000, 0}) || generated::encode({"", 0, 101}))
        return 5;
    // Exercise the existing, display-independent FSM without modifying it.
    constexpr std::array events{
        tiny::Event::previous, tiny::Event::next, tiny::Event::select, tiny::Event::back};
    for (unsigned history = 0; history < 65536; ++history)
    {
        tiny::Menu menu;
        unsigned remaining = history;
        for (unsigned step = 0; step < 8; ++step)
        {
            (void)menu.dispatch(events[remaining % 4U]);
            remaining /= 4U;
            if (!compare(tiny::present(menu)))
                return 6;
        }
    }
    // A different glass uses sparse IDs and a multi-electrode Boolean icon.
    for (unsigned value = 0; value <= 3; ++value)
        for (bool ready : {false, true})
        {
            const auto frame = status::encode({ready, value});
            if (!frame || frame->test(16) != ready || frame->test(3) != ready ||
                frame->test(9) != (value >= 1) || frame->test(0) != (value >= 3))
                return 7;
            for (std::size_t id = 0; id < 17; ++id)
                if (id != 16 && id != 3 && id != 9 && id != 0 && frame->test(id))
                    return 8;
        }
    if (status::encode({true, 4}))
        return 9;
    for (bool ready : {false, true})
        for (bool enabled : {false, true})
        {
            const auto frame = piece::encode({ready, enabled});
            if (!frame || frame->test(16) != ready || frame->test(3) != ready ||
                frame->test(9) != enabled || frame->test(0))
                return 10;
        }
    std::puts(
        "Generated segmented consumers match native bits, boundaries and 65536 FSM histories.");
    return 0;
}
