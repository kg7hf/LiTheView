// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class TabViewTests
{
    private const string RootId = "component.tabs.root";
    private const string TabViewId = "component.tabs.view";
    private const string HomePageId = "component.tabs.home";
    private const string SetupPageId = "component.tabs.setup";
    private const string ApplyId = "component.tabs.apply";
    private const string TabBarId = "component.tabs.bar";

    public static void TabsReachXmlAndCpp(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        FormDocument form = CreateForm();
        FormResolutionResult resolution = FormDocumentResolver.Resolve(form, catalog);
        Assert(
            resolution.IsValid && resolution.Diagnostics.Count == 0,
            Messages("A complete tab form did not resolve cleanly", resolution.Diagnostics));

        FormXmlWriteResult xml = FormXmlWriter.Write(form, catalog);
        Assert(xml.IsValid && xml.Utf8 is not null, Messages("Tabs did not write XML", xml.Diagnostics));
        string text = Encoding.UTF8.GetString(xml.Utf8!);
        Assert(
            text.Contains("<tabview frame=\"8,8,464,200\"", StringComparison.Ordinal) &&
            text.Contains("items=\"HOME|SETUP\"", StringComparison.Ordinal) &&
            text.Contains("tabHeight=\"28\"", StringComparison.Ordinal) &&
            text.Contains("<tabbar frame=\"8,216,464,32\"", StringComparison.Ordinal) &&
            text.Contains("wrap=\"false\"", StringComparison.Ordinal),
            "Canonical XML lost a tab element, titles, strip height, or wrap.");

        FormDocumentLoadResult imported = FormXmlImporter.Load(xml.Utf8!, catalog, FormImportMode.Strict);
        Assert(imported.IsValid, Messages("Strict import rejected tabs", imported.Diagnostics));
        Assert(
            imported.CanonicalXmlUtf8!.AsSpan().SequenceEqual(xml.Utf8),
            "Tab XML did not reach a canonical fixed point.");

        FormCppCompilationResult cpp = FormCppCompiler.Compile(form, catalog);
        Assert(cpp.HeaderUtf8 is not null, Messages("Tabs did not compile to C++", cpp.Diagnostics));
        string header = Encoding.UTF8.GetString(cpp.HeaderUtf8!);
        Assert(
            header.Contains("spec.kind = ViewKind::tabView;", StringComparison.Ordinal) &&
            header.Contains("spec.kind = ViewKind::tabBar;", StringComparison.Ordinal) &&
            header.Contains("spec.tabHeight = 28U;", StringComparison.Ordinal) &&
            header.Contains("spec.text = \"HOME|SETUP\";", StringComparison.Ordinal) &&
            header.Contains("spec.wraps = false;", StringComparison.Ordinal),
            "Generated C++ lost a tab kind, strip height, titles, or wrap.");
    }

    public static void TabRulesFailClosed(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        FormDocument form = CreateForm();
        foreach (FormPropertyValue[] titles in new[]
                 {
                     Array.Empty<FormPropertyValue>(),
                     [new FormTextValue("items", string.Join('|', Enumerable.Repeat("T", FormTabRules.MaximumTabs + 1)))],
                     [new FormTextValue("items", "ONE"), new FormUnsignedIntegerValue("selected-index", 1U)]
                 })
        {
            FormDocument invalid = Replace(form, TabBarId, properties =>
                [.. properties.Where(item => item.PropertyId is not ("items" or "selected-index")), .. titles]);
            FormResolutionResult resolution = FormDocumentResolver.Resolve(invalid, catalog);
            Assert(
                !resolution.IsValid && resolution.Diagnostics.Any(item => item.Code == "LVFD1133"),
                "Invalid tab titles or selection were not rejected.");
        }

        FormDocument extraTab = Replace(form, TabViewId, properties =>
            [.. properties.Where(item => item.PropertyId != "items"), new FormTextValue("items", "HOME|SETUP|INFO")]);
        FormResolutionResult mismatch = FormDocumentResolver.Resolve(extraTab, catalog);
        Assert(
            mismatch.IsValid && mismatch.Diagnostics.Any(item =>
                item.Code == "LVFD1134" && item.Severity == Profiles.ProfileDiagnosticSeverity.Warning),
            "A tab/page count mismatch must warn without blocking incremental editing.");

        FormDocument overlapping = Replace(form, SetupPageId, properties =>
            [.. properties.Where(item => item.PropertyId != "frame"),
             new FormRectangleValue("frame", new FormRectangle(0, 10, 464, 172))]);
        FormResolutionResult overlap = FormDocumentResolver.Resolve(overlapping, catalog);
        Assert(
            overlap.IsValid && overlap.Diagnostics.Any(item =>
                item.Code == "LVFD1135" && item.Path == "$.components[3]"),
            "A page covering the tab strip was not reported.");

        string tooMany = Encoding.UTF8.GetString(FormXmlWriter.Write(form, catalog).Utf8!)
            .Replace("items=\"A|B|C\"", "items=\"A|B|C\" selectedIndex=\"3\"", StringComparison.Ordinal);
        FormDocumentLoadResult strictImport = FormXmlImporter.Load(
            Encoding.UTF8.GetBytes(tooMany),
            catalog,
            FormImportMode.Strict);
        Assert(
            !strictImport.IsValid && strictImport.Diagnostics.Any(item => item.Code == "LVFD1133"),
            "Strict XML import accepted a selected tab beyond the titles.");
    }

    public static void SessionBuildsPagesAndHidesUnselectedOnes(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        FormDocument form = CreateForm();
        DesignerDocumentSession session = new(form, catalog);

        FormEditResult addedView = session.AddWidget("lithe.widget.tab-view", RootId);
        Assert(addedView.IsValid && addedView.Changed, Messages("A tab view could not be added", addedView.Diagnostics));
        FormComponent newView = session.Document.Components[^1];
        Assert(
            FormTabRules.Titles(newView.Properties) == FormTabRules.StarterTitles,
            "A new tab view did not start with valid titles.");
        Assert(
            addedView.Diagnostics.Any(item => item.Code == "LVFD1134"),
            "An empty new tab view should report its missing pages.");

        FormEditResult addedPage = session.AddWidget("lithe.widget.panel", newView.ComponentId);
        Assert(addedPage.IsValid && addedPage.Changed, Messages("A page could not be added", addedPage.Diagnostics));
        FormComponent page = session.Document.Components[^1];
        FormRectangle viewFrame = newView.Properties.OfType<FormRectangleValue>().Single().Value;
        FormRectangle pageFrame = page.Properties.OfType<FormRectangleValue>().Single().Value;
        Assert(
            pageFrame == new FormRectangle(0, (int)FormTabRules.DefaultTabHeight, viewFrame.Width,
                viewFrame.Height - (int)FormTabRules.DefaultTabHeight),
            "A new page did not fill the tab view's content area.");

        Assert(
            !session.AddWidget("lithe.widget.button", newView.ComponentId).IsValid,
            "A tab view accepted a non-container page.");

        HashSet<string> hidden = HiddenPages(session.Document, catalog);
        Assert(
            hidden.SetEquals([HomePageId]),
            "Only the unselected page of the fixture tab view should be hidden in the preview.");
        FormEditResult selectHome = session.SetProperty(TabViewId, new FormUnsignedIntegerValue("selected-index", 0U));
        Assert(selectHome.IsValid && selectHome.Changed, "Selecting another design-time page failed.");
        hidden = HiddenPages(session.Document, catalog);
        Assert(
            hidden.SetEquals([SetupPageId, ApplyId]),
            "Hiding a page must also hide the components inside it.");
        Assert(
            !session.SetProperty(TabViewId, new FormUnsignedIntegerValue("selected-index", 2U)).IsValid,
            "A selected index beyond the titles was accepted.");
    }

    private static HashSet<string> HiddenPages(FormDocument document, WidgetCatalog catalog)
    {
        return FormTabRules.HiddenPageComponentIds(
            document.Components,
            widgetId => catalog.Widgets.Any(widget => widget.Id == widgetId && FormTabRules.IsTabView(widget)));
    }

    private static FormDocument Replace(
        FormDocument form,
        string componentId,
        Func<FormPropertyValue[], FormPropertyValue[]> change)
    {
        return form with
        {
            Components = [.. form.Components.Select(item => item.ComponentId != componentId
                ? item
                : item with { Properties = change(item.Properties) })]
        };
    }

    private static FormDocument CreateForm()
    {
        return new FormDocument
        {
            SchemaVersion = 1,
            Id = "form.tabs",
            DisplayName = "Tabs",
            SurfaceProfileId = "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565",
            Components =
            [
                Component(RootId, "rootPanel", "lithe.widget.panel", null, 0, new FormRectangle(0, 0, 480, 272)),
                Component(TabViewId, "settingsTabs", "lithe.widget.tab-view", RootId, 0,
                    new FormRectangle(8, 8, 464, 200),
                    new FormUnsignedIntegerValue("runtime-id", 60U),
                    new FormTextValue("items", "HOME|SETUP"),
                    new FormUnsignedIntegerValue("selected-index", 1U),
                    new FormUnsignedIntegerValue("tab-height", 28U)),
                Component(HomePageId, "homePage", "lithe.widget.panel", TabViewId, 0,
                    new FormRectangle(0, 28, 464, 172)),
                Component(SetupPageId, "setupPage", "lithe.widget.panel", TabViewId, 1,
                    new FormRectangle(0, 28, 464, 172)),
                Component(ApplyId, "applyButton", "lithe.widget.button", SetupPageId, 0,
                    new FormRectangle(8, 8, 120, 36),
                    new FormTextValue("text", "APPLY")),
                Component(TabBarId, "modeBar", "lithe.widget.tab-bar", RootId, 1,
                    new FormRectangle(8, 216, 464, 32),
                    new FormUnsignedIntegerValue("runtime-id", 61U),
                    new FormTextValue("items", "A|B|C"),
                    new FormBooleanValue("wrap", false))
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
