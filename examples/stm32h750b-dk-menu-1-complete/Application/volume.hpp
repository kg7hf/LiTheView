// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <algorithm>
#include <array>
#include <cstdint>
#include <string_view>

namespace menu
{
enum class VolumeChange : std::uint8_t
{
    decrease,
    increase
};

// Application state, independent of the display, input device and RTOS.
// Only these bounded steps can change it; there is no borrowed mutable text.
class Volume final
{
public:
    static constexpr std::int32_t kMinimum{0};
    static constexpr std::int32_t kMaximum{100};
    static constexpr std::int32_t kStep{5};
    [[nodiscard]] std::int32_t value() const noexcept
    {
        return mValue;
    }
    void adjust(VolumeChange change) noexcept
    {
        const auto delta = change == VolumeChange::increase ? kStep : -kStep;
        mValue = std::clamp(mValue + delta, kMinimum, kMaximum);
    }
    [[nodiscard]] std::string_view text() const noexcept
    {
        // LITHEVIEW_CAST_BOUNDARY: mValue is a multiple of kStep in [0,100]; index is [0,20].
        return kLabels[static_cast<std::size_t>(mValue / kStep)];
    }

private:
    static constexpr std::array<std::string_view, 21U> kLabels{
        "0%",  "5%",  "10%", "15%", "20%", "25%", "30%", "35%", "40%", "45%", "50%",
        "55%", "60%", "65%", "70%", "75%", "80%", "85%", "90%", "95%", "100%"};
    static_assert(kMaximum / kStep + 1 == kLabels.size());
    std::int32_t mValue{50};
};
} // namespace menu
