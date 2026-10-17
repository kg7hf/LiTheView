// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "Application/menu.hpp"

#include <array>
#include <cstdio>

class Display final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return lithe::Surface{lithe::Rgb565Surface{mPixels, 480U, 272U, 480U}};
    }
    void present(std::span<const lithe::Rect>) noexcept override
    {
        ++frames;
    }
    auto middle() const noexcept
    {
        return mPixels[102U * 480U + 480U / 2];
    }
    std::size_t frames{};

private:
    std::array<lithe::Rgb565Pixel, 480U * 272U> mPixels{};
};
Display display;
menu::Menu application{display};
int main()
{
    const auto check = [](bool valid, const char* message)
    {
        if (!valid)
            std::printf("FAIL: %s\n", message);
        return valid;
    };
    const auto click = [](lithe::Point point)
    {
        return application.pointer({point, lithe::PointerPhase::pressed, 0U}) &&
               application.pointer({point, lithe::PointerPhase::released, 0U});
    };
    if (!check(application.initialize() && application.navigate(menu::Event::openSettings) &&
                   application.navigate(menu::Event::openVolume) && application.volume() == 50 &&
                   application.render(),
               "open Volume at 50 percent"))
        return 1;
    const auto half = display.middle();
    if (!check(click({300, 154}) && application.volume() == 55 && application.render(),
               "plus generated handler") ||
        !check(display.middle() != half, "progress shade follows volume") ||
        !check(click({160, 154}) && application.volume() == 50, "minus generated handler") ||
        !check(application.key(lithe::Key::right) && application.volume() == 55,
               "joystick right") ||
        !check(application.key(lithe::Key::left) && application.volume() == 50, "joystick left") ||
        !check(application.key(lithe::Key::down) && application.volume() == 50,
               "down changes focus only") ||
        !check(application.key(lithe::Key::select) && application.volume() == 55,
               "center activates focused plus") ||
        !check(application.key(lithe::Key::up) && application.key(lithe::Key::select) &&
                   application.volume() == 50,
               "up and center activate minus"))
        return 1;
    // A cancelled contact is not an activation.
    if (!check(application.pointer({{300, 154}, lithe::PointerPhase::pressed, 0U}), "press"))
        return 1;
    application.cancelPointer();
    if (!check(application.pointer({{300, 154}, lithe::PointerPhase::released, 0U}) &&
                   application.volume() == 50,
               "cancelled press leaves volume unchanged"))
        return 1;
    for (int n = 0; n < 30; ++n)
        if (!application.key(lithe::Key::right))
            return 1;
    if (!check(application.volume() == 100 && application.render(), "upper bound"))
        return 1;
    const auto full = display.middle();
    const auto fullFrames = display.frames;
    if (!check(application.key(lithe::Key::right) && application.render() &&
                   display.frames == fullFrames,
               "saturated adjustment does not invalidate unchanged controls"))
        return 1;
    for (int n = 0; n < 30; ++n)
        if (!application.key(lithe::Key::left))
            return 1;
    if (!check(application.volume() == 0 && application.render() && display.middle() != full,
               "lower bound and empty progress"))
        return 1;
    if (!check(application.key(lithe::Key::right) && application.volume() == 5 &&
                   click({160, 210}) && application.page() == menu::Page::settings &&
                   application.depth() == 2U,
               "Back generated handler") ||
        !check(application.key(lithe::Key::right) && application.volume() == 5,
               "horizontal key outside Volume does not edit") ||
        !check(application.navigate(menu::Event::openVolume) && application.volume() == 5 &&
                   application.depth() == 3U,
               "value retained on reopen") ||
        !check(application.navigate(menu::Event::openVolume) && application.depth() == 3U,
               "duplicate open bounded") ||
        !check(application.key(lithe::Key::back) && application.key(lithe::Key::back) &&
                   application.depth() == 1U,
               "logical Back restores parent stack"))
        return 1;
    menu::Volume model;
    menu::Requests requests;
    menu::VolumeMenuForm form{requests, model};
    if (!check(static_cast<bool>(form.build()), "standalone generated Volume form"))
        return 1;
    form.refresh();
    form.adjust(menu::VolumeChange::increase);
    if (!check(form.getLevelLabel()->text() == "55%" && form.getVolumeProgress()->value() == 55,
               "label and progress share application state"))
        return 1;
    for (int n = 0; n < 30; ++n)
        form.adjust(menu::VolumeChange::decrease);
    if (!check(form.getLevelLabel()->text() == "0%" && form.getVolumeProgress()->value() == 0,
               "short percentage text"))
        return 1;
    for (int n = 0; n < 30; ++n)
        form.adjust(menu::VolumeChange::increase);
    if (!check(form.getLevelLabel()->text() == "100%" && form.getVolumeProgress()->value() == 100,
               "three digit percentage text"))
        return 1;
    std::puts("PASS: Volume generated handlers, joystick focus/activation, bounded shared value, "
              "native progress pixels and persistence");
}
