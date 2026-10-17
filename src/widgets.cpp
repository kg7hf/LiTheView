// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/widgets.hpp"

#include "lithe/detail/index.hpp"
#include "lithe/detail/number.hpp"
#include "lithe/layout.hpp"

#include <algorithm>
#include <array>
#include <cstring>
#include <functional>
#include <limits>

namespace lithe
{

namespace
{

// Button content insets match the original text button, so text-only momentary
// buttons keep their established pixels.
constexpr std::int32_t kButtonContentInset{12};
constexpr std::int32_t kButtonIconTextGap{6};
// A toggle draws a latch bar above its bottom edge: filled while selected and
// outlined otherwise, in the resolved foreground. The value therefore remains
// visible in every widget state and on single-color panels.
constexpr std::int32_t kToggleIndicatorThickness{3};
constexpr std::int32_t kToggleIndicatorMargin{4};
constexpr std::int32_t kToggleIndicatorMinimumHeight{16};
constexpr std::int32_t kToggleIndicatorMinimumWidth{2 * kToggleIndicatorThickness};
constexpr std::uint8_t kToggleIndicatorOutlineWidth{1U};
// The selected tab carries an underline in its resolved foreground, like the
// toggle latch bar, so the selection stays visible in every state.
constexpr std::int32_t kTabTitleInset{4};
constexpr std::int32_t kTabIndicatorThickness{3};
constexpr std::int32_t kTabIndicatorMaximumInset{8};
// The wheel draws its centered band with one-pixel rules in the accent color and dims
// neighbors by distance, so the selection is visible without relying on the band color.
constexpr std::int32_t kWheelTextInset{6};
constexpr std::int32_t kWheelTapSlop{6};
constexpr std::int32_t kWheelBandRule{1};
constexpr std::uint8_t kWheelNearAlpha{176U};
constexpr std::uint8_t kWheelFarAlpha{96U};
// While editing, bars at both ends of the band mark edit mode without relying on color.
constexpr std::int32_t kWheelEditMarkWidth{3};
constexpr std::int32_t kWheelEditMarkInset{2};
constexpr char kItemSeparator{'|'};

// Pipe-delimited items (tab titles, wheel items) in caller-owned storage, capped at a limit.
std::size_t countDelimitedItems(std::string_view items, std::size_t limit) noexcept
{
    if (items.empty())
    {
        return 0U;
    }
    std::size_t count = 1U;
    for (const char character : items)
    {
        if (character == kItemSeparator)
        {
            ++count;
        }
    }
    return std::min(count, limit);
}

// The caller guarantees that index is below the item count.
std::string_view delimitedItem(std::string_view items, std::size_t index) noexcept
{
    std::size_t start = 0U;
    for (std::size_t current = 0U; current < index; ++current)
    {
        start = items.find(kItemSeparator, start) + 1U;
    }
    const std::size_t separator = items.find(kItemSeparator, start);
    return items.substr(
        start, separator == std::string_view::npos ? items.size() - start : separator - start);
}

constexpr std::int32_t magnitude(std::int32_t value) noexcept
{
    return value < 0 ? -value : value;
}

// Rounds toward negative infinity; divisor is positive.
constexpr std::int32_t floorQuotient(std::int32_t value, std::int32_t divisor) noexcept
{
    return value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
}

// Rounds half away from zero; divisor is positive.
constexpr std::int32_t roundedQuotient(std::int32_t value, std::int32_t divisor) noexcept
{
    return value >= 0 ? (value + (divisor / 2)) / divisor : -((-value + (divisor / 2)) / divisor);
}

} // namespace

LabelView::LabelView(Rect frame, const Font& font, std::string_view text, Style style) noexcept
    : View(frame, style), mFont(font), mText(text)
{
}

void LabelView::setText(std::string_view text) noexcept
{
    if (mText != text)
    {
        mText = text;
        invalidate();
    }
}

void LabelView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    const Style current = resolvedStyle();
    const std::int32_t inset = 4;
    painter.drawText({absoluteFrame.x + inset,
                      absoluteFrame.y,
                      absoluteFrame.width - (inset * 2),
                      absoluteFrame.height},
                     mText,
                     mFont,
                     current.foreground,
                     current.textAlign,
                     current.textScale);
}

ButtonView::ButtonView(Rect frame,
                       const Font& font,
                       std::string_view text,
                       std::string_view detail,
                       Style style) noexcept
    : ControlView(frame, style), mFont(font), mText(text), mDetail(detail)
{
    setFocusable(true);
}

void ButtonView::setText(std::string_view text) noexcept
{
    if (mText != text)
    {
        mText = text;
        invalidate();
    }
}

void ButtonView::setDetail(std::string_view detail) noexcept
{
    if (mDetail != detail)
    {
        mDetail = detail;
        invalidate();
    }
}

void ButtonView::setBehavior(ButtonBehavior behavior) noexcept
{
    if (mBehavior == behavior)
    {
        return;
    }
    mBehavior = behavior;
    if (mBehavior != ButtonBehavior::toggle)
    {
        setSelectedState(false);
    }
    invalidate();
}

bool ButtonView::setSelected(bool selected) noexcept
{
    if (selected && mBehavior != ButtonBehavior::toggle)
    {
        return false;
    }
    setSelectedState(selected);
    return true;
}

void ButtonView::setIcon(const Bitmap& icon) noexcept
{
    if (mIcon != &icon)
    {
        mIcon = &icon;
        invalidate();
    }
}

void ButtonView::clearIcon() noexcept
{
    if (mIcon != nullptr)
    {
        mIcon = nullptr;
        invalidate();
    }
}

void ButtonView::activate() noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled())
    {
        return;
    }
    if (mBehavior == ButtonBehavior::toggle)
    {
        const bool previous = selected();
        setSelectedState(!previous);
        notifyCheckedChanged({previous, selected()});
    }
    onClicked();
}

Rect ButtonView::drawToggleIndicator(Painter& painter,
                                     Rect absoluteFrame,
                                     Color color) const noexcept
{
    const std::int32_t inset = std::min(kButtonContentInset, absoluteFrame.width / 4);
    const Rect indicator{absoluteFrame.x + inset,
                         absoluteFrame.bottom() - kToggleIndicatorMargin -
                             kToggleIndicatorThickness,
                         absoluteFrame.width - (inset * 2),
                         kToggleIndicatorThickness};
    if (absoluteFrame.height < kToggleIndicatorMinimumHeight ||
        indicator.width < kToggleIndicatorMinimumWidth)
    {
        return absoluteFrame;
    }
    if (selected())
    {
        painter.fillRect(indicator, color);
    }
    else
    {
        painter.strokeRect(indicator, color, kToggleIndicatorOutlineWidth);
    }
    return {absoluteFrame.x,
            absoluteFrame.y,
            absoluteFrame.width,
            absoluteFrame.height - kToggleIndicatorMargin - kToggleIndicatorThickness};
}

void ButtonView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style current = resolvedStyle();
    painter.fillRect(absoluteFrame, current.background);
    if (current.borderWidth != 0U && current.border.alpha != 0U)
    {
        painter.strokeRect(absoluteFrame, current.border, current.borderWidth);
    }
    const Rect content = mBehavior == ButtonBehavior::toggle
                             ? drawToggleIndicator(painter, absoluteFrame, current.foreground)
                             : absoluteFrame;
    std::int32_t textLeft = content.x + kButtonContentInset;
    if (mIcon != nullptr)
    {
        Bitmap icon = *mIcon;
        if (icon.format == BitmapFormat::alpha1 || icon.format == BitmapFormat::alpha8)
        {
            icon.tint = current.foreground;
        }
        const std::int32_t iconY = content.y + ((content.height - icon.height) / 2);
        if (mText.empty() && mDetail.empty())
        {
            painter.drawBitmap({content.x + ((content.width - icon.width) / 2), iconY}, icon);
        }
        else
        {
            painter.drawBitmap({textLeft, iconY}, icon);
            textLeft += icon.width + kButtonIconTextGap;
        }
    }
    const std::int32_t contentRight = content.right() - kButtonContentInset;
    painter.drawText(
        {textLeft, content.y, std::max(std::int32_t{0}, contentRight - textLeft), content.height},
        mText,
        mFont,
        current.foreground,
        {HorizontalAlign::left, VerticalAlign::center},
        current.textScale);
    if (!mDetail.empty())
    {
        painter.drawText({content.x + kButtonContentInset,
                          content.y,
                          content.width - (kButtonContentInset * 2),
                          content.height},
                         mDetail,
                         mFont,
                         current.foreground,
                         {HorizontalAlign::right, VerticalAlign::center},
                         current.textScale);
    }
}

CheckboxView::CheckboxView(
    Rect frame, const Font& font, std::string_view text, bool checked, Style style) noexcept
    : ControlView(frame, style), mFont(font), mText(text)
{
    setFocusable(true);
    setSelectedState(checked);
}

void CheckboxView::activate() noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled())
    {
        return;
    }
    const bool previous = checked();
    setChecked(!previous);
    notifyCheckedChanged({previous, checked()});
}

RadioButtonView::~RadioButtonView()
{
    if (mGroup != nullptr)
    {
        mGroup->remove(*this);
    }
}

void RadioButtonView::setSelected(bool value) noexcept
{
    if (mGroup != nullptr)
    {
        mGroup->select(*this, value);
    }
    else
    {
        setSelectedState(value);
    }
}

void RadioButtonView::activate() noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled() || selected())
    {
        return;
    }
    const RadioButtonView* previous = mGroup != nullptr ? mGroup->selected() : nullptr;
    setSelected(true);
    notifyRadioSelectionChanged({previous, this});
}

RadioGroup::~RadioGroup()
{
    while (mCount != 0U)
        remove(*mChoices[mCount - 1U]);
}

std::expected<void, RadioGroupError> RadioGroup::add(RadioButtonView& choice) noexcept
{
    if (choice.mGroup != nullptr)
    {
        return std::unexpected(RadioGroupError::alreadyGrouped);
    }
    if (mCount == kCapacity)
    {
        return std::unexpected(RadioGroupError::capacityExceeded);
    }
    if (choice.selected() && mSelected != nullptr)
    {
        return std::unexpected(RadioGroupError::conflictingSelection);
    }
    mChoices[mCount++] = &choice;
    choice.mGroup = this;
    if (choice.selected())
    {
        mSelected = &choice;
    }
    return {};
}

void RadioGroup::remove(RadioButtonView& choice) noexcept
{
    for (std::size_t index = 0U; index < mCount; ++index)
    {
        if (mChoices[index] != &choice)
        {
            continue;
        }
        mChoices[index] = mChoices[--mCount];
        mChoices[mCount] = nullptr;
        choice.mGroup = nullptr;
        if (mSelected == &choice)
        {
            mSelected = nullptr;
        }
        return;
    }
}

void RadioGroup::select(RadioButtonView& choice, bool value) noexcept
{
    if (value)
    {
        if (mSelected != nullptr)
        {
            mSelected->setSelectedState(false);
        }
        mSelected = &choice;
    }
    else if (mSelected == &choice)
    {
        mSelected = nullptr;
    }
    choice.setSelectedState(value);
}

void CheckboxView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style current = resolvedStyle();
    painter.fillRect(absoluteFrame, current.background);
    const std::int32_t boxSize = absoluteFrame.height > 24 ? 20 : absoluteFrame.height - 4;
    const Rect box{absoluteFrame.x + 8,
                   absoluteFrame.y + ((absoluteFrame.height - boxSize) / 2),
                   boxSize,
                   boxSize};
    painter.fillRect(box, focused() ? current.foreground : current.background);
    painter.strokeRect(box, current.foreground, 2U);
    if (checked())
    {
        const Color mark = current.accent;
        painter.drawLine({box.x + 4, box.y + (box.height / 2)},
                         {box.x + (box.width / 2) - 1, box.bottom() - 5},
                         mark);
        painter.drawLine(
            {box.x + (box.width / 2) - 1, box.bottom() - 5}, {box.right() - 4, box.y + 4}, mark);
    }
    painter.drawText({box.right() + 10,
                      absoluteFrame.y,
                      absoluteFrame.right() - box.right() - 18,
                      absoluteFrame.height},
                     mText,
                     mFont,
                     current.foreground,
                     {HorizontalAlign::left, VerticalAlign::center},
                     current.textScale);
}

RadioButtonView::RadioButtonView(
    Rect frame, const Font& font, std::string_view text, bool selected, Style style) noexcept
    : ControlView(frame, style), mFont(font), mText(text)
{
    setFocusable(true);
    setSelectedState(selected);
}

void RadioButtonView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style current = resolvedStyle();
    painter.fillRect(absoluteFrame, current.background);
    const Point center{absoluteFrame.x + 18, absoluteFrame.y + (absoluteFrame.height / 2)};
    painter.fillCircle(center, 9, current.foreground);
    painter.fillCircle(center, 6, current.background);
    if (selected())
    {
        painter.fillCircle(center, 4, focused() ? current.foreground : current.accent);
    }
    painter.drawText(
        {absoluteFrame.x + 34, absoluteFrame.y, absoluteFrame.width - 42, absoluteFrame.height},
        mText,
        mFont,
        current.foreground,
        {HorizontalAlign::left, VerticalAlign::center},
        current.textScale);
}

TextFieldView::TextFieldView(Rect frame,
                             const Font& font,
                             std::span<char> buffer,
                             std::size_t maxLength,
                             std::string_view placeholder,
                             Style style,
                             std::span<char> editBuffer) noexcept
    : ControlView(frame, style), mFont(font), mBuffer(buffer), mPlaceholder(placeholder),
      mEditBuffer(editBuffer)
{
    if (maxLength != 0U && maxLength < mBuffer.size())
    {
        mBuffer = mBuffer.first(maxLength + 1U);
    }
    if (!mBuffer.empty())
    {
        mBuffer.back() = '\0';
    }
    setFocusable(true);
}

std::size_t TextFieldView::length() const noexcept
{
    if (mBuffer.empty())
    {
        return 0U;
    }
    std::size_t result = 0U;
    while (result < mBuffer.size() && mBuffer[result] != '\0')
    {
        ++result;
    }
    return result < mBuffer.size() ? result : mBuffer.size() - 1U;
}

std::string_view TextFieldView::text() const noexcept
{
    return mBuffer.empty() ? std::string_view{} : std::string_view{mBuffer.data(), length()};
}

bool TextFieldView::setText(std::string_view textValue) noexcept
{
    const InteractionGuard guard{*this};
    if (!guard || mBuffer.empty() || textValue.size() > maxLength() ||
        textValue.find('\0') != std::string_view::npos)
    {
        return false;
    }
    if (!textValue.empty())
    {
        std::memmove(mBuffer.data(), textValue.data(), textValue.size());
    }
    mBuffer[textValue.size()] = '\0';
    mEditing = false;
    invalidate();
    return true;
}

bool TextFieldView::startEdit() noexcept
{
    if (mEditing)
    {
        return true;
    }
    if (mBuffer.empty() || mEditBuffer.size() <= maxLength())
    {
        return false;
    }
    // std::less supplies a total pointer order even for different allocations.
    // Reject overlapping storage before touching either buffer.
    const std::less<const char*> before;
    if (!before(&mBuffer.back(), mEditBuffer.data()) &&
        !before(&mEditBuffer.back(), mBuffer.data()))
    {
        return false;
    }
    std::memcpy(mEditBuffer.data(), mBuffer.data(), length() + 1U);
    mEditing = true;
    return true;
}

std::string_view TextFieldView::initialText() const noexcept
{
    // startEdit established capacity and copied a terminated snapshot. Scanning
    // its bounded storage avoids a length field in every layout slot.
    const std::string_view storage{mEditBuffer.data(), maxLength()};
    return storage.substr(0U, storage.find('\0'));
}

std::expected<void, TextEditError> TextFieldView::beginEdit() noexcept
{
    const InteractionGuard guard{*this};
    if (!guard)
    {
        return std::unexpected(TextEditError::reentrant);
    }
    if (!interactionEnabled())
    {
        return std::unexpected(TextEditError::disabled);
    }
    if (!startEdit())
    {
        return std::unexpected(TextEditError::storageUnavailable);
    }
    return {};
}

bool TextFieldView::acceptsText([[maybe_unused]] std::string_view value) const noexcept
{
    return true;
}

std::expected<void, TextEditError> TextFieldView::commitEdit() noexcept
{
    const InteractionGuard guard{*this};
    if (!guard)
    {
        return std::unexpected(TextEditError::reentrant);
    }
    if (!interactionEnabled())
    {
        return std::unexpected(TextEditError::disabled);
    }
    if (!mEditing)
    {
        return std::unexpected(TextEditError::notEditing);
    }
    if (!acceptsText(text()))
    {
        return std::unexpected(TextEditError::invalidText);
    }
    mEditing = false;
    notifyTextEditCommitted({initialText(), text()});
    return {};
}

void TextFieldView::restoreEdit() noexcept
{
    const auto previousLength = length();
    const std::string_view initial = initialText();
    const bool changed = text() != initial;
    std::memcpy(mBuffer.data(), mEditBuffer.data(), initial.size() + 1U);
    mEditing = false;
    invalidate();
    if (changed)
    {
        notifyTextChanged({previousLength, text()});
    }
    notifyTextEditCancelled({initial, text()});
}

std::expected<void, TextEditError> TextFieldView::cancelEdit() noexcept
{
    const InteractionGuard guard{*this};
    if (!guard)
    {
        return std::unexpected(TextEditError::reentrant);
    }
    if (!mEditing)
    {
        return std::unexpected(TextEditError::notEditing);
    }
    restoreEdit();
    return {};
}

void TextFieldView::activate() noexcept
{
    (void)beginEdit();
}

bool TextFieldView::handleKey(Key key) noexcept
{
    if (key == Key::back && mEditing)
    {
        return cancelEdit().has_value();
    }
    if (key == Key::select && !mEditBuffer.empty())
    {
        // Rejected commits consume Select: they must not become numeric actions.
        const auto result = mEditing ? commitEdit() : beginEdit();
        return result || (result.error() != TextEditError::reentrant &&
                          result.error() != TextEditError::disabled);
    }
    return false;
}

void TextFieldView::focusChanged() noexcept
{
    if (!focused())
    {
        mFocusCleanupPending = true;
        const InteractionGuard guard{*this};
        // The outer guard, if any, performs cleanup after its notification.
    }
}

void TextFieldView::interactionFinished() noexcept
{
    if (mFocusCleanupPending)
    {
        mFocusCleanupPending = false;
        if (!focused() && mEditing)
        {
            restoreEdit();
        }
    }
}

bool TextFieldView::handleTextInput(char32_t codepoint) noexcept
{
    const InteractionGuard guard{*this};
    if (!guard || !interactionEnabled() || mBuffer.empty())
    {
        return false;
    }
    std::size_t currentLength = length();
    const auto previousLength = currentLength;
    if (codepoint == U'\b')
    {
        if (currentLength == 0U)
        {
            return false;
        }
        if (!mEditBuffer.empty() && !startEdit())
        {
            return false;
        }
        --currentLength;
        while (currentLength > 0U && (detail::codeUnit(mBuffer[currentLength]) & 0xc0U) == 0x80U)
        {
            --currentLength;
        }
        mBuffer[currentLength] = '\0';
        invalidate();
        notifyTextChanged({previousLength, text()});
        return true;
    }
    if (codepoint < 0x20U || codepoint > 0x10ffffU ||
        (codepoint >= 0xd800U && codepoint <= 0xdfffU))
    {
        return false;
    }
    std::array<char, 4> encoded{};
    std::size_t encodedLength = 0U;
    if (codepoint <= 0x7fU)
    {
        encoded[encodedLength++] = detail::utf8CodeUnit(codepoint);
    }
    else if (codepoint <= 0x7ffU)
    {
        encoded[encodedLength++] = detail::utf8CodeUnit(0xc0U | (codepoint >> 6U));
        encoded[encodedLength++] = detail::utf8CodeUnit(0x80U | (codepoint & 0x3fU));
    }
    else if (codepoint <= 0xffffU)
    {
        encoded[encodedLength++] = detail::utf8CodeUnit(0xe0U | (codepoint >> 12U));
        encoded[encodedLength++] = detail::utf8CodeUnit(0x80U | ((codepoint >> 6U) & 0x3fU));
        encoded[encodedLength++] = detail::utf8CodeUnit(0x80U | (codepoint & 0x3fU));
    }
    else
    {
        encoded[encodedLength++] = detail::utf8CodeUnit(0xf0U | (codepoint >> 18U));
        encoded[encodedLength++] = detail::utf8CodeUnit(0x80U | ((codepoint >> 12U) & 0x3fU));
        encoded[encodedLength++] = detail::utf8CodeUnit(0x80U | ((codepoint >> 6U) & 0x3fU));
        encoded[encodedLength++] = detail::utf8CodeUnit(0x80U | (codepoint & 0x3fU));
    }
    if (encodedLength > maxLength() - (currentLength <= maxLength() ? currentLength : maxLength()))
    {
        return false;
    }
    if (!mEditBuffer.empty() && !startEdit())
    {
        return false;
    }
    for (std::size_t index = 0U; index < encodedLength; ++index)
    {
        mBuffer[currentLength + index] = encoded[index];
    }
    mBuffer[currentLength + encodedLength] = '\0';
    invalidate();
    notifyTextChanged({previousLength, text()});
    return true;
}

void TextFieldView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style current = resolvedStyle();
    painter.fillRect(absoluteFrame, current.background);
    std::uint8_t borderWidth = current.borderWidth == 0U ? 1U : current.borderWidth;
    if (focused())
    {
        borderWidth = 2U;
    }
    painter.strokeRect(absoluteFrame, focused() ? current.accent : current.border, borderWidth);
    const std::string_view value = text();
    const std::string_view visible = value.empty() ? mPlaceholder : value;
    const Color textColor = value.empty() ? Color::rgb(0x7a8494U) : current.foreground;
    const Rect textFrame{
        absoluteFrame.x + 8, absoluteFrame.y, absoluteFrame.width - 16, absoluteFrame.height};
    painter.drawText(textFrame,
                     visible,
                     mFont,
                     textColor,
                     {HorizontalAlign::left, VerticalAlign::center},
                     current.textScale);
    if (focused())
    {
        const TextMetrics metrics = Painter::measureText(value, mFont, current.textScale);
        std::int32_t caretX = textFrame.x + metrics.width + 1;
        caretX = std::min(caretX, textFrame.right() - 2);
        painter.fillRect({caretX, absoluteFrame.y + 7, 2, absoluteFrame.height - 14},
                         current.accent);
    }
}

ProgressView::ProgressView(Rect frame, std::int32_t value, Style style) noexcept
    : View(frame, style), mValue(std::clamp(value, std::int32_t{0}, std::int32_t{100}))
{
}

void ProgressView::setValue(std::int32_t value) noexcept
{
    const std::int32_t bounded = std::clamp(value, std::int32_t{0}, std::int32_t{100});
    if (bounded != mValue)
    {
        mValue = bounded;
        invalidate();
    }
}

void ProgressView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style current = resolvedStyle();
    painter.fillRect(absoluteFrame, current.background);
    const Rect interior{absoluteFrame.x + 3,
                        absoluteFrame.y + 3,
                        absoluteFrame.width - 6,
                        absoluteFrame.height - 6};
    painter.fillRect({interior.x,
                      interior.y,
                      detail::multiplyDivide(interior.width, mValue, 100),
                      interior.height},
                     current.accent);
    painter.strokeRect(absoluteFrame,
                       current.border.alpha == 0U ? current.foreground : current.border,
                       current.borderWidth == 0U ? 1U : current.borderWidth);
}

ImageView::ImageView(Rect frame, const Bitmap& bitmap, Style style) noexcept
    : View(frame, style), mBitmap(bitmap)
{
}

void ImageView::setBitmap(const Bitmap& bitmap) noexcept
{
    if (&mBitmap.get() != &bitmap)
    {
        mBitmap = std::cref(bitmap);
        invalidate();
    }
}

void ImageView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    painter.drawBitmap({absoluteFrame.x, absoluteFrame.y}, mBitmap.get());
}

AnimatedImageView::AnimatedImageView(Rect frame,
                                     const ResourceProvider& resources,
                                     std::span<const std::string_view> frameNames,
                                     std::uint32_t intervalMs,
                                     std::size_t repeatIndex,
                                     bool autoplay,
                                     Style style) noexcept
    : View(frame, style), mResources(resources), mFrameNames(frameNames),
      mRepeatIndex(repeatIndex < frameNames.size() ? repeatIndex : 0U),
      mIntervalMs(intervalMs == 0U ? 1U : intervalMs), mRunning(autoplay && frameNames.size() > 1U)
{
}

void AnimatedImageView::start() noexcept
{
    if (mFrameNames.size() > 1U)
    {
        mRunning = true;
    }
}

void AnimatedImageView::setBitmapScale(std::uint8_t scale) noexcept
{
    const std::uint8_t normalized = scale == 0U ? 1U : scale;
    if (mBitmapScale != normalized)
    {
        mBitmapScale = normalized;
        invalidate();
    }
}

void AnimatedImageView::reset() noexcept
{
    mFrameIndex = 0U;
    mAccumulatedMs = 0U;
    invalidate();
}

bool AnimatedImageView::onAdvanceTime(std::uint32_t elapsedMs) noexcept
{
    if (!mRunning || mFrameNames.size() <= 1U || elapsedMs == 0U)
    {
        return false;
    }
    std::size_t steps = elapsedMs / mIntervalMs;
    const std::uint32_t remainder = elapsedMs % mIntervalMs;
    const std::uint32_t untilNextFrame = mIntervalMs - mAccumulatedMs;
    if (remainder >= untilNextFrame)
    {
        ++steps;
        mAccumulatedMs = remainder - untilNextFrame;
    }
    else
    {
        mAccumulatedMs += remainder;
    }
    if (steps == 0U)
    {
        return false;
    }

    const std::size_t before = mFrameIndex;
    const std::size_t stepsToWrap = mFrameNames.size() - mFrameIndex;
    if (steps < stepsToWrap)
    {
        mFrameIndex += steps;
    }
    else
    {
        steps -= stepsToWrap;
        const std::size_t loopLength = mFrameNames.size() - mRepeatIndex;
        mFrameIndex = mRepeatIndex + (steps % loopLength);
    }
    if (mFrameIndex != before)
    {
        invalidate();
        return true;
    }
    return false;
}

void AnimatedImageView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    if (mFrameIndex >= mFrameNames.size())
    {
        return;
    }
    const Bitmap* const bitmap = mResources.bitmap(mFrameNames[mFrameIndex]);
    if (bitmap == nullptr)
    {
        return;
    }
    const std::int32_t scaledWidth = detail::saturatingMultiply(bitmap->width, mBitmapScale);
    const std::int32_t scaledHeight = detail::saturatingMultiply(bitmap->height, mBitmapScale);
    const Point origin{absoluteFrame.x + ((absoluteFrame.width - scaledWidth) / 2),
                       absoluteFrame.y + ((absoluteFrame.height - scaledHeight) / 2)};
    painter.drawBitmap(origin, *bitmap, mBitmapScale);
}

IconView::IconView(Rect frame, const Bitmap& bitmap, Style style) noexcept
    : View(frame, style), mBitmap(bitmap)
{
}

void IconView::setBitmap(const Bitmap& bitmap) noexcept
{
    if (&mBitmap.get() != &bitmap)
    {
        mBitmap = std::cref(bitmap);
        invalidate();
    }
}

void IconView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    const Point origin{absoluteFrame.x + ((absoluteFrame.width - mBitmap.get().width) / 2),
                       absoluteFrame.y + ((absoluteFrame.height - mBitmap.get().height) / 2)};
    painter.drawBitmap(origin, mBitmap.get());
}

void LineView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    const Style current = resolvedStyle();
    const Color color = current.foreground.alpha == 0U ? current.accent : current.foreground;
    const std::int32_t thickness = current.borderWidth == 0U ? 1 : current.borderWidth;
    if (absoluteFrame.width >= absoluteFrame.height)
    {
        painter.fillRect({absoluteFrame.x,
                          absoluteFrame.y + ((absoluteFrame.height - thickness) / 2),
                          absoluteFrame.width,
                          thickness},
                         color);
    }
    else
    {
        painter.fillRect({absoluteFrame.x + ((absoluteFrame.width - thickness) / 2),
                          absoluteFrame.y,
                          thickness,
                          absoluteFrame.height},
                         color);
    }
}

ScrollView::ScrollView(Rect frame, std::int32_t contentHeight, Style style) noexcept
    : View(frame, style),
      mContentHeight(contentHeight > frame.height ? contentHeight : frame.height)
{
}

std::int32_t ScrollView::maximumOffset() const noexcept
{
    const std::int32_t maximum = mContentHeight - frame().height;
    return maximum > 0 ? maximum : 0;
}

void ScrollView::setContentHeight(std::int32_t height) noexcept
{
    const std::int32_t bounded = height > frame().height ? height : frame().height;
    if (bounded != mContentHeight)
    {
        invalidatePopups();
        mContentHeight = bounded;
        mContentOffset = std::min(mContentOffset, maximumOffset());
        invalidate();
        invalidatePopups();
    }
}

void ScrollView::setContentOffset(std::int32_t offset) noexcept
{
    offset = std::max(offset, std::int32_t{0});
    offset = std::min(offset, maximumOffset());
    if (offset != mContentOffset)
    {
        invalidatePopups();
        mContentOffset = offset;
        invalidate();
        invalidatePopups();
    }
}

void ScrollView::scrollBy(std::int32_t pixels) noexcept
{
    setContentOffset(detail::saturatingAdd(mContentOffset, pixels));
}

void ScrollView::fitContentToSubviews() noexcept
{
    std::int32_t bottom = mContentHeight > frame().height ? mContentHeight : frame().height;
    for (std::size_t index = 0; index < subviewCount(); ++index)
    {
        const View* child = subview(index);
        if (child != nullptr && child->frame().bottom() > bottom)
        {
            bottom = child->frame().bottom();
        }
    }
    setContentHeight(bottom);
}

void ScrollView::setScrollStep(std::int32_t pixels) noexcept
{
    mScrollStep = pixels > 0 ? pixels : 1;
}

void ScrollView::setShowsIndicator(bool visible) noexcept
{
    if (mShowsIndicator != visible)
    {
        mShowsIndicator = visible;
        invalidate();
    }
}

bool ScrollView::handleKey(Key key) noexcept
{
    if (key == Key::up)
    {
        scrollBy(-mScrollStep);
        return true;
    }
    if (key == Key::down)
    {
        scrollBy(mScrollStep);
        return true;
    }
    return false;
}

Rect ScrollView::indicatorRail() const noexcept
{
    constexpr std::int32_t railWidth = 8;
    return {frame().width - railWidth - 2, 4, railWidth, frame().height - 8};
}

Rect ScrollView::indicatorThumb() const noexcept
{
    const Rect rail = indicatorRail();
    std::int32_t thumbHeight = detail::multiplyDivide(rail.height, frame().height, mContentHeight);
    thumbHeight = std::max(thumbHeight, std::int32_t{12});
    thumbHeight = std::min(thumbHeight, rail.height);
    const std::int32_t travel = rail.height - thumbHeight;
    const std::int32_t thumbY = detail::saturatingAdd(
        rail.y,
        maximumOffset() == 0 ? 0 : detail::multiplyDivide(travel, mContentOffset, maximumOffset()));
    return {rail.x, thumbY, rail.width, thumbHeight};
}

bool ScrollView::capturesPointerAt(Point localPoint) const noexcept
{
    if (!mShowsIndicator || maximumOffset() == 0 || localPoint.y < 0 ||
        localPoint.y >= frame().height)
    {
        return false;
    }
    constexpr std::int32_t touchWidth = 20;
    return localPoint.x >= frame().width - touchWidth && localPoint.x < frame().width;
}

void ScrollView::dragIndicator(std::int32_t localY) noexcept
{
    const Rect rail = indicatorRail();
    const Rect thumb = indicatorThumb();
    const std::int32_t travel = rail.height - thumb.height;
    if (travel <= 0)
    {
        setContentOffset(0);
        return;
    }
    std::int32_t thumbY = localY - mIndicatorGrabOffset - rail.y;
    thumbY = std::max(thumbY, std::int32_t{0});
    thumbY = std::min(thumbY, travel);
    setContentOffset(detail::multiplyDivide(thumbY, maximumOffset(), travel));
}

void ScrollView::handlePointerEvent(PointerEvent event) noexcept
{
    if (event.phase == PointerPhase::pressed)
    {
        if (!capturesPointerAt(event.position))
        {
            return;
        }
        mDraggingIndicator = true;
        const Rect thumb = indicatorThumb();
        if (thumb.contains(event.position))
        {
            mIndicatorGrabOffset = event.position.y - thumb.y;
        }
        else
        {
            mIndicatorGrabOffset = thumb.height / 2;
            dragIndicator(event.position.y);
        }
    }
    else if (event.phase == PointerPhase::moved && mDraggingIndicator)
    {
        dragIndicator(event.position.y);
    }
    else if (event.phase == PointerPhase::released || event.phase == PointerPhase::cancelled)
    {
        mDraggingIndicator = false;
    }
}

void ScrollView::descendantFocused(View& descendant) noexcept
{
    const Rect visibleFrame = descendant.frameRelativeTo(*this);
    if (!visibleFrame.empty())
    {
        if (visibleFrame.y < 0)
        {
            scrollBy(visibleFrame.y);
        }
        else if (visibleFrame.bottom() > frame().height)
        {
            scrollBy(visibleFrame.bottom() - frame().height);
        }
    }
    View::descendantFocused(descendant);
}

void ScrollView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    if (!mShowsIndicator || mContentHeight <= frame().height)
    {
        return;
    }
    const Style current = resolvedStyle();
    Rect rail = indicatorRail();
    rail = rail.translated({absoluteFrame.x, absoluteFrame.y});
    painter.fillRect(rail, current.border.alpha == 0U ? Color::rgb(0xd4d9e2U) : current.border);
    Rect thumb = indicatorThumb();
    thumb = thumb.translated({absoluteFrame.x, absoluteFrame.y});
    painter.fillRect(thumb, current.accent);
}

PickerView::PickerView(Rect frame,
                       const Font& font,
                       std::string_view items,
                       std::size_t selectedIndex,
                       bool wraps,
                       Style style) noexcept
    : ControlView(frame, style), mFont(font), mItems(items), mItemCount(countItems(items)),
      mSelectedIndex(selectedIndex), mWraps(wraps)
{

    if (mItemCount == 0U)
    {
        mSelectedIndex = 0U;
    }
    else if (mSelectedIndex >= mItemCount)
    {
        mSelectedIndex = mItemCount - 1U;
    }

    setFocusable(true);
}

std::size_t PickerView::countItems(std::string_view items) noexcept
{
    if (items.empty())
    {
        return 0U;
    }
    std::size_t count = 1U;
    for (const char character : items)
    {
        if (character == '|')
        {
            ++count;
        }
    }
    return count;
}

std::string_view PickerView::itemAt(std::size_t index) const noexcept
{
    std::size_t start = 0U;
    for (std::size_t current = 0U; current < index; ++current)
    {
        const std::size_t separator = mItems.find('|', start);
        if (separator == std::string_view::npos)
        {
            return {};
        }
        start = separator + 1U;
    }
    const std::size_t separator = mItems.find('|', start);
    return mItems.substr(
        start, separator == std::string_view::npos ? mItems.size() - start : separator - start);
}

std::string_view PickerView::selectedItem() const noexcept
{
    return mItemCount == 0U ? std::string_view{} : itemAt(mSelectedIndex);
}

void PickerView::setSelectedIndex(std::size_t index) noexcept
{
    if (mItemCount == 0U)
    {
        return;
    }
    if (index >= mItemCount)
    {
        index = mItemCount - 1U;
    }
    if (mSelectedIndex != index)
    {
        mSelectedIndex = index;
        invalidate();
    }
}

void PickerView::selectPrevious() noexcept
{
    if (mItemCount == 0U || (mSelectedIndex == 0U && !mWraps))
    {
        return;
    }
    setSelectedIndex(mSelectedIndex == 0U ? mItemCount - 1U : mSelectedIndex - 1U);
}

void PickerView::selectNext() noexcept
{
    if (mItemCount == 0U || (mSelectedIndex + 1U == mItemCount && !mWraps))
    {
        return;
    }
    setSelectedIndex(mSelectedIndex + 1U == mItemCount ? 0U : mSelectedIndex + 1U);
}

bool PickerView::handleKey(Key key) noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled() || (key != Key::left && key != Key::right))
    {
        return false;
    }
    const std::size_t previous = selectedIndex();
    if (key == Key::left)
    {
        selectPrevious();
    }
    else
    {
        selectNext();
    }
    notifySelectionChanged({previous, selectedIndex()});
    return true;
}

void PickerView::pointerPressed(Point localPoint) noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled())
    {
        return;
    }
    const std::size_t previous = selectedIndex();
    if (localPoint.x < frame().width / 2)
    {
        selectPrevious();
    }
    else
    {
        selectNext();
    }
    notifySelectionChanged({previous, selectedIndex()});
}

void PickerView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style current = resolvedStyle();
    painter.fillRect(absoluteFrame, current.background);
    painter.strokeRect(absoluteFrame,
                       current.border.alpha == 0U ? current.foreground : current.border,
                       current.borderWidth == 0U ? 1U : current.borderWidth);
    painter.drawText({absoluteFrame.x + 8, absoluteFrame.y, 24, absoluteFrame.height},
                     "<",
                     mFont,
                     current.foreground,
                     {HorizontalAlign::left, VerticalAlign::center},
                     current.textScale);
    painter.drawText({absoluteFrame.right() - 32, absoluteFrame.y, 24, absoluteFrame.height},
                     ">",
                     mFont,
                     current.foreground,
                     {HorizontalAlign::right, VerticalAlign::center},
                     current.textScale);
    painter.drawText(
        {absoluteFrame.x + 36, absoluteFrame.y, absoluteFrame.width - 72, absoluteFrame.height},
        selectedItem(),
        mFont,
        current.foreground,
        {HorizontalAlign::center, VerticalAlign::center},
        current.textScale);
}

SoftkeyView::SoftkeyView(Rect frame,
                         const Font& font,
                         std::string_view leftText,
                         std::string_view rightText,
                         ViewId leftAction,
                         ViewId rightAction,
                         Style style) noexcept
    : View(frame, style), mFont(font), mLeftText(leftText), mRightText(rightText),
      mLeftAction(leftAction), mRightAction(rightAction)
{
    setFocusable(true);
}

void SoftkeyView::setLeftText(std::string_view text) noexcept
{
    if (mLeftText != text)
    {
        mLeftText = text;
        invalidate();
    }
}

void SoftkeyView::setRightText(std::string_view text) noexcept
{
    if (mRightText != text)
    {
        mRightText = text;
        invalidate();
    }
}

void SoftkeyView::setActions(ViewId leftAction, ViewId rightAction) noexcept
{
    mLeftAction = leftAction;
    mRightAction = rightAction;
}

bool SoftkeyView::handleKey(Key key) noexcept
{
    if (key == Key::left || key == Key::right)
    {
        const bool selectRight = key == Key::right;
        if (selectRight != mRightSelected)
        {
            mRightSelected = selectRight;
            invalidate();
        }
        return true;
    }
    return false;
}

void SoftkeyView::handlePointerEvent(PointerEvent event) noexcept
{
    View::handlePointerEvent(event);
    if (event.phase == PointerPhase::moved)
    {
        pointerPressed(event.position);
    }
}

void SoftkeyView::pointerPressed(Point localPoint) noexcept
{
    const bool selectRight = localPoint.x >= frame().width / 2;
    if (selectRight != mRightSelected)
    {
        mRightSelected = selectRight;
        invalidate();
    }
}

ViewId SoftkeyView::actionId() const noexcept
{
    const ViewId selected = mRightSelected ? mRightAction : mLeftAction;
    return selected == 0U ? id() : selected;
}

void SoftkeyView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style normal = style();
    const Style current = resolvedStyle();
    painter.fillRect(absoluteFrame, enabled() ? normal.background : current.background);
    painter.fillRect({absoluteFrame.x, absoluteFrame.y, absoluteFrame.width, 2}, normal.accent);
    if (focused() || pressed())
    {
        const Rect selected = mRightSelected ? Rect{absoluteFrame.x + (absoluteFrame.width / 2),
                                                    absoluteFrame.y,
                                                    absoluteFrame.width - (absoluteFrame.width / 2),
                                                    absoluteFrame.height}
                                             : Rect{absoluteFrame.x,
                                                    absoluteFrame.y,
                                                    absoluteFrame.width / 2,
                                                    absoluteFrame.height};
        painter.fillRect(selected, current.background);
    }
    const Color textColor =
        focused() || pressed() || !enabled() ? current.foreground : normal.foreground;
    painter.drawText({absoluteFrame.x + 10,
                      absoluteFrame.y,
                      (absoluteFrame.width / 2) - 20,
                      absoluteFrame.height},
                     mLeftText,
                     mFont,
                     textColor,
                     {HorizontalAlign::left, VerticalAlign::center},
                     current.textScale);
    painter.drawText({absoluteFrame.x + (absoluteFrame.width / 2) + 10,
                      absoluteFrame.y,
                      absoluteFrame.width - (absoluteFrame.width / 2) - 20,
                      absoluteFrame.height},
                     mRightText,
                     mFont,
                     textColor,
                     {HorizontalAlign::right, VerticalAlign::center},
                     current.textScale);
}

TabBarView::TabBarView(Rect frame,
                       const Font& font,
                       std::string_view titles,
                       std::size_t selectedIndex,
                       bool wraps,
                       Style style) noexcept
    : View(frame, style), mFont(font), mTitles(titles), mWraps(wraps)
{
    // Both values are bounded by kMaximumTabs, so the assignments always succeed.
    static_assert(kMaximumTabs < kNoTab);
    const std::size_t count = countDelimitedItems(titles, kMaximumTabs);
    if (detail::assignIfRepresentable(count, mTabCount) && count != 0U)
    {
        detail::assignIfRepresentable(std::min(selectedIndex, count - 1U), mSelectedIndex);
    }
    setFocusable(true);
}

std::string_view TabBarView::title(std::size_t index) const noexcept
{
    return index < mTabCount ? delimitedItem(mTitles, index) : std::string_view{};
}

bool TabBarView::setSelectedIndex(std::size_t index) noexcept
{
    std::uint8_t next = 0U;
    if (mTabCount == 0U ||
        !detail::assignIfRepresentable(std::min<std::size_t>(index, mTabCount - 1U), next) ||
        next == mSelectedIndex)
    {
        return false;
    }
    mSelectedIndex = next;
    invalidate();
    selectionChanged();
    return true;
}

bool TabBarView::selectPrevious() noexcept
{
    if (mTabCount == 0U || (mSelectedIndex == 0U && !mWraps))
    {
        return false;
    }
    return setSelectedIndex(mSelectedIndex == 0U ? mTabCount - 1U : mSelectedIndex - 1U);
}

bool TabBarView::selectNext() noexcept
{
    if (mTabCount == 0U || (mSelectedIndex + 1U == mTabCount && !mWraps))
    {
        return false;
    }
    return setSelectedIndex(mSelectedIndex + 1U == mTabCount ? 0U : mSelectedIndex + 1U);
}

bool TabBarView::handleKey(Key key) noexcept
{
    mPointerActivation = false;
    setPressedTab(kNoTab);
    setActionRequested(false);
    if (mTabCount == 0U || (key != Key::left && key != Key::right))
    {
        return false;
    }
    setActionRequested(key == Key::left ? selectPrevious() : selectNext());
    return true;
}

void TabBarView::handlePointerEvent(PointerEvent event) noexcept
{
    View::handlePointerEvent(event);
    switch (event.phase)
    {
    case PointerPhase::pressed:
        mPointerActivation = true;
        setPressedTab(tabAt(event.position));
        break;
    case PointerPhase::moved:
        setPressedTab(tabAt(event.position));
        break;
    case PointerPhase::released:
        // The controller activates after a release inside this view; the
        // pressed highlight ends either way.
        setPressedTab(tabAt(event.position));
        mPointerActivation = Rect{0, 0, frame().width, frame().height}.contains(event.position);
        invalidate();
        break;
    case PointerPhase::cancelled:
        mPointerActivation = false;
        setPressedTab(kNoTab);
        break;
    }
}

void TabBarView::activate() noexcept
{
    if (mPointerActivation)
    {
        mPointerActivation = false;
        const std::uint8_t released = mPressedTab;
        setPressedTab(kNoTab);
        if (released != kNoTab)
        {
            setSelectedIndex(released);
        }
        return;
    }
    if (mTabCount != 0U)
    {
        setSelectedIndex(mSelectedIndex + 1U == mTabCount ? 0U : mSelectedIndex + 1U);
    }
}

Rect TabBarView::tabRect(Rect strip, std::uint8_t index) const noexcept
{
    const std::int32_t left = strip.x + ((strip.width * index) / mTabCount);
    const std::int32_t right = strip.x + ((strip.width * (index + 1)) / mTabCount);
    return {left, strip.y, right - left, strip.height};
}

std::uint8_t TabBarView::tabAt(Point localPoint) const noexcept
{
    const Rect strip{0, 0, frame().width, stripHeight()};
    if (!strip.contains(localPoint))
    {
        return kNoTab;
    }
    for (std::uint8_t index = 0U; index < mTabCount; ++index)
    {
        if (tabRect(strip, index).contains(localPoint))
        {
            return index;
        }
    }
    return kNoTab;
}

void TabBarView::setPressedTab(std::uint8_t tab) noexcept
{
    if (mPressedTab != tab)
    {
        mPressedTab = tab;
        invalidate();
    }
}

void TabBarView::drawTabs(Painter& painter, Rect strip) const noexcept
{
    const Style normal =
        resolveWidgetStyle(style(), enabled() ? WidgetState::normal : WidgetState::disabled);
    if (mTabCount == 0U)
    {
        painter.fillRect(strip, normal.background);
    }
    for (std::uint8_t index = 0U; index < mTabCount; ++index)
    {
        const bool current = index == mSelectedIndex;
        const Style tab = resolveWidgetStyle(
            style(),
            resolveWidgetState(
                enabled(), focused() && current, pressed() && index == mPressedTab, current));
        const Rect bounds = tabRect(strip, index);
        painter.fillRect(bounds, tab.background);
        painter.drawText({bounds.x + kTabTitleInset,
                          bounds.y,
                          bounds.width - (kTabTitleInset * 2),
                          bounds.height - kTabIndicatorThickness},
                         title(index),
                         mFont,
                         tab.foreground,
                         {HorizontalAlign::center, VerticalAlign::center},
                         tab.textScale);
        if (current)
        {
            const std::int32_t inset = std::min(kTabIndicatorMaximumInset, bounds.width / 4);
            painter.fillRect({bounds.x + inset,
                              bounds.bottom() - kTabIndicatorThickness,
                              bounds.width - (inset * 2),
                              kTabIndicatorThickness},
                             tab.foreground);
        }
    }
    if (normal.borderWidth != 0U && normal.border.alpha != 0U)
    {
        painter.strokeRect(strip, normal.border, normal.borderWidth);
    }
}

void TabBarView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    drawTabs(painter, absoluteFrame);
}

TabView::TabView(Rect frame,
                 const Font& font,
                 std::string_view titles,
                 std::size_t selectedIndex,
                 std::uint16_t tabHeight,
                 bool wraps,
                 Style style) noexcept
    : TabBarView(frame, font, titles, selectedIndex, wraps, style), mTabHeight(tabHeight)
{
}

void TabView::setTabHeight(std::uint16_t height) noexcept
{
    if (mTabHeight != height)
    {
        mTabHeight = height;
        invalidate();
    }
}

std::int32_t TabView::stripHeight() const noexcept
{
    return std::min<std::int32_t>(mTabHeight, frame().height);
}

Rect TabView::contentFrame() const noexcept
{
    const std::int32_t strip = stripHeight();
    return {0, strip, frame().width, frame().height - strip};
}

void TabView::syncPages() noexcept
{
    for (std::size_t index = 0U; index < subviewCount(); ++index)
    {
        if (View* const page = subview(index))
        {
            page->setVisible(index == selectedIndex());
        }
    }
}

bool TabView::capturesPointerAt(Point localPoint) const noexcept
{
    return localPoint.y < stripHeight();
}

void TabView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style normal =
        resolveWidgetStyle(style(), enabled() ? WidgetState::normal : WidgetState::disabled);
    const Rect content = contentFrame().translated({absoluteFrame.x, absoluteFrame.y});
    if (normal.background.alpha != 0U)
    {
        painter.fillRect(content, normal.background);
    }
    if (normal.borderWidth != 0U && normal.border.alpha != 0U)
    {
        painter.strokeRect(absoluteFrame, normal.border, normal.borderWidth);
    }
    drawTabs(painter, {absoluteFrame.x, absoluteFrame.y, absoluteFrame.width, stripHeight()});
}

WheelPickerView::WheelPickerView(Rect frame,
                                 const Font& font,
                                 std::string_view items,
                                 std::size_t selectedIndex,
                                 std::uint8_t rowHeight,
                                 bool wraps,
                                 Style style) noexcept
    : ControlView(frame, style), mFont(font), mItems(items),
      mRowHeight(rowHeight == 0U ? detail::kDefaultWheelRowHeightPixels : rowHeight), mWraps(wraps)
{
    // Both values are bounded by kMaximumItems, so the assignments always succeed.
    static_assert(kMaximumItems <= std::numeric_limits<std::uint8_t>::max());
    const std::size_t count = countDelimitedItems(items, kMaximumItems);
    if (detail::assignIfRepresentable(count, mItemCount) && count != 0U)
    {
        detail::assignIfRepresentable(std::min(selectedIndex, count - 1U), mSelectedIndex);
    }
    mEditStartIndex = mSelectedIndex;
    setFocusable(true);
}

std::string_view WheelPickerView::item(std::size_t index) const noexcept
{
    return index < mItemCount ? delimitedItem(mItems, index) : std::string_view{};
}

bool WheelPickerView::setSelectedIndex(std::size_t index) noexcept
{
    std::uint8_t next = 0U;
    if (mItemCount == 0U ||
        !detail::assignIfRepresentable(std::min<std::size_t>(index, mItemCount - 1U), next) ||
        next == mSelectedIndex)
    {
        return false;
    }
    mSelectedIndex = next;
    invalidate();
    return true;
}

bool WheelPickerView::selectPrevious() noexcept
{
    if (mItemCount == 0U || (mSelectedIndex == 0U && !mWraps))
    {
        return false;
    }
    return setSelectedIndex(steppedIndex(-1));
}

bool WheelPickerView::selectNext() noexcept
{
    if (mItemCount == 0U || (mSelectedIndex + 1U == mItemCount && !mWraps))
    {
        return false;
    }
    return setSelectedIndex(steppedIndex(1));
}

std::size_t WheelPickerView::steppedIndex(std::int32_t rows) const noexcept
{
    if (mItemCount == 0U)
    {
        return 0U;
    }
    const std::int32_t count = mItemCount;
    const std::int32_t target = mSelectedIndex + rows;
    return detail::nonnegativeSize(mWraps ? (((target % count) + count) % count)
                                          : std::clamp(target, std::int32_t{0}, count - 1));
}

std::int32_t WheelPickerView::bandTop() const noexcept
{
    return (frame().height - mRowHeight) / 2;
}

std::int32_t WheelPickerView::rowsFromCenter(std::int32_t localY) const noexcept
{
    return floorQuotient(localY - bandTop(), mRowHeight);
}

void WheelPickerView::setEditing(bool editing) noexcept
{
    if (mEditing == editing)
    {
        return;
    }
    mEditing = editing;
    if (mEditing)
    {
        mEditStartIndex = mSelectedIndex;
    }
    invalidate();
}

void WheelPickerView::finishEdit(bool cancel) noexcept
{
    const std::size_t previous = selectedIndex();
    const std::size_t baseline = mEditStartIndex;
    if (cancel)
    {
        setSelectedIndex(baseline);
    }
    setEditing(false);
    const SelectionEdit event{baseline, selectedIndex()};
    notifySelectionChanged({previous, event.index});
    if (cancel)
    {
        notifyEditCancelled(event);
    }
    else
    {
        notifyEditCommitted(event);
    }
}

void WheelPickerView::toggleEditing() noexcept
{
    if (mEditing)
    {
        finishEdit(false);
    }
    else
    {
        setEditing(true);
    }
}

bool WheelPickerView::handleKey(Key key) noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled() || mItemCount == 0U)
    {
        return false;
    }
    mPointerActivation = false;
    setActionRequested(false);
    if (key == Key::select)
    {
        toggleEditing();
        return true;
    }
    if (!mEditing)
    {
        return false;
    }
    const std::size_t previous = selectedIndex();
    if (key == Key::back)
    {
        finishEdit(true);
        setActionRequested(previous != selectedIndex());
        return true;
    }
    if (key == Key::up || key == Key::left)
    {
        selectPrevious();
    }
    else if (key == Key::down || key == Key::right)
    {
        selectNext();
    }
    else
    {
        return false;
    }
    const std::size_t accepted = selectedIndex();
    setActionRequested(previous != accepted);
    notifySelectionChanged({previous, accepted});
    return true;
}

void WheelPickerView::handlePointerEvent(PointerEvent event) noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction)
    {
        return;
    }
    // Cancellation must still clear capture state after an ancestor is disabled.
    if (!interactionEnabled() && event.phase != PointerPhase::cancelled)
    {
        return;
    }
    View::handlePointerEvent(event);
    switch (event.phase)
    {
    case PointerPhase::pressed:
        if (mEditing)
        {
            finishEdit(false);
        }
        mEditStartIndex = mSelectedIndex;
        setActionRequested(false);
        mPointerActivation = true;
        mDragged = false;
        mDragOffset = 0;
        mPressY = event.position.y;
        invalidate();
        break;
    case PointerPhase::moved:
        if (mPointerActivation)
        {
            dragTo(event.position.y);
        }
        break;
    case PointerPhase::released:
    {
        if (!mPointerActivation)
        {
            break;
        }
        dragTo(event.position.y);
        const bool inside = Rect{0, 0, frame().width, frame().height}.contains(event.position);
        const bool accepted = mDragged || inside;
        const std::int32_t rows = mDragged ? -roundedQuotient(mDragOffset, mRowHeight)
                                  : inside ? rowsFromCenter(event.position.y)
                                           : 0;
        const std::size_t previous = selectedIndex();
        mDragOffset = 0;
        mDragged = false;
        setActionRequested(setSelectedIndex(steppedIndex(rows)));
        const SelectionEdit edit{mEditStartIndex, selectedIndex()};
        invalidate();
        if (mItemCount != 0U)
        {
            notifySelectionChanged({previous, edit.index});
            if (accepted)
            {
                notifyEditCommitted(edit);
            }
            else
            {
                notifyEditCancelled(edit);
            }
        }
        // The controller's following activate() consumes this flag without opening edit mode.
        break;
    }
    case PointerPhase::cancelled:
    {
        const bool wasActive = mPointerActivation;
        mDragOffset = 0;
        mDragged = false;
        mPointerActivation = false;
        setActionRequested(false);
        invalidate();
        if (wasActive && mItemCount != 0U)
        {
            notifyEditCancelled({mEditStartIndex, selectedIndex()});
        }
        break;
    }
    }
}

// Travel is measured from the press, so a clamped wheel still recognizes a drag rather than
// mistaking it for a tap on the row where the contact lifts.
void WheelPickerView::dragTo(std::int32_t localY) noexcept
{
    const std::int32_t travel = detail::saturatingSubtract(localY, mPressY);
    mDragged = mDragged || travel > kWheelTapSlop || travel < -kWheelTapSlop;
    std::int32_t offset = 0;
    if (mItemCount != 0U)
    {
        // Either rule keeps the offset below 255 rows of 255 pixels, so drawing and snapping
        // stay bounded and cannot overflow whatever coordinates the contact reports.
        const std::int32_t height = mRowHeight;
        if (mWraps)
        {
            // A wrapping column repeats every cycle, and a remainder with the travel's sign
            // draws and snaps exactly as the full travel would.
            offset = travel % (height * mItemCount);
        }
        else
        {
            // Without wrapping the first and last items cannot be dragged past the band.
            const std::int32_t above = mSelectedIndex;
            const std::int32_t below = mItemCount - 1 - mSelectedIndex;
            offset = std::clamp(travel, -(below * height), above * height);
        }
    }
    if (offset != mDragOffset)
    {
        mDragOffset = offset;
        invalidate();
    }
}

void WheelPickerView::activate() noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled())
    {
        return;
    }
    if (mPointerActivation)
    {
        mPointerActivation = false;
        return;
    }
    if (mItemCount != 0U)
    {
        toggleEditing();
    }
}

void WheelPickerView::focusChanged() noexcept
{
    const InteractionGuard interaction{*this};
    if (focused())
    {
        return;
    }
    if (!interaction)
    {
        mFocusCleanupPending = true;
        return;
    }
    // Preserve the established policy: ordinary focus loss accepts the edit.
    // Invalid/disabled focus cancels and restores the baseline instead.
    if (mEditing)
    {
        finishEdit(!interactionEnabled());
    }
    mDragOffset = 0;
    mDragged = false;
}

void WheelPickerView::interactionFinished() noexcept
{
    if (!mFocusCleanupPending)
    {
        return;
    }
    mFocusCleanupPending = false;
    if (!focused())
    {
        if (mEditing)
        {
            finishEdit(!interactionEnabled());
        }
        mDragOffset = 0;
        mDragged = false;
    }
}

void WheelPickerView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style normal =
        resolveWidgetStyle(style(), enabled() ? WidgetState::normal : WidgetState::disabled);
    // Editing and dragging show the pressed colors so they differ from plain focus.
    const Style band = resolveWidgetStyle(
        style(), resolveWidgetState(enabled(), focused(), pressed() || mEditing, true));
    const std::int32_t height = mRowHeight;
    const Rect bandRect{absoluteFrame.x, absoluteFrame.y + bandTop(), absoluteFrame.width, height};
    painter.fillRect(absoluteFrame, normal.background);
    painter.fillRect(bandRect, band.background);
    painter.fillRect({bandRect.x, bandRect.y, bandRect.width, kWheelBandRule}, normal.accent);
    painter.fillRect(
        {bandRect.x, bandRect.bottom() - kWheelBandRule, bandRect.width, kWheelBandRule},
        normal.accent);
    if (mEditing && focused())
    {
        const std::int32_t markTop = bandRect.y + kWheelEditMarkInset;
        const std::int32_t markHeight = height - (kWheelEditMarkInset * 2);
        painter.fillRect(
            {bandRect.x + kWheelEditMarkInset, markTop, kWheelEditMarkWidth, markHeight},
            band.foreground);
        painter.fillRect({bandRect.right() - kWheelEditMarkInset - kWheelEditMarkWidth,
                          markTop,
                          kWheelEditMarkWidth,
                          markHeight},
                         band.foreground);
    }
    if (mItemCount != 0U)
    {
        // Visit only the rows that intersect the frame: row r spans [rowZero + r * height,
        // rowZero + (r + 1) * height), where row 0 is the selected item.
        const std::int32_t rowZero = bandRect.y + mDragOffset;
        const std::int32_t firstRow = floorQuotient(absoluteFrame.y - rowZero, height);
        const std::int32_t lastRow = floorQuotient(absoluteFrame.bottom() - 1 - rowZero, height);
        for (std::int32_t row = firstRow; row <= lastRow; ++row)
        {
            const std::int32_t index = mSelectedIndex + row;
            if (!mWraps && (index < 0 || index >= mItemCount))
            {
                continue;
            }
            const std::int32_t top = rowZero + (row * height);
            const std::int32_t distance = magnitude(top - bandRect.y);
            Color color = band.foreground;
            if (distance * 2 >= height)
            {
                Color muted = normal.foreground;
                muted.alpha = distance < 2 * height ? kWheelNearAlpha : kWheelFarAlpha;
                color = muted.blendedOver(normal.background);
            }
            painter.drawText({absoluteFrame.x + kWheelTextInset,
                              top,
                              absoluteFrame.width - (kWheelTextInset * 2),
                              height},
                             item(steppedIndex(row)),
                             mFont,
                             color,
                             {HorizontalAlign::center, VerticalAlign::center},
                             normal.textScale);
        }
    }
    if (normal.borderWidth != 0U && normal.border.alpha != 0U)
    {
        painter.strokeRect(absoluteFrame, normal.border, normal.borderWidth);
    }
}

} // namespace lithe
