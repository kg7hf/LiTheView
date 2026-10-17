// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.Core.Projects;

// One resolved project for every manifest version. Display is the shared contract;
// Surface is the RGB editing surface that versions 1 and 2 lock and version 3 omits.
public sealed record ResolvedDesignerProject
{
    public required DesignerProjectManifest Manifest { get; init; }

    public required string ManifestSha256 { get; init; }

    public required DesignerDisplay Display { get; init; }

    public DisplaySurfaceProfile? Surface { get; init; }

    public string? SurfaceSha256 { get; init; }

    public required IReadOnlyList<LoadedProjectResource> Resources { get; init; }

    public required IReadOnlyList<ResolvedResourceVariant> ResourceVariants { get; init; }

    public required string ResolvedSha256 { get; init; }
}

public sealed record DesignerProjectResolutionResult
{
    public ResolvedDesignerProject? Project { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Project is not null && Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}
