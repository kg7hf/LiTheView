// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/widgets.hpp"

#include <cstdint>

namespace lithe
{

enum class ViewKind : std::uint8_t
{
    panel,
    label,
    button,
    progress,
    image,
    animatedImage,
    icon,
    line,
    scroll,
    table,
    picker,
    checkbox,
    radio,
    textField,
    softkey,
    tabBar,
    tabView,
    wheelPicker,
    dropDown
};

template <ViewKind Kind> struct WidgetType;

template <> struct WidgetType<ViewKind::panel>
{
    using Type = View;
};

template <> struct WidgetType<ViewKind::label>
{
    using Type = LabelView;
};

template <> struct WidgetType<ViewKind::button>
{
    using Type = ButtonView;
};

template <> struct WidgetType<ViewKind::progress>
{
    using Type = ProgressView;
};

template <> struct WidgetType<ViewKind::image>
{
    using Type = ImageView;
};

template <> struct WidgetType<ViewKind::animatedImage>
{
    using Type = AnimatedImageView;
};

template <> struct WidgetType<ViewKind::icon>
{
    using Type = IconView;
};

template <> struct WidgetType<ViewKind::line>
{
    using Type = LineView;
};

template <> struct WidgetType<ViewKind::scroll>
{
    using Type = ScrollView;
};

template <> struct WidgetType<ViewKind::table>
{
    using Type = TableView;
};

template <> struct WidgetType<ViewKind::picker>
{
    using Type = PickerView;
};

template <> struct WidgetType<ViewKind::checkbox>
{
    using Type = CheckboxView;
};

template <> struct WidgetType<ViewKind::radio>
{
    using Type = RadioButtonView;
};

template <> struct WidgetType<ViewKind::textField>
{
    using Type = TextFieldView;
};

template <> struct WidgetType<ViewKind::softkey>
{
    using Type = SoftkeyView;
};

template <> struct WidgetType<ViewKind::tabBar>
{
    using Type = TabBarView;
};

template <> struct WidgetType<ViewKind::tabView>
{
    using Type = TabView;
};

template <> struct WidgetType<ViewKind::wheelPicker>
{
    using Type = WheelPickerView;
};

template <> struct WidgetType<ViewKind::dropDown>
{
    using Type = DropDownView;
};

} // namespace lithe
