// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include "Application/navigation.hpp"
#include "Application/volume.hpp"
namespace menu
{
// An empty compile-time slot. It has no controller, resource provider or heap.
// MenuRuntime never calls a disabled slot: those branches are discarded.
struct UnusedPage final
{
    UnusedPage() = default;
    explicit UnusedPage(Requests&) noexcept
    {
    }
    UnusedPage(Requests&, Volume&) noexcept
    {
    }
};
} // namespace menu
