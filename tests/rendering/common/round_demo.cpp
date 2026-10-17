// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "round_demo.hpp"

#include "litheview/build_info.hpp"
#include "litheview/demo_font_config.hpp"

#include <algorithm>
#include <array>
#include <string_view>

namespace lithe::demo
{

namespace
{

constexpr ViewId kMoreAction{700U};
constexpr ViewId kGestureLabBackAction{701U};
constexpr ViewId kCloseOverlayAction{702U};
constexpr std::uint32_t kTransitionDurationMs{240U};
constexpr Color kCanvas{Color::rgb(0x050914U)};
constexpr Color kPanel{Color::rgb(0x111a2eU)};
constexpr Color kPrimary{Color::rgb(0x40e0c0U)};
constexpr Color kSecondary{Color::rgb(0x7c6cffU)};
constexpr Color kAccent{Color::rgb(0xff4f7bU)};
constexpr Color kText{Color::rgb(0xf6f8ffU)};
constexpr Color kMuted{Color::rgb(0x8f9bb8U)};
constexpr std::int32_t kGestureMinimumX{38};
constexpr std::int32_t kGestureMaximumX{202};
constexpr std::int32_t kGestureMinimumY{58};
constexpr std::int32_t kGestureMaximumY{132};

const Font& demoRegularFont() noexcept
{
    return builtInFont<config::kFontBackend>();
}

const Font& demoBoldFont() noexcept
{
    return builtInBoldFont<config::kFontBackend>();
}

constexpr std::uint8_t titleScale() noexcept
{
    return config::kFontBackend == BuiltInFontBackend::compact5x7 ? 2U : 1U;
}

Style buttonStyle() noexcept
{
    Style style{};
    style.background = kPanel;
    style.foreground = kText;
    style.accent = kPrimary;
    style.border = kSecondary;
    style.borderWidth = 1U;
    style.textAlign = {HorizontalAlign::center, VerticalAlign::center};
    style.textScale = titleScale();
    return style;
}

Style transparentStyle() noexcept
{
    Style style{};
    style.background = Color::transparent();
    return style;
}

constexpr std::string_view gestureName(GestureType type) noexcept
{
    switch (type)
    {
    case GestureType::tap:
        return "TAP";
    case GestureType::longPress:
        return "LONG PRESS";
    case GestureType::dragStarted:
        return "DRAG START";
    case GestureType::dragged:
        return "DRAGGING";
    case GestureType::dragEnded:
        return "DRAG END";
    case GestureType::swipeLeft:
        return "SWIPE LEFT";
    case GestureType::swipeRight:
        return "SWIPE RIGHT";
    case GestureType::swipeUp:
        return "SWIPE UP";
    case GestureType::swipeDown:
        return "SWIPE DOWN";
    case GestureType::cancelled:
        return "CANCELLED";
    }
    return "UNKNOWN";
}

} // namespace

RoundSceneView::RoundSceneView(RoundSceneKind kind, const Font& regular, const Font& bold) noexcept
    : View({0, 0, RoundDemo::kWidth, RoundDemo::kHeight},
           Style{kCanvas, kText, kPrimary, Color::transparent(), {}, 1U, 0U}),
      mKind(kind), mRegular(regular), mBold(bold)
{
}

void RoundSceneView::setGestureFeedback(GestureEvent event) noexcept
{
    if (mKind != RoundSceneKind::gestureLab)
    {
        return;
    }
    mGesturePosition = {std::clamp(event.position.x, kGestureMinimumX, kGestureMaximumX),
                        std::clamp(event.position.y, kGestureMinimumY, kGestureMaximumY)};
    mLastGesture = event.type;
    mGestureReceived = true;
    invalidate();
}

void RoundSceneView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    const Point center{detail::saturatingAdd(absoluteFrame.x, absoluteFrame.width / 2),
                       detail::saturatingAdd(absoluteFrame.y, 74)};
    const TextAlign centered{HorizontalAlign::center, VerticalAlign::center};
    if (mKind == RoundSceneKind::dashboard)
    {
        painter.fillCircle(center, 38, kPrimary);
        painter.fillCircle(center, 31, kCanvas);
        painter.drawText({absoluteFrame.x + 76, absoluteFrame.y + 50, 88, 34},
                         "72",
                         mBold,
                         kText,
                         centered,
                         titleScale());
        painter.drawText({absoluteFrame.x + 72, absoluteFrame.y + 88, 96, 18},
                         "HEART POINTS",
                         mRegular,
                         kPrimary,
                         centered);
        painter.fillCircle({absoluteFrame.x + 74, absoluteFrame.y + 126}, 14, kSecondary);
        painter.fillCircle({absoluteFrame.x + 120, absoluteFrame.y + 126}, 14, kAccent);
        painter.fillCircle({absoluteFrame.x + 166, absoluteFrame.y + 126}, 14, kPrimary);
        painter.drawText({absoluteFrame.x + 32, absoluteFrame.y + 108, 176, 36},
                         "32       486       18",
                         mRegular,
                         kText,
                         centered);
        painter.drawText({absoluteFrame.x + 42, absoluteFrame.y + 183, 156, 14},
                         "SWIPE LEFT: GESTURES",
                         mRegular,
                         kMuted,
                         centered);
    }
    else
    {
        painter.drawText({absoluteFrame.x + 40, absoluteFrame.y + 24, 160, 24},
                         "GESTURE LAB",
                         mBold,
                         kText,
                         centered,
                         titleScale());
        const Rect gestureArea{absoluteFrame.x + kGestureMinimumX,
                               absoluteFrame.y + kGestureMinimumY,
                               kGestureMaximumX - kGestureMinimumX + 1,
                               kGestureMaximumY - kGestureMinimumY + 1};
        painter.strokeRect(gestureArea, kMuted);
        painter.drawLine({absoluteFrame.x + 120, gestureArea.y},
                         {absoluteFrame.x + 120, gestureArea.bottom() - 1},
                         kPanel);
        painter.drawLine({gestureArea.x, absoluteFrame.y + 95},
                         {gestureArea.right() - 1, absoluteFrame.y + 95},
                         kPanel);
        const Point marker{absoluteFrame.x + mGesturePosition.x,
                           absoluteFrame.y + mGesturePosition.y};
        painter.fillCircle(marker, 10, kSecondary);
        painter.fillCircle(marker, 6, kCanvas);
        painter.fillCircle(marker, 3, kPrimary);
        painter.drawText({absoluteFrame.x + 42, absoluteFrame.y + 140, 156, 16},
                         "TAP OR DRAG MARKER",
                         mRegular,
                         kPrimary,
                         centered);
        painter.drawText({absoluteFrame.x + 42, absoluteFrame.y + 157, 156, 16},
                         mGestureReceived ? gestureName(mLastGesture) : "WAITING FOR INPUT",
                         mRegular,
                         kMuted,
                         centered);
    }

    painter.drawText({absoluteFrame.x + 42, absoluteFrame.y + 212, 156, 14},
                     "MORE: BUILD INFO",
                     mRegular,
                     kMuted,
                     centered);
}

RoundOverlayView::RoundOverlayView(const Font& regular, const Font& bold) noexcept
    : View({20, 74, 200, 146}, Style{kPanel, kText, kPrimary, kSecondary, {}, 1U, 1U}),
      mRegular(regular), mBold(bold)
{
}

void RoundOverlayView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    const TextAlign centered{HorizontalAlign::center, VerticalAlign::center};
    painter.fillCircle(
        {absoluteFrame.x + (absoluteFrame.width / 2), absoluteFrame.y + 9}, 3, kMuted);
    painter.drawText({absoluteFrame.x + 20, absoluteFrame.y + 12, absoluteFrame.width - 40, 20},
                     "BUILD INFO",
                     mBold,
                     kText,
                     centered,
                     titleScale());
    TextLayoutOptions options{};
    options.align = centered;
    options.overflow = TextOverflow::ellipsis;
    options.maxLines = 1U;
    const std::array<std::string_view, 5> buildLines{buildinfo::kVersionText,
                                                     buildinfo::kVariantText,
                                                     buildinfo::kBranchText,
                                                     buildinfo::kCommitText,
                                                     buildinfo::kTimestampText};
    std::int32_t lineY = absoluteFrame.y + 34;
    for (const std::string_view line : buildLines)
    {
        painter.drawTextLayout({absoluteFrame.x + 14, lineY, absoluteFrame.width - 28, 13},
                               line,
                               mRegular,
                               kMuted,
                               options);
        lineY += 13;
    }
}

RoundDemo::RoundDemo(Display* display) noexcept
    : mApplication(kViewport, display),
      mDashboard(RoundSceneKind::dashboard, demoRegularFont(), demoBoldFont()),
      mGestureLab(RoundSceneKind::gestureLab, demoRegularFont(), demoBoldFont()),
      mOverlayRoot({0, 0, kWidth, kHeight}, transparentStyle()),
      mOverlayPanel(demoRegularFont(), demoBoldFont()),
      mMoreButton({70, 148, 100, 32}, demoBoldFont(), "MORE", {}, buttonStyle()),
      mGestureLabBackButton({70, 184, 100, 28}, demoBoldFont(), "BACK", {}, buttonStyle()),
      mCloseButton({50, 110, 100, 26}, demoBoldFont(), "CLOSE", {}, buttonStyle()),
      mDashboardController(mDashboard, *this), mGestureLabController(mGestureLab, *this),
      mOverlayController(mOverlayRoot, *this)
{
    mMoreButton.setId(kMoreAction);
    mGestureLabBackButton.setId(kGestureLabBackAction);
    mCloseButton.setId(kCloseOverlayAction);
}

std::expected<SurfacePopupPlacement, SurfaceLayoutError> RoundDemo::overlayPlacement() noexcept
{
    return placeSurfacePopup(display_definitions::kRound240,
                             {20, 74, 200, 146},
                             SurfacePopupFallback::centeredSafeArea,
                             Size{168, 146});
}

bool RoundDemo::initialize() noexcept
{
    if (mInitialized)
    {
        return false;
    }
    const auto placement = overlayPlacement();
    if (!placement)
    {
        return false;
    }
    mOverlayPanel.setFrame(placement->frame);
    mCloseButton.setFrame({(placement->frame.width - 100) / 2, 110, 100, 26});
    if (!mDashboard.addSubview(mMoreButton) || !mGestureLab.addSubview(mGestureLabBackButton) ||
        !mOverlayRoot.addSubview(mOverlayPanel) || !mOverlayPanel.addSubview(mCloseButton))
    {
        return false;
    }
    mInitialized = mApplication.pushScene(mDashboardController);
    return mInitialized;
}

bool RoundDemo::showGestureLab() noexcept
{
    return mApplication.pushScene(mGestureLabController,
                                  {SceneTransition::slideLeft, kTransitionDurationMs});
}

bool RoundDemo::showOverlay() noexcept
{
    return mApplication.pushOverlay(mOverlayController,
                                    {SceneTransition::slideUp, kTransitionDurationMs});
}

bool RoundDemo::dismissTop() noexcept
{
    ViewController* const active = mApplication.activeController();
    if (active == &mOverlayController)
    {
        return mApplication.popOverlay({SceneTransition::dropAway, kTransitionDurationMs});
    }
    if (active == &mGestureLabController)
    {
        return mApplication.popScene({SceneTransition::slideRight, kTransitionDurationMs});
    }
    return false;
}

void RoundDemo::onAction([[maybe_unused]] ViewController& controller, ViewId action) noexcept
{
    if (action == kMoreAction)
    {
        showOverlay();
    }
    else if (action == kGestureLabBackAction || action == kCloseOverlayAction)
    {
        dismissTop();
    }
}

void RoundDemo::onBack([[maybe_unused]] ViewController& controller) noexcept
{
    dismissTop();
}

bool RoundDemo::onGesture(ViewController& controller, GestureEvent event) noexcept
{
    // A button release may begin a transition before Application dispatches
    // the gesture recognized from the same event. Once navigation starts, the
    // button action owns that event and the outgoing scene is left unchanged.
    if (mApplication.transitionRunning())
    {
        return false;
    }
    if (&controller == &mDashboardController && event.type == GestureType::swipeLeft)
    {
        return showGestureLab();
    }
    if (&controller == &mDashboardController &&
        (event.type == GestureType::swipeUp || event.type == GestureType::longPress))
    {
        return showOverlay();
    }
    if (&controller == &mGestureLabController && event.type == GestureType::swipeRight)
    {
        return dismissTop();
    }
    if (&controller == &mGestureLabController)
    {
        mGestureLab.setGestureFeedback(event);
        return true;
    }
    if (&controller == &mOverlayController && event.type == GestureType::swipeDown)
    {
        return dismissTop();
    }
    return false;
}

} // namespace lithe::demo
