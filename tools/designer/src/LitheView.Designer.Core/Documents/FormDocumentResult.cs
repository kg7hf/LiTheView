// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public sealed record FormDocumentLoadResult
{
    public FormDocument? Document { get; init; }

    public byte[]? CanonicalXmlUtf8 { get; init; }

    public string? Sha256 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Document is not null &&
        CanonicalXmlUtf8 is not null &&
        Sha256 is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record FormResolutionResult
{
    public ResolvedFormDocument? Document { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Document is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record FormXmlWriteResult
{
    public byte[]? Utf8 { get; init; }

    public string? Sha256 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Utf8 is not null &&
        Sha256 is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record FormCppCompilationResult
{
    public byte[]? HeaderUtf8 { get; init; }

    public byte[]? IntegrationUtf8 { get; init; }

    public string? SourceXmlSha256 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        HeaderUtf8 is not null &&
        IntegrationUtf8 is not null &&
        SourceXmlSha256 is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record PreservedRegionMergeResult
{
    public byte[]? Utf8 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Utf8 is not null && Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record FormSurfaceValidationResult
{
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}
