// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

public sealed record DesignerProjectLoadResult
{
    public DesignerProjectManifest? Manifest { get; init; }

    public byte[]? CanonicalUtf8 { get; init; }

    public string? Sha256 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Manifest is not null && Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}
