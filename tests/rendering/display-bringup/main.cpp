// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "product.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdio>
#include <span>

namespace
{

using namespace display_bringup;

class FrameDisplay final : public lithe::Display
{
public:
    explicit FrameDisplay(std::span<std::byte, kFrameBytes> storage) noexcept : mStorage(storage)
    {
    }

    lithe::Surface surface() noexcept override
    {
        return {mStorage, kDisplay.format, 128, 96, kStrideBytes};
    }

    void present([[maybe_unused]] std::span<const lithe::Rect> damage) noexcept override
    {
        // Host verification only: no panel or DMA operation is hidden here.
    }

private:
    std::span<std::byte, kFrameBytes> mStorage;
};

class ComparingSink final : public lithe::TileSink
{
public:
    explicit ComparingSink(std::span<const std::byte, kFrameBytes> expected,
                           bool failFirstWrite = false) noexcept
        : mExpected(expected), mFailFirstWrite(failFirstWrite)
    {
    }

    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        if (mStarted || size.width != 128 || size.height != 96 || format != kDisplay.format)
        {
            return false;
        }
        mStarted = true;
        return true;
    }

    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        if (!mStarted || mEnded || mNextRow >= 96 || destination.x != 0 ||
            destination.y != mNextRow || destination.width != 128 ||
            destination.height != kTileHeight || pixels.width != 128 ||
            pixels.height != kTileHeight || pixels.format != kDisplay.format ||
            pixels.strideBytes != kStrideBytes || pixels.data.size() != kTileBytes ||
            !pixels.valid() || mFailFirstWrite)
        {
            return false;
        }
        // Consume the borrowed span before returning. A synchronous panel sink
        // would submit these bytes here; this host sink only compares them.
        if (!std::ranges::equal(pixels.data, mExpected.subspan(mBytes, kTileBytes)))
        {
            return false;
        }
        mBytes += pixels.data.size();
        mNextRow += kTileHeight;
        return true;
    }

    bool endFrame(bool complete) noexcept override
    {
        const bool validEnd = mStarted && !mEnded;
        mEnded = true;
        mAborted = !complete;
        mComplete = validEnd && complete && mNextRow == 96 && mBytes == kFrameBytes;
        // A successfully accepted abort preserves the original write error.
        // Returning false here means cleanup itself failed, not just the frame.
        return validEnd && (!complete || mComplete);
    }

    [[nodiscard]] bool complete() const noexcept
    {
        return mComplete;
    }

    [[nodiscard]] bool aborted() const noexcept
    {
        return mAborted;
    }

private:
    std::span<const std::byte, kFrameBytes> mExpected;
    std::size_t mBytes{0U};
    std::int32_t mNextRow{0};
    bool mFailFirstWrite{false};
    bool mStarted{false};
    bool mEnded{false};
    bool mComplete{false};
    bool mAborted{false};
};

bool writePpm(const char* path, std::span<const std::byte, kFrameBytes> pixels) noexcept
{
    std::FILE* file = std::fopen(path, "wb");
    if (file == nullptr)
    {
        return false;
    }
    bool written = std::fputs("P6\n128 96\n255\n", file) >= 0;
    for (std::size_t offset = 0U; written && offset < pixels.size(); offset += 2U)
    {
        // Independent host RGB565 decode; the native byte order is little-endian.
        const int pixel =
            std::to_integer<int>(pixels[offset]) | (std::to_integer<int>(pixels[offset + 1U]) << 8);
        written = std::fputc(((pixel >> 11) & 31) * 255 / 31, file) != EOF &&
                  std::fputc(((pixel >> 5) & 63) * 255 / 63, file) != EOF &&
                  std::fputc((pixel & 31) * 255 / 31, file) != EOF;
    }
    const bool closed = std::fclose(file) == 0;
    return written && closed;
}

bool pixelEquals(std::span<const std::byte, kFrameBytes> pixels,
                 std::size_t x,
                 std::size_t y,
                 std::byte low,
                 std::byte high) noexcept
{
    if (x >= 128U || y >= 96U)
    {
        return false;
    }
    const std::size_t offset = y * kStrideBytes + x * 2U;
    return pixels[offset] == low && pixels[offset + 1U] == high;
}

bool compareFrame(Product& full,
                  Product& tiled,
                  FrameDisplay& display,
                  std::span<const std::byte, kFrameBytes> pixels,
                  lithe::Surface workspace) noexcept
{
    if (!full.render(display))
    {
        return false;
    }
    ComparingSink sink{pixels};
    const auto result = tiled.renderTiled(workspace, sink);
    return result && sink.complete() && result.tileCount == 24U &&
           result.pixelsTransferred == 12288U && full.activated() == tiled.activated();
}

int fail(const char* message) noexcept
{
    std::puts(message);
    return 1;
}

} // namespace

int main()
{
    // Static caller storage makes the example's lifetime and RAM visible. The
    // tiled Product owns no framebuffer; the full frame is the host oracle only.
    static std::array<std::byte, kFrameBytes> pixels{};
    static std::array<std::byte, kTileBytes> strip{};
    static Product full{};
    static Product tiled{};
    FrameDisplay display{pixels};
    const lithe::Surface workspace{strip, kDisplay.format, 128, kTileHeight, kStrideBytes};
    if (!full.initialize() || !tiled.initialize() ||
        !compareFrame(full, tiled, display, pixels, workspace))
    {
        return fail("FAIL: initial full-frame/tiled rendering");
    }
    // Literal byte checks do not use the production pixel encoder as an oracle.
    if (!pixelEquals(pixels, 0U, 0U, std::byte{0xff}, std::byte{0xff}) ||
        !pixelEquals(pixels, 20U, 20U, std::byte{0x00}, std::byte{0xf8}) ||
        !pixelEquals(pixels, 60U, 20U, std::byte{0xe0}, std::byte{0x07}) ||
        !pixelEquals(pixels, 100U, 20U, std::byte{0x1f}, std::byte{0x00}) ||
        !writePpm("initial.ppm", pixels))
    {
        return fail("FAIL: independent RGB565 pattern or initial.ppm output");
    }
    if (!full.handleKey(lithe::Key::select) || !tiled.handleKey(lithe::Key::select) ||
        !full.activated() || !compareFrame(full, tiled, display, pixels, workspace) ||
        !writePpm("activated.ppm", pixels))
    {
        return fail("FAIL: logical select replay or activated.ppm output");
    }
    constexpr std::array replay{
        lithe::PointerEvent{{64, 78}, lithe::PointerPhase::pressed, 1U, 100U},
        lithe::PointerEvent{{64, 78}, lithe::PointerPhase::released, 1U, 140U}};
    for (const auto event : replay)
    {
        if (!full.handlePointer(event) || !tiled.handlePointer(event) ||
            !compareFrame(full, tiled, display, pixels, workspace))
        {
            return fail("FAIL: logical pointer replay");
        }
    }
    if (full.activated())
    {
        return fail("FAIL: pointer release did not deliver the reset action");
    }
    constexpr std::array cancelledReplay{
        lithe::PointerEvent{{64, 78}, lithe::PointerPhase::pressed, 2U, 200U},
        lithe::PointerEvent{{-1, -1}, lithe::PointerPhase::moved, 2U, 220U},
        lithe::PointerEvent{{-1, -1}, lithe::PointerPhase::released, 2U, 240U},
        lithe::PointerEvent{{64, 78}, lithe::PointerPhase::pressed, 3U, 300U},
        lithe::PointerEvent{{64, 78}, lithe::PointerPhase::cancelled, 3U, 320U}};
    for (const auto event : cancelledReplay)
    {
        if (!full.handlePointer(event) || !tiled.handlePointer(event) || full.activated() ||
            tiled.activated() || !compareFrame(full, tiled, display, pixels, workspace))
        {
            return fail("FAIL: outside release or lost-contact cancellation");
        }
    }
    ComparingSink failure{pixels, true};
    const auto rejected = tiled.renderTiled(workspace, failure);
    if (rejected.error != lithe::TileRenderError::sinkWriteFailed || rejected.tileCount != 0U ||
        rejected.pixelsTransferred != 0U || !failure.aborted() || failure.complete() ||
        !compareFrame(full, tiled, display, pixels, workspace))
    {
        return fail("FAIL: sink failure must be reported and a complete retry must succeed");
    }
    std::printf("PASS: RGB565 128x96; full-frame=%zu bytes; tile-workspace=%zu bytes; "
                "24 tiles/frame\n",
                pixels.size(),
                strip.size());
    std::puts(
        "PASS: initial/select/pointer/cancellation bytes match; rejected sink and retry checked");
    std::puts("Wrote initial.ppm and activated.ppm in the working directory");
    return 0;
}
