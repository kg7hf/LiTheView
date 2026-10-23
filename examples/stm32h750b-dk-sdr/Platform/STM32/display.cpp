// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Devices/display.hpp"

#include "Application/status.hpp"
#include "Config/rtos_config.h"
#include "cmsis_os2.h"
#include "main.h"

#include <algorithm>
extern "C"
{
    extern SDRAM_HandleTypeDef hsdram1;
    extern LTDC_HandleTypeDef hltdc;
}
namespace starter
{
namespace
{
constexpr std::uintptr_t kFramebufferAddress{0xD0000000U};
constexpr std::uint32_t kPresentTimeoutMs{100U};
constexpr std::uint32_t kSdramRefresh{1542U}; // 100 MHz * 64 ms / 4096 rows - 20 cycles.
constexpr std::uint32_t kIoTimeoutMs{20U};
std::expected<void, ui::Error> sdramCommand(std::uint32_t mode,
                                            std::uint32_t refresh = 1U,
                                            std::uint32_t registerValue = 0U) noexcept
{
    FMC_SDRAM_CommandTypeDef command{mode, FMC_SDRAM_CMD_TARGET_BANK2, refresh, registerValue};
    if (HAL_SDRAM_SendCommand(&hsdram1, &command, kIoTimeoutMs) != HAL_OK)
    {
        return std::unexpected(ui::Error::memory);
    }
    return {};
}
void cleanFramebuffer(std::span<lithe::Rgb565Pixel> pixels) noexcept
{
    static_assert(framebufferBytes % 32U == 0U);
    // LITHEVIEW_CAST_BOUNDARY: CMSIS cache maintenance operates on this fixed physical region.
    SCB_CleanDCache_by_Addr(reinterpret_cast<std::uint32_t*>(pixels.data()), framebufferBytes);
}
} // namespace
std::expected<void, ui::Error> Display::initialize() noexcept
{
    __HAL_LTDC_DISABLE(&hltdc);
    if (!sdramCommand(FMC_SDRAM_CMD_CLK_ENABLE))
    {
        return std::unexpected(ui::Error::memory);
    }
    HAL_Delay(1U); // SDRAM requires at least 100 microseconds after clock enable.
    if (!sdramCommand(FMC_SDRAM_CMD_PALL) || !sdramCommand(FMC_SDRAM_CMD_AUTOREFRESH_MODE, 8U) ||
        !sdramCommand(FMC_SDRAM_CMD_LOAD_MODE, 1U, 0x230U) ||
        HAL_SDRAM_ProgramRefreshRate(&hsdram1, kSdramRefresh) != HAL_OK)
    {
        return std::unexpected(ui::Error::memory);
    }
    // LITHEVIEW_CAST_BOUNDARY: linker/MPU and FMC provision the external framebuffer at this
    // address.
    const std::span storage{reinterpret_cast<lithe::Rgb565Pixel*>(kFramebufferAddress),
                            pixelCount * 2U};
    mScanoutPixels = storage.first(pixelCount);
    mPixels = storage.last(pixelCount);
    std::fill(mScanoutPixels.begin(), mScanoutPixels.end(), 0U);
    std::fill(mPixels.begin(), mPixels.end(), 0U);
    cleanFramebuffer(mScanoutPixels);
    cleanFramebuffer(mPixels);
    if (HAL_LTDC_SetAddress(&hltdc, kFramebufferAddress, 0U) != HAL_OK)
    {
        return std::unexpected(ui::Error::bus);
    }
    __HAL_LTDC_ENABLE(&hltdc);
    // PD7 is the actual display enable. Cube's older PJ15 LCD_DISP label is a data pin.
    HAL_GPIO_WritePin(LCD_DISPD7_GPIO_Port, LCD_DISPD7_Pin, GPIO_PIN_SET);
    return {};
}
lithe::Surface Display::surface() noexcept
{
    return lithe::Surface{lithe::Rgb565Surface{mPixels, width, height, width}};
}
void Display::present(std::span<const lithe::Rect>) noexcept
{
    cleanFramebuffer(mPixels);
    // LITHEVIEW_CAST_BOUNDARY: the HAL LTDC address register accepts a physical 32-bit address.
    const auto address = reinterpret_cast<std::uintptr_t>(mPixels.data());
    if (HAL_LTDC_SetAddress_NoReload(&hltdc, address, 0U) != HAL_OK)
    {
        starterAssert(__FILE__, __LINE__);
        return;
    }
    // This task owns presentation; the LTDC reload interrupt remains disabled.
    // RM0433 section 32.7.6: hardware clears VBR only after the shadow reload.
    // Poll that completion bit, not RRIF (an interrupt flag with RRIE disabled).
    __HAL_LTDC_DISABLE_IT(&hltdc, LTDC_IT_RR);
    __HAL_LTDC_CLEAR_FLAG(&hltdc, LTDC_FLAG_RR);
    __HAL_LTDC_VERTICAL_BLANKING_RELOAD_CONFIG(&hltdc);
    const auto started = HAL_GetTick();
    while ((hltdc.Instance->SRCR & LTDC_SRCR_VBR) != 0U)
    {
        if (HAL_GetTick() - started >= kPresentTimeoutMs || osDelay(1U) != osOK)
        {
            // Fail closed: never recycle a buffer the panel may still be scanning.
            starterAssert(__FILE__, __LINE__);
            return;
        }
    }
    __HAL_LTDC_CLEAR_FLAG(&hltdc, LTDC_FLAG_RR);
    std::swap(mPixels, mScanoutPixels);
    // Damage rendering needs the last complete image in the next render buffer.
    // The retired scanout is now exclusively ours. A bounded full copy keeps the
    // adapter independent of damage coalescing and preserves every unchanged pixel.
    std::copy(mScanoutPixels.begin(), mScanoutPixels.end(), mPixels.begin());
    HAL_GPIO_WritePin(LCD_BL_GPIO_Port, LCD_BL_Pin, GPIO_PIN_SET);
    status.frames.fetch_add(1U, std::memory_order_release);
}
} // namespace starter
