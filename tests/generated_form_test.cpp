// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "generated_form_test.hpp"

#include "alarm_time.hpp"
#include "button_variants.hpp"
#include "lithe/painter.hpp"
#include "lithe/xml_layout.hpp"
#include "main_form.hpp"
#include "tab_settings.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <string_view>
#include <utility>

namespace lithe::tests
{
namespace
{

constexpr std::int32_t kSurfaceWidth{480};
constexpr std::int32_t kSurfaceHeight{272};
constexpr std::size_t kDeviceNameCapacity{24U};
constexpr std::size_t kRootIndex{0U};
constexpr std::size_t kOpenButtonIndex{1U};
constexpr std::size_t kNameFieldIndex{2U};
constexpr std::size_t kSoftkeyIndex{3U};
constexpr std::size_t kExpectedViewCount{4U};
constexpr ViewId kBackAction{100U};
constexpr ViewId kOpenAction{101U};
constexpr std::size_t kPixelCount{detail::nonnegativeSize(kSurfaceWidth) *
                                  detail::nonnegativeSize(kSurfaceHeight)};
constexpr std::size_t kExpectedButtonViewCount{7U};
constexpr ViewId kStartAction{10U};
constexpr ViewId kHomeAction{11U};
constexpr ViewId kAlarmAction{12U};
constexpr ViewId kMuteAction{13U};
constexpr std::size_t kServiceButtonIndex{6U};
constexpr std::size_t kExpectedTabViewCount{12U};
constexpr ViewId kSettingsAction{20U};
constexpr ViewId kModeAction{30U};
constexpr std::size_t kSettingsTabCount{3U};
constexpr std::uint16_t kSettingsTabHeight{32U};
constexpr std::size_t kExpectedWheelViewCount{7U};
constexpr ViewId kHourAction{40U};
constexpr ViewId kMinuteAction{41U};
constexpr ViewId kRepeatAction{42U};
constexpr std::size_t kHourCount{24U};
constexpr std::size_t kMinuteCount{12U};
constexpr std::size_t kRepeatCount{4U};
constexpr std::uint8_t kRepeatRowHeight{32U};
constexpr std::int32_t kHomeIconSize{16};
constexpr std::size_t kHomeIconStrideBytes{2U};
// A 16-by-16 alpha1 house, most-significant bit first, supplied by the application.
constexpr std::array<std::uint8_t, 32> kHomeIconBits{
    0x01U, 0x80U, 0x03U, 0xc0U, 0x07U, 0xe0U, 0x0fU, 0xf0U, 0x1fU, 0xf8U, 0x3fU,
    0xfcU, 0x7fU, 0xfeU, 0xffU, 0xffU, 0x3fU, 0xfcU, 0x3cU, 0x3cU, 0x3cU, 0x3cU,
    0x3cU, 0x3cU, 0x3cU, 0x3cU, 0x3cU, 0x3cU, 0x3fU, 0xfcU, 0x00U, 0x00U,
};

class FormResources final : public ResourceProvider
{
public:
    const Bitmap* bitmap([[maybe_unused]] std::string_view name) const noexcept override
    {
        return nullptr;
    }

    MutableTextBuffer textBuffer(std::string_view name) const noexcept override
    {
        return name == "device-name" ? MutableTextBuffer{mText} : MutableTextBuffer{};
    }

private:
    mutable std::array<char, kDeviceNameCapacity> mText{};
};

class ButtonFormResources final : public ResourceProvider
{
public:
    ButtonFormResources() noexcept
    {
        mIcon.data = std::as_bytes(std::span{kHomeIconBits});
        mIcon.width = kHomeIconSize;
        mIcon.height = kHomeIconSize;
        mIcon.strideBytes = kHomeIconStrideBytes;
        mIcon.format = BitmapFormat::alpha1;
    }

    const Bitmap* bitmap(std::string_view name) const noexcept override
    {
        return name == "home-icon" ? &mIcon : nullptr;
    }

private:
    Bitmap mIcon{};
};

bool sameColor(Color lhs, Color rhs) noexcept
{
    return lhs.legacyValue() == rhs.legacyValue();
}

bool sameRect(Rect lhs, Rect rhs) noexcept
{
    return lhs.x == rhs.x && lhs.y == rhs.y && lhs.width == rhs.width && lhs.height == rhs.height;
}

bool sameStates(const WidgetStateStyle& lhs, const WidgetStateStyle& rhs) noexcept
{
    return sameColor(lhs.focusedBackground, rhs.focusedBackground) &&
           sameColor(lhs.focusedForeground, rhs.focusedForeground) &&
           sameColor(lhs.pressedBackground, rhs.pressedBackground) &&
           sameColor(lhs.pressedForeground, rhs.pressedForeground) &&
           sameColor(lhs.disabledBackground, rhs.disabledBackground) &&
           sameColor(lhs.disabledForeground, rhs.disabledForeground) &&
           sameColor(lhs.selectedBackground, rhs.selectedBackground) &&
           sameColor(lhs.selectedForeground, rhs.selectedForeground);
}

bool sameStyle(Style lhs, Style rhs) noexcept
{
    if ((lhs.states == nullptr) != (rhs.states == nullptr))
    {
        return false;
    }
    const bool statesMatch = lhs.states == nullptr || sameStates(*lhs.states, *rhs.states);
    return statesMatch && sameColor(lhs.background, rhs.background) &&
           sameColor(lhs.foreground, rhs.foreground) && sameColor(lhs.accent, rhs.accent) &&
           sameColor(lhs.border, rhs.border) &&
           lhs.textAlign.horizontal == rhs.textAlign.horizontal &&
           lhs.textAlign.vertical == rhs.textAlign.vertical && lhs.textScale == rhs.textScale &&
           lhs.borderWidth == rhs.borderWidth;
}

bool sameStructuralFields(const ViewSpec& lhs, const ViewSpec& rhs) noexcept
{
    return lhs.kind == rhs.kind && lhs.parent == rhs.parent && lhs.id == rhs.id &&
           sameRect(lhs.frame, rhs.frame) && sameStyle(lhs.style, rhs.style) &&
           lhs.resource == rhs.resource && lhs.font == rhs.font &&
           lhs.leftAction == rhs.leftAction && lhs.rightAction == rhs.rightAction &&
           lhs.animationFrameOffset == rhs.animationFrameOffset &&
           lhs.animationFrameCount == rhs.animationFrameCount &&
           lhs.animationIntervalMs == rhs.animationIntervalMs &&
           lhs.animationRepeatIndex == rhs.animationRepeatIndex && lhs.maxLength == rhs.maxLength &&
           lhs.contentHeight == rhs.contentHeight && lhs.scrollOffset == rhs.scrollOffset &&
           lhs.scrollStep == rhs.scrollStep && lhs.selectedIndex == rhs.selectedIndex &&
           lhs.value == rhs.value && lhs.wraps == rhs.wraps &&
           lhs.showsIndicator == rhs.showsIndicator &&
           lhs.animationAutoplay == rhs.animationAutoplay && lhs.checked == rhs.checked &&
           lhs.visible == rhs.visible && lhs.enabled == rhs.enabled &&
           lhs.focusable == rhs.focusable && lhs.buttonBehavior == rhs.buttonBehavior &&
           lhs.tabHeight == rhs.tabHeight && lhs.rowHeight == rhs.rowHeight;
}

bool sameText(const ViewSpec& lhs, const ViewSpec& rhs) noexcept
{
    return lhs.text == rhs.text && lhs.detail == rhs.detail;
}

template <std::size_t Capacity, typename Resources>
bool renderMatches(std::span<const ViewSpec> parsed,
                   std::span<const ViewSpec> generatedSpecs) noexcept
{
    StaticLayout<Capacity> parsedLayout{};
    StaticLayout<Capacity> generatedLayout{};
    Resources parsedResources{};
    Resources generatedResources{};
    if (!parsedLayout.build(parsed, font5x7(), &parsedResources) ||
        !generatedLayout.build(generatedSpecs, font5x7(), &generatedResources))
    {
        return false;
    }

    static std::array<Pixel, kPixelCount> parsedPixels{};
    static std::array<Pixel, kPixelCount> generatedPixels{};
    std::fill(parsedPixels.begin(), parsedPixels.end(), Pixel{});
    std::fill(generatedPixels.begin(), generatedPixels.end(), Pixel{});
    DamageTracker parsedDamage{};
    DamageTracker generatedDamage{};
    Painter parsedPainter{Surface{parsedPixels, kSurfaceWidth, kSurfaceHeight, kSurfaceWidth},
                          parsedDamage};
    Painter generatedPainter{Surface{generatedPixels, kSurfaceWidth, kSurfaceHeight, kSurfaceWidth},
                             generatedDamage};
    parsedLayout.root()->render(parsedPainter, {}, true);
    generatedLayout.root()->render(generatedPainter, {}, true);
    return parsedPixels == generatedPixels;
}

} // namespace

bool generatedFormValid() noexcept
{
    using Form = generated::MainForm;
    static_assert(Form::kViewCount == kExpectedViewCount);
    static_assert(std::to_underlying(Form::Action::back) == kBackAction);
    static_assert(std::to_underlying(Form::Action::open) == kOpenAction);
    // The form declares exactly the kinds it uses, so its Layout links only that widget code.
    static_assert(Form::Widgets::size == 4U && Form::Widgets::contains(ViewKind::textField) &&
                  Form::Widgets::contains(ViewKind::softkey) &&
                  !Form::Widgets::contains(ViewKind::label));

    std::array<ViewSpec, Form::kViewCount> parsed{};
    std::array<std::string_view, 1U> parsedFrames{};
    std::array<WidgetStateStyle, Form::kViewCount> parsedStateStyles{};
    const XmlLayoutResult result = XmlLayoutParser::parse(
        Form::kSourceXml, parsed, XmlParseOptions{true, parsedFrames, parsedStateStyles});
    if (!result || result.nodeCount != Form::kViewCount || result.animationFrameCount != 0U)
    {
        return false;
    }

    const std::span<const ViewSpec> generatedSpecs = Form::views();
    for (std::size_t index = 0U; index < parsed.size(); ++index)
    {
        if (!sameStructuralFields(parsed[index], generatedSpecs[index]))
        {
            return false;
        }
    }
    return parsed[kRootIndex].text == generatedSpecs[kRootIndex].text &&
           parsed[kOpenButtonIndex].text == generatedSpecs[kOpenButtonIndex].text &&
           parsed[kOpenButtonIndex].detail == generatedSpecs[kOpenButtonIndex].detail &&
           parsed[kNameFieldIndex].text == generatedSpecs[kNameFieldIndex].text &&
           parsed[kNameFieldIndex].detail == generatedSpecs[kNameFieldIndex].detail &&
           parsed[kSoftkeyIndex].text == generatedSpecs[kSoftkeyIndex].text &&
           parsed[kSoftkeyIndex].detail == generatedSpecs[kSoftkeyIndex].detail &&
           renderMatches<Form::kViewCount, FormResources>(parsed, generatedSpecs);
}

bool generatedButtonFormValid() noexcept
{
    using Form = generated::ButtonVariants;
    static_assert(Form::kViewCount == kExpectedButtonViewCount);
    static_assert(std::to_underlying(Form::Action::start) == kStartAction);
    static_assert(Form::Widgets::size == 3U && Form::Widgets::contains(ViewKind::button) &&
                  !Form::Widgets::contains(ViewKind::picker));
    static_assert(std::to_underlying(Form::Action::home) == kHomeAction);
    static_assert(std::to_underlying(Form::Action::alarm) == kAlarmAction);
    static_assert(std::to_underlying(Form::Action::mute) == kMuteAction);

    std::array<ViewSpec, Form::kViewCount> parsed{};
    std::array<WidgetStateStyle, Form::kViewCount> parsedStateStyles{};
    const XmlLayoutResult result = XmlLayoutParser::parse(
        Form::kSourceXml, parsed, XmlParseOptions{true, {}, parsedStateStyles});
    if (!result || result.nodeCount != Form::kViewCount)
    {
        return false;
    }
    const std::span<const ViewSpec> generatedSpecs = Form::views();
    for (std::size_t index = 0U; index < parsed.size(); ++index)
    {
        if (!sameStructuralFields(parsed[index], generatedSpecs[index]) ||
            !sameText(parsed[index], generatedSpecs[index]))
        {
            return false;
        }
    }
    if (!renderMatches<Form::kViewCount, ButtonFormResources>(parsed, generatedSpecs))
    {
        return false;
    }

    // The generated layout carries the variant contract, not only matching pixels.
    Form::Layout layout{};
    ButtonFormResources resources{};
    if (!layout.build(generatedSpecs, font5x7(), &resources))
    {
        return false;
    }
    const ButtonView* home = layout.find<ButtonView>(kHomeAction);
    const ButtonView* alarm = layout.find<ButtonView>(kAlarmAction);
    const ButtonView* mute = layout.find<ButtonView>(kMuteAction);
    return home != nullptr && alarm != nullptr && mute != nullptr &&
           home->behavior() == ButtonBehavior::momentary && home->icon() != nullptr &&
           alarm->behavior() == ButtonBehavior::toggle && alarm->selected() &&
           mute->behavior() == ButtonBehavior::toggle && !mute->selected() &&
           mute->icon() != nullptr && !generatedSpecs[kServiceButtonIndex].enabled;
}

bool generatedTabFormValid() noexcept
{
    using Form = generated::TabSettings;
    static_assert(Form::kViewCount == kExpectedTabViewCount);
    static_assert(std::to_underlying(Form::Action::settings) == kSettingsAction);
    static_assert(std::to_underlying(Form::Action::mode) == kModeAction);
    static_assert(Form::Widgets::size == 7U && Form::Widgets::contains(ViewKind::tabView) &&
                  !Form::Widgets::contains(ViewKind::wheelPicker));

    std::array<ViewSpec, Form::kViewCount> parsed{};
    std::array<WidgetStateStyle, Form::kViewCount> parsedStateStyles{};
    const XmlLayoutResult result = XmlLayoutParser::parse(
        Form::kSourceXml, parsed, XmlParseOptions{true, {}, parsedStateStyles});
    if (!result || result.nodeCount != Form::kViewCount)
    {
        return false;
    }
    const std::span<const ViewSpec> generatedSpecs = Form::views();
    for (std::size_t index = 0U; index < parsed.size(); ++index)
    {
        if (!sameStructuralFields(parsed[index], generatedSpecs[index]) ||
            !sameText(parsed[index], generatedSpecs[index]))
        {
            return false;
        }
    }
    if (!renderMatches<Form::kViewCount, FormResources>(parsed, generatedSpecs))
    {
        return false;
    }

    // The generated layout carries the tab contract: titles, strip height, and page visibility.
    Form::Layout layout{};
    FormResources resources{};
    if (!layout.build(generatedSpecs, font5x7(), &resources))
    {
        return false;
    }
    const TabView* settings = layout.find<TabView>(kSettingsAction);
    const TabBarView* mode = layout.find<TabBarView>(kModeAction);
    if (settings == nullptr || mode == nullptr || settings->subviewCount() != kSettingsTabCount)
    {
        return false;
    }
    return settings->tabCount() == kSettingsTabCount &&
           settings->tabHeight() == kSettingsTabHeight && settings->selectedIndex() == 0U &&
           settings->subview(0U)->visible() && !settings->subview(1U)->visible() &&
           !settings->subview(2U)->visible() && mode->tabCount() == kSettingsTabCount &&
           !mode->wraps();
}

bool generatedWheelFormValid() noexcept
{
    using Form = generated::AlarmTime;
    static_assert(Form::kViewCount == kExpectedWheelViewCount);
    static_assert(std::to_underlying(Form::Action::hour) == kHourAction);
    static_assert(std::to_underlying(Form::Action::repeat) == kRepeatAction);
    static_assert(Form::Widgets::size == 4U && Form::Widgets::contains(ViewKind::wheelPicker) &&
                  !Form::Widgets::contains(ViewKind::tabBar));

    std::array<ViewSpec, Form::kViewCount> parsed{};
    std::array<WidgetStateStyle, Form::kViewCount> parsedStateStyles{};
    const XmlLayoutResult result = XmlLayoutParser::parse(
        Form::kSourceXml, parsed, XmlParseOptions{true, {}, parsedStateStyles});
    if (!result || result.nodeCount != Form::kViewCount)
    {
        return false;
    }
    const std::span<const ViewSpec> generatedSpecs = Form::views();
    for (std::size_t index = 0U; index < parsed.size(); ++index)
    {
        if (!sameStructuralFields(parsed[index], generatedSpecs[index]) ||
            !sameText(parsed[index], generatedSpecs[index]))
        {
            return false;
        }
    }
    if (!renderMatches<Form::kViewCount, FormResources>(parsed, generatedSpecs))
    {
        return false;
    }

    // The generated layout carries the wheel contract: items, selection, row height, and wrap.
    Form::Layout layout{};
    FormResources resources{};
    if (!layout.build(generatedSpecs, font5x7(), &resources))
    {
        return false;
    }
    const WheelPickerView* hour = layout.find<WheelPickerView>(kHourAction);
    const WheelPickerView* minute = layout.find<WheelPickerView>(kMinuteAction);
    const WheelPickerView* repeat = layout.find<WheelPickerView>(kRepeatAction);
    if (hour == nullptr || minute == nullptr || repeat == nullptr)
    {
        return false;
    }
    return hour->itemCount() == kHourCount && hour->selectedItem() == "07" && hour->wraps() &&
           hour->rowHeight() == detail::kDefaultWheelRowHeightPixels &&
           minute->itemCount() == kMinuteCount && minute->selectedItem() == "30" &&
           repeat->itemCount() == kRepeatCount && repeat->selectedItem() == "DAILY" &&
           !repeat->wraps() && repeat->rowHeight() == kRepeatRowHeight;
}

} // namespace lithe::tests
