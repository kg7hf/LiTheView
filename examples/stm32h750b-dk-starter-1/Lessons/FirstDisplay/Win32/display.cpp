// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Lessons/FirstDisplay/display.hpp"

#include "Application/status.hpp"
#include "Platform/Win32/window.hpp"

#include <algorithm>
#include <array>
#include <atomic>

namespace starter
{
namespace
{
using Frame = std::array<lithe::Rgb565Pixel, pixelCount>;
Frame renderBuffer;
// Single producer (application task), single consumer (window/test thread).
// Exchanging the middle slot transfers ownership; no Windows lock can be held
// by a task suspended by the FreeRTOS port. The persistent render buffer retains damage.
class Frames final
{
public:
    void publish(std::span<const lithe::Rgb565Pixel> pixels) noexcept
    {
        std::copy(pixels.begin(), pixels.end(), mBuffers[mBack].begin());
        mBack = mMiddle.exchange(mBack | dirty, std::memory_order_acq_rel) & indexMask;
    }
    std::span<const lithe::Rgb565Pixel> acquire() noexcept
    {
        if ((mMiddle.load(std::memory_order_acquire) & dirty) != 0U)
        {
            mFront = mMiddle.exchange(mFront, std::memory_order_acq_rel) & indexMask;
        }
        return mBuffers[mFront];
    }

private:
    static constexpr std::uint32_t dirty{4U};
    static constexpr std::uint32_t indexMask{3U};
    std::array<Frame, 3U> mBuffers{};
    std::atomic<std::uint32_t> mMiddle{1U};
    std::uint32_t mFront{0U};
    std::uint32_t mBack{2U};
};
Frames frames;
std::array<std::uint32_t, pixelCount> dibPixels; // Window thread only.
} // namespace
std::expected<void, ui::Error> Display::initialize() noexcept
{
    mPixels = renderBuffer;
    return {};
}
lithe::Surface Display::surface() noexcept
{
    return lithe::Surface{lithe::Rgb565Surface{mPixels, width, height, width}};
}
void Display::present(std::span<const lithe::Rect>) noexcept
{
    frames.publish(mPixels);
    status.frames.fetch_add(1U, std::memory_order_release);
}
namespace host
{
void paint(HDC context, const RECT& destination) noexcept
{
    const auto pixels = frames.acquire();
    std::transform(pixels.begin(),
                   pixels.end(),
                   dibPixels.begin(),
                   [](lithe::Rgb565Pixel pixel)
                   {
                       const std::uint32_t value = pixel;
                       const auto r = (value >> 11U) & 31U;
                       const auto g = (value >> 5U) & 63U;
                       const auto b = value & 31U;
                       return (((r << 3U) | (r >> 2U)) << 16U) | (((g << 2U) | (g >> 4U)) << 8U) |
                              ((b << 3U) | (b >> 2U));
                   });
    BITMAPINFO info{};
    info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    info.bmiHeader.biWidth = width;
    info.bmiHeader.biHeight = -height;
    info.bmiHeader.biPlanes = 1U;
    info.bmiHeader.biBitCount = 32U;
    info.bmiHeader.biCompression = BI_RGB;
    StretchDIBits(context, destination.left, destination.top,
                  destination.right - destination.left, destination.bottom - destination.top,
                  0, 0, width, height, dibPixels.data(), &info, DIB_RGB_COLORS, SRCCOPY);
}
std::uint32_t frameFingerprint() noexcept
{
    // Called by the window/test thread, which exclusively owns frame acquisition.
    std::uint32_t value{2166136261U};
    for (const auto pixel : frames.acquire())
    {
        value = (value ^ pixel) * 16777619U;
    }
    return value;
}
bool checkDisplay() noexcept
{
    // Application smoke check: the first frame reached the platform driver.
    // Layout colors and border widths are developer choices, not runtime invariants.
    return status.frames.load(std::memory_order_acquire) != 0U;
}
} // namespace host
} // namespace starter
