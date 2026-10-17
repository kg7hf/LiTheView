// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "runtime_types.hpp"

// Developer-owned file: regeneration preserves this whole file. Keep text and
// resource storage alive while views borrow it. String literals are suitable.
namespace lithe::simulator
{
class ApplicationHooks
{
public:
    void onReady([[maybe_unused]] Views& views) noexcept
    {
    }
    void onAction([[maybe_unused]] Views& views, [[maybe_unused]] ViewId action) noexcept
    {
        // Example for a form declaring action 1 and label runtime ID 100:
        // if (action == 1U)
        //     if (auto* label = views.find<LabelView>(100U)) label->setText("ACTIVE");
    }
    void onBack([[maybe_unused]] Views& views) noexcept
    {
    }
};
} // namespace lithe::simulator
