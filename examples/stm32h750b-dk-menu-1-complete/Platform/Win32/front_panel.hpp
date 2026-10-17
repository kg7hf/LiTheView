// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Platform/Win32/window.hpp"
#include "Platform/Win32/front_panel_geometry.hpp"

namespace starter::host
{
enum class MousePhase : std::uint8_t { pressed, moved, released, cancelled };
[[nodiscard]] bool initializeFrontPanel() noexcept;
[[nodiscard]] Area windowArea() noexcept;
[[nodiscard]] bool paintWindow(HDC context) noexcept;
void mouse(MousePhase phase, std::int32_t x, std::int32_t y) noexcept;
} // namespace starter::host
