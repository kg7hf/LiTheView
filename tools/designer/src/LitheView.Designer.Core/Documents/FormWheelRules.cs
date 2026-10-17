// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Documents;

/// <summary>
/// Wheel picker rules shared by the resolver and the editing session. They mirror
/// <c>lithe::WheelPickerView</c>: one through 255 pipe-delimited items and a selected index that
/// names one of them.
/// </summary>
public static class FormWheelRules
{
    public const int MaximumItems = 255;
    public const ulong DefaultRowHeight = 28U;
    public const string StarterItems = "ITEM 1|ITEM 2|ITEM 3|ITEM 4|ITEM 5";

    public static bool IsWheelPicker(WidgetDefinition widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return widget.RuntimeKind == "wheelPicker";
    }

    public static int CountItems(IEnumerable<FormPropertyValue> properties)
    {
        return FormTabRules.CountTitles(FormTabRules.Titles(properties));
    }
}
