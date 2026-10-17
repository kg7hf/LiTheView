// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Board/system_clock.hpp"

#include "stm32l4xx_hal.h"

namespace board
{
std::expected<void, ClockError> configureSystemClock() noexcept
{
    __HAL_RCC_PWR_CLK_ENABLE();
    if (HAL_PWREx_ControlVoltageScaling(PWR_REGULATOR_VOLTAGE_SCALE1_BOOST) != HAL_OK)
    {
        return std::unexpected(ClockError::voltage);
    }
    RCC_OscInitTypeDef oscillator{};
    oscillator.OscillatorType = RCC_OSCILLATORTYPE_MSI;
    oscillator.MSIState = RCC_MSI_ON;
    oscillator.MSICalibrationValue = RCC_MSICALIBRATION_DEFAULT;
    oscillator.MSIClockRange = RCC_MSIRANGE_6;
    oscillator.PLL.PLLState = RCC_PLL_ON;
    oscillator.PLL.PLLSource = RCC_PLLSOURCE_MSI;
    oscillator.PLL.PLLM = 1;
    oscillator.PLL.PLLN = 60;
    oscillator.PLL.PLLP = 7;
    oscillator.PLL.PLLQ = 2;
    oscillator.PLL.PLLR = 2;
    if (HAL_RCC_OscConfig(&oscillator) != HAL_OK)
    {
        return std::unexpected(ClockError::oscillator);
    }
    RCC_ClkInitTypeDef clocks{};
    clocks.ClockType =
        RCC_CLOCKTYPE_SYSCLK | RCC_CLOCKTYPE_HCLK | RCC_CLOCKTYPE_PCLK1 | RCC_CLOCKTYPE_PCLK2;
    clocks.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
    clocks.AHBCLKDivider = RCC_SYSCLK_DIV2;
    clocks.APB1CLKDivider = RCC_HCLK_DIV1;
    clocks.APB2CLKDivider = RCC_HCLK_DIV1;
    if (HAL_RCC_ClockConfig(&clocks, FLASH_LATENCY_3) != HAL_OK)
    {
        return std::unexpected(ClockError::busClock);
    }
    // ST's two-step transition avoids supply undershoot on the jump to 120 MHz.
    clocks.ClockType = RCC_CLOCKTYPE_HCLK;
    clocks.AHBCLKDivider = RCC_SYSCLK_DIV1;
    if (HAL_RCC_ClockConfig(&clocks, FLASH_LATENCY_5) != HAL_OK)
    {
        return std::unexpected(ClockError::busClock);
    }
    return {};
}
} // namespace board
