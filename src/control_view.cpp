// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/control_view.hpp"

namespace lithe
{

bool ControlView::interactionEnabled() const noexcept
{
    for (const View* view = this; view != nullptr; view = view->parent())
    {
        if (!view->visible() || !view->enabled())
        {
            return false;
        }
    }
    return true;
}

void ControlView::interactionFinished() noexcept
{
}

void ControlView::onCheckedChanged([[maybe_unused]] CheckedChange event) noexcept
{
}

void ControlView::onSelectionChanged([[maybe_unused]] SelectionChange event) noexcept
{
}

void ControlView::onRadioSelectionChanged([[maybe_unused]] RadioSelectionChange event) noexcept
{
}

void ControlView::onEditCommitted([[maybe_unused]] SelectionEdit event) noexcept
{
}

void ControlView::onEditCancelled([[maybe_unused]] SelectionEdit event) noexcept
{
}

void ControlView::notifyCheckedChanged(CheckedChange event) noexcept
{
    if (event.previousValue == event.value)
    {
        return;
    }
    onCheckedChanged(event);
    if (mEventSink != nullptr)
    {
        mEventSink->checkedChanged(*this, event);
    }
}

void ControlView::notifySelectionChanged(SelectionChange event) noexcept
{
    if (event.previousIndex == event.index)
    {
        return;
    }
    onSelectionChanged(event);
    if (mEventSink != nullptr)
    {
        mEventSink->selectionChanged(*this, event);
    }
}

void ControlView::notifyRadioSelectionChanged(RadioSelectionChange event) noexcept
{
    if (event.previous == event.selected)
    {
        return;
    }
    onRadioSelectionChanged(event);
    if (mEventSink != nullptr)
    {
        mEventSink->radioSelectionChanged(*this, event);
    }
}

void ControlView::notifyEditCommitted(SelectionEdit event) noexcept
{
    onEditCommitted(event);
    if (mEventSink != nullptr)
    {
        mEventSink->editCommitted(*this, event);
    }
}

void ControlView::notifyEditCancelled(SelectionEdit event) noexcept
{
    onEditCancelled(event);
    if (mEventSink != nullptr)
    {
        mEventSink->editCancelled(*this, event);
    }
}

void ControlView::onTextChanged([[maybe_unused]] TextChange event) noexcept
{
}

void ControlView::notifyTextChanged(TextChange event) noexcept
{
    onTextChanged(event);
    if (mEventSink != nullptr)
    {
        mEventSink->textChanged(*this, event);
    }
}

void ControlView::onTextEditCommitted([[maybe_unused]] TextEdit event) noexcept
{
}

void ControlView::notifyTextEditCommitted(TextEdit event) noexcept
{
    onTextEditCommitted(event);
    if (mEventSink != nullptr)
    {
        mEventSink->textEditCommitted(*this, event);
    }
}

void ControlView::onTextEditCancelled([[maybe_unused]] TextEdit event) noexcept
{
}

void ControlView::notifyTextEditCancelled(TextEdit event) noexcept
{
    onTextEditCancelled(event);
    if (mEventSink != nullptr)
    {
        mEventSink->textEditCancelled(*this, event);
    }
}

ControlEventSink* ControlView::exchangeEventSink(ControlEventSink* sink) noexcept
{
    return std::exchange(mEventSink, sink);
}

} // namespace lithe
