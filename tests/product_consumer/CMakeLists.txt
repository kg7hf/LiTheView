# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(LitheViewProductConsumer LANGUAGES CXX)

find_package(LitheView CONFIG REQUIRED)

set(EXPECTED_DESTINATION_FORMAT "" CACHE STRING "Required single destination in the installed library")
set(_destinations RGB565 RGB888 XRGB8888 ARGB8888 R2_PACKED2X2 RRRR2222 RYRY2222)
if(NOT EXPECTED_DESTINATION_FORMAT IN_LIST _destinations)
    message(FATAL_ERROR "Set EXPECTED_DESTINATION_FORMAT to a single supported destination")
endif()
string(TOLOWER "${EXPECTED_DESTINATION_FORMAT}" expected_destination_enum)
if(EXPECTED_DESTINATION_FORMAT STREQUAL "R2_PACKED2X2")
    set(expected_destination_enum r2Packed2x2)
endif()
configure_file(expected_destination.hpp.in generated/expected_destination.hpp @ONLY)

add_executable(litheview_product_consumer main.cpp)
target_link_libraries(litheview_product_consumer PRIVATE LitheView::Core LitheView::CompactFont)
target_include_directories(litheview_product_consumer PRIVATE "${CMAKE_CURRENT_BINARY_DIR}/generated")
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(litheview_product_consumer PRIVATE
        -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Werror -fno-exceptions -fno-rtti)
endif()

enable_testing()
add_test(NAME litheview_product_consumer COMMAND litheview_product_consumer)
