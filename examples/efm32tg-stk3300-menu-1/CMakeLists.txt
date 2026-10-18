# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
cmake_minimum_required(VERSION 3.25)
project(TinyGeckoMenu LANGUAGES CXX)
enable_testing()
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
if(WIN32 AND MINGW)
    include(cmake/debugger.cmake)
endif()

set(LITHEVIEW_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/Middlewares/Third_Party/LiTheView" CACHE PATH
    "LiTheView checkout; any application-owned dependency location is supported")
if(NOT EXISTS "${LITHEVIEW_ROOT}/cmake/Segments.cmake")
    # Contributor checkout convenience. Copied applications must set LITHEVIEW_ROOT.
    if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/../../cmake/Segments.cmake")
        set(LITHEVIEW_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../..")
    else()
        message(FATAL_ERROR "Set LITHEVIEW_ROOT to your LiTheView submodule or checkout")
    endif()
endif()
include("${LITHEVIEW_ROOT}/cmake/Segments.cmake")

# The same application starts blank and acquires its presentation after Generate UI.
# CONFIGURE_DEPENDS makes the first generated header trigger reconfiguration on build.
file(GLOB tiny_ui_exports CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/UI/Generated/segmented/home.hpp")
set(tiny_has_ui 0)
if(tiny_ui_exports)
    set(tiny_has_ui 1)
    add_custom_target(tiny_generated_contract
        COMMAND "${CMAKE_COMMAND}" "-DTINY_SOURCE=${CMAKE_CURRENT_SOURCE_DIR}"
            -P "${CMAKE_CURRENT_SOURCE_DIR}/cmake/check_generated.cmake" VERBATIM)
else()
    add_custom_target(tiny_generated_contract)
endif()
add_library(tiny_ui_configuration INTERFACE)
target_compile_definitions(tiny_ui_configuration INTERFACE TINY_HAS_UI=${tiny_has_ui})

function(tiny_target target)
    if(NOT target STREQUAL "fsm_checks")
        add_dependencies(${target} tiny_generated_contract)
    endif()
    target_link_libraries(${target} PRIVATE tiny_ui_configuration)
    target_include_directories(${target} PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}")
    if(MSVC)
        target_compile_options(${target} PRIVATE /W4 /WX /GR-)
    else()
        target_compile_options(${target} PRIVATE -Wall -Wextra -Wpedantic -Wconversion
            -Wsign-conversion -Werror -fno-exceptions -fno-rtti -fstack-usage)
        if(MINGW)
            # Local build, no copied compiler DLLs and no global PATH edits.
            target_link_options(${target} PRIVATE -static)
        endif()
    endif()
endfunction()

if(TINY_GECKO_EMBEDDED)
    include(cmake/firmware.cmake)
    return()
endif()
add_executable(fsm_checks Tests/fsm_checks.cpp)
tiny_target(fsm_checks)
# Intentionally no LiTheView link or include dependency for the FSM tests.
add_test(NAME fsm COMMAND fsm_checks)
if(tiny_has_ui)
    add_executable(segment_checks Tests/segment_checks.cpp)
    tiny_target(segment_checks)
    target_link_libraries(segment_checks PRIVATE LitheView::Segments)
    add_test(NAME segments COMMAND segment_checks)
endif()
add_executable(hardware_contract_checks Tests/hardware_contract_checks.cpp)
tiny_target(hardware_contract_checks)
target_link_libraries(hardware_contract_checks PRIVATE LitheView::Segments)
add_test(NAME hardware_contracts COMMAND hardware_contract_checks)
if(WIN32)
    add_executable(tiny_gecko Platform/Win32/main.cpp)
    tiny_target(tiny_gecko)
    target_link_libraries(tiny_gecko PRIVATE LitheView::Segments user32 gdi32 gdiplus)
    configure_file(board-front.png "${CMAKE_CURRENT_BINARY_DIR}/board-front.png" COPYONLY)
    add_test(NAME win32_input_workflow COMMAND tiny_gecko --smoke)
endif()
