// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once

#include <array>
#include <cstddef>
#include <span>

namespace lithe
{
// Logical electrode IDs, not MCU register offsets. The consuming glass definition
// supplies their meaning. Bytes are LSB-first within each group of eight IDs.
template <std::size_t Count> class SegmentFrame
{
    static constexpr std::size_t kMaximumSegments{65536};
    static constexpr std::size_t kBitsPerByte{8};
    static_assert(Count > 0 && Count <= kMaximumSegments);

public:
    static constexpr std::size_t kSegmentCount{Count};
    [[nodiscard]] constexpr bool set(std::size_t id, bool enabled = true) noexcept
    {
        if (id >= Count)
        {
            return false;
        }
        const auto mask = std::byte{1} << (id % kBitsPerByte);
        // Access through a byte view shared by all frame sizes. This also avoids
        // GCC's identical-code folding attaching another instantiation's array
        // type to an inlined access when several frame sizes share a translation unit.
        auto& byte = std::span<std::byte>{mBytes}[id / kBitsPerByte];
        byte = enabled ? byte | mask : byte & ~mask;
        return true;
    }
    [[nodiscard]] constexpr bool test(std::size_t id) const noexcept
    {
        return id < Count &&
               (mBytes[id / kBitsPerByte] & (std::byte{1} << (id % kBitsPerByte))) != std::byte{0};
    }
    [[nodiscard]] constexpr std::span<const std::byte> bytes() const noexcept
    {
        return mBytes;
    }
    constexpr bool operator==(const SegmentFrame&) const noexcept = default;

private:
    std::array<std::byte, (Count + kBitsPerByte - 1U) / kBitsPerByte> mBytes{};
};
} // namespace lithe
