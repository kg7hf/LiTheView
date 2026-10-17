// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/widget_overrides.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <span>
#include <string_view>
#include <type_traits>
#include <utility>

namespace lithe
{

using MutableTextBuffer = std::span<char>;

struct ViewSpec
{
    ViewKind kind{ViewKind::panel};
    std::optional<std::uint16_t> parent;
    ViewId id{0};
    Rect frame{};
    Style style{};
    std::string_view text;
    std::string_view detail;
    std::string_view resource;
    std::string_view font;
    ViewId leftAction{0};
    ViewId rightAction{0};
    std::uint16_t animationFrameOffset{0U};
    std::uint16_t animationFrameCount{0U};
    std::uint16_t animationIntervalMs{detail::kDefaultAnimationIntervalMilliseconds};
    std::uint16_t animationRepeatIndex{0U};
    std::uint16_t maxLength{0U};
    std::int32_t contentHeight{0};
    std::int32_t scrollOffset{0};
    std::int32_t scrollStep{detail::kDefaultScrollStepPixels};
    std::uint16_t selectedIndex{0U};
    // Tab views only: the strip height above the pages.
    std::uint16_t tabHeight{detail::kDefaultTabHeightPixels};
    std::int32_t value{0};
    bool wraps : 1 = true;
    bool showsIndicator : 1 = true;
    bool xmlEncodedText : 1 = false;
    bool animationAutoplay : 1 = true;
    bool checked : 1 = false;
    bool visible : 1 = true;
    bool enabled : 1 = true;
    std::int8_t focusable : 2 = -1;
    // Button kinds only. A momentary button ignores `checked`; strict XML and
    // the designer reject that combination.
    ButtonBehavior buttonBehavior{ButtonBehavior::momentary};
    // Wheel pickers only: the height of each item row.
    std::uint8_t rowHeight{detail::kDefaultWheelRowHeightPixels};
};

enum class LayoutError : std::uint8_t
{
    none,
    empty,
    capacity,
    invalidParent,
    multipleRoots,
    tooManyChildren,
    missingResource,
    invalidKind,
    alreadyBuilt
};

struct LayoutResult
{
    LayoutError error{LayoutError::none};
    std::size_t node{0};
    constexpr bool ok() const noexcept
    {
        return error == LayoutError::none;
    }
    constexpr explicit operator bool() const noexcept
    {
        return ok();
    }
};

class ResourceProvider
{
public:
    ResourceProvider() = default;
    virtual ~ResourceProvider() = default;

    ResourceProvider(const ResourceProvider&) = delete;
    ResourceProvider& operator=(const ResourceProvider&) = delete;
    ResourceProvider(ResourceProvider&&) = delete;
    ResourceProvider& operator=(ResourceProvider&&) = delete;

    virtual const Bitmap* bitmap(std::string_view name) const noexcept = 0;
    virtual const Font* font([[maybe_unused]] std::string_view name) const noexcept
    {
        return nullptr;
    }
    // Optional separate rollback storage, with the same capacity as textBuffer.
    // Empty preserves immediate text editing without transactional commit/cancel.
    virtual MutableTextBuffer textEditBuffer([[maybe_unused]] std::string_view name) const noexcept
    {
        return {};
    }
    virtual MutableTextBuffer textBuffer([[maybe_unused]] std::string_view name) const noexcept
    {
        return {};
    }
};

// The widget kinds a StaticLayout may build. Only listed kinds are constructed, so a product whose
// layouts name the kinds they use links only that widget code; build() reports invalidKind for any
// other kind. Designer-generated forms provide their exact set as Form::Widgets.
template <ViewKind... Kinds> struct WidgetSet
{
    static constexpr std::size_t size{sizeof...(Kinds)};
    static constexpr bool contains(ViewKind kind) noexcept
    {
        // An array lookup rather than a fold over Kinds, which Cppcheck 2.13 cannot parse.
        constexpr std::array<ViewKind, size> kinds{Kinds...};
        return std::ranges::find(kinds, kind) != kinds.end();
    }
};

// Every built-in kind. It is the default because XML parsed at run time can name any kind.
using AllWidgets = WidgetSet<ViewKind::panel,
                             ViewKind::label,
                             ViewKind::button,
                             ViewKind::progress,
                             ViewKind::image,
                             ViewKind::animatedImage,
                             ViewKind::icon,
                             ViewKind::line,
                             ViewKind::scroll,
                             ViewKind::table,
                             ViewKind::picker,
                             ViewKind::checkbox,
                             ViewKind::radio,
                             ViewKind::textField,
                             ViewKind::softkey,
                             ViewKind::tabBar,
                             ViewKind::tabView,
                             ViewKind::wheelPicker,
                             ViewKind::dropDown>;

template <std::size_t Capacity,
          typename Widgets = AllWidgets,
          typename Overrides = WidgetOverrides<>>
class StaticLayout
{
public:
    static_assert(Overrides::template fits<Capacity>());

    template <std::size_t Index, typename Default>
    using ComponentType = typename Overrides::template TypeAt<Index, Default>;

    StaticLayout() noexcept = default;
    ~StaticLayout()
    {
        destroy();
    }
    StaticLayout(const StaticLayout&) = delete;
    StaticLayout& operator=(const StaticLayout&) = delete;
    StaticLayout(StaticLayout&&) = delete;
    StaticLayout& operator=(StaticLayout&&) = delete;

    LayoutResult build(std::span<const ViewSpec> specs,
                       const Font& defaultFont = font5x7(),
                       const ResourceProvider* resources = nullptr,
                       std::span<const std::string_view> animationFrames = {}) noexcept
    {
        destroy();
        if (specs.empty())
        {
            return {LayoutError::empty, 0U};
        }
        if (specs.size() > Capacity)
        {
            return {LayoutError::capacity, specs.size()};
        }

        if (!Overrides::presentIn(specs.size()))
        {
            return {LayoutError::capacity, specs.size()};
        }

        for (std::size_t index = 0; index < specs.size(); ++index)
        {
            const ViewSpec& spec = specs[index];
            if (!Overrides::accepts(index, spec.kind))
            {
                destroy();
                return {LayoutError::invalidKind, index};
            }
            // Every kind is in AllWidgets; the switch's default still rejects an invalid value.
            if constexpr (!std::is_same_v<Widgets, AllWidgets>)
            {
                if (!builds(spec.kind))
                {
                    destroy();
                    return {LayoutError::invalidKind, index};
                }
            }
            const Font* nodeFont = &defaultFont;
            if (!spec.font.empty())
            {
                nodeFont = resources == nullptr ? nullptr : resources->font(spec.font);
                if (nodeFont == nullptr)
                {
                    destroy();
                    return {LayoutError::missingResource, index};
                }
            }
            // Each case compiles only for a kind in Widgets, so other widgets are never referenced.
            switch (spec.kind)
            {
            case ViewKind::panel:
                if constexpr (builds(ViewKind::panel))
                {
                    construct<View>(index, spec.frame, spec.style);
                }
                break;
            case ViewKind::label:
                if constexpr (builds(ViewKind::label))
                {
                    construct<LabelView>(index, spec.frame, *nodeFont, spec.text, spec.style);
                }
                break;
            case ViewKind::button:
                if constexpr (builds(ViewKind::button))
                {
                    const Bitmap* icon = nullptr;
                    if (!spec.resource.empty())
                    {
                        icon = resources == nullptr ? nullptr : resources->bitmap(spec.resource);
                        if (icon == nullptr)
                        {
                            destroy();
                            return {LayoutError::missingResource, index};
                        }
                    }
                    auto& view = construct<ButtonView>(
                        index, spec.frame, *nodeFont, spec.text, spec.detail, spec.style);
                    view.setBehavior(spec.buttonBehavior);
                    if (icon != nullptr)
                    {
                        view.setIcon(*icon);
                    }
                    if (spec.buttonBehavior == ButtonBehavior::toggle)
                    {
                        view.setSelected(spec.checked);
                    }
                }
                break;
            case ViewKind::progress:
                if constexpr (builds(ViewKind::progress))
                {
                    construct<ProgressView>(index, spec.frame, spec.value, spec.style);
                }
                break;
            case ViewKind::image:
                if constexpr (builds(ViewKind::image))
                {
                    const Bitmap* bitmap =
                        resources == nullptr ? nullptr : resources->bitmap(spec.resource);
                    if (bitmap == nullptr)
                    {
                        destroy();
                        return {LayoutError::missingResource, index};
                    }
                    construct<ImageView>(index, spec.frame, *bitmap, spec.style);
                }
                break;
            case ViewKind::animatedImage:
                if constexpr (builds(ViewKind::animatedImage))
                {
                    const std::size_t first = spec.animationFrameOffset;
                    const std::size_t frames = spec.animationFrameCount;
                    if (resources == nullptr || frames == 0U || first > animationFrames.size() ||
                        frames > animationFrames.size() - first)
                    {
                        destroy();
                        return {LayoutError::missingResource, index};
                    }
                    for (std::size_t frame = 0U; frame < frames; ++frame)
                    {
                        if (resources->bitmap(animationFrames[first + frame]) == nullptr)
                        {
                            destroy();
                            return {LayoutError::missingResource, index};
                        }
                    }
                    construct<AnimatedImageView>(index,
                                                 spec.frame,
                                                 *resources,
                                                 animationFrames.subspan(first, frames),
                                                 spec.animationIntervalMs,
                                                 spec.animationRepeatIndex,
                                                 spec.animationAutoplay,
                                                 spec.style);
                }
                break;
            case ViewKind::icon:
                if constexpr (builds(ViewKind::icon))
                {
                    const Bitmap* bitmap =
                        resources == nullptr ? nullptr : resources->bitmap(spec.resource);
                    if (bitmap == nullptr)
                    {
                        destroy();
                        return {LayoutError::missingResource, index};
                    }
                    construct<IconView>(index, spec.frame, *bitmap, spec.style);
                }
                break;
            case ViewKind::line:
                if constexpr (builds(ViewKind::line))
                {
                    construct<LineView>(index, spec.frame, spec.style);
                }
                break;
            case ViewKind::scroll:
                if constexpr (builds(ViewKind::scroll))
                {
                    auto& view =
                        construct<ScrollView>(index, spec.frame, spec.contentHeight, spec.style);
                    view.setContentOffset(spec.scrollOffset);
                    view.setScrollStep(spec.scrollStep);
                    view.setShowsIndicator(spec.showsIndicator);
                }
                break;
            case ViewKind::table:
                if constexpr (builds(ViewKind::table))
                {
                    auto& view =
                        construct<TableView>(index, spec.frame, spec.contentHeight, spec.style);
                    view.setContentOffset(spec.scrollOffset);
                    view.setScrollStep(spec.scrollStep);
                    view.setShowsIndicator(spec.showsIndicator);
                }
                break;
            case ViewKind::picker:
                if constexpr (builds(ViewKind::picker))
                {
                    construct<PickerView>(index,
                                          spec.frame,
                                          *nodeFont,
                                          spec.text,
                                          spec.selectedIndex,
                                          spec.wraps,
                                          spec.style);
                }
                break;
            case ViewKind::checkbox:
                if constexpr (builds(ViewKind::checkbox))
                {
                    construct<CheckboxView>(
                        index, spec.frame, *nodeFont, spec.text, spec.checked, spec.style);
                }
                break;
            case ViewKind::radio:
                if constexpr (builds(ViewKind::radio))
                {
                    construct<RadioButtonView>(
                        index, spec.frame, *nodeFont, spec.text, spec.checked, spec.style);
                }
                break;
            case ViewKind::textField:
                if constexpr (builds(ViewKind::textField))
                {
                    if (resources == nullptr)
                    {
                        destroy();
                        return {LayoutError::missingResource, index};
                    }
                    const MutableTextBuffer buffer = resources->textBuffer(spec.resource);
                    if (buffer.empty())
                    {
                        destroy();
                        return {LayoutError::missingResource, index};
                    }
                    construct<TextFieldView>(index,
                                             spec.frame,
                                             *nodeFont,
                                             buffer,
                                             spec.maxLength,
                                             spec.detail,
                                             spec.style,
                                             resources->textEditBuffer(spec.resource));
                }
                break;
            case ViewKind::softkey:
                if constexpr (builds(ViewKind::softkey))
                {
                    construct<SoftkeyView>(index,
                                           spec.frame,
                                           *nodeFont,
                                           spec.text,
                                           spec.detail,
                                           spec.leftAction,
                                           spec.rightAction,
                                           spec.style);
                }
                break;
            case ViewKind::tabBar:
                if constexpr (builds(ViewKind::tabBar))
                {
                    construct<TabBarView>(index,
                                          spec.frame,
                                          *nodeFont,
                                          spec.text,
                                          spec.selectedIndex,
                                          spec.wraps,
                                          spec.style);
                }
                break;
            case ViewKind::tabView:
                if constexpr (builds(ViewKind::tabView))
                {
                    construct<TabView>(index,
                                       spec.frame,
                                       *nodeFont,
                                       spec.text,
                                       spec.selectedIndex,
                                       spec.tabHeight,
                                       spec.wraps,
                                       spec.style);
                }
                break;
            case ViewKind::dropDown:
                if constexpr (builds(ViewKind::dropDown))
                {
                    construct<DropDownView>(index,
                                            spec.frame,
                                            *nodeFont,
                                            spec.text,
                                            spec.selectedIndex,
                                            spec.rowHeight,
                                            spec.wraps,
                                            spec.style);
                }
                break;
            case ViewKind::wheelPicker:
                if constexpr (builds(ViewKind::wheelPicker))
                {
                    construct<WheelPickerView>(index,
                                               spec.frame,
                                               *nodeFont,
                                               spec.text,
                                               spec.selectedIndex,
                                               spec.rowHeight,
                                               spec.wraps,
                                               spec.style);
                }
                break;
            default:
                destroy();
                return {LayoutError::invalidKind, index};
            }
            View* const currentView = viewAt(index);
            if (currentView == nullptr)
            {
                destroy();
                return {LayoutError::invalidKind, index};
            }
            View& current = *currentView;
            current.setId(spec.id);
            current.setVisible(spec.visible);
            current.setEnabled(spec.enabled);
            if (spec.focusable >= 0)
            {
                current.setFocusable(spec.focusable != 0);
            }
            ++mCount;

            if (!spec.parent.has_value())
            {
                if (mRoot != nullptr)
                {
                    destroy();
                    return {LayoutError::multipleRoots, index};
                }
                mRoot = &current;
            }
            else
            {
                const std::size_t parent = *spec.parent;
                if (parent >= index)
                {
                    destroy();
                    return {LayoutError::invalidParent, index};
                }
                View* const parentView = viewAt(parent);
                if (parentView == nullptr || !parentView->addSubview(current))
                {
                    destroy();
                    return {LayoutError::tooManyChildren, index};
                }
            }
        }
        for (std::size_t index = 0; index < mCount; ++index)
        {
            finishContainer(index);
        }
        return mRoot == nullptr ? LayoutResult{LayoutError::empty, 0U} : LayoutResult{};
    }

    View* root() noexcept
    {
        return mRoot;
    }
    const View* root() const noexcept
    {
        return mRoot;
    }
    View* find(ViewId id) noexcept
    {
        return mRoot == nullptr ? nullptr : mRoot->find(id);
    }

    template <typename ViewType> ViewType* find(ViewId id) noexcept
    {
        static_assert(std::is_base_of_v<View, ViewType>);
        for (std::size_t index = 0U; index < mCount; ++index)
        {
            if (viewAt(index)->id() == id)
            {
                return storedAs<ViewType>(index);
            }
        }
        return nullptr;
    }

    template <typename ViewType> const ViewType* find(ViewId id) const noexcept
    {
        static_assert(std::is_base_of_v<View, ViewType>);
        for (std::size_t index = 0U; index < mCount; ++index)
        {
            if (viewAt(index)->id() == id)
            {
                return storedAs<ViewType>(index);
            }
        }
        return nullptr;
    }

    // Index access is checked and keeps the actual selected type. Generated names
    // use this path, so duplicate/unassigned runtime IDs are irrelevant.
    template <std::size_t Index, typename Default>
    ComponentType<Index, Default>* component() noexcept
    {
        static_assert(Index < Capacity);
        using Actual = ComponentType<Index, Default>;
        if (Index >= mCount)
        {
            return nullptr;
        }
        if constexpr (Overrides::contains(Index))
        {
            return mOverrides.template get<Actual>(Index);
        }
        else
        {
            return storedAs<Actual>(Index);
        }
    }

    void destroy() noexcept
    {
        for (std::size_t index = mCount; index > 0U; --index)
        {
            const std::size_t slot = index - 1U;
            destroyAt(slot);
        }
        mCount = 0U;
        mRoot = nullptr;
    }

private:
    static constexpr bool builds(ViewKind kind) noexcept
    {
        return Widgets::contains(kind);
    }

    template <typename ViewType, typename... Arguments>
    ViewType& construct(std::size_t index, Arguments&&... arguments) noexcept
    {
        if (Overrides::contains(index))
        {
            mKinds[index] = kindFor<ViewType>();
            return mOverrides.template construct<ViewType>(index,
                                                           std::forward<Arguments>(arguments)...);
        }
        ViewType* const storage =
            storageAddress<ViewType>(mStorage[Overrides::builtinIndex(index)]);
        ViewType& view = *std::construct_at(storage, std::forward<Arguments>(arguments)...);
        mKinds[index] = kindFor<ViewType>();
        return view;
    }

    // This is a private discriminated union: mKinds is set immediately after construction,
    // consulted before every access, and used for reverse-order destruction. Core Guidelines
    // C.180/C.181 permit this bounded-storage use. std::variant added 31,996 bytes to the H750
    // debug image, which is disproportionate on the intended small targets.
    // NOLINTBEGIN(cppcoreguidelines-pro-type-union-access)
    union StoredView
    {
        constexpr StoredView() noexcept
        {
        }
        ~StoredView() noexcept
        {
        }

        StoredView(const StoredView&) = delete;
        StoredView& operator=(const StoredView&) = delete;
        StoredView(StoredView&&) = delete;
        StoredView& operator=(StoredView&&) = delete;

        View panel;
        LabelView label;
        ButtonView button;
        ProgressView progress;
        ImageView image;
        AnimatedImageView animatedImage;
        IconView icon;
        LineView line;
        ScrollView scroll;
        TableView table;
        PickerView picker;
        CheckboxView checkbox;
        RadioButtonView radio;
        TextFieldView textField;
        SoftkeyView softkey;
        TabBarView tabBar;
        TabView tabView;
        WheelPickerView wheelPicker;
        DropDownView dropDown;
    };

    template <typename ViewType> static constexpr ViewKind kindFor() noexcept
    {
        if constexpr (std::is_same_v<ViewType, View>)
        {
            return ViewKind::panel;
        }
        else if constexpr (std::is_same_v<ViewType, LabelView>)
        {
            return ViewKind::label;
        }
        else if constexpr (std::is_same_v<ViewType, ButtonView>)
        {
            return ViewKind::button;
        }
        else if constexpr (std::is_same_v<ViewType, ProgressView>)
        {
            return ViewKind::progress;
        }
        else if constexpr (std::is_same_v<ViewType, ImageView>)
        {
            return ViewKind::image;
        }
        else if constexpr (std::is_same_v<ViewType, AnimatedImageView>)
        {
            return ViewKind::animatedImage;
        }
        else if constexpr (std::is_same_v<ViewType, IconView>)
        {
            return ViewKind::icon;
        }
        else if constexpr (std::is_same_v<ViewType, LineView>)
        {
            return ViewKind::line;
        }
        else if constexpr (std::is_same_v<ViewType, ScrollView>)
        {
            return ViewKind::scroll;
        }
        else if constexpr (std::is_same_v<ViewType, TableView>)
        {
            return ViewKind::table;
        }
        else if constexpr (std::is_same_v<ViewType, PickerView>)
        {
            return ViewKind::picker;
        }
        else if constexpr (std::is_same_v<ViewType, CheckboxView>)
        {
            return ViewKind::checkbox;
        }
        else if constexpr (std::is_same_v<ViewType, RadioButtonView>)
        {
            return ViewKind::radio;
        }
        else if constexpr (std::is_same_v<ViewType, TextFieldView>)
        {
            return ViewKind::textField;
        }
        else if constexpr (std::is_same_v<ViewType, SoftkeyView>)
        {
            return ViewKind::softkey;
        }
        else if constexpr (std::is_same_v<ViewType, TabBarView>)
        {
            return ViewKind::tabBar;
        }
        else if constexpr (std::is_same_v<ViewType, TabView>)
        {
            return ViewKind::tabView;
        }
        else if constexpr (std::is_same_v<ViewType, DropDownView>)
        {
            return ViewKind::dropDown;
        }
        else
        {
            static_assert(std::is_same_v<ViewType, WheelPickerView>);
            return ViewKind::wheelPicker;
        }
    }

    template <typename ViewType> static ViewType* storageAddress(StoredView& storage) noexcept
    {
        if constexpr (std::is_same_v<ViewType, View>)
        {
            return &storage.panel;
        }
        else if constexpr (std::is_same_v<ViewType, LabelView>)
        {
            return &storage.label;
        }
        else if constexpr (std::is_same_v<ViewType, ButtonView>)
        {
            return &storage.button;
        }
        else if constexpr (std::is_same_v<ViewType, ProgressView>)
        {
            return &storage.progress;
        }
        else if constexpr (std::is_same_v<ViewType, ImageView>)
        {
            return &storage.image;
        }
        else if constexpr (std::is_same_v<ViewType, AnimatedImageView>)
        {
            return &storage.animatedImage;
        }
        else if constexpr (std::is_same_v<ViewType, IconView>)
        {
            return &storage.icon;
        }
        else if constexpr (std::is_same_v<ViewType, LineView>)
        {
            return &storage.line;
        }
        else if constexpr (std::is_same_v<ViewType, ScrollView>)
        {
            return &storage.scroll;
        }
        else if constexpr (std::is_same_v<ViewType, TableView>)
        {
            return &storage.table;
        }
        else if constexpr (std::is_same_v<ViewType, PickerView>)
        {
            return &storage.picker;
        }
        else if constexpr (std::is_same_v<ViewType, CheckboxView>)
        {
            return &storage.checkbox;
        }
        else if constexpr (std::is_same_v<ViewType, RadioButtonView>)
        {
            return &storage.radio;
        }
        else if constexpr (std::is_same_v<ViewType, TextFieldView>)
        {
            return &storage.textField;
        }
        else if constexpr (std::is_same_v<ViewType, SoftkeyView>)
        {
            return &storage.softkey;
        }
        else if constexpr (std::is_same_v<ViewType, TabBarView>)
        {
            return &storage.tabBar;
        }
        else if constexpr (std::is_same_v<ViewType, TabView>)
        {
            return &storage.tabView;
        }
        else if constexpr (std::is_same_v<ViewType, DropDownView>)
        {
            return &storage.dropDown;
        }
        else
        {
            static_assert(std::is_same_v<ViewType, WheelPickerView>);
            return &storage.wheelPicker;
        }
    }

    template <typename ViewType>
    static const ViewType* storageAddress(const StoredView& storage) noexcept
    {
        if constexpr (std::is_same_v<ViewType, View>)
        {
            return &storage.panel;
        }
        else if constexpr (std::is_same_v<ViewType, LabelView>)
        {
            return &storage.label;
        }
        else if constexpr (std::is_same_v<ViewType, ButtonView>)
        {
            return &storage.button;
        }
        else if constexpr (std::is_same_v<ViewType, ProgressView>)
        {
            return &storage.progress;
        }
        else if constexpr (std::is_same_v<ViewType, ImageView>)
        {
            return &storage.image;
        }
        else if constexpr (std::is_same_v<ViewType, AnimatedImageView>)
        {
            return &storage.animatedImage;
        }
        else if constexpr (std::is_same_v<ViewType, IconView>)
        {
            return &storage.icon;
        }
        else if constexpr (std::is_same_v<ViewType, LineView>)
        {
            return &storage.line;
        }
        else if constexpr (std::is_same_v<ViewType, ScrollView>)
        {
            return &storage.scroll;
        }
        else if constexpr (std::is_same_v<ViewType, TableView>)
        {
            return &storage.table;
        }
        else if constexpr (std::is_same_v<ViewType, PickerView>)
        {
            return &storage.picker;
        }
        else if constexpr (std::is_same_v<ViewType, CheckboxView>)
        {
            return &storage.checkbox;
        }
        else if constexpr (std::is_same_v<ViewType, RadioButtonView>)
        {
            return &storage.radio;
        }
        else if constexpr (std::is_same_v<ViewType, TextFieldView>)
        {
            return &storage.textField;
        }
        else if constexpr (std::is_same_v<ViewType, SoftkeyView>)
        {
            return &storage.softkey;
        }
        else if constexpr (std::is_same_v<ViewType, TabBarView>)
        {
            return &storage.tabBar;
        }
        else if constexpr (std::is_same_v<ViewType, TabView>)
        {
            return &storage.tabView;
        }
        else if constexpr (std::is_same_v<ViewType, DropDownView>)
        {
            return &storage.dropDown;
        }
        else
        {
            static_assert(std::is_same_v<ViewType, WheelPickerView>);
            return &storage.wheelPicker;
        }
    }

    // Calls action with the view stored at index, typed as constructed. Only kinds in Widgets are
    // visited, so the widgets a layout never builds are never referenced.
    template <typename Storage, typename Action>
    static void visitStored(Storage& storage, ViewKind kind, Action&& action) noexcept
    {
        visitStoredAs<View,
                      LabelView,
                      ButtonView,
                      ProgressView,
                      ImageView,
                      AnimatedImageView,
                      IconView,
                      LineView,
                      ScrollView,
                      TableView,
                      PickerView,
                      CheckboxView,
                      RadioButtonView,
                      TextFieldView,
                      SoftkeyView,
                      TabBarView,
                      TabView,
                      WheelPickerView,
                      DropDownView>(storage, kind, action);
    }

    template <typename... ViewTypes, typename Storage, typename Action>
    static void visitStoredAs(Storage& storage, ViewKind kind, Action& action) noexcept
    {
        static_assert(sizeof...(ViewTypes) == AllWidgets::size);
        (visitIfStored<ViewTypes>(storage, kind, action) || ...);
    }

    template <typename ViewType, typename Storage, typename Action>
    static bool visitIfStored(Storage& storage, ViewKind kind, Action& action) noexcept
    {
        if constexpr (builds(kindFor<ViewType>()))
        {
            if (kind == kindFor<ViewType>())
            {
                action(*storageAddress<ViewType>(storage));
                return true;
            }
        }
        return false;
    }

    View* viewPointer(std::size_t index) noexcept
    {
        if (Overrides::contains(index))
        {
            return mOverrides.template get<View>(index);
        }
        View* view = nullptr;
        visitStored(mStorage[Overrides::builtinIndex(index)],
                    mKinds[index],
                    [&view](View& stored) noexcept
                    {
                        view = &stored;
                    });
        return view;
    }

    const View* viewPointer(std::size_t index) const noexcept
    {
        if (Overrides::contains(index))
        {
            return mOverrides.template get<View>(index);
        }
        const View* view = nullptr;
        visitStored(mStorage[Overrides::builtinIndex(index)],
                    mKinds[index],
                    [&view](const View& stored) noexcept
                    {
                        view = &stored;
                    });
        return view;
    }

    template <typename ViewType> ViewType* storedAs(std::size_t index) noexcept
    {
        if (Overrides::contains(index))
        {
            return mOverrides.template get<ViewType>(index);
        }
        if constexpr (!Overrides::template isCustomType<ViewType>())
        {
            if constexpr (builds(kindFor<ViewType>()))
            {
                return mKinds[index] == kindFor<ViewType>()
                           ? storageAddress<ViewType>(mStorage[Overrides::builtinIndex(index)])
                           : nullptr;
            }
        }
        return nullptr;
    }

    template <typename ViewType> const ViewType* storedAs(std::size_t index) const noexcept
    {
        if (Overrides::contains(index))
        {
            return mOverrides.template get<ViewType>(index);
        }
        if constexpr (!Overrides::template isCustomType<ViewType>())
        {
            if constexpr (builds(kindFor<ViewType>()))
            {
                return mKinds[index] == kindFor<ViewType>()
                           ? storageAddress<ViewType>(mStorage[Overrides::builtinIndex(index)])
                           : nullptr;
            }
        }
        return nullptr;
    }

    // Containers whose state depends on their attached children finish here.
    void finishContainer(std::size_t index) noexcept
    {
        if (Overrides::contains(index))
        {
            mOverrides.finish(index);
            return;
        }
        visitStored(mStorage[Overrides::builtinIndex(index)],
                    mKinds[index],
                    []<typename ViewType>(ViewType& view) noexcept
                    {
                        if constexpr (std::is_same_v<ViewType, ScrollView> ||
                                      std::is_same_v<ViewType, TableView>)
                        {
                            view.fitContentToSubviews();
                        }
                        else if constexpr (std::is_same_v<ViewType, TabView>)
                        {
                            view.syncPages();
                        }
                    });
    }

    void destroyAt(std::size_t index) noexcept
    {
        if (Overrides::contains(index))
        {
            mOverrides.destroy(index);
            return;
        }
        visitStored(mStorage[Overrides::builtinIndex(index)],
                    mKinds[index],
                    []<typename ViewType>(ViewType& view) noexcept
                    {
                        std::destroy_at(&view);
                    });
    }
    // NOLINTEND(cppcoreguidelines-pro-type-union-access)

    View* viewAt(std::size_t index) noexcept
    {
        return viewPointer(index);
    }

    const View* viewAt(std::size_t index) const noexcept
    {
        return viewPointer(index);
    }

    std::array<StoredView, Capacity - Overrides::size> mStorage{};
    [[no_unique_address]] Overrides mOverrides{};
    std::array<ViewKind, Capacity> mKinds{};
    std::size_t mCount{0};
    View* mRoot{nullptr};
};

} // namespace lithe
