// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Devices/input.hpp"

#include "Devices/user_button.hpp"
#include "Platform/Win32/window.hpp"

#include <array>
#include <atomic>

namespace starter
{
namespace
{
// Window thread produces; the FreeRTOS application task consumes. No Windows
// lock may be held by a thread the RTOS port can suspend. Queue exhaustion is
// explicit so a lost release cannot leave a widget captured indefinitely.
constexpr std::uint32_t kCapacity{64U};
std::array<PointerSample, kCapacity> samples{};
std::atomic<std::uint32_t> head{};
std::atomic<std::uint32_t> tail{};
std::atomic<bool> overflow{};
std::atomic<bool> userButton{};
static_assert(std::atomic<std::uint32_t>::is_always_lock_free);
static_assert(std::atomic<bool>::is_always_lock_free);
void enqueue(PointerSample sample) noexcept
{
    const auto index = head.load(std::memory_order_relaxed);
    const auto next = (index + 1U) % kCapacity;
    if (next == tail.load(std::memory_order_acquire))
    {
        overflow.store(true, std::memory_order_release);
        return;
    }
    samples[index] = sample;
    head.store(next, std::memory_order_release);
}
} // namespace

bool readUserButton() noexcept
{
    return userButton.load(std::memory_order_relaxed);
}
std::uint32_t inputMilliseconds() noexcept
{
    return GetTickCount();
}

std::expected<void, ui::Error> initializeInput() noexcept
{
    return {};
}

std::expected<std::optional<PointerSample>, ui::Error> readPointer() noexcept
{
    if (overflow.load(std::memory_order_acquire))
    {
        return std::unexpected(ui::Error::input);
    }
    const auto index = tail.load(std::memory_order_relaxed);
    if (index == head.load(std::memory_order_acquire))
    {
        return std::optional<PointerSample>{};
    }
    const auto sample = samples[index];
    tail.store((index + 1U) % kCapacity, std::memory_order_release);
    return sample;
}

namespace host
{
void setUserButton(bool pressed) noexcept
{
    userButton.store(pressed, std::memory_order_relaxed);
}
void pointer(std::int32_t x, std::int32_t y, bool pressed, bool cancelled) noexcept
{
    enqueue({x, y, pressed, cancelled, std::nullopt});
}
void key(lithe::Key value) noexcept
{
    enqueue({0, 0, false, false, value});
}

} // namespace host
} // namespace starter
