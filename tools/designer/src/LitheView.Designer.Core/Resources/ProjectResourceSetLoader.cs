// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Resources;

public sealed record LoadedProjectResource
{
    public required string RelativePath { get; init; }

    public required ResourceManifest Manifest { get; init; }

    public required string ManifestSha256 { get; init; }

    public required IReadOnlyList<VerifiedResourceArtifact> Artifacts { get; init; }
}

public sealed record ProjectResourceSetLoadResult
{
    public required IReadOnlyList<LoadedProjectResource> Resources { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class ProjectResourceSetLoader
{
    public static ProjectResourceSetLoadResult Load(
        DesignerProjectManifest project,
        string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Load(project.ResourceManifests, projectRoot);
    }

    public static ProjectResourceSetLoadResult Load(
        IEnumerable<string> resourceManifestPaths,
        string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(resourceManifestPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        List<ProfileDiagnostic> diagnostics = [];
        List<LoadedProjectResource> resources = [];
        HashSet<string> resourceIds = new(StringComparer.Ordinal);
        foreach (string relativePath in resourceManifestPaths.Order(StringComparer.Ordinal))
        {
            if (!TryResolveContainedPath(projectRoot, relativePath, out string manifestPath))
            {
                diagnostics.Add(Error(
                    "LVRM3001",
                    "$.resourceManifests",
                    $"Resource-manifest path '{relativePath}' resolves outside the project root."));
                continue;
            }

            ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(manifestPath);
            diagnostics.AddRange(loaded.Diagnostics);
            if (!loaded.IsValid || loaded.Manifest is null || loaded.Sha256 is null)
            {
                continue;
            }
            if (!resourceIds.Add(loaded.Manifest.Id))
            {
                diagnostics.Add(Error(
                    "LVRM3002",
                    "$.resourceManifests",
                    $"Resource ID '{loaded.Manifest.Id}' is declared more than once."));
                continue;
            }

            ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
                loaded.Manifest,
                projectRoot);
            diagnostics.AddRange(verification.Diagnostics);
            if (!verification.IsValid)
            {
                continue;
            }
            resources.Add(new LoadedProjectResource
            {
                RelativePath = relativePath,
                Manifest = loaded.Manifest,
                ManifestSha256 = loaded.Sha256,
                Artifacts = verification.Artifacts
            });
        }

        return new ProjectResourceSetLoadResult
        {
            Resources = resources,
            Diagnostics = diagnostics
        };
    }

    private static bool TryResolveContainedPath(
        string projectRoot,
        string relativePath,
        out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (!ProfileRules.IsSafeProjectPath(relativePath))
        {
            return false;
        }
        try
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            resolvedPath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }
}
