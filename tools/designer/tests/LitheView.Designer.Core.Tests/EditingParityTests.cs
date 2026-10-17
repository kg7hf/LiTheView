// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class EditingParityTests
{
    private const string RootId = "component.edit.root";
    private const string LabelId = "component.edit.label";
    private const string PanelId = "component.edit.panel";
    private const string ButtonId = "component.edit.button";

    public static void ReparentIntoLaterContainerKeepsDocumentOrder(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        DesignerDocumentSession session = new(CreateForm(), catalog);

        FormEditResult moved = session.Reparent(LabelId, PanelId);
        Assert(moved.IsValid && moved.Changed, Messages("Reparenting into a later container failed", moved.Diagnostics));
        string[] order = [.. session.Document.Components.Select(item => item.ComponentId)];
        Assert(
            order.SequenceEqual([RootId, PanelId, LabelId, ButtonId]),
            "A reparented component must follow its new parent in document order.");
        FormRectangle frame = Frame(session.Document, LabelId);
        Assert(frame == new FormRectangle(4, 6, 100, 20), "Reparenting did not keep the label's display position.");
        Assert(
            session.Document.Components.Single(item => item.ComponentId == ButtonId).SiblingOrder == 1,
            "The old parent's remaining children must be renumbered.");

        FormEditResult front = session.ChangeZOrder(PanelId, FormZOrderOperation.BringToFront);
        Assert(front.IsValid && front.Changed, "Bring to front failed.");
        Assert(
            session.Document.Components.Select(item => item.ComponentId)
                .SequenceEqual([RootId, ButtonId, PanelId, LabelId]),
            "Document order must follow sibling order after a z-order change.");
    }

    public static void ThemeTokensRenameAndDeleteSafely(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        DesignerDocumentSession session = new(CreateForm(), catalog);

        Assert(session.SetThemeColor("theme.brand", 0x00df6c2dU).Changed, "Adding a theme color failed.");
        Assert(!session.SetThemeColor("theme.brand", 0x00df6c2dU).Changed, "An identical color must not add an undo step.");
        Assert(session.SetThemeColor("theme.brand", 0x000000ffU).Changed, "Changing a theme color failed.");
        Assert(session.Document.ThemeColors.Single().EncodedColor == 0x000000ffU, "The token kept its old color.");
        Assert(
            session.SetProperty(ButtonId, new FormColorValue("background-color", "theme.brand")).IsValid,
            "A component could not use the token.");

        FormEditResult renamed = session.RenameThemeColor("theme.brand", "theme.primary");
        Assert(renamed.IsValid && renamed.Changed, Messages("Renaming a token failed", renamed.Diagnostics));
        Assert(
            session.Document.Components.Single(item => item.ComponentId == ButtonId).Properties
                .OfType<FormColorValue>().Single().ThemeTokenId == "theme.primary",
            "Renaming a token did not rewrite its reference.");
        Assert(
            session.RenameThemeColor("theme.primary", "primary").Diagnostics.Any(item => item.Code == "LVFD1204"),
            "A token ID outside theme.* was accepted.");
        Assert(
            session.SetThemeColor("theme.other", 0x00102030U).Changed &&
            session.RenameThemeColor("theme.primary", "theme.other").Diagnostics.Any(item => item.Code == "LVED1803"),
            "Renaming onto an existing token was accepted.");

        FormEditResult inUse = session.RemoveThemeColor("theme.primary");
        Assert(
            !inUse.IsValid && inUse.Diagnostics.Any(item => item.Code == "LVED1802"),
            "A token in use was deleted.");
        Assert(session.ResetProperty(ButtonId, "background-color").IsValid, "Resetting the color failed.");
        FormEditResult removed = session.RemoveThemeColor("theme.primary");
        Assert(
            removed.IsValid && session.Document.ThemeColors.All(item => item.Id != "theme.primary"),
            "An unused token could not be deleted.");
        Assert(
            session.RemoveThemeColor("theme.missing").Diagnostics.Any(item => item.Code == "LVED1801"),
            "Deleting a missing token was not reported.");
        Assert(session.Undo() && session.Document.ThemeColors.Any(item => item.Id == "theme.primary"),
            "Deleting a token was not undoable.");
    }

    private static FormRectangle Frame(FormDocument document, string componentId)
    {
        return document.Components.Single(item => item.ComponentId == componentId)
            .Properties.OfType<FormRectangleValue>().Single().Value;
    }

    private static FormDocument CreateForm()
    {
        return new FormDocument
        {
            SchemaVersion = 1,
            Id = "form.edit",
            DisplayName = "Editing",
            SurfaceProfileId = null,
            Components =
            [
                Component(RootId, "rootPanel", "lithe.widget.panel", null, 0, new(0, 0, 480, 272)),
                Component(LabelId, "titleLabel", "lithe.widget.label", RootId, 0, new(44, 66, 100, 20)),
                Component(PanelId, "groupPanel", "lithe.widget.panel", RootId, 1, new(40, 60, 200, 120)),
                Component(ButtonId, "okButton", "lithe.widget.button", RootId, 2, new(300, 200, 100, 36))
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
        FormRectangle frame)
    {
        return new FormComponent
        {
            ComponentId = id,
            Name = name,
            WidgetId = widgetId,
            ParentComponentId = parentId,
            SiblingOrder = siblingOrder,
            Properties = [new FormRectangleValue("frame", frame)],
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
