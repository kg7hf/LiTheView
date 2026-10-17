// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/status.hpp"
#include "Platform/Win32/window.hpp"

namespace starter::host
{
void pointer(std::int32_t, std::int32_t, bool, bool) noexcept
{
}
void key(Key) noexcept
{
}
void setUserButton(bool) noexcept
{
}
void paint(HDC context, const RECT& destination) noexcept
{
    // LITHEVIEW_CAST_BOUNDARY: GDI's generic stock handle is a BLACK_BRUSH here.
    FillRect(context, &destination, static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));
}
std::uint32_t frameFingerprint() noexcept
{
    return 0U;
}
bool checkDisplay() noexcept
{
    return status.frames.load() == 0U;
}
} // namespace starter::host
