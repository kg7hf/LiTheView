// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "Application/menu.hpp"

#include <array>
#include <cstdio>
#include <span>
#include <string>
#include <type_traits>
#include <utility>
namespace
{
class Display final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return lithe::Surface{lithe::Rgb565Surface{mPixels, 480U, 272U, 480U}};
    }
    void present(std::span<const lithe::Rect>) noexcept override
    {
    }
    lithe::Color pixel(std::size_t x, std::size_t y) const noexcept
    {
        const auto value = mPixels[y * 480U + x];
        return lithe::Color::fromRgb565(value);
    }
    bool white(std::size_t x, std::size_t y) const noexcept
    {
        const auto color = pixel(x, y);
        return color.red == 255U && color.green == 255U && color.blue == 255U;
    }
    bool snapshot(const std::string& path) const
    {
        auto* file = std::fopen(path.c_str(), "wb");
        if (file == nullptr)
            return false;
        std::fprintf(file, "P3\n%u %u\n255\n", 480U, 272U);
        for (std::size_t y = 0; y < 272U; ++y)
            for (std::size_t x = 0; x < 480U; ++x)
            {
                const auto color = pixel(x, y);
                std::fprintf(file,
                             "%u %u %u\n",
                             unsigned{color.red},
                             unsigned{color.green},
                             unsigned{color.blue});
            }
        const bool failed = std::ferror(file) != 0;
        return std::fclose(file) == 0 && !failed;
    }

private:
    std::array<lithe::Rgb565Pixel, 480U * 272U> mPixels{};
};
Display display;
menu::Menu application{display};

} // namespace

int main(int argc, char** argv)
{
    const auto check = [](bool ok, const char* why)
    {
        if (!ok)
            std::printf("FAIL: %s\n", why);
        return ok;
    };
    const auto click = [](lithe::Point p)
    {
        return application.pointer({p, lithe::PointerPhase::pressed, 0U}) &&
               application.pointer({p, lithe::PointerPhase::released, 0U});
    };
    const auto snapshot = [&](const char* name)
    {
        return argc < 2 || display.snapshot(std::string{std::span{argv, 2U}[1]} + name + ".ppm");
    };
    const lithe::Point rowB{160, 132}, rowDemo{160, 168}, load{165, 230}, back{300, 230};
    using Id = menu::EntryId;
    if (!check(application.initialize() && application.navigate(menu::Event::openSettings) &&
                   application.navigate(menu::Event::openStorage) &&
                   application.navigate(menu::Event::openLoad) &&
                   application.page() == menu::Page::load && application.depth() == 4U &&
                   application.selectedEntry() == Id::sessionA && !application.loadedEntry() &&
                   application.render(),
               "open Load with selection but no activation") ||
        !snapshot("initial"))
        return 1;
    if (!check(application.pointer({rowB, lithe::PointerPhase::pressed, 0U}) &&
                   application.selectedEntry() == Id::sessionA,
               "press is not selection"))
        return 1;
    application.cancelPointer();
    if (!check(application.pointer({rowB, lithe::PointerPhase::released, 0U}) &&
                   application.selectedEntry() == Id::sessionA,
               "cancelled row does not select") ||
        !check(click(rowB) && application.selectedEntry() == Id::sessionB &&
                   !application.loadedEntry() && application.render(),
               "select B without loading") ||
        !snapshot("selected"))
        return 1;
    if (!check(click(load) && application.loadedEntry() == Id::sessionB && application.render(),
               "Load activates the selected identity") ||
        !snapshot("loaded"))
        return 1;
    if (!check(click(rowDemo) && application.selectedEntry() == Id::demoLoop &&
                   application.loadedEntry() == Id::sessionB,
               "changing selection does not load") ||
        !check(application.key(lithe::Key::right) && application.key(lithe::Key::select) &&
                   application.loadedEntry() == Id::demoLoop,
               "keyboard focus and activation") ||
        !check(click(back) && application.page() == menu::Page::storage &&
                   application.navigate(menu::Event::openLoad) &&
                   application.selectedEntry() == Id::demoLoop &&
                   application.loadedEntry() == Id::demoLoop,
               "Back/reopen retains selection and loaded identity"))
        return 1;
    for (int n = 0; n < 10; ++n)
        (void)application.key(lithe::Key::down);
    if (!check(application.selectedEntry() == Id::factory && application.render(),
               "application scrolls past first viewport") || !snapshot("scrolled"))
        return 1;
    menu::Requests requests;
    menu::LoadMenuForm form{requests};
    if (!check(form.build(lithe::font5x7()) && form.connect(), "connect list model"))
        return 1;
    form.controller()->activate();
    if (!check(form.controller()->handleKey(lithe::Key::down) &&
                   form.selectedEntry() == Id::sessionB && !form.loadedEntry(),
               "Down highlights the next row without loading") ||
        !check(form.controller()->handleKey(lithe::Key::select) &&
                   form.loadedEntry() == Id::sessionB &&
                   form.getLoadedLabel()->text() == "LOADED: SESSION B",
               "center activates the highlighted row"))
        return 1;
    for (int n = 0; n < 10; ++n)
        (void)form.controller()->handleKey(lithe::Key::down);
    if (!check(form.selectedEntry() == Id::factory && form.loadedEntry() == Id::sessionB &&
                   form.getEntryList()->firstVisibleIndex() == 5U,
               "eight-entry catalog scrolls to the final page without loading"))
        return 1;
    if (!check(form.controller()->handleKey(lithe::Key::select) &&
                   form.loadedEntry() == Id::factory,
               "activate a previously hidden item"))
        return 1;
    form.getEntryList()->setEnabled(false);
    form.getEntryList()->activate();
    if (!check(form.loadedEntry() == Id::factory, "disabled list cannot activate"))
        return 1;

    if (!check(application.render(), "render after return"))
        return 1;
    std::puts("PASS: exclusive selection, explicit load, typed callbacks, cancellation, keyboard "
              "and retained state");
}
