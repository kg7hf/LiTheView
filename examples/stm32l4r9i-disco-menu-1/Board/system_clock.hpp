// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <expected>

namespace board
{
enum class [[nodiscard]] ClockError
{
    voltage,
    oscillator,
    busClock
};
[[nodiscard]] std::expected<void, ClockError> configureSystemClock() noexcept;
} // namespace board
