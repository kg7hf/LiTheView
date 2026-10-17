// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "lithe/control_view.hpp"

#include <algorithm>
#include <optional>
#include <span>
#include <string_view>
namespace menu::controls
{
// A borrowed synchronous interface. The owning form and catalog outlive the
// list.
class ListEvents
{
public:
    ListEvents() = default;
    ListEvents(const ListEvents&) = delete;
    ListEvents& operator=(const ListEvents&) = delete;
    ListEvents(ListEvents&&) = delete;
    ListEvents& operator=(ListEvents&&) = delete;
    virtual ~ListEvents() = default;
    virtual void onListSelectionChanged(lithe::SelectionChange event) noexcept = 0;
    virtual void onListActivated(std::size_t index) noexcept = 0;
};
class SelectionList : public lithe::ControlView
{
public:
    static constexpr std::size_t kMaximumItems{64U};
    static constexpr std::int32_t kRowHeight{36};
    static constexpr std::int32_t kScrollWidth{28};
    explicit SelectionList(lithe::Rect frame, lithe::Style style = {}) noexcept
        : ControlView(frame, style)
    {
        setFocusable(true);
    }
    [[nodiscard]] bool configure(std::span<const std::string_view> items,
                                 const lithe::Font& font,
                                 ListEvents& events) noexcept
    {
        if (mEvents != nullptr || items.empty() || items.size() > kMaximumItems ||
            frame().height < kRowHeight || frame().width < 48)
        {
            return false;
        }
        mItems = items;
        mFont = &font;
        mEvents = &events;
        invalidate();
        return true;
    }
    [[nodiscard]] std::size_t selectedIndex() const noexcept
    {
        return mSelected;
    }
    [[nodiscard]] std::size_t firstVisibleIndex() const noexcept
    {
        return mFirst;
    }
    bool handleKey(lithe::Key key) noexcept override
    {
        const InteractionGuard interaction{*this};
        if (!interaction || !interactionEnabled() || mItems.empty())
        {
            return false;
        }
        mSuppressActivation = false;
        if (key == lithe::Key::up || key == lithe::Key::down)
        {
            if (key == lithe::Key::up && mSelected != 0U)
            {
                change(mSelected - 1U);
            }
            if (key == lithe::Key::down && mSelected + 1U < mItems.size())
            {
                change(mSelected + 1U);
            }
            return true; // Clamp at the endpoints, never wrap unexpectedly.
        }
        return false; // Left/right traverse focus; center activates the selection.
    }
    void handlePointerEvent(lithe::PointerEvent event) noexcept override
    {
        const InteractionGuard interaction{*this};
        if (!interaction || !interactionEnabled())
        {
            return;
        }
        View::handlePointerEvent(event);
        if (event.phase == lithe::PointerPhase::pressed)
        {
            mSuppressActivation = false;
            mPressedRow = rowAt(event.position);
            mPressedScroll = scrollAt(event.position);
        }
        else if (event.phase == lithe::PointerPhase::moved)
        {
            if (rowAt(event.position) != mPressedRow)
            {
                mPressedRow.reset();
            }
            if (scrollAt(event.position) != mPressedScroll)
            {
                mPressedScroll.reset();
            }
        }
        else
        {
            const auto released = rowAt(event.position);
            const auto pressed = mPressedRow;
            const auto scroll = mPressedScroll;
            mPressedScroll.reset();
            mPressedRow.reset();
            mSuppressActivation = event.phase == lithe::PointerPhase::released;
            if (event.phase == lithe::PointerPhase::released && pressed && released == pressed)
            {
                change(*released);
            }
            if (event.phase == lithe::PointerPhase::released && scroll &&
                scrollAt(event.position) == scroll)
            {
                if (*scroll == lithe::Key::up && mSelected != 0U)
                {
                    change(mSelected - 1U);
                }
                if (*scroll == lithe::Key::down && mSelected + 1U < mItems.size())
                {
                    change(mSelected + 1U);
                }
            }
        }
    }
    void activate() noexcept override
    {
        const InteractionGuard interaction{*this};
        if (!interaction || !interactionEnabled() || mEvents == nullptr)
        {
            return;
        }
        if (mSuppressActivation)
        {
            mSuppressActivation = false;
            return;
        }
        mEvents->onListActivated(mSelected);
    }

protected:
    void onSelectionChanged(lithe::SelectionChange event) noexcept override
    {
        if (mEvents != nullptr)
        {
            mEvents->onListSelectionChanged(event);
        }
    }
    void focusChanged() noexcept override
    {
        mPressedRow.reset();
        mPressedScroll.reset();
        mSuppressActivation = false;
        invalidate();
    }
    void drawSelf(lithe::Painter& painter, lithe::Rect bounds) noexcept override
    {
        const auto current = resolvedStyle();
        const auto base = style();
        painter.fillRect(bounds, base.background);
        if (mFont == nullptr)
        {
            return;
        }
        auto row = bounds;
        row.height = kRowHeight;
        if (mItems.size() > visibleRows())
        {
            row.width -= kScrollWidth;
        }
        for (auto index = mFirst; index < mItems.size() && row.bottom() <= bounds.bottom(); ++index)
        {
            if (index == mSelected)
            {
                painter.fillRect(row, enabled() ? base.accent : current.background);
            }
            painter.drawText({row.x + 8, row.y, row.width - 20, row.height},
                             mItems[index],
                             *mFont,
                             current.foreground,
                             {lithe::HorizontalAlign::left, lithe::VerticalAlign::center},
                             base.textScale);
            row.y += kRowHeight;
        }
        // Explicit touch targets also make off-screen entries reachable on a
        // touch-only board.
        if (mItems.size() > visibleRows())
        {
            const auto x = bounds.right() - kScrollWidth;
            painter.strokeRect({x, bounds.y, kScrollWidth, kRowHeight}, base.border, 1U);
            painter.strokeRect(
                {x, bounds.bottom() - kRowHeight, kScrollWidth, kRowHeight}, base.border, 1U);
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
            arrow(bounds.bottom() - 21, false, mSelected + 1U < mItems.size());
        }
        painter.strokeRect(bounds, focused() ? current.foreground : base.border, 1U);
    }

private:
    [[nodiscard]] std::size_t visibleRows() const noexcept
    {
        const auto rows = std::max(std::int32_t{1}, frame().height / kRowHeight);
        // LITHEVIEW_CAST_BOUNDARY: rows is positive and fits size_t.
        return static_cast<std::size_t>(rows);
    }
    [[nodiscard]] std::optional<lithe::Key> scrollAt(lithe::Point point) const noexcept
    {
        if (mItems.size() <= visibleRows() ||
            !lithe::Rect{frame().width - kScrollWidth, 0, kScrollWidth, frame().height}.contains(
                point))
        {
            return std::nullopt;
        }
        if (point.y < kRowHeight)
        {
            return lithe::Key::up;
        }
        if (point.y >= frame().height - kRowHeight)
        {
            return lithe::Key::down;
        }
        return std::nullopt;
    }
    [[nodiscard]] std::optional<std::size_t> rowAt(lithe::Point point) const noexcept
    {
        if (!lithe::Rect{0, 0, frame().width, frame().height}.contains(point))
        {
            return std::nullopt;
        }
        if (mItems.size() > visibleRows() && point.x >= frame().width - kScrollWidth)
        {
            return std::nullopt;
        }
        // LITHEVIEW_CAST_BOUNDARY: rectangle containment proves point.y is
        // nonnegative.
        const auto row = static_cast<std::size_t>(point.y / kRowHeight);
        if (row >= visibleRows() || mFirst + row >= mItems.size())
        {
            return std::nullopt;
        }
        return mFirst + row;
    }
    void change(std::size_t index) noexcept
    {
        if (index >= mItems.size() || index == mSelected)
        {
            return;
        }
        const auto previous = mSelected;
        mSelected = index;
        if (mSelected < mFirst)
        {
            mFirst = mSelected;
        }
        else if (mSelected >= mFirst + visibleRows())
        {
            mFirst = mSelected - visibleRows() + 1U;
        }
        invalidate();
        notifySelectionChanged({previous, mSelected});
    }
    std::span<const std::string_view> mItems;
    const lithe::Font* mFont{};
    ListEvents* mEvents{};
    std::size_t mSelected{};
    std::size_t mFirst{};
    std::optional<std::size_t> mPressedRow;
    std::optional<lithe::Key> mPressedScroll;
    bool mSuppressActivation{};
};
} // namespace menu::controls
