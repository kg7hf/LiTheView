# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
include_guard(GLOBAL)

# Explicit settings, then a valid cache/PATH, then known installation locations.
# No downloads or machine-specific paths are written into the source tree.
function(tiny_find_tool variable executable override)
    if(NOT "${override}" STREQUAL "")
        if(NOT EXISTS "${override}" OR IS_DIRECTORY "${override}")
            message(FATAL_ERROR "Missing explicit tool override: ${override}")
        endif()
        file(TO_CMAKE_PATH "${override}" selected)
    elseif(DEFINED ${variable} AND EXISTS "${${variable}}")
        return()
    else()
        find_program(selected NAMES "${executable}" NO_CACHE)
        if(NOT selected AND CMAKE_HOST_WIN32)
            foreach(pattern IN LISTS ARGN)
                file(GLOB candidates LIST_DIRECTORIES false "${pattern}")
                list(SORT candidates COMPARE NATURAL ORDER DESCENDING)
                if(candidates)
                    list(GET candidates 0 selected)
                    break()
                endif()
            endforeach()
        endif()
        if(NOT selected)
            message(FATAL_ERROR "Cannot find ${executable}. Install the documented tool or set ${variable} in a local CMake user preset. See README: Tool discovery.")
        endif()
    endif()
    set(${variable} "${selected}" CACHE FILEPATH "Selected ${executable}" FORCE)
    message(STATUS "${variable}: ${selected}")
endfunction()

function(tiny_find_ninja compiler_bin)
    if(NOT CMAKE_GENERATOR STREQUAL "Ninja")
        return()
    endif()
    set(TINY_NINJA "$ENV{TINY_NINJA}" CACHE FILEPATH "Optional Ninja executable")
    get_filename_component(cmake_bin "${CMAKE_COMMAND}" DIRECTORY)
    tiny_find_tool(CMAKE_MAKE_PROGRAM ninja "${TINY_NINJA}"
        "${compiler_bin}/ninja.exe"
        "${cmake_bin}/ninja.exe" "${cmake_bin}/../../Ninja/ninja.exe"
        "$ENV{USERPROFILE}/.mcuxpressotools/ninja-*/ninja.exe"
        "$ENV{LOCALAPPDATA}/stm32cube/bundles/ninja/*/ninja.exe"
        "$ENV{LOCALAPPDATA}/stm32cube/bundles/ninja/*/bin/ninja.exe")
endfunction()
