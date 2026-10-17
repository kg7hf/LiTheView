// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "Application/menu.hpp"
#include "Devices/display_contract.hpp"

#include <array>
#include <cstdio>
#include <string>
#include <type_traits>
#include <utility>
static_assert(std::is_same_v<decltype(std::declval<menu::HomeMenuForm&>().getPlayButton()),
                             menu::controls::PlayPauseButton*>);
static_assert(std::is_same_v<decltype(std::declval<menu::HomeMenuForm&>().getStopButton()),
                             menu::controls::StopButton*>);
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
            return false;
        std::fprintf(file, "P3\n%u %u\n255\n", 390U, 390U);
        for (std::size_t y = 0; y < 390U; ++y)
            for (std::size_t x = 0; x < 390U; ++x)
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
    std::array<lithe::Rgb888Pixel, 390U * 390U> mPixels{};
};
Display display;
menu::Menu application{display, lithe::generated::DisplayConfiguration::kViewport};
int main(int argc, char** argv)
{
    const auto check = [](bool ok, const char* why)
    {
        if (!ok)
            std::printf("FAIL: %s\n", why);
        return ok;
    };
    const auto click = [](lithe::Point point)
    {
        return application.pointer({point, lithe::PointerPhase::pressed, 0U}) &&
               application.pointer({point, lithe::PointerPhase::released, 0U});
    };
    const auto snapshot = [&](const char* state)
    {
        return argc < 2 || display.snapshot(std::string{argv[1]} + state + ".ppm");
    };
    using State = menu::PlaybackState;
    const lithe::Point play{160, 154};
    const lithe::Point stop{280, 154};
    if (!check(application.initialize() && application.playbackState() == State::stopped &&
                   application.render(),
               "initial stopped state") ||
        !check(display.white(77U + 8U, 146U + 7U), "play triangle center") ||
        !check(display.white(247U + 8U, 146U + 7U), "separate stop square") || !snapshot("stopped"))
        return 1;
    if (!check(application.pointer({play, lithe::PointerPhase::pressed, 0U}) &&
                   application.playbackState() == State::stopped,
               "press is not activation") ||
        !check(application.pointer({play, lithe::PointerPhase::released, 0U}) &&
                   application.playbackState() == State::playing && application.render(),
               "release starts playback") ||
        !check(!display.white(77U + 8U, 146U + 7U) && display.white(77U + 10U, 146U + 7U),
               "pause bars with transparent gap") ||
        !snapshot("playing"))
        return 1;
    if (!check(click(play) && application.playbackState() == State::paused && application.render(),
               "click pauses") ||
        !check(display.white(77U + 8U, 146U + 7U), "paused offers play triangle") ||
        !snapshot("paused"))
        return 1;
    if (!check(application.key(lithe::Key::select) && application.playbackState() == State::playing,
               "center resumes") ||
        !check(application.key(lithe::Key::right) && application.key(lithe::Key::select) &&
                   application.playbackState() == State::stopped,
               "focus Stop and center activates") ||
        !check(click(stop) && application.playbackState() == State::stopped,
               "Stop is idempotent") ||
        !check(click(play) && click(play) && click(stop) &&
                   application.playbackState() == State::stopped,
               "Stop from paused"))
        return 1;
    if (!application.pointer({play, lithe::PointerPhase::pressed, 0U}))
        return 1;
    application.cancelPointer();
    if (!check(application.pointer({play, lithe::PointerPhase::released, 0U}) &&
                   application.playbackState() == State::stopped,
               "cancelled touch does not play"))
        return 1;
    if (!check(click(play) && application.navigate(menu::Event::openSettings) &&
                   application.navigate(menu::Event::openStorage) &&
                   application.key(lithe::Key::back) && application.key(lithe::Key::back) &&
                   application.playbackState() == State::playing && application.depth() == 1U,
               "state survives menu navigation"))
        return 1;
    menu::Requests requests;
    menu::HomeMenuForm form{requests};
    if (!check(static_cast<bool>(form.build()), "custom form build"))
        return 1;
    form.refreshPlayback();
    if (!check(form.getPlayButton()->text() == "PLAY" &&
                   form.getPlaybackLabel()->text() == "STOPPED",
               "initial captions"))
        return 1;
    form.getPlayButton()->setEnabled(false);
    (void)form.controller()->handleKey(lithe::Key::select);
    if (!check(form.playbackState() == State::stopped, "disabled custom control does not activate"))
        return 1;
    form.getPlayButton()->setEnabled(true);
    (void)form.controller()->handlePointer({{160, 154}, lithe::PointerPhase::pressed, 0U});
    (void)form.controller()->handlePointer({{160, 154}, lithe::PointerPhase::released, 0U});
    if (!check(form.getPlayButton()->text() == "PAUSE" &&
                   form.getPlaybackLabel()->text() == "PLAYING" &&
                   form.getPlayButton()->icon() == &menu::controls::symbols::kPause,
               "typed replacement and handler update state/caption/symbol"))
        return 1;
    (void)form.controller()->handlePointer({{160, 154}, lithe::PointerPhase::pressed, 0U});
    (void)form.controller()->handlePointer({{0, 0}, lithe::PointerPhase::moved, 0U});
    (void)form.controller()->handlePointer({{0, 0}, lithe::PointerPhase::released, 0U});
    if (!check(form.playbackState() == State::playing, "drag away cancels inherited activation"))
        return 1;
    std::puts("PASS: typed custom controls, native play/pause/stop pixels, generated handlers, "
              "cancellation, disabled state and navigation persistence");
}
