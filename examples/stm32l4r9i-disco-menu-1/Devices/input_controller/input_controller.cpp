// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Devices/input_controller/input_controller.hpp"

#include "ft3x67.h"
#include "mfxstm32l152.h"
#include "stm32l4r9i_discovery.h"
#include "stm32l4r9i_discovery_io.h"

#include <array>

// Vendor C transport functions use HAL-shifted addresses and return HAL status,
// despite the upstream ReadMultiple comment describing a byte count.
extern "C" void TS_IO_Init(void);
extern "C" void TS_IO_Write(std::uint8_t, std::uint8_t, std::uint8_t);
extern "C" std::uint32_t I2c1Timeout;
extern "C" std::uint16_t
TS_IO_ReadMultiple(std::uint8_t, std::uint8_t, std::uint8_t*, std::uint16_t);
extern "C" std::uint16_t
MFX_IO_ReadMultiple(std::uint16_t, std::uint8_t, std::uint8_t*, std::uint16_t);

namespace devices::input_controller
{
std::expected<void, Error> Controller::initialize() noexcept
{
    TS_IO_Init();
    std::array<std::uint8_t, 1> value{};
    if (TS_IO_ReadMultiple(TS_I2C_ADDRESS, FT3X67_CHIP_ID_REG, value.data(), 1U) != HAL_OK)
    {
        return std::unexpected(Error::bus);
    }
    if (value.front() != FT3X67_ID_VALUE)
    {
        return std::unexpected(Error::identity);
    }
    if (TS_IO_ReadMultiple(TS_I2C_ADDRESS, FT3X67_FIRMID_REG, value.data(), 1U) != HAL_OK)
    {
        return std::unexpected(Error::bus);
    }
    mFirmware = value.front();
    // This adapter polls continuously. Keep the controller active instead of
    // letting its inactivity timer switch to inactivity/monitor mode. This is
    // an explicit configuration, not a diagnosis of intermittent idle reads.
    constexpr std::array<std::uint8_t, 3> pollingRegisters{
        FT3X67_GESTURE_ENABLE_REG, FT3X67_GMODE_REG, FT3X67_CTRL_REG};
    for (const auto reg : pollingRegisters)
    {
        TS_IO_Write(TS_I2C_ADDRESS, reg, 0U);
        if (TS_IO_ReadMultiple(TS_I2C_ADDRESS, reg, value.data(), 1U) != HAL_OK ||
            value.front() != 0U)
        {
            return std::unexpected(Error::bus);
        }
    }
    // Poll data registers with bounded transaction time; no input IRQ enabled.
    I2c1Timeout = 20U;
    if (BSP_JOY_Init(JOY_MODE_GPIO) != HAL_OK)
    {
        return std::unexpected(Error::joystick);
    }
    // BSP_JOY_Init does not propagate I2C write errors. Verify the four MFX
    // direction inputs and their pull-down configuration through checked reads.
    constexpr std::uint8_t joystickMask = RIGHT_JOY_PIN | LEFT_JOY_PIN | UP_JOY_PIN | DOWN_JOY_PIN;
    struct RegisterCheck
    {
        std::uint8_t address;
        std::uint8_t expected;
    };
    constexpr std::array checks{RegisterCheck{MFXSTM32L152_REG_ADR_GPIO_DIR1, 0U},
                                RegisterCheck{MFXSTM32L152_REG_ADR_GPIO_TYPE1, joystickMask},
                                RegisterCheck{MFXSTM32L152_REG_ADR_GPIO_PUPD1, 0U},
                                RegisterCheck{MFXSTM32L152_REG_ADR_IRQ_GPI_SRC1, 0U}};
    for (const auto check : checks)
    {
        if (MFX_IO_ReadMultiple(IO_I2C_ADDRESS, check.address, value.data(), 1U) != HAL_OK ||
            (value.front() & joystickMask) != check.expected)
        {
            return std::unexpected(Error::joystick);
        }
    }
    mReady = true;
    return {};
}

std::expected<Snapshot, Error> Controller::poll() const noexcept
{
    if (!mReady)
    {
        return std::unexpected(Error::notInitialized);
    }
    // A failed touch transaction must not suppress a healthy button sample,
    // and vice versa. Each channel reports its own validity to the application.
    return Snapshot{pollTouch(), pollButton()};
}

std::expected<Touch, Error> Controller::pollTouch() const noexcept
{
    std::array<std::uint8_t, 1> status{};
    if (TS_IO_ReadMultiple(TS_I2C_ADDRESS, FT3X67_TD_STAT_REG, status.data(), status.size()) !=
        HAL_OK)
    {
        return std::unexpected(Error::bus);
    }
    const auto count = status.front() & FT3X67_TD_STAT_MASK;
    if (count > 2U)
    {
        return std::unexpected(Error::invalidReport);
    }
    Touch snapshot{};
    snapshot.contactsPresent = count != 0U;
    snapshot.cancelled = count > 1U;
    if (count == 1U)
    {
        // Match ST's FT3X67 access sequence: read the contact count, then only
        // the first contact's four coordinate bytes. No second-contact payload
        // is needed by this single-pointer product.
        std::array<std::uint8_t, 4> report{};
        if (TS_IO_ReadMultiple(TS_I2C_ADDRESS, FT3X67_P1_XH_REG, report.data(), report.size()) !=
            HAL_OK)
        {
            return std::unexpected(Error::bus);
        }
        // FT3X67 wire fields: XH, XL, YH, YL. YH upper nibble is contact ID.
        const std::int32_t x = ((report[0] & 0x0fU) << 8U) | report[1];
        const std::int32_t y = ((report[2] & 0x0fU) << 8U) | report[3];
        snapshot.contactId = report[2] >> 4U;
        const auto phase = report[0] >> 6U;
        snapshot.pressed = phase == 0U || phase == 2U;
        snapshot.hasPosition = true;
        snapshot.cancelled = phase == 3U;
        if (mFirmware == 0x01U)
        {
            // Match ST's old-firmware 45-degree correction, with float only.
            // Numeric conversions are confined to this device-coordinate boundary.
            constexpr float inverseRootTwo = 0.7071067811865475F;
            snapshot.x =
                static_cast<std::int32_t>(static_cast<float>(x - y) * inverseRootTwo) + 195;
            snapshot.y =
                static_cast<std::int32_t>(static_cast<float>(x + y - 390) * inverseRootTwo) + 195;
        }
        else
        {
            snapshot.x = x;
            snapshot.y = y;
        }
    }
    // Multi-touch is deliberately cancelled by this single-pointer product.
    // Do not turn an out-of-bounds report into a clamped, accidental button press.
    return snapshot;
}

std::expected<Button, Error> Controller::pollButton() const noexcept
{
    std::array<std::uint8_t, 1> joystick{};
    if (MFX_IO_ReadMultiple(
            IO_I2C_ADDRESS, MFXSTM32L152_REG_ADR_GPIO_STATE1, joystick.data(), 1U) != HAL_OK)
    {
        return std::unexpected(Error::bus);
    }
    if (HAL_GPIO_ReadPin(SEL_JOY_GPIO_PORT, SEL_JOY_PIN) == GPIO_PIN_SET)
    {
        return Button::select;
    }
    if ((joystick.front() & DOWN_JOY_PIN) != 0U)
    {
        return Button::down;
    }
    if ((joystick.front() & LEFT_JOY_PIN) != 0U)
    {
        return Button::left;
    }
    if ((joystick.front() & RIGHT_JOY_PIN) != 0U)
    {
        return Button::right;
    }
    if ((joystick.front() & UP_JOY_PIN) != 0U)
    {
        return Button::up;
    }
    return Button::none;
}
} // namespace devices::input_controller
