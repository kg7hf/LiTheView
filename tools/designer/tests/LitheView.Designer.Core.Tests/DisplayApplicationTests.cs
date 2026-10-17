// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.DisplayBuild;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class DisplayApplicationTests
{
    public static void Run(string repositoryRoot)
    {
        string fixture = Path.Combine(repositoryRoot, "tests", "rendering", "display-model-generated");
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(
            Path.Combine(repositoryRoot, "schema", "widgets.json"));
        Require(catalog.IsValid && catalog.Catalog is not null, "Widget catalog must load.");
        ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(
            ["resources/gallop.resource.json"], fixture);
        Require(resources.IsValid, "Retained generated resource variants must load and verify.");
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(
            Path.Combine(repositoryRoot, "profiles", "logical-inputs", "encoder-select.json"));
        Require(input.IsValid, "Logical inputs must load independently.");

        foreach (string definitionPath in Directory.GetFiles(
            Path.Combine(repositoryRoot, "profiles", "display-definitions"), "*.json"))
        {
            DisplayDefinitionLoadResult definition = DisplayDefinitionProfileLoader.LoadFile(definitionPath);
            string formPath = Path.Combine(fixture, Path.GetFileNameWithoutExtension(definitionPath) + ".litheview.xml");
            FormDocumentLoadResult form = FormXmlImporter.LoadFile(formPath, catalog.Catalog!, FormImportMode.Strict);
            Require(form.IsValid && form.Document is not null, "A checked form fixture must load.");
            DisplayApplicationCompilationResult compiled = DisplayApplicationCompiler.Compile(
                form.Document!, catalog.Catalog!, definition, input, resources);
            Require(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(item => item.Message)));
            Require(compiled.SurfaceStatus == ValidationStageStatus.Passed &&
                compiled.TargetStatus == ValidationStageStatus.Unevaluated,
                "Surface compatibility and generation must not require or certify an MCU target.");
            Require(compiled.Files.Count == 3 && compiled.Resources.Count == 1 &&
                compiled.EstimatedResourceRomBytes > 0,
                "Only the requested, compatible static resource variant must be emitted.");
            DisplayApplicationCompilationResult repeated = DisplayApplicationCompiler.Compile(
                form.Document!, catalog.Catalog!, definition, input, resources);
            Require(repeated.IsValid && compiled.Files.All(item =>
                repeated.Files[item.Key].AsSpan().SequenceEqual(item.Value)),
                "All generated headers must be deterministic.");

            ProjectResourceSetLoadResult empty = new() { Resources = [], Diagnostics = [] };
            DisplayApplicationCompilationResult missing = DisplayApplicationCompiler.Compile(
                form.Document!, catalog.Catalog!, definition, input, empty);
            Require(!missing.IsValid && missing.Files.Count == 0 &&
                missing.Diagnostics.Any(item => item.Code == "LVDB1001") &&
                missing.TargetStatus == ValidationStageStatus.Unevaluated,
                "Missing application resources must fail without evaluating the target.");

            DisplayApplicationCompilationResult wrongSurface = DisplayApplicationCompiler.Compile(
                form.Document! with { SurfaceProfileId = "unrelated.surface" },
                catalog.Catalog!, definition, input, resources);
            Require(!wrongSurface.IsValid && wrongSurface.Files.Count == 0 &&
                wrongSurface.Diagnostics.Any(item => item.Code == "LVFS1001") &&
                wrongSurface.TargetStatus == ValidationStageStatus.Unevaluated,
                "Surface identity failures must remain independent of target selection.");
        }
    }

    // Exercise the runtime names produced by the actual managed importers. The
    // converter fixtures are synthetic; native payload compilation is a separate check.
    public static void CheckImportedResource(string repositoryRoot, ProjectResourceSetLoadResult resources)
    {
        WidgetCatalog catalog = WidgetCatalogLoader.LoadFile(Path.Combine(repositoryRoot, "schema", "widgets.json")).Catalog!;
        DisplayDefinitionLoadResult definition = DisplayDefinitionProfileLoader.LoadFile(Path.Combine(repositoryRoot,
            "profiles", "display-definitions", "rk043fn48h-rgb565.json"));
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(Path.Combine(repositoryRoot,
            "profiles", "logical-inputs", "encoder-select.json"));
        FormDocument baseline = FormXmlImporter.LoadFile(Path.Combine(repositoryRoot, "tests", "rendering", "display-model-generated",
            "rk043fn48h-rgb565.litheview.xml"), catalog, FormImportMode.Strict).Document!;
        LoadedProjectResource loaded = resources.Resources.Single();
        string entryPoint = loaded.Manifest.Variants.Single().Converter.Options["entryPoint"];
        bool animation = loaded.Manifest.Kind == ResourceKind.Animation;
        FormResource reference = new()
        {
            Id = "resource.imported", Symbol = "Imported", RuntimeName = animation ? entryPoint + "-f0" : entryPoint,
            Kind = loaded.Manifest.Kind == ResourceKind.Font ? FormResourceKind.Font : FormResourceKind.Bitmap
        };
        FormDocument form = baseline with
        {
            Components = [baseline.Components.Single(item => item.ParentComponentId is null)],
            Actions = [], Bindings = [], Resources = [reference]
        };
        DisplayApplicationCompilationResult compiled = DisplayApplicationCompiler.Compile(form, catalog, definition, input, resources);
        Require(compiled.IsValid && compiled.Resources.Count == 1,
            "An imported converter entry point/frame must bind a single checked static provider: " +
            string.Join("; ", compiled.Diagnostics.Select(item => item.Message)));
        string provider = System.Text.Encoding.UTF8.GetString(compiled.Files["resources.hpp"]);
        Require(provider.Contains(entryPoint + "()", StringComparison.Ordinal) &&
            provider.Contains(reference.RuntimeName, StringComparison.Ordinal), "Generated provider lost the imported runtime name.");
        ProjectResourceSetLoadResult ambiguous = resources with
        {
            Resources = [loaded, loaded with { Manifest = loaded.Manifest with { Id = "another.resource" } }]
        };
        Require(!DisplayApplicationCompiler.Compile(form, catalog, definition, input, ambiguous).IsValid,
            "Two manifests sharing the same imported runtime name must be rejected.");
        FormDocument wrongKind = form with
        {
            Resources = [reference with { Kind = reference.Kind == FormResourceKind.Font ? FormResourceKind.Bitmap : FormResourceKind.Font }]
        };
        Require(DisplayApplicationCompiler.Compile(wrongKind, catalog, definition, input, resources).Diagnostics
            .Any(item => item.Code == "LVDB1002"), "Font and bitmap resource kinds must not be interchangeable.");
        if (animation)
        {
            foreach (string suffix in new[] { "-f00", "-f4", "-f-1", "-f+1" })
            {
                FormDocument invalid = form with { Resources = [reference with { RuntimeName = entryPoint + suffix }] };
                Require(!DisplayApplicationCompiler.Compile(invalid, catalog, definition, input, resources).IsValid,
                    "Animation frame references require canonical, in-range indices.");
            }
            FormDocument frames = form with
            {
                Resources = [reference, reference with { Id = "resource.last", Symbol = "Last", RuntimeName = entryPoint + "-f3" }]
            };
            Require(DisplayApplicationCompiler.Compile(frames, catalog, definition, input, resources) is { IsValid: true, Resources.Count: 1 },
                "Multiple animation frames must share one selected static sequence.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
