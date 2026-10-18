# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
find_program(CMAKE_CXX_COMPILER NAMES g++ REQUIRED)
get_filename_component(compiler_bin "${CMAKE_CXX_COMPILER}" DIRECTORY)
set(ENV{PATH} "${compiler_bin};$ENV{PATH}")
set(CMAKE_CXX_COMPILER_LAUNCHER
    "${CMAKE_COMMAND};-E;env;--modify;PATH=path_list_prepend:${compiler_bin}")
set(CMAKE_CXX_LINKER_LAUNCHER "${CMAKE_CXX_COMPILER_LAUNCHER}")
