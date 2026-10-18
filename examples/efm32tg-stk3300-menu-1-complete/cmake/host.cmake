# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
include("${CMAKE_CURRENT_LIST_DIR}/find_tools.cmake")
set(TINY_MINGW_BIN "$ENV{TINY_MINGW_BIN}" CACHE PATH "Optional MinGW-w64 bin directory")
list(APPEND CMAKE_TRY_COMPILE_PLATFORM_VARIABLES TINY_MINGW_BIN TINY_NINJA)
set(tiny_cxx_override "")
if(TINY_MINGW_BIN)
    set(tiny_cxx_override "${TINY_MINGW_BIN}/g++.exe")
endif()
tiny_find_tool(CMAKE_CXX_COMPILER g++ "${tiny_cxx_override}"
    "$ENV{GCC_HOME}/bin/g++.exe"
    "$ENV{LOCALAPPDATA}/Microsoft/WinGet/Packages/BrechtSanders.WinLibs*/mingw64/bin/g++.exe"
    "$ENV{SystemDrive}/msys64/ucrt64/bin/g++.exe"
    "$ENV{SystemDrive}/msys64/mingw64/bin/g++.exe")
get_filename_component(compiler_bin "${CMAKE_CXX_COMPILER}" DIRECTORY)
tiny_find_ninja("${compiler_bin}")
set(ENV{PATH} "${compiler_bin};$ENV{PATH}")
set(CMAKE_CXX_COMPILER_LAUNCHER
    "${CMAKE_COMMAND};-E;env;--modify;PATH=path_list_prepend:${compiler_bin}")
set(CMAKE_CXX_LINKER_LAUNCHER "${CMAKE_CXX_COMPILER_LAUNCHER}")
