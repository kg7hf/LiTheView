// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstddef>
#include <cstdint>

namespace lithe::detail
{

inline constexpr std::size_t kBitsPerByte{8U};
inline constexpr std::size_t kAlpha1RoundingBits{kBitsPerByte - 1U};
inline constexpr std::size_t kIndexed4PixelsPerByte{2U};
inline constexpr std::size_t kMaximumIndexed4PaletteEntries{16U};

inline constexpr std::uint32_t kByteMask{0xffU};
inline constexpr std::uint8_t kMaximumChannelValue{255U};
inline constexpr std::uint32_t kRgbColorMask{0xffffffU};

inline constexpr std::uint32_t kGreenByteShift{8U};
inline constexpr std::uint32_t kRedByteShift{16U};
inline constexpr std::uint32_t kAlphaByteShift{24U};

inline constexpr std::uint16_t kDefaultAnimationIntervalMilliseconds{100U};
inline constexpr std::int32_t kDefaultScrollStepPixels{32};
inline constexpr std::uint16_t kDefaultTabHeightPixels{32U};
inline constexpr std::size_t kMaximumTabs{16U};
inline constexpr std::size_t kMaximumWheelItems{255U};
inline constexpr std::uint8_t kDefaultWheelRowHeightPixels{28U};

} // namespace lithe::detail
