# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(LitheViewStrategyConsumer LANGUAGES CXX)

find_package(LitheView CONFIG REQUIRED)

set(EXPECTED_DESTINATION_FORMAT "" CACHE STRING "Required destination of the installed library")
set_property(CACHE EXPECTED_DESTINATION_FORMAT PROPERTY STRINGS
    RGB565 XRGB8888 R2_PACKED2X2 RRRR2222 RYRY2222)
set(STRATEGY "" CACHE STRING "Rendering strategy for this executable: FULL_FRAME or TILED")
set_property(CACHE STRATEGY PROPERTY STRINGS FULL_FRAME TILED)

if(EXPECTED_DESTINATION_FORMAT STREQUAL "R2_PACKED2X2")
    set(expected_destination_enum r2Packed2x2)
elseif(EXPECTED_DESTINATION_FORMAT STREQUAL "RGB565" OR
       EXPECTED_DESTINATION_FORMAT STREQUAL "XRGB8888" OR
       EXPECTED_DESTINATION_FORMAT STREQUAL "RRRR2222" OR
       EXPECTED_DESTINATION_FORMAT STREQUAL "RYRY2222")
    string(TOLOWER "${EXPECTED_DESTINATION_FORMAT}" expected_destination_enum)
else()
    message(FATAL_ERROR
        "EXPECTED_DESTINATION_FORMAT must be RGB565, XRGB8888, R2_PACKED2X2, RRRR2222, or RYRY2222")
endif()

if(STRATEGY STREQUAL "FULL_FRAME")
    set(strategy_source full_frame.cpp)
elseif(STRATEGY STREQUAL "TILED")
    set(strategy_source tiled.cpp)
else()
    message(FATAL_ERROR "STRATEGY must be FULL_FRAME or TILED")
endif()

configure_file(configuration.hpp.in generated/configuration.hpp @ONLY)
add_executable(litheview_strategy_consumer ${strategy_source})
target_link_libraries(litheview_strategy_consumer PRIVATE LitheView::Core LitheView::CompactFont)
target_compile_features(litheview_strategy_consumer PRIVATE cxx_std_23)
target_include_directories(litheview_strategy_consumer PRIVATE "${CMAKE_CURRENT_BINARY_DIR}/generated")
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(litheview_strategy_consumer PRIVATE
        -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Wdouble-promotion
        -Wold-style-cast -Werror -fno-exceptions -fno-rtti)
endif()

enable_testing()
add_test(NAME litheview_strategy_consumer
    COMMAND litheview_strategy_consumer "${CMAKE_CURRENT_BINARY_DIR}/ctest-frame.bin")
