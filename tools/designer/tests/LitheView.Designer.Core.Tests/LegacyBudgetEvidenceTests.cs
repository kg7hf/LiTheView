// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class LegacyBudgetEvidenceTests
{
    public static void Run(string repositoryRoot)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(
            Path.Combine(repositoryRoot, "schema", "widgets.json"));
        TargetProfileLoadResult target = TargetProfileLoader.LoadFile(
            Path.Combine(repositoryRoot, "profiles", "targets", "stm32u545-standard-rgb565-tiled.json"));
        Require(catalog.IsValid && target.IsValid, "Budget test prerequisites must load.");
        CheckSourceIsNotArchive(catalog.Catalog!, target.Profile!);
        CheckViewDepthIsNotControllerDepth(catalog.Catalog!, target.Profile!);
        CheckFocusDefaults(catalog.Catalog!, target.Profile!);
        CheckPartialMemoryEvidence(catalog.Catalog!, target.Profile!);
    }

    private static void CheckSourceIsNotArchive(WidgetCatalog catalog, TargetProfile template)
    {
        ResolvedFormDocument document = Resolve(catalog, [new("panel", null)]);
        FormCppCompilationResult generated = FormCppCompiler.Compile(document.Source, catalog);
        Require(generated.IsValid, "The source-size fixture must compile to C++.");
        long sourceBytes = checked(generated.HeaderUtf8!.LongLength + generated.IntegrationUtf8!.LongLength);
        TargetProfile target = template with
        {
            Budgets = template.Budgets with
            {
                ArchiveText = new()
                {
                    MaximumBytes = 1, Enforcement = BudgetEnforcement.ArchiveGate,
                    Source = "Independent test archive ceiling"
                }
            }
        };
        FormBudgetRequest request = new()
        {
            Target = target, RequiredRenderWorkspaceBytes = 0, GeneratedSourceBytes = sourceBytes
        };
        FormBudgetReport sourceOnly = FormBudgetAnalyzer.Analyze(document, request);
        FormBudgetUse archive = Use(sourceOnly, "generated-archive");
        Require(sourceBytes > 1 && sourceOnly.IsValid && sourceOnly.HasUnknownUses,
            "Source bytes above an archive ceiling must not become a false code-size failure or pass.");
        Require(archive.Used is null && archive.Maximum == 1 && !archive.IsWithinLimit,
            "The unmeasured archive constraint must retain its ceiling and remain unevaluated.");
        Require(Use(sourceOnly, "generated-source").Used == sourceBytes &&
                Use(sourceOnly, "generated-source").Maximum is null,
            "Exact generated source bytes are informational and have no compiled archive ceiling.");

        FormBudgetReport measured = FormBudgetAnalyzer.Analyze(document, request with { GeneratedArchiveBytes = 1 });
        Require(measured.IsValid && Use(measured, "generated-archive").IsWithinLimit,
            "An explicit compiled archive measurement at its ceiling must remain accepted.");
        FormBudgetReport oversized = FormBudgetAnalyzer.Analyze(document, request with { GeneratedArchiveBytes = 2 });
        Require(!oversized.IsValid && oversized.Diagnostics.Count(item => item.Code == "LVFB1401") == 1,
            "An explicit compiled archive overrun must still fail independently of source bytes.");
    }

    private static void CheckViewDepthIsNotControllerDepth(WidgetCatalog catalog, TargetProfile target)
    {
        NodeSpec[] nodes = new NodeSpec[target.Capacities.MaxControllerDepth + 1];
        for (int index = 0; index < nodes.Length; ++index)
        {
            nodes[index] = new("panel", index == 0 ? null : index - 1);
        }
        ResolvedFormDocument document = Resolve(catalog, nodes);
        FormBudgetRequest request = new() { Target = target, RequiredRenderWorkspaceBytes = 0 };
        FormBudgetReport unknown = FormBudgetAnalyzer.Analyze(document, request);
        Require(unknown.IsValid && Use(unknown, "view-depth").Used == nodes.Length &&
                Use(unknown, "view-depth").Maximum is null,
            "A deep view tree must remain informational rather than consume controller stack entries.");
        Require(Use(unknown, "controller-depth").Used is null &&
                !Use(unknown, "controller-depth").IsWithinLimit,
            "Missing controller-stack evidence cannot be replaced by view-tree depth.");
        FormBudgetReport oneController = FormBudgetAnalyzer.Analyze(document,
            request with { RequiredControllerDepth = 1 });
        Require(oneController.IsValid && Use(oneController, "controller-depth").Used == 1,
            "A deeply nested form can be owned by a single controller.");
        FormBudgetReport tooManyControllers = FormBudgetAnalyzer.Analyze(document,
            request with { RequiredControllerDepth = nodes.Length });
        Require(!tooManyControllers.IsValid &&
                tooManyControllers.Diagnostics.Count(item => item.Code == "LVFB1102") == 1,
            "Only the independently supplied controller count must enforce the controller ceiling.");
    }

    private static void CheckFocusDefaults(WidgetCatalog catalog, TargetProfile target)
    {
        FormResource frame = new()
        {
            Id = "resource.frame", RuntimeName = "frame", Symbol = "Frame", Kind = FormResourceKind.Bitmap
        };
        ResolvedFormDocument outputOnly = Resolve(catalog,
        [
            new("panel", null),
            new("animated-image", 0,
                [new FormReferenceListValue("animation-frames", FormReferenceKind.Bitmap, [frame.Id])]),
            new("scroll", 0)
        ], resources: [frame]);
        Require(outputOnly.Components[1].Widget.InputCapabilities.Length > 0,
            "The animation fixture must retain its elapsed-time capability.");
        FormBudgetRequest request = new() { Target = target, RequiredRenderWorkspaceBytes = 0 };
        Require(Use(FormBudgetAnalyzer.Analyze(outputOnly, request), "focusable-views").Used == 0,
            "Animation and scroll handlers must not imply default keyboard focusability.");
        ResolvedFormDocument overrides = Resolve(catalog,
        [
            new("panel", null), new("button", 0),
            new("button", 0, [new FormTextValue("focus-policy", "false")]),
            new("label", 0, [new FormTextValue("focus-policy", "true")])
        ]);
        Require(Use(FormBudgetAnalyzer.Analyze(overrides, request), "focusable-views").Used == 2,
            "Runtime focus defaults and explicit true/false overrides must be counted independently.");
    }

    private static void CheckPartialMemoryEvidence(WidgetCatalog catalog, TargetProfile template)
    {
        TargetProfile target = template with
        {
            Budgets = template.Budgets with
            {
                InternalRam = new()
                {
                    MaximumBytes = 1024, Enforcement = BudgetEnforcement.LinkerRegion,
                    Source = "Independent test complete RAM ceiling"
                }
            }
        };
        FormBinding known = new()
        {
            Id = "binding.known", RuntimeName = "known", Symbol = "Known",
            Kind = FormBindingKind.MutableText, CapacityBytes = 64
        };
        FormBinding unknown = known with
        {
            Id = "binding.unknown", RuntimeName = "unknown", Symbol = "Unknown", CapacityBytes = null
        };
        FormBudgetRequest request = new() { Target = target, RequiredRenderWorkspaceBytes = 0 };
        FormBudgetReport knownText = FormBudgetAnalyzer.Analyze(
            Resolve(catalog, [new("panel", null)], bindings: [known]), request);
        Require(Use(knownText, "mutable-text").Used == 64 && Use(knownText, "mutable-text").Maximum is null,
            "A known mutable-text subtotal must be identified as a component, not complete RAM.");
        FormBudgetUse ram = Use(knownText, "internal-ram");
        Require(knownText.IsValid && knownText.HasUnknownUses && ram.Used is null &&
                ram.Maximum == 1024 && !ram.IsWithinLimit,
            "A small text buffer cannot establish that total target RAM fits.");
        FormBudgetReport incompleteText = FormBudgetAnalyzer.Analyze(
            Resolve(catalog, [new("panel", null)], bindings: [known, unknown]), request);
        Require(Use(incompleteText, "mutable-text").Used is null &&
                Use(incompleteText, "mutable-text-known-minimum").Used == 64 &&
                incompleteText.Diagnostics.Any(item => item.Code == "LVFB1301"),
            "Unknown application bindings must not be represented as zero bytes in a complete text total.");
    }

    private readonly record struct NodeSpec(string Kind, int? Parent, FormPropertyValue[]? Properties = null);

    private static ResolvedFormDocument Resolve(
        WidgetCatalog catalog, NodeSpec[] nodes, FormBinding[]? bindings = null, FormResource[]? resources = null)
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
                Properties = [new FormRectangleValue("frame", new(0, 0, 100, 100)), .. node.Properties ?? []],
                HistoricalAttributes = []
            };
        }
        FormDocument source = new()
        {
            SchemaVersion = 1, Id = "form.legacy-budget-tests", DisplayName = "Legacy Budget Tests",
            SurfaceProfileId = null, Components = components, Actions = [],
            Bindings = bindings ?? [], Resources = resources ?? [], ThemeColors = []
        };
        FormResolutionResult result = FormDocumentResolver.Resolve(source, catalog);
        Require(result.IsValid, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        return result.Document!;
    }

    private static FormBudgetUse Use(FormBudgetReport report, string name) =>
        report.Uses.Single(item => item.Name == name);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
