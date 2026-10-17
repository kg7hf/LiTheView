// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "Application/ui.hpp"

#include <cstdint>
#include <expected>
#include <optional>

namespace starter
{
struct PointerSample
{
    std::int32_t x{};
    std::int32_t y{};
    bool pressed{};
    bool cancelled{};
};

// The application task owns reads; platform code maps hardware coordinates.
[[nodiscard]] std::expected<void, ui::Error> initializeInput() noexcept;
[[nodiscard]] std::expected<std::optional<PointerSample>, ui::Error> readPointer() noexcept;
} // namespace starter
