// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/controller.hpp"

namespace lithe
{

namespace
{

View* nextFocusBranch(View& current, const View& root) noexcept
{
    for (View* branch = &current; branch != &root;)
    {
        View* const parent = branch->parent();
        if (parent == nullptr)
        {
            return nullptr;
        }
        for (std::size_t index = 0U; index < parent->subviewCount(); ++index)
        {
            if (parent->subview(index) == branch)
            {
                if (View* sibling = parent->subview(index + 1U))
                {
                    return sibling;
                }
                break;
            }
        }
        branch = parent;
    }
    return nullptr;
}

} // namespace

ViewController::ViewController(View& root) noexcept : mRoot(root)
{
}

ViewController::ViewController(View& root, ControllerDelegate& delegate) noexcept
    : mRoot(root), mDelegate(&delegate)
{
}

void ViewController::checkedChanged(View& source, CheckedChange event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlCheckedChanged(*this, source, event);
    }
}

void ViewController::selectionChanged(View& source, SelectionChange event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlSelectionChanged(*this, source, event);
    }
}

void ViewController::radioSelectionChanged(View& source, RadioSelectionChange event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlRadioSelectionChanged(*this, source, event);
    }
}

void ViewController::editCommitted(View& source, SelectionEdit event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlEditCommitted(*this, source, event);
    }
}

void ViewController::editCancelled(View& source, SelectionEdit event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlEditCancelled(*this, source, event);
    }
}

void ViewController::textChanged(View& source, TextChange event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlTextChanged(*this, source, event);
    }
}

void ViewController::textEditCommitted(View& source, TextEdit event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlTextEditCommitted(*this, source, event);
    }
}

void ViewController::textEditCancelled(View& source, TextEdit event) noexcept
{
    if (mDelegate != nullptr)
    {
        mDelegate->onControlTextEditCancelled(*this, source, event);
    }
}

std::size_t ViewController::collectFocusable(View& view, std::span<View*> output) noexcept
{
    // Follow the caller-owned tree links instead of consuming one stack frame
    // per nesting level. Stop once the bounded focus list is full.
    std::size_t count = 0U;
    View* current = &view;
    while (current != nullptr && count < output.size())
    {
        if (current->visible() && current->enabled())
        {
            if (current->focusable())
            {
                output[count++] = current;
            }
            if (View* child = current->subview(0U))
            {
                current = child;
                continue;
            }
        }
        current = nextFocusBranch(*current, view);
    }
    return count;
}

bool ViewController::focusEligible(const View& view) const noexcept
{
    if (!view.focusable())
    {
        return false;
    }
    return inputEligible(view);
}

bool ViewController::inputEligible(const View& view) const noexcept
{
    for (const View* current = &view; current != nullptr; current = current->parent())
    {
        if (!current->visible() || !current->enabled())
        {
            return false;
        }
        if (current == &mRoot)
        {
            return true;
        }
    }
    return false;
}

void ViewController::clearInvalidFocus() noexcept
{
    if (mFocused != nullptr && !focusEligible(*mFocused))
    {
        setFocus(nullptr);
    }
}

View* ViewController::focusedView() noexcept
{
    if (mDispatching)
    {
        return mFocused != nullptr && focusEligible(*mFocused) ? mFocused : nullptr;
    }
    const DispatchGuard dispatch{*this};
    clearInvalidFocus();
    return mFocused;
}

void ViewController::setFocus(View* view) noexcept
{
    if (mFocused == view)
    {
        return;
    }
    if (mFocused != nullptr)
    {
        View* const previous = mFocused;
        mFocused = nullptr;
        dispatchTo(*previous,
                   [&]() noexcept
                   {
                       previous->setFocused(false);
                   });
    }
    mFocused = view;
    if (mFocused != nullptr)
    {
        dispatchTo(*mFocused,
                   [&]() noexcept
                   {
                       mFocused->setFocused(true);
                   });
    }
}

void ViewController::activate() noexcept
{
    if (mDispatching)
    {
        return;
    }
    const DispatchGuard dispatch{*this};
    cancelCapture();
    clearInvalidFocus();
    if (mFocused != nullptr)
    {
        return;
    }
    std::array<View*, config::kMaxFocusableViews> focusable{};
    const std::size_t count = collectFocusable(mRoot, focusable);
    setFocus(count == 0U ? nullptr : focusable[0]);
}

bool ViewController::moveFocus(int direction) noexcept
{
    std::array<View*, config::kMaxFocusableViews> focusable{};
    const std::size_t count = collectFocusable(mRoot, focusable);
    if (count == 0U)
    {
        setFocus(nullptr);
        return false;
    }
    std::size_t current = 0U;
    while (current < count && focusable[current] != mFocused)
    {
        ++current;
    }
    if (current == count)
    {
        current = direction >= 0 ? 0U : count - 1U;
    }
    else if (direction >= 0)
    {
        current = (current + 1U) % count;
    }
    else
    {
        current = current == 0U ? count - 1U : current - 1U;
    }
    setFocus(focusable[current]);
    return true;
}

bool ViewController::handleKey(Key key) noexcept
{
    if (mDispatching)
    {
        return false;
    }
    const DispatchGuard dispatch{*this};
    clearInvalidFocus();
    View* const target = mFocused;
    if (target != nullptr && dispatchTo(*target,
                                        [&]() noexcept
                                        {
                                            return target->handleKey(key);
                                        }))
    {
        if (target->takeActionRequest() && mDelegate != nullptr)
        {
            mDelegate->onControlAction(*this, *target);
        }
        return true;
    }
    switch (key)
    {
    case Key::up:
    case Key::left:
        return moveFocus(-1);
    case Key::down:
    case Key::right:
        return moveFocus(1);
    case Key::select:
        if (target != nullptr)
        {
            dispatchTo(*target,
                       [&]() noexcept
                       {
                           target->activate();
                       });
            if (mDelegate != nullptr)
            {
                mDelegate->onControlAction(*this, *target);
            }
            return true;
        }
        return false;
    case Key::back:
        if (mDelegate != nullptr)
        {
            mDelegate->onBack(*this);
            return true;
        }
        return false;
    }
    return false;
}

bool ViewController::handleTextInput(char32_t codepoint) noexcept
{
    if (mDispatching)
    {
        return false;
    }
    const DispatchGuard dispatch{*this};
    clearInvalidFocus();
    return mFocused != nullptr && dispatchTo(*mFocused,
                                             [&]() noexcept
                                             {
                                                 return mFocused->handleTextInput(codepoint);
                                             });
}

bool ViewController::handlePointer(Point point) noexcept
{
    const bool pressed = handlePointer({point, PointerPhase::pressed, 0U});
    return pressed && handlePointer({point, PointerPhase::released, 0U});
}

bool ViewController::handlePointer(PointerEvent event) noexcept
{
    if (mDispatching)
    {
        return false;
    }
    const DispatchGuard dispatch{*this};
    clearInvalidFocus();
    if (event.phase == PointerPhase::pressed)
    {
        if (mCapturedPointer != nullptr)
        {
            return false;
        }
        // An outside press dismisses the focused popup without clicking through
        // to an unrelated control. A second press can then activate that control.
        if (mFocused != nullptr && !mFocused->popupFrame().empty() &&
            !mFocused->popupFrame().contains(event.position) &&
            !mFocused->frameInRoot().contains(event.position))
        {
            return dispatchTo(*mFocused,
                              [&]() noexcept
                              {
                                  return mFocused->dismissPopup();
                              });
        }
        View* const target = mRoot.hitTestPointer(event.position);
        if (target == nullptr)
        {
            return false;
        }
        if (target->focusable())
        {
            setFocus(target);
        }
        mCapturedPointer = target;
        mCapturedContact = event.contactId;
        mLastPointerPosition = event.position;
        const Rect absolute = target->frameInRoot();
        event.position = {detail::saturatingSubtract(event.position.x, absolute.x),
                          detail::saturatingSubtract(event.position.y, absolute.y)};
        dispatchTo(*target,
                   [&]() noexcept
                   {
                       target->handlePointerEvent(event);
                   });
        return true;
    }

    if (mCapturedPointer == nullptr || event.contactId != mCapturedContact)
    {
        return false;
    }

    if (!inputEligible(*mCapturedPointer))
    {
        cancelCapture();
        return false;
    }
    View* const target = mCapturedPointer;
    const Point rootPosition = event.position;
    const bool releasedOnTarget = mRoot.hitTestPointer(rootPosition) == target;
    mLastPointerPosition = rootPosition;
    const Rect absolute = target->frameInRoot();
    event.position = {detail::saturatingSubtract(rootPosition.x, absolute.x),
                      detail::saturatingSubtract(rootPosition.y, absolute.y)};
    dispatchTo(*target,
               [&]() noexcept
               {
                   target->handlePointerEvent(event);
               });
    if (event.phase == PointerPhase::moved)
    {
        return true;
    }

    mCapturedPointer = nullptr;
    if (event.phase == PointerPhase::cancelled)
    {
        return true;
    }
    if (event.phase == PointerPhase::released && target->focusable() && inputEligible(*target))
    {
        // A widget whose value a release already changed (a dragged wheel) reports it even
        // when the contact lifts outside; otherwise a release outside cancels activation.
        const bool requested = target->takeActionRequest();
        if (releasedOnTarget)
        {
            dispatchTo(*target,
                       [&]() noexcept
                       {
                           target->activate();
                       });
            if (mDelegate != nullptr)
            {
                mDelegate->onControlAction(*this, *target);
            }
            return true;
        }
        if (requested && mDelegate != nullptr)
        {
            mDelegate->onControlAction(*this, *target);
        }
        return requested;
    }
    return true;
}

bool ViewController::handleGesture(GestureEvent event) noexcept
{
    if (mDispatching)
    {
        return false;
    }
    const DispatchGuard dispatch{*this};
    return mDelegate != nullptr && mDelegate->onGesture(*this, event);
}

bool ViewController::cancelPointerCapture() noexcept
{
    if (mDispatching)
    {
        return false;
    }
    const DispatchGuard dispatch{*this};
    return cancelCapture();
}

bool ViewController::cancelCapture() noexcept
{
    if (mCapturedPointer == nullptr)
    {
        return false;
    }
    View* const target = mCapturedPointer;
    const Rect absolute = target->frameInRoot();
    const Point localPosition{detail::saturatingSubtract(mLastPointerPosition.x, absolute.x),
                              detail::saturatingSubtract(mLastPointerPosition.y, absolute.y)};
    mCapturedPointer = nullptr;
    dispatchTo(*target,
               [&]() noexcept
               {
                   target->handlePointerEvent(
                       {localPosition, PointerPhase::cancelled, mCapturedContact, 0U});
               });
    return true;
}

} // namespace lithe
