# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(H750Sdr LANGUAGES C CXX ASM)
set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

set(STARTER_PLATFORM "Win32" CACHE STRING "Win32 or STM32")
set_property(CACHE STARTER_PLATFORM PROPERTY STRINGS Win32 STM32)
if(NOT STARTER_PLATFORM MATCHES "^(Win32|STM32)$")
    message(FATAL_ERROR "Select STARTER_PLATFORM=Win32 or STM32")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 4)
    message(FATAL_ERROR "CMSIS-RTOS2 requires a 32-bit build. Use the win32-debug i686 preset.")
endif()

set(default_platform_root "${CMAKE_CURRENT_SOURCE_DIR}")
if(NOT EXISTS "${default_platform_root}/Middlewares/Third_Party/FreeRTOS/Source/tasks.c")
    # In the source monorepo, reuse the already pinned board package while this
    # example's own dependency pins await the next library publication.
    set(default_platform_root "${CMAKE_CURRENT_SOURCE_DIR}/../stm32h750b-dk-starter-1")
endif()
set(H750_PLATFORM_ROOT "${default_platform_root}" CACHE PATH "Board SDK and FreeRTOS submodules")
set(RTOS "${H750_PLATFORM_ROOT}/Middlewares/Third_Party/FreeRTOS/Source")
set(CMSIS "${H750_PLATFORM_ROOT}/Drivers/CMSIS_6/CMSIS")
set(HAL "${H750_PLATFORM_ROOT}/Drivers/STM32H7xx_HAL_Driver")
set(DEVICE "${H750_PLATFORM_ROOT}/Drivers/CMSIS/Device/ST/STM32H7xx")
foreach(required IN ITEMS "${RTOS}/tasks.c" "${CMSIS}/Core/Include/cmsis_gcc.h"
                          "${HAL}/Src/stm32h7xx_hal.c" "${DEVICE}/Include/stm32h750xx.h")
    if(NOT EXISTS "${required}")
        message(FATAL_ERROR "Missing dependency ${required}. Run tools/init-platform.ps1.")
    endif()
endforeach()
if(STARTER_PLATFORM STREQUAL "Win32")
    set(PORT "${H750_PLATFORM_ROOT}/Middlewares/Third_Party/FreeRTOS-Kernel/portable/MSVC-MingW")
else()
    set(PORT "${RTOS}/portable/GCC/ARM_CM4F")
endif()

add_library(starter_config INTERFACE)
target_include_directories(starter_config INTERFACE
    "${CMAKE_CURRENT_SOURCE_DIR}/Platform/${STARTER_PLATFORM}" "${CMAKE_CURRENT_SOURCE_DIR}")
target_include_directories(starter_config SYSTEM INTERFACE
    "${RTOS}/include" "${RTOS}/CMSIS_RTOS_V2" "${CMSIS}/RTOS2/Include" "${PORT}")
if(STARTER_PLATFORM STREQUAL "STM32")
    target_include_directories(starter_config SYSTEM INTERFACE
        "${CMAKE_CURRENT_SOURCE_DIR}/Core/Inc" "${HAL}/Inc" "${HAL}/Inc/Legacy"
        "${DEVICE}/Include" "${CMSIS}/Core/Include")
    target_compile_definitions(starter_config INTERFACE USE_HAL_DRIVER USE_PWR_LDO_SUPPLY STM32H750xx)
endif()

add_library(starter_rtos STATIC
    "${RTOS}/tasks.c" "${RTOS}/queue.c" "${RTOS}/list.c" "${RTOS}/timers.c"
    "${RTOS}/event_groups.c" "${RTOS}/stream_buffer.c"
    "${RTOS}/CMSIS_RTOS_V2/cmsis_os2.c" "${PORT}/port.c")
target_link_libraries(starter_rtos PUBLIC starter_config)

add_library(starter_application OBJECT Application/app_main.cpp Tasks/kernel_memory.cpp)
target_link_libraries(starter_application PUBLIC starter_config)
target_compile_options(starter_application PRIVATE -Wall -Wextra -Wconversion -Wshadow
    -Wold-style-cast -Wdouble-promotion -Werror -fno-exceptions -fno-rtti -fno-threadsafe-statics)

set(default_litheview_root "${CMAKE_CURRENT_SOURCE_DIR}/Middlewares/Third_Party/LiTheView")
if(NOT EXISTS "${default_litheview_root}/include/lithe/telemetry_views.hpp")
    set(default_litheview_root "${CMAKE_CURRENT_SOURCE_DIR}/../..")
endif()
set(LITHEVIEW_ROOT "${default_litheview_root}"
    CACHE PATH "LiTheView package in this application")
if(NOT EXISTS "${LITHEVIEW_ROOT}/include/lithe/telemetry_views.hpp")
    message(FATAL_ERROR "LiTheView telemetry views were not found. Set LITHEVIEW_ROOT to the current library source.")
endif()
foreach(option IN ITEMS BUILD_RENDERING_FIXTURES BUILD_RENDERING_TESTS BUILD_WIN32 BUILD_TESTS
                        BUILD_DESIGNER_PREVIEW BUILD_STM32U545_PROBE ENABLE_EMBEDDED_BUDGET_GATE)
    set(LITHEVIEW_${option} OFF CACHE BOOL "SDR example builds only the library" FORCE)
endforeach()
set(LITHEVIEW_BUILD_COMPACT_FONT OFF CACHE BOOL "SDR screen supplies its own font" FORCE)
set(LITHEVIEW_BUILD_GENERATED_FONTS ON CACHE BOOL "SDR screen uses DejaVu Sans" FORCE)
set(LITHEVIEW_DESTINATION_FORMAT RGB565 CACHE STRING "H750 uses native RGB565" FORCE)
add_subdirectory("${LITHEVIEW_ROOT}" "${CMAKE_CURRENT_BINARY_DIR}/litheview" EXCLUDE_FROM_ALL)
target_link_libraries(starter_application PUBLIC LitheView::Core LitheView::GeneratedFonts)
target_sources(starter_application PRIVATE Tasks/UI/runtime.cpp
    "Platform/${STARTER_PLATFORM}/display.cpp" "Platform/${STARTER_PLATFORM}/input.cpp")

if(STARTER_PLATFORM STREQUAL "Win32")
    add_executable(starter WIN32 Platform/Win32/main.cpp)
    include(cmake/front_panel.cmake)
    target_link_libraries(starter PRIVATE starter_application starter_rtos user32 gdi32 winmm)
    target_compile_options(starter PRIVATE -Wall -Wextra -Wconversion -Wshadow
        -Wold-style-cast -Wdouble-promotion -Werror -fno-exceptions -fno-rtti)
    target_link_options(starter PRIVATE -static -Wl,--gc-sections)
    enable_testing()
    add_executable(sdr_controls_test Tests/sdr_controls.cpp)
    target_link_libraries(sdr_controls_test PRIVATE starter_config LitheView::Core
        LitheView::GeneratedFonts)
    target_compile_options(sdr_controls_test PRIVATE -Wall -Wextra -Wconversion -Wshadow
        -Wold-style-cast -Wdouble-promotion -Werror -fno-exceptions -fno-rtti)
    target_link_options(sdr_controls_test PRIVATE -static)
    add_test(NAME sdr_controls COMMAND sdr_controls_test)
    add_test(NAME sdr_application_task COMMAND starter --self-test)
    set_tests_properties(sdr_application_task PROPERTIES TIMEOUT 20 PASS_REGULAR_EXPRESSION "PASS:")
else()
    include(cmake/check_cube.cmake)
    include(cmake/hal_sources.cmake)
    add_library(starter_hal STATIC ${STARTER_HAL_SOURCES})
    target_link_libraries(starter_hal PUBLIC starter_config)
    add_executable(starter
        Core/Src/main.c Core/Src/stm32h7xx_it.c Core/Src/stm32h7xx_hal_msp.c
        Core/Src/stm32h7xx_hal_timebase_tim.c Core/Src/system_stm32h7xx.c
        Core/Src/syscalls.c Core/Src/sysmem.c startup_stm32h750xx.s
        Platform/STM32/indicator.cpp)
    set_target_properties(starter PROPERTIES SUFFIX .elf)
    target_link_libraries(starter PRIVATE starter_application starter_rtos starter_hal)
    target_compile_options(starter PRIVATE $<$<COMPILE_LANGUAGE:CXX>:-Wall;-Wextra;-Wconversion;-Wshadow;-Wold-style-cast;-Wdouble-promotion;-Werror>)
    target_link_options(starter PRIVATE
        "-Wl,--defsym=__starter_framebuffer_bytes=522240"
        -T${CMAKE_CURRENT_SOURCE_DIR}/STM32H750xx_FLASH.ld
        --specs=nano.specs --specs=nosys.specs -Wl,--gc-sections
        -Wl,-Map=${CMAKE_CURRENT_BINARY_DIR}/starter.map -Wl,--print-memory-usage)
    set_property(TARGET starter APPEND PROPERTY LINK_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/STM32H750xx_FLASH.ld)
    add_custom_command(TARGET starter POST_BUILD
        COMMAND ${CMAKE_OBJCOPY} -O binary $<TARGET_FILE:starter> ${CMAKE_CURRENT_BINARY_DIR}/starter.bin
        COMMAND ${CMAKE_SIZE} $<TARGET_FILE:starter>)
endif()
