// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/application.hpp"

#include <cstddef>
#include <cstdint>
#include <span>

namespace lithe
{

// A sink must consume or copy the tile before writeTile() returns. This keeps
// ownership of the reusable render buffer with the caller.
class TileSink
{
public:
    TileSink() = default;
    virtual ~TileSink() = default;

    TileSink(const TileSink&) = delete;
    TileSink& operator=(const TileSink&) = delete;
    TileSink(TileSink&&) = delete;
    TileSink& operator=(TileSink&&) = delete;

    virtual bool beginFrame(Size frameSize, PixelFormat format) noexcept = 0;
    virtual bool writeTile(Rect destination, PixelBufferView pixels) noexcept = 0;
    virtual bool endFrame(bool complete) noexcept = 0;
};

enum class TileRenderError : std::uint8_t
{
    none,
    invalidArgument,
    noActiveController,
    renderFailed,
    sinkBeginFailed,
    sinkWriteFailed,
    sinkEndFailed,
    arithmeticOverflow,
    unsupportedFormat,
};

struct TileRenderResult
{
    TileRenderError error{TileRenderError::none};
    std::size_t tileCount{0U};
    std::size_t pixelsTransferred{0U};

    constexpr bool ok() const noexcept
    {
        return error == TileRenderError::none;
    }
    constexpr explicit operator bool() const noexcept
    {
        return ok();
    }
};

class TiledRenderer final
{
public:
    // workspace.width/height select the maximum tile dimensions. Its origin is
    // ignored. Frame and workspace extents must contain complete storage blocks.
    // Tiles at the right and bottom edge can be smaller. Each published tile
    // includes initialized intermediate-row padding; the final storage row ends
    // after its active bytes. The sink consumes this borrowed span synchronously.
    // A structurally valid workspace with no writer in this build is rejected
    // with unsupportedFormat before the sink's beginFrame() is called.
    static TileRenderResult render(Application& application,
                                   Size frameSize,
                                   Surface workspace,
                                   TileSink& sink,
                                   Color clear = Color::rgb(0x000000U)) noexcept;
};

} // namespace lithe
