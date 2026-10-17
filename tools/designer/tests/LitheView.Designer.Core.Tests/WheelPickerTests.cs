// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class WheelPickerTests
{
    private const string RootId = "component.wheel.root";
    private const string WheelId = "component.wheel.hour";

    public static void WheelReachesXmlAndCpp(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        FormDocument form = CreateForm();
        FormResolutionResult resolution = FormDocumentResolver.Resolve(form, catalog);
        Assert(
            resolution.IsValid && resolution.Diagnostics.Count == 0,
            Messages("A wheel picker form did not resolve cleanly", resolution.Diagnostics));

        FormXmlWriteResult xml = FormXmlWriter.Write(form, catalog);
        Assert(xml.IsValid && xml.Utf8 is not null, Messages("The wheel did not write XML", xml.Diagnostics));
        string text = Encoding.UTF8.GetString(xml.Utf8!);
        Assert(
            text.Contains("<wheelpicker frame=\"40,20,120,140\"", StringComparison.Ordinal) &&
            text.Contains("items=\"06|07|08|09|10\"", StringComparison.Ordinal) &&
            text.Contains("selectedIndex=\"2\"", StringComparison.Ordinal) &&
            text.Contains("rowHeight=\"24\"", StringComparison.Ordinal) &&
            text.Contains("wrap=\"false\"", StringComparison.Ordinal),
            "Canonical XML lost the wheel element, items, selection, row height, or wrap.");

        FormDocumentLoadResult imported = FormXmlImporter.Load(xml.Utf8!, catalog, FormImportMode.Strict);
        Assert(imported.IsValid, Messages("Strict import rejected the wheel", imported.Diagnostics));
        Assert(
            imported.CanonicalXmlUtf8!.AsSpan().SequenceEqual(xml.Utf8),
            "Wheel XML did not reach a canonical fixed point.");

        FormCppCompilationResult cpp = FormCppCompiler.Compile(form, catalog);
        Assert(cpp.HeaderUtf8 is not null, Messages("The wheel did not compile to C++", cpp.Diagnostics));
        string header = Encoding.UTF8.GetString(cpp.HeaderUtf8!);
        Assert(
            header.Contains("spec.kind = ViewKind::wheelPicker;", StringComparison.Ordinal) &&
            header.Contains("spec.rowHeight = 24U;", StringComparison.Ordinal) &&
            header.Contains("spec.text = \"06|07|08|09|10\";", StringComparison.Ordinal) &&
            header.Contains("spec.selectedIndex = 2U;", StringComparison.Ordinal) &&
            header.Contains("spec.wraps = false;", StringComparison.Ordinal),
            "Generated C++ lost the wheel kind, row height, items, selection, or wrap.");
        Assert(
            header.Contains(
                "using Widgets = WidgetSet<ViewKind::panel, ViewKind::wheelPicker>;",
                StringComparison.Ordinal) &&
            header.Contains("using Layout = StaticLayout<kViewCount, Widgets>;", StringComparison.Ordinal),
            "Generated C++ must declare exactly the widget kinds the form uses, in catalog order.");
    }

    public static void WheelRulesFailClosed(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        FormDocument form = CreateForm();
        foreach (FormPropertyValue[] items in new[]
                 {
                     Array.Empty<FormPropertyValue>(),
                     [new FormTextValue("items", string.Join('|', Enumerable.Repeat("I", FormWheelRules.MaximumItems + 1)))],
                     [new FormTextValue("items", "ONE|TWO"), new FormUnsignedIntegerValue("selected-index", 2U)]
                 })
        {
            FormDocument invalid = Replace(form, properties =>
                [.. properties.Where(item => item.PropertyId is not ("items" or "selected-index")), .. items]);
            FormResolutionResult resolution = FormDocumentResolver.Resolve(invalid, catalog);
            Assert(
                !resolution.IsValid && resolution.Diagnostics.Any(item => item.Code == "LVFD1136"),
                "Invalid wheel items or selection were not rejected.");
        }

        FormDocument full = Replace(form, properties =>
            [.. properties.Where(item => item.PropertyId is not ("items" or "selected-index")),
             new FormTextValue("items", string.Join('|', Enumerable.Repeat("I", FormWheelRules.MaximumItems))),
             new FormUnsignedIntegerValue("selected-index", (ulong)FormWheelRules.MaximumItems - 1U)]);
        Assert(FormDocumentResolver.Resolve(full, catalog).IsValid, "A wheel with the maximum item count was rejected.");

        FormDocument shortRows = Replace(form, properties =>
            [.. properties.Where(item => item.PropertyId != "row-height"), new FormUnsignedIntegerValue("row-height", 11U)]);
        Assert(!FormDocumentResolver.Resolve(shortRows, catalog).IsValid, "A row height below 12 was accepted.");

        string beyond = Encoding.UTF8.GetString(FormXmlWriter.Write(form, catalog).Utf8!)
            .Replace("selectedIndex=\"2\"", "selectedIndex=\"5\"", StringComparison.Ordinal);
        FormDocumentLoadResult strictImport = FormXmlImporter.Load(
            Encoding.UTF8.GetBytes(beyond),
            catalog,
            FormImportMode.Strict);
        Assert(
            !strictImport.IsValid && strictImport.Diagnostics.Any(item => item.Code == "LVFD1136"),
            "Strict XML import accepted a selected item beyond the list.");
    }

    public static void SessionAddsAWheelWithStarterItems(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        DesignerDocumentSession session = new(CreateForm(), catalog);
        FormEditResult added = session.AddWidget("lithe.widget.wheel-picker", RootId);
        Assert(added.IsValid && added.Changed, Messages("A wheel picker could not be added", added.Diagnostics));
        FormComponent wheel = session.Document.Components[^1];
        Assert(
            FormTabRules.Titles(wheel.Properties) == FormWheelRules.StarterItems,
            "A new wheel picker did not start with valid items.");
        Assert(added.Diagnostics.Count == 0, Messages("A new wheel picker reported diagnostics", added.Diagnostics));
        Assert(
            !session.SetProperty(wheel.ComponentId, new FormUnsignedIntegerValue("selected-index", 5U)).IsValid,
            "A selected index beyond the starter items was accepted.");
        Assert(
            session.SetProperty(wheel.ComponentId, new FormUnsignedIntegerValue("selected-index", 4U)).IsValid,
            "The last starter item could not be selected.");
    }

    private static FormDocument Replace(FormDocument form, Func<FormPropertyValue[], FormPropertyValue[]> change)
    {
        return form with
        {
            Components = [.. form.Components.Select(item => item.ComponentId != WheelId
                ? item
                : item with { Properties = change(item.Properties) })]
        };
    }

    private static FormDocument CreateForm()
    {
        return new FormDocument
        {
            SchemaVersion = 1,
            Id = "form.wheel",
            DisplayName = "Wheel",
            SurfaceProfileId = "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565",
            Components =
            [
                Component(RootId, "rootPanel", "lithe.widget.panel", null, 0, new FormRectangle(0, 0, 480, 272)),
                Component(WheelId, "hourWheel", "lithe.widget.wheel-picker", RootId, 0,
                    new FormRectangle(40, 20, 120, 140),
                    new FormUnsignedIntegerValue("runtime-id", 70U),
                    new FormTextValue("items", "06|07|08|09|10"),
                    new FormUnsignedIntegerValue("selected-index", 2U),
                    new FormBooleanValue("wrap", false),
                    new FormUnsignedIntegerValue("row-height", 24U))
            ],
            Actions = [],
            Bindings = [],
            Resources = [],
            ThemeColors = []
        };
    }

    private static FormComponent Component(
        string id,
        string name,
        string widgetId,
        string? parentId,
        int siblingOrder,
        FormRectangle frame,
        params FormPropertyValue[] properties)
    {
        return new FormComponent
        {
            ComponentId = id,
            Name = name,
            WidgetId = widgetId,
            ParentComponentId = parentId,
            SiblingOrder = siblingOrder,
            Properties = [new FormRectangleValue("frame", frame), .. properties],
            HistoricalAttributes = []
        };
    }

    private static WidgetCatalog LoadCatalog(string path)
    {
        WidgetCatalogLoadResult result = WidgetCatalogLoader.LoadFile(path);
        Assert(result.IsValid && result.Catalog is not null, "The widget catalog is invalid.");
        return result.Catalog!;
    }

    private static string Messages(string prefix, IEnumerable<Profiles.ProfileDiagnostic> diagnostics)
    {
        return $"{prefix}: {string.Join("; ", diagnostics.Select(item => $"{item.Code} {item.Message}"))}";
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
