// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class ButtonVariantTests
{
    private const string ToggleId = "component.buttons.alarm";
    private const string IconId = "component.buttons.home";
    private const uint SelectedSurface = 0x00fae4d6U;

    public static void VariantsReachXmlAndCpp(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        FormDocument form = CreateForm();

        FormXmlWriteResult xml = FormXmlWriter.Write(form, catalog);
        Assert(xml.IsValid && xml.Utf8 is not null, Messages("Button variants did not write XML", xml.Diagnostics));
        string text = Encoding.UTF8.GetString(xml.Utf8!);
        Assert(
            text.Contains("behavior=\"toggle\"", StringComparison.Ordinal) &&
            text.Contains("checked=\"true\"", StringComparison.Ordinal) &&
            text.Contains("icon=\"home-icon\"", StringComparison.Ordinal) &&
            text.Contains("selectedBackgroundColor=\"0x00fae4d6\"", StringComparison.Ordinal),
            "Canonical XML lost toggle behavior, latched state, icon, or selected color.");
        Assert(
            text.Contains(
                "propertyId=\"selected-background-color\" tokenId=\"theme.selected\"",
                StringComparison.Ordinal),
            "Canonical XML did not bind the selected-state token reference.");

        FormDocumentLoadResult imported = FormXmlImporter.Load(xml.Utf8!, catalog, FormImportMode.Strict);
        Assert(imported.IsValid, Messages("Strict import rejected button variants", imported.Diagnostics));
        Assert(
            imported.CanonicalXmlUtf8!.AsSpan().SequenceEqual(xml.Utf8),
            "Button variant XML did not reach a canonical fixed point.");
        FormComponent toggle = imported.Document!.Components.Single(item => item.ComponentId == ToggleId);
        Assert(
            toggle.Properties.OfType<FormColorValue>().Any(item =>
                item.PropertyId == "selected-background-color" && item.ThemeTokenId == "theme.selected"),
            "Reopening the form flattened the selected-state token into a literal.");

        FormCppCompilationResult cpp = FormCppCompiler.Compile(form, catalog);
        Assert(cpp.HeaderUtf8 is not null, Messages("Button variants did not compile to C++", cpp.Diagnostics));
        string header = Encoding.UTF8.GetString(cpp.HeaderUtf8!);
        Assert(
            header.Contains("spec.buttonBehavior = ButtonBehavior::toggle;", StringComparison.Ordinal) &&
            header.Contains("spec.buttonBehavior = ButtonBehavior::momentary;", StringComparison.Ordinal) &&
            header.Contains("spec.checked = true;", StringComparison.Ordinal) &&
            header.Contains("spec.resource = \"home-icon\";", StringComparison.Ordinal),
            "Generated C++ lost a button behavior, latched state, or icon resource.");
        Assert(
            header.Contains(
                $"state.selectedBackground = Color::fromLegacyValue(0x{SelectedSurface:x8}U);",
                StringComparison.Ordinal) &&
            header.Contains("spec.style.states = &kStateStyles[1U];", StringComparison.Ordinal),
            "Generated C++ did not materialize the selected-state token into the borrowed state table.");
        Assert(
            header.Contains("            result[1U] = state;", StringComparison.Ordinal) &&
            !header.Contains("WidgetStateStyle& state", StringComparison.Ordinal),
            "Sparse state-style entries must be built locally and copied, not written through a reference.");
    }

    public static void MomentaryButtonsCannotLatch(string catalogPath)
    {
        WidgetCatalog catalog = LoadCatalog(catalogPath);
        FormDocument form = CreateForm();

        foreach (FormPropertyValue[] behavior in new[]
                 {
                     new FormPropertyValue[] { new FormTextValue("button-behavior", "momentary") },
                     []
                 })
        {
            FormDocument invalid = form with
            {
                Components = [.. form.Components.Select(item => item.ComponentId != IconId
                    ? item
                    : item with
                    {
                        Properties =
                        [
                            .. item.Properties.Where(value => value.PropertyId != "button-behavior"),
                            .. behavior,
                            new FormBooleanValue("checked", true)
                        ]
                    })]
            };
            FormResolutionResult resolution = FormDocumentResolver.Resolve(invalid, catalog);
            Assert(
                !resolution.IsValid && resolution.Diagnostics.Any(item => item.Code == "LVFD1132"),
                "A checked momentary button was not rejected.");
        }

        DesignerDocumentSession session = new(form, catalog);
        FormEditResult rejected = session.SetProperty(IconId, new FormBooleanValue("checked", true));
        Assert(
            !rejected.IsValid && !rejected.Changed &&
            rejected.Diagnostics.Any(item => item.Code == "LVFD1132"),
            "The session accepted a checked momentary button.");

        FormEditResult momentary = session.SetProperty(
            ToggleId,
            new FormTextValue("button-behavior", "momentary"));
        Assert(momentary.IsValid && momentary.Changed, "A checked toggle could not become momentary.");
        FormComponent changed = session.Document.Components.Single(item => item.ComponentId == ToggleId);
        Assert(
            !changed.Properties.Any(item => item.PropertyId == "checked"),
            "Changing to momentary did not clear the latched value in the same edit.");
        Assert(session.Undo(), "The behavior change was not undoable.");
        FormComponent restored = session.Document.Components.Single(item => item.ComponentId == ToggleId);
        Assert(
            restored.Properties.OfType<FormBooleanValue>().Any(item => item.PropertyId == "checked" && item.Value) &&
            restored.Properties.OfType<FormTextValue>().Any(item =>
                item.PropertyId == "button-behavior" && item.Value == "toggle"),
            "Undo did not restore behavior and latched value as one transaction.");

        FormEditResult reset = session.ResetProperty(ToggleId, "button-behavior");
        Assert(
            reset.IsValid && reset.Changed &&
            !session.Document.Components.Single(item => item.ComponentId == ToggleId)
                .Properties.Any(item => item.PropertyId == "checked"),
            "Resetting behavior to its momentary default did not clear the latched value.");

        string checkedMomentary = Encoding.UTF8.GetString(FormXmlWriter.Write(form, catalog).Utf8!)
            .Replace("<button frame=\"8,52,48,48\"", "<button checked=\"true\" frame=\"8,52,48,48\"", StringComparison.Ordinal);
        FormDocumentLoadResult strictImport = FormXmlImporter.Load(
            Encoding.UTF8.GetBytes(checkedMomentary),
            catalog,
            FormImportMode.Strict);
        Assert(
            !strictImport.IsValid && strictImport.Diagnostics.Any(item => item.Code == "LVFD1132"),
            "Strict XML import accepted a checked momentary button.");
    }

    public static void PreviewInputScriptIsBounded()
    {
        string[] script =
        [
            "focus", "next", "previous", "select", "key:back", "press:10,-2", "move:10,40",
            "release:10,40", "cancel:0,0", "elapsed:16"
        ];
        Assert(
            PreviewInputScript.TryParse(script, out PreviewInput[] inputs, out string error),
            $"A valid preview input script was rejected: {error}");
        PreviewInput[] expected =
        [
            new(PreviewInputKind.Focus, 1),
            new(PreviewInputKind.Encoder, 1),
            new(PreviewInputKind.Encoder, -1),
            new(PreviewInputKind.PushButton, 1),
            new(PreviewInputKind.Key, (int)PreviewKey.Back),
            new(PreviewInputKind.PointerEvent, (int)PreviewPointerPhase.Pressed, 10, -2),
            new(PreviewInputKind.PointerEvent, (int)PreviewPointerPhase.Moved, 10, 40),
            new(PreviewInputKind.PointerEvent, (int)PreviewPointerPhase.Released, 10, 40),
            new(PreviewInputKind.PointerEvent, (int)PreviewPointerPhase.Cancelled, 0, 0),
            new(PreviewInputKind.ElapsedTime, 16)
        ];
        Assert(inputs.SequenceEqual(expected), "Preview input scripts map to the wrong worker records.");

        foreach (string invalid in new[] { "press:1", "key:enter", "elapsed:-1", "tap", "press:x,1", ":1" })
        {
            Assert(
                !PreviewInputScript.TryParse([invalid], out _, out _),
                $"Malformed preview input '{invalid}' was accepted.");
        }
        Assert(
            !PreviewInputScript.TryParse(
                [.. Enumerable.Repeat("select", PreviewLimits.MaximumInputs + 1)],
                out _,
                out _),
            "An oversized preview input script was accepted.");
    }

    private static FormDocument CreateForm()
    {
        return new FormDocument
        {
            SchemaVersion = 1,
            Id = "form.buttons",
            DisplayName = "Buttons",
            SurfaceProfileId = "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565",
            Components =
            [
                new FormComponent
                {
                    ComponentId = "component.buttons.root",
                    Name = "rootPanel",
                    WidgetId = "lithe.widget.panel",
                    ParentComponentId = null,
                    SiblingOrder = 0,
                    Properties = [new FormRectangleValue("frame", new FormRectangle(0, 0, 480, 272))],
                    HistoricalAttributes = []
                },
                new FormComponent
                {
                    ComponentId = ToggleId,
                    Name = "alarmToggle",
                    WidgetId = "lithe.widget.button",
                    ParentComponentId = "component.buttons.root",
                    SiblingOrder = 0,
                    Properties =
                    [
                        new FormRectangleValue("frame", new FormRectangle(8, 8, 144, 36)),
                        new FormUnsignedIntegerValue("runtime-id", 51U),
                        new FormTextValue("text", "ALARM"),
                        new FormTextValue("button-behavior", "toggle"),
                        new FormBooleanValue("checked", true),
                        new FormColorValue("selected-background-color", "theme.selected"),
                        new FormColorValue("selected-foreground-color", 0x00803d1aU)
                    ],
                    HistoricalAttributes = []
                },
                new FormComponent
                {
                    ComponentId = IconId,
                    Name = "homeButton",
                    WidgetId = "lithe.widget.button",
                    ParentComponentId = "component.buttons.root",
                    SiblingOrder = 1,
                    Properties =
                    [
                        new FormRectangleValue("frame", new FormRectangle(8, 52, 48, 48)),
                        new FormUnsignedIntegerValue("runtime-id", 52U),
                        new FormTextValue("button-behavior", "momentary"),
                        new FormReferenceValue("icon", FormReferenceKind.Bitmap, "resource.bitmap.home-icon")
                    ],
                    HistoricalAttributes = []
                }
            ],
            Actions = [],
            Bindings = [],
            Resources =
            [
                new FormResource
                {
                    Id = "resource.bitmap.home-icon",
                    RuntimeName = "home-icon",
                    Symbol = "HomeIcon",
                    Kind = FormResourceKind.Bitmap
                }
            ],
            ThemeColors = [new FormThemeColor { Id = "theme.selected", EncodedColor = SelectedSurface }]
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
