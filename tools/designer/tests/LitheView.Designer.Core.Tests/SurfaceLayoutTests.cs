// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class SurfaceLayoutTests
{
    public static void Run(string repositoryRoot)
    {
        DisplayDefinitionLoadResult definition = DisplayDefinitionProfileLoader.LoadFile(
            Path.Combine(repositoryRoot, "profiles", "display-definitions", "round-240-rgb565.json"));
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(
            Path.Combine(repositoryRoot, "profiles", "logical-inputs", "output-only.json"));
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(
            Path.Combine(repositoryRoot, "schema", "widgets.json"));
        Require(definition.IsValid && input.IsValid && catalog.IsValid, "Surface test prerequisites must load.");
        string fixtures = Path.Combine(repositoryRoot, "tests", "fixtures", "display-model");
        CheckPlacementCases(fixtures, definition.Profile!);
        CheckInputCases(fixtures, input.Configuration!);
        CheckHierarchyCases(fixtures, definition.Profile!);
        CheckPopup(definition.Profile!);
        CheckFormAdapter(definition.Profile!, input.Configuration!, catalog.Catalog!);
    }

    private static void CheckPlacementCases(string fixtureRoot, DisplayDefinitionProfile template)
    {
        string[][] rows = ReadRows(Path.Combine(fixtureRoot, "surface-placement.csv"), 14);
        Require(rows.Length == 26, "The common placement fixture must retain all 26 cases.");
        foreach (string[] row in rows)
        {
            DisplayDefinitionProfile definition = template with
            {
                Geometry = template.Geometry with
                {
                    Shape = Enum.Parse<SurfaceShape>(row[1], ignoreCase: true),
                    Width = Number(row[2]), Height = Number(row[3]),
                    SafeArea = new()
                    {
                        X = Number(row[4]), Y = Number(row[5]),
                        Width = Number(row[6]), Height = Number(row[7])
                    }
                }
            };
            ProfileDiagnostic? result = FormSurfaceValidator.ValidatePlacement(definition,
                new(Number(row[8]), Number(row[9]), Number(row[10]), Number(row[11])), Placement(row[12]));
            Require((result?.Code ?? "OK") == row[13], $"Placement fixture '{row[0]}' failed: {result?.Code}.");
        }
    }

    private static void CheckInputCases(string fixtureRoot, LogicalInputConfigurationProfile template)
    {
        string[][] rows = ReadRows(Path.Combine(fixtureRoot, "logical-input.csv"), 10);
        Require(rows.Length == 20, "The common input fixture must retain all 20 cases.");
        foreach (string[] row in rows)
        {
            LogicalInputConfigurationProfile input = template with
            {
                Pointer = Flag(row[1]), Keys = Flag(row[2]), Text = Flag(row[3]),
                Encoder = Flag(row[4]), Select = Flag(row[5])
            };
            SurfaceInputRequirements requirements = new()
            {
                Activation = Flag(row[6]), Navigation = Flag(row[7]), Text = Flag(row[8])
            };
            ProfileDiagnostic? result = FormSurfaceValidator.ValidateInputRequirements(input, requirements);
            Require((result?.Code ?? "OK") == row[9], $"Input fixture '{row[0]}' failed: {result?.Code}.");
        }
    }

    private static void CheckHierarchyCases(string fixtureRoot, DisplayDefinitionProfile definition)
    {
        string[][] rows = ReadRows(Path.Combine(fixtureRoot, "surface-hierarchy.csv"), 12);
        IGrouping<string, string[]>[] groups = [.. rows.GroupBy(row => row[0], StringComparer.Ordinal)];
        Require(groups.Length == 21 && rows.Length == 54, "The common hierarchy fixture must retain all 21 cases.");
        foreach (IGrouping<string, string[]> group in groups)
        {
            string[][] entries = [.. group];
            SurfaceLayoutNode[] nodes = new SurfaceLayoutNode[entries.Length];
            for (int index = 0; index < entries.Length; ++index)
            {
                string[] row = entries[index];
                Require(Number(row[1]) == index && row[10] == entries[0][10] && row[11] == entries[0][11],
                    $"Hierarchy fixture '{group.Key}' must have stable order and expectations.");
                int parent = Number(row[2]);
                nodes[index] = new(parent < 0 ? null : parent,
                    new(Number(row[3]), Number(row[4]), Number(row[5]), Number(row[6])),
                    Placement(row[7]), Flag(row[8]), Number(row[9]));
            }
            ProfileDiagnostic? result = FormSurfaceValidator.ValidateLayout(definition, nodes);
            Require((result?.Code ?? "OK") == entries[0][10],
                $"Hierarchy fixture '{group.Key}' failed: {result?.Code}.");
            if (result is not null)
            {
                Require(result.Path == $"$.components[{Number(entries[0][11])}].frame",
                    $"Hierarchy fixture '{group.Key}' identified the wrong node.");
            }
        }
        Require(FormSurfaceValidator.ValidateLayout(definition, [])?.Code == "LVSL3007",
            "An empty hierarchy must report its structural failure.");
    }

    private static void CheckPopup(DisplayDefinitionProfile definition)
    {
        FormRectangle anchored = new(20, 74, 200, 146);
        Require(FormSurfaceValidator.PlacePopup(definition, anchored).Diagnostic?.Code == "LVSL3012",
            "An unusable popup cannot silently move without an explicit fallback.");
        Require(FormSurfaceValidator.PlacePopup(definition, anchored,
                SurfacePopupFallback.CenteredSafeArea).Diagnostic?.Code == "LVSL3012",
            "Centering cannot silently shrink a popup that is too wide.");
        SurfacePopupPlacementResult fitted = FormSurfaceValidator.PlacePopup(definition, anchored,
            SurfacePopupFallback.CenteredSafeArea, new(168, 146));
        Require(fitted.IsValid && fitted.UsedFallback && fitted.Frame == new FormRectangle(36, 47, 168, 146),
            "The declared round overlay fallback must preserve its exact bounded size and centered location.");
        FormRectangle alreadyFits = new(40, 50, 120, 80);
        SurfacePopupPlacementResult retained = FormSurfaceValidator.PlacePopup(definition, alreadyFits,
            SurfacePopupFallback.CenteredSafeArea, new(100, 60));
        Require(retained.IsValid && !retained.UsedFallback && retained.Frame == alreadyFits,
            "A fitting anchored popup must be retained unchanged.");
        Require(FormSurfaceValidator.PlacePopup(definition, anchored, SurfacePopupFallback.None,
                new(168, 146)).Diagnostic?.Code == "LVSL3002",
            "A fallback size without a fallback policy is invalid.");
        Require(FormSurfaceValidator.PlacePopup(definition, anchored,
                Enum.Parse<SurfacePopupFallback>("255")).Diagnostic?.Code == "LVSL3002",
            "Unknown popup policies must fail closed.");
        Require(FormSurfaceValidator.PlacePopup(definition, alreadyFits,
                SurfacePopupFallback.CenteredSafeArea, new(0, 146)).Diagnostic?.Code == "LVSL3003",
            "Invalid fallback geometry fails even when the anchor fits.");
        Require(FormSurfaceValidator.PlacePopup(definition, new(int.MaxValue, 0, 1, 1),
                SurfacePopupFallback.CenteredSafeArea).Diagnostic?.Code == "LVSL3003",
            "Overflowing popup geometry must be diagnosed before placement.");
    }

    private static void CheckFormAdapter(
        DisplayDefinitionProfile definition,
        LogicalInputConfigurationProfile outputOnly,
        WidgetCatalog catalog)
    {
        NodeSpec root = new("panel", null, new(0, 0, 240, 240));
        ResolvedFormDocument output = Resolve(definition, catalog, root,
            new("label", 0, new(20, 80, 20, 20)));
        Require(FormSurfaceValidator.Validate(output, definition, outputOnly).IsValid,
            "A clipped decorative form must validate without any logical inputs or target.");

        ResolvedFormDocument outside = Resolve(definition, catalog, root,
            new("button", 0, new(20, 80, 20, 20)));
        Require(HasCode(FormSurfaceValidator.Validate(outside, definition, outputOnly), "LVSL3004"),
            "Ordinary controls default to the safe area.");
        FormSurfacePolicy visibleOverride = new()
        {
            Components = [new() { ComponentId = "component.node-1", Placement = SurfacePlacementPolicy.VisibleShape }]
        };
        Require(FormSurfaceValidator.Validate(outside, definition, outputOnly, visibleOverride).IsValid,
            "An explicit visible-shape policy may place a fully visible control outside the conservative safe area.");
        foreach (FormPropertyValue state in new FormPropertyValue[]
        {
            new FormBooleanValue("visible", false), new FormBooleanValue("enabled", false),
            new FormTextValue("focus-policy", "false")
        })
        {
            ResolvedFormDocument mutableState = Resolve(definition, catalog, root,
                new("button", 0, new(20, 80, 20, 20), [state]));
            Require(HasCode(FormSurfaceValidator.Validate(mutableState, definition, outputOnly), "LVSL3004"),
                "Mutable visibility, enabled state, or focus override must not hide invalid control geometry.");
        }
        ResolvedFormDocument focusableLabel = Resolve(definition, catalog, root,
            new("label", 0, new(20, 80, 20, 20), [new FormTextValue("focus-policy", "true")]));
        Require(HasCode(FormSurfaceValidator.Validate(focusableLabel, definition, outputOnly), "LVSL3004"),
            "An explicitly focusable decorative widget receives the control placement policy.");

        ResolvedFormDocument controls = Resolve(definition, catalog, root,
            new("button", 0, new(40, 40, 60, 30)), new("button", 0, new(110, 40, 60, 30)));
        Require(FormSurfaceValidator.Validate(controls, definition, outputOnly).IsValid,
            "Catalog input handlers must not become jointly mandatory device requirements.");
        FormSurfacePolicy operations = new()
        {
            InputRequirements = new() { Activation = true, Navigation = true }
        };
        Require(HasCode(FormSurfaceValidator.Validate(controls, definition, outputOnly, operations), "LVSL3009"),
            "Missing explicitly declared activation must fail the surface stage.");
        LogicalInputConfigurationProfile selectOnly = outputOnly with { Select = true };
        Require(HasCode(FormSurfaceValidator.Validate(controls, definition, selectOnly, operations), "LVSL3010"),
            "Select alone must not satisfy a declared navigation requirement.");
        LogicalInputConfigurationProfile encoder = selectOnly with { Encoder = true };
        Require(FormSurfaceValidator.Validate(controls, definition, encoder, operations).IsValid,
            "Encoder/select can supply the declared operations without keys or a pointer.");
        Require(HasCode(FormSurfaceValidator.Validate(controls, definition, encoder,
                operations with { InputRequirements = operations.InputRequirements with { Text = true } }), "LVSL3011"),
            "Available memory and navigation cannot replace missing logical text input.");

        ResolvedFormDocument scroll = Resolve(definition, catalog, root,
            new("scroll", 0, new(40, 40, 160, 100)),
            new("button", 1, new(10, 240, 80, 30)));
        Require(FormSurfaceValidator.Validate(scroll, definition, encoder, operations).IsValid,
            "A control reachable after fitted-content scrolling must not fail for initial clipping.");
        FormXmlWriteResult xml = FormXmlWriter.Write(scroll);
        Require(xml.IsValid, "The existing XML writer must retain the scroll form.");
        FormDocumentLoadResult imported = FormXmlImporter.Load(xml.Utf8!, catalog, FormImportMode.Strict);
        Require(imported.IsValid, "The existing strict XML importer must accept the retained form.");
        FormResolutionResult roundTrip = FormDocumentResolver.Resolve(imported.Document!, catalog);
        Require(roundTrip.IsValid && FormSurfaceValidator.Validate(roundTrip.Document!, definition, encoder, operations).IsValid,
            "XML and handwritten host form construction must use the same surface decisions.");

        FormSurfacePolicy badReference = new()
        {
            Components = [new() { ComponentId = "missing.component", Placement = SurfacePlacementPolicy.SafeArea }]
        };
        Require(HasCode(FormSurfaceValidator.Validate(controls, definition, outputOnly, badReference), "LVSL3007"),
            "Unknown sidecar references must not be silently ignored.");
        Require(HasCode(FormSurfaceValidator.Validate(controls, definition, outputOnly,
                visibleOverride with { Components = [visibleOverride.Components[0], visibleOverride.Components[0]] }), "LVSL3007"),
            "Duplicate placement overrides must fail explicitly.");
        Require(HasCode(FormSurfaceValidator.Validate(controls with
                { Source = controls.Source with { SurfaceProfileId = "wrong.surface" } }, definition, outputOnly), "LVFS1001"),
            "The existing surface identity diagnostic must remain stable.");
        ResolvedFormDocument wrongRoot = Resolve(definition, catalog, new NodeSpec("panel", null, new(0, 0, 200, 200)));
        Require(HasCode(FormSurfaceValidator.Validate(wrongRoot, definition, outputOnly), "LVFS1002"),
            "The existing exact root extent diagnostic must remain stable.");
    }

    private readonly record struct NodeSpec(
        string Kind, int? Parent, FormRectangle Frame, FormPropertyValue[]? Properties = null);

    private static ResolvedFormDocument Resolve(
        DisplayDefinitionProfile definition, WidgetCatalog catalog, params NodeSpec[] nodes)
    {
        FormComponent[] components = new FormComponent[nodes.Length];
        for (int index = 0; index < nodes.Length; ++index)
        {
            NodeSpec node = nodes[index];
            components[index] = new()
            {
                ComponentId = $"component.node-{index}", Name = $"Node{index}",
                WidgetId = $"lithe.widget.{node.Kind}",
                ParentComponentId = node.Parent.HasValue ? $"component.node-{node.Parent.Value}" : null,
                SiblingOrder = nodes.Take(index).Count(previous => previous.Parent == node.Parent),
                Properties = [new FormRectangleValue("frame", node.Frame), .. node.Properties ?? []],
                HistoricalAttributes = []
            };
        }
        FormDocument source = new()
        {
            SchemaVersion = 1, Id = "form.surface-tests", DisplayName = "Surface Tests",
            SurfaceProfileId = definition.Id, Components = components,
            Actions = [], Bindings = [], Resources = [], ThemeColors = []
        };
        FormResolutionResult result = FormDocumentResolver.Resolve(source, catalog);
        Require(result.IsValid, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        return result.Document!;
    }

    private static bool HasCode(FormSurfaceValidationResult result, string code) =>
        !result.IsValid && result.Diagnostics.Any(item => item.Code == code);

    private static string[][] ReadRows(string path, int fields)
    {
        string[][] rows = [.. File.ReadLines(path).Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split(',', StringSplitOptions.TrimEntries))];
        Require(rows.All(row => row.Length == fields), $"Malformed common fixture: {path}.");
        return rows;
    }

    private static SurfacePlacementPolicy Placement(string value) => value switch
    {
        "safeArea" => SurfacePlacementPolicy.SafeArea,
        "visibleShape" => SurfacePlacementPolicy.VisibleShape,
        "clipToViewport" => SurfacePlacementPolicy.ClipToViewport,
        "invalid" => Enum.Parse<SurfacePlacementPolicy>("255"),
        _ => throw new InvalidOperationException($"Unknown fixture placement: {value}.")
    };

    private static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static bool Flag(string value) => value switch
    {
        "0" => false, "1" => true,
        _ => throw new InvalidOperationException($"Invalid fixture flag: {value}.")
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
