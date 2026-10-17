// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public sealed record ResolvedResourceVariant
{
    public required string ResourceId { get; init; }

    public required string ResourceManifestSha256 { get; init; }

    public required ResourceVariant Variant { get; init; }

    public required ResourceKind Kind { get; init; }

    public required bool OutputVerified { get; init; }
}

public sealed record ResourceSurfaceResolutionResult
{
    public required IReadOnlyList<ResolvedResourceVariant> Variants { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class ResourceSurfaceResolver
{
    public static ResourceSurfaceResolutionResult Resolve(
        ProjectResourceSetLoadResult resources,
        DisplaySurfaceProfile surface)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(surface);
        return Resolve(resources, surface.PainterSurface.Format);
    }

    public static ResourceSurfaceResolutionResult Resolve(
        ProjectResourceSetLoadResult resources,
        SurfacePixelFormat format,
        IReadOnlyCollection<ResourceFormat>? allowedSourceFormats = null)
    {
        ArgumentNullException.ThrowIfNull(resources);

        List<ProfileDiagnostic> diagnostics = [.. resources.Diagnostics];
        List<ResolvedResourceVariant> variants = [];
        if (!resources.IsValid)
        {
            return Result(variants, diagnostics);
        }
        if (!Enum.IsDefined(format) ||
            allowedSourceFormats is not null && allowedSourceFormats.Any(item => !Enum.IsDefined(item)))
        {
            diagnostics.Add(Error("LVRM3102", "$.resourceFormats",
                "Resource selection requires a known painter format and known source formats."));
            return Result(variants, diagnostics);
        }

        foreach (LoadedProjectResource resource in resources.Resources.OrderBy(
            item => item.Manifest.Id, StringComparer.Ordinal))
        {
            ResourceVariant[] matches = [.. resource.Manifest.Variants.Where(variant =>
                variant.CompatiblePainterFormats.Contains(format) &&
                SupportsSourceFormat(format, variant.Output.Format) &&
                (allowedSourceFormats is null || allowedSourceFormats.Contains(variant.Output.Format)))];
            if (matches.Length != 1)
            {
                diagnostics.Add(Error(
                    "LVRM3101",
                    "$.resourceManifests",
                    $"Resource '{resource.Manifest.Id}' requires exactly one variant compatible with " +
                        $"painter format '{format}' and its allowed source formats; found {matches.Length}."));
                continue;
            }

            variants.Add(new ResolvedResourceVariant
            {
                ResourceId = resource.Manifest.Id,
                ResourceManifestSha256 = resource.ManifestSha256,
                Variant = matches[0],
                Kind = resource.Manifest.Kind,
                OutputVerified = resource.Artifacts.Any(artifact =>
                    artifact.Path == matches[0].Output.Path &&
                    artifact.Sha256 == matches[0].Output.Sha256 &&
                    artifact.ByteCount == matches[0].Output.ByteCount)
            });
        }
        return Result(variants, diagnostics);
    }

    public static bool SupportsSourceFormat(SurfacePixelFormat destination, ResourceFormat source)
    {
        // Every current painter destination uses the same bitmap decoder. A
        // compatibility label cannot introduce an unknown source representation.
        return Enum.IsDefined(destination) && ImageConversionRequestValidator.IsBitmapFormat(source);
    }

    private static ResourceSurfaceResolutionResult Result(
        IReadOnlyList<ResolvedResourceVariant> variants,
        IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new ResourceSurfaceResolutionResult
        {
            Variants = variants,
            Diagnostics = diagnostics
        };
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }
}
