# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(LitheViewNativeComponentConsumer LANGUAGES CXX)

set(COMPONENT_DIRECTORY "" CACHE PATH "Generate UI component directory for a native packed project")
if(NOT EXISTS "${COMPONENT_DIRECTORY}/component.json")
    message(FATAL_ERROR "Set COMPONENT_DIRECTORY to a generated UI component")
endif()

# The installed package compiles only the selected destination writer; the generated display
# configuration asserts that it matches.
find_package(LitheView CONFIG REQUIRED)
add_subdirectory("${COMPONENT_DIRECTORY}" generated-component)

add_executable(litheview_native_component_consumer main.cpp)
target_link_libraries(litheview_native_component_consumer PRIVATE
    LitheView::GeneratedComponent LitheView::CompactFont)
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(litheview_native_component_consumer PRIVATE
        -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Werror
        -fno-exceptions -fno-rtti)
endif()

enable_testing()
add_test(NAME litheview_native_component_consumer COMMAND litheview_native_component_consumer)
