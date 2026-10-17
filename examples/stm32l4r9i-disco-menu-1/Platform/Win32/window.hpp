// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <cstdint>
#include <windows.h>

namespace starter::host
{
enum class Key
{
    select,
    back,
    up,
    down,
    left,
    right
};
void paint(HDC context, const RECT& destination) noexcept;
void pointer(std::int32_t x, std::int32_t y, bool pressed, bool cancelled = false) noexcept;
void key(Key value) noexcept;
void setUserButton(bool pressed) noexcept;
[[nodiscard]] bool checkDisplay() noexcept;
[[nodiscard]] std::uint32_t frameFingerprint() noexcept;
} // namespace starter::host
