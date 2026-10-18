# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
# Resolve from the selected compiler every time; do not reuse another toolchain's GDB.
get_filename_component(tiny_compiler_bin "${CMAKE_CXX_COMPILER}" DIRECTORY)
find_program(tiny_gdb NAMES gdb HINTS "${tiny_compiler_bin}" NO_DEFAULT_PATH NO_CACHE REQUIRED)
find_program(tiny_powershell NAMES pwsh powershell REQUIRED)
execute_process(COMMAND "${tiny_powershell}" -NoProfile -File
    "${CMAKE_CURRENT_LIST_DIR}/configure-debugger.ps1" -CompilerBin "${tiny_compiler_bin}"
    COMMAND_ERROR_IS_FATAL ANY)
