// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "display_driver.hpp"

#include "display_hal.h"

#include <optional>

namespace devices::display_driver
{
namespace
{

enum class State
{
    cold,
    ready,
    drawing,
    refreshing,
    fault
};

State state{State::cold};
std::optional<Error> fault{};
std::uint32_t refreshStarted{0U};
bool frameHasPixels{false};
constexpr std::uint32_t kRefreshTimeoutMs{250U};

Error fromHal(DisplayHalStatus status) noexcept
{
    if (status == DISPLAY_HAL_TIMEOUT)
    {
        return Error::timeout;
    }
    if (status == DISPLAY_HAL_BOARD_IO_ERROR)
    {
        return Error::boardIoFailure;
    }
    return Error::halFailure;
}

std::unexpected<Error> fail(Error error) noexcept
{
    display_hal_stop();
    fault = error;
    state = State::fault;
    return std::unexpected{error};
}

} // namespace

std::expected<void, Error> initialize(BoardRevision revision) noexcept
{
    if (state != State::cold)
    {
        return std::unexpected{fault.value_or(Error::invalidState)};
    }
    std::uint32_t revisionNumber{0U};
    switch (revision)
    {
    case BoardRevision::a:
        revisionNumber = 1U;
        break;
    case BoardRevision::b:
        revisionNumber = 2U;
        break;
    case BoardRevision::c:
        revisionNumber = 3U;
        break;
    default:
        return std::unexpected{Error::unsupportedRevision};
    }
    const auto status = display_hal_initialize(revisionNumber);
    if (status != DISPLAY_HAL_OK)
    {
        return fail(fromHal(status));
    }
    state = State::ready;
    return {};
}

std::expected<bool, Error> pollReady() noexcept
{
    if (state == State::cold)
    {
        return std::unexpected{Error::notInitialized};
    }
    if (fault)
    {
        return std::unexpected{*fault};
    }
    if (display_hal_failed() != 0U)
    {
        return fail(Error::halFailure);
    }
    if (state == State::refreshing)
    {
        if (display_hal_completed() != 0U)
        {
            state = State::ready;
        }
        else if (display_hal_ticks() - refreshStarted >= kRefreshTimeoutMs)
        {
            return fail(Error::timeout);
        }
    }
    return state == State::ready;
}

std::expected<void, Error> beginFrame() noexcept
{
    const auto ready = pollReady();
    if (!ready)
    {
        return std::unexpected{ready.error()};
    }
    if (!*ready)
    {
        return std::unexpected{state == State::refreshing ? Error::busy : Error::invalidState};
    }
    state = State::drawing;
    frameHasPixels = false;
    return {};
}

std::expected<void, Error> copyTile(const Rgb888Tile& tile) noexcept
{
    if (fault)
    {
        return std::unexpected{*fault};
    }
    if (state != State::drawing)
    {
        return std::unexpected{state == State::cold ? Error::notInitialized : Error::invalidState};
    }
    if (tile.width == 0U || tile.height == 0U || tile.width > kWidth || tile.height > kHeight ||
        tile.x > kWidth - tile.width || tile.y > kHeight - tile.height ||
        tile.strideBytes > std::size_t{kWidth} * 3U || tile.strideBytes % 3U != 0U ||
        tile.strideBytes < std::size_t{tile.width} * 3U)
    {
        return std::unexpected{Error::invalidTile};
    }
    const std::size_t usedBytes =
        (std::size_t{tile.height} - 1U) * tile.strideBytes + std::size_t{tile.width} * 3U;
    if (tile.data.size() < usedBytes)
    {
        return std::unexpected{Error::invalidTile};
    }
    if (display_hal_failed() != 0U)
    {
        return fail(Error::halFailure);
    }
    // Reviewed hardware boundary: HAL DMA2D consumes a physical bus address.
    const auto source = reinterpret_cast<std::uintptr_t>(tile.data.data());
    const auto status =
        display_hal_copy(source, tile.x, tile.y, tile.width, tile.height, tile.strideBytes / 3U);
    if (status != DISPLAY_HAL_OK)
    {
        return fail(fromHal(status));
    }
    frameHasPixels = true;
    return {};
}

std::expected<void, Error> refresh() noexcept
{
    if (fault)
    {
        return std::unexpected{*fault};
    }
    if (state != State::drawing || !frameHasPixels)
    {
        return std::unexpected{Error::invalidState};
    }
    if (display_hal_failed() != 0U)
    {
        return fail(Error::halFailure);
    }
    refreshStarted = display_hal_ticks();
    const auto status = display_hal_refresh();
    if (status != DISPLAY_HAL_OK)
    {
        return fail(fromHal(status));
    }
    state = State::refreshing;
    return {};
}

void abortFrame() noexcept
{
    if (state == State::drawing)
    {
        frameHasPixels = false;
        state = State::ready;
    }
}

} // namespace devices::display_driver
