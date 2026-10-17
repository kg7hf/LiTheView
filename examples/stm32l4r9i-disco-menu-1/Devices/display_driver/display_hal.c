/**
 * Board adapter derived from STMicroelectronics STM32CubeL4 1.18.2
 * DSI_CmdMode_SingleBuffer/main.c and stm32l4xx_hal_msp.c, and the
 * STM32L4R9I-Discovery LCD power sequence.
 * Copyright (c) 2017 STMicroelectronics. All rights reserved.
 * Adaptations Copyright (c) 2026 Paul R. Decker.
 * SPDX-License-Identifier: BSD-3-Clause
 * See LICENSE.txt and PROVENANCE.md in this directory.
 */

#include "display_hal.h"

#include "gfxmmu_lut_390x390_24bpp.h"
#include "stm32l4r9i_discovery.h"
#include "stm32l4r9i_discovery_io.h"
#include "stm32l4xx_hal.h"

#include <stddef.h>
#include <string.h>

_Static_assert(HSE_VALUE == 16000000U, "DSI reference requires the 16-MHz crystal");
_Static_assert(sizeof(uintptr_t) == sizeof(uint32_t), "DMA bus addresses are 32-bit");

// The linker must reserve this complete section in SRAM3. NOLOAD storage is
// explicitly cleared before GFXMMU or LTDC can access it.
static uint32_t PhysFrameBuffer[91260]
    __attribute__((section(".display_framebuffer"), aligned(16), used));

static GFXMMU_HandleTypeDef GfxmmuHandle;
static LTDC_HandleTypeDef LtdcHandle;
static DSI_HandleTypeDef DsiHandle;
static DMA2D_HandleTypeDef Dma2dHandle;
static volatile uint32_t refresh_completed = 1U;
static volatile uint32_t display_failed = 0U;

static DisplayHalStatus result_from_hal(HAL_StatusTypeDef status)
{
    if (status == HAL_OK)
    {
        return DISPLAY_HAL_OK;
    }
    return status == HAL_TIMEOUT ? DISPLAY_HAL_TIMEOUT : DISPLAY_HAL_ERROR;
}

static uint32_t output_pin(uint32_t pin, uint8_t level)
{
    BSP_IO_WritePin(pin, level);
    if (BSP_IO_ConfigPin(pin, IO_MODE_OUTPUT) != IO_OK)
    {
        return 0U;
    }
    // Use the status-returning bus read: BSP_IO_ReadPin hides a failed read as
    // zero, which cannot verify a low output. These board signals use banks2/3.
    uint8_t address = MFXSTM32L152_REG_ADR_GPIO_STATE2;
    uint32_t mask = pin >> 8U;
    if ((pin & 0x00FF0000U) != 0U)
    {
        address = MFXSTM32L152_REG_ADR_GPIO_STATE3;
        mask = pin >> 16U;
    }
    uint8_t value = 0U;
    if (MFX_IO_ReadMultiple(IO_I2C_ADDRESS, address, &value, 1U) != HAL_OK)
    {
        return 0U;
    }
    return ((value & mask) != 0U) == (level != 0U);
}

static DisplayHalStatus power_on(uint32_t revision)
{
    if (BSP_IO_Init() != IO_OK)
    {
        return DISPLAY_HAL_BOARD_IO_ERROR;
    }
    // The MFX can retain outputs across an MCU-only reset. Assert panel reset
    // before changing either supply, even when the prior firmware left it high.
    if (output_pin(IO_PIN_10, GPIO_PIN_RESET) == 0U)
    {
        return DISPLAY_HAL_BOARD_IO_ERROR;
    }
    if (revision == 1U || revision == 2U)
    {
        if (BSP_IO_ConfigPin(IO_PIN_8, IO_MODE_INPUT) != IO_OK ||
            BSP_IO_ConfigPin(IO_PIN_8, IO_MODE_INPUT_PD) != IO_OK)
        {
            return DISPLAY_HAL_BOARD_IO_ERROR;
        }
        HAL_Delay(1U);
        if (output_pin(IO_PIN_8, GPIO_PIN_RESET) == 0U)
        {
            return DISPLAY_HAL_BOARD_IO_ERROR;
        }
    }
    else if (revision == 3U)
    {
        if (output_pin(IO_PIN_8, GPIO_PIN_RESET) == 0U)
        {
            return DISPLAY_HAL_BOARD_IO_ERROR;
        }
        HAL_Delay(1U);
        if (output_pin(AGPIO_PIN_2, GPIO_PIN_RESET) == 0U)
        {
            return DISPLAY_HAL_BOARD_IO_ERROR;
        }
    }
    else
    {
        return DISPLAY_HAL_ERROR;
    }
    HAL_Delay(15U);
    if (output_pin(IO_PIN_10, GPIO_PIN_SET) == 0U)
    {
        return DISPLAY_HAL_BOARD_IO_ERROR;
    }
    HAL_Delay(120U);
    return DISPLAY_HAL_OK;
}

static HAL_StatusTypeDef configure_clocks(void)
{
    RCC_PeriphCLKInitTypeDef clocks = {0};
    RCC_OscInitTypeDef oscillator = {0};
    HAL_RCC_GetOscConfig(&oscillator);
    if (HAL_RCC_GetSysClockFreq() != 120000000U || oscillator.MSIState != RCC_MSI_ON ||
        oscillator.MSIClockRange != RCC_MSIRANGE_6)
    {
        return HAL_ERROR;
    }

    __HAL_RCC_GFXMMU_CLK_ENABLE();
    __HAL_RCC_GFXMMU_FORCE_RESET();
    __HAL_RCC_GFXMMU_RELEASE_RESET();
    __HAL_RCC_LTDC_CLK_ENABLE();
    __HAL_RCC_LTDC_FORCE_RESET();
    __HAL_RCC_LTDC_RELEASE_RESET();
    __HAL_RCC_DMA2D_CLK_ENABLE();
    __HAL_RCC_DMA2D_FORCE_RESET();
    __HAL_RCC_DMA2D_RELEASE_RESET();
    __HAL_RCC_DSI_CLK_ENABLE();
    __HAL_RCC_DSI_FORCE_RESET();
    __HAL_RCC_DSI_RELEASE_RESET();

    clocks.PeriphClockSelection = RCC_PERIPHCLK_LTDC;
    clocks.PLLSAI2.PLLSAI2Source = RCC_PLLSOURCE_MSI;
    clocks.PLLSAI2.PLLSAI2M = 1;
    clocks.PLLSAI2.PLLSAI2N = 60;
    clocks.PLLSAI2.PLLSAI2R = RCC_PLLR_DIV4;
    clocks.LtdcClockSelection = RCC_LTDCCLKSOURCE_PLLSAI2_DIV4;
    clocks.PLLSAI2.PLLSAI2ClockOut = RCC_PLLSAI2_LTDCCLK;
    HAL_StatusTypeDef status = HAL_RCCEx_PeriphCLKConfig(&clocks);
    if (status != HAL_OK)
    {
        return status;
    }
    if (oscillator.HSEState == RCC_HSE_OFF)
    {
        // Retain ST's board-specific HSE startup workaround.
        GPIO_InitTypeDef pin = {0};
        __HAL_RCC_GPIOH_CLK_ENABLE();
        pin.Mode = GPIO_MODE_OUTPUT_PP;
        pin.Pull = GPIO_NOPULL;
        pin.Speed = GPIO_SPEED_FREQ_VERY_HIGH;
        pin.Pin = GPIO_PIN_0;
        HAL_GPIO_Init(GPIOH, &pin);
        HAL_GPIO_WritePin(GPIOH, GPIO_PIN_0, GPIO_PIN_RESET);
        oscillator.OscillatorType = RCC_OSCILLATORTYPE_HSE;
        oscillator.HSEState = RCC_HSE_ON;
        oscillator.PLL.PLLState = RCC_PLL_NONE;
        status = HAL_RCC_OscConfig(&oscillator);
    }
    else if (oscillator.HSEState != RCC_HSE_ON)
    {
        return HAL_ERROR;
    }
    return status;
}

static HAL_StatusTypeDef configure_display(void)
{
    HAL_StatusTypeDef status = HAL_OK;
    LTDC_LayerCfgTypeDef LayerCfg = {0};
    DSI_PLLInitTypeDef dsiPllInit = {0};
    DSI_PHY_TimerTypeDef PhyTimings = {0};
    DSI_HOST_TimeoutTypeDef HostTimeouts = {0};
    DSI_LPCmdTypeDef LPCmd = {0};
    DSI_CmdCfgTypeDef CmdCfg = {0};

    /* Power on LCD */

    /************************/
    /* GFXMMU CONFIGURATION */
    /************************/
    __HAL_GFXMMU_RESET_HANDLE_STATE(&GfxmmuHandle);
    GfxmmuHandle.Instance = GFXMMU;
    GfxmmuHandle.Init.BlocksPerLine = GFXMMU_192BLOCKS;
    GfxmmuHandle.Init.DefaultValue = 0xFFFFFFFF;
    GfxmmuHandle.Init.Buffers.Buf0Address = (uint32_t)PhysFrameBuffer;
    GfxmmuHandle.Init.Buffers.Buf1Address = 0; /* Not Used */
    GfxmmuHandle.Init.Buffers.Buf2Address = 0; /* Not Used */
    GfxmmuHandle.Init.Buffers.Buf3Address = 0; /* Not Used */
    GfxmmuHandle.Init.Interrupts.Activation = DISABLE;
    GfxmmuHandle.Init.Interrupts.UsedInterrupts = GFXMMU_AHB_MASTER_ERROR_IT; /* Not Used */
    if (HAL_OK != HAL_GFXMMU_Init(&GfxmmuHandle))
    {
        return (HAL_ERROR);
    }

    /* Initialize LUT */
    if (HAL_OK != HAL_GFXMMU_ConfigLut(&GfxmmuHandle, 0, 390, (uint32_t)gfxmmu_lut_config_rgb888))
    {
        return (HAL_ERROR);
    }
    /* Disable non visible lines : from line 390 to 1023 (634 lines) */
    if (HAL_OK != HAL_GFXMMU_DisableLutLines(&GfxmmuHandle, 390, 634))
    {
        return (HAL_ERROR);
    }

    /**********************/
    /* LTDC CONFIGURATION */
    /**********************/

    /* LTDC initialization */
    __HAL_LTDC_RESET_HANDLE_STATE(&LtdcHandle);
    LtdcHandle.Instance = LTDC;
    LtdcHandle.Init.HSPolarity = LTDC_HSPOLARITY_AL;
    LtdcHandle.Init.VSPolarity = LTDC_VSPOLARITY_AL;
    LtdcHandle.Init.DEPolarity = LTDC_DEPOLARITY_AL;
    LtdcHandle.Init.PCPolarity = LTDC_PCPOLARITY_IPC;
    LtdcHandle.Init.HorizontalSync = 0;       /* HSYNC width - 1 */
    LtdcHandle.Init.VerticalSync = 0;         /* VSYNC width - 1 */
    LtdcHandle.Init.AccumulatedHBP = 1;       /* HSYNC width + HBP - 1 */
    LtdcHandle.Init.AccumulatedVBP = 1;       /* VSYNC width + VBP - 1 */
    LtdcHandle.Init.AccumulatedActiveW = 391; /* HSYNC width + HBP + Active width - 1 */
    LtdcHandle.Init.AccumulatedActiveH = 391; /* VSYNC width + VBP + Active height - 1 */
    LtdcHandle.Init.TotalWidth = 392;         /* HSYNC width + HBP + Active width + HFP - 1 */
    LtdcHandle.Init.TotalHeigh = 392;         /* VSYNC width + VBP + Active height + VFP - 1 */
    LtdcHandle.Init.Backcolor.Red = 0;        /* Not used default value */
    LtdcHandle.Init.Backcolor.Green = 0;      /* Not used default value */
    LtdcHandle.Init.Backcolor.Blue = 0;       /* Not used default value */
    LtdcHandle.Init.Backcolor.Reserved = 0xFF;
    if (HAL_LTDC_Init(&LtdcHandle) != HAL_OK)
    {
        return (HAL_ERROR);
    }

    /* LTDC layer 1 configuration */
    LayerCfg.WindowX0 = 0;
    LayerCfg.WindowX1 = 390;
    LayerCfg.WindowY0 = 0;
    LayerCfg.WindowY1 = 390;
    LayerCfg.PixelFormat = LTDC_PIXEL_FORMAT_RGB888;
    LayerCfg.Alpha = 0xFF;                                  /* NU default value */
    LayerCfg.Alpha0 = 0;                                    /* NU default value */
    LayerCfg.BlendingFactor1 = LTDC_BLENDING_FACTOR1_PAxCA; /* Not Used: default value */
    LayerCfg.BlendingFactor2 = LTDC_BLENDING_FACTOR2_PAxCA; /* Not Used: default value */
    LayerCfg.FBStartAdress = GFXMMU_VIRTUAL_BUFFER0_BASE;
    LayerCfg.ImageWidth = 1024; /* 3072-byte virtual stride / 3 bytes per pixel */

    LayerCfg.ImageHeight = 390;
    LayerCfg.Backcolor.Red = 0;   /* Not Used: default value */
    LayerCfg.Backcolor.Green = 0; /* Not Used: default value */
    LayerCfg.Backcolor.Blue = 0;  /* Not Used: default value */
    LayerCfg.Backcolor.Reserved = 0xFF;
    if (HAL_LTDC_ConfigLayer(&LtdcHandle, &LayerCfg, 0U) != HAL_OK)
    {
        return (HAL_ERROR);
    }
    __HAL_LTDC_DISABLE(&LtdcHandle);

    /*********************/
    /* DSI CONFIGURATION */
    /*********************/

    /* DSI initialization */
    __HAL_DSI_RESET_HANDLE_STATE(&DsiHandle);
    DsiHandle.Instance = DSI;
    DsiHandle.Init.AutomaticClockLaneControl = DSI_AUTO_CLK_LANE_CTRL_DISABLE;
    /* We have 1 data lane at 500Mbps => lane byte clock at 500/8 = 62,5 MHZ */
    /* We want TX escape clock at around 20MHz and under 20MHz so clock division is set to 4 */
    DsiHandle.Init.TXEscapeCkdiv = 4;
    DsiHandle.Init.NumberOfLanes = DSI_ONE_DATA_LANE;
    /* We have HSE value at 16 Mhz and we want data lane at 500Mbps */
    dsiPllInit.PLLNDIV = 125;
    dsiPllInit.PLLIDF = DSI_PLL_IN_DIV4;
    dsiPllInit.PLLODF = DSI_PLL_OUT_DIV1;
    if (HAL_DSI_Init(&DsiHandle, &dsiPllInit) != HAL_OK)
    {
        return (HAL_ERROR);
    }

    PhyTimings.ClockLaneHS2LPTime =
        33; /* Tclk-post + Tclk-trail + Ths-exit = [(60ns + 52xUI) + (60ns) + (300ns)]/16ns */
    PhyTimings.ClockLaneLP2HSTime =
        30; /* Tlpx + (Tclk-prepare + Tclk-zero) + Tclk-pre = [150ns + 300ns + 8xUI]/16ns */
    PhyTimings.DataLaneHS2LPTime = 11; /* Ths-trail + Ths-exit = [(60ns + 4xUI) + 100ns]/16ns */
    PhyTimings.DataLaneLP2HSTime =
        21; /* Tlpx + (Ths-prepare + Ths-zero) + Ths-sync = [150ns + (145ns + 10xUI) + 8xUI]/16ns */
    PhyTimings.DataLaneMaxReadTime = 0;
    PhyTimings.StopWaitTime = 7;
    if (HAL_DSI_ConfigPhyTimer(&DsiHandle, &PhyTimings) != HAL_OK)
    {
        return (HAL_ERROR);
    }

    HostTimeouts.TimeoutCkdiv = 1;
    HostTimeouts.HighSpeedTransmissionTimeout = 0;
    HostTimeouts.LowPowerReceptionTimeout = 0;
    HostTimeouts.HighSpeedReadTimeout = 0;
    HostTimeouts.LowPowerReadTimeout = 0;
    HostTimeouts.HighSpeedWriteTimeout = 0;
    HostTimeouts.HighSpeedWritePrespMode = 0;
    HostTimeouts.LowPowerWriteTimeout = 0;
    HostTimeouts.BTATimeout = 0;
    if (HAL_DSI_ConfigHostTimeouts(&DsiHandle, &HostTimeouts) != HAL_OK)
    {
        return (HAL_ERROR);
    }

    LPCmd.LPGenShortWriteNoP = DSI_LP_GSW0P_ENABLE;
    LPCmd.LPGenShortWriteOneP = DSI_LP_GSW1P_ENABLE;
    LPCmd.LPGenShortWriteTwoP = DSI_LP_GSW2P_ENABLE;
    LPCmd.LPGenShortReadNoP = DSI_LP_GSR0P_ENABLE;
    LPCmd.LPGenShortReadOneP = DSI_LP_GSR1P_ENABLE;
    LPCmd.LPGenShortReadTwoP = DSI_LP_GSR2P_ENABLE;
    LPCmd.LPGenLongWrite = DSI_LP_GLW_DISABLE;
    LPCmd.LPDcsShortWriteNoP = DSI_LP_DSW0P_ENABLE;
    LPCmd.LPDcsShortWriteOneP = DSI_LP_DSW1P_ENABLE;
    LPCmd.LPDcsShortReadNoP = DSI_LP_DSR0P_ENABLE;
    LPCmd.LPDcsLongWrite = DSI_LP_DLW_DISABLE;
    LPCmd.LPMaxReadPacket = DSI_LP_MRDP_DISABLE;
    LPCmd.AcknowledgeRequest = DSI_ACKNOWLEDGE_DISABLE;
    if (HAL_DSI_ConfigCommand(&DsiHandle, &LPCmd) != HAL_OK)
    {
        return (HAL_ERROR);
    }

    CmdCfg.VirtualChannelID = 0;
    CmdCfg.ColorCoding = DSI_RGB888;
    CmdCfg.CommandSize = 390;
    CmdCfg.TearingEffectSource = DSI_TE_DSILINK;
    CmdCfg.TearingEffectPolarity = DSI_TE_FALLING_EDGE;
    CmdCfg.HSPolarity = DSI_HSYNC_ACTIVE_LOW;
    CmdCfg.VSPolarity = DSI_VSYNC_ACTIVE_LOW;
    CmdCfg.DEPolarity = DSI_DATA_ENABLE_ACTIVE_HIGH;
    CmdCfg.VSyncPol = DSI_VSYNC_FALLING;
    CmdCfg.AutomaticRefresh = DSI_AR_ENABLE;
    CmdCfg.TEAcknowledgeRequest = DSI_TE_ACKNOWLEDGE_ENABLE;
    if (HAL_DSI_ConfigAdaptedCommandMode(&DsiHandle, &CmdCfg) != HAL_OK)
    {
        return (HAL_ERROR);
    }

    /* Disable the Tearing Effect interrupt activated by default on previous function */
    __HAL_DSI_DISABLE_IT(&DsiHandle, DSI_IT_TE);

    if (HAL_DSI_ConfigFlowControl(&DsiHandle, DSI_FLOW_CONTROL_BTA) != HAL_OK)
    {
        return (HAL_ERROR);
    }

    /* Enable DSI */
    __HAL_DSI_ENABLE(&DsiHandle);

    /*************************/
    /* LCD POWER ON SEQUENCE */
    /*************************/
    /* Step 1 */
    /* Go to command 2 */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0xFE, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    /* IC Frame rate control, set power, sw mapping, mux switch timing command */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x06, 0x62);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0E, 0x80);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0F, 0x80);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x10, 0x71);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x13, 0x81);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x14, 0x81);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x15, 0x82);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x16, 0x82);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x18, 0x88);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x19, 0x55);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1A, 0x10);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1C, 0x99);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1D, 0x03);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1E, 0x03);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1F, 0x03);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x20, 0x03);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x25, 0x03);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x26, 0x8D);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x2A, 0x03);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x2B, 0x8D);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x36, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x37, 0x10);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x3A, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x3B, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x3D, 0x20);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x3F, 0x3A);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x40, 0x30);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x41, 0x1A);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x42, 0x33);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x43, 0x22);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x44, 0x11);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x45, 0x66);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x46, 0x55);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x47, 0x44);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x4C, 0x33);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x4D, 0x22);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x4E, 0x11);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x4F, 0x66);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x50, 0x55);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x51, 0x44);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x57, 0x33);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x6B, 0x1B);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x70, 0x55);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x74, 0x0C);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Go to command 3 */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0xFE, 0x02);
    if (status != HAL_OK)
    {
        return status;
    }
    /* Set the VGMP/VGSP coltage control */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x9B, 0x40);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x9C, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x9D, 0x20);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Go to command 4 */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0xFE, 0x03);
    if (status != HAL_OK)
    {
        return status;
    }
    /* Set the VGMP/VGSP coltage control */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x9B, 0x40);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x9C, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x9D, 0x20);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Go to command 5 */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0xFE, 0x04);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR command */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x5D, 0x10);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR1 timing set */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x00, 0x8D);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x01, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x02, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x03, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x04, 0x10);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x05, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x06, 0xA7);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x07, 0x20);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x08, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR2 timing set */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x09, 0xC2);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0A, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0B, 0x02);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0C, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0D, 0x40);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0E, 0x06);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x0F, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x10, 0xA7);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x11, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR3 timing set */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x12, 0xC2);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x13, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x14, 0x02);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x15, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x16, 0x40);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x17, 0x07);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x18, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x19, 0xA7);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1A, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR4 timing set */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1B, 0x82);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1C, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1D, 0xFF);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1E, 0x05);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x1F, 0x60);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x20, 0x02);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x21, 0x01);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x22, 0x7C);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x23, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR5 timing set */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x24, 0xC2);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x25, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x26, 0x04);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x27, 0x02);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x28, 0x70);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x29, 0x05);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x2A, 0x74);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x2B, 0x8D);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x2D, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR6 timing set */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x2F, 0xC2);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x30, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x31, 0x04);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x32, 0x02);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x33, 0x70);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x34, 0x07);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x35, 0x74);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x36, 0x8D);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x37, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }
    /* VSR marping command */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x5E, 0x20);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x5F, 0x31);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x60, 0x54);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x61, 0x76);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x62, 0x98);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Go to command 6 */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0xFE, 0x05);
    if (status != HAL_OK)
    {
        return status;
    }
    /* Set the ELVSS voltage */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x05, 0x17);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x2A, 0x04);
    if (status != HAL_OK)
    {
        return status;
    }
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x91, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Go back in standard commands */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0xFE, 0x00);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Set the Pixel format */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x3A, 0x07);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Set tear off */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, DSI_SET_TEAR_OFF, 0x0);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Set DSI mode to internal timing added vs ORIGINAL for Command mode */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0xC2, 0x0);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Set memory address MODIFIED vs ORIGINAL */
    {
        uint8_t InitParam1[4] = {0x00, 0x04, 0x01, 0x89};
        uint8_t InitParam2[4] = {0x00, 0x00, 0x01, 0x85};

        status = HAL_DSI_LongWrite(
            &DsiHandle, 0, DSI_DCS_LONG_PKT_WRITE, 4, DSI_SET_COLUMN_ADDRESS, InitParam1);

        if (status != HAL_OK)

        {

            return status;
        }
        status = HAL_DSI_LongWrite(
            &DsiHandle, 0, DSI_DCS_LONG_PKT_WRITE, 4, DSI_SET_PAGE_ADDRESS, InitParam2);
        if (status != HAL_OK)
        {
            return status;
        }
    }

    /* Sleep out */
    status =
        HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P0, DSI_EXIT_SLEEP_MODE, 0x0);
    if (status != HAL_OK)
    {
        return status;
    }

    HAL_Delay(120);

    /* Set default Brightness */
    status = HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P1, 0x51, 200U);
    if (status != HAL_OK)
    {
        return status;
    }

    /* Set display on */
    if (HAL_DSI_ShortWrite(&DsiHandle, 0, DSI_DCS_SHORT_PKT_WRITE_P0, DSI_SET_DISPLAY_ON, 0x0) !=
        HAL_OK)
    {
        return (HAL_ERROR);
    }

    /* Enable DSI Wrapper */
    __HAL_DSI_WRAPPER_ENABLE(&DsiHandle);
    __HAL_LTDC_ENABLE(&LtdcHandle);

    return HAL_OK;
}

DisplayHalStatus display_hal_initialize(uint32_t revision)
{
    const HAL_StatusTypeDef clocks = configure_clocks();
    if (clocks != HAL_OK)
    {
        return result_from_hal(clocks);
    }
    const DisplayHalStatus power = power_on(revision);
    if (power != DISPLAY_HAL_OK)
    {
        return power;
    }
    memset(PhysFrameBuffer, 0, sizeof(PhysFrameBuffer));
    __DSB();
    const HAL_StatusTypeDef initialized = configure_display();
    if (initialized != HAL_OK)
    {
        return result_from_hal(initialized);
    }
    const HAL_StatusTypeDef monitor = HAL_DSI_ConfigErrorMonitor(
        &DsiHandle,
        HAL_DSI_ERROR_ACK | HAL_DSI_ERROR_PHY | HAL_DSI_ERROR_TX | HAL_DSI_ERROR_RX |
            HAL_DSI_ERROR_ECC | HAL_DSI_ERROR_CRC | HAL_DSI_ERROR_PSE | HAL_DSI_ERROR_EOT |
            HAL_DSI_ERROR_OVF | HAL_DSI_ERROR_GEN);
    if (monitor != HAL_OK)
    {
        return result_from_hal(monitor);
    }
    HAL_NVIC_ClearPendingIRQ(DSI_IRQn);
    HAL_NVIC_ClearPendingIRQ(LTDC_IRQn);
    HAL_NVIC_ClearPendingIRQ(LTDC_ER_IRQn);
    HAL_NVIC_SetPriority(DSI_IRQn, 3U, 0U);
    HAL_NVIC_SetPriority(LTDC_IRQn, 3U, 0U);
    HAL_NVIC_SetPriority(LTDC_ER_IRQn, 3U, 0U);
    HAL_NVIC_EnableIRQ(DSI_IRQn);
    HAL_NVIC_EnableIRQ(LTDC_IRQn);
    HAL_NVIC_EnableIRQ(LTDC_ER_IRQn);
    return DISPLAY_HAL_OK;
}

DisplayHalStatus display_hal_copy(uintptr_t source,
                                  uint32_t x,
                                  uint32_t y,
                                  uint32_t width,
                                  uint32_t height,
                                  size_t input_stride_pixels)
{
    // C++ validates the bounded rectangle/span before this private HAL seam.
    Dma2dHandle.Instance = DMA2D;
    Dma2dHandle.Init.Mode = DMA2D_M2M_PFC;
    Dma2dHandle.Init.ColorMode = DMA2D_OUTPUT_RGB888;
    Dma2dHandle.Init.OutputOffset = 1024U - width;
    Dma2dHandle.Init.AlphaInverted = DMA2D_REGULAR_ALPHA;
    Dma2dHandle.Init.RedBlueSwap = DMA2D_RB_REGULAR;
    Dma2dHandle.Init.BytesSwap = DMA2D_BYTES_REGULAR;
    Dma2dHandle.Init.LineOffsetMode = DMA2D_LOM_PIXELS;
    Dma2dHandle.LayerCfg[1].InputColorMode = DMA2D_INPUT_RGB888;
    Dma2dHandle.LayerCfg[1].InputOffset = input_stride_pixels - width;
    Dma2dHandle.LayerCfg[1].AlphaMode = DMA2D_NO_MODIF_ALPHA;
    Dma2dHandle.LayerCfg[1].InputAlpha = 0xFFU;
    Dma2dHandle.LayerCfg[1].RedBlueSwap = DMA2D_RB_SWAP;
    Dma2dHandle.LayerCfg[1].AlphaInverted = DMA2D_REGULAR_ALPHA;
    HAL_StatusTypeDef status = HAL_DMA2D_Init(&Dma2dHandle);
    if (status == HAL_OK)
    {
        status = HAL_DMA2D_ConfigLayer(&Dma2dHandle, 1U);
    }
    if (status == HAL_OK)
    {
        __DSB();
        status = HAL_DMA2D_Start(
            &Dma2dHandle, source, GFXMMU_VIRTUAL_BUFFER0_BASE + y * 3072U + x * 3U, width, height);
    }
    if (status == HAL_OK)
    {
        status = HAL_DMA2D_PollForTransfer(&Dma2dHandle, 100U);
    }
    if (status != HAL_OK)
    {
        // Force-reset guarantees no further source reads even if HAL abort
        // itself cannot complete. The public driver becomes permanently faulted.
        __HAL_RCC_DMA2D_FORCE_RESET();
        __DSB();
        __HAL_RCC_DMA2D_RELEASE_RESET();
    }
    __DSB();
    return result_from_hal(status);
}

DisplayHalStatus display_hal_refresh(void)
{
    refresh_completed = 0U;
    __DSB();
    return result_from_hal(HAL_DSI_Refresh(&DsiHandle));
}

uint32_t display_hal_completed(void)
{
    return refresh_completed;
}

uint32_t display_hal_failed(void)
{
    return display_failed;
}

uint32_t display_hal_ticks(void)
{
    return HAL_GetTick();
}

void display_hal_stop(void)
{
    HAL_NVIC_DisableIRQ(DSI_IRQn);
    HAL_NVIC_DisableIRQ(LTDC_IRQn);
    HAL_NVIC_DisableIRQ(LTDC_ER_IRQn);
    if (DsiHandle.Instance != NULL)
    {
        CLEAR_BIT(DsiHandle.Instance->WCR, DSI_WCR_LTDCEN);
        __HAL_DSI_WRAPPER_DISABLE(&DsiHandle);
    }
    if (LtdcHandle.Instance != NULL)
    {
        __HAL_LTDC_DISABLE(&LtdcHandle);
    }
    if (Dma2dHandle.Instance != NULL)
    {
        __HAL_RCC_DMA2D_FORCE_RESET();
        __DSB();
        __HAL_RCC_DMA2D_RELEASE_RESET();
    }
    __DSB();
}

void DSI_IRQHandler(void)
{
    HAL_DSI_IRQHandler(&DsiHandle);
}

void LTDC_IRQHandler(void)
{
    HAL_LTDC_IRQHandler(&LtdcHandle);
}

void LTDC_ER_IRQHandler(void)
{
    HAL_LTDC_IRQHandler(&LtdcHandle);
}

void HAL_DSI_EndOfRefreshCallback(DSI_HandleTypeDef* handle)
{
    if (handle == &DsiHandle)
    {
        __HAL_DSI_CLEAR_FLAG(handle, DSI_FLAG_TE);
        refresh_completed = 1U;
    }
}

void HAL_DSI_ErrorCallback(DSI_HandleTypeDef* handle)
{
    if (handle == &DsiHandle)
    {
        display_failed = 1U;
    }
}

void HAL_LTDC_ErrorCallback(LTDC_HandleTypeDef* handle)
{
    if (handle == &LtdcHandle)
    {
        display_failed = 1U;
    }
}
