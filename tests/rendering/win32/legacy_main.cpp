// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "win32_host.hpp"

int WINAPI WinMain(HINSTANCE instance,
                   [[maybe_unused]] HINSTANCE previousInstance,
                   [[maybe_unused]] LPSTR commandLine,
                   int showCommand)
{
    return lithe::examples::runWin32(instance, showCommand);
}
