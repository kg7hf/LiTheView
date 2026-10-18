# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
cmake_minimum_required(VERSION 3.25)
project(SegmentedDesignerConsumer LANGUAGES CXX)
set(GENERATED_DIRECTORY "" CACHE PATH "Directory containing exported tiny.hpp, status.hpp and piece.hpp")
if(NOT EXISTS "${GENERATED_DIRECTORY}/tiny.hpp" OR NOT EXISTS "${GENERATED_DIRECTORY}/status.hpp"
    OR NOT EXISTS "${GENERATED_DIRECTORY}/piece.hpp")
    message(FATAL_ERROR "Export the segmented fixtures and set GENERATED_DIRECTORY")
endif()
include("${CMAKE_CURRENT_LIST_DIR}/../../cmake/Segments.cmake")
add_executable(segmented_designer_consumer main.cpp)
target_link_libraries(segmented_designer_consumer PRIVATE LitheView::Segments)
target_include_directories(segmented_designer_consumer PRIVATE "${GENERATED_DIRECTORY}"
    "${CMAKE_CURRENT_LIST_DIR}/../../examples/efm32tg-stk3300-menu")
if(MSVC)
    target_compile_options(segmented_designer_consumer PRIVATE /W4 /WX /GR-)
else()
    target_compile_options(segmented_designer_consumer PRIVATE -Wall -Wextra -Wpedantic -Wconversion
        -Wsign-conversion -Werror -fno-exceptions -fno-rtti)
    if(MINGW)
        target_link_options(segmented_designer_consumer PRIVATE -static)
    endif()
endif()
enable_testing()
add_test(NAME segmented_generated_consumer COMMAND segmented_designer_consumer)

# Exercise the native worker parser without platform handles or the raster library.
add_executable(segmented_native_preview native_preview.cpp)
target_include_directories(segmented_native_preview PRIVATE "${CMAKE_CURRENT_LIST_DIR}/../..")
target_link_libraries(segmented_native_preview PRIVATE LitheView::Segments)
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(segmented_native_preview PRIVATE -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Werror)
endif()
add_test(NAME segmented_native_preview COMMAND segmented_native_preview)
