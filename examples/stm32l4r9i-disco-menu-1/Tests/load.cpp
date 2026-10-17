// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "Application/menu.hpp"
#include "Devices/display_contract.hpp"

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
        return lithe::Surface{lithe::Rgb888Surface{mPixels, 390U, 390U, 390U}};
    }
    void present(std::span<const lithe::Rect>) noexcept override
    {
    }
    lithe::Color pixel(std::size_t x, std::size_t y) const noexcept
    {
        const auto value = mPixels[y * 390U + x];
        return lithe::Color{value.red, value.green, value.blue, 255U};
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
        {
            return false;
        }
        std::fprintf(file, "P3\n%u %u\n255\n", 390U, 390U);
        for (std::size_t y = 0; y < 390U; ++y)
        {
            for (std::size_t x = 0; x < 390U; ++x)
            {
                const auto color = pixel(x, y);
                std::fprintf(file,
                             "%u %u %u\n",
                             unsigned{color.red},
                             unsigned{color.green},
                             unsigned{color.blue});
            }
        }
        const bool failed = std::ferror(file) != 0;
        return std::fclose(file) == 0 && !failed;
    }

private:
    std::array<lithe::Rgb888Pixel, 390U * 390U> mPixels{};
};
Display display;
menu::Menu application{display,
                       lithe::generated::assets::HomeMenuMainForm::DisplayConfiguration::kViewport};

} // namespace

int main(int argc, char** argv)
{
    const auto check = [](bool ok, const char* why)
    {
        if (!ok)
        {
            std::printf("FAIL: %s\n", why);
        }
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
    const lithe::Point rowB{135, 174}, rowDemo{135, 212}, load{140, 298}, back{245, 298};
    using Id = menu::EntryId;
    if (!check(application.initialize() && application.navigate(menu::Event::openSettings) &&
                   application.navigate(menu::Event::openStorage) &&
                   application.navigate(menu::Event::openLoad) &&
                   application.page() == menu::Page::load && application.depth() == 4U &&
                   application.selectedEntry() == Id::sessionA && !application.loadedEntry() &&
                   application.render(),
               "open Load with selection but no activation") ||
        !snapshot("initial"))
    {
        return 1;
    }
    if (!check(application.pointer({rowB, lithe::PointerPhase::pressed, 0U}) &&
                   application.selectedEntry() == Id::sessionA,
               "press is not selection"))
    {
        return 1;
    }
    application.cancelPointer();
    if (!check(application.pointer({rowB, lithe::PointerPhase::released, 0U}) &&
                   application.selectedEntry() == Id::sessionA,
               "cancelled row does not select") ||
        !check(click(rowB) && application.selectedEntry() == Id::sessionB &&
                   !application.loadedEntry() && application.render(),
               "select B without loading") ||
        !snapshot("selected"))
    {
        return 1;
    }
    if (!check(click(load) && application.loadedEntry() == Id::sessionB && application.render(),
               "Load activates the selected identity") ||
        !snapshot("loaded"))
    {
        return 1;
    }
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
    {
        return 1;
    }
    for (int n = 0; n < 10; ++n)
    {
        (void)application.key(lithe::Key::down);
    }
    if (!check(application.selectedEntry() == Id::factory && application.render(),
               "application scrolls past first viewport") ||
        !snapshot("scrolled"))
    {
        return 1;
    }
    menu::Requests requests;
    menu::LoadMenuForm form{requests};
    if (!check(form.build(lithe::font5x7()) && form.connect(), "connect list model"))
    {
        return 1;
    }
    form.controller()->activate();
    if (!check(form.controller()->handleKey(lithe::Key::down) &&
                   form.selectedEntry() == Id::sessionB && !form.loadedEntry(),
               "Down highlights the next row without loading") ||
        !check(form.controller()->handleKey(lithe::Key::select) &&
                   form.loadedEntry() == Id::sessionB &&
                   form.getLoadedLabel()->text() == "LOADED: SESSION B",
               "center activates the highlighted row"))
    {
        return 1;
    }
    for (int n = 0; n < 10; ++n)
    {
        (void)form.controller()->handleKey(lithe::Key::down);
    }
    if (!check(form.selectedEntry() == Id::factory && form.loadedEntry() == Id::sessionB &&
                   form.getEntryList()->firstVisibleIndex() == 5U,
               "eight-entry catalog scrolls to the final page without loading"))
    {
        return 1;
    }
    if (!check(form.controller()->handleKey(lithe::Key::select) &&
                   form.loadedEntry() == Id::factory,
               "activate a previously hidden item"))
    {
        return 1;
    }
    form.getEntryList()->setEnabled(false);
    form.getEntryList()->activate();
    if (!check(form.loadedEntry() == Id::factory, "disabled list cannot activate"))
    {
        return 1;
    }

    struct Events final : menu::controls::ListEvents
    {
        std::size_t changes{};
        std::size_t activations{};
        std::size_t selected{};
        void onListSelectionChanged(lithe::SelectionChange e) noexcept override
        {
            ++changes;
            selected = e.index;
        }
        void onListActivated(std::size_t index) noexcept override
        {
            ++activations;
            selected = index;
        }
    } events;
    constexpr std::array<std::string_view, 6U> catalog{
        "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX"};
    menu::controls::SelectionList list{{0, 0, 200, 108}};
    if (!check(list.configure(catalog, lithe::font5x7(), events) &&
                   !list.configure(catalog, lithe::font5x7(), events),
               "configure once with borrowed data"))
    {
        return 1;
    }
    for (int n = 0; n < 7; ++n)
    {
        (void)list.handleKey(lithe::Key::down);
    }
    if (!check(list.selectedIndex() == 5U && list.firstVisibleIndex() == 3U &&
                   events.changes == 5U && events.activations == 0U,
               "bounded keyboard scrolling and endpoint clamp"))
    {
        return 1;
    }
    (void)list.handleKey(lithe::Key::up);
    list.activate();
    if (!check(events.selected == 4U && events.activations == 1U, "scroll then activate"))
    {
        return 1;
    }
    list.handlePointerEvent({{10, 10}, lithe::PointerPhase::pressed, 0U});
    list.handlePointerEvent({{10, 50}, lithe::PointerPhase::moved, 0U});
    list.handlePointerEvent({{10, 50}, lithe::PointerPhase::released, 0U});
    list.activate();
    if (!check(list.selectedIndex() == 4U && events.activations == 1U,
               "drag between rows neither selects nor activates"))
    {
        return 1;
    }
    const auto tap = [&](lithe::Point p)
    {
        list.handlePointerEvent({p, lithe::PointerPhase::pressed, 0U});
        list.handlePointerEvent({p, lithe::PointerPhase::released, 0U});
        list.activate(); // Controller's release activation must be suppressed.
    };
    for (int n = 0; n < 10; ++n)
    {
        tap({186, 18});
    }
    if (!check(list.selectedIndex() == 0U && list.firstVisibleIndex() == 0U &&
                   events.activations == 1U,
               "touch Up scrolls back and clamps without activation"))
    {
        return 1;
    }
    for (int n = 0; n < 10; ++n)
    {
        tap({186, 90});
    }
    if (!check(list.selectedIndex() == 5U && list.firstVisibleIndex() == 3U &&
                   events.activations == 1U,
               "touch Down exposes hidden entries and clamps"))
    {
        return 1;
    }
    tap({10, 18});
    if (!check(list.selectedIndex() == 3U && events.selected == 3U && events.activations == 1U,
               "touch selection maps visible row to scrolled catalog identity"))
    {
        return 1;
    }
    list.handlePointerEvent({{186, 90}, lithe::PointerPhase::pressed, 0U});
    list.handlePointerEvent({{186, 18}, lithe::PointerPhase::moved, 0U});
    list.handlePointerEvent({{186, 18}, lithe::PointerPhase::released, 0U});
    list.activate();
    if (!check(list.selectedIndex() == 3U && events.activations == 1U,
               "drag between scroll buttons cancels"))
    {
        return 1;
    }
    menu::controls::SelectionList empty{{0, 0, 200, 108}};
    if (!check(!empty.configure({}, lithe::font5x7(), events), "empty catalog rejected"))
    {
        return 1;
    }
    if (!check(application.render(), "render after return"))
    {
        return 1;
    }
    std::puts("PASS: exclusive selection, explicit load, typed callbacks, "
              "cancellation, keyboard "
              "and retained state");
}
