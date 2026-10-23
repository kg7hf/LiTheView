# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$application = Split-Path $PSScriptRoot -Parent
$repository = (& git -C $application rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Open this example inside the LiTheView source checkout.' }
$sibling = 'examples/stm32h750b-dk-starter-1'
$dependencies = @(
    @('Drivers/CMSIS_6', 'CMSIS/Core/Include/cmsis_gcc.h'),
    @('Drivers/CMSIS/Device/ST/STM32H7xx', 'Include/stm32h750xx.h'),
    @('Drivers/STM32H7xx_HAL_Driver', 'Src/stm32h7xx_hal.c'),
    @('Middlewares/Third_Party/FreeRTOS', 'Source/tasks.c'),
    @('Middlewares/Third_Party/FreeRTOS-Kernel', 'portable/MSVC-MingW/port.c')
)
foreach ($entry in $dependencies) {
    $submodule = "$sibling/$($entry[0])"
    $required = Join-Path (Join-Path $repository $submodule) $entry[1]
    if (Test-Path -LiteralPath $required -PathType Leaf) { continue }
    & git -C $repository submodule update --init -- $submodule
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Could not initialize $submodule. Local sibling changes were not reset."
    }
}
Write-Output 'H750 board dependencies are available.'
