// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "graphics_probe.hpp"

#include "crc_sink.hpp"

namespace lithe::stm32u545
{

GraphicsProbe::GraphicsProbe() noexcept = default;

ProbeResult GraphicsProbe::renderFrame() noexcept
{
    ProbeResult result{};
    result.objectBytes = sizeof(*this);
    if (!mInitialized)
    {
        mInitialized = mDemo.initialize();
        if (!mInitialized)
        {
            result.error = ProbeError::initializationFailed;
            return result;
        }
    }

    detail::CrcSink<demo::Demo::kWidth, demo::Demo::kHeight> sink{};
    const Surface workspace{
        mStrip, demo::Demo::kWidth, GraphicsProbe::kStripHeight, demo::Demo::kWidth};
    const TileRenderResult rendered = mDemo.renderTiled(sink, workspace);
    if (!rendered)
    {
        result.error = ProbeError::renderFailed;
        return result;
    }
    result.frameCrc32 = sink.value();
    result.tileCount = rendered.tileCount;
    result.pixelsRendered = rendered.pixelsTransferred;
    return result;
}

} // namespace lithe::stm32u545
