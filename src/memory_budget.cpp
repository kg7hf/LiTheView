// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/layout.hpp"

#include <cstddef>

namespace
{

constexpr std::size_t kCortexMAddressBytes{4U};
constexpr std::size_t kViewMaximumBytes{80U};
constexpr std::size_t kViewSpecMaximumBytes{124U};
constexpr std::size_t kPickerViewMaximumBytes{112U};
// Icon and toggle button state must stay within the largest existing
// StaticLayout member, AnimatedImageView, so the layout union does not grow.
constexpr std::size_t kButtonViewMaximumBytes{112U};
constexpr std::size_t kWidgetStateStyleMaximumBytes{32U};
// Tab widgets hold borrowed titles and byte-sized indexes; they share the same
// union ceiling as buttons.
constexpr std::size_t kTabBarViewMaximumBytes{112U};
constexpr std::size_t kTabViewMaximumBytes{112U};
constexpr std::size_t kWheelPickerViewMaximumBytes{112U};
constexpr std::size_t kTextFieldViewMaximumBytes{116U};
constexpr std::size_t kDropDownViewMaximumBytes{112U};
constexpr std::size_t kBudgetedLayoutViewCapacity{16U};
constexpr std::size_t kBudgetedLayoutMaximumBytes{1'900U};

} // namespace

// These are regression ceilings, not ABI promises. They keep accidental host-
// style growth visible when the library is compiled for a 32-bit Cortex-M.
static_assert(sizeof(void*) != kCortexMAddressBytes || sizeof(lithe::View) <= kViewMaximumBytes,
              "View exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::ViewSpec) <= kViewSpecMaximumBytes,
              "ViewSpec exceeded the small-target parse-buffer RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::PickerView) <= kPickerViewMaximumBytes,
              "PickerView exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::ButtonView) <= kButtonViewMaximumBytes,
              "ButtonView exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::TabBarView) <= kTabBarViewMaximumBytes,
              "TabBarView exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::TabView) <= kTabViewMaximumBytes,
              "TabView exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::WheelPickerView) <= kWheelPickerViewMaximumBytes,
              "WheelPickerView exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::TextFieldView) <= kTextFieldViewMaximumBytes,
              "TextFieldView exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::DropDownView) <= kDropDownViewMaximumBytes,
              "DropDownView exceeded the small-target per-widget RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::WidgetStateStyle) <= kWidgetStateStyleMaximumBytes,
              "WidgetStateStyle exceeded its per-overridden-view RAM budget");
static_assert(sizeof(void*) != kCortexMAddressBytes ||
                  sizeof(lithe::StaticLayout<kBudgetedLayoutViewCapacity>) <=
                      kBudgetedLayoutMaximumBytes,
              "StaticLayout<16> exceeded the small-target RAM budget");
