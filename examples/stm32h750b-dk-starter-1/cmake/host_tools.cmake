# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

# Resolve the same installation for CMake Tools, terminal builds and F5.
# Local absolute paths and debugger junctions are generated under ignored build/.
find_program(STARTER_PWSH NAMES pwsh REQUIRED)
get_filename_component(starter_root "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)
execute_process(COMMAND "${STARTER_PWSH}" -NoProfile -File
    "${starter_root}/tools/configure-tools.ps1" -Platform "${STARTER_PLATFORM}"
    RESULT_VARIABLE tool_result OUTPUT_VARIABLE tool_output ERROR_VARIABLE tool_error)
if(NOT tool_result EQUAL 0)
    message(FATAL_ERROR "Starter tool discovery failed:\n${tool_output}\n${tool_error}")
endif()
include("${starter_root}/build/tool-paths-${STARTER_PLATFORM}.cmake")
if(NOT CMAKE_MAKE_PROGRAM)
    set(CMAKE_MAKE_PROGRAM "${STARTER_NINJA}" CACHE FILEPATH "Ninja executable")
endif()
# A cached compiler must not silently diverge from the selected debugger.
foreach(pair IN ITEMS "C;STARTER_CC" "CXX;STARTER_CXX")
    list(GET pair 0 language)
    list(GET pair 1 selected)
    if(CMAKE_${language}_COMPILER AND NOT CMAKE_${language}_COMPILER STREQUAL "${${selected}}")
        message(FATAL_ERROR "Compiler selection changed. Configure with --fresh before building ${STARTER_PLATFORM}.")
    endif()
    set(CMAKE_${language}_COMPILER "${${selected}}" CACHE FILEPATH "Selected compiler")
endforeach()
