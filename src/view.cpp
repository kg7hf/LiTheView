// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/view.hpp"

namespace lithe
{

namespace
{

bool stateColorsDiffer(Style style, WidgetState before, WidgetState after) noexcept
{
    const Style previous = resolveWidgetStyle(style, before);
    const Style current = resolveWidgetStyle(style, after);
    return previous.background.legacyValue() != current.background.legacyValue() ||
           previous.foreground.legacyValue() != current.foreground.legacyValue();
}

} // namespace

View::View(Rect frame, Style style) noexcept : mFrame(frame), mStyle(style)
{
}

Style View::resolvedStyle() const noexcept
{
    return resolveWidgetStyle(mStyle, widgetState());
}

bool View::addSubview(View& view) noexcept
{
    if (mChildCount >= config::kMaxChildren || &view == this || view.mParent != nullptr)
    {
        return false;
    }
    for (const View* ancestor = this; ancestor != nullptr; ancestor = ancestor->mParent)
    {
        if (ancestor == &view)
        {
            return false;
        }
    }
    View** insertion = &mFirstChild;
    while (*insertion != nullptr)
    {
        insertion = &((*insertion)->mNextSibling);
    }
    *insertion = &view;
    ++mChildCount;
    view.mParent = this;
    invalidate();
    return true;
}

View* View::subview(std::size_t index) noexcept
{
    View* child = mFirstChild;
    while (child != nullptr && index > 0U)
    {
        child = child->mNextSibling;
        --index;
    }
    return child;
}

const View* View::subview(std::size_t index) const noexcept
{
    const View* child = mFirstChild;
    while (child != nullptr && index > 0U)
    {
        child = child->mNextSibling;
        --index;
    }
    return child;
}

View* View::find(ViewId id) noexcept
{
    if (mId == id)
    {
        return this;
    }
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        if (View* match = child->find(id))
        {
            return match;
        }
    }
    return nullptr;
}

const View* View::find(ViewId id) const noexcept
{
    if (mId == id)
    {
        return this;
    }
    for (const View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        if (const View* match = child->find(id))
        {
            return match;
        }
    }
    return nullptr;
}

void View::setFrame(Rect frame) noexcept
{
    invalidatePopups();
    if (mParent != nullptr)
    {
        mParent->invalidate();
    }
    mFrame = frame;
    invalidate();
    invalidatePopups();
}

void View::setStyle(Style style) noexcept
{
    mStyle = style;
    invalidate();
}

void View::setVisible(bool visible) noexcept
{
    invalidatePopups();
    if (mVisible == visible)
    {
        return;
    }
    mVisible = visible;
    if (!mVisible)
    {
        mPressed = false;
    }
    if (mParent != nullptr)
    {
        mParent->invalidate();
    }
    else
    {
        invalidate();
    }
}

void View::setEnabled(bool enabled) noexcept
{
    invalidatePopups();
    if (mEnabled != enabled)
    {
        mEnabled = enabled;
        const bool lostFocus = !mEnabled && mFocused;
        if (!mEnabled)
        {
            mFocused = false;
            mPressed = false;
        }
        invalidate();
        if (lostFocus)
        {
            focusChanged();
        }
    }
}

void View::setFocusable(bool focusable) noexcept
{
    mFocusable = focusable;
    const bool lostFocus = !mFocusable && mFocused;
    if (!mFocusable)
    {
        mFocused = false;
    }
    invalidate();
    if (lostFocus)
    {
        focusChanged();
    }
}

void View::setSelectedState(bool selected) noexcept
{
    if (mSelected != selected)
    {
        mSelected = selected;
        invalidate();
    }
}

void View::setFocused(bool focused) noexcept
{
    if (mFocused != focused)
    {
        const bool wasFocused = mFocused;
        mFocused = focused && focusable();
        invalidate();
        if (mFocused != wasFocused)
        {
            focusChanged();
        }
        if (mFocused && mParent != nullptr)
        {
            mParent->descendantFocused(*this);
        }
    }
}

Rect View::frameInRoot() const noexcept
{
    Rect result = mFrame;
    const View* current = mParent;
    while (current != nullptr)
    {
        result = result.translated(current->childOffset());
        result = result.translated({current->mFrame.x, current->mFrame.y});
        current = current->mParent;
    }
    return result;
}

Rect View::frameRelativeTo(const View& ancestor) const noexcept
{
    if (this == &ancestor)
    {
        return {0, 0, mFrame.width, mFrame.height};
    }
    Rect result = mFrame;
    const View* current = mParent;
    while (current != nullptr)
    {
        result = result.translated(current->childOffset());
        if (current == &ancestor)
        {
            return result;
        }
        result = result.translated({current->mFrame.x, current->mFrame.y});
        current = current->mParent;
    }
    return {};
}

bool View::handleKey([[maybe_unused]] Key key) noexcept
{
    return false;
}

bool View::handleTextInput([[maybe_unused]] char32_t codepoint) noexcept
{
    return false;
}

void View::handlePointerEvent(PointerEvent event) noexcept
{
    if (event.phase == PointerPhase::pressed)
    {
        if (!mPressed)
        {
            const WidgetState previous = widgetState();
            mPressed = true;
            if (stateColorsDiffer(mStyle, previous, widgetState()))
            {
                invalidate();
            }
        }
        pointerPressed(event.position);
    }
    else if (event.phase == PointerPhase::moved)
    {
        const bool inside = Rect{0, 0, mFrame.width, mFrame.height}.contains(event.position);
        if (mPressed != inside)
        {
            const WidgetState previous = widgetState();
            mPressed = inside;
            if (stateColorsDiffer(mStyle, previous, widgetState()))
            {
                invalidate();
            }
        }
    }
    else if (mPressed)
    {
        const WidgetState previous = widgetState();
        mPressed = false;
        if (stateColorsDiffer(mStyle, previous, widgetState()))
        {
            invalidate();
        }
    }
}

void View::pointerPressed([[maybe_unused]] Point localPoint) noexcept
{
}

bool View::advanceTime(std::uint32_t elapsedMs) noexcept
{
    if (!mVisible)
    {
        return false;
    }
    bool changed = onAdvanceTime(elapsedMs);
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        changed = child->advanceTime(elapsedMs) || changed;
    }
    return changed;
}

bool View::animationsRunning() const noexcept
{
    if (!mVisible)
    {
        return false;
    }
    if (animationRunning())
    {
        return true;
    }
    for (const View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        if (child->animationsRunning())
        {
            return true;
        }
    }
    return false;
}

void View::descendantFocused(View& descendant) noexcept
{
    if (mParent != nullptr)
    {
        mParent->descendantFocused(descendant);
    }
}

void View::invalidate() noexcept
{
    mDirty = true;
    mDirtyRegion = {};
    if (mParent != nullptr)
    {
        mParent->invalidateRegion(frameInRoot());
    }
}

void View::setDirtyRegion(Rect rootRegion) noexcept
{
    mDirtyRegion = rootRegion.empty() ? Rect{} : rootRegion;
}

void View::invalidateRegion(Rect rootRegion) noexcept
{
    if (!mDirty)
    {
        mDirty = true;
        setDirtyRegion(rootRegion);
    }
    else if (!mDirtyRegion.empty())
    {
        setDirtyRegion(unite(mDirtyRegion, rootRegion));
    }
    if (mParent != nullptr)
    {
        mParent->invalidateRegion(rootRegion);
    }
}

void View::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    const Style current = resolvedStyle();
    if (current.background.alpha != 0U)
    {
        painter.fillRect(absoluteFrame, current.background);
    }
    if (current.borderWidth != 0U && current.border.alpha != 0U)
    {
        painter.strokeRect(absoluteFrame, current.border, current.borderWidth);
    }
}

void View::render(Painter& painter, Point parentOrigin, bool force) noexcept
{
    if (!mVisible)
    {
        return;
    }
    const Rect absolute = absoluteFrame(parentOrigin);
    const Rect previousClip = painter.clip();
    Rect renderClip = intersect(previousClip, absolute);
    const bool redraw = force || mDirty;
    if (redraw && !force && !mDirtyRegion.empty())
    {
        renderClip = intersect(renderClip, mDirtyRegion);
    }
    painter.setClip(renderClip);
    if (redraw)
    {
        drawSelf(painter, absolute);
    }
    const Point offset = childOffset();
    const Point childOrigin{detail::saturatingAdd(absolute.x, offset.x),
                            detail::saturatingAdd(absolute.y, offset.y)};
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        child->render(painter, childOrigin, redraw);
    }
    mDirty = false;
    mDirtyRegion = {};
    painter.setClip(previousClip);
    if (mParent == nullptr)
    {
        painter.setClip(intersect(previousClip, absolute));
        renderPopups(painter, parentOrigin);
        painter.setClip(previousClip);
    }
}

void View::invalidateRootRegion(Rect region) noexcept
{
    if (region.empty())
    {
        return;
    }
    View* root = this;
    while (root->mParent != nullptr)
    {
        root = root->mParent;
    }
    root->invalidateRegion(region);
}

void View::invalidatePopups() noexcept
{
    const Rect popup = popupFrame();
    if (!popup.empty())
    {
        // Container geometry/state changes can move both its children and their
        // overlays. Repaint their common root, including the old popup background.
        // Ordinary popup selection changes use bounded invalidateRootRegion instead.
        View* root = this;
        while (root->mParent != nullptr)
        {
            root = root->mParent;
        }
        root->invalidate();
    }
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        child->invalidatePopups();
    }
}

void View::renderPopups(Painter& painter, Point origin) noexcept
{
    if (!mVisible || !mEnabled)
    {
        return;
    }
    const Rect popup = popupFrame().translated(origin);
    if (!popup.empty())
    {
        const Rect previous = painter.clip();
        painter.setClip(intersect(previous, popup));
        drawPopup(painter, popup);
        painter.setClip(previous);
    }
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        child->renderPopups(painter, origin);
    }
}

View* View::hitTestPopup(Point point) noexcept
{
    if (!mVisible || !mEnabled)
    {
        return nullptr;
    }
    View* topmost = popupFrame().contains(point) ? this : nullptr;
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        if (View* match = child->hitTestPopup(point))
        {
            topmost = match;
        }
    }
    return topmost;
}

View* View::hitTestFocusable(Point point, Point parentOrigin) noexcept
{
    if (!mVisible)
    {
        return nullptr;
    }
    const Rect absolute = absoluteFrame(parentOrigin);
    if (!absolute.contains(point))
    {
        return nullptr;
    }
    const Point offset = childOffset();
    const Point childOrigin{detail::saturatingAdd(absolute.x, offset.x),
                            detail::saturatingAdd(absolute.y, offset.y)};
    View* topmost = nullptr;
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        if (View* match = child->hitTestFocusable(point, childOrigin))
        {
            topmost = match;
        }
    }
    if (topmost != nullptr)
    {
        return topmost;
    }
    return focusable() ? this : nullptr;
}

View* View::hitTestPointer(Point point, Point parentOrigin) noexcept
{
    if (mParent == nullptr && absoluteFrame(parentOrigin).contains(point))
    {
        const Point rootPoint{detail::saturatingSubtract(point.x, parentOrigin.x),
                              detail::saturatingSubtract(point.y, parentOrigin.y)};
        if (View* popup = hitTestPopup(rootPoint))
        {
            return popup;
        }
    }
    if (!mVisible || !mEnabled)
    {
        return nullptr;
    }
    const Rect absolute = absoluteFrame(parentOrigin);
    if (!absolute.contains(point))
    {
        return nullptr;
    }
    const Point local{detail::saturatingSubtract(point.x, absolute.x),
                      detail::saturatingSubtract(point.y, absolute.y)};
    if (capturesPointerAt(local))
    {
        return this;
    }
    const Point offset = childOffset();
    const Point childOrigin{detail::saturatingAdd(absolute.x, offset.x),
                            detail::saturatingAdd(absolute.y, offset.y)};
    View* topmost = nullptr;
    for (View* child = mFirstChild; child != nullptr; child = child->mNextSibling)
    {
        if (View* match = child->hitTestPointer(point, childOrigin))
        {
            topmost = match;
        }
    }
    if (topmost != nullptr)
    {
        return topmost;
    }
    return focusable() ? this : nullptr;
}

} // namespace lithe
