// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/control_events.hpp"
#include "lithe/gesture.hpp"
#include "lithe/view.hpp"

#include <array>
#include <cstddef>
#include <span>

namespace lithe
{

class ViewController;

class ControllerDelegate
{
public:
    ControllerDelegate() = default;
    virtual ~ControllerDelegate() = default;

    ControllerDelegate(const ControllerDelegate&) = delete;
    ControllerDelegate& operator=(const ControllerDelegate&) = delete;
    ControllerDelegate(ControllerDelegate&&) = delete;
    ControllerDelegate& operator=(ControllerDelegate&&) = delete;

    virtual void onAction(ViewController& controller, ViewId action) noexcept = 0;
    virtual void onBack(ViewController& controller) noexcept = 0;
    // Typed identity for generated forms. Existing numeric-action delegates keep
    // their behavior through the default implementation. Called once per action,
    // after the control commits its value; input dispatch is non-reentrant.
    virtual void onControlAction(ViewController& controller, View& control) noexcept
    {
        onAction(controller, control.actionId());
    }
    virtual void onControlCheckedChanged([[maybe_unused]] ViewController& controller,
                                         [[maybe_unused]] View& control,
                                         [[maybe_unused]] CheckedChange event) noexcept
    {
    }
    virtual void onControlSelectionChanged([[maybe_unused]] ViewController& controller,
                                           [[maybe_unused]] View& control,
                                           [[maybe_unused]] SelectionChange event) noexcept
    {
    }
    virtual void
    onControlRadioSelectionChanged([[maybe_unused]] ViewController& controller,
                                   [[maybe_unused]] View& control,
                                   [[maybe_unused]] RadioSelectionChange event) noexcept
    {
    }
    virtual void onControlEditCommitted([[maybe_unused]] ViewController& controller,
                                        [[maybe_unused]] View& control,
                                        [[maybe_unused]] SelectionEdit event) noexcept
    {
    }
    virtual void onControlEditCancelled([[maybe_unused]] ViewController& controller,
                                        [[maybe_unused]] View& control,
                                        [[maybe_unused]] SelectionEdit event) noexcept
    {
    }
    virtual void onControlTextChanged([[maybe_unused]] ViewController& controller,
                                      [[maybe_unused]] View& control,
                                      [[maybe_unused]] TextChange event) noexcept
    {
    }
    virtual void onControlTextEditCommitted([[maybe_unused]] ViewController& controller,
                                            [[maybe_unused]] View& control,
                                            [[maybe_unused]] TextEdit event) noexcept
    {
    }
    virtual void onControlTextEditCancelled([[maybe_unused]] ViewController& controller,
                                            [[maybe_unused]] View& control,
                                            [[maybe_unused]] TextEdit event) noexcept
    {
    }
    virtual bool onGesture([[maybe_unused]] ViewController& controller,
                           [[maybe_unused]] GestureEvent event) noexcept
    {
        return false;
    }
};

class ViewController : private ControlEventSink
{
public:
    explicit ViewController(View& root) noexcept;
    ViewController(View& root, ControllerDelegate& delegate) noexcept;

    View& rootView() noexcept
    {
        return mRoot;
    }
    const View& rootView() const noexcept
    {
        return mRoot;
    }
    // Hidden or disabled ancestors make retained focus ineligible. Querying
    // or dispatching input clears that stale state before returning a view.
    View* focusedView() noexcept;
    void setDelegate(ControllerDelegate& delegate) noexcept
    {
        mDelegate = &delegate;
    }
    void clearDelegate() noexcept
    {
        mDelegate = nullptr;
    }

    void activate() noexcept;
    bool handleKey(Key key) noexcept;
    bool handleTextInput(char32_t codepoint) noexcept;
    bool handlePointer(PointerEvent event) noexcept;
    bool handlePointer(Point point) noexcept;
    bool handleGesture(GestureEvent event) noexcept;
    bool cancelPointerCapture() noexcept;

private:
    class EventScope
    {
    public:
        EventScope(View& view, ControlEventSink& sink) noexcept
            : mView(view), mPrevious(view.exchangeEventSink(&sink))
        {
        }
        ~EventScope()
        {
            mView.exchangeEventSink(mPrevious);
        }
        EventScope(const EventScope&) = delete;
        EventScope& operator=(const EventScope&) = delete;
        EventScope(EventScope&&) = delete;
        EventScope& operator=(EventScope&&) = delete;

    private:
        View& mView;
        ControlEventSink* mPrevious;
    };
    template <typename Operation> auto dispatchTo(View& view, Operation operation) noexcept
    {
        const EventScope scope{view, *this};
        return operation();
    }
    void checkedChanged(View& source, CheckedChange event) noexcept override;
    void selectionChanged(View& source, SelectionChange event) noexcept override;
    void radioSelectionChanged(View& source, RadioSelectionChange event) noexcept override;
    void editCommitted(View& source, SelectionEdit event) noexcept override;
    void editCancelled(View& source, SelectionEdit event) noexcept override;
    void textChanged(View& source, TextChange event) noexcept override;
    void textEditCommitted(View& source, TextEdit event) noexcept override;
    void textEditCancelled(View& source, TextEdit event) noexcept override;
    bool inputEligible(const View& view) const noexcept;
    bool cancelCapture() noexcept;
    class DispatchGuard
    {
    public:
        explicit DispatchGuard(ViewController& controller) noexcept : mController(controller)
        {
            mController.mDispatching = true;
        }
        ~DispatchGuard()
        {
            mController.clearInvalidFocus();
            mController.mDispatching = false;
        }
        DispatchGuard(const DispatchGuard&) = delete;
        DispatchGuard& operator=(const DispatchGuard&) = delete;
        DispatchGuard(DispatchGuard&&) = delete;
        DispatchGuard& operator=(DispatchGuard&&) = delete;

    private:
        ViewController& mController;
    };
    static std::size_t collectFocusable(View& view, std::span<View*> output) noexcept;
    bool focusEligible(const View& view) const noexcept;
    void clearInvalidFocus() noexcept;
    void setFocus(View* view) noexcept;
    bool moveFocus(int direction) noexcept;

    View& mRoot;
    View* mFocused{nullptr};
    View* mCapturedPointer{nullptr};
    ControllerDelegate* mDelegate{nullptr};
    Point mLastPointerPosition{};
    std::uint8_t mCapturedContact{0U};
    bool mDispatching{false};
};

} // namespace lithe
