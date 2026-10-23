// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/sdr_screen.hpp"
#include "Application/ui.hpp"
#include "Devices/display.hpp"
#include "Devices/input.hpp"
#include "Devices/user_button.hpp"

namespace starter::ui
{
namespace
{
Display display;
sdr::Screen application{display};
UserButton userButton;
bool pressed{};
std::uint32_t lastSample{};
} // namespace

std::expected<void, Error> initialize() noexcept
{
    if (const auto result = display.initialize(); !result)
        return result;
    if (const auto result = initializeInput(); !result)
        return result;
    if (!application.initialize())
        return std::unexpected(Error::layout);
    lastSample = inputMilliseconds();
    return {};
}

std::expected<void, Error> update() noexcept
{
    const auto now = inputMilliseconds();
    if (userButton.pressed(readUserButton(), now))
        (void)application.key(lithe::Key::select);

    for (std::size_t index = 0U; index < 64U; ++index)
    {
        const auto read = readPointer();
        if (!read)
        {
            application.cancelPointer();
            return std::unexpected(read.error());
        }
        if (!read->has_value())
            break;
        const auto sample = **read;
        if (sample.key)
        {
            (void)application.key(*sample.key);
            continue;
        }
        if (sample.cancelled)
        {
            application.cancelPointer();
            pressed = false;
            continue;
        }
        const auto phase = sample.pressed
                               ? (pressed ? lithe::PointerPhase::moved : lithe::PointerPhase::pressed)
                               : lithe::PointerPhase::released;
        pressed = sample.pressed;
        (void)application.pointer({{sample.x, sample.y}, phase, 0U});
    }

    if (now - lastSample >= 100U)
    {
        lastSample = now;
        if (!application.tick())
            return std::unexpected(Error::scene);
    }
    if (!application.render())
        return std::unexpected(Error::render);
    return {};
}
} // namespace starter::ui
