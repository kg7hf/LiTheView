# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(LitheViewDisplayBringup LANGUAGES CXX)

find_package(LitheView CONFIG REQUIRED)

add_executable(litheview_display_bringup main.cpp)
set(bringup_targets litheview_display_bringup)
if(WIN32)
    add_executable(litheview_display_bringup_simulator WIN32 win32_main.cpp)
    target_link_libraries(litheview_display_bringup_simulator PRIVATE user32 gdi32)
    target_compile_definitions(litheview_display_bringup_simulator PRIVATE
        UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
    list(APPEND bringup_targets litheview_display_bringup_simulator)
endif()
foreach(bringup_target IN LISTS bringup_targets)
    target_link_libraries(${bringup_target} PRIVATE LitheView::Core LitheView::CompactFont)
    target_compile_features(${bringup_target} PRIVATE cxx_std_23)
    set_target_properties(${bringup_target} PROPERTIES CXX_EXTENSIONS OFF)
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        target_compile_options(${bringup_target} PRIVATE
            -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Wdouble-promotion
            -Wold-style-cast -Werror -fno-exceptions -fno-rtti)
    elseif(MSVC)
        target_compile_options(${bringup_target} PRIVATE /W4 /WX /GR- /EHs-c-)
    endif()
endforeach()

enable_testing()
add_test(NAME litheview_display_bringup COMMAND litheview_display_bringup)
set_tests_properties(litheview_display_bringup PROPERTIES
    WORKING_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}")
