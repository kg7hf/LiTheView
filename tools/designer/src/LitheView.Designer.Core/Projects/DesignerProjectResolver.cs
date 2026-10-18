// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.Core.Projects;

public static class DesignerProjectResolver
{
    public static DesignerProjectResolutionResult Resolve(
        DesignerProjectLoadResult project,
        IEnumerable<DisplaySurfaceLoadResult> surfaces)
    {
        return Resolve(project, surfaces, resources: null, projectRoot: null);
    }

    public static DesignerProjectResolutionResult Resolve(
        DesignerProjectLoadResult project,
        IEnumerable<DisplaySurfaceLoadResult> surfaces,
        string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ProjectResourceSetLoadResult? resources = project.Manifest is null
            ? null
            : ProjectResourceSetLoader.Load(project.Manifest, projectRoot);
        return Resolve(project, surfaces, resources, projectRoot);
    }

    private static DesignerProjectResolutionResult Resolve(
        DesignerProjectLoadResult project,
        IEnumerable<DisplaySurfaceLoadResult> surfaces,
        ProjectResourceSetLoadResult? resources,
        string? projectRoot)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(surfaces);

        List<ProfileDiagnostic> diagnostics = [.. project.Diagnostics];
        if (!project.IsValid || project.Manifest is null || project.Sha256 is null)
        {
            return Failed(diagnostics);
        }
        if (project.Manifest.SchemaVersion == 3)
        {
            // A native display project never consults the editing-surface catalog.
            return ResolveNativeDisplay(project.Manifest, project.Sha256, resources, projectRoot, diagnostics);
        }
        if (project.Manifest.SchemaVersion == 4)
        {
            AddError(diagnostics, "LVSG1400", "$", "This is a segmented project. Use the segmented workspace in the VS Code designer or segment CLI; the raster/desktop editor cannot edit it.");
            return Failed(diagnostics);
        }

        Dictionary<string, DisplaySurfaceLoadResult> surfaceCatalog =
            BuildSurfaceCatalog(surfaces, diagnostics);
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        DisplaySurfaceLoadResult? surface = ResolveSurface(
            project.Manifest.SurfaceProfile!,
            surfaceCatalog,
            diagnostics);
        if (surface is null || surface.Profile is null || surface.Sha256 is null)
        {
            return Failed(diagnostics);
        }

        DisplayDefinitionProfile? definition = null;
        DesignerDisplay display;
        if (project.Manifest.SchemaVersion == 2)
        {
            if (projectRoot is null)
            {
                AddError(diagnostics, "LVDP1007", "$",
                    "Version-2 project resolution requires its directory to validate the locked display/input snapshots.");
                return Failed(diagnostics);
            }
            DesignerProductResolutionResult snapshots = DesignerProductResolver.ResolveSnapshots(
                project.Manifest, surface, projectRoot);
            diagnostics.AddRange(snapshots.Diagnostics);
            if (HasErrors(diagnostics))
            {
                return Failed(diagnostics);
            }
            definition = snapshots.Definition!.Profile!;
            display = DesignerDisplay.FromDefinition(definition, snapshots.Definition.Sha256!,
                snapshots.Input!.Configuration!, snapshots.Input.Sha256!, surface.Profile);
        }
        else
        {
            display = AdaptLegacyDisplay(surface);
        }

        IReadOnlyList<LoadedProjectResource> resolvedResources = [];
        IReadOnlyList<ResolvedResourceVariant> resourceVariants = [];
        if (project.Manifest.ResourceManifests.Length != 0 && resources is null)
        {
            AddError(
                diagnostics,
                "LVRP1601",
                "$.resourceManifests",
                "Project resource manifests require a contained project root during resolution.");
            return Failed(diagnostics);
        }
        if (resources is not null)
        {
            ResourceSurfaceResolutionResult resourceResolution = ResourceSurfaceResolver.Resolve(
                resources,
                surface.Profile.PainterSurface.Format,
                definition?.ResourceFormats);
            diagnostics.AddRange(resourceResolution.Diagnostics);
            if (HasErrors(diagnostics))
            {
                return Failed(diagnostics);
            }
            resolvedResources = resources.Resources;
            resourceVariants = resourceResolution.Variants;
        }

        string resolvedSha256 = ComputeResolvedSha256(
            1,
            project.Manifest.Id,
            project.Sha256,
            [("surfaceId", surface.Profile.Id), ("surfaceSha256", surface.Sha256)],
            resourceVariants);
        return new DesignerProjectResolutionResult
        {
            Project = new ResolvedDesignerProject
            {
                Manifest = project.Manifest,
                ManifestSha256 = project.Sha256,
                Display = display,
                Surface = surface.Profile,
                SurfaceSha256 = surface.Sha256,
                Resources = resolvedResources,
                ResourceVariants = resourceVariants,
                ResolvedSha256 = resolvedSha256
            },
            Diagnostics = diagnostics
        };
    }

    private static DesignerProjectResolutionResult ResolveNativeDisplay(
        DesignerProjectManifest manifest,
        string manifestSha256,
        ProjectResourceSetLoadResult? resources,
        string? projectRoot,
        List<ProfileDiagnostic> diagnostics)
    {
        if (projectRoot is null || resources is null)
        {
            AddError(diagnostics, "LVDP1007", "$",
                "Version-3 project resolution requires its directory to validate the locked display/input snapshots.");
            return Failed(diagnostics);
        }
        DesignerProductResolutionResult snapshots = DesignerProductResolver.ResolveNativeSnapshots(
            manifest, projectRoot);
        diagnostics.AddRange(snapshots.Diagnostics);
        if (HasErrors(diagnostics) ||
            snapshots.Definition is not { Profile: { } definition, Sha256: { } definitionSha256 } ||
            snapshots.Input is not { Configuration: { } input, Sha256: { } inputSha256 })
        {
            return Failed(diagnostics);
        }

        ResourceSurfaceResolutionResult resourceResolution = ResourceSurfaceResolver.Resolve(
            resources,
            definition.Format,
            definition.ResourceFormats);
        diagnostics.AddRange(resourceResolution.Diagnostics);
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        // Identity version 2 locks the definition and input instead of an editing surface.
        string resolvedSha256 = ComputeResolvedSha256(
            2,
            manifest.Id,
            manifestSha256,
            [
                ("displayDefinitionId", definition.Id),
                ("displayDefinitionSha256", definitionSha256),
                ("logicalInputId", input.Id),
                ("logicalInputSha256", inputSha256)
            ],
            resourceResolution.Variants);
        return new DesignerProjectResolutionResult
        {
            Project = new ResolvedDesignerProject
            {
                Manifest = manifest,
                ManifestSha256 = manifestSha256,
                Display = DesignerDisplay.FromDefinition(definition, definitionSha256, input, inputSha256,
                    editingSurface: null),
                Resources = resources.Resources,
                ResourceVariants = resourceResolution.Variants,
                ResolvedSha256 = resolvedSha256
            },
            Diagnostics = diagnostics
        };
    }

    // Version 1 has no snapshots. Its display uses the same explicit adaptation the product
    // check reports, without adding diagnostics here or rewriting the project; a legacy surface
    // that no compiled definition can represent (for example a rotated one) stays editable.
    private static DesignerDisplay AdaptLegacyDisplay(DisplaySurfaceLoadResult surface)
    {
        LogicalInputConfigurationLoadResult input = DesignerProductResolver.DefaultInput(surface.Profile!);
        DisplayDefinitionLoadResult adapted = DisplayDefinitionProfileLoader.AdaptV1(surface);
        return adapted is { IsValid: true, Profile: { } definition, Sha256: { } definitionSha256 }
            ? DesignerDisplay.FromDefinition(definition, definitionSha256, input.Configuration!, input.Sha256!,
                surface.Profile)
            : DesignerDisplay.FromEditingSurface(surface.Profile!, input.Configuration!, input.Sha256!);
    }

    private static Dictionary<string, DisplaySurfaceLoadResult> BuildSurfaceCatalog(
        IEnumerable<DisplaySurfaceLoadResult> surfaces,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, DisplaySurfaceLoadResult> catalog = new(StringComparer.Ordinal);
        foreach (DisplaySurfaceLoadResult surface in surfaces)
        {
            diagnostics.AddRange(surface.Diagnostics);
            if (!surface.IsValid || surface.Profile is null)
            {
                continue;
            }
            if (!catalog.TryAdd(surface.Profile.Id, surface))
            {
                AddError(
                    diagnostics,
                    "LVRP1001",
                    "surfaceCatalog",
                    $"Display-surface ID '{surface.Profile.Id}' occurs more than once.");
            }
        }
        return catalog;
    }

    private static DisplaySurfaceLoadResult? ResolveSurface(
        LockedProfileReference reference,
        Dictionary<string, DisplaySurfaceLoadResult> catalog,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!catalog.TryGetValue(reference.Id, out DisplaySurfaceLoadResult? surface))
        {
            AddError(
                diagnostics,
                "LVRP1101",
                "$.surfaceProfile.id",
                $"Display-surface profile '{reference.Id}' was not found in the supplied catalog.");
            return null;
        }
        if (surface.Sha256 != reference.Sha256)
        {
            AddError(
                diagnostics,
                "LVRP1102",
                "$.surfaceProfile.sha256",
                $"Display-surface profile '{reference.Id}' does not match its locked hash.");
            return null;
        }
        return surface;
    }

    private static string ComputeResolvedSha256(
        int identityVersion,
        string projectId,
        string manifestSha256,
        IEnumerable<(string Name, string Value)> display,
        IEnumerable<ResolvedResourceVariant> resources)
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", identityVersion);
            writer.WriteString("projectId", projectId);
            writer.WriteString("manifestSha256", manifestSha256);
            foreach ((string name, string value) in display)
            {
                writer.WriteString(name, value);
            }
            ResolvedResourceVariant[] orderedResources = [.. resources.OrderBy(
                item => item.ResourceId,
                StringComparer.Ordinal)];
            if (orderedResources.Length != 0)
            {
                writer.WritePropertyName("resources");
                writer.WriteStartArray();
                foreach (ResolvedResourceVariant resource in orderedResources)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", resource.ResourceId);
                    writer.WriteString("manifestSha256", resource.ResourceManifestSha256);
                    writer.WriteString("variantId", resource.Variant.Id);
                    writer.WriteString("outputSha256", resource.Variant.Output.Sha256);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        return DisplaySurfaceCanonicalizer.ComputeSha256(output.ToArray());
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private static DesignerProjectResolutionResult Failed(
        IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new DesignerProjectResolutionResult
        {
            Project = null,
            Diagnostics = diagnostics
        };
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }
}
