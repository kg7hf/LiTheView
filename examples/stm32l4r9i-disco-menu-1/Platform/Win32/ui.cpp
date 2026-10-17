// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/ui.hpp"

#include "Application/menu.hpp"
#include "Application/status.hpp"
#include "Devices/display.hpp"
#include "Devices/input.hpp"
#include "Devices/user_button.hpp"
#include "Tasks/UI/Generated/resources.hpp"
#include "lithe/application.hpp"

namespace starter::ui
{
namespace
{
lithe::generated::SelectedResources resources;
Display display;
menu::Menu application{display, lithe::generated::DisplayConfiguration::kViewport};
bool pressed{};
UserButton userButton;
} // namespace
std::expected<void, Error> initialize() noexcept
{
    if (const auto result = display.initialize(); !result)
    {
        return result;
    }
    if (const auto result = initializeInput(); !result)
    {
        return result;
    }
    if (!application.initialize(&resources))
    {
        return std::unexpected(Error::layout);
    }
    return update();
}
std::expected<void, Error> update() noexcept
{
    if (userButton.pressed(readUserButton(), inputMilliseconds()) &&
        !application.key(lithe::Key::select))
    {
        return std::unexpected(Error::scene);
    }
    for (std::size_t index = 0U; index < 64U; ++index)
    {
        const auto read = readPointer();
        if (!read)
        {
            application.cancelPointer();
            return std::unexpected(read.error());
        }
        if (!read->has_value())
        {
            break;
        }
        const auto sample = **read;
        if (sample.key)
        {
            if (!application.key(*sample.key))
            {
                return std::unexpected(Error::scene);
            }
            continue;
        }
        if (sample.cancelled)
        {
            application.cancelPointer();
            pressed = false;
            continue;
        }
        const auto phase =
            sample.pressed ? (pressed ? lithe::PointerPhase::moved : lithe::PointerPhase::pressed)
                           : lithe::PointerPhase::released;
        pressed = sample.pressed;
        if (!application.pointer({{sample.x, sample.y}, phase, 0U}))
        {
            return std::unexpected(Error::scene);
        }
    }
    if (!application.render())
    {
        return std::unexpected(Error::render);
    }
    return {};
}
} // namespace starter::ui
