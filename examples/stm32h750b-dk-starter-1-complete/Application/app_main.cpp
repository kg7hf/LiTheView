// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/app_main.h"

#include "Application/status.hpp"
#include "Application/ui.hpp"
#include "Config/rtos_config.h"
#include "Devices/indicator.hpp"
#include "cmsis_os2.h"

// Both platform mains enter this same task. UI ownership stays on this task
// when the first-display lesson replaces no_ui.cpp with its implementation.
extern "C" void appMain()
{
    if (!starter::ui::initialize())
    {
        starterAssert(__FILE__, __LINE__);
    }
    starter::status.ready.store(true, std::memory_order_release);
    for (;;)
    {
        if (!starter::ui::update())
        {
            starterAssert(__FILE__, __LINE__);
        }
        const auto count = starter::status.heartbeats.fetch_add(1U, std::memory_order_relaxed);
        starter::setHeartbeat((count / 50U) % 2U != 0U);
        if (osDelay(10U) != osOK)
        {
            starterAssert(__FILE__, __LINE__);
        }
    }
}
