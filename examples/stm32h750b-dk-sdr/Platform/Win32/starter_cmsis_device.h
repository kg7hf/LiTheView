// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <stdint.h>
#include <windows.h>
#include "FreeRTOS.h"
#include "task.h"

// This header is private to the unmodified vendor CMSIS-RTOS2 C wrapper.
extern DWORD starterInterruptThreadId;
extern BaseType_t starterYieldRequested;
static inline uint32_t __get_IPSR(void)
{
    return (GetCurrentThreadId() == starterInterruptThreadId &&
            xTaskGetSchedulerState() != taskSCHEDULER_NOT_STARTED) ? 1U : 0U;
}
static inline uint32_t __get_PRIMASK(void) { return 0U; }
static inline uint32_t __get_BASEPRI(void) { return 0U; }
#define __disable_irq() portDISABLE_INTERRUPTS()
#define __enable_irq() portENABLE_INTERRUPTS()

// A variable deliberately avoids enabling the wrapper's Cortex-M SysTick_Handler.
typedef struct { uint32_t CTRL; uint32_t LOAD; uint32_t VAL; } StarterSysTick;
static StarterSysTick starterSysTick;
static StarterSysTick* const SysTick = &starterSysTick;

// The Windows port's default macro returns from the ISR. CMSIS calls it midway
// through functions that must still return their own result. The ISR wrapper
// below consumes this request instead. Only the interrupt thread accesses it.
#undef portYIELD_FROM_ISR
#define portYIELD_FROM_ISR(value) do { if ((value) != pdFALSE) { starterYieldRequested = pdTRUE; } } while (0)
