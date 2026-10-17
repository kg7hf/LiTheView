// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/detail/number.hpp"
#include "lithe/widgets.hpp"

#include <algorithm>

namespace lithe
{
namespace
{
constexpr std::uint8_t kNoItem{255U};
constexpr std::int32_t kInset{4};
constexpr std::int32_t kIndicatorWidth{14};
constexpr std::int32_t kDragSlop{6};
} // namespace

DropDownView::DropDownView(Rect frame,
                           const Font& font,
                           std::string_view items,
                           std::size_t selectedIndex,
                           std::uint8_t rowHeight,
                           bool wraps,
                           Style style) noexcept
    : ControlView(frame, style), mFont(font), mItems(items),
      mRowHeight(rowHeight == 0U ? kDefaultRowHeight : rowHeight), mWraps(wraps)
{
    std::size_t count = items.empty() ? 0U : 1U;
    for (const char character : items)
    {
        if (character == '|')
        {
            ++count;
        }
    }
    // Unlike a truncated model, rejecting overflow cannot silently change item identity.
    if (count <= kMaximumItems && selectedIndex < count)
    {
        detail::assignIfRepresentable(count, mItemCount);
        detail::assignIfRepresentable(selectedIndex, mSelectedIndex);
    }
    setFocusable(true);
}

std::string_view DropDownView::item(std::size_t index) const noexcept
{
    if (index >= mItemCount)
    {
        return {};
    }
    std::size_t start = 0U;
    for (std::size_t current = 0U; current < index; ++current)
    {
        start = mItems.find('|', start) + 1U;
    }
    const std::size_t end = mItems.find('|', start);
    return mItems.substr(start,
                         end == std::string_view::npos ? mItems.size() - start : end - start);
}

Rect DropDownView::availablePopup() const noexcept
{
    if (mItemCount == 0U || !interactionEnabled())
    {
        return {};
    }
    const Rect anchor = frameInRoot();
    Rect visibleAnchor = anchor;
    const View* root = this;
    for (const View* ancestor = parent(); ancestor != nullptr; ancestor = ancestor->parent())
    {
        visibleAnchor = intersect(visibleAnchor, ancestor->frameInRoot());
        root = ancestor;
    }
    if (visibleAnchor.empty() || root == this)
    {
        return {};
    }
    const Rect bounds = root->frameInRoot();
    const std::int32_t below = std::max(
        std::int32_t{0}, detail::saturatingSubtract(bounds.bottom(), visibleAnchor.bottom()));
    const std::int32_t above =
        std::max(std::int32_t{0}, detail::saturatingSubtract(visibleAnchor.y, bounds.y));
    const std::int32_t wanted = std::min(mItemCount, kMaximumVisibleRows) * mRowHeight;
    const bool downward = below >= wanted || below >= above;
    const std::int32_t height =
        (std::min(wanted, downward ? below : above) / mRowHeight) * mRowHeight;
    if (height == 0)
    {
        return {};
    }
    return {visibleAnchor.x,
            downward ? visibleAnchor.bottom() : detail::saturatingSubtract(visibleAnchor.y, height),
            visibleAnchor.width,
            height};
}

Rect DropDownView::popupFrame() const noexcept
{
    return mEditing ? availablePopup() : Rect{};
}

void DropDownView::ensureSelectionVisible() noexcept
{
    const std::int32_t rows = availablePopup().height / mRowHeight;
    if (rows == 0)
    {
        return;
    }
    const std::int32_t first = std::clamp<std::int32_t>(mFirstRow, 0, mItemCount - rows);
    const std::int32_t next = std::clamp<std::int32_t>(
        first, std::max<std::int32_t>(0, mSelectedIndex - rows + 1), mSelectedIndex);
    detail::assignIfRepresentable(detail::nonnegativeSize(next), mFirstRow);
}

bool DropDownView::open() noexcept
{
    if (mEditing || !focused() || availablePopup().empty())
    {
        return false;
    }
    mInitialIndex = mSelectedIndex;
    mEditing = true;
    ensureSelectionVisible();
    invalidateRootRegion(unite(frameInRoot(), popupFrame()));
    invalidate();
    return true;
}

bool DropDownView::beginEdit() noexcept
{
    const InteractionGuard interaction{*this};
    return interaction && open();
}

bool DropDownView::setSelectedIndex(std::size_t index) noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || index >= mItemCount)
    {
        return false;
    }
    invalidateRootRegion(unite(frameInRoot(), popupFrame()));
    mEditing = false;
    mPointerActive = false;
    mSuppressActivation = false;
    detail::assignIfRepresentable(index, mSelectedIndex);
    invalidate();
    return true;
}

void DropDownView::changeSelection(std::uint8_t index) noexcept
{
    const std::size_t previous = mSelectedIndex;
    mSelectedIndex = index;
    ensureSelectionVisible();
    invalidateRootRegion(unite(frameInRoot(), popupFrame()));
    invalidate();
    notifySelectionChanged({previous, mSelectedIndex});
}

void DropDownView::finish(bool cancel) noexcept
{
    const std::size_t previous = mSelectedIndex;
    invalidateRootRegion(unite(frameInRoot(), popupFrame()));
    if (cancel)
    {
        mSelectedIndex = mInitialIndex;
    }
    mEditing = false;
    mPointerActive = false;
    invalidate();
    const SelectionEdit event{mInitialIndex, mSelectedIndex};
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

bool DropDownView::commitEdit() noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled() || !mEditing)
    {
        return false;
    }
    finish(false);
    return true;
}

bool DropDownView::cancelEdit() noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !mEditing)
    {
        return false;
    }
    finish(true);
    return true;
}

bool DropDownView::handleKey(Key key) noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled() || mItemCount == 0U || mPointerActive)
    {
        return false;
    }
    mSuppressActivation = false;
    if (key == Key::select)
    {
        if (mEditing)
        {
            finish(false);
            return true;
        }
        return open();
    }
    if (!mEditing)
    {
        return false;
    }
    if (key == Key::back)
    {
        finish(true);
        return true;
    }
    std::int32_t next = mSelectedIndex;
    if (key == Key::up || key == Key::left)
    {
        --next;
    }
    else if (key == Key::down || key == Key::right)
    {
        ++next;
    }
    else
    {
        return false;
    }
    next = mWraps ? (next + mItemCount) % mItemCount
                  : std::clamp<std::int32_t>(next, 0, mItemCount - 1);
    std::uint8_t index = 0U;
    if (detail::assignIfRepresentable(detail::nonnegativeSize(next), index))
    {
        changeSelection(index);
    }
    return true;
}

std::int32_t DropDownView::firstVisibleRow() const noexcept
{
    return std::clamp<std::int32_t>(mFirstRow, 0, mItemCount - popupFrame().height / mRowHeight);
}

std::uint8_t DropDownView::rowAt(Point localPoint) const noexcept
{
    const Rect anchor = frameInRoot();
    const Point absolute{detail::saturatingAdd(anchor.x, localPoint.x),
                         detail::saturatingAdd(anchor.y, localPoint.y)};
    const Rect popup = popupFrame();
    if (!popup.contains(absolute))
    {
        return kNoItem;
    }
    const std::int32_t index = firstVisibleRow() + (absolute.y - popup.y) / mRowHeight;
    std::uint8_t result = kNoItem;
    if (index < mItemCount)
    {
        detail::assignIfRepresentable(detail::nonnegativeSize(index), result);
    }
    return result;
}

void DropDownView::scrollTo(std::int32_t localY) noexcept
{
    const std::int32_t travel = detail::saturatingSubtract(mPressY, localY);
    mDragged = mDragged || travel > kDragSlop || travel < -kDragSlop;
    if (!mDragged)
    {
        return;
    }
    const std::int32_t rows = popupFrame().height / mRowHeight;
    const std::int32_t first = std::clamp<std::int32_t>(
        detail::saturatingAdd(mPressFirstRow, travel / mRowHeight), 0, mItemCount - rows);
    detail::assignIfRepresentable(detail::nonnegativeSize(first), mFirstRow);
    invalidateRootRegion(unite(frameInRoot(), popupFrame()));
}

void DropDownView::handlePointerEvent(PointerEvent event) noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || (!interactionEnabled() && event.phase != PointerPhase::cancelled))
    {
        return;
    }
    View::handlePointerEvent(event);
    const bool inside = Rect{0, 0, frame().width, frame().height}.contains(event.position);
    switch (event.phase)
    {
    case PointerPhase::pressed:
        mPointerActive = true;
        mSuppressActivation = false;
        mPopupPress = rowAt(event.position) != kNoItem;
        mPressY = event.position.y;
        mPressFirstRow = mFirstRow;
        mDragged = false;
        break;
    case PointerPhase::moved:
        if (mPointerActive && mPopupPress)
        {
            scrollTo(event.position.y);
        }
        break;
    case PointerPhase::released:
        if (!mPointerActive)
        {
            break;
        }
        mPointerActive = false;
        mSuppressActivation = inside || rowAt(event.position) != kNoItem;
        if (mPopupPress)
        {
            scrollTo(event.position.y);
            const std::uint8_t row = rowAt(event.position);
            if (!mDragged && row != kNoItem)
            {
                changeSelection(row);
                // A callback can disable the control. Finish that edit as a cancellation.
                if (mEditing)
                {
                    finish(!interactionEnabled() || !focused());
                }
            }
            else if (row == kNoItem && mEditing)
            {
                finish(true);
            }
        }
        else if (inside)
        {
            if (mEditing)
            {
                finish(true);
            }
            else
            {
                open();
            }
        }
        break;
    case PointerPhase::cancelled:
        mPointerActive = false;
        mSuppressActivation = false;
        if (mEditing)
        {
            finish(true);
        }
        break;
    }
}

void DropDownView::activate() noexcept
{
    const InteractionGuard interaction{*this};
    if (!interaction || !interactionEnabled())
    {
        return;
    }
    if (mSuppressActivation)
    {
        mSuppressActivation = false;
        return;
    }
    if (mEditing)
    {
        finish(false);
    }
    else
    {
        open();
    }
}

void DropDownView::focusChanged() noexcept
{
    if (focused())
    {
        return;
    }
    const InteractionGuard interaction{*this};
    if (!interaction)
    {
        mFocusCleanupPending = true;
        return;
    }
    if (mEditing)
    {
        finish(true);
    }
    mPointerActive = false;
    mSuppressActivation = false;
}

void DropDownView::interactionFinished() noexcept
{
    if (mFocusCleanupPending || !interactionEnabled())
    {
        mFocusCleanupPending = false;
        if (mEditing && (!focused() || !interactionEnabled()))
        {
            finish(true);
        }
        if (!focused() || !interactionEnabled())
        {
            mPointerActive = false;
            mSuppressActivation = false;
        }
    }
}

void DropDownView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    const Style current = resolvedStyle();
    painter.drawText({detail::saturatingAdd(absoluteFrame.x, kInset),
                      absoluteFrame.y,
                      detail::saturatingSubtract(absoluteFrame.width, kIndicatorWidth + kInset),
                      absoluteFrame.height},
                     selectedItem(),
                     mFont,
                     current.foreground,
                     {HorizontalAlign::left, VerticalAlign::center},
                     current.textScale);
    painter.drawText({detail::saturatingSubtract(absoluteFrame.right(), kIndicatorWidth),
                      absoluteFrame.y,
                      kIndicatorWidth,
                      absoluteFrame.height},
                     mEditing ? "^" : "v",
                     mFont,
                     current.foreground,
                     {HorizontalAlign::center, VerticalAlign::center},
                     1U);
}

void DropDownView::drawPopup(Painter& painter, Rect absolutePopup) noexcept
{
    const Style current = resolvedStyle();
    // A popup must cover lower siblings even when the closed selector is transparent.
    const Color background = current.background.blendedOver(Color::rgb(0U));
    painter.fillRect(absolutePopup, background);
    const std::int32_t rows = absolutePopup.height / mRowHeight;
    for (std::int32_t row = 0; row < rows; ++row)
    {
        const std::size_t index = detail::nonnegativeSize(firstVisibleRow() + row);
        const Rect area{absolutePopup.x,
                        detail::saturatingAdd(absolutePopup.y, row * mRowHeight),
                        absolutePopup.width,
                        mRowHeight};
        if (index == mSelectedIndex)
        {
            painter.strokeRect(area, current.foreground, 1U);
        }
        painter.drawText({detail::saturatingAdd(area.x, kInset),
                          area.y,
                          detail::saturatingSubtract(area.width, kIndicatorWidth + kInset),
                          area.height},
                         item(index),
                         mFont,
                         current.foreground,
                         {HorizontalAlign::left, VerticalAlign::center},
                         current.textScale);
    }
    if (firstVisibleRow() != 0)
    {
        painter.drawText({detail::saturatingSubtract(absolutePopup.right(), kIndicatorWidth),
                          absolutePopup.y,
                          kIndicatorWidth,
                          mRowHeight},
                         "^",
                         mFont,
                         current.foreground,
                         {},
                         1U);
    }
    if (firstVisibleRow() + rows < mItemCount)
    {
        painter.drawText({detail::saturatingSubtract(absolutePopup.right(), kIndicatorWidth),
                          detail::saturatingSubtract(absolutePopup.bottom(), mRowHeight),
                          kIndicatorWidth,
                          mRowHeight},
                         "v",
                         mFont,
                         current.foreground,
                         {},
                         1U);
    }
    painter.strokeRect(absolutePopup, current.foreground, 1U);
}
} // namespace lithe
