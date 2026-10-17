// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Devices/indicator.hpp"

#include "Application/status.hpp"
#include "main.h"

namespace starter
{
void setHeartbeat(bool enabled) noexcept
{
    HAL_GPIO_WritePin(LD1_GPIO_Port, LD1_Pin, enabled ? GPIO_PIN_RESET : GPIO_PIN_SET);
}
} // namespace starter
extern "C" void starterAssert(const char*, std::uint32_t line)
{
    starter::status.failureLine.store(line, std::memory_order_relaxed);
    __disable_irq();
    for (;;)
    {
        __NOP();
    }
}
