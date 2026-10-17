// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

public sealed record DisplaySurfaceCatalogLoadResult
{
    public required IReadOnlyList<DisplaySurfaceLoadResult> Surfaces { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error) &&
        Surfaces.All(surface => surface.IsValid);
}

public sealed record ProfileCatalogLoadResult
{
    public required IReadOnlyList<DisplaySurfaceLoadResult> Surfaces { get; init; }

    public required IReadOnlyList<TargetProfileLoadResult> Targets { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error) &&
        Surfaces.All(surface => surface.IsValid) &&
        Targets.All(target => target.IsValid);
}

public static class ProfileCatalogLoader
{
    private const int MaximumProfilesPerCatalog = 256;

    public static DisplaySurfaceCatalogLoadResult LoadSurfaces(string surfaceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceDirectory);

        List<ProfileDiagnostic> diagnostics = [];
        string[] surfacePaths = EnumerateCatalog(
            surfaceDirectory,
            "surfaceCatalog",
            diagnostics);
        DisplaySurfaceLoadResult[] surfaces = [.. surfacePaths.Select(DisplaySurfaceProfileLoader.LoadFile)];
        AddDuplicateIds(
            surfaces.Where(surface => surface.Profile is not null).Select(surface => surface.Profile!.Id),
            "surfaceCatalog",
            "LVRP0003",
            diagnostics);
        return new DisplaySurfaceCatalogLoadResult
        {
            Surfaces = surfaces,
            Diagnostics = diagnostics
        };
    }

    public static ProfileCatalogLoadResult Load(
        string surfaceDirectory,
        string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        List<ProfileDiagnostic> diagnostics = [];
        string[] surfacePaths = EnumerateCatalog(
            surfaceDirectory,
            "surfaceCatalog",
            diagnostics);
        string[] targetPaths = EnumerateCatalog(
            targetDirectory,
            "targetCatalog",
            diagnostics);
        if (diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return new ProfileCatalogLoadResult
            {
                Surfaces = [],
                Targets = [],
                Diagnostics = diagnostics
            };
        }

        DisplaySurfaceLoadResult[] surfaces = [.. surfacePaths.Select(DisplaySurfaceProfileLoader.LoadFile)];
        TargetProfileLoadResult[] targets = [.. targetPaths.Select(TargetProfileLoader.LoadFile)];
        AddDuplicateIds(
            surfaces.Where(surface => surface.Profile is not null).Select(surface => surface.Profile!.Id),
            "surfaceCatalog",
            "LVRP0003",
            diagnostics);
        AddDuplicateIds(
            targets.Where(target => target.Profile is not null).Select(target => target.Profile!.Id),
            "targetCatalog",
            "LVRP0004",
            diagnostics);
        return new ProfileCatalogLoadResult
        {
            Surfaces = surfaces,
            Targets = targets,
            Diagnostics = diagnostics
        };
    }

    private static void AddDuplicateIds(
        IEnumerable<string> ids,
        string path,
        string code,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!seen.Add(id))
            {
                diagnostics.Add(
                    new ProfileDiagnostic(
                        code,
                        ProfileDiagnosticSeverity.Error,
                        path,
                        $"Profile ID '{id}' occurs more than once in the catalog."));
            }
        }
    }

    private static string[] EnumerateCatalog(
        string directory,
        string diagnosticPath,
        List<ProfileDiagnostic> diagnostics)
    {
        try
        {
            string[] paths = [.. Directory
                .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Take(MaximumProfilesPerCatalog + 1)];
            if (paths.Length > MaximumProfilesPerCatalog)
            {
                diagnostics.Add(
                    new ProfileDiagnostic(
                        "LVRP0001",
                        ProfileDiagnosticSeverity.Error,
                        diagnosticPath,
                        $"A profile catalog may contain no more than {MaximumProfilesPerCatalog} files."));
                return [];
            }
            return paths;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVRP0002",
                    ProfileDiagnosticSeverity.Error,
                    diagnosticPath,
                    $"Unable to enumerate profile catalog '{directory}': {exception.Message}"));
            return [];
        }
    }
}
