// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/config.hpp"
#include "lithe/painter.hpp"

#include <cstddef>
#include <cstdint>

namespace lithe
{

class ControlEventSink;
class ControlView;
class ViewController;

class View
{
public:
    explicit View(Rect frame = {}, Style style = {}) noexcept;
    virtual ~View() = default;

    View(const View&) = delete;
    View& operator=(const View&) = delete;
    View(View&&) = delete;
    View& operator=(View&&) = delete;

    bool addSubview(View& view) noexcept;
    std::size_t subviewCount() const noexcept
    {
        return mChildCount;
    }
    View* subview(std::size_t index) noexcept;
    const View* subview(std::size_t index) const noexcept;
    View* find(ViewId id) noexcept;
    const View* find(ViewId id) const noexcept;

    Rect frame() const noexcept
    {
        return mFrame;
    }
    void setFrame(Rect frame) noexcept;
    ViewId id() const noexcept
    {
        return mId;
    }
    void setId(ViewId id) noexcept
    {
        mId = id;
    }
    Style style() const noexcept
    {
        return mStyle;
    }
    void setStyle(Style style) noexcept;

    bool visible() const noexcept
    {
        return mVisible;
    }
    void setVisible(bool visible) noexcept;
    bool enabled() const noexcept
    {
        return mEnabled;
    }
    void setEnabled(bool enabled) noexcept;
    bool focusable() const noexcept
    {
        return mFocusable && mVisible && mEnabled;
    }
    void setFocusable(bool focusable) noexcept;
    bool focused() const noexcept
    {
        return mFocused;
    }
    void setFocused(bool focused) noexcept;
    bool pressed() const noexcept
    {
        return mPressed;
    }
    // A retained value state shared by toggle buttons, checkboxes, and radio
    // buttons. Unlike focus and press it survives disable and visibility changes.
    bool selected() const noexcept
    {
        return mSelected;
    }
    WidgetState widgetState() const noexcept
    {
        return resolveWidgetState(mEnabled, mFocused, mPressed, mSelected);
    }
    // Reports, once, that a consumed key changed this widget's value (for
    // example a tab bar stepping with left/right). The controller then sends
    // the widget's action exactly as it does for an activation.
    bool takeActionRequest() noexcept
    {
        const bool requested = mActionRequested;
        mActionRequested = false;
        return requested;
    }

    View* parent() noexcept
    {
        return mParent;
    }
    const View* parent() const noexcept
    {
        return mParent;
    }
    Rect frameInRoot() const noexcept;
    Rect frameRelativeTo(const View& ancestor) const noexcept;

    virtual bool handleKey(Key key) noexcept;
    virtual bool handleTextInput(char32_t codepoint) noexcept;
    virtual void handlePointerEvent(PointerEvent event) noexcept;
    virtual void pointerPressed(Point localPoint) noexcept;
    virtual void activate() noexcept
    {
    }
    virtual ViewId actionId() const noexcept
    {
        return mId;
    }

    // Transient overlays use root coordinates, draw after the ordinary tree, and
    // receive pointer hits before its children. Only the focused control owns input.
    virtual Rect popupFrame() const noexcept
    {
        return {};
    }
    virtual bool dismissPopup() noexcept
    {
        return false;
    }

    bool advanceTime(std::uint32_t elapsedMs) noexcept;
    bool animationsRunning() const noexcept;

    void invalidate() noexcept;
    void render(Painter& painter, Point parentOrigin = {}, bool force = false) noexcept;
    View* hitTestFocusable(Point point, Point parentOrigin = {}) noexcept;
    View* hitTestPointer(Point point, Point parentOrigin = {}) noexcept;

protected:
    void invalidateRootRegion(Rect region) noexcept;
    // Call before and after changing a derived container's child coordinate transform.
    void invalidatePopups() noexcept;
    virtual void drawPopup([[maybe_unused]] Painter& painter,
                           [[maybe_unused]] Rect absolutePopup) noexcept
    {
    }
    virtual void drawSelf(Painter& painter, Rect absoluteFrame) noexcept;
    Style resolvedStyle() const noexcept;
    // Derived widgets own the meaning of selection and always redraw a change.
    void setSelectedState(bool selected) noexcept;
    // A widget that requests actions assigns this on every key it handles, so
    // a request never outlives the key that caused it.
    void setActionRequested(bool requested) noexcept
    {
        mActionRequested = requested;
    }
    // Called after this view gains or loses focus, so a widget can end transient interaction.
    virtual void focusChanged() noexcept
    {
    }
    virtual Point childOffset() const noexcept
    {
        return {};
    }
    virtual void descendantFocused(View& descendant) noexcept;
    virtual bool onAdvanceTime([[maybe_unused]] std::uint32_t elapsedMs) noexcept
    {
        return false;
    }
    virtual bool animationRunning() const noexcept
    {
        return false;
    }
    virtual bool capturesPointerAt([[maybe_unused]] Point localPoint) const noexcept
    {
        return false;
    }
    Rect absoluteFrame(Point parentOrigin) const noexcept
    {
        return mFrame.translated(parentOrigin);
    }

private:
    friend class ViewController;
    friend class ControlView;
    virtual ControlEventSink* exchangeEventSink([[maybe_unused]] ControlEventSink* sink) noexcept
    {
        return nullptr;
    }
    void renderPopups(Painter& painter, Point origin) noexcept;
    View* hitTestPopup(Point point) noexcept;
    void invalidateRegion(Rect rootRegion) noexcept;
    void setDirtyRegion(Rect rootRegion) noexcept;

    Rect mFrame{};
    Style mStyle{};
    View* mParent{nullptr};
    View* mFirstChild{nullptr};
    View* mNextSibling{nullptr};
    std::size_t mChildCount{0};
    Rect mDirtyRegion{};
    ViewId mId{0};
    bool mVisible : 1 = true;
    bool mEnabled : 1 = true;
    bool mFocusable : 1 = false;
    bool mFocused : 1 = false;
    bool mPressed : 1 = false;
    bool mSelected : 1 = false;
    bool mDirty : 1 = true;
    bool mActionRequested : 1 = false;
    // Uses existing tail padding; the Cortex-M View ceiling stays unchanged.
    bool mControlInteracting : 1 = false;
};

} // namespace lithe
