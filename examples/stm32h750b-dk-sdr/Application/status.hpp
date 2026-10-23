// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <atomic>
#include <cstdint>

namespace starter
{
// One application task publishes; the Windows message thread/debugger observes.
struct Status final
{
    std::atomic<std::uint32_t> heartbeats{};
    std::atomic<std::uint32_t> frames{};
    std::atomic<std::uint32_t> failureLine{};
    std::atomic<std::uint32_t> buttonPresses{};
    std::atomic<bool> ready{};
};
inline Status status;
} // namespace starter
