# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

file(READ "${CMAKE_CURRENT_SOURCE_DIR}/Core/Src/main.c" cube_main)
foreach(required IN ITEMS "Application/app_main.h" "appMain();" ".cb_mem = &defaultTaskControl"
        ".stack_mem = defaultTaskStack" "pLayerCfg.PixelFormat = LTDC_PIXEL_FORMAT_RGB565"
        "pLayerCfg.FBStartAdress = 0xD0000000")
    string(FIND "${cube_main}" "${required}" found)
    if(found EQUAL -1)
        message(FATAL_ERROR "Cube integration missing '${required}'. Run tools/prepare-cube.ps1 and review its diff.")
    endif()
endforeach()
if(cube_main MATCHES "pLayerCfg1|HAL_RCCEx_EnableLSECSS|\n  MX_RTC_Init\\(\\);")
    message(FATAL_ERROR "Unused board-selector startup returned. Run tools/prepare-cube.ps1 and review its diff.")
endif()
file(READ "${CMAKE_CURRENT_SOURCE_DIR}/STM32H750xx_FLASH.ld" starter_linker)
foreach(required IN ITEMS "__starter_framebuffer_bytes" "__framebuffer_start" "0xD0000000")
    string(FIND "${starter_linker}" "${required}" found)
    if(found EQUAL -1)
        message(FATAL_ERROR "Cube replaced the application linker contract '${required}'. Restore/review STM32H750xx_FLASH.ld before building the display lesson.")
    endif()
endforeach()
