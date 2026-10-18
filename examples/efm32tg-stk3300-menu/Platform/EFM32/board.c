// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "board.h"

#include "caplesenseconfig.h"
#include "em_acmp.h"
#include "em_chip.h"
#include "em_cmu.h"
#include "em_gpio.h"
#include "em_lcd.h"
#include "em_lesense.h"
#include "segmentlcdconfig.h"

// Aligned 32-bit counter: one ISR writer, foreground single-word reads on Cortex-M3.
static volatile uint32_t milliseconds;
static uint32_t scan_started;
static bool scan_pending;
static bool scan_failed;
void SysTick_Handler(void)
{
    milliseconds = milliseconds + 1U;
}
uint32_t board_milliseconds(void)
{
    return milliseconds;
}
bool board_pb0(void)
{
    return GPIO_PinInGet(gpioPortD, 8) == 0;
}
bool board_pb1(void)
{
    return GPIO_PinInGet(gpioPortB, 11) == 0;
}
void board_led(bool on)
{
    if (on)
        GPIO_PinOutSet(gpioPortD, 7);
    else
        GPIO_PinOutClear(gpioPortD, 7);
}
void board_sleep(void)
{
    __WFI();
}

bool board_init(void)
{
    CHIP_Init();
    SystemCoreClockUpdate();
    if (SysTick_Config(SystemCoreClock / 1000U) != 0)
        return false;
    CMU_ClockEnable(cmuClock_HFPER, true);
    CMU_ClockEnable(cmuClock_GPIO, true);
    GPIO_PinModeSet(gpioPortD, 8, gpioModeInputPull, 1);
    GPIO_PinModeSet(gpioPortB, 11, gpioModeInputPull, 1);
    GPIO_PinModeSet(gpioPortD, 7, gpioModePushPull, 0);
    CMU_ClockEnable(cmuClock_CORELE, true);
    // Bound oscillator start before EMLIB clock-selection routines are called.
    CMU_OscillatorEnable(cmuOsc_LFRCO, true, false);
    const uint32_t start = milliseconds;
    while ((CMU->STATUS & CMU_STATUS_LFRCORDY) == 0)
        if (milliseconds - start >= 100U)
            return false;
    CMU_ClockSelectSet(cmuClock_LFA, cmuSelect_LFRCO);
    CMU_ClockDivSet(cmuClock_LCDpre, LCD_CMU_CLK_PRE);
    CMU_LCDClkFDIVSet(LCD_CMU_CLK_DIV);
    CMU_ClockEnable(cmuClock_LCD, true);
    static const LCD_Init_TypeDef lcd = LCD_INIT_DEF;
    LCD_IntDisable(_LCD_IEN_MASK);
    LCD_Init(&lcd);
    LCD_SEGMENTS_ENABLE();

    CMU_ClockEnable(cmuClock_ACMP0, true);
    CMU_ClockEnable(cmuClock_ACMP1, true);
    CMU_ClockEnable(cmuClock_LESENSE, true);
    const unsigned pads[4] = {5, 7, 12, 13};
    for (unsigned i = 0; i < 4; ++i)
        GPIO_PinModeSet(gpioPortC, pads[i], gpioModeDisabled, 0);
    ACMP_CapsenseInit_TypeDef acmp = ACMP_CAPSENSE_INIT_DEFAULT;
    acmp.enable = false; // LESENSE owns comparator enable and channel selection.
    acmp.hysteresisLevel = acmpHysteresisLevel7;
    acmp.resistor = acmpResistor0;
    ACMP_GPIOSetup(ACMP0, 0, false, false);
    ACMP_GPIOSetup(ACMP1, 0, false, false);
    ACMP_CapsenseInit(ACMP0, &acmp);
    ACMP_CapsenseInit(ACMP1, &acmp);
    LESENSE_Init_TypeDef lesense = LESENSE_INIT_DEFAULT;
    lesense.coreCtrl.scanStart = lesenseScanStartOneShot;
    lesense.coreCtrl.storeScanRes = false;
    lesense.coreCtrl.bufOverWr = false;
    lesense.coreCtrl.biasMode = lesenseBiasModeDutyCycle;
    lesense.perCtrl.warmupMode = lesenseWarmupModeNormal;
    LESENSE_Init(&lesense, false); // Reset_Handler entered with reset peripheral state.
    LESENSE_ClkDivSet(lesenseClkLF, lesenseClkDiv_8);
    static const LESENSE_ChAll_TypeDef channels = LESENSE_CAPSENSE_SCAN_CONF_SENSE;
    LESENSE_ChannelAllConfig(&channels);
    LESENSE_IntDisable(_LESENSE_IEN_MASK);
    return true;
}

bool board_display(const uint32_t rows[8])
{
    // Coalesce changes while the previous register update crosses the LF clock.
    if (LCD->SYNCBUSY != 0)
        return false;
    LCD_FreezeEnable(true);
    for (int common = 0; common < 8; ++common)
        LCD_SegmentSetLow(common, 0xFFFFFU, rows[common]);
    LCD_FreezeEnable(false);
    return true;
}

int board_slider(uint32_t counts[4])
{
    if (scan_failed)
        return 0;
    const uint32_t now = milliseconds;
    if (scan_pending)
    {
        if ((LESENSE->IF & LESENSE_IF_SCANCOMPLETE) != 0)
        {
            // Only four enabled channels store data, in ascending channel order.
            for (unsigned i = 0; i < 4; ++i)
            {
                if ((LESENSE->STATUS & LESENSE_STATUS_BUFDATAV) == 0)
                {
                    scan_failed = true;
                    return -1;
                }
                counts[i] = LESENSE_ScanResultDataGet();
            }
            LESENSE_IntClear(LESENSE_IF_SCANCOMPLETE);
            scan_pending = false;
            return 1;
        }
        if (now - scan_started >= 100U)
        {
            scan_failed = true; // Fail closed; buttons and display remain responsive.
            return -1;
        }
    }
    else if (now - scan_started >= 20U && LESENSE->SYNCBUSY == 0)
    {
        // Direct nonblocking command instead of EMLIB's synchronization wait loop.
        LESENSE->CMD = LESENSE_CMD_START;
        scan_started = now;
        scan_pending = true;
    }
    return 0;
}
