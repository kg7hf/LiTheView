// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// LiTheView handwritten form: form.main
// Application-owned. Generate UI never rewrites this file.
#pragma once

#include "Generated/product_config.hpp"

#include <array>
#include <charconv>
#include <cstdint>
#include <limits>
#include <string_view>

namespace application::ui
{
class Form : public lithe::simulator::Form::Instance<>
{
public:
    // Application device event; both platform adapters enter this on the UI task.
    void onUserButtonPressed() noexcept
    {
        if (mButtonCount == std::numeric_limits<std::uint32_t>::max())
        {
            return;
        }
        ++mButtonCount;
        const auto result = std::to_chars(mCountText.data() + 9U,
                                         mCountText.data() + mCountText.size(), mButtonCount);
        if (result.ec != std::errc{})
        {
            getButtonCount()->setText("COUNT ERROR");
            return;
        }
        getButtonCount()->setText(std::string_view{mCountText.data(), result.ptr});
        // The label borrows this persistent buffer; its contents changed in place.
        getButtonCount()->invalidate();
    }

protected:
    // <litheview-handlers>
    // litheview-handler component.main.button1 onButton1Clicked
    void onButton1Clicked() noexcept override
    {
        mOn = !mOn;
        // Dispatch only starts after build succeeds, so this named control exists.
        getLabel1()->setText(mOn ? "ON" : "OFF");
    }
    // </litheview-handlers>

private:
    bool mOn{true};
    std::uint32_t mButtonCount{};
    std::array<char, 24U> mCountText{'P', 'R', 'E', 'S', 'S', 'E', 'S', ':', ' '};
};
} // namespace application::ui
