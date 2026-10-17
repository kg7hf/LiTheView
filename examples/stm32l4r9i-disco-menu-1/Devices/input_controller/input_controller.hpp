// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <cstdint>
#include <expected>

namespace devices::input_controller
{
enum class [[nodiscard]] Error
{
    notInitialized,
    bus,
    identity,
    invalidReport,
    joystick
};
enum class Button : std::uint8_t
{
    none,
    up,
    down,
    left,
    right,
    select
};
struct Touch
{
    std::int32_t x{};
    std::int32_t y{};
    std::uint8_t contactId{};
    bool pressed{};
    bool contactsPresent{};
    bool hasPosition{};
    bool cancelled{};
};
struct Snapshot
{
    std::expected<Touch, Error> touch{Touch{}};
    std::expected<Button, Error> button{Button::none};
};

class Controller final
{
public:
    [[nodiscard]] std::expected<void, Error> initialize() noexcept;
    [[nodiscard]] std::expected<Snapshot, Error> poll() const noexcept;
    [[nodiscard]] std::uint8_t firmwareRevision() const noexcept
    {
        return mFirmware;
    }

private:
    [[nodiscard]] std::expected<Touch, Error> pollTouch() const noexcept;
    [[nodiscard]] std::expected<Button, Error> pollButton() const noexcept;
    std::uint8_t mFirmware{};
    bool mReady{};
};
} // namespace devices::input_controller
