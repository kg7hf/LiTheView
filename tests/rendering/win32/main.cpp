// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "litheview/win32_host_config.hpp"
#include "win32_host.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <span>
#include <windowsx.h>

namespace
{

using ConfiguredDemo = lithe::examples::ProductScene;

constexpr int kLogicalWidth = ConfiguredDemo::kWidth;
constexpr int kLogicalHeight = ConfiguredDemo::kHeight;
constexpr UINT_PTR kAnimationTimer = 1U;
constexpr UINT kAnimationTickMs = 40U;

template <lithe::PixelFormat Format> class Win32Display final : public lithe::Display
{
    static_assert(Format == lithe::PixelFormat::rgb565 || Format == lithe::PixelFormat::xrgb8888);

public:
    Win32Display() noexcept
    {
        mBitmap.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        mBitmap.bmiHeader.biWidth = kLogicalWidth;
        mBitmap.bmiHeader.biHeight = -kLogicalHeight;
        mBitmap.bmiHeader.biPlanes = 1;
        mBitmap.bmiHeader.biBitCount = 32;
        mBitmap.bmiHeader.biCompression = BI_RGB;
    }

    template <class Scene> [[nodiscard]] bool render(Scene& scene) noexcept
    {
        return scene.render(*this);
    }

    void attach(HWND window) noexcept
    {
        mWindow = window;
    }

    lithe::Surface surface() noexcept override
    {
        return lithe::Surface{lithe::PixelSurface<Format>{
            std::span{mPixels}, kLogicalWidth, kLogicalHeight, kLogicalWidth}};
    }

    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        updatePresentation(regions);
        if (mWindow != nullptr)
        {
            InvalidateRect(mWindow, nullptr, FALSE);
        }
    }

    void paint(HDC device, RECT client) noexcept
    {
        SetStretchBltMode(device, COLORONCOLOR);
        StretchDIBits(device,
                      0,
                      0,
                      client.right - client.left,
                      client.bottom - client.top,
                      0,
                      0,
                      kLogicalWidth,
                      kLogicalHeight,
                      presentationPixels().data(),
                      &mBitmap,
                      DIB_RGB_COLORS,
                      SRCCOPY);
    }

private:
    using FramebufferPixel = typename lithe::PixelFormatTraits<Format>::Storage;

    static constexpr std::size_t kPixelCount = lithe::detail::nonnegativeSize(kLogicalWidth) *
                                               lithe::detail::nonnegativeSize(kLogicalHeight);
    static constexpr std::size_t kPresentationPixelCount =
        Format == lithe::PixelFormat::rgb565 ? kPixelCount : 0U;

    void updatePresentation(std::span<const lithe::Rect> regions) noexcept
    {
        if constexpr (Format == lithe::PixelFormat::rgb565)
        {
            for (const lithe::Rect& region : regions)
            {
                const lithe::Rect visible =
                    lithe::intersect(region, {0, 0, kLogicalWidth, kLogicalHeight});
                for (std::int32_t y = visible.y; y < visible.bottom(); ++y)
                {
                    for (std::int32_t x = visible.x; x < visible.right(); ++x)
                    {
                        const std::size_t index = (lithe::detail::nonnegativeSize(y) *
                                                   lithe::detail::nonnegativeSize(kLogicalWidth)) +
                                                  lithe::detail::nonnegativeSize(x);
                        const lithe::Color color = lithe::Color::fromRgb565(mPixels[index]);
                        mPresentationPixels[index] = {color.blue, color.green, color.red, 255U};
                    }
                }
            }
        }
    }

    std::span<const lithe::Xrgb8888Pixel> presentationPixels() const noexcept
    {
        if constexpr (Format == lithe::PixelFormat::rgb565)
        {
            return mPresentationPixels;
        }
        else
        {
            return mPixels;
        }
    }

    BITMAPINFO mBitmap{};
    std::array<FramebufferPixel, kPixelCount> mPixels{};
    std::array<lithe::Xrgb8888Pixel, kPresentationPixelCount> mPresentationPixels{};
    HWND mWindow{nullptr};
};

using ProductConfiguration = lithe::examples::ProductConfiguration;
using ConfiguredWin32Display = Win32Display<ProductConfiguration::kFormat>;
constexpr const wchar_t* kConfiguredWindowTitle = lithe::examples::kHostWindowTitle;

struct Host
{
    ConfiguredWin32Display display;
    ConfiguredDemo demo{};
    bool render() noexcept
    {
        return display.render(demo);
    }
    lithe::Point lastPointer{};
    bool pointerDown{false};
    bool animationTimerRunning{false};
    wchar_t pendingHighSurrogate{0};
};

void syncAnimationTimer(HWND window, Host& host) noexcept
{
    const bool needed = host.demo.animationsRunning();
    if (needed && !host.animationTimerRunning)
    {
        host.animationTimerRunning =
            SetTimer(window, kAnimationTimer, kAnimationTickMs, nullptr) != 0U;
    }
    else if (!needed && host.animationTimerRunning)
    {
        KillTimer(window, kAnimationTimer);
        host.animationTimerRunning = false;
    }
}

lithe::Point logicalPointer(HWND window, LPARAM lParam) noexcept
{
    RECT client{};
    GetClientRect(window, &client);
    const int clientWidth = client.right - client.left;
    const int clientHeight = client.bottom - client.top;
    if (clientWidth <= 0 || clientHeight <= 0)
    {
        return {};
    }
    const int mouseX = GET_X_LPARAM(lParam);
    const int mouseY = GET_Y_LPARAM(lParam);
    return {lithe::detail::multiplyDivide(mouseX, kLogicalWidth, clientWidth),
            lithe::detail::multiplyDivide(mouseY, kLogicalHeight, clientHeight)};
}

lithe::Key translateKey(WPARAM key, bool& recognized) noexcept
{
    recognized = true;
    switch (key)
    {
    case VK_UP:
        return lithe::Key::up;
    case VK_DOWN:
        return lithe::Key::down;
    case VK_LEFT:
        return lithe::Key::left;
    case VK_RIGHT:
        return lithe::Key::right;
    case VK_RETURN:
    case VK_SPACE:
        return lithe::Key::select;
    case VK_ESCAPE:
        return lithe::Key::back;
    default:
        recognized = false;
        return lithe::Key::select;
    }
}

bool dispatchTextInput(Host& host, WPARAM value) noexcept
{
    if (value > 0xffffU)
    {
        return false;
    }
    const WORD unit = LOWORD(value);
    if (unit >= 0xd800U && unit <= 0xdbffU)
    {
        host.pendingHighSurrogate = unit;
        return true;
    }

    char32_t codepoint = unit;
    if (unit >= 0xdc00U && unit <= 0xdfffU)
    {
        if (host.pendingHighSurrogate == 0)
        {
            return false;
        }
        const std::uint32_t high = host.pendingHighSurrogate;
        const std::uint32_t low = unit;
        const std::uint32_t scalar = 0x10000U + ((high - 0xd800U) << 10U) + (low - 0xdc00U);
        codepoint = scalar;
        host.pendingHighSurrogate = 0;
    }
    else
    {
        host.pendingHighSurrogate = 0;
    }
    return host.demo.handleTextInput(codepoint);
}

LRESULT CALLBACK windowProcedure(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
{
    // GWLP_USERDATA is the Win32-defined integer-width storage slot for an application pointer.
    // LITHEVIEW_CAST_BOUNDARY: Win32 returns pointer bits through LONG_PTR.
    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast,performance-no-int-to-ptr)
    Host* host = reinterpret_cast<Host*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if (message == WM_NCCREATE)
    {
        // WM_NCCREATE defines lParam as a CREATESTRUCTW pointer.
        // LITHEVIEW_CAST_BOUNDARY: WM_NCCREATE specifies LPARAM as CREATESTRUCTW*.
        // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast,performance-no-int-to-ptr)
        const auto* create = reinterpret_cast<const CREATESTRUCTW*>(lParam);
        // LITHEVIEW_CAST_BOUNDARY: Win32 returns the caller's typed context as void*.
        host = static_cast<Host*>(create->lpCreateParams);
        // GWLP_USERDATA stores the non-owning host pointer for later callbacks.
        // LITHEVIEW_CAST_BOUNDARY: GWLP_USERDATA is Win32's pointer-sized integer slot.
        // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(host));
        host->display.attach(window);
    }

    switch (message)
    {
    case WM_CHAR:
        if (host != nullptr && dispatchTextInput(*host, wParam))
        {
            host->render();
            return 0;
        }
        break;
    case WM_KEYDOWN:
        if (host != nullptr)
        {
            bool recognized = false;
            const lithe::Key key = translateKey(wParam, recognized);
            if (recognized)
            {
                if (host->demo.handleKey(key))
                {
                    host->render();
                }
                syncAnimationTimer(window, *host);
                return 0;
            }
        }
        break;
    case WM_LBUTTONDOWN:
        if (host != nullptr)
        {
            host->lastPointer = logicalPointer(window, lParam);
            host->pointerDown = true;
            SetCapture(window);
            if (host->demo.handlePointer(
                    {host->lastPointer, lithe::PointerPhase::pressed, 0U, GetTickCount()}))
            {
                host->render();
            }
            syncAnimationTimer(window, *host);
            return 0;
        }
        break;
    case WM_MOUSEMOVE:
        if (host != nullptr && host->pointerDown)
        {
            host->lastPointer = logicalPointer(window, lParam);
            if (host->demo.handlePointer(
                    {host->lastPointer, lithe::PointerPhase::moved, 0U, GetTickCount()}))
            {
                host->render();
            }
            syncAnimationTimer(window, *host);
            return 0;
        }
        break;
    case WM_LBUTTONUP:
        if (host != nullptr && host->pointerDown)
        {
            host->lastPointer = logicalPointer(window, lParam);
            host->pointerDown = false;
            const bool handled = host->demo.handlePointer(
                {host->lastPointer, lithe::PointerPhase::released, 0U, GetTickCount()});
            ReleaseCapture();
            if (handled)
            {
                host->render();
            }
            syncAnimationTimer(window, *host);
            return 0;
        }
        break;
    case WM_CAPTURECHANGED:
        if (host != nullptr && host->pointerDown)
        {
            host->pointerDown = false;
            if (host->demo.handlePointer(
                    {host->lastPointer, lithe::PointerPhase::cancelled, 0U, GetTickCount()}))
            {
                host->render();
            }
            syncAnimationTimer(window, *host);
        }
        break;
    case WM_TIMER:
        if (host != nullptr && wParam == kAnimationTimer)
        {
            if (host->demo.advanceTime(kAnimationTickMs))
            {
                host->render();
            }
            syncAnimationTimer(window, *host);
            return 0;
        }
        break;
    case WM_PAINT:
        if (host != nullptr)
        {
            PAINTSTRUCT paint{};
            HDC device = BeginPaint(window, &paint);
            RECT client{};
            GetClientRect(window, &client);
            host->display.paint(device, client);
            EndPaint(window, &paint);
            return 0;
        }
        break;
    case WM_ERASEBKGND:
        return 1;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }
    return DefWindowProcW(window, message, wParam, lParam);
}

} // namespace

int lithe::examples::runWin32(HINSTANCE hInstance, int nShowCmd)
{
    constexpr const wchar_t* kClassName = lithe::examples::kHostWindowClass;
    WNDCLASSW windowClass{};
    windowClass.style = CS_HREDRAW | CS_VREDRAW;
    windowClass.lpfnWndProc = windowProcedure;
    windowClass.hInstance = hInstance;
    // MAKEINTRESOURCEW is the Win32 resource-ID-as-pointer boundary.
    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast,performance-no-int-to-ptr)
    windowClass.hCursor = LoadCursorW(nullptr, MAKEINTRESOURCEW(32512));
    windowClass.hbrBackground = GetSysColorBrush(COLOR_WINDOW);
    windowClass.lpszClassName = kClassName;
    if (RegisterClassW(&windowClass) == 0)
    {
        return 1;
    }

    // Host adapter presentation storage is bounded and kept off the WinMain stack.
    static Host host{};
    constexpr int kWindowWidth = kLogicalWidth * 2;
    constexpr int kWindowHeight = kLogicalHeight * 2;
    RECT desired{0, 0, kWindowWidth, kWindowHeight};
    AdjustWindowRect(&desired, WS_OVERLAPPEDWINDOW, FALSE);
    HWND window = CreateWindowExW(0,
                                  kClassName,
                                  kConfiguredWindowTitle,
                                  WS_OVERLAPPEDWINDOW,
                                  CW_USEDEFAULT,
                                  CW_USEDEFAULT,
                                  desired.right - desired.left,
                                  desired.bottom - desired.top,
                                  nullptr,
                                  nullptr,
                                  hInstance,
                                  &host);
    if (window == nullptr || !host.demo.initialize())
    {
        return 2;
    }
    if (!host.render())
    {
        return 3;
    }
    syncAnimationTimer(window, host);
    ShowWindow(window, nShowCmd);
    UpdateWindow(window);

    MSG message{};
    while (GetMessage(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessage(&message);
    }
    return LOWORD(message.wParam);
}
