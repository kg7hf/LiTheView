// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <cstdint>
#include <expected>

namespace starter::ui
{
enum class Error : std::uint8_t
{
    memory,
    bus,
    timeout,
    layout,
    scene,
    render,
    input
};
[[nodiscard]] std::expected<void, Error> initialize() noexcept;
[[nodiscard]] std::expected<void, Error> update() noexcept;
} // namespace starter::ui
