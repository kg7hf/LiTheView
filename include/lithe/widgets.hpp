// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/control_view.hpp"
#include "lithe/detail/constants.hpp"
#include "lithe/list_view.hpp"

#include <array>
#include <cstdint>
#include <expected>
#include <functional>
#include <span>
#include <string_view>

namespace lithe
{

class ResourceProvider;

class LabelView : public View
{
public:
    LabelView(Rect frame, const Font& font, std::string_view text = {}, Style style = {}) noexcept;

    std::string_view text() const noexcept
    {
        return mText;
    }
    void setText(std::string_view text) noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    const Font& mFont;
    std::string_view mText;
};

// Momentary buttons are non-latching: they show the pressed state only while a
// contact is held and report one action for each completed activation. Toggle
// buttons latch the shared View selected state and flip it on every activation,
// before the controller reports the action. Enumerator values are stable.
enum class ButtonBehavior : std::uint8_t
{
    momentary,
    toggle
};

class ButtonView : public ControlView
{
public:
    ButtonView(Rect frame,
               const Font& font,
               std::string_view text = {},
               std::string_view detail = {},
               Style style = {}) noexcept;

    std::string_view text() const noexcept
    {
        return mText;
    }
    std::string_view detail() const noexcept
    {
        return mDetail;
    }
    void setText(std::string_view text) noexcept;
    void setDetail(std::string_view detail) noexcept;

    ButtonBehavior behavior() const noexcept
    {
        return mBehavior;
    }
    // Changing to momentary clears any latched selection.
    void setBehavior(ButtonBehavior behavior) noexcept;
    // Only a toggle button can be selected. A momentary button rejects true by
    // returning false and remains unselected; false is always accepted.
    bool setSelected(bool selected) noexcept;

    // The optional icon is borrowed and must outlive the button. Alpha1 and
    // alpha8 icons are tinted with the resolved state foreground; direct-color
    // and indexed icons keep their authored pixels.
    const Bitmap* icon() const noexcept
    {
        return mIcon;
    }
    void setIcon(const Bitmap& icon) noexcept;
    void setIcon(const Bitmap&& icon) = delete;
    void clearIcon() noexcept;

    void activate() noexcept final;

protected:
    // Runs once after the built-in activation updates toggle state. Keep state
    // invariants in the base; specialize behavior here, not by replacing activate.
    // The controller notifies its delegate after this hook returns. A hook must
    // not destroy/rebuild the active tree or recursively dispatch input.
    virtual void onClicked() noexcept
    {
    }
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    Rect drawToggleIndicator(Painter& painter, Rect absoluteFrame, Color color) const noexcept;

    const Font& mFont;
    std::string_view mText;
    std::string_view mDetail;
    const Bitmap* mIcon{nullptr};
    ButtonBehavior mBehavior{ButtonBehavior::momentary};
};

class CheckboxView : public ControlView
{
public:
    CheckboxView(Rect frame,
                 const Font& font,
                 std::string_view text,
                 bool checked = false,
                 Style style = {}) noexcept;

    // The checked value is the shared View selected state.
    bool checked() const noexcept
    {
        return selected();
    }
    void setChecked(bool checked) noexcept
    {
        setSelectedState(checked);
    }
    void activate() noexcept final;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    const Font& mFont;
    std::string_view mText;
};

class RadioGroup;

class RadioButtonView : public ControlView
{
public:
    RadioButtonView(Rect frame,
                    const Font& font,
                    std::string_view text,
                    bool selected = false,
                    Style style = {}) noexcept;

    ~RadioButtonView() override;
    RadioButtonView(const RadioButtonView&) = delete;
    RadioButtonView& operator=(const RadioButtonView&) = delete;
    RadioButtonView(RadioButtonView&&) = delete;
    RadioButtonView& operator=(RadioButtonView&&) = delete;
    // A grouped setter silently maintains exclusivity. False may clear the group.
    void setSelected(bool selected) noexcept;
    void activate() noexcept final;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    friend class RadioGroup;
    RadioGroup* mGroup{nullptr};
    const Font& mFont;
    std::string_view mText;
};

// Application-owned, bounded group. Attach after form.build(). Group and controls
// may be destroyed in either order; neither can be copied or moved. add() rejects
// duplicate membership and conflicting initial selections without changing state.
enum class RadioGroupError
{
    alreadyGrouped,
    capacityExceeded,
    conflictingSelection
};
class RadioGroup
{
public:
    static constexpr std::size_t kCapacity = config::kMaxFocusableViews;
    RadioGroup() = default;
    ~RadioGroup();
    RadioGroup(const RadioGroup&) = delete;
    RadioGroup& operator=(const RadioGroup&) = delete;
    RadioGroup(RadioGroup&&) = delete;
    RadioGroup& operator=(RadioGroup&&) = delete;
    [[nodiscard]] std::expected<void, RadioGroupError> add(RadioButtonView& choice) noexcept;
    const RadioButtonView* selected() const noexcept
    {
        return mSelected;
    }

private:
    friend class RadioButtonView;
    void remove(RadioButtonView& choice) noexcept;
    void select(RadioButtonView& choice, bool selected) noexcept;
    std::array<RadioButtonView*, kCapacity> mChoices{};
    std::size_t mCount{0U};
    RadioButtonView* mSelected{nullptr};
};

enum class TextEditError : std::uint8_t
{
    storageUnavailable,
    notEditing,
    disabled,
    reentrant,
    invalidText
};

class TextFieldView : public ControlView
{
public:
    TextFieldView(Rect frame,
                  const Font& font,
                  std::span<char> buffer,
                  std::size_t maxLength = 0U,
                  std::string_view placeholder = {},
                  Style style = {},
                  std::span<char> editBuffer = {}) noexcept;

    std::string_view text() const noexcept;
    std::size_t maxLength() const noexcept
    {
        return mBuffer.empty() ? 0U : mBuffer.size() - 1U;
    }
    // Silent authoritative refresh: ends any pending edit without notifying.
    // Cannot mutate borrowed event text during a callback on this control.
    bool setText(std::string_view text) noexcept;
    bool editing() const noexcept
    {
        return mEditing;
    }
    [[nodiscard]] std::expected<void, TextEditError> beginEdit() noexcept;
    [[nodiscard]] std::expected<void, TextEditError> commitEdit() noexcept;
    [[nodiscard]] std::expected<void, TextEditError> cancelEdit() noexcept;
    void activate() noexcept override;
    bool handleKey(Key key) noexcept override;
    bool handleTextInput(char32_t codepoint) noexcept override;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

    // Validation is a side-effect-free admission check, before commit notification.
    virtual bool acceptsText(std::string_view value) const noexcept;
    void focusChanged() noexcept override;
    void interactionFinished() noexcept override;

private:
    bool startEdit() noexcept;
    void restoreEdit() noexcept;
    std::string_view initialText() const noexcept;
    std::size_t length() const noexcept;

    const Font& mFont;
    std::span<char> mBuffer;
    std::string_view mPlaceholder;
    std::span<char> mEditBuffer;
    bool mEditing{false};
    bool mFocusCleanupPending{false};
};

class ProgressView final : public View
{
public:
    ProgressView(Rect frame, std::int32_t value = 0, Style style = {}) noexcept;

    std::int32_t value() const noexcept
    {
        return mValue;
    }
    void setValue(std::int32_t value) noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::int32_t mValue{0};
};

class ImageView final : public View
{
public:
    ImageView(Rect frame, const Bitmap& bitmap, Style style = {}) noexcept;

    const Bitmap& bitmap() const noexcept
    {
        return mBitmap.get();
    }
    void setBitmap(const Bitmap& bitmap) noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::reference_wrapper<const Bitmap> mBitmap;
};

class AnimatedImageView final : public View
{
public:
    AnimatedImageView(Rect frame,
                      const ResourceProvider& resources,
                      std::span<const std::string_view> frameNames,
                      std::uint32_t intervalMs,
                      std::size_t repeatIndex = 0U,
                      bool autoplay = true,
                      Style style = {}) noexcept;

    std::size_t frameCount() const noexcept
    {
        return mFrameNames.size();
    }
    std::size_t frameIndex() const noexcept
    {
        return mFrameIndex;
    }
    std::uint32_t intervalMs() const noexcept
    {
        return mIntervalMs;
    }
    bool running() const noexcept
    {
        return mRunning;
    }
    std::uint8_t bitmapScale() const noexcept
    {
        return mBitmapScale;
    }
    void setBitmapScale(std::uint8_t scale) noexcept;
    void start() noexcept;
    void stop() noexcept
    {
        mRunning = false;
    }
    void reset() noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;
    bool onAdvanceTime(std::uint32_t elapsedMs) noexcept override;
    bool animationRunning() const noexcept override
    {
        return mRunning && mFrameNames.size() > 1U;
    }

private:
    const ResourceProvider& mResources;
    std::span<const std::string_view> mFrameNames;
    std::size_t mFrameIndex{0U};
    std::size_t mRepeatIndex{0U};
    std::uint32_t mAccumulatedMs{0U};
    std::uint32_t mIntervalMs{detail::kDefaultAnimationIntervalMilliseconds};
    std::uint8_t mBitmapScale{1U};
    bool mRunning{false};
};

class IconView final : public View
{
public:
    IconView(Rect frame, const Bitmap& bitmap, Style style = {}) noexcept;

    const Bitmap& bitmap() const noexcept
    {
        return mBitmap.get();
    }
    void setBitmap(const Bitmap& bitmap) noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::reference_wrapper<const Bitmap> mBitmap;
};

class LineView final : public View
{
public:
    explicit LineView(Rect frame, Style style = {}) noexcept : View(frame, style)
    {
    }

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;
};

class ScrollView : public View
{
public:
    explicit ScrollView(Rect frame, std::int32_t contentHeight = 0, Style style = {}) noexcept;

    std::int32_t contentHeight() const noexcept
    {
        return mContentHeight;
    }
    std::int32_t contentOffset() const noexcept
    {
        return mContentOffset;
    }
    void setContentHeight(std::int32_t height) noexcept;
    void setContentOffset(std::int32_t offset) noexcept;
    void scrollBy(std::int32_t pixels) noexcept;
    void fitContentToSubviews() noexcept;
    void setScrollStep(std::int32_t pixels) noexcept;
    void setShowsIndicator(bool visible) noexcept;

    bool handleKey(Key key) noexcept override;
    void handlePointerEvent(PointerEvent event) noexcept override;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;
    Point childOffset() const noexcept override
    {
        return {0, -mContentOffset};
    }
    void descendantFocused(View& descendant) noexcept override;
    bool capturesPointerAt(Point localPoint) const noexcept override;

private:
    std::int32_t maximumOffset() const noexcept;
    Rect indicatorRail() const noexcept;
    Rect indicatorThumb() const noexcept;
    void dragIndicator(std::int32_t localY) noexcept;

    std::int32_t mContentHeight{0};
    std::int32_t mContentOffset{0};
    std::int32_t mScrollStep{detail::kDefaultScrollStepPixels};
    std::int32_t mIndicatorGrabOffset{0};
    bool mShowsIndicator{true};
    bool mDraggingIndicator{false};
};

class TableView final : public ScrollView
{
public:
    explicit TableView(Rect frame, std::int32_t contentHeight = 0, Style style = {}) noexcept
        : ScrollView(frame, contentHeight, style)
    {
    }
};

class PickerView : public ControlView
{
public:
    PickerView(Rect frame,
               const Font& font,
               std::string_view items,
               std::size_t selectedIndex = 0U,
               bool wraps = true,
               Style style = {}) noexcept;

    std::size_t itemCount() const noexcept
    {
        return mItemCount;
    }
    std::size_t selectedIndex() const noexcept
    {
        return mSelectedIndex;
    }
    std::string_view selectedItem() const noexcept;
    void setSelectedIndex(std::size_t index) noexcept;
    void selectPrevious() noexcept;
    void selectNext() noexcept;
    bool handleKey(Key key) noexcept final;
    void pointerPressed(Point localPoint) noexcept final;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::string_view itemAt(std::size_t index) const noexcept;
    static std::size_t countItems(std::string_view items) noexcept;

    const Font& mFont;
    std::string_view mItems;
    std::size_t mItemCount{0};
    std::size_t mSelectedIndex{0};
    bool mWraps{true};
};

// Immutable, caller-owned pipe-delimited items. The index is the item identity;
// duplicate labels are allowed. Keep the item storage alive for this view's lifetime.
// Selection while open is tentative. Commit accepts it; cancel restores the baseline.
class DropDownView : public ControlView
{
public:
    static constexpr std::size_t kMaximumItems{255U};
    static constexpr std::uint8_t kDefaultRowHeight{28U};
    static constexpr std::uint8_t kMaximumVisibleRows{5U};

    DropDownView(Rect frame,
                 const Font& font,
                 std::string_view items,
                 std::size_t selectedIndex = 0U,
                 std::uint8_t rowHeight = kDefaultRowHeight,
                 bool wraps = true,
                 Style style = {}) noexcept;

    std::size_t itemCount() const noexcept
    {
        return mItemCount;
    }
    std::size_t selectedIndex() const noexcept
    {
        return mSelectedIndex;
    }
    std::string_view item(std::size_t index) const noexcept;
    std::string_view selectedItem() const noexcept
    {
        return item(mSelectedIndex);
    }
    bool editing() const noexcept
    {
        return mEditing;
    }
    std::uint8_t rowHeight() const noexcept
    {
        return mRowHeight;
    }
    bool wraps() const noexcept
    {
        return mWraps;
    }
    // Reject invalid indices and reentrant mutation; a valid refresh silently ends an edit.
    bool setSelectedIndex(std::size_t index) noexcept;
    bool beginEdit() noexcept;
    bool commitEdit() noexcept;
    bool cancelEdit() noexcept;
    bool handleKey(Key key) noexcept final;
    void handlePointerEvent(PointerEvent event) noexcept final;
    void activate() noexcept final;
    Rect popupFrame() const noexcept final;
    bool dismissPopup() noexcept final
    {
        return cancelEdit();
    }

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;
    void drawPopup(Painter& painter, Rect absolutePopup) noexcept override;
    void focusChanged() noexcept final;
    void interactionFinished() noexcept final;

private:
    Rect availablePopup() const noexcept;
    bool open() noexcept;
    void finish(bool cancel) noexcept;
    void changeSelection(std::uint8_t index) noexcept;
    void ensureSelectionVisible() noexcept;
    void scrollTo(std::int32_t localY) noexcept;
    std::uint8_t rowAt(Point localPoint) const noexcept;
    std::int32_t firstVisibleRow() const noexcept;

    const Font& mFont;
    std::string_view mItems;
    std::int32_t mPressY{0};
    std::uint8_t mItemCount{0U};
    std::uint8_t mSelectedIndex{0U};
    std::uint8_t mInitialIndex{0U};
    std::uint8_t mFirstRow{0U};
    std::uint8_t mPressFirstRow{0U};
    std::uint8_t mRowHeight{kDefaultRowHeight};
    bool mWraps : 1 = true;
    bool mEditing : 1 = false;
    bool mPointerActive : 1 = false;
    bool mPopupPress : 1 = false;
    bool mDragged : 1 = false;
    bool mSuppressActivation : 1 = false;
    bool mFocusCleanupPending : 1 = false;
};

class SoftkeyView final : public View
{
public:
    SoftkeyView(Rect frame,
                const Font& font,
                std::string_view leftText,
                std::string_view rightText,
                ViewId leftAction,
                ViewId rightAction,
                Style style = {}) noexcept;

    void setLeftText(std::string_view text) noexcept;
    void setRightText(std::string_view text) noexcept;
    void setActions(ViewId leftAction, ViewId rightAction) noexcept;
    bool handleKey(Key key) noexcept override;
    void handlePointerEvent(PointerEvent event) noexcept override;
    void pointerPressed(Point localPoint) noexcept override;
    ViewId actionId() const noexcept override;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    const Font& mFont;
    std::string_view mLeftText;
    std::string_view mRightText;
    ViewId mLeftAction{0};
    ViewId mRightAction{0};
    bool mRightSelected{true};
};

// An equal-width strip of tabs whose pipe-delimited titles live in caller-owned
// immutable storage. Left/right step the selection and report the action;
// select (or an encoder push) advances and always wraps; a pointer selects the
// tab under its release, and dragging off every tab cancels.
class TabBarView : public View
{
public:
    static constexpr std::size_t kMaximumTabs{detail::kMaximumTabs};

    TabBarView(Rect frame,
               const Font& font,
               std::string_view titles,
               std::size_t selectedIndex = 0U,
               bool wraps = true,
               Style style = {}) noexcept;

    // Titles beyond kMaximumTabs are ignored; strict XML and the designer reject them.
    std::size_t tabCount() const noexcept
    {
        return mTabCount;
    }
    std::size_t selectedIndex() const noexcept
    {
        return mSelectedIndex;
    }
    std::string_view title(std::size_t index) const noexcept;
    bool wraps() const noexcept
    {
        return mWraps;
    }
    void setWraps(bool wraps) noexcept
    {
        mWraps = wraps;
    }
    // Clamps to the last tab. Each returns true only when the selection changed.
    bool setSelectedIndex(std::size_t index) noexcept;
    bool selectPrevious() noexcept;
    bool selectNext() noexcept;

    bool handleKey(Key key) noexcept override;
    void handlePointerEvent(PointerEvent event) noexcept override;
    void activate() noexcept override;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;
    void drawTabs(Painter& painter, Rect strip) const noexcept;
    // The strip's height from the top of this view.
    virtual std::int32_t stripHeight() const noexcept
    {
        return frame().height;
    }
    virtual void selectionChanged() noexcept
    {
    }

private:
    static constexpr std::uint8_t kNoTab{0xffU};

    Rect tabRect(Rect strip, std::uint8_t index) const noexcept;
    std::uint8_t tabAt(Point localPoint) const noexcept;
    void setPressedTab(std::uint8_t tab) noexcept;

    const Font& mFont;
    std::string_view mTitles;
    std::uint8_t mTabCount{0U};
    std::uint8_t mSelectedIndex{0U};
    std::uint8_t mPressedTab{kNoTab};
    bool mWraps : 1 = true;
    bool mPointerActivation : 1 = false;
};

// A tab strip above a content area whose children are the pages: page i is
// visible only while tab i is selected. Page frames use this view's coordinates
// and belong below the strip; the strip always receives its own pointer presses.
class TabView final : public TabBarView
{
public:
    TabView(Rect frame,
            const Font& font,
            std::string_view titles,
            std::size_t selectedIndex = 0U,
            std::uint16_t tabHeight = detail::kDefaultTabHeightPixels,
            bool wraps = true,
            Style style = {}) noexcept;

    std::uint16_t tabHeight() const noexcept
    {
        return mTabHeight;
    }
    void setTabHeight(std::uint16_t height) noexcept;
    // The page area in this view's coordinates.
    Rect contentFrame() const noexcept;
    // Shows the selected tab's page and hides the others. StaticLayout calls it
    // once the pages are attached; call it after attaching pages by hand.
    void syncPages() noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;
    std::int32_t stripHeight() const noexcept override;
    void selectionChanged() noexcept override
    {
        syncPages();
    }
    bool capturesPointerAt(Point localPoint) const noexcept override;

private:
    std::uint16_t mTabHeight{detail::kDefaultTabHeightPixels};
};

// A vertical drum of pipe-delimited items in caller-owned immutable storage. The selected item
// sits in a centered band and neighbors are dimmed by distance. A drag scrolls and snaps to the
// nearest item on release, wherever the contact lifts; a tap on a neighbor selects it. With keys
// or an encoder, select (push) starts editing, up/down (rotation) step while editing, select
// confirms, and back restores the value from before editing. Outside editing, up/down move focus,
// so an encoder-only product can always leave the wheel.
class WheelPickerView : public ControlView
{
public:
    static constexpr std::size_t kMaximumItems{detail::kMaximumWheelItems};

    WheelPickerView(Rect frame,
                    const Font& font,
                    std::string_view items,
                    std::size_t selectedIndex = 0U,
                    std::uint8_t rowHeight = detail::kDefaultWheelRowHeightPixels,
                    bool wraps = true,
                    Style style = {}) noexcept;

    // Items beyond kMaximumItems are ignored; strict XML and the designer reject them.
    std::size_t itemCount() const noexcept
    {
        return mItemCount;
    }
    std::size_t selectedIndex() const noexcept
    {
        return mSelectedIndex;
    }
    std::string_view item(std::size_t index) const noexcept;
    std::string_view selectedItem() const noexcept
    {
        return item(mSelectedIndex);
    }
    std::uint8_t rowHeight() const noexcept
    {
        return mRowHeight;
    }
    bool wraps() const noexcept
    {
        return mWraps;
    }
    bool editing() const noexcept
    {
        return mEditing;
    }
    // Clamps to the last item. Each returns true only when the selection changed.
    bool setSelectedIndex(std::size_t index) noexcept;
    bool selectPrevious() noexcept;
    bool selectNext() noexcept;

    bool handleKey(Key key) noexcept final;
    void handlePointerEvent(PointerEvent event) noexcept final;
    // A programmatic activation toggles editing, as select does.
    void activate() noexcept final;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;
    void focusChanged() noexcept final;
    void interactionFinished() noexcept final;

private:
    std::size_t steppedIndex(std::int32_t rows) const noexcept;
    std::int32_t bandTop() const noexcept;
    std::int32_t rowsFromCenter(std::int32_t localY) const noexcept;
    void setEditing(bool editing) noexcept;
    void finishEdit(bool cancel) noexcept;
    void toggleEditing() noexcept;
    void dragTo(std::int32_t localY) noexcept;

    const Font& mFont;
    std::string_view mItems;
    std::int32_t mDragOffset{0};
    std::int32_t mPressY{0};
    std::uint8_t mItemCount{0U};
    std::uint8_t mSelectedIndex{0U};
    std::uint8_t mEditStartIndex{0U};
    std::uint8_t mRowHeight{detail::kDefaultWheelRowHeightPixels};
    bool mWraps : 1 = true;
    bool mEditing : 1 = false;
    bool mPointerActivation : 1 = false;
    bool mDragged : 1 = false;
    bool mFocusCleanupPending : 1 = false;
};

} // namespace lithe
