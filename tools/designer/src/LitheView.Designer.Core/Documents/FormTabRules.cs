// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Documents;

/// <summary>
/// Tab bar and tab view rules shared by the resolver, the editing session, and the designer service.
/// They mirror <c>lithe::TabBarView</c> and <c>lithe::TabView</c>: pipe-delimited titles, at most
/// sixteen tabs, and pages that are the tab view's direct children in sibling order.
/// </summary>
public static class FormTabRules
{
    public const int MaximumTabs = 16;
    public const ulong DefaultTabHeight = 32U;
    public const string StarterTitles = "TAB 1|TAB 2";
    private const char TitleSeparator = '|';

    public static bool IsTabWidget(WidgetDefinition widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return widget.RuntimeKind is "tabBar" or "tabView";
    }

    public static bool IsTabView(WidgetDefinition widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return widget.RuntimeKind == "tabView";
    }

    public static int CountTitles(string? titles)
    {
        return string.IsNullOrEmpty(titles) ? 0 : titles.Count(character => character == TitleSeparator) + 1;
    }

    public static string? Titles(IEnumerable<FormPropertyValue> properties)
    {
        return properties.OfType<FormTextValue>().SingleOrDefault(item => item.PropertyId == "items")?.Value;
    }

    public static ulong SelectedIndex(IEnumerable<FormPropertyValue> properties)
    {
        return properties.OfType<FormUnsignedIntegerValue>()
            .SingleOrDefault(item => item.PropertyId == "selected-index")?.Value ?? 0U;
    }

    public static ulong TabHeight(IEnumerable<FormPropertyValue> properties)
    {
        return properties.OfType<FormUnsignedIntegerValue>()
            .SingleOrDefault(item => item.PropertyId == "tab-height")?.Value ?? DefaultTabHeight;
    }

    /// <summary>The page area in tab-view coordinates; the strip is clamped to the frame like the runtime.</summary>
    public static FormRectangle ContentFrame(FormRectangle tabViewFrame, ulong tabHeight)
    {
        int strip = (int)Math.Min(tabHeight, (ulong)Math.Max(0, tabViewFrame.Height));
        return new FormRectangle(0, strip, tabViewFrame.Width, tabViewFrame.Height - strip);
    }

    public static bool Contains(FormRectangle outer, FormRectangle inner)
    {
        return inner.X >= outer.X && inner.Y >= outer.Y &&
            (long)inner.X + inner.Width <= (long)outer.X + outer.Width &&
            (long)inner.Y + inner.Height <= (long)outer.Y + outer.Height;
    }

    /// <summary>
    /// Component IDs that the runtime hides because they are, or are inside, a page other than the
    /// selected one. Pages are a tab view's direct children in sibling order.
    /// </summary>
    public static HashSet<string> HiddenPageComponentIds(
        IReadOnlyList<FormComponent> components,
        Func<string, bool> isTabView)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(isTabView);
        HashSet<string> hidden = new(StringComparer.Ordinal);
        foreach (FormComponent tabView in components.Where(item => isTabView(item.WidgetId)))
        {
            ulong selected = SelectedIndex(tabView.Properties);
            FormComponent[] pages = [.. components
                .Where(item => item.ParentComponentId == tabView.ComponentId)
                .OrderBy(item => item.SiblingOrder)];
            for (int index = 0; index < pages.Length; ++index)
            {
                if ((ulong)index != selected)
                {
                    hidden.Add(pages[index].ComponentId);
                }
            }
        }
        // Components appear in preorder, so a hidden parent is always seen before its children.
        foreach (FormComponent component in components)
        {
            if (component.ParentComponentId is not null && hidden.Contains(component.ParentComponentId))
            {
                hidden.Add(component.ComponentId);
            }
        }
        return hidden;
    }
}
