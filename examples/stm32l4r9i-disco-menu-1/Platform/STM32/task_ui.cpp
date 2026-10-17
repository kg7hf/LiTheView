// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/status.hpp"
#include "Application/ui.hpp"
#include "Devices/display_driver/display_driver.hpp"
#include "Devices/input_controller/input_controller.hpp"
#include "Platform/STM32/ui.hpp"
#include "stm32l4xx_hal.h"
namespace starter::ui
{
namespace
{
devices::input_controller::Controller input;
bool dirty{true};
} // namespace
std::expected<void, Error> initialize() noexcept
{
    if (!devices::display_driver::initialize(devices::display_driver::BoardRevision::c))
    {
        return std::unexpected(Error::bus);
    }
    if (!input.initialize())
    {
        return std::unexpected(Error::input);
    }
    if (!::ui::initialize())
    {
        return std::unexpected(Error::layout);
    }
    return {};
}
std::expected<void, Error> update() noexcept
{
    const auto now = HAL_GetTick();
    const auto sample = input.poll();
    if (!sample)
    {
        ::ui::cancelInput(now);
        dirty = true;
    }
    else
    {
        dirty = ::ui::input(*sample, now) || dirty;
    }
    if (!::ui::healthy())
    {
        return std::unexpected(Error::scene);
    }
    const auto ready = devices::display_driver::pollReady();
    if (!ready)
    {
        return std::unexpected(Error::bus);
    }
    if (*ready && dirty)
    {
        if (!::ui::render())
        {
            return std::unexpected(Error::render);
        }
        dirty = false;
        status.frames.fetch_add(1U, std::memory_order_release);
    }
    return {};
}
} // namespace starter::ui
