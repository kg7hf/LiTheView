# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param([switch]$CheckOnly, [string]$MainFile = '')
$ErrorActionPreference = 'Stop'
if (!$MainFile) { $MainFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'Core/Src/main.c' }
$original = [IO.File]::ReadAllText($MainFile)
$source = $original.Replace("`r`n", "`n")

# The board-selector .ioc includes unused peripheral stubs. Apply only this known
# starter integration; never rewrite pin, clock, GPIO or used-peripheral settings.
# Review the diff after regeneration. Unknown output fails the checks below.
$unused = 'ADC1|ADC2|ETH|FDCAN1|FDCAN2|QUADSPI|RTC|SAI2|SDMMC1_MMC|SPI2|USART3_UART|USB_OTG_FS_PCD'
$source = [regex]::Replace($source, "(?m)^  MX_($unused)_Init\(\);\n", '')
$source = $source.Replace('RCC_OSCILLATORTYPE_LSE|', '').Replace('|RCC_OSCILLATORTYPE_LSE', '')
$source = [regex]::Replace($source, '(?m)^  HAL_RCCEx_EnableLSECSS\(\);\n', '')
$source = [regex]::Replace($source, '(?m)^  LTDC_LayerCfgTypeDef pLayerCfg1 = \{0\};\n', '')
$source = [regex]::Replace($source,
    '(?s)  pLayerCfg1\.WindowX0 = 0;.*?HAL_LTDC_ConfigLayer\(&hltdc, &pLayerCfg1, 1\) != HAL_OK\)\s*\{\s*Error_Handler\(\);\s*\}\n', '')
if (!$source.Contains('#include "Application/app_main.h"')) {
    $source = $source.Replace('/* USER CODE BEGIN Includes */', "/* USER CODE BEGIN Includes */`n#include `"Application/app_main.h`"")
}
if (!$source.Contains('#include "FreeRTOS.h"')) {
    $source = $source.Replace('#include "Application/app_main.h"', "#include `"FreeRTOS.h`"`n#include `"Application/app_main.h`"")
}
$source = $source.Replace('  osKernelInitialize();', '  if (osKernelInitialize() != osOK) { Error_Handler(); }')
$attributes = @'
osThreadId_t defaultTaskHandle;
static StaticTask_t defaultTaskControl;
static StackType_t defaultTaskStack[2048];
const osThreadAttr_t defaultTask_attributes = {
  .name = "defaultTask",
  .cb_mem = &defaultTaskControl,
  .cb_size = sizeof(defaultTaskControl),
  .stack_mem = defaultTaskStack,
  .stack_size = sizeof(defaultTaskStack),
  .priority = osPriorityBelowNormal,
};
'@
$source = [regex]::Replace($source, '(?s)osThreadId_t defaultTaskHandle;.*?const osThreadAttr_t defaultTask_attributes = \{.*?\};', $attributes)
if (!$source.Contains('  appMain();')) {
    $source = [regex]::Replace($source, '(?s)(/\* USER CODE BEGIN 5 \*/).*?(/\* USER CODE END 5 \*/)', "`$1`n  appMain();`n  `$2")
}
if (!$source.Contains('if (defaultTaskHandle == NULL)')) {
    $source = $source.Replace('/* USER CODE BEGIN RTOS_THREADS */', "/* USER CODE BEGIN RTOS_THREADS */`n  if (defaultTaskHandle == NULL) { Error_Handler(); }")
}
foreach ($required in @('#include "Application/app_main.h"', '  appMain();',
    '.cb_mem = &defaultTaskControl', '.stack_mem = defaultTaskStack',
    'pLayerCfg.PixelFormat = LTDC_PIXEL_FORMAT_RGB565;', 'pLayerCfg.FBStartAdress = 0xD0000000;',
    'pLayerCfg.ImageWidth = 480;', 'pLayerCfg.ImageHeight = 272;')) {
    if (!$source.Contains($required)) { throw "Unexpected Cube output: missing $required. Review integration manually." }
}
if ($source -match 'pLayerCfg1|HAL_RCCEx_EnableLSECSS|OscillatorType[^;]*RCC_OSCILLATORTYPE_LSE') {
    throw 'Unexpected Cube output: unused LTDC layer or LSE startup remains. Review manually.'
}
$calls = @([regex]::Matches($source, '(?m)^  (MX_\w+_Init)\(\);') | ForEach-Object { $_.Groups[1].Value })
if (($calls -join ',') -ne 'MX_GPIO_Init,MX_ADC3_Init,MX_FMC_Init,MX_LTDC_Init,MX_I2C4_Init') {
    throw "Unexpected startup sequence: $($calls -join ','). Review before running hardware."
}
if ($source -ne $original.Replace("`r`n", "`n")) {
    if ($CheckOnly) { throw 'Cube integration is stale. Run tools/prepare-cube.ps1, inspect its diff, then rebuild.' }
    [IO.File]::WriteAllText($MainFile, $source.Replace("`n", "`r`n"), [Text.UTF8Encoding]::new($false))
    Write-Output "Updated starter integration in $MainFile"
} else { Write-Output 'Cube integration checks passed.' }
