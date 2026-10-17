// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/menu.hpp"

#include "Devices/display_contract.hpp"

#include <array>
#include <cstdio>
namespace
{
class Display final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return lithe::Surface{lithe::Rgb888Surface{mPixels, 390, 390, 390}};
    }
    void present(std::span<const lithe::Rect>) noexcept override
    {
    }

    [[nodiscard]] bool circleClipped() const noexcept
    {
        const auto corner = mPixels.front();
        const auto edge = mPixels[195U * 390U];
        return corner.red == 0U && corner.green == 0U && corner.blue == 0U && edge.red != 0U;
    }

private:
    std::array<lithe::Rgb888Pixel, 390U * 390U> mPixels{};
};
Display display;
menu::Menu application{display,
                       lithe::generated::assets::HomeMenuMainForm::DisplayConfiguration::kViewport};
} // namespace

int main()
{
    const auto check = [](bool value, const char* message)
    {
        if (!value)
        {
            std::printf("FAIL: %s\n", message);
        }
        return value;
    };
    if (!check(!application.key(lithe::Key::select), "input before initialization") ||
        !check(application.initialize(), "build generated menu forms") ||
        !check(!application.initialize(), "reject repeated initialization") ||
        !check(application.render() && display.circleClipped(),
               "full bounding-square canvas clipped to circle, not inset square") ||
        !check(application.key(lithe::Key::back) && application.depth() == 1U, "Back at Home"))
    {
        return 1;
    }
    const auto click = [](int row)
    {
        const lithe::Point point{135, 154 + row * 65};
        return application.pointer({point, lithe::PointerPhase::pressed, 0U}) &&
               application.pointer({point, lithe::PointerPhase::released, 0U});
    };
    for (int count = 0; count < 100; ++count)
    {
        if (!check(click(0) && click(1) && application.page() == menu::Page::home,
                   "Home actions do not navigate") ||
            !check(click(2) && application.page() == menu::Page::settings &&
                       application.depth() == 2U,
                   "Home Settings handler") ||
            !check(application.navigate(menu::Event::openSettings) && application.depth() == 2U,
                   "duplicate Settings") ||
            (menu::features::hasVolume
                 ? (!check(click(0) && application.page() == menu::Page::volume &&
                               application.depth() == 3U,
                           "Settings Volume handler") ||
                    !check(application.key(lithe::Key::back) &&
                               application.page() == menu::Page::settings,
                           "Volume Back to parent"))
                 : (!check(click(0) && application.page() == menu::Page::settings,
                           "Volume lesson not connected"))) ||
            !check(click(1) && application.page() == menu::Page::storage &&
                       application.depth() == 3U,
                   "Storage handler") ||
            !check(click(0) && application.page() == menu::Page::storage, "Save placeholder") ||
            (menu::features::hasLoad
                 ? (!check(click(1) && application.page() == menu::Page::load &&
                               application.depth() == 4U,
                           "Storage Load handler") ||
                    !check(application.navigate(menu::Event::openLoad) && application.depth() == 4U,
                           "Repeated Load does not push") ||
                    !check(application.key(lithe::Key::back) &&
                               application.page() == menu::Page::storage,
                           "Load Back to Storage"))
                 : (!check(click(1) && application.page() == menu::Page::storage,
                           "Load not connected"))) ||
            !check(application.navigate(menu::Event::openStorage) && application.depth() == 3U,
                   "duplicate Storage") ||
            !check(click(2) && application.page() == menu::Page::settings &&
                       application.depth() == 2U,
                   "Storage Back handler") ||
            !check(application.key(lithe::Key::back) && application.page() == menu::Page::home &&
                       application.depth() == 1U,
                   "logical Back to parent") ||
            !check(application.render(), "render current page"))
        {
            return 1;
        }
    }
    if (!check(application.key(lithe::Key::select) && application.page() == menu::Page::settings,
               "Home Settings focus restored") ||
        !check(click(2) && application.page() == menu::Page::home, "Settings Back handler"))
    {
        return 1;
    }
    menu::Requests requests;
    requests.offer(menu::Event::back);
    requests.offer(menu::Event::openSettings);
    if (!check(requests.overflowed(), "bounded callback request storage"))
    {
        return 1;
    }
    std::puts("PASS: generated handlers, Home/Settings/Storage/Back, repeated "
              "navigation, focus and "
              "bounded requests");
}
