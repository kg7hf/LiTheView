// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "lithe/control_view.hpp"
#include "lithe/font.hpp"

#include <algorithm>
#include <optional>
#include <span>
#include <string_view>
namespace lithe
{
class ListView : public ControlView
{
public:
    static constexpr std::size_t kMaximumItems{64U};
    static constexpr std::uint8_t kNoRow{255U};
    static constexpr std::int32_t kDefaultRowHeight{28};
    static constexpr std::int32_t kScrollWidth{28};
    ListView(Rect frame,
             const Font& font,
             std::string_view items = {},
             std::size_t selected = 0U,
             std::uint8_t rowHeight = 28U,
             Style style = {}) noexcept
        : ControlView(frame, style), mPipe(items),
          mRowHeight(rowHeight == 0U ? kDefaultRowHeight : rowHeight), mFont(&font)
    {
        setFocusable(true);
        if (selected < itemCount())
            mSelected = selected;
        ensureVisible();
    }
    // Caller storage is borrowed. A replacement silently clamps selection and scroll.
    [[nodiscard]] bool setItems(std::span<const std::string_view> items) noexcept
    {
        const InteractionGuard interaction{*this};
        if (!interaction || items.size() > kMaximumItems)
            return false;
        mItems = items;
        mPipe = {};
        if (mSelected >= itemCount())
            mSelected = itemCount() == 0U ? 0U : itemCount() - 1U;
        mFirst = 0U;
        ensureVisible();
        mPressedRow = kNoRow;
        mPressedScroll = Key::back;
        invalidate();
        return true;
    }
    // Silent application refresh; event callbacks may not recursively alter the list.
    [[nodiscard]] bool setSelectedIndex(std::size_t index) noexcept
    {
        const InteractionGuard interaction{*this};
        if (!interaction || index >= itemCount())
            return false;
        mSelected = index;
        ensureVisible();
        invalidate();
        return true;
    }
    [[nodiscard]] std::size_t itemCount() const noexcept
    {
        if (!mItems.empty())
            return mItems.size();
        if (mPipe.empty())
            return 0U;
        std::size_t count = 1U;
        for (const char ch : mPipe)
        {
            if (ch == '|' && ++count > kMaximumItems)
                return 0U;
        }
        return count;
    }
    [[nodiscard]] std::string_view item(std::size_t index) const noexcept
    {
        if (index >= itemCount())
            return {};
        if (!mItems.empty())
            return mItems[index];
        auto remaining = mPipe;
        for (std::size_t i = 0U; i < index; ++i)
            remaining.remove_prefix(remaining.find('|') + 1U);
        return remaining.substr(0U, remaining.find('|'));
    }
    [[nodiscard]] std::size_t selectedIndex() const noexcept
    {
        return mSelected;
    }
    [[nodiscard]] std::size_t firstVisibleIndex() const noexcept
    {
        return mFirst;
    }
    bool handleKey(Key key) noexcept override
    {
        const InteractionGuard interaction{*this};
        if (!interaction || !interactionEnabled() || itemCount() == 0U)
        {
            return false;
        }
        mSuppressActivation = false;
        if (key == Key::up || key == Key::down)
        {
            if (key == Key::up && mSelected != 0U)
            {
                change(mSelected - 1U);
            }
            if (key == Key::down && mSelected + 1U < itemCount())
            {
                change(mSelected + 1U);
            }
            return true; // Clamp at the endpoints, never wrap unexpectedly.
        }
        return false; // Left/right traverse focus; center activates the selection.
    }
    void handlePointerEvent(PointerEvent event) noexcept override
    {
        const InteractionGuard interaction{*this};
        if (!interaction || !interactionEnabled())
        {
            return;
        }
        View::handlePointerEvent(event);
        if (event.phase == PointerPhase::pressed)
        {
            mSuppressActivation = false;
            mPressedRow = rowAt(event.position).value_or(kNoRow);
            mPressedScroll = scrollAt(event.position).value_or(Key::back);
        }
        else if (event.phase == PointerPhase::moved)
        {
            if (rowAt(event.position).value_or(kNoRow) != mPressedRow)
            {
                mPressedRow = kNoRow;
            }
            if (scrollAt(event.position).value_or(Key::back) != mPressedScroll)
            {
                mPressedScroll = Key::back;
            }
        }
        else
        {
            const auto released = rowAt(event.position);
            const auto pressed = mPressedRow;
            const auto scroll = mPressedScroll;
            mPressedScroll = Key::back;
            mPressedRow = kNoRow;
            mSuppressActivation = event.phase == PointerPhase::released;
            if (event.phase == PointerPhase::released && pressed != kNoRow && released == pressed)
            {
                change(*released);
            }
            if (event.phase == PointerPhase::released && scroll != Key::back &&
                scrollAt(event.position) == scroll)
            {
                if (scroll == Key::up && mSelected != 0U)
                {
                    change(mSelected - 1U);
                }
                if (scroll == Key::down && mSelected + 1U < itemCount())
                {
                    change(mSelected + 1U);
                }
            }
        }
    }
    void activate() noexcept override
    {
        const InteractionGuard interaction{*this};
        if (!interaction || !interactionEnabled() || itemCount() == 0U)
        {
            return;
        }
        if (mSuppressActivation)
        {
            mSuppressActivation = false;
            return;
        }
        notifyItemActivated({mSelected});
    }

protected:
    void focusChanged() noexcept override
    {
        mPressedRow = kNoRow;
        mPressedScroll = Key::back;
        mSuppressActivation = false;
        invalidate();
    }
    void drawSelf(Painter& painter, Rect bounds) noexcept override
    {
        const auto current = resolvedStyle();
        const auto base = style();
        painter.fillRect(bounds, base.background);
        if (mFont == nullptr)
        {
            return;
        }
        auto row = bounds;
        row.height = mRowHeight;
        if (itemCount() > visibleRows())
        {
            row.width -= kScrollWidth;
        }
        for (auto index = mFirst; index < itemCount() && row.bottom() <= bounds.bottom(); ++index)
        {
            if (index == mSelected)
            {
                painter.fillRect(row, enabled() ? base.accent : current.background);
            }
            painter.drawText({row.x + 8, row.y, row.width - 20, row.height},
                             item(index),
                             *mFont,
                             current.foreground,
                             {HorizontalAlign::left, VerticalAlign::center},
                             base.textScale);
            row.y += mRowHeight;
        }
        // Explicit touch targets also make off-screen entries reachable on a touch-only board.
        if (itemCount() > visibleRows())
        {
            const auto x = bounds.right() - kScrollWidth;
            painter.strokeRect({x, bounds.y, kScrollWidth, mRowHeight}, base.border, 1U);
            painter.strokeRect(
                {x, bounds.bottom() - mRowHeight, kScrollWidth, mRowHeight}, base.border, 1U);
            const auto arrow = [&](std::int32_t y, bool up, bool available)
            {
                const auto color = available && enabled() ? current.foreground : base.border;
                for (std::int32_t n = 0; n < 6; ++n)
                {
                    painter.fillRect({x + kScrollWidth / 2 - n, y + (up ? n : 5 - n), 2 * n + 1, 1},
                                     color);
                }
            };
            arrow(bounds.y + 15, true, mSelected != 0U);
            arrow(bounds.bottom() - 21, false, mSelected + 1U < itemCount());
        }
        painter.strokeRect(bounds, focused() ? current.foreground : base.border, 1U);
    }

private:
    void ensureVisible() noexcept
    {
        if (mSelected < mFirst)
            mFirst = mSelected;
        else if (mSelected >= mFirst + visibleRows())
            mFirst = mSelected - visibleRows() + 1U;
    }
    [[nodiscard]] std::size_t visibleRows() const noexcept
    {
        // LITHEVIEW_CAST_BOUNDARY: max below proves rows is positive and fits size_t.
        const auto rows = std::max(std::int32_t{1}, frame().height / mRowHeight);
        return static_cast<std::size_t>(rows);
    }
    [[nodiscard]] std::optional<Key> scrollAt(Point point) const noexcept
    {
        if (itemCount() <= visibleRows() ||
            !Rect{frame().width - kScrollWidth, 0, kScrollWidth, frame().height}.contains(point))
        {
            return std::nullopt;
        }
        if (point.y < mRowHeight)
        {
            return Key::up;
        }
        if (point.y >= frame().height - mRowHeight)
        {
            return Key::down;
        }
        return std::nullopt;
    }
    [[nodiscard]] std::optional<std::uint8_t> rowAt(Point point) const noexcept
    {
        if (!Rect{0, 0, frame().width, frame().height}.contains(point))
        {
            return std::nullopt;
        }
        if (itemCount() > visibleRows() && point.x >= frame().width - kScrollWidth)
        {
            return std::nullopt;
        }
        // LITHEVIEW_CAST_BOUNDARY: rectangle containment proves point.y is nonnegative.
        const auto row = static_cast<std::size_t>(point.y / mRowHeight);
        if (row >= visibleRows() || mFirst + row >= itemCount())
        {
            return std::nullopt;
        }
        // LITHEVIEW_CAST_BOUNDARY: an accepted item index is below kMaximumItems (64).
        return static_cast<std::uint8_t>(mFirst + row);
    }
    void change(std::size_t index) noexcept
    {
        if (index >= itemCount() || index == mSelected)
        {
            return;
        }
        const auto previous = mSelected;
        mSelected = index;
        ensureVisible();
        invalidate();
        notifySelectionChanged({previous, mSelected});
    }
    std::span<const std::string_view> mItems;
    std::string_view mPipe;
    std::int32_t mRowHeight{kDefaultRowHeight};
    const Font* mFont{};

    std::size_t mSelected{};
    std::size_t mFirst{};
    std::uint8_t mPressedRow{kNoRow};
    Key mPressedScroll{Key::back};
    bool mSuppressActivation{};
};
} // namespace lithe
