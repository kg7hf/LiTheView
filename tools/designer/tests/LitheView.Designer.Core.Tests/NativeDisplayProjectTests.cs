// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.Json.Nodes;
using LitheView.Designer.Core.DisplayBuild;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

// Version-3 projects lock a native packed display definition and an independent logical
// input without an RGB editing surface. The checked fixture is pinned by "all project
// fixtures resolve"; these checks use small temporary copies of it.
internal static class NativeDisplayProjectTests
{
    // Identities of an RGB version-2 project captured before the version-3 contract existed.
    private const string RgbManifestSha256 = "cffd3639253c07895f57fb1d9fb6fec25a6ac5b07a25df64d2a737eb2213dca8";
    private const string RgbResolvedSha256 = "eb10171da5413d2d9f068274d6c35acb111e91218883de2ea673d5a22869f283";
    private const string RgbProductSha256 = "91166fb06841b5cc1a1cb50301b95afa0acc41506f3b0c906de7e4dddc5a8752";

    public static void Run(string repositoryRoot)
    {
        string temporary = Path.Combine(repositoryRoot, "build", "native-display-project-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            WidgetCatalog widgets = WidgetCatalogLoader.LoadFile(Path.Combine(repositoryRoot, "schema", "widgets.json")).Catalog!;
            DisplaySurfaceCatalogLoadResult surfaces = ProfileCatalogLoader.LoadSurfaces(
                Path.Combine(repositoryRoot, "profiles", "display-surfaces"));
            LogicalInputConfigurationProfile input = LogicalInputConfigurationLoader.LoadFile(
                Path.Combine(repositoryRoot, "profiles", "logical-inputs", "encoder-select.json")).Configuration!;
            string fixture = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "native-synthetic-r2-smoke");
            NativeFormatsResolveWithoutTarget(repositoryRoot, temporary, widgets, surfaces, input);
            StaleSnapshotsAreRejected(fixture, temporary, widgets, surfaces);
            GeometryAndStorageMismatchesAreRejected(repositoryRoot, fixture, temporary, widgets, surfaces, input);
            ManifestContractIsStrict(repositoryRoot, fixture, temporary, widgets, input);
            LegacyStagingIsNotReinterpreted(repositoryRoot, temporary, widgets, surfaces, input);
            RgbResolutionIsUnchanged(temporary, widgets, surfaces);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static void NativeFormatsResolveWithoutTarget(string repositoryRoot, string root, WidgetCatalog widgets,
        DisplaySurfaceCatalogLoadResult surfaces, LogicalInputConfigurationProfile input)
    {
        foreach ((string name, SurfacePixelFormat format, int width, int height) in new[]
                 {
                     ("synthetic-r2", SurfacePixelFormat.R2Packed2x2, 512, 384),
                     ("synthetic-red-cell", SurfacePixelFormat.Rrrr2222, 256, 192),
                     ("synthetic-red-yellow", SurfacePixelFormat.Ryry2222, 256, 192)
                 })
        {
            DesignerWorkspaceLoadResult created = Create(root, name, widgets, Definition(repositoryRoot, name), input);
            Require(created.IsValid, $"Native {name} project creation failed: {Messages(created.Diagnostics)}");
            DesignerWorkspace workspace = created.Workspace!;
            string manifestPath = workspace.ManifestPath;
            string directory = workspace.ProjectDirectory;
            string manifestText = File.ReadAllText(manifestPath);
            Require(!manifestText.Contains("surface", StringComparison.OrdinalIgnoreCase) &&
                !manifestText.Contains("target", StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(Path.Combine(directory, "profiles", "display-surface.json")),
                "A native project persists no editing surface, RGB staging profile or MCU target.");

            DesignerProjectLoadResult loaded = DesignerProjectLoader.LoadFile(manifestPath);
            Require(loaded.IsValid && loaded.Manifest!.SchemaVersion == 3 && loaded.Manifest.SurfaceProfile is null &&
                loaded.Manifest.SchemaCompatibility.DisplaySurface is null &&
                File.ReadAllBytes(manifestPath).SequenceEqual(loaded.CanonicalUtf8!),
                "A native project manifest must be canonical version 3 without surface fields.");
            DesignerProjectResolutionResult resolved = DesignerProjectResolver.Resolve(loaded, [], directory);
            Require(resolved.IsValid && resolved.Project is { Surface: null, SurfaceSha256: null },
                $"Native {name} must resolve from its snapshots with no surface catalog or target.");
            ResolvedDesignerProject project = resolved.Project!;
            DesignerDisplay display = project.Display;
            Require(display.Format == format && display.Width == width && display.Height == height &&
                display.IsNativePacked && display.LegacyStagingSurface is null && display.Definition?.Format == format &&
                display.Input.Encoder && display.Input.Select && !display.Input.Pointer &&
                project.ResolvedSha256 == workspace.ResolvedProject.ResolvedSha256,
                $"Native {name} must keep its native extent, format and independent input.");
            DisplaySurfaceLoadResult anySurface = surfaces.Surfaces[0];
            Require(DesignerProjectResolver.Resolve(loaded, [anySurface, anySurface], directory)
                    .Project?.ResolvedSha256 == project.ResolvedSha256,
                "A native project must not consult an editing-surface catalog, even an invalid one.");

            // The ordinary workspace opened it: one editor and product check for RGB and packed displays.
            DesignerProductResolutionResult product = DesignerProductResolver.Resolve(workspace);
            Require(product.IsValid && product.Definition!.Sha256 == display.DefinitionSha256,
                $"Native {name} form must validate against its definition: {Messages(product.Diagnostics)}");
            FormDocument form = workspace.Form;
            Require(form.SurfaceProfileId == display.Id &&
                form.Components[0].Properties.OfType<FormRectangleValue>().Single().Value ==
                new FormRectangle(0, 0, width, height),
                "The default form must use the native logical extent without rescaling.");

            // The existing host compiler consumes the snapshots without a target; it does not read the manifest.
            DisplayApplicationCompilationResult compiled = DisplayApplicationCompiler.Compile(form, widgets,
                DisplayDefinitionProfileLoader.LoadFile(Path.Combine(directory, DesignerProductResolver.DefinitionRelativePath)),
                LogicalInputConfigurationLoader.LoadFile(Path.Combine(directory, DesignerProductResolver.InputRelativePath)),
                ProjectResourceSetLoader.Load(loaded.Manifest!, directory));
            Require(compiled.IsValid && compiled.SurfaceStatus == ValidationStageStatus.Passed &&
                compiled.TargetStatus == ValidationStageStatus.Unevaluated &&
                compiled.DisplayDefinitionSha256 == display.DefinitionSha256,
                $"Native {name} snapshots must compile without a target: {Messages(compiled.Diagnostics)}");

            DesignerWorkspaceLoadResult reopened = DesignerWorkspaceService.Open(manifestPath, surfaces, widgets);
            Require(reopened.IsValid && reopened.Workspace!.ResolvedProject.ResolvedSha256 == project.ResolvedSha256 &&
                reopened.Workspace.ResolvedProject.Display.DefinitionSha256 == display.DefinitionSha256,
                "Reopening a native project must resolve the same display identity.");
            Require(!Create(root, name, widgets, Definition(repositoryRoot, name), input).IsValid,
                "Native project creation must never overwrite an existing project.");
        }
    }

    private static void StaleSnapshotsAreRejected(string fixture, string root, WidgetCatalog widgets,
        DisplaySurfaceCatalogLoadResult surfaces)
    {
        string definitionPath = DesignerProductResolver.DefinitionRelativePath;
        string inputPath = DesignerProductResolver.InputRelativePath;

        string definitionChanged = CopyFixture(fixture, root, "stale-definition");
        DisplayDefinitionProfile definition = DisplayDefinitionProfileLoader.LoadFile(
            Path.Combine(definitionChanged, definitionPath)).Profile!;
        File.WriteAllBytes(Path.Combine(definitionChanged, definitionPath),
            DisplayDefinitionCanonicalizer.Canonicalize(definition with { DisplayName = "changed" }));
        DesignerProjectResolutionResult definitionResult = Resolve(definitionChanged);
        Require(!definitionResult.IsValid && Has(definitionResult.Diagnostics, "LVDP1003", "$.displayDefinition"),
            "A valid definition snapshot that no longer matches its lock must be rejected.");

        string inputChanged = CopyFixture(fixture, root, "stale-input");
        LogicalInputConfigurationProfile input = LogicalInputConfigurationLoader.LoadFile(
            Path.Combine(inputChanged, inputPath)).Configuration!;
        File.WriteAllBytes(Path.Combine(inputChanged, inputPath),
            LogicalInputConfigurationCanonicalizer.Canonicalize(input with { Pointer = true }));
        DesignerProjectResolutionResult inputResult = Resolve(inputChanged);
        Require(!inputResult.IsValid && Has(inputResult.Diagnostics, "LVDP1003", "$.logicalInput"),
            "A changed logical-input snapshot must be rejected.");

        string lockChanged = CopyFixture(fixture, root, "stale-lock");
        RewriteManifest(lockChanged, manifest => manifest with
        {
            DisplayDefinition = manifest.DisplayDefinition! with { Sha256 = new string('0', 64) }
        });
        Require(Has(Resolve(lockChanged).Diagnostics, "LVDP1003", "$.displayDefinition"),
            "A manifest lock that does not match unchanged snapshots must be rejected.");

        string missing = CopyFixture(fixture, root, "missing-input");
        File.Delete(Path.Combine(missing, inputPath));
        Require(Has(Resolve(missing).Diagnostics, "LVDP1003", "$.logicalInput"),
            "A missing snapshot must be rejected.");

        DesignerProjectLoadResult loaded = DesignerProjectLoader.LoadFile(
            Path.Combine(fixture, DesignerWorkspaceService.ManifestFileName));
        Require(Has(DesignerProjectResolver.Resolve(loaded, []).Diagnostics, "LVDP1007"),
            "Lock metadata alone must not resolve a native project.");
        DesignerWorkspace workspace = Open(fixture, surfaces, widgets);
        ResolvedDesignerProject project = workspace.ResolvedProject;
        Require(Has(DesignerProductResolver.Resolve(workspace with
            {
                ResolvedProject = project with { Display = project.Display with { DefinitionSha256 = new string('0', 64) } }
            }).Diagnostics, "LVDP1001"),
            "A workspace display that no longer matches its snapshots must be rejected.");
    }

    private static void GeometryAndStorageMismatchesAreRejected(string repositoryRoot, string fixture, string root,
        WidgetCatalog widgets, DisplaySurfaceCatalogLoadResult surfaces, LogicalInputConfigurationProfile input)
    {
        (string Name, string Code, Action<JsonNode> Change)[] mismatches =
        [
            ("odd-emitter-width", "LVDS2007", node =>
            {
                node["geometry"]!["width"] = 511;
                node["geometry"]!["safeArea"]!["width"] = 511;
            }),
            ("cell-storage-block", "LVDS2007", node => node["storageBlock"]!["width"] = 1),
            ("cell-address-space", "LVDS2006", node => node["addressSpace"] = "cells")
        ];
        foreach ((string name, string code, Action<JsonNode> change) in mismatches)
        {
            JsonNode node = JsonNode.Parse(File.ReadAllText(Path.Combine(
                repositoryRoot, "profiles", "display-definitions", "synthetic-r2.json")))!;
            change(node);
            byte[] bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
            DesignerWorkspaceLoadResult created = Create(root, name, widgets,
                DisplayDefinitionProfileLoader.Load(bytes), input);
            Require(!created.IsValid && Has(created.Diagnostics, code) && !Directory.Exists(Path.Combine(root, name)),
                $"Creation must reject {name} with {code} before writing files.");

            string copy = CopyFixture(fixture, root, "snapshot-" + name);
            File.WriteAllBytes(Path.Combine(copy, DesignerProductResolver.DefinitionRelativePath), bytes);
            DesignerProjectResolutionResult resolved = Resolve(copy);
            Require(!resolved.IsValid && Has(resolved.Diagnostics, code),
                $"Resolution must reject a {name} snapshot with {code}.");
        }

        // A valid, relocked definition with a different extent still disagrees with the form.
        string relocked = CopyFixture(fixture, root, "relocked-extent");
        string definitionPath = Path.Combine(relocked, DesignerProductResolver.DefinitionRelativePath);
        DisplayDefinitionProfile definition = DisplayDefinitionProfileLoader.LoadFile(definitionPath).Profile!;
        byte[] shorter = DisplayDefinitionCanonicalizer.Canonicalize(definition with
        {
            Geometry = definition.Geometry with
            {
                Height = 192,
                SafeArea = definition.Geometry.SafeArea with { Height = 192 }
            }
        });
        File.WriteAllBytes(definitionPath, shorter);
        RewriteManifest(relocked, manifest => manifest with
        {
            DisplayDefinition = manifest.DisplayDefinition! with { Sha256 = DisplaySurfaceCanonicalizer.ComputeSha256(shorter) }
        });
        Require(Resolve(relocked).IsValid && Has(DesignerProductResolver.Resolve(Open(relocked, surfaces, widgets)).Diagnostics,
                "LVFS1002"),
            "A form whose extent differs from the locked native definition must be rejected.");
    }

    private static void ManifestContractIsStrict(string repositoryRoot, string fixture, string root,
        WidgetCatalog widgets, LogicalInputConfigurationProfile input)
    {
        string native = File.ReadAllText(Path.Combine(fixture, DesignerWorkspaceService.ManifestFileName));
        string legacy = File.ReadAllText(Path.Combine(repositoryRoot, "tests", "designer-fixtures",
            "synthetic-red-staging-smoke", DesignerWorkspaceService.ManifestFileName));
        JsonNode surfaceLock = JsonNode.Parse(legacy)!["surfaceProfile"]!;
        (string Name, string Code, Action<JsonNode> Change)[] cases =
        [
            ("surface lock", "LVPM1203", node => node["surfaceProfile"] = surfaceLock.DeepClone()),
            ("null surface placeholder", "LVPM1203", node => node["surfaceProfile"] = null),
            ("surface compatibility", "LVPM1203", node => node["schemaCompatibility"]!["displaySurface"] = 1),
            ("null surface compatibility", "LVPM1203", node => node["schemaCompatibility"]!["displaySurface"] = null),
            ("missing input lock", "LVPM1202", node => node.AsObject().Remove("logicalInput")),
            ("version-2 compatibility", "LVPM1201", node => node["schemaCompatibility"]!["projectManifest"] = 2),
            ("unknown version", "LVPM1001", node => node["schemaVersion"] = 99)
        ];
        foreach ((string name, string code, Action<JsonNode> change) in cases)
        {
            JsonNode node = JsonNode.Parse(native)!;
            change(node);
            DesignerProjectLoadResult loaded = DesignerProjectLoader.Load(Encoding.UTF8.GetBytes(node.ToJsonString()));
            Require(!loaded.IsValid && Has(loaded.Diagnostics, code), $"A version-3 manifest with a {name} must fail with {code}.");
        }

        JsonNode relabeled = JsonNode.Parse(legacy)!;
        relabeled["schemaVersion"] = 3;
        Require(Has(DesignerProjectLoader.Load(Encoding.UTF8.GetBytes(relabeled.ToJsonString())).Diagnostics, "LVPM1203"),
            "A legacy RGB-staging manifest must not be relabeled as a native display project.");
        JsonNode surfaceless = JsonNode.Parse(legacy)!;
        surfaceless.AsObject().Remove("surfaceProfile");
        Require(Has(DesignerProjectLoader.Load(Encoding.UTF8.GetBytes(surfaceless.ToJsonString())).Diagnostics, "LVPM1203"),
            "Version-1 and version-2 projects still require their editing surface.");

        DisplayDefinitionLoadResult rgb = Definition(repositoryRoot, "rk043fn48h-rgb565");
        DesignerWorkspaceLoadResult created = Create(root, "rgb-definition", widgets, rgb, input);
        Require(!created.IsValid && Has(created.Diagnostics, "LVDP1008"),
            "RGB definitions keep their version-2 editing surface instead of a native project.");
        string relocked = CopyFixture(fixture, root, "rgb-snapshot");
        File.WriteAllBytes(Path.Combine(relocked, DesignerProductResolver.DefinitionRelativePath), rgb.CanonicalUtf8!);
        RewriteManifest(relocked, manifest => manifest with
        {
            DisplayDefinition = new() { Id = rgb.Profile!.Id, Sha256 = rgb.Sha256! }
        });
        Require(Has(Resolve(relocked).Diagnostics, "LVDP1008"),
            "A relocked RGB snapshot must not resolve as a native display project.");
    }

    private static void LegacyStagingIsNotReinterpreted(string repositoryRoot, string root, WidgetCatalog widgets,
        DisplaySurfaceCatalogLoadResult surfaces, LogicalInputConfigurationProfile input)
    {
        string legacyDirectory = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "synthetic-red-staging-smoke");
        string legacyPath = Path.Combine(legacyDirectory, DesignerWorkspaceService.ManifestFileName);
        byte[] before = File.ReadAllBytes(legacyPath);
        DesignerProjectResolutionResult legacy = DesignerProjectResolver.Resolve(
            DesignerProjectLoader.LoadFile(legacyPath), surfaces.Surfaces, legacyDirectory);
        Require(legacy.IsValid && legacy.Project!.Manifest.SchemaVersion == 1 &&
            legacy.Project.Surface!.PainterSurface.Format == SurfacePixelFormat.Rgb565 &&
            legacy.Project.Display.Format == SurfacePixelFormat.Rgb565 && !legacy.Project.Display.IsNativePacked &&
            legacy.Project.Display.LegacyStagingSurface == legacy.Project.Surface &&
            File.ReadAllBytes(legacyPath).SequenceEqual(before),
            "A legacy packed-display project keeps its RGB565 staging interpretation and bytes.");

        DisplaySurfaceLoadResult mdp = surfaces.Surfaces.Single(item => item.Profile!.Id == legacy.Project!.Surface!.Id);
        DisplayDefinitionLoadResult adapted = DisplayDefinitionProfileLoader.AdaptV1(mdp);
        DesignerWorkspaceLoadResult created = Create(root, "adapted-legacy", widgets, adapted, input);
        Require(!created.IsValid && Has(created.Diagnostics, "LVDP1009") &&
            !Directory.Exists(Path.Combine(root, "adapted-legacy")),
            "A version-1 staging adaptation must not seed a native display project.");
    }

    private static void RgbResolutionIsUnchanged(string root, WidgetCatalog widgets, DisplaySurfaceCatalogLoadResult surfaces)
    {
        DisplaySurfaceLoadResult rgb = surfaces.Surfaces.Single(item =>
            item.Profile!.Id == "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565");
        DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(new NewDesignerProjectRequest
        {
            ProjectDirectory = Path.Combine(root, "rgb-baseline"), ProjectId = "example.project.rgb-baseline",
            DisplayName = "RGB baseline", SchemaReference = "https://litheview.dev/schema/project-manifest.schema.json",
            Surface = rgb, Catalog = widgets
        }, surfaces);
        Require(created.IsValid, "RGB project creation failed: " + Messages(created.Diagnostics));
        DesignerWorkspace workspace = created.Workspace!;
        DesignerProjectResolutionResult resolved = DesignerProjectResolver.Resolve(
            DesignerProjectLoader.LoadFile(workspace.ManifestPath), surfaces.Surfaces, workspace.ProjectDirectory);
        Require(workspace.Manifest.SchemaVersion == 2 && resolved.IsValid && resolved.Project!.Surface is not null &&
            resolved.Project.Display.Format == SurfacePixelFormat.Rgb565 && resolved.Project.Display.LegacyStagingSurface is null &&
            resolved.Project!.ManifestSha256 == RgbManifestSha256 && resolved.Project.ResolvedSha256 == RgbResolvedSha256 &&
            DesignerProductResolver.Resolve(workspace).ResolvedSha256 == RgbProductSha256,
            "RGB version-2 manifest, resolution and product identities must be unchanged.");
    }

    private static DesignerWorkspaceLoadResult Create(string root, string name, WidgetCatalog widgets,
        DisplayDefinitionLoadResult definition, LogicalInputConfigurationProfile input) =>
        DesignerWorkspaceService.CreateNativeDisplayProject(new()
        {
            ProjectDirectory = Path.Combine(root, name),
            ProjectId = "example.native." + name,
            DisplayName = "Native " + name,
            Definition = definition,
            Input = input,
            Catalog = widgets
        });

    private static DisplayDefinitionLoadResult Definition(string repositoryRoot, string name) =>
        DisplayDefinitionProfileLoader.LoadFile(Path.Combine(repositoryRoot, "profiles", "display-definitions", name + ".json"));

    private static DesignerWorkspace Open(string directory, DisplaySurfaceCatalogLoadResult surfaces, WidgetCatalog widgets)
    {
        DesignerWorkspaceLoadResult opened = DesignerWorkspaceService.Open(
            Path.Combine(directory, DesignerWorkspaceService.ManifestFileName), surfaces, widgets);
        Require(opened.IsValid, "The project must open: " + Messages(opened.Diagnostics));
        return opened.Workspace!;
    }

    private static DesignerProjectResolutionResult Resolve(string directory) =>
        DesignerProjectResolver.Resolve(
            DesignerProjectLoader.LoadFile(Path.Combine(directory, DesignerWorkspaceService.ManifestFileName)), [], directory);

    private static void RewriteManifest(string directory, Func<DesignerProjectManifest, DesignerProjectManifest> change)
    {
        string path = Path.Combine(directory, DesignerWorkspaceService.ManifestFileName);
        DesignerProjectManifest manifest = DesignerProjectLoader.LoadFile(path).Manifest!;
        File.WriteAllBytes(path, DesignerProjectCanonicalizer.Canonicalize(change(manifest)));
    }

    private static string CopyFixture(string fixture, string root, string name)
    {
        string destination = Path.Combine(root, name);
        foreach (string file in Directory.GetFiles(fixture, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(fixture, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return destination;
    }

    private static bool Has(IEnumerable<ProfileDiagnostic> diagnostics, string code, string? path = null) =>
        diagnostics.Any(item => item.Code == code && item.Severity == ProfileDiagnosticSeverity.Error &&
            (path is null || item.Path == path));

    private static string Messages(IEnumerable<ProfileDiagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
