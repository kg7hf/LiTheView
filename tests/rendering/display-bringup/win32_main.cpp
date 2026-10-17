// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "product.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <span>
#include <windows.h>
#include <windowsx.h>

namespace
{

using namespace display_bringup;
constexpr int kWidth{kDisplay.logicalSize.width};
constexpr int kHeight{kDisplay.logicalSize.height};
constexpr int kInitialScale{4};
constexpr std::size_t kPixelCount{kFrameBytes / 2U};
constexpr wchar_t kClassName[]{L"LitheViewDisplayBringup"};
constexpr wchar_t kTitle[]{L"LitheView PC simulator - mouse/touch, wheel/knob, Enter/select"};

struct Presentation
{
    int scale{0};
    int left{0};
    int top{0};
};

constexpr Presentation presentationForSize(int width, int height) noexcept
{
    if (width < kWidth || height < kHeight)
    {
        return {};
    }
    const int scale = std::min(width / kWidth, height / kHeight);
    return {scale, (width - kWidth * scale) / 2, (height - kHeight * scale) / 2};
}

Presentation presentation(HWND window) noexcept
{
    RECT client{};
    if (!GetClientRect(window, &client))
    {
        return {};
    }
    return presentationForSize(client.right - client.left, client.bottom - client.top);
}

constexpr int logicalAxis(int position, int origin, int scale, int extent) noexcept
{
    if (scale <= 0 || position < origin)
    {
        return -1;
    }
    if (position >= origin + extent * scale)
    {
        return extent;
    }
    return (position - origin) / scale;
}

lithe::Point logicalPointer(HWND window, LPARAM position) noexcept
{
    const Presentation area = presentation(window);
    if (area.scale <= 0)
    {
        return {-1, -1};
    }
    // Win32's signed-coordinate macros also handle captured movement outside
    // the client. Letterbox presses stay outside instead of snapping to an edge.
    const int x = GET_X_LPARAM(position);
    const int y = GET_Y_LPARAM(position);
    return {logicalAxis(x, area.left, area.scale, kWidth),
            logicalAxis(y, area.top, area.scale, kHeight)};
}

// A square client produces a 3x centered image with real letterboxing. Verify
// both inclusive first/last pixels and the outside coordinates used by capture.
static_assert(presentationForSize(400, 400).scale == 3 && presentationForSize(400, 400).left == 8 &&
              presentationForSize(400, 400).top == 56);
static_assert(logicalAxis(7, 8, 3, kWidth) == -1 && logicalAxis(8, 8, 3, kWidth) == 0 &&
              logicalAxis(391, 8, 3, kWidth) == 127 && logicalAxis(392, 8, 3, kWidth) == 128);
static_assert(presentationForSize(0, 0).scale == 0 && logicalAxis(0, 0, 0, kWidth) == -1);

// One static typed owner holds the portable product, native framebuffer, and
// host-only GDI conversion storage. No hardware bus, touch controller, or MCU
// initialization is implemented by this simulator.
class Host final : public lithe::Display
{
public:
    bool initialize() noexcept
    {
        mBitmap.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        mBitmap.bmiHeader.biWidth = kWidth;
        mBitmap.bmiHeader.biHeight = -kHeight; // Top-down DIB matches logical origin.
        mBitmap.bmiHeader.biPlanes = 1;
        mBitmap.bmiHeader.biBitCount = 32;
        mBitmap.bmiHeader.biCompression = BI_RGB;
        return mProduct.initialize() && mProduct.render(*this);
    }

    lithe::Surface surface() noexcept override
    {
        return {mFrame, kDisplay.format, kWidth, kHeight, kStrideBytes};
    }

    void present([[maybe_unused]] std::span<const lithe::Rect> damage) noexcept override
    {
        std::size_t offset = 0U;
        for (auto& pixel : mPresentation)
        {
            const auto color =
                lithe::PixelFormatTraits<lithe::PixelFormat::rgb565>::read(mFrame, offset);
            pixel = {color.blue, color.green, color.red, 255U};
            offset += 2U;
        }
    }

    [[nodiscard]] bool failed() const noexcept
    {
        return mFailed;
    }

    LRESULT message(HWND window, UINT message, WPARAM key, LPARAM parameter) noexcept
    {
        switch (message)
        {
        case WM_PAINT:
            paint(window);
            return 0;
        case WM_ERASEBKGND:
            return 1;
        case WM_SIZE:
            InvalidateRect(window, nullptr, FALSE);
            return 0;
        case WM_GETMINMAXINFO:
        {
            RECT minimum{0, 0, kWidth, kHeight};
            if (!AdjustWindowRect(&minimum, WS_OVERLAPPEDWINDOW, FALSE))
            {
                fail(window, L"Cannot calculate the minimum simulator window size.");
                return 0;
            }
            // LITHEVIEW_CAST_BOUNDARY: WM_GETMINMAXINFO defines LPARAM as MINMAXINFO*.
            // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast,performance-no-int-to-ptr)
            auto* limits = reinterpret_cast<MINMAXINFO*>(parameter);
            limits->ptMinTrackSize = {minimum.right - minimum.left, minimum.bottom - minimum.top};
            return 0;
        }
        case WM_KEYDOWN:
            switch (key)
            {
            case VK_UP:
            case VK_LEFT:
                sendKey(window, lithe::Key::up);
                return 0;
            case VK_DOWN:
            case VK_RIGHT:
                sendKey(window, lithe::Key::down);
                return 0;
            case VK_RETURN:
            case VK_SPACE:
                sendKey(window, lithe::Key::select);
                return 0;
            default:
                break;
            }
            break;
        case WM_CHAR:
            return 0; // This product declares no logical text input.
        case WM_MOUSEWHEEL:
            mWheelRemainder += GET_WHEEL_DELTA_WPARAM(key);
            while (mWheelRemainder >= WHEEL_DELTA)
            {
                mWheelRemainder -= WHEEL_DELTA;
                sendKey(window, lithe::Key::up);
            }
            while (mWheelRemainder <= -WHEEL_DELTA)
            {
                mWheelRemainder += WHEEL_DELTA;
                sendKey(window, lithe::Key::down);
            }
            return 0;
        case WM_LBUTTONDOWN:
            if (!mPointerDown)
            {
                SetFocus(window);
                mLastPointer = logicalPointer(window, parameter);
                if (mProduct.handlePointer(
                        {mLastPointer, lithe::PointerPhase::pressed, 0U, GetTickCount()}))
                {
                    mPointerDown = true;
                    SetCapture(window);
                    if (GetCapture() != window)
                    {
                        cancelPointer(window);
                        fail(window, L"Cannot capture the simulator pointer.");
                    }
                    refresh(window);
                }
            }
            return 0;
        case WM_MOUSEMOVE:
            if (mPointerDown)
            {
                mLastPointer = logicalPointer(window, parameter);
                mProduct.handlePointer(
                    {mLastPointer, lithe::PointerPhase::moved, 0U, GetTickCount()});
                refresh(window);
            }
            return 0;
        case WM_LBUTTONUP:
            if (mPointerDown)
            {
                mPointerDown = false;
                mLastPointer = logicalPointer(window, parameter);
                mProduct.handlePointer(
                    {mLastPointer, lithe::PointerPhase::released, 0U, GetTickCount()});
                if (GetCapture() == window)
                {
                    ReleaseCapture();
                }
                refresh(window);
            }
            return 0;
        case WM_CANCELMODE:
        case WM_KILLFOCUS:
            cancelPointer(window);
            if (GetCapture() == window)
            {
                ReleaseCapture();
            }
            return 0;
        case WM_CAPTURECHANGED:
            cancelPointer(window);
            return 0;
        case WM_CLOSE:
            DestroyWindow(window);
            return 0;
        case WM_DESTROY:
            PostQuitMessage(mFailed ? 1 : 0);
            return 0;
        default:
            break;
        }
        return DefWindowProcW(window, message, key, parameter);
    }

private:
    void fail(HWND window, const wchar_t* reason) noexcept
    {
        if (!mFailed)
        {
            mFailed = true;
            MessageBoxW(window, reason, L"LitheView simulator failure", MB_OK | MB_ICONERROR);
            PostQuitMessage(1);
        }
    }

    void refresh(HWND window) noexcept
    {
        if (!mFailed && (!mProduct.render(*this) || !InvalidateRect(window, nullptr, FALSE)))
        {
            fail(window, L"Cannot render the simulator frame.");
        }
    }

    void sendKey(HWND window, lithe::Key key) noexcept
    {
        if (mProduct.handleKey(key))
        {
            refresh(window);
        }
    }

    void cancelPointer(HWND window) noexcept
    {
        if (mPointerDown)
        {
            mPointerDown = false;
            mProduct.handlePointer(
                {mLastPointer, lithe::PointerPhase::cancelled, 0U, GetTickCount()});
            refresh(window);
        }
    }

    void paint(HWND window) noexcept
    {
        PAINTSTRUCT paintState{};
        HDC device = BeginPaint(window, &paintState);
        if (device == nullptr)
        {
            fail(window, L"Cannot begin simulator painting.");
            return;
        }
        RECT client{};
        bool painted = GetClientRect(window, &client) != FALSE;
        if (painted && client.right > 0 && client.bottom > 0)
        {
            painted = PatBlt(device, 0, 0, client.right, client.bottom, BLACKNESS) != FALSE;
            const Presentation area = presentation(window);
            if (painted && area.scale > 0)
            {
                // Typed pixels stay borrowed until the actual GDI C boundary.
                painted = StretchDIBits(device,
                                        area.left,
                                        area.top,
                                        kWidth * area.scale,
                                        kHeight * area.scale,
                                        0,
                                        0,
                                        kWidth,
                                        kHeight,
                                        std::span{mPresentation}.data(),
                                        &mBitmap,
                                        DIB_RGB_COLORS,
                                        SRCCOPY) > 0;
            }
        }
        const bool ended = EndPaint(window, &paintState) != FALSE;
        if (!painted || !ended)
        {
            fail(window, L"Cannot present the simulator frame.");
        }
    }

    Product mProduct{};
    std::array<std::byte, kFrameBytes> mFrame{};
    std::array<lithe::Xrgb8888Pixel, kPixelCount> mPresentation{};
    BITMAPINFO mBitmap{};
    lithe::Point mLastPointer{};
    int mWheelRemainder{0};
    bool mPointerDown{false};
    bool mFailed{false};
};

Host& host() noexcept
{
    static Host instance{};
    return instance;
}

LRESULT CALLBACK windowProcedure(HWND window, UINT message, WPARAM key, LPARAM parameter) noexcept
{
    return host().message(window, message, key, parameter);
}

int startupFailure(const wchar_t* reason) noexcept
{
    MessageBoxW(nullptr, reason, L"LitheView simulator failure", MB_OK | MB_ICONERROR);
    return 1;
}

} // namespace

int WINAPI WinMain(HINSTANCE instance,
                   [[maybe_unused]] HINSTANCE previous,
                   [[maybe_unused]] LPSTR commandLine,
                   int showCommand)
{
    if (!host().initialize())
    {
        return startupFailure(L"Cannot initialize the portable product.");
    }
    WNDCLASSW windowClass{};
    windowClass.style = CS_HREDRAW | CS_VREDRAW;
    windowClass.lpfnWndProc = windowProcedure;
    windowClass.hInstance = instance;
    windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    windowClass.lpszClassName = std::span{kClassName}.data();
    if (windowClass.hCursor == nullptr || RegisterClassW(&windowClass) == 0)
    {
        return startupFailure(L"Cannot register the simulator window.");
    }
    RECT desired{0, 0, kWidth * kInitialScale, kHeight * kInitialScale};
    if (!AdjustWindowRect(&desired, WS_OVERLAPPEDWINDOW, FALSE))
    {
        return startupFailure(L"Cannot calculate the simulator window size.");
    }
    HWND window = CreateWindowExW(0,
                                  std::span{kClassName}.data(),
                                  std::span{kTitle}.data(),
                                  WS_OVERLAPPEDWINDOW,
                                  CW_USEDEFAULT,
                                  CW_USEDEFAULT,
                                  desired.right - desired.left,
                                  desired.bottom - desired.top,
                                  nullptr,
                                  nullptr,
                                  instance,
                                  nullptr);
    if (window == nullptr)
    {
        return startupFailure(L"Cannot create the simulator window.");
    }
    ShowWindow(window, showCommand);
    UpdateWindow(window);
    MSG message{};
    int result = 0;
    while ((result = GetMessageW(&message, nullptr, 0, 0)) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    if (result < 0)
    {
        return startupFailure(L"Cannot read simulator window messages.");
    }
    return host().failed() ? 1 : 0;
}
