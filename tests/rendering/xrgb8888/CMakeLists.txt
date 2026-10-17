# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(LitheViewXrgb8888Example LANGUAGES CXX)

find_package(LitheView CONFIG REQUIRED)
include(../common/showcase.cmake)
litheview_add_showcase(showcase XRGB8888)
if(NOT WIN32)
    message(FATAL_ERROR "This entry uses the Win32 host adapter; the configuration and Demo remain portable")
endif()

set(litheview_rgb_configuration_header "${CMAKE_CURRENT_SOURCE_DIR}/configuration.hpp")
set(litheview_rgb_configuration_type "lithe::examples::xrgb8888::Configuration")
set(litheview_host_scene "lithe::demo::Demo")
set(litheview_host_scene_header "demo.hpp")
set(litheview_host_title "LitheView graphics demo - XRGB8888")
set(litheview_host_class "LitheView.Demo.xrgb8888")
configure_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../cmake/win32_host_config.hpp.in"
    "${CMAKE_CURRENT_BINARY_DIR}/generated/litheview/win32_host_config.hpp"
    @ONLY)

add_executable(litheview_xrgb8888 WIN32 win32_main.cpp ../win32/main.cpp)
target_link_libraries(litheview_xrgb8888 PRIVATE showcase user32 gdi32)
target_include_directories(litheview_xrgb8888 PRIVATE "${CMAKE_CURRENT_BINARY_DIR}/generated")
# Windows SDK header contract only; the product format is a C++ constant.
target_compile_definitions(litheview_xrgb8888 PRIVATE WIN32_LEAN_AND_MEAN)
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(litheview_xrgb8888 PRIVATE
        -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Werror
        -fno-exceptions -fno-rtti)
endif()
