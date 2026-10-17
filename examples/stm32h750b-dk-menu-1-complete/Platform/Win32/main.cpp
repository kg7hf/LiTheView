// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/app_main.h"
#include "Application/status.hpp"
#include "Devices/display.hpp"
#include "Devices/indicator.hpp"
#include "FreeRTOS.h"
#include "Platform/Win32/window.hpp"
#include "Platform/Win32/front_panel.hpp"
#include "front_panel_config.hpp"
#include "cmsis_os2.h"
#include "task.h"

#include <array>
#include <cstdio>
#include <string_view>
#include <windowsx.h>

extern "C"
{
    DWORD starterInterruptThreadId{};
    BaseType_t starterYieldRequested{};
}
namespace
{
StaticTask_t taskControl;
alignas(8) std::array<StackType_t, 2048U> taskStack;
constexpr auto windowClass = L"LiTheViewH750Starter1";

[[noreturn]] void finishProcess(UINT result) noexcept
{
    // The Win32 RTOS owns process-lifetime threads. Terminate them before any
    // static display/form destruction can race an application task still running.
    std::fflush(stdout);
    std::fflush(stderr);
    ExitProcess(result);
}

// C callbacks are the only erased signatures. No application object is hidden
// in void*: the one application entry point is known at compile time.
void taskEntry(void*)
{
    appMain();
}
DWORD WINAPI schedulerThread(void*)
{
    starterInterruptThreadId = GetCurrentThreadId();
    const osThreadAttr_t attributes{.name = "defaultTask",
                                    .attr_bits = 0U,
                                    .cb_mem = &taskControl,
                                    .cb_size = sizeof(taskControl),
                                    .stack_mem = taskStack.data(),
                                    .stack_size = sizeof(taskStack),
                                    .priority = osPriorityBelowNormal,
                                    .tz_module = 0U,
                                    .affinity_mask = 0U};
    if (osKernelInitialize() != osOK || osThreadNew(taskEntry, nullptr, &attributes) == nullptr ||
        osKernelStart() != osOK)
    {
        starterAssert(__FILE__, __LINE__);
    }
    starterAssert(__FILE__, __LINE__);
    return 1U;
}
LRESULT CALLBACK windowProcedure(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_LBUTTONDOWN:
        SetCapture(window);
        starter::host::mouse(starter::host::MousePhase::pressed, GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
        return 0;
    case WM_MOUSEMOVE:
        if (GetCapture() == window)
        {
            starter::host::mouse(starter::host::MousePhase::moved, GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
        }
        return 0;
    case WM_LBUTTONUP:
        if (GetCapture() == window)
        {
            starter::host::mouse(starter::host::MousePhase::released, GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
            ReleaseCapture();
        }
        return 0;
    case WM_CAPTURECHANGED:
        starter::host::mouse(starter::host::MousePhase::cancelled, 0, 0);
        return 0;
    case WM_KEYDOWN:
        if ((lParam & (1L << 30)) != 0)
        {
            return 0;
        }
        switch (wParam)
        {
        case VK_RETURN:
        case VK_SPACE:
            starter::host::key(lithe::Key::select);
            break;
        case VK_ESCAPE:
        case VK_BACK:
            starter::host::key(lithe::Key::back);
            break;
        case VK_UP:
            starter::host::key(lithe::Key::up);
            break;
        case VK_DOWN:
            starter::host::key(lithe::Key::down);
            break;
        case VK_LEFT:
            starter::host::key(lithe::Key::left);
            break;
        case VK_RIGHT:
            starter::host::key(lithe::Key::right);
            break;
        default:
            break;
        }
        return 0;
    case WM_TIMER:
    {
        std::array<wchar_t, 120U> title{};
        // LITHEVIEW_CAST_BOUNDARY: explicit types required by the Windows CRT varargs format.
        const auto heartbeats = static_cast<unsigned long>(starter::status.heartbeats.load());
        const auto frames = static_cast<unsigned long>(starter::status.frames.load());
        swprintf_s(title.data(),
                   title.size(),
                   L"H750 menu 1 | task ticks: %lu | frames: %lu",
                   heartbeats,
                   frames);
        SetWindowTextW(window, title.data());
        InvalidateRect(window, nullptr, FALSE);
        return 0;
    }
    case WM_PAINT:
    {
        PAINTSTRUCT paint{};
        const auto context = BeginPaint(window, &paint);
        if (!starter::host::paintWindow(context))
        {
            finishProcess(1U);
        }
        EndPaint(window, &paint);
        return 0;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    default:
        return DefWindowProcW(window, message, wParam, lParam);
    }
}
} // namespace
namespace starter
{
void setHeartbeat(bool) noexcept
{
} // The title exposes the simulated device's task ticks.
} // namespace starter
extern "C" void starterAssert(const char* file, std::uint32_t line)
{
    starter::status.failureLine.store(line);
    // LITHEVIEW_CAST_BOUNDARY: the CRT varargs format expects unsigned long.
    std::fprintf(stderr, "FAIL: %s:%lu\n", file, static_cast<unsigned long>(line));
    ExitProcess(1U);
}
int WINAPI WinMain(HINSTANCE instance, HINSTANCE, LPSTR commandLine, int show)
{
    if (!starter::host::initializeFrontPanel())
    {
        finishProcess(1U);
    }
    const auto thread = CreateThread(nullptr, 0U, schedulerThread, nullptr, 0U, nullptr);
    if (thread == nullptr)
    {
        return 1;
    }
    CloseHandle(thread); // Kernel threads have process lifetime; WM_CLOSE terminates the simulator.
    const bool frontPanelTest = std::string_view{commandLine} == "--front-panel-self-test";
    const bool interactionTest = std::string_view{commandLine} == "--interaction-self-test";
    if (std::string_view{commandLine} == "--self-test" || interactionTest || frontPanelTest)
    {
        const auto started = GetTickCount64();
        while (starter::status.heartbeats.load() < 4U && GetTickCount64() - started < 10000U)
        {
            Sleep(10U);
        }
        bool passed = starter::status.ready.load() && starter::status.heartbeats.load() >= 4U &&
                      starter::status.failureLine.load() == 0U && starter::host::checkDisplay();
        if (passed && frontPanelTest)
        {
            using namespace starter::host;
            const auto lcd = front::transform.toWindow(front::display);
            const auto x = lcd.x + 160 * lcd.width / 480;
            const auto y = lcd.y + 224 * lcd.height / 272;
            const auto initial = frameFingerprint();
            mouse(MousePhase::pressed, x, y);
            Sleep(50U);
            mouse(MousePhase::released, x, y);
            Sleep(80U);
            const auto settings = frameFingerprint();
            passed = passed && settings != initial;
            key(lithe::Key::back);
            Sleep(80U);
            const auto home = frameFingerprint();
            passed = passed && home != settings;
            for (const auto& button : front::buttons)
            {
                if (button.id != "userButton")
                    continue;
                const auto area = front::transform.toWindow(button.bounds);
                mouse(MousePhase::pressed, area.x + area.width / 2, area.y + area.height / 2);
                Sleep(120U);
                mouse(MousePhase::released, area.x + area.width / 2, area.y + area.height / 2);
                Sleep(80U);
                passed = passed && frameFingerprint() == settings;
                key(lithe::Key::back);
                Sleep(80U);
            }
            const auto beforeCancel = frameFingerprint();
            mouse(MousePhase::pressed, x, y);
            mouse(MousePhase::moved, 0, 0);
            mouse(MousePhase::released, 0, 0);
            Sleep(80U);
            passed = passed && frameFingerprint() == beforeCancel;
            std::printf("%s: H750 photo mapping, touch, blue-button activation and cancellation\n", passed ? "PASS" : "FAIL");
        }
        if (passed && interactionTest)
        {
            const auto original = starter::host::frameFingerprint();
            const auto click = [](int row)
            {
                starter::host::pointer(160, 104 + row * 60, true);
                Sleep(50U);
                starter::host::pointer(160, 104 + row * 60, false);
                Sleep(80U);
                return starter::host::frameFingerprint();
            };
            const auto playing = click(0);
            const auto paused = click(0);
            const auto resumed = click(0);
            starter::host::key(lithe::Key::right); // Stop follows Play in the form.
            starter::host::key(lithe::Key::select);
            Sleep(80U);
            passed = passed && playing != original && paused != playing && resumed == playing &&
                     starter::host::frameFingerprint() != resumed;
            const auto settings = click(2);
            const auto volume = click(0);
            starter::host::key(lithe::Key::right);
            Sleep(80U);
            const auto louder = starter::host::frameFingerprint();
            starter::host::key(lithe::Key::left);
            Sleep(80U);
            passed = passed && volume != settings && louder != volume &&
                     starter::host::frameFingerprint() == volume;
            starter::host::key(lithe::Key::back);
            Sleep(80U);
            const auto storage = click(1);
            const auto loadPage = click(1);
            starter::host::pointer(160, 132, true);
            Sleep(50U);
            starter::host::pointer(160, 132, false);
            Sleep(80U);
            const auto selected = starter::host::frameFingerprint();
            starter::host::pointer(165, 230, true);
            Sleep(50U);
            starter::host::pointer(165, 230, false);
            Sleep(80U);
            passed = passed && loadPage != storage && selected != loadPage &&
                     starter::host::frameFingerprint() != selected;
            starter::host::key(lithe::Key::back);
            Sleep(80U);
            const auto settingsAgain = click(2);
            const auto homeAgain = click(2);
            passed = passed && original != settings && settings != storage &&
                     storage != settingsAgain && settingsAgain != homeAgain;
            starter::host::key(lithe::Key::select);
            Sleep(80U);
            const auto keyboardSettings = starter::host::frameFingerprint();
            starter::host::key(lithe::Key::back);
            Sleep(80U);
            passed = passed && keyboardSettings != homeAgain &&
                     starter::host::frameFingerprint() == homeAgain;
            std::printf("%s: mouse -> application task -> generated handlers -> "
                        "Home/Settings/Storage/Settings/Home "
                        "pixels\n",
                        passed ? "PASS" : "FAIL");
        }
        // LITHEVIEW_CAST_BOUNDARY: explicit CRT varargs argument types.
        const auto heartbeats = static_cast<unsigned long>(starter::status.heartbeats.load());
        const auto frames = static_cast<unsigned long>(starter::status.frames.load());
        std::printf("%s: one shared RTOS application task; heartbeats=%lu frames=%lu\n",
                    passed ? "PASS" : "FAIL",
                    heartbeats,
                    frames);
        finishProcess(passed ? 0U : 1U);
    }
    WNDCLASSW definition{};
    definition.lpfnWndProc = windowProcedure;
    definition.hInstance = instance;
    definition.lpszClassName = windowClass;
    definition.hCursor = LoadCursor(nullptr, IDC_ARROW);
    if (RegisterClassW(&definition) == 0U)
    {
        finishProcess(1U);
    }
    constexpr DWORD style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
    const auto area = starter::host::windowArea();
    RECT bounds{0, 0, area.width, area.height};
    AdjustWindowRect(&bounds, style, FALSE);
    const auto window = CreateWindowW(windowClass,
                                      L"H750 menu 1",
                                      style,
                                      CW_USEDEFAULT,
                                      CW_USEDEFAULT,
                                      bounds.right - bounds.left,
                                      bounds.bottom - bounds.top,
                                      nullptr,
                                      nullptr,
                                      instance,
                                      nullptr);
    if (window == nullptr || SetTimer(window, 1U, 100U, nullptr) == 0U)
    {
        finishProcess(1U);
    }
    ShowWindow(window, show);
    MSG message{};
    for (;;)
    {
        const auto result = GetMessageW(&message, nullptr, 0U, 0U);
        if (result <= 0)
        {
            finishProcess(result < 0 || message.wParam != 0U ? 1U : 0U);
        }
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
}
