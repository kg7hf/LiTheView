// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/ui.hpp"

#include "Devices/input.hpp"
#include "Devices/user_button.hpp"
#include "Application/status.hpp"
#include "Lessons/FirstDisplay/display.hpp"
#include "Tasks/UI/Generated/resources.hpp"
#include "lithe/application.hpp"
#include "starter_form.hpp"

#include <concepts>

namespace starter::ui
{
namespace
{
// This task owns the form, its controller, resources and all control mutations.
// Resources are declared first and outlive the form. Application detaches before
// the form is destroyed (these application objects have process lifetime).
lithe::generated::SelectedResources resources;
Display display;
UiForm form;
lithe::Application application{display};
bool pressed{false};
UserButton userButton;

// The bonus lesson adds this public application handler to the handwritten form.
// Detection keeps earlier tutorial steps buildable with the same presets.
template <typename Form>
void dispatchUserButton(Form& target) noexcept
{
    if constexpr (requires { { target.onUserButtonPressed() } noexcept -> std::same_as<void>; })
    {
        if (userButton.pressed(readUserButton(), inputMilliseconds()))
        {
            target.onUserButtonPressed();
            status.buttonPresses.fetch_add(1U, std::memory_order_relaxed);
        }
    }
}
} // namespace
std::expected<void, Error> initialize() noexcept
{
    if (const auto result = display.initialize(); !result)
    {
        return result;
    }
    if constexpr (lithe::generated::DisplayConfiguration::kInput.pointer)
    {
        if (const auto result = initializeInput(); !result)
        {
            return result;
        }
    }
    if (!form.build(lithe::font5x7(), &resources) || form.controller() == nullptr)
    {
        return std::unexpected(Error::layout);
    }
    if (!application.pushScene(*form.controller()))
    {
        return std::unexpected(Error::scene);
    }
    return update();
}
std::expected<void, Error> update() noexcept
{
    dispatchUserButton(form);
    if constexpr (lithe::generated::DisplayConfiguration::kInput.pointer)
    {
        // Bound each drain so a stream of mouse moves cannot starve rendering.
        constexpr std::size_t kMaxSamples{64U};
        for (std::size_t index = 0U; index < kMaxSamples; ++index)
        {
            const auto read = readPointer();
            if (!read)
            {
                (void)form.controller()->cancelPointerCapture();
                return std::unexpected(read.error());
            }
            if (!read->has_value())
            {
                break;
            }
            const auto sample = **read;
            if (sample.cancelled)
            {
                (void)form.controller()->cancelPointerCapture();
                pressed = false;
                continue;
            }
            const auto phase = sample.pressed ? (pressed ? lithe::PointerPhase::moved
                                                         : lithe::PointerPhase::pressed)
                                              : lithe::PointerPhase::released;
            pressed = sample.pressed;
            (void)application.handlePointer(lithe::PointerEvent{{sample.x, sample.y}, phase, 0U});
        }
    }
    if (!application.render())
    {
        return std::unexpected(Error::render);
    }
    return {};
}
} // namespace starter::ui
