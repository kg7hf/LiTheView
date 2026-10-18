// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include <array>
#include <cstdint>
#include <optional>
namespace tiny
{
class CapacitivePosition
{
public:
    using Counts = std::array<std::uint32_t, 4>;
    // First 32 complete scans calibrate with hands off. Counts decrease on touch.
    [[nodiscard]] std::optional<std::uint16_t> sample(const Counts& counts) noexcept
    {
        for (auto count : counts)
            if (count == 0 || count >= 65535U)
                return {};
        if (mCalibration < 32U)
        {
            for (unsigned i = 0; i < 4; ++i)
                if (counts[i] > mBaseline[i])
                    mBaseline[i] = counts[i];
            ++mCalibration;
            return {};
        }
        std::uint32_t weight{}, moment{};
        for (unsigned i = 0; i < 4; ++i)
        {
            const auto normalized = counts[i] * 256U / mBaseline[i];
            const auto contribution = normalized < 224U ? 224U - normalized : 0U;
            weight += contribution;
            moment += contribution * i * 1000U;
        }
        if (weight == 0)
            return {};
        // LITHEVIEW_CAST_BOUNDARY: weighted mean of four positions is in [0,1000].
        return static_cast<std::uint16_t>(moment / (weight * 3U));
    }
    [[nodiscard]] bool calibrated() const noexcept
    {
        return mCalibration == 32U;
    }

private:
    Counts mBaseline{};
    unsigned mCalibration{};
};
} // namespace tiny
