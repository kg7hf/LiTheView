// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class DesignerProductTests
{
    public static void Run(string repositoryRoot)
    {
        string temporary = Path.Combine(repositoryRoot, "build", "designer-product-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(
                Path.Combine(repositoryRoot, "profiles", "display-surfaces"));
            WidgetCatalog widgets = WidgetCatalogLoader.LoadFile(Path.Combine(repositoryRoot, "schema", "widgets.json")).Catalog!;
            DisplaySurfaceLoadResult rgb = catalog.Surfaces.Single(item => item.Profile!.Id ==
                "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565");
            CanonicalRoundTrip(temporary, catalog, widgets, rgb);
            CustomSurfaceAndInput(temporary, catalog, widgets, rgb);
            SnapshotTampering(temporary, catalog, widgets, rgb);
            TutorialAndRound(temporary, catalog, widgets, rgb);
            LegacyProjects(repositoryRoot, temporary, catalog, widgets);
            VersionedManifestIsStrict(rgb);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static void CanonicalRoundTrip(string root, DisplaySurfaceCatalogLoadResult catalog,
        WidgetCatalog widgets, DisplaySurfaceLoadResult surface)
    {
        DesignerWorkspace workspace = Create(root, "canonical", catalog, widgets, surface);
        Assert(workspace.Manifest.SchemaVersion == 2 && workspace.Manifest.SchemaCompatibility.DisplayDefinition == 2 &&
            workspace.Manifest.SchemaCompatibility.LogicalInput == 1, "New RGB projects must persist the versioned product contract.");
        DesignerProductResolutionResult first = DesignerProductResolver.Resolve(workspace);
        Assert(first.IsValid && first.Definition!.Profile!.Id == surface.Profile!.Id && !first.Input!.Configuration!.Text,
            "The product must retain the source ID and must not infer text input from keyboard capability.");
        DesignerProjectLoadResult loaded = DesignerProjectLoader.LoadFile(workspace.ManifestPath);
        Assert(!DesignerProjectResolver.Resolve(loaded, catalog.Surfaces).IsValid &&
            DesignerProjectResolver.Resolve(loaded, catalog.Surfaces, workspace.ProjectDirectory).IsValid,
            "Version-2 resolution must validate actual snapshots instead of accepting lock metadata alone.");
        Assert(File.ReadAllBytes(workspace.ManifestPath).SequenceEqual(DesignerProjectCanonicalizer.Canonicalize(workspace.Manifest)),
            "Project manifest must be canonical.");
        Assert(File.ReadAllBytes(Path.Combine(workspace.ProjectDirectory, DesignerProductResolver.DefinitionRelativePath))
            .SequenceEqual(first.Definition!.CanonicalUtf8!), "Definition snapshot must be canonical.");
        Assert(File.ReadAllBytes(Path.Combine(workspace.ProjectDirectory, DesignerProductResolver.InputRelativePath))
            .SequenceEqual(first.Input!.CanonicalUtf8!), "Input snapshot must be canonical.");
        Assert(DesignerWorkspaceService.SaveForm(workspace, workspace.Form).IsValid, "Canonical form save failed.");
        DesignerWorkspaceLoadResult reopened = DesignerWorkspaceService.Open(workspace.ManifestPath, catalog, widgets);
        Assert(reopened.IsValid && DesignerProductResolver.Resolve(reopened.Workspace!).ResolvedSha256 == first.ResolvedSha256,
            "Save/reopen must preserve product identity without a target.");
        Assert(!File.ReadAllText(workspace.ManifestPath).Contains("target", StringComparison.OrdinalIgnoreCase),
            "A new project must not require an MCU target.");
        DesignerProductResolutionResult edited = DesignerProductResolver.Resolve(workspace with
        {
            Form = workspace.Form with { DisplayName = "Unsaved form edit" }
        });
        Assert(edited.IsValid && edited.FormSha256 != first.FormSha256 && edited.ResolvedSha256 != first.ResolvedSha256,
            "Resolution must bind the active in-memory form rather than silently reopening stale XML.");
        DesignerProductResolutionResult changedSurface = DesignerProductResolver.Resolve(workspace with
        {
            ResolvedProject = workspace.ResolvedProject with
            {
                Surface = workspace.ResolvedProject.Surface! with { DisplayName = "changed" }
            }
        });
        Assert(!changedSurface.IsValid && Has(changedSurface.Diagnostics, "LVDP1001"), "A stale resolved editing surface must fail.");
        DesignerWorkspaceLoadResult staleRequest = DesignerWorkspaceService.CreateNew(Request(root, "stale", widgets,
            surface with { Profile = surface.Profile! with { DisplayName = "changed" } }), catalog);
        Assert(!staleRequest.IsValid && Has(staleRequest.Diagnostics, "LVDP1001") &&
            !Directory.Exists(Path.Combine(root, "stale")), "A changed creation input must fail before publishing a project directory.");
    }

    private static void CustomSurfaceAndInput(string root, DisplaySurfaceCatalogLoadResult catalog,
        WidgetCatalog widgets, DisplaySurfaceLoadResult source)
    {
        DisplaySurfaceProfile profile = source.Profile! with
        {
            Id = "example.surface.custom-128",
            DisplayName = "Custom 128",
            PixelSize = new() { Width = 128, Height = 96 },
            SafeArea = new() { X = 0, Y = 0, Width = 128, Height = 96 }
        };
        DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.Load(DisplaySurfaceCanonicalizer.Canonicalize(profile));
        Assert(surface.IsValid, "Custom fixture must be a valid RGB surface.");
        LogicalInputConfigurationProfile input = Input() with { Encoder = true, Select = true };
        DesignerWorkspace workspace = Create(root, "custom", catalog, widgets, surface, input);
        DesignerProductResolutionResult product = DesignerProductResolver.Resolve(workspace);
        Assert(product.IsValid && product.Definition!.Profile!.Geometry.Width == 128 &&
            product.Definition.Profile.Geometry.Height == 96 && product.Input!.Configuration == input,
            "Custom geometry and separate encoder/select input must survive independent project-local snapshots.");
        Assert(workspace.ResolvedProject.Surface!.InputCapabilities.SequenceEqual(source.Profile.InputCapabilities),
            "The separate input contract must not silently rewrite historical profile hints.");
        FormComponent original = workspace.Form.Components[0];
        DesignerProductResolutionResult malformed = DesignerProductResolver.Resolve(workspace with
        {
            Form = workspace.Form with
            {
                Components = [original with { Properties = [new FormRectangleValue("frame", new(0, 0, 127, 96))] }]
            }
        });
        Assert(!malformed.IsValid && Has(malformed.Diagnostics, "LVFS1002"), "The active form must match the exact display extent.");
    }

    private static void SnapshotTampering(string root, DisplaySurfaceCatalogLoadResult catalog,
        WidgetCatalog widgets, DisplaySurfaceLoadResult surface)
    {
        DesignerWorkspace workspace = Create(root, "tamper", catalog, widgets, surface);
        string inputPath = Path.Combine(workspace.ProjectDirectory, DesignerProductResolver.InputRelativePath);
        byte[] originalInput = File.ReadAllBytes(inputPath);
        LogicalInputConfigurationProfile input = LogicalInputConfigurationLoader.Load(originalInput).Configuration!;
        File.WriteAllBytes(inputPath, LogicalInputConfigurationCanonicalizer.Canonicalize(input with { Text = !input.Text }));
        DesignerWorkspaceLoadResult changedInput = DesignerWorkspaceService.Open(workspace.ManifestPath, catalog, widgets);
        Assert(!changedInput.IsValid && Has(changedInput.Diagnostics, "LVDP1003"), "Changed input must fail its canonical hash lock.");
        File.WriteAllBytes(inputPath, originalInput);
        string definitionPath = Path.Combine(workspace.ProjectDirectory, DesignerProductResolver.DefinitionRelativePath);
        DisplayDefinitionProfile definition = DisplayDefinitionProfileLoader.LoadFile(definitionPath).Profile!;
        byte[] altered = DisplayDefinitionCanonicalizer.Canonicalize(definition with
        {
            Geometry = definition.Geometry with { Width = definition.Geometry.Width + 2 }
        });
        File.WriteAllBytes(definitionPath, altered);
        Assert(!DesignerWorkspaceService.Open(workspace.ManifestPath, catalog, widgets).IsValid,
            "Changed display must fail its hash lock.");
        DesignerProjectManifest relocked = workspace.Manifest with
        {
            DisplayDefinition = workspace.Manifest.DisplayDefinition! with
            {
                Sha256 = DisplaySurfaceCanonicalizer.ComputeSha256(altered)
            }
        };
        File.WriteAllBytes(workspace.ManifestPath, DesignerProjectCanonicalizer.Canonicalize(relocked));
        DesignerWorkspaceLoadResult changedGeometry = DesignerWorkspaceService.Open(workspace.ManifestPath, catalog, widgets);
        Assert(!changedGeometry.IsValid && Has(changedGeometry.Diagnostics, "LVDP1004"),
            "Relocking a divergent v2 definition must not produce two different display geometries.");
        File.Delete(inputPath);
        Assert(!DesignerWorkspaceService.Open(workspace.ManifestPath, catalog, widgets).IsValid,
            "A missing v2 snapshot must fail instead of adapting or falling back silently.");
    }

    private static void TutorialAndRound(string root, DisplaySurfaceCatalogLoadResult catalog,
        WidgetCatalog widgets, DisplaySurfaceLoadResult rgb)
    {
        DisplaySurfaceLoadResult round = catalog.Surfaces.Single(item => item.Profile!.Shape == SurfaceShape.Circle);
        DesignerWorkspace workspace = Create(root, "round", catalog, widgets, round,
            Input() with { Encoder = true, Select = true }, starter: true);
        DesignerProductResolutionResult product = DesignerProductResolver.Resolve(workspace);
        Assert(product.IsValid && product.Definition!.Profile!.Geometry.Shape == SurfaceShape.Circle &&
            product.Definition.Profile.Geometry.SafeArea == round.Profile!.SafeArea,
            "Round definition and safe area must retain the selected meaning.");
        Assert(workspace.Form.Components.Length == 4 && workspace.Form.Actions.Length == 2 &&
            workspace.Form.Actions[0].RuntimeId == 1 && workspace.Form.Actions[1].RuntimeId == 2,
            "The explicit tutorial must provide the agreed status and two action controls.");
        string hookPath = Path.Combine(workspace.ProjectDirectory, "simulation", "application.hpp");
        Assert(File.ReadAllBytes(hookPath).SequenceEqual(DesignerTutorialStarter.ApplicationSource),
            "The tutorial must seed a typed developer-owned hook deterministically.");
        File.WriteAllText(hookPath, "// developer-owned change\n");
        Assert(DesignerWorkspaceService.SaveForm(workspace, workspace.Form).IsValid &&
            DesignerWorkspaceService.Open(workspace.ManifestPath, catalog, widgets).IsValid &&
            File.ReadAllText(hookPath) == "// developer-owned change\n", "Save/reopen must not replace developer-owned hooks.");
        DesignerWorkspaceLoadResult outputOnly = DesignerWorkspaceService.CreateNew(
            Request(root, "output-starter", widgets, rgb, Input(), true), catalog);
        Assert(!outputOnly.IsValid && Has(outputOnly.Diagnostics, "LVSL3009") &&
            !Directory.Exists(Path.Combine(root, "output-starter")), "Output-only tutorial rejection must occur before file creation.");
        DesignerWorkspace output = Create(root, "output", catalog, widgets, rgb, Input());
        Assert(DesignerProductResolver.Resolve(output).IsValid, "An empty output-only product is valid.");
        DisplaySurfaceProfile keyboard = rgb.Profile! with { InputCapabilities = [InputCapability.Keyboard] };
        DesignerWorkspace keys = Create(root, "keys", catalog, widgets,
            DisplaySurfaceProfileLoader.Load(DisplaySurfaceCanonicalizer.Canonicalize(keyboard)));
        LogicalInputConfigurationProfile keyInput = DesignerProductResolver.Resolve(keys).Input!.Configuration!;
        Assert(keyInput.Keys && !keyInput.Text, "A keyboard hint must not invent text-entry capability.");
        DisplaySurfaceProfile tiny = rgb.Profile! with
        {
            PixelSize = new() { Width = 64, Height = 48 }, SafeArea = new() { X = 0, Y = 0, Width = 64, Height = 48 }
        };
        DesignerWorkspaceLoadResult tooSmall = DesignerWorkspaceService.CreateNew(Request(root, "tiny", widgets,
            DisplaySurfaceProfileLoader.Load(DisplaySurfaceCanonicalizer.Canonicalize(tiny)),
            Input() with { Pointer = true }, true), catalog);
        Assert(!tooSmall.IsValid && Has(tooSmall.Diagnostics, "LVDP1101"), "Small surfaces must choose their own layout rather than receive clipped controls.");
    }

    private static void LegacyProjects(string repositoryRoot, string root,
        DisplaySurfaceCatalogLoadResult catalog, WidgetCatalog widgets)
    {
        string legacyPath = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "synthetic-red-staging-smoke", "project.litheview.json");
        DesignerProjectLoadResult fixture = DesignerProjectLoader.LoadFile(legacyPath);
        DisplaySurfaceLoadResult mdp = catalog.Surfaces.Single(item => item.Profile!.Id == fixture.Manifest!.SurfaceProfile!.Id);
        DesignerWorkspace created = Create(root, "legacy-mdp", catalog, widgets, mdp);
        byte[] before = File.ReadAllBytes(created.ManifestPath);
        DesignerWorkspaceLoadResult opened = DesignerWorkspaceService.Open(created.ManifestPath, catalog, widgets);
        Assert(opened.IsValid && opened.Workspace!.Manifest.SchemaVersion == 1 &&
            before.SequenceEqual(File.ReadAllBytes(created.ManifestPath)), "Opening legacy projects must not migrate their bytes.");
        DesignerProductResolutionResult product = DesignerProductResolver.Resolve(opened.Workspace!);
        Assert(product.Definition?.LegacySurface is not null && product.Definition.Profile!.Geometry.Width == 512 &&
            product.Definition.Profile.Format == SurfacePixelFormat.Rgb565 && Has(product.Diagnostics, "LVDD3003"),
            "Legacy packed-display remains 512-wide RGB staging with explicit migration metadata.");
        Assert(!DisplayDefinitionCppCompiler.Compile(product.Definition!, product.Input!.Configuration!).IsValid,
            "The retained packed-display staging metadata must continue to block an unsupported native export.");
        Assert(created.Manifest.SchemaVersion == 1 && !File.Exists(Path.Combine(created.ProjectDirectory,
            DesignerProductResolver.DefinitionRelativePath)), "New legacy packed-display creation must not silently relabel native meaning.");
        DisplaySurfaceProfile rotated = created.ResolvedProject.Surface! with
        {
            CoordinateSystem = created.ResolvedProject.Surface!.CoordinateSystem with { RotationDegrees = 90 }
        };
        byte[] rotatedBytes = DisplaySurfaceCanonicalizer.Canonicalize(rotated);
        File.WriteAllBytes(Path.Combine(created.ProjectDirectory, "profiles", "display-surface.json"), rotatedBytes);
        File.WriteAllBytes(created.ManifestPath, DesignerProjectCanonicalizer.Canonicalize(created.Manifest with
        {
            SurfaceProfile = created.Manifest.SurfaceProfile! with { Sha256 = DisplaySurfaceCanonicalizer.ComputeSha256(rotatedBytes) }
        }));
        DesignerWorkspaceLoadResult rotatedWorkspace = DesignerWorkspaceService.Open(created.ManifestPath, catalog, widgets);
        Assert(rotatedWorkspace.IsValid && !DesignerProductResolver.Resolve(rotatedWorkspace.Workspace!).IsValid,
            "Legacy rotation remains editable even when the modern compiled definition cannot represent it.");
    }

    private static void VersionedManifestIsStrict(DisplaySurfaceLoadResult surface)
    {
        DesignerProjectLoadResult legacy = DesignerProjectFactory.Create("test-schema", "example.project", "Example", surface);
        Assert(legacy.IsValid && legacy.CanonicalUtf8!.SequenceEqual(DesignerProjectCanonicalizer.Canonicalize(legacy.Manifest!)),
            "Legacy factory and canonical contract must remain unchanged.");
        string json = Encoding.UTF8.GetString(legacy.CanonicalUtf8!);
        Assert(!DesignerProjectLoader.Load(Encoding.UTF8.GetBytes(json.Replace("\"forms\":", "\"displayDefinition\":null,\"forms\":", StringComparison.Ordinal))).IsValid,
            "Version-1 product fields cannot hide behind null placeholders.");
        DesignerProjectManifest incomplete = legacy.Manifest! with
        {
            SchemaVersion = 2,
            SchemaCompatibility = legacy.Manifest.SchemaCompatibility with { ProjectManifest = 2, DisplayDefinition = 2, LogicalInput = 1 }
        };
        Assert(!DesignerProjectLoader.Load(DesignerProjectCanonicalizer.Canonicalize(incomplete)).IsValid,
            "Version-2 projects require both locks.");
    }

    private static LogicalInputConfigurationProfile Input() => new()
    {
        Schema = "https://litheview.dev/schema/logical-input-configuration.schema.json",
        SchemaVersion = 1, Id = "example.input", Pointer = false, Keys = false, Text = false, Encoder = false, Select = false
    };

    private static NewDesignerProjectRequest Request(string root, string name, WidgetCatalog widgets,
        DisplaySurfaceLoadResult surface, LogicalInputConfigurationProfile? input = null, bool starter = false) => new()
    {
        ProjectDirectory = Path.Combine(root, name), ProjectId = "example.project." + name,
        DisplayName = "Example", SchemaReference = "https://litheview.dev/schema/project-manifest.schema.json",
        Surface = surface, Catalog = widgets, Input = input, IncludeTutorialStarter = starter
    };

    private static DesignerWorkspace Create(string root, string name, DisplaySurfaceCatalogLoadResult catalog,
        WidgetCatalog widgets, DisplaySurfaceLoadResult surface, LogicalInputConfigurationProfile? input = null, bool starter = false)
    {
        DesignerWorkspaceLoadResult result = DesignerWorkspaceService.CreateNew(Request(root, name, widgets, surface, input, starter), catalog);
        Assert(result.IsValid, "Project creation failed: " + string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        return result.Workspace!;
    }

    private static bool Has(IEnumerable<ProfileDiagnostic> diagnostics, string code) => diagnostics.Any(item => item.Code == code);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
