// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "Devices/button.hpp"
#include "Devices/capacitive_position.hpp"
#include "Platform/EFM32/lcd_rows.hpp"
#include "UI/presenter.hpp"

#include <cstdio>
#include <cstdlib>
void check(bool condition)
{
    if (!condition)
    {
        std::fputs("Hardware contract check failed\n", stderr);
        std::abort();
    }
}
int main()
{
    tiny::Button button;
    check(!button.sample(true, 0));
    check(!button.sample(true, 100)); // held at boot cannot activate
    check(!button.sample(false, 101));
    check(!button.sample(false, 121)); // arm
    check(!button.sample(true, 125));
    check(!button.sample(false, 130)); // bounce
    check(!button.sample(false, 150));
    check(!button.sample(true, 155));
    check(!button.sample(true, 175));
    check(!button.sample(false, 180));
    check(button.sample(false, 200));
    check(!button.sample(false, 220));
    check(!button.sample(true, 0xFFFFFFF0U));
    check(!button.sample(true, 5)); // elapsed 21 across wrap
    check(!button.sample(false, 6));
    check(button.sample(false, 26));
    tiny::CapacitivePosition sensor;
    for (unsigned i = 0; i < 32; ++i)
        check(!sensor.sample({1000, 1000, 1000, 1000}));
    check(sensor.calibrated());
    check(!sensor.sample({1000, 1000, 1000, 1000}));
    check(!sensor.sample({900, 1000, 1000, 1000}));
    check(!sensor.sample({0, 1000, 1000, 1000}));
    check(!sensor.sample({65535, 1000, 1000, 1000}));
    check(sensor.sample({500, 1000, 1000, 1000}) == 0);
    check(sensor.sample({1000, 1000, 1000, 500}) == 1000);
    check(sensor.sample({1000, 500, 500, 1000}) == 500);
    tiny::Frame frame;
    check(frame.set(0));
    check(frame.set(159));
    const auto rows = tiny::lcdRows(frame);
    check(rows[7] == 1U && rows[0] == (1U << 19));
    for (unsigned i = 1; i < 7; ++i)
        check(rows[i] == 0);
    // Independent known glyph: L at position 1, numeric 1 at rightmost digit.
    const auto encoded = tiny::encodePresentation({"L", 1, 0});
    check(encoded.has_value());
    const auto glyph = tiny::lcdRows(*encoded);
    check(glyph[7] == 6U && glyph[3] == 2U);
    check(glyph[5] == (1U << 15) && glyph[2] == (1U << 15));
    tiny::Frame ring;
    ring = *tiny::encodePresentation({"", 0, 50});
    const auto half = tiny::lcdRows(ring);
    check(half[0] == 0xE1U); // A7,A0,A1,A2 -> MCU COM0 SEG0,7,6,5
    // The generated complete frame also shows numeric zero at S15.
    constexpr std::array<std::uint32_t, 8> halfWithZero{0xE1U, 0x8000U, 0x8000U, 0x8000U, 0U, 0x8000U, 0x8000U, 0x8000U};
    check(half == halfWithZero);
    ring = *tiny::encodePresentation({"", 0, 100});
    check(tiny::lcdRows(ring)[0] == 0xFFU);
    ring = *tiny::encodePresentation({"", 0, 0});
    check(tiny::lcdRows(ring)[0] == 0U);
    return 0;
}
