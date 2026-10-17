// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// Compiles a Generate UI component for its selected native packed configuration against an
// installed fixed-format LitheView::Core. The rendered native storage is checked against an
// independent packing oracle, and logical select input drives the handwritten button handler.
#include "product_config.hpp"

#include "lithe/application.hpp"
#include "lithe/detail/index.hpp"
#include "lithe/storage_layout.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <span>

namespace
{
using Configuration = lithe::simulator::Configuration;

static_assert(Configuration::kFormat == lithe::PixelFormat::r2Packed2x2 ||
                  Configuration::kFormat == lithe::PixelFormat::rrrr2222 ||
                  Configuration::kFormat == lithe::PixelFormat::ryry2222,
              "This consumer checks native packed components.");

constexpr lithe::Size kExtent{Configuration::kDefinition.logicalSize};
constexpr lithe::StorageBlock kBlock{lithe::storageBlock(Configuration::kFormat)};
constexpr std::int32_t kColumns{kExtent.width / kBlock.width};
constexpr std::int32_t kRows{kExtent.height / kBlock.height};
constexpr std::size_t kStrideBytes{lithe::detail::nonnegativeSize(kColumns) * kBlock.bytes};
constexpr auto kLayout = lithe::storageLayout(Configuration::kFormat, kExtent, kStrideBytes);
static_assert(kLayout.has_value(), "The generated configuration must contain whole storage blocks.");
constexpr std::size_t kFrameBytes{kLayout->usedBytes};

// The checked form's opaque fills in logical emitters or cells; later fills cover earlier ones.
// Legacy colors 0x000000ff, 0x00000080 and 0x0000ffff are red, half red and yellow.
struct Fill
{
    std::int32_t x;
    std::int32_t y;
    std::int32_t width;
    std::int32_t height;
    std::uint32_t red;
    std::uint32_t green;
};

constexpr std::array<Fill, 3U> kScene{{{0, 0, kExtent.width, kExtent.height, 255U, 0U},
                                       {8, 4, 16, 8, 128U, 0U},
                                       {41, 33, 5, 3, 255U, 255U}}};
// The button's own rendering is not part of the oracle.
constexpr Fill kButton{100, 100, 80, 30, 0U, 0U};
// Row-major fields of one storage byte: TL bits 3:2, TR 7:6, BL 1:0, BR 5:4.
constexpr std::array<std::uint32_t, 4U> kFieldShift{2U, 6U, 0U, 4U};

constexpr bool contains(const Fill& fill, std::int32_t x, std::int32_t y) noexcept
{
    return x >= fill.x && x < fill.x + fill.width && y >= fill.y && y < fill.y + fill.height;
}

constexpr Fill colorAt(std::int32_t x, std::int32_t y) noexcept
{
    Fill color = kScene.front();
    for (const Fill& fill : kScene)
    {
        if (contains(fill, x, y))
        {
            color = fill;
        }
    }
    return color;
}

constexpr std::uint32_t level(std::uint32_t channel) noexcept
{
    return ((channel * 3U) + 127U) / 255U;
}

// An independent statement of the packing contract. R2 stores one red level per emitter of a
// 2x2 block. RRRR repeats a cell's red level; RYRY stores yellow, min(red, green), at TL/BR and
// the remaining red at TR/BL. Returns false for a byte touched by the button.
constexpr bool expectedByte(std::int32_t column, std::int32_t row, std::uint32_t& encoded) noexcept
{
    constexpr bool kEmitters = Configuration::kFormat == lithe::PixelFormat::r2Packed2x2;
    encoded = 0U;
    for (std::int32_t field = 0; field < 4; ++field)
    {
        const std::int32_t x = kEmitters ? (column * 2) + (field % 2) : column;
        const std::int32_t y = kEmitters ? (row * 2) + (field / 2) : row;
        if (contains(kButton, x, y))
        {
            return false;
        }
        const Fill fill = colorAt(x, y);
        std::uint32_t value = level(fill.red);
        if constexpr (Configuration::kFormat == lithe::PixelFormat::ryry2222)
        {
            const std::uint32_t yellow = std::min(fill.red, fill.green);
            value = level(field == 0 || field == 3 ? yellow : fill.red - yellow);
        }
        encoded |= value << kFieldShift[lithe::detail::nonnegativeSize(field)];
    }
    return true;
}

// The application-owned form, as Create/Open click handler writes form_handlers.hpp.
class Form final : public lithe::simulator::Form::Instance<>
{
public:
    [[nodiscard]] bool clicked() const noexcept
    {
        return mClicked;
    }

protected:
    void onButton1Clicked() noexcept override
    {
        mClicked = true;
        getButton1()->setText("PRESSED");
    }

private:
    bool mClicked{false};
};

class FrameDisplay final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return {mFrame, Configuration::kFormat, kExtent.width, kExtent.height, kStrideBytes};
    }

    void present([[maybe_unused]] std::span<const lithe::Rect> regions) noexcept override
    {
    }

    [[nodiscard]] std::span<const std::byte> frame() const noexcept
    {
        return mFrame;
    }

private:
    std::array<std::byte, kFrameBytes> mFrame{};
};

// Static application objects: resources outlive the form, and the application is destroyed
// first, before the form whose controller it borrows.
// NOLINTBEGIN(cppcoreguidelines-avoid-non-const-global-variables)
lithe::simulator::SelectedResources gResources;
FrameDisplay gDisplay;
Form gForm;
lithe::Application gApplication{gDisplay, Configuration::kViewport};
// NOLINTEND(cppcoreguidelines-avoid-non-const-global-variables)

} // namespace

int main()
{
    if (!gForm.build(lithe::font5x7(), &gResources) || gForm.controller() == nullptr ||
        !gApplication.pushScene(*gForm.controller()) || !gApplication.render())
    {
        std::cerr << "FAIL the generated native form did not build and render\n";
        return 1;
    }
    const std::span<const std::byte> frame = gDisplay.frame();
    std::size_t checked = 0U;
    std::size_t mismatches = 0U;
    for (std::int32_t row = 0; row < kRows; ++row)
    {
        for (std::int32_t column = 0; column < kColumns; ++column)
        {
            std::uint32_t expected = 0U;
            if (!expectedByte(column, row, expected))
            {
                continue;
            }
            ++checked;
            const std::size_t offset = (lithe::detail::nonnegativeSize(row) * kStrideBytes) +
                                       lithe::detail::nonnegativeSize(column);
            if (std::to_integer<std::uint32_t>(frame[offset]) != expected)
            {
                ++mismatches;
            }
        }
    }

    // Logical select activates the focused button and runs the handwritten handler.
    gForm.controller()->activate();
    const bool selected = gApplication.handleKey(lithe::Key::select);
    const bool rendered = gApplication.render();
    std::cout << "native bytes checked " << checked << ", mismatches " << mismatches << ", frame "
              << kFrameBytes << " bytes\n";
    const bool passed = checked != 0U && mismatches == 0U && selected && gForm.clicked() && rendered;
    std::cout << "RESULT " << (passed ? "PASS" : "FAIL") << '\n';
    return passed ? 0 : 1;
}
