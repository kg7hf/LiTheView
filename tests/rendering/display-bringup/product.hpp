// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <lithe/application.hpp>
#include <lithe/display_definition.hpp>
#include <lithe/render_config.hpp>
#include <lithe/surface_layout.hpp>
#include <lithe/tiled_renderer.hpp>
#include <lithe/widgets.hpp>

namespace display_bringup
{

inline constexpr lithe::DisplayDefinition kDisplay{"example.display-bringup.rgb565",
                                                   {128, 96},
                                                   lithe::ViewportShape::rectangle,
                                                   {0, 0, 128, 96},
                                                   lithe::PixelFormat::rgb565,
                                                   lithe::DisplayAddressSpace::pixels};
inline constexpr lithe::LogicalInputConfiguration kInput{.pointer = true, .keys = true};
inline constexpr lithe::Rect kButtonFrame{26, 70, 76, 18};
inline constexpr std::size_t kStrideBytes{256U};
inline constexpr std::int32_t kTileHeight{4};
inline constexpr auto kFrameLayout =
    lithe::storageLayout(kDisplay.format, kDisplay.logicalSize, kStrideBytes);
inline constexpr auto kTileLayout =
    lithe::storageLayout(kDisplay.format, {kDisplay.logicalSize.width, kTileHeight}, kStrideBytes);
static_assert(lithe::validateDisplayDefinition(kDisplay));
static_assert(lithe::validateSurfacePlacement(kDisplay,
                                              kButtonFrame,
                                              lithe::SurfacePlacementPolicy::safeArea));
static_assert(lithe::validateSurfaceInput(kInput, {.activation = true}));
static_assert(kFrameLayout && kTileLayout);
inline constexpr std::size_t kFrameBytes{kFrameLayout->usedBytes};
inline constexpr std::size_t kTileBytes{kTileLayout->usedBytes};
static_assert(kFrameBytes == 24576U && kTileBytes == 1024U);
static_assert(lithe::render_config::kRgb565Enabled && !lithe::render_config::kRgb888Enabled &&
                  !lithe::render_config::kXrgb8888Enabled &&
                  !lithe::render_config::kArgb8888Enabled &&
                  !lithe::render_config::kR2Packed2x2Enabled &&
                  !lithe::render_config::kRrrr2222Enabled &&
                  !lithe::render_config::kRyry2222Enabled,
              "Install a separate RGB565-only Core package for this product");

class PatternView final : public lithe::View
{
public:
    PatternView() noexcept : View(kDisplay.safeArea)
    {
    }

    void setActivated(bool value) noexcept
    {
        mActivated = value;
        invalidate();
    }

protected:
    void drawSelf(lithe::Painter& painter, lithe::Rect frame) noexcept override
    {
        using lithe::Color;
        const auto white = Color::rgb(0xffffffU);
        painter.fillRect(frame, Color::rgb(0x102030U));
        painter.strokeRect(frame, white);
        // Unequal labeled corners expose mirroring, rotation, and clipped edges.
        painter.drawText({3, 2, 20, 10}, "TL", lithe::font5x7(), white);
        painter.drawText({105, 2, 20, 10}, "TR", lithe::font5x7(), white);
        painter.drawText({3, 84, 20, 10}, "BL", lithe::font5x7(), white);
        painter.drawText({105, 84, 20, 10}, "BR", lithe::font5x7(), white);
        painter.fillRect({8, 16, 36, 18}, Color::rgb(0xff0000U));
        painter.fillRect({44, 16, 40, 18}, Color::rgb(0x00ff00U));
        painter.fillRect({84, 16, 36, 18}, Color::rgb(0x0000ffU));
        constexpr std::array grays{
            0x000000U, 0x242424U, 0x494949U, 0x6d6d6dU, 0x929292U, 0xb6b6b6U, 0xdbdbdbU, 0xffffffU};
        std::int32_t x = 8;
        for (const auto gray : grays)
        {
            painter.fillRect({x, 38, 14, 12}, Color::rgb(gray));
            x += 14;
        }
        painter.fillRect({8, 54, 112, 12}, Color::rgb(mActivated ? 0x006020U : 0x202020U));
        painter.drawText(
            {12, 55, 104, 10}, mActivated ? "ACTIVE" : "READY", lithe::font5x7(), white);
    }

private:
    bool mActivated{false};
};

// Product owns the view/controller/application graph. The caller supplies all
// display storage and transport. Keep this object alive for every render/input call.
class Product final : public lithe::ControllerDelegate
{
public:
    [[nodiscard]] bool initialize() noexcept
    {
        if (mInitialized)
        {
            return true;
        }
        mButton.setId(kActivateAction);
        mInitialized = mRoot.addSubview(mButton) && mApplication.pushScene(mController);
        return mInitialized;
    }

    bool handleKey(lithe::Key key) noexcept
    {
        const bool available =
            kInput.keys || (kInput.encoder && (key == lithe::Key::up || key == lithe::Key::down)) ||
            (kInput.select && key == lithe::Key::select);
        return available && mApplication.handleKey(key);
    }

    bool handlePointer(lithe::PointerEvent event) noexcept
    {
        return kInput.pointer && mApplication.handlePointer(event);
    }

    [[nodiscard]] bool render(lithe::Display& display) noexcept
    {
        return mApplication.render(display);
    }

    [[nodiscard]] lithe::TileRenderResult renderTiled(lithe::Surface workspace,
                                                      lithe::TileSink& sink) noexcept
    {
        return lithe::TiledRenderer::render(mApplication, kDisplay.logicalSize, workspace, sink);
    }

    [[nodiscard]] bool activated() const noexcept
    {
        return mActivated;
    }

private:
    void onAction([[maybe_unused]] lithe::ViewController& controller,
                  lithe::ViewId action) noexcept override
    {
        if (action == kActivateAction)
        {
            mActivated = !mActivated;
            mRoot.setActivated(mActivated);
            mButton.setText(mActivated ? "RESET" : "ACTIVATE");
        }
    }

    void onBack([[maybe_unused]] lithe::ViewController& controller) noexcept override
    {
    }

    static constexpr lithe::ViewId kActivateAction{1U};
    PatternView mRoot{};
    lithe::ButtonView mButton{kButtonFrame, lithe::font5x7(), "ACTIVATE"};
    lithe::ViewController mController{mRoot, *this};
    lithe::Application mApplication{*lithe::displayViewport(kDisplay)};
    bool mInitialized{false};
    bool mActivated{false};
};

} // namespace display_bringup
