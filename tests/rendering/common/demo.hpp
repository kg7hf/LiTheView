// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "display_definitions.hpp"
#include "lithe/lithe.hpp"
#include "litheview/demo_font_config.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <span>
#include <string_view>

namespace lithe::demo
{

class DemoResources final : public ResourceProvider
{
public:
    DemoResources() noexcept;

    const Bitmap* bitmap(std::string_view name) const noexcept override;
    const Font* font(std::string_view name) const noexcept override;
    MutableTextBuffer textBuffer(std::string_view name) const noexcept override;
    void resetText() noexcept;

private:
    static constexpr std::size_t kDeviceNameCapacity{24U};
    static constexpr std::uint16_t kIconDimension{16U};
    static constexpr std::size_t kIconStrideBytes{2U};
    static constexpr std::uint32_t kIconTintRgb{0xffffffU};

    static void configure(Bitmap& bitmap, std::span<const std::byte> data) noexcept;

    Bitmap mGear{};
    Bitmap mTune{};
    Bitmap mInfo{};
    GallopResources<config::kAnimationFormat> mGallopResources;
    mutable std::array<char, kDeviceNameCapacity> mDeviceName{};
};

class Demo final : private ControllerDelegate
{
public:
    static constexpr std::int32_t kWidth = 480;
    static constexpr std::int32_t kHeight = 272;

    static constexpr Viewport kViewport{display_definitions::kStandardViewport};
    static_assert(kViewport.valid());
    static_assert(kViewport.bounds.width == kWidth && kViewport.bounds.height == kHeight);

    // The same scene can use a tiled sink or a caller-supplied full-frame display.
    Demo() noexcept : Demo(nullptr)
    {
    }
    explicit Demo(Display& display) noexcept : Demo(&display)
    {
    }

    bool initialize() noexcept;
    bool initialized() const noexcept
    {
        return mInitialized;
    }
    bool render() noexcept
    {
        return mApplication.render();
    }
    [[nodiscard]] bool render(Display& display) noexcept
    {
        return mApplication.render(display);
    }
    TileRenderResult renderTiled(TileSink& sink, Surface workspace) noexcept
    {
        return TiledRenderer::render(
            mApplication, {kWidth, kHeight}, workspace, sink, Color::rgb(0x000000U));
    }
    bool handleKey(Key key) noexcept
    {
        return mApplication.handleKey(key);
    }
    bool handleTextInput(char32_t codepoint) noexcept
    {
        return mApplication.handleTextInput(codepoint);
    }
    bool handlePointer(PointerEvent event) noexcept
    {
        return mApplication.handlePointer(event);
    }
    bool handlePointer(Point point) noexcept
    {
        return mApplication.handlePointer(point);
    }
    bool advanceTime(std::uint32_t elapsedMs) noexcept
    {
        return mApplication.advanceTime(elapsedMs);
    }
    bool animationsRunning() const noexcept
    {
        return mApplication.animationsRunning();
    }

private:
    explicit Demo(Display* display) noexcept;

    static constexpr std::size_t kPercentTextCapacity{6U};
    static constexpr std::size_t kLayoutCapacity{16U};
    static constexpr std::size_t kAnimationFrameCapacity{8U};
    static constexpr std::int32_t kDefaultBrightness{75};
    static constexpr std::int32_t kDefaultVolume{60};
    static constexpr std::int32_t kAdjustmentStep{10};
    static constexpr std::int32_t kMaximumBeforeIncrement{90};
    static constexpr std::int32_t kMaximumAdjustment{100};
    static constexpr std::int32_t kDecimalBase{10};
    static constexpr ViewId kBuildVersionLabelId{40U};
    static constexpr ViewId kBuildVariantLabelId{41U};
    static constexpr ViewId kBuildBranchLabelId{42U};
    static constexpr ViewId kBuildCommitLabelId{43U};
    static constexpr ViewId kBuildTimestampLabelId{44U};
    static constexpr ViewId kGalleryAction{100U};
    static constexpr ViewId kBrightnessAction{101U};
    static constexpr ViewId kVolumeAction{102U};
    static constexpr ViewId kAlternateBrightnessAction{103U};
    static constexpr ViewId kThemeAction{104U};
    static constexpr ViewId kAboutAction{105U};
    static constexpr ViewId kAlternateAboutAction{106U};
    static constexpr ViewId kDecrementAction{201U};
    static constexpr ViewId kIncrementAction{202U};
    static constexpr ViewId kCancelAdjustmentAction{203U};
    static constexpr ViewId kApplyAdjustmentAction{204U};
    static constexpr ViewId kProfilePickerId{205U};
    static constexpr ViewId kCloseAboutAction{301U};
    static constexpr ViewId kAutomaticModeAction{402U};
    static constexpr ViewId kManualModeAction{403U};
    static constexpr ViewId kCloseGalleryAction{405U};

    enum class Adjustment : std::uint8_t
    {
        brightness,
        volume
    };

    void onAction(ViewController& controller, ViewId action) noexcept override;
    void onBack(ViewController& controller) noexcept override;
    bool bindBuildInfo() noexcept;
    void refreshModelViews() noexcept;
    void configureAdjustment(Adjustment adjustment) noexcept;
    void applyTheme(View& root) noexcept;
    void pushController(ViewController& controller) noexcept;
    void popController() noexcept;
    static void formatPercent(std::array<char, kPercentTextCapacity>& output,
                              std::int32_t value) noexcept;

    Application mApplication;
    DemoResources mResources;
    // XML specs are construction-only, so all screens reuse one bounded parse
    // workspace instead of retaining three arrays for the life of the demo.
    std::array<ViewSpec, kLayoutCapacity> mParseWorkspace{};
    std::array<std::string_view, kAnimationFrameCapacity> mAboutAnimationFrames{};
    std::array<std::string_view, kAnimationFrameCapacity> mGalleryAnimationFrames{};
    StaticLayout<kLayoutCapacity> mMenuLayout;
    StaticLayout<kLayoutCapacity> mAdjustmentLayout;
    StaticLayout<kLayoutCapacity> mAboutLayout;
    StaticLayout<kLayoutCapacity> mGalleryLayout;
    std::optional<ViewController> mMenuController;
    std::optional<ViewController> mAdjustmentController;
    std::optional<ViewController> mAboutController;
    std::optional<ViewController> mGalleryController;
    std::array<char, kPercentTextCapacity> mBrightnessText{};
    std::array<char, kPercentTextCapacity> mVolumeText{};
    std::int32_t mBrightness{kDefaultBrightness};
    std::int32_t mVolume{kDefaultVolume};
    std::size_t mProfileIndex{1U};
    bool mDarkTheme{false};
    bool mInitialized{false};
    Adjustment mAdjustment{Adjustment::brightness};
};

} // namespace lithe::demo
