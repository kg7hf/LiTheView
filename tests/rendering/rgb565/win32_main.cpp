// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "../win32/win32_host.hpp"
#include "configuration.hpp"

using ProductConfiguration = lithe::examples::rgb565::Configuration;
static_assert(lithe::supportsDestination(ProductConfiguration::kFormat));
static_assert(lithe::demo::config::kFontBackend == ProductConfiguration::kFontBackend);
static_assert(lithe::demo::config::kAnimationFormat == ProductConfiguration::kAnimationFormat);

int WINAPI WinMain(HINSTANCE instance,
                   [[maybe_unused]] HINSTANCE previousInstance,
                   [[maybe_unused]] LPSTR commandLine,
                   int showCommand)
{
    return lithe::examples::runWin32(instance, showCommand);
}
