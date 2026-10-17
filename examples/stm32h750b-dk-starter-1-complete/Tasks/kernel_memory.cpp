// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "FreeRTOS.h"
#include "task.h"

#include <array>

namespace
{
StaticTask_t idleControl;
StaticTask_t timerControl;
alignas(8) std::array<StackType_t, configMINIMAL_STACK_SIZE> idleStack;
alignas(8) std::array<StackType_t, configTIMER_TASK_STACK_DEPTH> timerStack;
} // namespace

// Required FreeRTOS C hooks. Kernel objects and buffers live until process/reset end.
extern "C" void vApplicationGetIdleTaskMemory(StaticTask_t** control,
                                              StackType_t** stack,
                                              configSTACK_DEPTH_TYPE* size)
{
    *control = &idleControl;
    *stack = idleStack.data();
    *size = idleStack.size();
}

extern "C" void vApplicationGetTimerTaskMemory(StaticTask_t** control,
                                               StackType_t** stack,
                                               configSTACK_DEPTH_TYPE* size)
{
    *control = &timerControl;
    *stack = timerStack.data();
    *size = timerStack.size();
}

extern "C" void vApplicationStackOverflowHook(TaskHandle_t, char*)
{
    starterAssert(__FILE__, __LINE__);
}

// The unmodified ST CMSIS memory-pool implementation references these C symbols
// even with configSUPPORT_DYNAMIC_ALLOCATION=0. This static allocation policy
// refuses its optional heap fallback. No FreeRTOS heap implementation is linked.
extern "C" void* pvPortMalloc(std::size_t)
{
    return nullptr;
}
extern "C" void vPortFree(void* allocation)
{
    configASSERT(allocation == nullptr);
}
