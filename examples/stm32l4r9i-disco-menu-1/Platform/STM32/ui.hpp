// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Devices/input_controller/input_controller.hpp"

#include <cstdint>
#include <expected>

namespace ui
{
enum class [[nodiscard]] Error
{
    layout,
    display,
    render
};
[[nodiscard]] std::expected<void, Error> initialize() noexcept;
[[nodiscard]] bool input(const devices::input_controller::Snapshot& sample,
                         std::uint32_t now) noexcept;
[[nodiscard]] bool healthy() noexcept;
void cancelInput(std::uint32_t now) noexcept;
[[nodiscard]] std::expected<void, Error> render() noexcept;
} // namespace ui
