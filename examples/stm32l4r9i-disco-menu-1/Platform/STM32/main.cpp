// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/app_main.h"
#include "Application/status.hpp"
#include "Board/system_clock.hpp"
#include "Devices/indicator.hpp"
#include "FreeRTOS.h"
#include "cmsis_os2.h"
#include "stm32l4xx_hal.h"
#include "task.h"

#include <array>
extern "C"
{
    volatile std::uint32_t menu_bsp_errors{};
    void xPortSysTickHandler(void);
    // HAL needs a live millisecond tick before scheduler startup too. The RTOS owns
    // SysTick once started; tickless idle is disabled and both rates are 1000 Hz.
    void SysTick_Handler(void)
    {
        (void)SysTick->CTRL;
        HAL_IncTick();
        if (xTaskGetSchedulerState() != taskSCHEDULER_NOT_STARTED)
        {
            xPortSysTickHandler();
        }
    }
    void BSP_ErrorHandler(void)
    {
        menu_bsp_errors = menu_bsp_errors + 1U;
    }
}
namespace
{
StaticTask_t taskControl;
alignas(8) std::array<StackType_t, 2048U> taskStack;
void taskEntry(void*)
{
    appMain();
}
} // namespace
extern "C" void starterAssert(const char*, std::uint32_t line)
{
    starter::status.failureLine.store(line);
    __disable_irq();
    for (;;)
    {
        __WFI();
    }
}
extern "C" void Error_Handler(void)
{
    starterAssert(__FILE__, __LINE__);
}
extern "C" void HardFault_Handler(void)
{
    starterAssert(__FILE__, __LINE__);
}
namespace starter
{
void setHeartbeat(bool) noexcept
{
}
} // namespace starter
extern "C" int main(void)
{
    if (HAL_Init() != HAL_OK || !board::configureSystemClock())
    {
        Error_Handler();
    }
    const osThreadAttr_t attributes{.name = "menuUi",
                                    .attr_bits = 0U,
                                    .cb_mem = &taskControl,
                                    .cb_size = sizeof(taskControl),
                                    .stack_mem = taskStack.data(),
                                    .stack_size = sizeof(taskStack),
                                    .priority = osPriorityBelowNormal,
                                    .tz_module = 0U,
                                    .affinity_mask = 0U};
    if (osKernelInitialize() != osOK || osThreadNew(taskEntry, nullptr, &attributes) == nullptr ||
        osKernelStart() != osOK)
    {
        Error_Handler();
    }
    Error_Handler();
}
