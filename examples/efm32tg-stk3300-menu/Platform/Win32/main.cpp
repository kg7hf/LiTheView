// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define UNICODE
#define _UNICODE
#include "Devices/slider.hpp"
#include "UI/presenter.hpp"

#include <algorithm>
#include <array>
#include <cstdio>
#include <limits>
#include <string_view>
#include <vector>
#include <windows.h>
#include <windowsx.h>

namespace
{
constexpr RECT kPb0{560, 335, 660, 405};
constexpr RECT kPb1{690, 335, 790, 405};
constexpr RECT kSlider{80, 440, 790, 510};
constexpr COLORREF kBoard{RGB(26, 67, 59)};
constexpr COLORREF kGlass{RGB(178, 194, 162)};
constexpr COLORREF kOn{RGB(24, 42, 38)};
constexpr COLORREF kOff{RGB(162, 179, 147)};
struct Line
{
    int x1;
    int y1;
    int x2;
    int y2;
};
// A B C D E F G H J K M N P Q, matching the A03 cell labels.
constexpr std::array<Line, 14> kAlphaLines{{{8, 0, 42, 0},
                                            {48, 6, 48, 44},
                                            {48, 56, 48, 94},
                                            {8, 100, 42, 100},
                                            {2, 56, 2, 94},
                                            {2, 6, 2, 44},
                                            {8, 50, 22, 50},
                                            {9, 9, 21, 41},
                                            {25, 7, 25, 42},
                                            {41, 9, 29, 41},
                                            {28, 50, 42, 50},
                                            {29, 59, 41, 91},
                                            {25, 58, 25, 93},
                                            {21, 59, 9, 91}}};
constexpr std::array<std::size_t, 7> kNumericLines{0, 1, 2, 3, 4, 5, 6};

void fill(HDC dc, RECT bounds, COLORREF color)
{
    const auto brush = CreateSolidBrush(color);
    if (brush)
    {
        FillRect(dc, &bounds, brush);
        DeleteObject(brush);
    }
}
void text(HDC dc, int x, int y, std::wstring_view value, COLORREF color = RGB(232, 241, 228))
{
    SetTextColor(dc, color);
    SetBkMode(dc, TRANSPARENT);
    if (value.size() > std::numeric_limits<int>::max())
        return;
    // LITHEVIEW_CAST_BOUNDARY: TextOut requires int; length checked above.
    TextOutW(dc, x, y, value.data(), static_cast<int>(value.size()));
}
void stroke(HDC dc, int x, int y, Line line, bool enabled, bool numeric = false)
{
    if (numeric && line.y1 == 50)
        line.x2 = 42;
    const auto pen = CreatePen(PS_SOLID, 5, enabled ? kOn : kOff);
    if (!pen)
        return;
    const auto previous = SelectObject(dc, pen);
    MoveToEx(dc, x + line.x1, y + line.y1, nullptr);
    LineTo(dc, x + line.x2, y + line.y2);
    SelectObject(dc, previous);
    DeleteObject(pen);
}

class Window
{
public:
    [[nodiscard]] bool create(HINSTANCE instance)
    {
        WNDCLASSW definition{};
        definition.lpfnWndProc = procedure;
        definition.hInstance = instance;
        definition.lpszClassName = L"LitheViewTinyGecko";
        definition.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        if (!RegisterClassW(&definition))
            return false;
        RECT size{0, 0, 870, 600};
        constexpr DWORD style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
        if (!AdjustWindowRect(&size, style, FALSE))
            return false;
        mHandle = CreateWindowW(definition.lpszClassName,
                                L"LiTheView | Tiny Gecko A03 | Windows simulator",
                                style,
                                CW_USEDEFAULT,
                                CW_USEDEFAULT,
                                size.right - size.left,
                                size.bottom - size.top,
                                nullptr,
                                nullptr,
                                instance,
                                this);
        return mHandle != nullptr && refresh();
    }
    void show()
    {
        ShowWindow(mHandle, SW_SHOW);
        UpdateWindow(mHandle);
    }
    [[nodiscard]] HWND handle() const noexcept
    {
        return mHandle;
    }
    [[nodiscard]] const tiny::Menu& menu() const noexcept
    {
        return mMenu;
    }
    [[nodiscard]] bool failed() const noexcept
    {
        return mFailed;
    }
    [[nodiscard]] const tiny::Frame& frame() const noexcept
    {
        return mFrame;
    }
    [[nodiscard]] bool refresh()
    {
        const auto model = tiny::present(mMenu);
        const auto encoded = tiny::encodePresentation(model);
        if (!encoded)
        {
            mFailed = true;
            return false;
        }
        mFrame = *encoded;
        InvalidateRect(mHandle, nullptr, FALSE);
        return true;
    }
    void draw(HDC dc)
    {
        fill(dc, {0, 0, 870, 600}, RGB(17, 29, 28));
        fill(dc, {35, 55, 835, 535}, kBoard);
        text(dc, 35, 20, L"TINY GECKO   /   SEGMENTED MENU   /   BRD2100A A03");
        fill(dc, {65, 80, 805, 315}, kGlass);
        // Numeric cells 8..11 above the seven alphanumeric cells.
        for (std::size_t i = 0; i < 4; ++i)
            for (std::size_t j = 0; j < 7; ++j)
                stroke(dc,
                       // LITHEVIEW_CAST_BOUNDARY: four fixed numeric cells, i < 4.
                       520 + static_cast<int>(i) * 65,
                       95,
                       kAlphaLines[kNumericLines[j]],
                       mFrame.test(tiny::kDigits[i][j]),
                       true);
        for (std::size_t i = 0; i < 7; ++i)
            for (std::size_t j = 0; j < 14; ++j)
                stroke(dc,
                       // LITHEVIEW_CAST_BOUNDARY: seven fixed character cells, i < 7.
                       140 + static_cast<int>(i) * 85,
                       205,
                       kAlphaLines[j],
                       mFrame.test(tiny::kCharacters[i][j]));
        // Authored schematic geometry, in the same clockwise order as kVolumeRing.
        // No angle calculation, raster asset or volume calculation in this renderer.
        constexpr std::array<std::array<POINT, 4>, 8> ring{
            {{{{133, 91}, {167, 91}, {159, 116}, {141, 116}}},
             {{{173, 94}, {198, 119}, {175, 131}, {163, 119}}},
             {{{201, 125}, {201, 159}, {176, 151}, {176, 133}}},
             {{{198, 165}, {173, 190}, {163, 165}, {175, 153}}},
             {{{167, 193}, {133, 193}, {141, 168}, {159, 168}}},
             {{{127, 190}, {102, 165}, {125, 153}, {137, 165}}},
             {{{99, 159}, {99, 125}, {124, 133}, {124, 151}}},
             {{{102, 119}, {127, 94}, {137, 119}, {125, 131}}}}};
        for (std::size_t i = 0; i < ring.size(); ++i)
        {
            const auto brush = CreateSolidBrush(mFrame.test(tiny::kVolumeRing[i]) ? kOn : kOff);
            if (!brush)
                continue;
            const auto oldBrush = SelectObject(dc, brush);
            const auto oldPen = SelectObject(dc, GetStockObject(NULL_PEN));
            Polygon(dc, ring[i].data(), 4);
            SelectObject(dc, oldPen);
            SelectObject(dc, oldBrush);
            DeleteObject(brush);
        }
        text(dc, 225, 105, L"VOLUME RING", kOn);
        text(dc, 225, 130, L"8 fixed segments", kOn);
        text(dc, 225, 155, L"Draft while editing", kOn);
        fill(dc, kPb0, mPressed == Press::pb0 ? RGB(95, 144, 183) : RGB(210, 218, 215));
        fill(dc, kPb1, mPressed == Press::pb1 ? RGB(95, 144, 183) : RGB(210, 218, 215));
        text(dc, 575, 348, L"PB0", kOn);
        text(dc, 575, 375, L"SELECT", kOn);
        text(dc, 705, 348, L"PB1", kOn);
        text(dc, 705, 375, L"BACK", kOn);
        fill(dc, {90, 350, 115, 375}, mLed ? RGB(100, 250, 80) : RGB(35, 90, 49));
        text(dc, 125, 355, L"USER LED");
        text(dc, 80, 412, L"TOUCH SLIDER   |   drag left / right to browse or adjust");
        for (int i = 0; i < 4; ++i)
            fill(dc, {80 + i * 178, 440, 80 + i * 178 + 172, 510}, RGB(39, 100, 85));
        if (mPressed == Press::slider)
            fill(dc, {mSliderX - 4, 447, mSliderX + 4, 503}, RGB(195, 231, 179));
        text(dc, 80, 465, L"<        PREVIOUS                         NEXT        >");
        text(
            dc,
            35,
            550,
            L"Arrows: slider steps     Enter: PB0     Esc: PB1     Volume: PB0 saves, PB1 cancels");
        if (mFailed)
            text(dc,
                 35,
                 575,
                 L"ENCODING ERROR: last valid segment frame retained",
                 RGB(255, 120, 120));
    }

private:
    enum class Press
    {
        none,
        pb0,
        pb1,
        slider
    };
    void event(tiny::Event input)
    {
        const auto effect = mMenu.dispatch(input);
        if (effect == tiny::Effect::lightChanged)
            mLed = mMenu.light();
        // Playback and volume effects intentionally have no physical backend here.
        (void)refresh();
    }
    void cancel()
    {
        mPressed = Press::none;
        mSlider.release();
        if (GetCapture() == mHandle)
            ReleaseCapture();
        InvalidateRect(mHandle, nullptr, FALSE);
    }
    void slide(int x)
    {
        // LITHEVIEW_CAST_BOUNDARY: fixed Win32 LONG coordinates 80..790 fit int.
        mSliderX = std::clamp(x, static_cast<int>(kSlider.left), static_cast<int>(kSlider.right));
        // LITHEVIEW_CAST_BOUNDARY: clamped slider scales to 0..1000, fits uint16_t.
        const auto position = static_cast<std::uint16_t>((mSliderX - kSlider.left) * 1000 /
                                                         (kSlider.right - kSlider.left));
        if (const auto input = mSlider.sample(position))
            event(*input);
        InvalidateRect(mHandle, nullptr, FALSE);
    }
    LRESULT message(UINT message, WPARAM wParam, LPARAM lParam)
    {
        const POINT point{GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
        switch (message)
        {
        case WM_PAINT:
        {
            PAINTSTRUCT paint{};
            const auto dc = BeginPaint(mHandle, &paint);
            // Single buffered host paint; the portable path owns only segment bits.
            const auto buffer = CreateCompatibleDC(dc);
            const auto bitmap = CreateCompatibleBitmap(dc, 870, 600);
            if (buffer && bitmap)
            {
                const auto previous = SelectObject(buffer, bitmap);
                draw(buffer);
                BitBlt(dc, 0, 0, 870, 600, buffer, 0, 0, SRCCOPY);
                SelectObject(buffer, previous);
            }
            else
                draw(dc);
            if (bitmap)
                DeleteObject(bitmap);
            if (buffer)
                DeleteDC(buffer);
            EndPaint(mHandle, &paint);
            return 0;
        }
        case WM_LBUTTONDOWN:
            SetFocus(mHandle);
            if (PtInRect(&kPb0, point))
                mPressed = Press::pb0;
            else if (PtInRect(&kPb1, point))
                mPressed = Press::pb1;
            else if (PtInRect(&kSlider, point))
                mPressed = Press::slider;
            if (mPressed != Press::none)
                SetCapture(mHandle);
            if (mPressed == Press::slider)
                slide(point.x);
            InvalidateRect(mHandle, nullptr, FALSE);
            return 0;
        case WM_MOUSEMOVE:
            if (mPressed == Press::slider)
                slide(point.x);
            return 0;
        case WM_LBUTTONUP:
        {
            const auto pressed = mPressed;
            cancel();
            if (pressed == Press::pb0 && PtInRect(&kPb0, point))
                event(tiny::Event::select);
            if (pressed == Press::pb1 && PtInRect(&kPb1, point))
                event(tiny::Event::back);
            return 0;
        }
        case WM_KEYDOWN:
            // Suppress OS repeat for all keys; slider steps are deliberate presses.
            if ((lParam & (1LL << 30)) != 0)
                return 0;
            if (wParam == VK_LEFT || wParam == VK_UP)
                event(tiny::Event::previous);
            if (wParam == VK_RIGHT || wParam == VK_DOWN)
                event(tiny::Event::next);
            if (wParam == VK_RETURN)
                event(tiny::Event::select);
            if (wParam == VK_ESCAPE)
                event(tiny::Event::back);
            return 0;
        case WM_KILLFOCUS:
        case WM_CANCELMODE:
        case WM_CAPTURECHANGED:
            cancel();
            return 0;
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
        default:
            return DefWindowProcW(mHandle, message, wParam, lParam);
        }
    }
    // Pointer erasure is confined to the Win32 window-user-data boundary.
    static LRESULT CALLBACK procedure(HWND handle, UINT message, WPARAM wParam, LPARAM lParam)
    {
        // LITHEVIEW_CAST_BOUNDARY: Win32 user data holds this live Window exclusively.
        auto* window = reinterpret_cast<Window*>(GetWindowLongPtrW(handle, GWLP_USERDATA));
        if (message == WM_NCCREATE)
        {
            // LITHEVIEW_CAST_BOUNDARY: WM_NCCREATE guarantees a CREATESTRUCTW pointer.
            const auto* creation = reinterpret_cast<const CREATESTRUCTW*>(lParam);
            // LITHEVIEW_CAST_BOUNDARY: CreateWindowW receives our typed this pointer.
            window = static_cast<Window*>(creation->lpCreateParams);
            window->mHandle = handle;
            // LITHEVIEW_CAST_BOUNDARY: LONG_PTR is the Win32 pointer-sized storage slot.
            SetWindowLongPtrW(handle, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(window));
        }
        return window ? window->message(message, wParam, lParam)
                      : DefWindowProcW(handle, message, wParam, lParam);
    }
    HWND mHandle{};
    tiny::Menu mMenu{};
    tiny::Frame mFrame{};
    tiny::Slider mSlider{};
    Press mPressed{Press::none};
    int mSliderX{80};
    bool mLed{};
    bool mFailed{};
};

// Integration check drives this executable's own hidden window through its real
// Win32 message boundary. It never selects or controls another application.
bool smoke(Window& window)
{
    const auto ringIs = [&](unsigned count)
    {
        constexpr std::array<std::size_t, 8> ids{140, 147, 146, 145, 144, 143, 142, 141};
        for (std::size_t i = 0; i < ids.size(); ++i)
            if (window.frame().test(ids[i]) != (i < count))
                return false;
        return true;
    };
    if (!ringIs(4))
        return false;
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RIGHT, 0);
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RETURN, 0);
    for (unsigned i = 0; i < 50; ++i)
        SendMessageW(window.handle(), WM_KEYDOWN, VK_RIGHT, 0);
    if (!ringIs(8))
        return false;
    SendMessageW(window.handle(), WM_KEYDOWN, VK_ESCAPE, 0);
    if (!ringIs(4))
        return false;
    SendMessageW(window.handle(), WM_KEYDOWN, VK_LEFT, 0);
    const auto click = [&](int x, int y)
    {
        SendMessageW(window.handle(), WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(x, y));
        SendMessageW(window.handle(), WM_LBUTTONUP, 0, MAKELPARAM(x, y));
    };
    click(600, 360);
    if (!window.menu().playing())
        return false;
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RIGHT, 0);
    click(600, 360);
    if (window.menu().page() != tiny::Page::volume)
        return false;
    SendMessageW(window.handle(), WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(200, 475));
    SendMessageW(window.handle(), WM_MOUSEMOVE, MK_LBUTTON, MAKELPARAM(300, 475));
    SendMessageW(window.handle(), WM_LBUTTONUP, 0, MAKELPARAM(300, 475));
    if (window.menu().draft() != 51)
        return false;
    click(730, 360);
    if (window.menu().volume() != 50 || window.menu().page() != tiny::Page::home)
        return false;
    click(600, 360);
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RIGHT, 0);
    click(600, 360);
    if (window.menu().volume() != 51)
        return false;
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RIGHT, 0);
    click(600, 360);
    click(600, 360);
    if (!window.menu().light())
        return false;
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RIGHT, 0);
    click(600, 360);
    if (window.menu().page() != tiny::Page::info)
        return false;
    click(730, 360);
    click(730, 360);
    // Auto-repeat must not duplicate selection or navigation.
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RETURN, 1LL << 30);
    SendMessageW(window.handle(), WM_KEYDOWN, VK_RIGHT, 1LL << 30);
    if (window.menu().page() != tiny::Page::home || window.menu().selection() != 2)
        return false;
    // Releasing outside PB0 cancels the click.
    SendMessageW(window.handle(), WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(600, 360));
    SendMessageW(window.handle(), WM_LBUTTONUP, 0, MAKELPARAM(20, 20));
    if (window.menu().page() != tiny::Page::home)
        return false;
    // Focus loss breaks slider contact; a later mouse move cannot navigate.
    SendMessageW(window.handle(), WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(200, 475));
    SendMessageW(window.handle(), WM_KILLFOCUS, 0, 0);
    SendMessageW(window.handle(), WM_MOUSEMOVE, MK_LBUTTON, MAKELPARAM(700, 475));
    if (window.menu().selection() != 2)
        return false;
    // Capture loss must cancel PB0, not activate it on a later release.
    SendMessageW(window.handle(), WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(600, 360));
    SendMessageW(window.handle(), WM_CANCELMODE, 0, 0);
    SendMessageW(window.handle(), WM_LBUTTONUP, 0, MAKELPARAM(600, 360));
    return window.menu().page() == tiny::Page::home && !window.failed();
}

// Host-only rendering artifact for visual QA; no screenshot of other windows.
bool snapshot(Window& window, const char* path)
{
    const auto screen = GetDC(window.handle());
    if (!screen)
        return false;
    const auto dc = CreateCompatibleDC(screen);
    const auto bitmap = CreateCompatibleBitmap(screen, 870, 600);
    bool saved = false;
    if (dc && bitmap)
    {
        const auto previous = SelectObject(dc, bitmap);
        window.draw(dc);
        SelectObject(dc, previous);
        BITMAPINFO info{};
        info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = 870;
        info.bmiHeader.biHeight = -600;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = BI_RGB;
        std::vector<unsigned char> pixels(870U * 600U * 4U);
        if (GetDIBits(dc, bitmap, 0, 600, pixels.data(), &info, DIB_RGB_COLORS) == 600)
        {
            BITMAPFILEHEADER header{};
            header.bfType = 0x4D42;
            header.bfOffBits = sizeof(header) + sizeof(info.bmiHeader);
            // LITHEVIEW_CAST_BOUNDARY: fixed 870*600*4 host image fits DWORD.
            header.bfSize = header.bfOffBits + static_cast<DWORD>(pixels.size());
            if (auto* file = std::fopen(path, "wb"))
            {
                saved = std::fwrite(&header, sizeof(header), 1, file) == 1 &&
                        std::fwrite(&info.bmiHeader, sizeof(info.bmiHeader), 1, file) == 1 &&
                        std::fwrite(pixels.data(), pixels.size(), 1, file) == 1;
                if (std::fclose(file) != 0)
                    saved = false;
            }
        }
    }
    if (bitmap)
        DeleteObject(bitmap);
    if (dc)
        DeleteDC(dc);
    ReleaseDC(window.handle(), screen);
    return saved;
}
} // namespace
int main(int argc, char** argv)
{
    Window window;
    if (!window.create(GetModuleHandleW(nullptr)))
        return 1;
    if (argc == 3 && std::string_view{argv[1]} == "--snapshot")
    {
        const bool saved = snapshot(window, argv[2]);
        DestroyWindow(window.handle());
        return saved ? 0 : 3;
    }
    if (argc == 2 && std::string_view{argv[1]} == "--smoke")
    {
        const bool passed = smoke(window);
        std::puts(passed ? "Win32 mouse/slider/key menu smoke passed" : "Win32 smoke FAILED");
        DestroyWindow(window.handle());
        return passed ? 0 : 2;
    }
    window.show();
    MSG message{};
    int result{};
    while ((result = GetMessageW(&message, nullptr, 0, 0)) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    return result < 0 || window.failed() ? 1 : 0;
}
