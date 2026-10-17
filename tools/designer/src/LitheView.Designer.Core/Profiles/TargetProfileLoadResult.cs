// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public sealed record TargetProfileLoadResult
{
    public TargetProfile? Profile { get; init; }

    public byte[]? CanonicalUtf8 { get; init; }

    public string? Sha256 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Profile is not null && Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}
