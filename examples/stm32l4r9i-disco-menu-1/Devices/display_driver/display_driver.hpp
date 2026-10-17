// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstddef>
#include <cstdint>
#include <expected>
#include <span>

namespace devices::display_driver
{

inline constexpr std::uint16_t kWidth{390U};
inline constexpr std::uint16_t kHeight{390U};
inline constexpr std::size_t kPhysicalFramebufferBytes{365040U};
inline constexpr std::size_t kVirtualStrideBytes{3072U};

// Select from the marking on the main board, never by probing power pins.
enum class BoardRevision : std::uint32_t
{
    a = 1U,
    b = 2U,
    c = 3U
};

enum class Error
{
    notInitialized,
    unsupportedRevision,
    invalidTile,
    invalidState,
    busy,
    timeout,
    halFailure,
    boardIoFailure
};

struct Rgb888Tile
{
    std::span<const std::byte> data{};
    std::uint16_t x{0U};
    std::uint16_t y{0U};
    std::uint16_t width{0U};
    std::uint16_t height{0U};
    std::size_t strideBytes{0U};
};

// Call from one foreground context with interrupts and the HAL 1-ms tick active.
// Board owns SYSCLK=120 MHz from MSI=4 MHz. Driver owns LTDC/DSI/GFXMMU/DMA2D
// and their interrupts. Reinitialization after failure requires an MCU reset.
[[nodiscard]] std::expected<void, Error> initialize(BoardRevision revision) noexcept;
[[nodiscard]] std::expected<bool, Error> pollReady() noexcept;
[[nodiscard]] std::expected<void, Error> beginFrame() noexcept;

// Input bytes are R,G,B. The DMA2D boundary converts to LTDC B,G,R storage.
// Stride must be a whole number of pixels, at most 390 pixels. Copy completes
// before return, including failure cleanup; no source span is retained.
[[nodiscard]] std::expected<void, Error> copyTile(const Rgb888Tile& tile) noexcept;

// Submit the current framebuffer asynchronously. Do not begin another frame
// until pollReady() returns true. A missing completion becomes a sticky error.
[[nodiscard]] std::expected<void, Error> refresh() noexcept;

// Discard an unfinished drawing transaction. Already-copied rectangles remain
// in backing memory; the next frame must redraw them before submission.
void abortFrame() noexcept;

} // namespace devices::display_driver
