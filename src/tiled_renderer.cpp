// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/tiled_renderer.hpp"

#include "lithe/painter.hpp"
#include "lithe/render_config.hpp"

#include <algorithm>

namespace lithe
{

TileRenderResult TiledRenderer::render(Application& application,
                                       Size frameSize,
                                       Surface workspace,
                                       TileSink& sink,
                                       Color clear) noexcept
{
    TileRenderResult result{};
    // Reusable workspace has no logical position. Actual tile origins below
    // remain aligned because each extent contains complete storage blocks.
    workspace.origin = {};
    if (frameSize.width <= 0 || frameSize.height <= 0 || !workspace.valid())
    {
        result.error = TileRenderError::invalidArgument;
        return result;
    }
    if (!supportsDestination(workspace.format))
    {
        result.error = TileRenderError::unsupportedFormat;
        return result;
    }
    const auto block = storageBlock(workspace.format);
    if (frameSize.width % block.width != 0 || frameSize.height % block.height != 0)
    {
        result.error = TileRenderError::invalidArgument;
        return result;
    }
    std::size_t framePixels = 0U;
    if (!detail::checkedMultiply(detail::nonnegativeSize(frameSize.width),
                                 detail::nonnegativeSize(frameSize.height),
                                 framePixels))
    {
        result.error = TileRenderError::arithmeticOverflow;
        return result;
    }
    if (application.activeController() == nullptr)
    {
        result.error = TileRenderError::noActiveController;
        return result;
    }
    if (!sink.beginFrame(frameSize, workspace.format))
    {
        result.error = TileRenderError::sinkBeginFailed;
        return result;
    }

    DamageTracker damage{};
    std::int32_t y = 0;
    while (y < frameSize.height)
    {
        const std::int32_t remainingHeight = frameSize.height - y;
        const std::int32_t tileHeight =
            remainingHeight < workspace.height ? remainingHeight : workspace.height;
        std::int32_t x = 0;
        while (x < frameSize.width)
        {
            const std::int32_t remainingWidth = frameSize.width - x;
            const std::int32_t tileWidth =
                remainingWidth < workspace.width ? remainingWidth : workspace.width;
            Surface tile{workspace.data,
                         workspace.format,
                         tileWidth,
                         tileHeight,
                         workspace.strideBytes,
                         {x, y}};
            const auto layout = tile.layout();
            if (!layout)
            {
                result.error = sink.endFrame(false) ? TileRenderError::invalidArgument
                                                    : TileRenderError::sinkEndFailed;
                return result;
            }
            const auto usedBytes = layout->usedBytes;
            tile.data = tile.data.first(usedBytes);
            // Intermediate row padding is part of the borrowed payload. Clear
            // it along with active storage before rendering and publishing.
            std::ranges::fill(tile.data, std::byte{});
            DamageTracker clearDamage{};
            Painter clearPainter{tile, clearDamage};
            clearPainter.fillRect(tile.bounds(), clear);
            // A valid tile can contain no viewport pixels (for example, a
            // corner tile around a circular display). The cleared tile still
            // belongs in the deterministic frame stream; renderSurface()
            // returning false here means no damage, not a render failure.
            application.renderSurface(tile, damage, true);
            const Rect destination{x, y, tileWidth, tileHeight};
            const PixelBufferView tilePixels{
                tile.data.first(usedBytes), tile.format, tile.width, tile.height, tile.strideBytes};
            if (!sink.writeTile(destination, tilePixels))
            {
                result.error = sink.endFrame(false) ? TileRenderError::sinkWriteFailed
                                                    : TileRenderError::sinkEndFailed;
                return result;
            }
            // Preflight proved the complete logical frame count representable;
            // disjoint positive tiles cannot exceed that count.
            const auto pixels =
                detail::nonnegativeSize(tileWidth) * detail::nonnegativeSize(tileHeight);
            result.pixelsTransferred += pixels;
            ++result.tileCount;
            x += tileWidth;
        }
        y += tileHeight;
    }

    if (result.pixelsTransferred != framePixels)
    {
        result.error =
            sink.endFrame(false) ? TileRenderError::renderFailed : TileRenderError::sinkEndFailed;
    }
    else if (!sink.endFrame(true))
    {
        result.error = TileRenderError::sinkEndFailed;
    }
    return result;
}

} // namespace lithe
