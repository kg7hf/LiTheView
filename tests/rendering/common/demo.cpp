// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "demo.hpp"

#include "litheview/build_info.hpp"

#include <array>
#include <string_view>

namespace lithe::demo
{

namespace
{

const Font& demoFont() noexcept
{
    return builtInFont<config::kFontBackend>();
}

const Font& demoBoldFont() noexcept
{
    return builtInBoldFont<config::kFontBackend>();
}

void adaptTextScales(std::span<ViewSpec> specs) noexcept
{
    if constexpr (config::kFontBackend == BuiltInFontBackend::generatedSans)
    {
        for (ViewSpec& spec : specs)
        {
            if (spec.style.textScale == 2U)
            {
                spec.style.textScale = 1U;
            }
            else if (spec.style.textScale > 2U)
            {
                spec.style.textScale = 2U;
            }
        }
    }
}

constexpr auto kGearBits = std::to_array<std::uint8_t>({
    0x03, 0xc0, 0x0f, 0xf0, 0x1c, 0x38, 0x39, 0x9c, 0x73, 0xce, 0x67, 0xe6, 0xce, 0x73, 0xcc, 0x33,
    0xcc, 0x33, 0xce, 0x73, 0x67, 0xe6, 0x73, 0xce, 0x39, 0x9c, 0x1c, 0x38, 0x0f, 0xf0, 0x03, 0xc0,
});

constexpr auto kTuneBits = std::to_array<std::uint8_t>({
    0x18, 0x18, 0x18, 0x18, 0x7e, 0x7e, 0x18, 0x18, 0x18, 0x18, 0x00, 0x00, 0x06, 0x60, 0x06, 0x60,
    0x7f, 0xe0, 0x7f, 0xe0, 0x06, 0x60, 0x06, 0x60, 0x00, 0x00, 0x30, 0x0c, 0x30, 0x0c, 0xff, 0xff,
});

constexpr auto kInfoBits = std::to_array<std::uint8_t>({
    0x07, 0xe0, 0x1f, 0xf8, 0x38, 0x1c, 0x70, 0x0e, 0x60, 0x06, 0xc1, 0x83, 0xc1, 0x83, 0xc0, 0x03,
    0xc1, 0x83, 0xc1, 0x83, 0xc1, 0x83, 0x60, 0x06, 0x70, 0x0e, 0x38, 0x1c, 0x1f, 0xf8, 0x07, 0xe0,
});

constexpr std::array<std::string_view, 3> kProfileNames{"ECO", "BALANCED", "PERFORMANCE"};

// The layout remains ordinary ROM data. It uses LiTheView class names where they
// exist and generic extension attributes for the new portable controls.
constexpr std::string_view kMenuXml = R"xml(
<object className="LCDVIEW" frame="0,0,480,272" runtimeID="1" backgroundColor="0x00FBF6F4">
  <object className="LCDLABELVIEW" frame="0,0,480,42" runtimeID="10"
          backgroundColor="0x0057351D" textColor="16777215" font="ui-bold"
          textAlignmentFlags="14" textScale="3" text="LITHEVIEW"/>
  <object className="LCDICONVIEW" frame="10,5,32,32" runtimeID="11" imageName="gear"/>
  <object className="LCDSCROLLVIEW" frame="16,48,448,176" runtimeID="50"
          contentHeight="276" scrollStep="40" showsIndicator="1"
          backgroundColor="0x00FBF6F4" accentColor="0x00DF6C2D" borderColor="0x00E3D2C9">
    <object className="LCDBUTTONVIEW" frame="0,0,438,36" runtimeID="100"
            backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
            borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
            text="WIDGET GALLERY" detail="ANIMATION + INPUT"/>
    <object className="LCDBUTTONVIEW" frame="0,40,438,36" runtimeID="101"
            backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
            borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
            text="BRIGHTNESS" detail="75%"/>
    <object className="LCDBUTTONVIEW" frame="0,80,438,36" runtimeID="102"
            backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
            borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
            text="VOLUME" detail="60%"/>
    <object className="LCDBUTTONVIEW" frame="0,120,438,36" runtimeID="103"
            backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
            borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
            text="POWER PROFILE" detail="BALANCED"/>
    <object className="LCDBUTTONVIEW" frame="0,160,438,36" runtimeID="104"
            backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
            borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
            text="APPEARANCE" detail="LIGHT"/>
    <object className="LCDBUTTONVIEW" frame="0,200,438,36" runtimeID="105"
            backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
            borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
            text="XML &amp; MVC" detail="ROUND TRIP"/>
    <object className="LCDBUTTONVIEW" frame="0,240,438,36" runtimeID="106"
            backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
            borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
            text="ABOUT" detail="M33 READY"/>
  </object>
  <object className="LCDLINEVIEW" frame="0,228,480,2" runtimeID="90"
          textColor="0x00E3D2C9" borderWidth="2"/>
  <object className="LCDSOFTKEYVIEW" frame="0,230,480,42" runtimeID="60"
          left_text="INFO" right_text="OPEN" leftAction="106" rightAction="100"
          backgroundColor="0x00FBF6F4" textColor="0x00504A42" accentColor="0x00DF6C2D"
          font="ui-bold" textScale="2"/>
</object>
)xml";

constexpr std::string_view kAdjustmentXml = R"xml(
<object className="LCDVIEW" frame="0,0,480,272" runtimeID="1" backgroundColor="0x00FBF6F4">
  <object className="LCDLABELVIEW" frame="0,0,480,42" runtimeID="10"
          backgroundColor="0x0057351D" textColor="16777215" font="ui-bold"
          textAlignmentFlags="14" textScale="3" text="SYSTEM CONTROLS"/>
  <object className="LCDICONVIEW" frame="10,5,32,32" runtimeID="12" imageName="tune"/>
  <object className="LCDLABELVIEW" frame="20,50,440,24" runtimeID="30"
          backgroundColor="0x00FBF6F4" textColor="0" font="ui-bold"
          textAlignmentFlags="14" textScale="2" text="BRIGHTNESS 75%"/>
  <object className="LCDPROGRESSVIEW" frame="20,78,440,22" runtimeID="31"
          backgroundColor="0x00E3D2C9" accentColor="0x00DF6C2D"
          borderColor="0x0057351D" borderWidth="1" value="75"/>
  <object className="LCDBUTTONVIEW" frame="20,108,100,34" runtimeID="201"
          backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
          borderColor="0x00E3D2C9" borderWidth="1" textAlignmentFlags="14"
          textScale="2" text="- 10"/>
  <object className="LCDBUTTONVIEW" frame="360,108,100,34" runtimeID="202"
          backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
          borderColor="0x00E3D2C9" borderWidth="1" textAlignmentFlags="14"
          textScale="2" text="+ 10"/>
  <object className="LCDLABELVIEW" frame="20,150,440,20" runtimeID="32"
          backgroundColor="0x00FBF6F4" textColor="0x00504A42" font="ui-bold"
          textAlignmentFlags="12" textScale="1" text="POWER PROFILE - LEFT / RIGHT"/>
  <object className="LCDPICKERVIEW" frame="20,176,440,42" runtimeID="205"
          backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
          borderColor="0x00E3D2C9" borderWidth="1" textScale="2" font="ui-bold"
          items="ECO|BALANCED|PERFORMANCE" selectedIndex="1" wrap="1"/>
  <object className="LCDLINEVIEW" frame="0,228,480,2" runtimeID="90"
          textColor="0x00E3D2C9" borderWidth="2"/>
  <object className="LCDSOFTKEYVIEW" frame="0,230,480,42" runtimeID="61"
          left_text="BACK" right_text="APPLY" leftAction="203" rightAction="204"
          backgroundColor="0x00FBF6F4" textColor="0x00504A42" accentColor="0x00DF6C2D"
          font="ui-bold" textScale="2"/>
</object>
)xml";

constexpr std::string_view kAboutXml = R"xml(
<object className="LCDVIEW" frame="0,0,480,272" runtimeID="1" backgroundColor="0x00FBF6F4">
  <object className="LCDLABELVIEW" frame="0,0,480,42" runtimeID="10"
          backgroundColor="0x0057351D" textColor="16777215" font="ui-bold"
          textAlignmentFlags="14" textScale="3" text="BUILD INFO"/>
  <object className="LCDICONVIEW" frame="10,5,32,32" runtimeID="13" imageName="info"/>
  <object className="LCDLABELVIEW" frame="18,50,444,28" runtimeID="40"
          backgroundColor="0x00FBF6F4" textColor="0" font="ui-bold"
          textScale="2" text="VERSION: UNKNOWN"/>
  <object className="LCDLABELVIEW" frame="18,82,444,20" runtimeID="41"
          backgroundColor="0x00FBF6F4" textColor="0x00504A42"
          textScale="1" text="TARGET: UNKNOWN"/>
  <object className="LCDLABELVIEW" frame="18,105,444,20" runtimeID="42"
          backgroundColor="0x00FBF6F4" textColor="0x00504A42"
          textScale="1" text="BRANCH: UNKNOWN"/>
  <object className="LCDLABELVIEW" frame="18,128,444,20" runtimeID="43"
          backgroundColor="0x00FBF6F4" textColor="0x00504A42"
          textScale="1" text="COMMIT: UNKNOWN"/>
  <object className="LCDLABELVIEW" frame="18,151,444,20" runtimeID="44"
          backgroundColor="0x00FBF6F4" textColor="0x00504A42"
          textScale="1" text="BUILT: UNKNOWN"/>
  <object className="LCDIMAGEVIEW" frame="192,153,96,72" runtimeID="45"
          animationInterval="90" animationImage0="gallop-f0"
          animationImage1="gallop-f1" animationImage2="gallop-f2"
          animationImage3="gallop-f3" animationImage4="gallop-f4"
          animationImage5="gallop-f5" animationImage6="gallop-f6"
          animationImage7="gallop-f7"/>
  <object className="LCDLINEVIEW" frame="0,228,480,2" runtimeID="90"
          textColor="0x00E3D2C9" borderWidth="2"/>
  <object className="LCDSOFTKEYVIEW" frame="0,230,480,42" runtimeID="62"
          right_text="BACK" rightAction="301" backgroundColor="0x00FBF6F4"
          textColor="0x00504A42" accentColor="0x00DF6C2D" font="ui-bold" textScale="2"/>
</object>
)xml";

constexpr std::string_view kGalleryXml = R"xml(
<object className="LCDVIEW" frame="0,0,480,272" runtimeID="1" backgroundColor="0x00FBF6F4">
  <object className="LCDLABELVIEW" frame="0,0,480,42" runtimeID="10"
          backgroundColor="0x0057351D" textColor="16777215" font="ui-bold"
          textAlignmentFlags="14" textScale="3" text="WIDGET GALLERY"/>
  <object className="LCDLABELVIEW" frame="18,51,160,20" runtimeID="410"
          textColor="0x00504A42" font="ui-bold" textScale="1" text="GALLOP ANIMATION"/>
  <object className="LCDIMAGEVIEW" frame="50,64,120,72" runtimeID="411"
          animationInterval="90" animationImage0="gallop-f0"
          animationImage1="gallop-f1" animationImage2="gallop-f2"
          animationImage3="gallop-f3" animationImage4="gallop-f4"
          animationImage5="gallop-f5" animationImage6="gallop-f6"
          animationImage7="gallop-f7"/>
  <object className="LCDCHECKBOXVIEW" frame="190,52,270,34" runtimeID="401"
          backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
          borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
          text="ENABLE ALERTS" checked="1"/>
  <object className="LCDRADIOBUTTONVIEW" frame="190,90,130,34" runtimeID="402"
          backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
          borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
          text="AUTO" selected="1"/>
  <object className="LCDRADIOBUTTONVIEW" frame="330,90,130,34" runtimeID="403"
          backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
          borderColor="0x00E3D2C9" borderWidth="1" textScale="2"
          text="MANUAL" selected="0"/>
  <object className="LCDLABELVIEW" frame="20,137,440,18" runtimeID="412"
          textColor="0x00504A42" font="ui-bold" textScale="1"
          text="EDITABLE CALLER-OWNED BUFFER"/>
  <object className="LCDTEXTFIELDVIEW" frame="20,160,440,42" runtimeID="404"
          backgroundColor="16777215" textColor="0" accentColor="0x00DF6C2D"
          borderColor="0x0057351D" borderWidth="1" font="ui-bold" textScale="2"
          binding="device-name" placeholder="TYPE A DEVICE NAME" maxLength="20"/>
  <object className="LCDLINEVIEW" frame="0,228,480,2" runtimeID="90"
          textColor="0x00E3D2C9" borderWidth="2"/>
  <object className="LCDSOFTKEYVIEW" frame="0,230,480,42" runtimeID="413"
          right_text="BACK" rightAction="405" backgroundColor="0x00FBF6F4"
          textColor="0x00504A42" accentColor="0x00DF6C2D" font="ui-bold" textScale="2"/>
</object>
)xml";

} // namespace

DemoResources::DemoResources() noexcept
{
    configure(mGear, std::as_bytes(std::span{kGearBits}));
    configure(mTune, std::as_bytes(std::span{kTuneBits}));
    configure(mInfo, std::as_bytes(std::span{kInfoBits}));
}

const Bitmap* DemoResources::bitmap(std::string_view name) const noexcept
{
    if (name == "gear")
    {
        return &mGear;
    }
    if (name == "tune")
    {
        return &mTune;
    }
    if (name == "info")
    {
        return &mInfo;
    }
    return mGallopResources.bitmap(name);
}

const Font* DemoResources::font(std::string_view name) const noexcept
{
    if (name == "ui-regular")
    {
        return &demoFont();
    }
    if (name == "ui-bold")
    {
        return &demoBoldFont();
    }
    return nullptr;
}

MutableTextBuffer DemoResources::textBuffer(std::string_view name) const noexcept
{
    return name == "device-name" ? MutableTextBuffer{mDeviceName} : MutableTextBuffer{};
}

void DemoResources::resetText() noexcept
{
    constexpr std::string_view kInitial{"LITHEVIEW"};
    std::size_t index = 0U;
    for (; index + 1U < mDeviceName.size() && index < kInitial.size(); ++index)
    {
        mDeviceName[index] = kInitial[index];
    }
    mDeviceName[index] = '\0';
}

void DemoResources::configure(Bitmap& bitmap, std::span<const std::byte> data) noexcept
{
    bitmap.data = data;
    bitmap.width = kIconDimension;
    bitmap.height = kIconDimension;
    bitmap.strideBytes = kIconStrideBytes;
    bitmap.format = BitmapFormat::alpha1;
    bitmap.tint = Color::rgb(kIconTintRgb);
}

Demo::Demo(Display* display) noexcept : mApplication(kViewport, display)
{
}

bool Demo::initialize() noexcept
{
    if (mInitialized)
    {
        return false;
    }
    mResources.resetText();

    const XmlLayoutResult menuParse = XmlLayoutParser::parse(kMenuXml, mParseWorkspace);
    if (!menuParse)
    {
        return false;
    }
    adaptTextScales(std::span{mParseWorkspace}.first(menuParse.nodeCount));
    if (!mMenuLayout.build(std::span<const ViewSpec>{mParseWorkspace}.first(menuParse.nodeCount),
                           demoFont(),
                           &mResources))
    {
        return false;
    }
    const XmlLayoutResult adjustmentParse = XmlLayoutParser::parse(kAdjustmentXml, mParseWorkspace);
    if (!adjustmentParse)
    {
        return false;
    }
    adaptTextScales(std::span{mParseWorkspace}.first(adjustmentParse.nodeCount));
    if (!mAdjustmentLayout.build(
            std::span<const ViewSpec>{mParseWorkspace}.first(adjustmentParse.nodeCount),
            demoFont(),
            &mResources))
    {
        return false;
    }
    const XmlLayoutResult aboutParse = XmlLayoutParser::parse(
        kAboutXml, mParseWorkspace, XmlParseOptions{false, mAboutAnimationFrames});
    if (!aboutParse)
    {
        return false;
    }
    adaptTextScales(std::span{mParseWorkspace}.first(aboutParse.nodeCount));
    if (!mAboutLayout.build(std::span<const ViewSpec>{mParseWorkspace}.first(aboutParse.nodeCount),
                            demoFont(),
                            &mResources,
                            std::span<const std::string_view>{mAboutAnimationFrames}.first(
                                aboutParse.animationFrameCount)))
    {
        return false;
    }
    const XmlLayoutResult galleryParse = XmlLayoutParser::parse(
        kGalleryXml, mParseWorkspace, XmlParseOptions{false, mGalleryAnimationFrames});
    if (!galleryParse)
    {
        return false;
    }
    adaptTextScales(std::span{mParseWorkspace}.first(galleryParse.nodeCount));
    if (!mGalleryLayout.build(
            std::span<const ViewSpec>{mParseWorkspace}.first(galleryParse.nodeCount),
            demoFont(),
            &mResources,
            std::span<const std::string_view>{mGalleryAnimationFrames}.first(
                galleryParse.animationFrameCount)))
    {
        return false;
    }

    ControllerDelegate& delegate = *this;
    mMenuController.emplace(*mMenuLayout.root(), delegate);
    mAdjustmentController.emplace(*mAdjustmentLayout.root(), delegate);
    mAboutController.emplace(*mAboutLayout.root(), delegate);
    mGalleryController.emplace(*mGalleryLayout.root(), delegate);
    if (!bindBuildInfo())
    {
        return false;
    }
    refreshModelViews();
    applyTheme(*mMenuLayout.root());
    applyTheme(*mAdjustmentLayout.root());
    applyTheme(*mAboutLayout.root());
    applyTheme(*mGalleryLayout.root());
    mInitialized = mApplication.push(*mMenuController);
    return mInitialized;
}

bool Demo::bindBuildInfo() noexcept
{
    auto* version = mAboutLayout.find<LabelView>(kBuildVersionLabelId);
    auto* variant = mAboutLayout.find<LabelView>(kBuildVariantLabelId);
    auto* branch = mAboutLayout.find<LabelView>(kBuildBranchLabelId);
    auto* commit = mAboutLayout.find<LabelView>(kBuildCommitLabelId);
    auto* timestamp = mAboutLayout.find<LabelView>(kBuildTimestampLabelId);
    if (version == nullptr || variant == nullptr || branch == nullptr || commit == nullptr ||
        timestamp == nullptr)
    {
        return false;
    }
    version->setText(buildinfo::kVersionText);
    variant->setText(buildinfo::kVariantText);
    branch->setText(buildinfo::kBranchText);
    commit->setText(buildinfo::kCommitText);
    timestamp->setText(buildinfo::kTimestampText);
    return true;
}

void Demo::formatPercent(std::array<char, kPercentTextCapacity>& output,
                         std::int32_t value) noexcept
{
    std::size_t position = 0U;
    if (value >= kMaximumAdjustment)
    {
        output[position++] = '1';
        output[position++] = '0';
        output[position++] = '0';
    }
    else
    {
        if (value >= kDecimalBase)
        {
            char tens = '0';
            while (value >= kDecimalBase)
            {
                ++tens;
                value -= kDecimalBase;
            }
            output[position++] = tens;
        }
        char ones = '0';
        while (value > 0)
        {
            ++ones;
            --value;
        }
        output[position++] = ones;
    }
    output[position++] = '%';
    output[position] = '\0';
}

void Demo::refreshModelViews() noexcept
{
    formatPercent(mBrightnessText, mBrightness);
    formatPercent(mVolumeText, mVolume);
    if (auto* button = mMenuLayout.find<ButtonView>(101U))
    {
        button->setDetail(mBrightnessText.data());
    }
    if (auto* button = mMenuLayout.find<ButtonView>(102U))
    {
        button->setDetail(mVolumeText.data());
    }
    if (auto* button = mMenuLayout.find<ButtonView>(103U))
    {
        button->setDetail(kProfileNames[mProfileIndex]);
    }
    if (auto* button = mMenuLayout.find<ButtonView>(104U))
    {
        button->setDetail(mDarkTheme ? "DARK" : "LIGHT");
    }
}

void Demo::configureAdjustment(Adjustment adjustment) noexcept
{
    mAdjustment = adjustment;
    const std::int32_t value = adjustment == Adjustment::brightness ? mBrightness : mVolume;
    if (auto* label = mAdjustmentLayout.find<LabelView>(30U))
    {
        label->setText(adjustment == Adjustment::brightness ? mBrightnessText.data()
                                                            : mVolumeText.data());
    }
    if (auto* progress = mAdjustmentLayout.find<ProgressView>(31U))
    {
        progress->setValue(value);
    }
    if (auto* picker = mAdjustmentLayout.find<PickerView>(205U))
    {
        picker->setSelectedIndex(mProfileIndex);
    }
}

void Demo::applyTheme(View& root) noexcept
{
    for (std::size_t index = 0; index < root.subviewCount(); ++index)
    {
        if (View* child = root.subview(index))
        {
            applyTheme(*child);
        }
    }
    Style style = root.style();
    if (root.id() == 1U || root.id() == 30U || (root.id() >= 32U && root.id() <= 44U) ||
        root.id() == 50U)
    {
        style.background = mDarkTheme ? Color::rgb(0x172033U) : Color::rgb(0xf4f6fbU);
        style.foreground = mDarkTheme ? Color::rgb(0xe9eef8U) : Color::rgb(0x000000U);
    }
    if (root.id() == 10U)
    {
        style.background = mDarkTheme ? Color::rgb(0x0d1422U) : Color::rgb(0x1d3557U);
        style.foreground = Color::rgb(0xffffffU);
    }
    if (root.id() == 90U)
    {
        style.foreground = mDarkTheme ? Color::rgb(0x435473U) : Color::rgb(0xc9d2e3U);
    }
    if (root.focusable())
    {
        style.background = mDarkTheme ? Color::rgb(0x26344fU) : Color::rgb(0xffffffU);
        style.foreground = mDarkTheme ? Color::rgb(0xffffffU) : Color::rgb(0x000000U);
        style.border = mDarkTheme ? Color::rgb(0x435473U) : Color::rgb(0xc9d2e3U);
        style.accent = Color::rgb(0x2d6cdfU);
    }
    root.setStyle(style);
}

void Demo::pushController(ViewController& controller) noexcept
{
    if (!mApplication.push(controller))
    {
        mInitialized = false;
    }
}

void Demo::popController() noexcept
{
    if (!mApplication.pop())
    {
        mInitialized = false;
    }
}

void Demo::onAction(ViewController& controller, ViewId action) noexcept
{
    if (!mMenuController.has_value() || !mAdjustmentController.has_value() ||
        !mAboutController.has_value() || !mGalleryController.has_value())
    {
        return;
    }

    ViewController& menuController = *mMenuController;
    ViewController& adjustmentController = *mAdjustmentController;
    ViewController& aboutController = *mAboutController;
    ViewController& galleryController = *mGalleryController;
    if (&controller == &menuController)
    {
        if (action == kGalleryAction)
        {
            pushController(galleryController);
        }
        else if (action == kBrightnessAction || action == kVolumeAction ||
                 action == kAlternateBrightnessAction)
        {
            configureAdjustment(action == kVolumeAction ? Adjustment::volume
                                                        : Adjustment::brightness);
            pushController(adjustmentController);
        }
        else if (action == kThemeAction)
        {
            mDarkTheme = !mDarkTheme;
            applyTheme(*mMenuLayout.root());
            applyTheme(*mAdjustmentLayout.root());
            applyTheme(*mAboutLayout.root());
            applyTheme(*mGalleryLayout.root());
            refreshModelViews();
        }
        else if (action == kAboutAction || action == kAlternateAboutAction)
        {
            pushController(aboutController);
        }
        return;
    }

    if (&controller == &galleryController)
    {
        if (action == kAutomaticModeAction || action == kManualModeAction)
        {
            auto* automatic = mGalleryLayout.find<RadioButtonView>(kAutomaticModeAction);
            auto* manual = mGalleryLayout.find<RadioButtonView>(kManualModeAction);
            if (automatic != nullptr)
            {
                automatic->setSelected(action == kAutomaticModeAction);
            }
            if (manual != nullptr)
            {
                manual->setSelected(action == kManualModeAction);
            }
        }
        else if (action == kCloseGalleryAction)
        {
            popController();
        }
        return;
    }

    if (&controller == &adjustmentController)
    {
        if (action == kCancelAdjustmentAction)
        {
            if (auto* picker = mAdjustmentLayout.find<PickerView>(kProfilePickerId))
            {
                picker->setSelectedIndex(mProfileIndex);
            }
            popController();
            return;
        }
        if (action == kApplyAdjustmentAction)
        {
            if (auto* picker = mAdjustmentLayout.find<PickerView>(kProfilePickerId))
            {
                mProfileIndex = picker->selectedIndex();
            }
            refreshModelViews();
            popController();
            return;
        }
        std::int32_t& value = mAdjustment == Adjustment::brightness ? mBrightness : mVolume;
        if (action == kDecrementAction)
        {
            value = value >= kAdjustmentStep ? value - kAdjustmentStep : 0;
        }
        else if (action == kIncrementAction)
        {
            value = value <= kMaximumBeforeIncrement ? value + kAdjustmentStep : kMaximumAdjustment;
        }
        refreshModelViews();
        configureAdjustment(mAdjustment);
        return;
    }

    if (&controller == &aboutController && action == kCloseAboutAction)
    {
        popController();
    }
}

void Demo::onBack(ViewController& controller) noexcept
{
    if (mMenuController.has_value() && &controller != &*mMenuController)
    {
        popController();
    }
}

} // namespace lithe::demo
