# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
enable_language(C ASM)
set(sdk "${CMAKE_CURRENT_SOURCE_DIR}/Drivers/GeckoSDK")
if(NOT EXISTS "${sdk}/platform/emlib/src/em_lcd.c")
    message(FATAL_ERROR "Initialize the Drivers/GeckoSDK submodule before configuring firmware")
endif()
find_package(Git REQUIRED)
execute_process(COMMAND "${GIT_EXECUTABLE}" -C "${sdk}" rev-parse HEAD
    OUTPUT_VARIABLE sdk_head OUTPUT_STRIP_TRAILING_WHITESPACE COMMAND_ERROR_IS_FATAL ANY)
if(NOT sdk_head STREQUAL "59d2160fe448b13730a91c0f019a8b4c53c43eb2")
    message(FATAL_ERROR "Gecko SDK pin differs from the validated v4.4.6 dependency")
endif()
set(device "${sdk}/platform/Device/SiliconLabs/EFM32TG")
add_library(gecko_sdk STATIC
    "${device}/Source/system_efm32tg.c"
    "${sdk}/platform/emlib/src/em_cmu.c"
    "${sdk}/platform/emlib/src/em_core.c"
    "${sdk}/platform/emlib/src/em_gpio.c"
    "${sdk}/platform/emlib/src/em_lcd.c"
    "${sdk}/platform/emlib/src/em_acmp.c"
    "${sdk}/platform/emlib/src/em_lesense.c")
target_include_directories(gecko_sdk SYSTEM PUBLIC "${device}/Include"
    "${sdk}/platform/CMSIS/Core/Include" "${sdk}/platform/emlib/inc" "${sdk}/platform/common/inc"
    "${sdk}/hardware/kit/EFM32TG_STK3300/config")
target_compile_definitions(gecko_sdk PUBLIC EFM32TG840F32)
target_compile_options(gecko_sdk PUBLIC -mcpu=cortex-m3 -mthumb -mfloat-abi=soft
    PRIVATE -Os -g3 -ffunction-sections -fdata-sections -fstack-usage)
add_executable(tiny_gecko Platform/EFM32/main.cpp Platform/EFM32/board.c
    Platform/EFM32/startup.S)
add_dependencies(tiny_gecko tiny_generated_contract)
set_target_properties(tiny_gecko PROPERTIES SUFFIX .elf)
target_include_directories(tiny_gecko PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}")
target_link_libraries(tiny_gecko PRIVATE gecko_sdk LitheView::Segments tiny_ui_configuration)
target_compile_options(tiny_gecko PRIVATE -Os -g3 -ffunction-sections -fdata-sections
    -fstack-usage -Wall -Wextra -Werror
    "$<$<COMPILE_LANGUAGE:CXX>:-fno-exceptions;-fno-rtti;-fno-threadsafe-statics;-Wconversion;-Wsign-conversion>")
target_link_options(tiny_gecko PRIVATE -mcpu=cortex-m3 -mthumb -mfloat-abi=soft
    -nostartfiles --specs=nosys.specs -Wl,--gc-sections
    "-T${CMAKE_CURRENT_SOURCE_DIR}/Platform/EFM32/memory.ld"
    "-Wl,-Map=${CMAKE_CURRENT_BINARY_DIR}/tiny_gecko.map" -Wl,--print-memory-usage)
set_property(TARGET tiny_gecko APPEND PROPERTY LINK_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/Platform/EFM32/memory.ld")
add_custom_command(TARGET tiny_gecko POST_BUILD
    COMMAND "${CMAKE_OBJCOPY}" -O binary "$<TARGET_FILE:tiny_gecko>" "${CMAKE_CURRENT_BINARY_DIR}/tiny_gecko.bin"
    COMMAND "${CMAKE_SIZE}" "$<TARGET_FILE:tiny_gecko>" VERBATIM)
