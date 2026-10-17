# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

# The CMSIS wrapper encodes handles in uint32_t. Require i686 on a 64-bit PC too.
set(STARTER_PLATFORM Win32)
include("${CMAKE_CURRENT_LIST_DIR}/host_tools.cmake")
# GCC's cc1/cc1plus and linker helpers also load DLLs through PATH. An absolute
# g++.exe path alone can still load a different installed MinGW's 64-bit runtime.
# Put the selected compiler's bin first for each compile/link invocation, including
# direct Ninja/CMake Tools builds and try_compile, without changing the user's PATH.
foreach(language IN ITEMS C CXX)
    get_filename_component(runtime_bin "${CMAKE_${language}_COMPILER}" DIRECTORY)
    set(CMAKE_${language}_COMPILER_LAUNCHER
        "${CMAKE_COMMAND};-E;env;--modify;PATH=path_list_prepend:${runtime_bin}")
    set(CMAKE_${language}_LINKER_LAUNCHER "${CMAKE_${language}_COMPILER_LAUNCHER}")
endforeach()
# CMake's initial compiler-identification probes run before launchers are used.
# This modifies only the configure process and its children, not the user's PATH.
set(ENV{PATH} "${runtime_bin};$ENV{PATH}")
set(CMAKE_ASM_COMPILER ${CMAKE_C_COMPILER})
set(CMAKE_C_FLAGS_INIT "-ffunction-sections -fdata-sections")
set(CMAKE_CXX_FLAGS_INIT "${CMAKE_C_FLAGS_INIT}")
