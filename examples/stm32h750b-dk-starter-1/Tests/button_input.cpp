// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Devices/user_button.hpp"
#include "Platform/Win32/front_panel_geometry.hpp"

#include <cstdio>

namespace
{
constexpr bool debounceChecks()
{
    starter::UserButton button;
    if (button.pressed(true, 0U) || button.pressed(false, 5U) || button.pressed(true, 10U) ||
        button.pressed(true, 29U) || !button.pressed(true, 30U) || button.pressed(true, 500U) ||
        button.pressed(false, 510U) || button.pressed(false, 530U) ||
        button.pressed(true, 540U) || !button.pressed(true, 560U))
    {
        return false;
    }
    starter::UserButton wrap;
    return !wrap.pressed(true, 0xfffffff0U) && !wrap.pressed(true, 3U) && wrap.pressed(true, 4U);
}
constexpr bool geometryChecks()
{
    const starter::host::FrontPanelTransform view{1810, 1326, 905, 663};
    const starter::host::Area lcd{232, 166, 1386, 812};
    const starter::host::Area button{344, 1140, 84, 80};
    const auto bounds = view.toWindow(lcd);
    const auto first = view.displayPoint(lcd, bounds.x, bounds.y, 480, 272);
    const auto last = view.displayPoint(lcd, bounds.x + bounds.width - 1, bounds.y + bounds.height - 1, 480, 272);
    const auto pushButton = view.toWindow(button);
    return first && first->x == 0 && first->y == 0 && last && last->x < 480 && last->y < 272 &&
           !view.displayPoint(lcd, bounds.x - 1, bounds.y, 480, 272) &&
           !view.displayPoint(lcd, bounds.x + bounds.width, bounds.y, 480, 272) &&
           pushButton.contains(193, 590) && !pushButton.contains(10, 10) &&
           !view.displayPoint(lcd, 193, 590, 480, 272);
}
static_assert(debounceChecks());
static_assert(geometryChecks());
} // namespace
int main()
{
    std::puts("PASS: bounce, hold, release, repeated press, clock wrap, scaled hit testing and LCD boundaries");
}
