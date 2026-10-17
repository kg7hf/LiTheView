// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/control_events.hpp"
#include "lithe/view.hpp"

#include <utility>

namespace lithe
{
// Only event-producing controls carry an event sink. A controller borrows it for
// one synchronous input call, then restores it. Direct calls reach control hooks
// only. All mutation and dispatch belong to the application's UI owner task.
class ControlView : public View
{
public:
    using View::View;

protected:
    class InteractionGuard
    {
    public:
        explicit InteractionGuard(ControlView& control) noexcept
            : mControl(control), mEntered(!control.mControlInteracting)
        {
            if (mEntered)
            {
                mControl.mControlInteracting = true;
            }
        }
        ~InteractionGuard()
        {
            if (mEntered)
            {
                mControl.interactionFinished();
                mControl.mControlInteracting = false;
            }
        }
        InteractionGuard(const InteractionGuard&) = delete;
        InteractionGuard& operator=(const InteractionGuard&) = delete;
        InteractionGuard(InteractionGuard&&) = delete;
        InteractionGuard& operator=(InteractionGuard&&) = delete;
        explicit operator bool() const noexcept
        {
            return mEntered;
        }

    private:
        ControlView& mControl;
        const bool mEntered;
    };

    bool interactionEnabled() const noexcept;

    // A property mutation in a callback may request focus cleanup. Run it while
    // the original event sink and recursion guard are still active.
    virtual void interactionFinished() noexcept;

    virtual void onCheckedChanged([[maybe_unused]] CheckedChange event) noexcept;
    virtual void onSelectionChanged([[maybe_unused]] SelectionChange event) noexcept;
    virtual void onRadioSelectionChanged([[maybe_unused]] RadioSelectionChange event) noexcept;
    virtual void onEditCommitted([[maybe_unused]] SelectionEdit event) noexcept;
    virtual void onEditCancelled([[maybe_unused]] SelectionEdit event) noexcept;

    void notifyCheckedChanged(CheckedChange event) noexcept;
    void notifySelectionChanged(SelectionChange event) noexcept;
    void notifyRadioSelectionChanged(RadioSelectionChange event) noexcept;
    void notifyEditCommitted(SelectionEdit event) noexcept;
    void notifyEditCancelled(SelectionEdit event) noexcept;

    virtual void onTextChanged([[maybe_unused]] TextChange event) noexcept;
    void notifyTextChanged(TextChange event) noexcept;

    virtual void onTextEditCommitted([[maybe_unused]] TextEdit event) noexcept;
    void notifyTextEditCommitted(TextEdit event) noexcept;

    virtual void onTextEditCancelled([[maybe_unused]] TextEdit event) noexcept;
    void notifyTextEditCancelled(TextEdit event) noexcept;

private:
    ControlEventSink* exchangeEventSink(ControlEventSink* sink) noexcept final;
    ControlEventSink* mEventSink{nullptr};
};
} // namespace lithe
