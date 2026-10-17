# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

set(CMAKE_SYSTEM_NAME Generic)
set(CMAKE_SYSTEM_PROCESSOR arm)
set(CMAKE_TRY_COMPILE_TARGET_TYPE STATIC_LIBRARY)

set(LITHEVIEW_ARM_TOOLCHAIN_ROOT "" CACHE PATH
    "Path to an Arm GNU Toolchain installation")
set(LITHEVIEW_ARM_CPU "cortex-m33" CACHE STRING
    "Arm CPU passed to the compiler")
set(LITHEVIEW_ARM_FLOAT_ABI "soft" CACHE STRING
    "Arm floating-point ABI passed to the compiler")
set(LITHEVIEW_ARM_FPU "" CACHE STRING
    "Optional Arm floating-point unit passed to the compiler")
list(APPEND CMAKE_TRY_COMPILE_PLATFORM_VARIABLES
    LITHEVIEW_ARM_TOOLCHAIN_ROOT
    LITHEVIEW_ARM_CPU
    LITHEVIEW_ARM_FLOAT_ABI
    LITHEVIEW_ARM_FPU)

if(LITHEVIEW_ARM_TOOLCHAIN_ROOT STREQUAL "" AND
   DEFINED ENV{STM32_TOOLCHAIN_ROOT} AND
   NOT "$ENV{STM32_TOOLCHAIN_ROOT}" STREQUAL "")
    set(LITHEVIEW_ARM_TOOLCHAIN_ROOT "$ENV{STM32_TOOLCHAIN_ROOT}")
endif()

if(LITHEVIEW_ARM_TOOLCHAIN_ROOT STREQUAL "")
    find_program(LITHEVIEW_ARM_GXX
        NAMES arm-none-eabi-g++ arm-none-eabi-g++.exe)
    if(LITHEVIEW_ARM_GXX)
        get_filename_component(LITHEVIEW_ARM_TOOLCHAIN_BIN
            "${LITHEVIEW_ARM_GXX}" DIRECTORY)
        get_filename_component(LITHEVIEW_ARM_TOOLCHAIN_ROOT
            "${LITHEVIEW_ARM_TOOLCHAIN_BIN}" DIRECTORY)
    endif()
endif()

if(LITHEVIEW_ARM_TOOLCHAIN_ROOT STREQUAL "")
    message(FATAL_ERROR
        "Arm GNU Toolchain not found. Set LITHEVIEW_ARM_TOOLCHAIN_ROOT, "
        "set the STM32_TOOLCHAIN_ROOT environment variable, or put "
        "arm-none-eabi-g++ on PATH.")
endif()

file(TO_CMAKE_PATH "${LITHEVIEW_ARM_TOOLCHAIN_ROOT}"
    LITHEVIEW_ARM_TOOLCHAIN_ROOT)
set(LITHEVIEW_ARM_TOOLCHAIN_BIN "${LITHEVIEW_ARM_TOOLCHAIN_ROOT}/bin")
if(WIN32)
    set(LITHEVIEW_TOOL_SUFFIX ".exe")
else()
    set(LITHEVIEW_TOOL_SUFFIX "")
endif()

set(CMAKE_CXX_COMPILER
    "${LITHEVIEW_ARM_TOOLCHAIN_BIN}/arm-none-eabi-g++${LITHEVIEW_TOOL_SUFFIX}")
set(CMAKE_C_COMPILER
    "${LITHEVIEW_ARM_TOOLCHAIN_BIN}/arm-none-eabi-gcc${LITHEVIEW_TOOL_SUFFIX}")
set(CMAKE_ASM_COMPILER
    "${LITHEVIEW_ARM_TOOLCHAIN_BIN}/arm-none-eabi-gcc${LITHEVIEW_TOOL_SUFFIX}")
set(CMAKE_AR
    "${LITHEVIEW_ARM_TOOLCHAIN_BIN}/arm-none-eabi-ar${LITHEVIEW_TOOL_SUFFIX}")
set(CMAKE_RANLIB
    "${LITHEVIEW_ARM_TOOLCHAIN_BIN}/arm-none-eabi-ranlib${LITHEVIEW_TOOL_SUFFIX}")
set(CMAKE_SIZE
    "${LITHEVIEW_ARM_TOOLCHAIN_BIN}/arm-none-eabi-size${LITHEVIEW_TOOL_SUFFIX}"
    CACHE FILEPATH "")
set(CMAKE_OBJCOPY
    "${LITHEVIEW_ARM_TOOLCHAIN_BIN}/arm-none-eabi-objcopy${LITHEVIEW_TOOL_SUFFIX}"
    CACHE FILEPATH "")

set(litheview_arm_arch_flags
    "-mcpu=${LITHEVIEW_ARM_CPU} -mthumb -mfloat-abi=${LITHEVIEW_ARM_FLOAT_ABI}")
if(NOT LITHEVIEW_ARM_FPU STREQUAL "")
    string(APPEND litheview_arm_arch_flags " -mfpu=${LITHEVIEW_ARM_FPU}")
endif()
set(CMAKE_ASM_FLAGS_INIT "${litheview_arm_arch_flags}")
set(CMAKE_C_FLAGS_INIT
    "${litheview_arm_arch_flags} -ffreestanding -ffunction-sections -fdata-sections")
set(CMAKE_CXX_FLAGS_INIT
    "${litheview_arm_arch_flags} -ffunction-sections -fdata-sections")

foreach(litheview_required_tool IN ITEMS
        CMAKE_C_COMPILER
        CMAKE_CXX_COMPILER
        CMAKE_ASM_COMPILER
        CMAKE_AR
        CMAKE_RANLIB
        CMAKE_SIZE
        CMAKE_OBJCOPY)
    if(NOT EXISTS "${${litheview_required_tool}}")
        message(FATAL_ERROR
            "${litheview_required_tool} was not found at ${${litheview_required_tool}}")
    endif()
endforeach()
