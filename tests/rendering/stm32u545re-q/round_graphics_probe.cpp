// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "crc_sink.hpp"
#include "graphics_probe.hpp"

namespace lithe::stm32u545
{

RoundGraphicsProbe::RoundGraphicsProbe() noexcept = default;

ProbeResult RoundGraphicsProbe::renderFrame() noexcept
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

    detail::CrcSink<demo::RoundDemo::kWidth, demo::RoundDemo::kHeight> sink{};
    const Surface workspace{
        mStrip, demo::RoundDemo::kWidth, RoundGraphicsProbe::kStripHeight, demo::RoundDemo::kWidth};
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
