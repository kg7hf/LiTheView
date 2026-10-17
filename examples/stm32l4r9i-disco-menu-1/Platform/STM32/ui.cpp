// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Platform/STM32/ui.hpp"

#include "Application/menu.hpp"
#include "Devices/display_contract.hpp"
#include "Devices/display_driver/display_driver.hpp"

#include <array>
#include <optional>

namespace ui
{
namespace
{
namespace display = devices::display_driver;
using Button = devices::input_controller::Button;
constexpr std::int32_t kWidth{390};
constexpr std::int32_t kHeight{390};
constexpr std::int32_t kTileHeight{4};
constexpr std::size_t kStrideBytes{1170U};
menu::Menu application{lithe::generated::assets::HomeMenuMainForm::DisplayConfiguration::kViewport};
alignas(4) std::array<std::byte, kTileHeight * kStrideBytes> workspace{};
std::optional<lithe::PointerEvent> contact{};
bool contactBlocked{};
bool inputFailed{};
bool deliverPointer(const lithe::PointerEvent& event) noexcept
{
    if (!application.pointer(event))
    {
        inputFailed = true;
    }
    return true;
}
bool deliverKey(lithe::Key key) noexcept
{
    if (!application.key(key))
    {
        inputFailed = true;
    }
    return true;
}
Button pendingButton{Button::none};
Button stableButton{Button::none};
bool buttonBlocked{};
std::uint32_t buttonChangedAt{};

class Sink final : public lithe::TileSink
{
public:
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        mNextRow = 0;
        return size.width == kWidth && size.height == kHeight &&
               format == lithe::PixelFormat::rgb888 && display::beginFrame().has_value();
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        if (destination.x != 0 || destination.y != mNextRow || destination.width != kWidth ||
            destination.height <= 0 || destination.height > kHeight - mNextRow ||
            pixels.width != destination.width || pixels.height != destination.height ||
            pixels.format != lithe::PixelFormat::rgb888 || !pixels.layout())
        {
            return false;
        }
        // Validated geometry is narrowed once at the 16-bit hardware-address boundary.
        const display::Rgb888Tile tile{pixels.data,
                                       0U,
                                       // LITHEVIEW_CAST_BOUNDARY: validated tile coordinates/extents lie in [0,390].
                                       static_cast<std::uint16_t>(destination.y),
                                       // LITHEVIEW_CAST_BOUNDARY: validated tile coordinates/extents lie in [0,390].
                                       static_cast<std::uint16_t>(destination.width),
                                       // LITHEVIEW_CAST_BOUNDARY: validated tile coordinates/extents lie in [0,390].
                                       static_cast<std::uint16_t>(destination.height),
                                       pixels.strideBytes};
        if (!display::copyTile(tile))
        {
            return false;
        }
        mNextRow += destination.height;
        return true;
    }
    bool endFrame(bool complete) noexcept override
    {
        if (!complete || mNextRow != kHeight)
        {
            display::abortFrame();
            return !complete;
        }
        return display::refresh().has_value();
    }

private:
    std::int32_t mNextRow{};
};

[[nodiscard]] bool visible(lithe::Point point) noexcept
{
    if (point.x < 0 || point.y < 0 || point.x >= 390 || point.y >= 390)
    {
        return false;
    }
    const auto x = point.x * 2 + 1 - 390;
    const auto y = point.y * 2 + 1 - 390;
    return x * x + y * y <= 390 * 390;
}
[[nodiscard]] bool cancelTouch(std::uint32_t now) noexcept
{
    contactBlocked = true;
    bool changed = false;
    if (contact)
    {
        auto cancelled = *contact;
        cancelled.phase = lithe::PointerPhase::cancelled;
        cancelled.timestampMs = now;
        changed = deliverPointer(cancelled);
        contact.reset();
    }
    return changed;
}

void cancelButton(std::uint32_t now) noexcept
{
    pendingButton = Button::none;
    stableButton = Button::none;
    buttonBlocked = true;
    buttonChangedAt = now;
}

[[nodiscard]] bool touchInput(const devices::input_controller::Touch& sample,
                              std::uint32_t now) noexcept
{
    bool changed = false;
    const lithe::Point point{sample.x, sample.y};
    const bool rejected = sample.cancelled || (sample.hasPosition && !visible(point)) ||
                          (contact && sample.hasPosition && sample.contactId != contact->contactId);
    if (rejected)
    {
        contactBlocked = true;
    }
    if (contact && (contactBlocked || !sample.pressed))
    {
        auto ended = *contact;
        ended.phase =
            contactBlocked ? lithe::PointerPhase::cancelled : lithe::PointerPhase::released;
        if (sample.hasPosition)
        {
            ended.position = point;
        }
        ended.timestampMs = now;
        changed = deliverPointer(ended) || changed;
        contact.reset();
    }
    if (!sample.contactsPresent)
    {
        contactBlocked = false;
    }
    if (!contactBlocked && sample.pressed && sample.hasPosition && visible(point))
    {
        const lithe::PointerEvent event{point,
                                        contact ? lithe::PointerPhase::moved
                                                : lithe::PointerPhase::pressed,
                                        sample.contactId,
                                        now};
        if (!contact || contact->position.x != point.x || contact->position.y != point.y)
        {
            changed = deliverPointer(event) || changed;
        }
        contact = event;
    }
    return changed;
}

[[nodiscard]] bool buttonInput(Button button, std::uint32_t now) noexcept
{
    if (buttonBlocked)
    {
        if (button == Button::none)
        {
            buttonBlocked = false;
        }
        return false;
    }
    if (button != pendingButton)
    {
        pendingButton = button;
        buttonChangedAt = now;
    }
    if (stableButton != pendingButton && now - buttonChangedAt >= 25U)
    {
        stableButton = pendingButton;
        std::optional<lithe::Key> key{};
        switch (stableButton)
        {
        case Button::up:
            key = lithe::Key::up;
            break;
        case Button::down:
            key = lithe::Key::down;
            break;
        case Button::left:
            key = lithe::Key::left;
            break;
        case Button::right:
            key = lithe::Key::right;
            break;
        case Button::select:
            key = lithe::Key::select;
            break;
        case Button::none:
            break;
        }
        if (key)
        {
            return deliverKey(*key);
        }
    }
    return false;
}
} // namespace

std::expected<void, Error> initialize() noexcept
{
    if (!application.initialize())
    {
        return std::unexpected(Error::layout);
    }
    return {};
}

void cancelInput(std::uint32_t now) noexcept
{
    (void)cancelTouch(now);
    cancelButton(now);
}

bool input(const devices::input_controller::Snapshot& sample, std::uint32_t now) noexcept
{
    bool changed = sample.touch ? touchInput(*sample.touch, now) : cancelTouch(now);
    if (sample.button)
    {
        changed = buttonInput(*sample.button, now) || changed;
    }
    else
    {
        cancelButton(now);
    }
    return changed;
}

std::expected<void, Error> render() noexcept
{
    Sink sink{};
    const lithe::Surface tile{
        workspace, lithe::PixelFormat::rgb888, kWidth, kTileHeight, kStrideBytes};
    if (!application.renderTiled({kWidth, kHeight}, tile, sink))
    {
        return std::unexpected(Error::render);
    }
    return {};
}

bool healthy() noexcept
{
    return !inputFailed;
}
} // namespace ui
