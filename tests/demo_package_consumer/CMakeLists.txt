# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(LitheViewDemoPackageConsumer LANGUAGES CXX)

find_package(LitheView CONFIG REQUIRED)
include(../../tests/rendering/common/showcase.cmake)
set(EXPECTED_DESTINATION_FORMAT "" CACHE STRING "Required fixed showcase configuration")
if(EXPECTED_DESTINATION_FORMAT STREQUAL "RGB565")
    set(expected_format rgb565)
    set(expected_font compact5x7)
    set(expected_animation alpha1)
    set(expected_crc 0xf88f6224U)
elseif(EXPECTED_DESTINATION_FORMAT STREQUAL "XRGB8888")
    set(expected_format xrgb8888)
    set(expected_font generatedSans)
    set(expected_animation indexed4)
    set(expected_crc 0x7955ec5cU)
else()
    message(FATAL_ERROR "EXPECTED_DESTINATION_FORMAT must be RGB565 or XRGB8888")
endif()
litheview_add_showcase(showcase "${EXPECTED_DESTINATION_FORMAT}")
configure_file(configuration.hpp.in generated/configuration.hpp @ONLY)
add_executable(litheview_demo_package_consumer main.cpp)
target_link_libraries(litheview_demo_package_consumer PRIVATE showcase)
target_include_directories(litheview_demo_package_consumer PRIVATE "${CMAKE_CURRENT_BINARY_DIR}/generated")
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(litheview_demo_package_consumer PRIVATE
        -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Werror)
endif()
enable_testing()
add_test(NAME litheview_demo_package_consumer
    COMMAND litheview_demo_package_consumer "${CMAKE_CURRENT_BINARY_DIR}/initial-frame.bin")
