// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "../../Config/rtos_config.h"
#undef CMSIS_device_header
#define CMSIS_device_header "starter_cmsis_device.h"

// The host port supplies ticks. There is no Cortex-M SysTick or interrupt priority register.
#undef configCPU_CLOCK_HZ
#define configCPU_CLOCK_HZ configTICK_RATE_HZ
#undef vPortSVCHandler
#undef xPortPendSVHandler
