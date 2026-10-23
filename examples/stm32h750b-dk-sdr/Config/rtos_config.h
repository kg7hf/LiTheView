// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <stdint.h>

// Cube owns peripheral configuration. These application overrides survive regeneration.
// Its SystemCoreClock declaration belongs to the C platform ABI in C++ builds too.
#ifdef __cplusplus
extern "C" {
#endif
#include "../Core/Inc/FreeRTOSConfig.h"
#ifdef __cplusplus
}
#endif

#undef configSUPPORT_DYNAMIC_ALLOCATION
#define configSUPPORT_DYNAMIC_ALLOCATION 0
#undef USE_FreeRTOS_HEAP_4
#define INCLUDE_xTaskAbortDelay 1
#define configCHECK_FOR_STACK_OVERFLOW 2
// ST's enumeration API always allocates a temporary array; it is not used here.
#undef configUSE_OS2_THREAD_ENUMERATE
#define configUSE_OS2_THREAD_ENUMERATE 0

// FreeRTOS/CMSIS C configuration boundary. Assertions publish a diagnostic before stopping.
#ifdef __cplusplus
extern "C" {
#endif
void starterAssert(const char* file, uint32_t line);
#ifdef __cplusplus
}
#endif
#undef configASSERT
#define configASSERT(condition) do { if (!(condition)) { starterAssert(__FILE__, __LINE__); } } while (0)
