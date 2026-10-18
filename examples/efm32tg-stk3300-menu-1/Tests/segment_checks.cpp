// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "UI/presenter.hpp"
#include "Tests/reference_encoder.hpp"

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
    lithe::SegmentFrame<9> otherGlass;
    check(otherGlass.set(8) && otherGlass.bytes()[1] == std::byte{1}, "synthetic glass last bit");
    const auto before = otherGlass;
    check(!otherGlass.set(9) && otherGlass == before, "bounds preserve frame");
    check(otherGlass.set(8, false) && !otherGlass.test(8), "clear bit");
    // Independent oracle: A03 page 26, position 1 L = D,E,F.
    // D=COM0/S2, E=COM0/S1, F=COM4/S1. Last numeric 1=B,C=COM2/S15,COM5/S15.
    const auto letter = tiny::reference::encode("L", 1);
    check(letter.has_value(), "L supported");
    for (std::size_t i = 0; i < 160 && letter; ++i)
        check(letter->test(i) == (i == 2 || i == 1 || i == 81 || i == 55 || i == 115),
              "L/1 exact electrodes");
    // Fifth alphanumeric A uses A=COM7/S9, B=COM6/S10, C=COM2/S10,
    // E=COM1/S9, F=COM5/S9, G=COM4/S9, M=COM3/S10; numeric zero at S15.
    const auto fifth = tiny::reference::encode("    A", 0);
    constexpr std::array<std::size_t, 13> expected{
        149, 130, 50, 29, 109, 89, 70, 15, 55, 115, 135, 95, 35};
    for (std::size_t i = 0; i < 160 && fifth; ++i)
    {
        bool enabled = false;
        for (const auto id : expected)
            if (i == id)
                enabled = true;
        check(fifth->test(i) == enabled, "fifth position irregular mapping");
    }
    check(fifth.has_value(), "fifth A supported");
    check(!tiny::reference::encode("TOO LONG", 0) && !tiny::reference::encode("?", 0) && !tiny::reference::encode("PLAY", 10000),
          "reject invalid presentation");
    // Independent A03 schematic oracle: top A7=S0, then A0=S7 through A6=S1,
    // all on glass COM7. Thresholds are the eight rounded-eighth boundaries.
    constexpr std::array<std::size_t, 8> ringIds{140, 147, 146, 145, 144, 143, 142, 141};
    constexpr std::array<unsigned, 8> thresholds{7, 19, 32, 44, 57, 69, 82, 94};
    for (unsigned volume = 0; volume <= 100; ++volume)
    {
        auto frame = *letter;
        check(tiny::reference::setVolumeRing(frame, volume), "valid ring value");
        for (std::size_t id = 0; id < 160; ++id)
        {
            bool expectedBit = letter->test(id);
            for (std::size_t piece = 0; piece < 8; ++piece)
                if (id == ringIds[piece])
                    expectedBit = volume >= thresholds[piece];
            check(frame.test(id) == expectedBit, "ring oracle and non-ring preservation");
        }
        check(tiny::reference::setVolumeRing(frame, 0) && frame == *letter, "ring clears stale pieces");
        const auto beforeInvalid = frame;
        check(!tiny::reference::setVolumeRing(frame, 101) && frame == beforeInvalid, "invalid ring is atomic");
    }
    check(!tiny::encodePresentation({"PLAY", 0, 101}), "invalid presentation ring rejected");
    tiny::Menu edit;
    (void)edit.dispatch(tiny::Event::next);
    (void)edit.dispatch(tiny::Event::select);
    for (unsigned i = 0; i < 20; ++i)
        (void)edit.dispatch(tiny::Event::next);
    check(tiny::present(edit).volumePercent == 70 && edit.volume() == 50, "ring follows draft");
    (void)edit.dispatch(tiny::Event::back);
    check(tiny::present(edit).volumePercent == 50, "cancel restores committed ring");
    (void)edit.dispatch(tiny::Event::select);
    for (unsigned i = 0; i < 20; ++i)
        (void)edit.dispatch(tiny::Event::next);
    (void)edit.dispatch(tiny::Event::select);
    check(tiny::present(edit).volumePercent == 70 && edit.volume() == 70, "save retains ring");
    (void)edit.dispatch(tiny::Event::next);
    (void)edit.dispatch(tiny::Event::select);
    check(tiny::present(edit).volumePercent == 70, "ring retained in setup");
    // Every reachable presentation must encode, including all volume drafts.
    for (unsigned sequence = 0; sequence < 65536; ++sequence)
    {
        tiny::Menu menu;
        auto code = sequence;
        for (unsigned step = 0; step < 8; ++step)
        {
            constexpr std::array events{
                tiny::Event::previous, tiny::Event::next, tiny::Event::select, tiny::Event::back};
            (void)menu.dispatch(events[code % 4U]);
            code /= 4U;
            const auto model = tiny::present(menu);
            const auto generated = tiny::encodePresentation(model);
            auto reference = tiny::reference::encode(model.label, model.value);
            check(reference.has_value(), "reference presentation");
            if (reference)
                check(tiny::reference::setVolumeRing(*reference, model.volumePercent), "reference ring");
            check(generated.has_value() && generated == reference, "generated reachable presentation equals baseline");
        }
    }
    for (unsigned value = 0; value <= 100; ++value)
        check(tiny::reference::encode("VOLUME", value).has_value(), "volume range");
    for (unsigned value = 0; value <= 9999; ++value)
    {
        auto reference = tiny::reference::encode("INFO", value);
        if (reference)
            (void)tiny::reference::setVolumeRing(*reference, 50);
        check(tiny::encodePresentation({"INFO", value, 50}) == reference, "generated full numeric range");
    }
    std::printf("Segment checks: %d failures\n", failures);
    return failures == 0 ? 0 : 1;
}
