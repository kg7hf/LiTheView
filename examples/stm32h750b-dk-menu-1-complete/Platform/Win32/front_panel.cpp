// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Platform/Win32/front_panel.hpp"

#include "Application/status.hpp"
#include "front_panel_config.hpp"

#include <array>
#include <cstdio>
#include <wtypes.h>

// MinGW GDI+ requires PROPID from wtypes.h before its declarations.
#include <gdiplus.h>
#include <optional>
#include <string_view>
#include <utility>

namespace starter::host
{
namespace
{
// GDI+ owns its PNG decoder memory on the Windows thread, outside the shared UI task.
struct FrontImage final
{
    ULONG_PTR token{};
    std::optional<Gdiplus::Bitmap> bitmap;
    ~FrontImage()
    {
        bitmap.reset();
        if (token != 0U)
        {
            Gdiplus::GdiplusShutdown(token);
        }
    }
};
FrontImage image;
enum class Capture : std::uint8_t { none, display, userButton };
Capture captured{};
void cancelInput() noexcept
{
    if (captured == Capture::display)
    {
        pointer(0, 0, false, true);
    }
    setUserButton(false);
    captured = Capture::none;
}
} // namespace

bool initializeFrontPanel() noexcept
{
    if constexpr (!front::enabled)
    {
        return true;
    }
    Gdiplus::GdiplusStartupInput startup;
    if (Gdiplus::GdiplusStartup(&image.token, &startup, nullptr) != Gdiplus::Ok)
    {
        return false;
    }
    std::array<wchar_t, 32768U> executable{};
    const auto length = GetModuleFileNameW(nullptr, executable.data(), executable.size());
    if (length == 0U || length >= executable.size())
    {
        return false;
    }
    std::wstring_view name{executable.data(), length};
    const auto separator = name.find_last_of(L"\\/");
    constexpr std::wstring_view filename{L"board-front.png"};
    if (separator == name.npos || separator + 1U + filename.size() >= executable.size())
    {
        return false;
    }
    filename.copy(executable.data() + separator + 1U, filename.size());
    executable[separator + 1U + filename.size()] = L'\0';
    image.bitmap.emplace(executable.data());
    return image.bitmap->GetLastStatus() == Gdiplus::Ok &&
           std::cmp_equal(image.bitmap->GetWidth(), front::transform.imageWidth) &&
           std::cmp_equal(image.bitmap->GetHeight(), front::transform.imageHeight);
}

Area windowArea() noexcept
{
    return {0, 0, front::transform.windowWidth, front::transform.windowHeight};
}

bool paintWindow(HDC context) noexcept
{
    const auto display = front::transform.toWindow(front::display);
    if constexpr (front::enabled)
    {
        if (!image.bitmap)
        {
            return false;
        }
        // Keep the old LCD pixels visible until their replacement is drawn.
        // Painting the photograph over the LCD first would introduce flicker.
        const auto saved = SaveDC(context);
        if (saved == 0)
        {
            return false;
        }
        ExcludeClipRect(context, display.x, display.y, display.x + display.width, display.y + display.height);
        bool drawn{false};
        {
            Gdiplus::Graphics graphics{context};
            drawn = graphics.DrawImage(&*image.bitmap, 0, 0, front::transform.windowWidth,
                                      front::transform.windowHeight) == Gdiplus::Ok;
        }
        if (!RestoreDC(context, saved) || !drawn)
        {
            return false;
        }
    }
    paint(context, RECT{display.x, display.y, display.x + display.width, display.y + display.height});
    return true;
}

void mouse(MousePhase phase, std::int32_t x, std::int32_t y) noexcept
{
    if (phase == MousePhase::cancelled)
    {
        cancelInput();
        return;
    }
    if (phase == MousePhase::pressed)
    {
        cancelInput();
        for (const auto& button : front::buttons)
        {
            if (button.id == "userButton" && front::transform.toWindow(button.bounds).contains(x, y))
            {
                captured = Capture::userButton;
                setUserButton(true);
                return;
            }
        }
        if (front::transform.displayPoint(front::display, x, y, 480, 272))
        {
            captured = Capture::display;
        }
    }
    if (captured == Capture::display)
    {
        const auto point = front::transform.displayPoint(front::display, x, y, 480, 272);
        if (!point)
        {
            cancelInput();
            return;
        }
        pointer(point->x, point->y, phase != MousePhase::released);
    }
    else if (captured == Capture::userButton && phase == MousePhase::moved)
    {
        for (const auto& button : front::buttons)
        {
            if (button.id == "userButton" && !front::transform.toWindow(button.bounds).contains(x, y))
            {
                cancelInput();
                return;
            }
        }
    }
    if (phase == MousePhase::released)
    {
        setUserButton(false);
        captured = Capture::none;
    }
}

} // namespace starter::host
