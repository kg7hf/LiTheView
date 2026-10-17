# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $prefix = [string](& git rev-parse --show-prefix)
    if ($LASTEXITCODE -ne 0) { throw 'Use a Git checkout of this example.' }
    $root = (& git rev-parse --show-toplevel).Trim()
    foreach ($dependency in @('Drivers/CMSIS_6', 'Drivers/CMSIS/Device/ST/STM32L4xx',
        'Drivers/STM32L4xx_HAL_Driver', 'Drivers/BSP/STM32L4R9I-Discovery',
        'Drivers/BSP/Components/Common', 'Drivers/BSP/Components/ft3x67',
        'Drivers/BSP/Components/mfxstm32l152', 'Middlewares/Third_Party/FreeRTOS',
        'Middlewares/Third_Party/FreeRTOS-Kernel')) {
        & git -C $root submodule update --init -- "$($prefix.Trim())$dependency"
        if ($LASTEXITCODE -ne 0) { throw "Could not initialize $dependency" }
    }
} finally { Pop-Location }
