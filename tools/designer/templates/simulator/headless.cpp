// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "application.hpp"
#include "runtime.hpp"

#include <algorithm>
#include <array>
#include <cstdio>
#include <span>
#include <string_view>

namespace
{
using namespace lithe::simulator;
using HostProduct = Product<ApplicationHooks>;

class FrameDisplay final : public lithe::Display
{
public:
    explicit FrameDisplay(std::span<std::byte, kFrameBytes> storage) noexcept : mStorage(storage)
    {
    }
    lithe::Surface surface() noexcept override
    {
        return {mStorage, kDisplay.format, kWidth, kHeight, kStrideBytes};
    }
    void present([[maybe_unused]] std::span<const lithe::Rect> damage) noexcept override
    {
    }

private:
    std::span<std::byte, kFrameBytes> mStorage;
};

class ComparingSink final : public lithe::TileSink
{
public:
    explicit ComparingSink(std::span<const std::byte, kFrameBytes> expected,
                           bool rejectWrite = false) noexcept
        : mExpected(expected), mRejectWrite(rejectWrite)
    {
    }
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        if (mStarted || size.width != kWidth || size.height != kHeight || format != kDisplay.format)
        {
            return false;
        }
        mStarted = true;
        return true;
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        const auto rows = std::min(kTileHeight, kHeight - mNextRow);
        const auto layout = pixels.layout();
        if (!mStarted || mEnded || mNextRow >= kHeight || destination.x != 0 ||
            destination.y != mNextRow || destination.width != kWidth ||
            destination.height != rows || pixels.width != kWidth || pixels.height != rows ||
            pixels.format != kDisplay.format || pixels.strideBytes != kStrideBytes || !layout ||
            pixels.data.size() != layout->usedBytes ||
            pixels.data.size() > mExpected.size() - mBytes || mRejectWrite)
        {
            return false;
        }
        // Borrowed bytes are consumed before return; no asynchronous work retains the span.
        if (!std::ranges::equal(pixels.data, mExpected.subspan(mBytes, pixels.data.size())))
        {
            return false;
        }
        mBytes += pixels.data.size();
        mNextRow += rows;
        return true;
    }
    bool endFrame(bool complete) noexcept override
    {
        const bool valid = mStarted && !mEnded;
        mEnded = true;
        mAborted = !complete;
        mComplete = valid && complete && mNextRow == kHeight && mBytes == kFrameBytes;
        return valid && (!complete || mComplete);
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
    bool mRejectWrite{false};
    bool mStarted{false};
    bool mEnded{false};
    bool mComplete{false};
    bool mAborted{false};
};

bool writePpm(const char* path, std::span<const std::byte, kFrameBytes> pixels) noexcept
{
    auto* file = std::fopen(path, "wb");
    if (file == nullptr)
    {
        return false;
    }
    bool written = std::fprintf(file, "P6\n%zu %zu\n255\n", kWidthCount, kHeightCount) > 0;
    for (std::size_t offset = 0U; written && offset < pixels.size(); offset += kPixelBytes)
    {
        const auto color = lithe::PixelFormatTraits<kDisplay.format>::read(pixels, offset);
        written = std::fputc(color.red, file) != EOF && std::fputc(color.green, file) != EOF &&
                  std::fputc(color.blue, file) != EOF;
    }
    const bool closed = std::fclose(file) == 0;
    return written && closed;
}

bool compareFrame(HostProduct& full,
                  HostProduct& tiled,
                  FrameDisplay& display,
                  std::span<const std::byte, kFrameBytes> pixels,
                  lithe::Surface workspace,
                  std::string_view step) noexcept
{
    if (!full.render(display))
    {
        return false;
    }
    ComparingSink sink{pixels};
    const auto result = tiled.renderTiled(workspace, sink);
    const bool equal = result && sink.complete() && result.tileCount == kTileCount &&
                       result.pixelsTransferred == kPixelCount &&
                       full.lastAction() == tiled.lastAction() &&
                       full.actionCount() == tiled.actionCount();
    std::printf("%s: %s; action=%u; action-count=%zu\n",
                equal ? "PASS" : "FAIL",
                step.data(),
                full.lastAction(),
                full.actionCount());
    return equal;
}

int fail(const char* reason) noexcept
{
    std::puts(reason);
    return 1;
}
} // namespace

int main()
{
    // Separate static owners make replay and rendering-strategy agreement testable.
    static ApplicationHooks fullHooks{};
    static ApplicationHooks tiledHooks{};
    static HostProduct full{fullHooks};
    static HostProduct tiled{tiledHooks};
    static std::array<std::byte, kFrameBytes> pixels{};
    static std::array<std::byte, kTileBytes> strip{};
    FrameDisplay display{pixels};
    const lithe::Surface workspace{strip, kDisplay.format, kWidth, kTileHeight, kStrideBytes};
    const auto compare = [&](std::string_view step) noexcept
    {
        return compareFrame(full, tiled, display, pixels, workspace, step);
    };
    if (!full.initialize() || !tiled.initialize() || !compare("initial") ||
        !writePpm("initial.ppm", pixels))
    {
        return fail("FAIL: initialize or initial capture");
    }

    struct KeyStep
    {
        std::string_view name;
        lithe::Key key;
    };
    constexpr std::array replay{KeyStep{"select", lithe::Key::select},
                                KeyStep{"next", lithe::Key::down},
                                KeyStep{"select-next", lithe::Key::select},
                                KeyStep{"previous", lithe::Key::up}};
    for (const auto step : replay)
    {
        if (full.handleKey(step.key) != tiled.handleKey(step.key) || !compare(step.name))
        {
            return fail("FAIL: logical key replay");
        }
    }
    if (const auto point = full.focusedPoint(); kInput.pointer && point)
    {
        const std::array pointerReplay{
            lithe::PointerEvent{*point, lithe::PointerPhase::pressed, 1U, 100U},
            lithe::PointerEvent{*point, lithe::PointerPhase::released, 1U, 140U}};
        for (const auto event : pointerReplay)
        {
            if (full.handlePointer(event) != tiled.handlePointer(event) || !compare("pointer"))
            {
                return fail("FAIL: logical pointer replay");
            }
        }
    }
    if (full.handleTextInput(U'A') != tiled.handleTextInput(U'A') || !compare("text-A") ||
        full.advanceTime(125U) != tiled.advanceTime(125U) || !compare("elapsed-125ms") ||
        !writePpm("replayed.ppm", pixels))
    {
        return fail("FAIL: logical text/time replay or replayed capture");
    }

    ComparingSink rejected{pixels, true};
    const auto result = tiled.renderTiled(workspace, rejected);
    if (result.error != lithe::TileRenderError::sinkWriteFailed || result.tileCount != 0U ||
        result.pixelsTransferred != 0U || !rejected.aborted() || rejected.complete() ||
        !compare("sink-retry"))
    {
        return fail("FAIL: rejected sink and complete retry");
    }
    std::printf("PASS: full-frame=%zu; workspace=%zu; mutable-text=%zu; tiles=%zu\n",
                kFrameBytes,
                kTileBytes,
                kTextBytes,
                kTileCount);
    std::puts(
        "Wrote initial.ppm and replayed.ppm. Application behavior belongs in application.hpp.");
    return 0;
}
