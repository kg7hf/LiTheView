// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "demo.hpp"
#include "round_demo.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <span>

namespace lithe::stm32u545
{

namespace probe_detail
{

inline constexpr std::size_t kLargest32BitPointerBytes{4U};
inline constexpr std::size_t kStandardStripBytes{3840U};
inline constexpr std::size_t kStandardObjectBudgetBytes{16000U};
inline constexpr std::size_t kRoundStripBytes{1920U};
inline constexpr std::size_t kRoundObjectBudgetBytes{12000U};

} // namespace probe_detail

enum class ProbeError : std::uint8_t
{
    none,
    initializationFailed,
    renderFailed
};

struct ProbeResult
{
    ProbeError error{ProbeError::none};
    std::uint32_t frameCrc32{0U};
    std::size_t tileCount{0U};
    std::size_t pixelsRendered{0U};
    std::size_t nativeBytesProduced{0U};
    std::size_t objectBytes{0U};

    constexpr bool ok() const noexcept
    {
        return error == ProbeError::none;
    }
    constexpr explicit operator bool() const noexcept
    {
        return ok();
    }
};

// Displayless bring-up harness for NUCLEO-U545RE-Q. The real demo is rendered
// through four-row RGB565 strips, so no full framebuffer is required.
class GraphicsProbe final
{
public:
    static constexpr std::int32_t kStripHeight{4};
    static constexpr std::size_t kStripPixels{lithe::detail::nonnegativeSize(demo::Demo::kWidth) *
                                              lithe::detail::nonnegativeSize(kStripHeight)};
    static constexpr std::size_t kStripBytes{kStripPixels * sizeof(Pixel)};

    GraphicsProbe() noexcept;

    ProbeResult renderFrame() noexcept;
    bool handleKey(Key key) noexcept
    {
        return mInitialized && mDemo.handleKey(key);
    }
    bool handlePointer(PointerEvent event) noexcept
    {
        return mInitialized && mDemo.handlePointer(event);
    }

private:
    demo::Demo mDemo;
    std::array<Pixel, kStripPixels> mStrip{};
    bool mInitialized{false};
};

// Customer-oriented 240x240 round-panel probe. It exercises the same circular
// viewport, gesture/scene state machines, and RGB565 tile stream as the Win32
// round simulator without claiming that NUCLEO-U545RE-Q has an attached panel.
class RoundGraphicsProbe final
{
public:
    static constexpr std::int32_t kStripHeight{4};
    static constexpr std::size_t kStripPixels{
        lithe::detail::nonnegativeSize(demo::RoundDemo::kWidth) *
        lithe::detail::nonnegativeSize(kStripHeight)};
    static constexpr std::size_t kStripBytes{kStripPixels * sizeof(Pixel)};

    RoundGraphicsProbe() noexcept;

    ProbeResult renderFrame() noexcept;
    bool handlePointer(PointerEvent event) noexcept
    {
        return mInitialized && mDemo.handlePointer(event);
    }
    bool advanceTime(std::uint32_t elapsedMs) noexcept
    {
        return mInitialized && mDemo.advanceTime(elapsedMs);
    }

private:
    demo::RoundDemo mDemo;
    std::array<Pixel, kStripPixels> mStrip{};
    bool mInitialized{false};
};

static_assert(GraphicsProbe::kStripBytes == probe_detail::kStandardStripBytes,
              "The U545 probe strip budget changed unexpectedly");
static_assert(sizeof(void*) > probe_detail::kLargest32BitPointerBytes ||
                  sizeof(GraphicsProbe) <= probe_detail::kStandardObjectBudgetBytes,
              "The complete U545 demo/probe exceeded its 16 KiB static-object budget");
static_assert(RoundGraphicsProbe::kStripBytes == probe_detail::kRoundStripBytes,
              "The U545 round-panel probe strip budget changed unexpectedly");
static_assert(sizeof(void*) > probe_detail::kLargest32BitPointerBytes ||
                  sizeof(RoundGraphicsProbe) <= probe_detail::kRoundObjectBudgetBytes,
              "The complete U545 round-panel probe exceeded its 12 KiB static-object budget");
} // namespace lithe::stm32u545
