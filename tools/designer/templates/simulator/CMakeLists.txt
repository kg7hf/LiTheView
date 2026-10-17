# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)
    project(LitheViewGeneratedSimulator LANGUAGES CXX)
    set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
    set(LITHEVIEW_GENERATED_HOST_DEFAULT ON)
else()
    set(LITHEVIEW_GENERATED_HOST_DEFAULT OFF)
endif()
option(LITHEVIEW_BUILD_GENERATED_SIMULATOR
    "Build the standalone native simulator and headless check"
    ${LITHEVIEW_GENERATED_HOST_DEFAULT})

# An embedding application may already provide Core through its own package,
# source checkout, or toolchain. This component never creates another Core.
if(NOT TARGET LitheView::Core LitheView::CompactFont)
    find_package(LitheView CONFIG REQUIRED)
endif()
include("${CMAKE_CURRENT_SOURCE_DIR}/resources.cmake")

add_library(litheview_generated_component INTERFACE)
add_library(LitheView::GeneratedComponent ALIAS litheview_generated_component)
target_link_libraries(litheview_generated_component INTERFACE LitheView::Core LitheView::CompactFont)
target_compile_features(litheview_generated_component INTERFACE cxx_std_23)
target_include_directories(litheview_generated_component INTERFACE "${CMAKE_CURRENT_SOURCE_DIR}")
if(LITHEVIEW_SELECTED_RESOURCE_SOURCES)
    # The parent compiles only the selected immutable resources with its own
    # toolchain and application flags. Nothing owns a process or device here.
    target_sources(litheview_generated_component INTERFACE ${LITHEVIEW_SELECTED_RESOURCE_SOURCES})
endif()

if(NOT LITHEVIEW_BUILD_GENERATED_SIMULATOR)
    return()
endif()

add_executable(litheview_generated_smoke headless.cpp)
set(LITHEVIEW_GENERATED_HOST_TARGETS litheview_generated_smoke)
if(WIN32)
    add_executable(litheview_generated_simulator WIN32 win32.cpp)
    target_link_libraries(litheview_generated_simulator PRIVATE user32 gdi32)
    target_compile_definitions(litheview_generated_simulator PRIVATE
        UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
    list(APPEND LITHEVIEW_GENERATED_HOST_TARGETS litheview_generated_simulator)
endif()
foreach(LITHEVIEW_GENERATED_HOST_TARGET IN LISTS LITHEVIEW_GENERATED_HOST_TARGETS)
    target_link_libraries(${LITHEVIEW_GENERATED_HOST_TARGET} PRIVATE LitheView::GeneratedComponent)
    set_target_properties(${LITHEVIEW_GENERATED_HOST_TARGET} PROPERTIES CXX_EXTENSIONS OFF)
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        target_compile_options(${LITHEVIEW_GENERATED_HOST_TARGET} PRIVATE
            -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Wdouble-promotion
            -Wold-style-cast -Werror -fno-exceptions -fno-rtti)
    elseif(MSVC)
        target_compile_options(${LITHEVIEW_GENERATED_HOST_TARGET} PRIVATE /W4 /WX /GR- /EHs-c-)
    endif()
endforeach()
enable_testing()
add_test(NAME litheview_generated_smoke COMMAND litheview_generated_smoke)
set_tests_properties(litheview_generated_smoke PROPERTIES WORKING_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}")
