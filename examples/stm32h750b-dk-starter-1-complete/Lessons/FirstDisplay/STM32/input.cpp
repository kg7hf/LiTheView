// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Devices/input.hpp"
#include "Devices/user_button.hpp"

#include "Lessons/FirstDisplay/display.hpp"
#include "main.h"

#include <array>

extern "C"
{
    extern I2C_HandleTypeDef hi2c4;
}

namespace starter
{
namespace
{
constexpr std::uint16_t kTouchAddress{0x70U}; // HAL-shifted FT5336 address.
constexpr std::uint32_t kTimeoutMs{20U};
PointerSample previous{};
} // namespace

bool readUserButton() noexcept
{
    // Cube already configures B1 as PC13. The shared task debounces the sampled level.
    return HAL_GPIO_ReadPin(B1_GPIO_Port, B1_Pin) == GPIO_PIN_SET;
}
std::uint32_t inputMilliseconds() noexcept
{
    return HAL_GetTick();
}

std::expected<void, ui::Error> initializeInput() noexcept
{
    std::uint8_t chip{};
    if (HAL_I2C_Mem_Read(
            &hi2c4, kTouchAddress, 0xA8U, I2C_MEMADD_SIZE_8BIT, &chip, 1U, kTimeoutMs) != HAL_OK ||
        chip != 0x51U)
    {
        return std::unexpected(ui::Error::input);
    }
    std::uint8_t mode{0U}; // Polling mode; the owning task samples contacts and releases.
    if (HAL_I2C_Mem_Write(
            &hi2c4, kTouchAddress, 0xA4U, I2C_MEMADD_SIZE_8BIT, &mode, 1U, kTimeoutMs) != HAL_OK)
    {
        return std::unexpected(ui::Error::input);
    }
    return {};
}

std::expected<std::optional<PointerSample>, ui::Error> readPointer() noexcept
{
    std::array<std::uint8_t, 5U> registers{};
    if (HAL_I2C_Mem_Read(&hi2c4,
                         kTouchAddress,
                         0x02U,
                         I2C_MEMADD_SIZE_8BIT,
                         registers.data(),
                         registers.size(),
                         kTimeoutMs) != HAL_OK)
    {
        return std::unexpected(ui::Error::input);
    }
    const auto contacts = registers[0] & 0x0FU;
    PointerSample sample{previous.x, previous.y, contacts == 1U, contacts > 1U};
    if (sample.pressed)
    {
        // RK043 reports portrait coordinates; landscape maps the two axes as ST's TS_SWAP_XY.
        sample.x = ((registers[3] & 0x0FU) << 8U) | registers[4];
        sample.y = ((registers[1] & 0x0FU) << 8U) | registers[2];
        if (sample.x >= width || sample.y >= height)
        {
            return std::unexpected(ui::Error::input);
        }
    }
    if (sample.x == previous.x && sample.y == previous.y && sample.pressed == previous.pressed &&
        sample.cancelled == previous.cancelled)
    {
        return std::optional<PointerSample>{};
    }
    previous = sample;
    return sample;
}
} // namespace starter
