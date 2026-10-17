// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Schema;

public sealed record WidgetSchemaCompilationResult
{
    public byte[]? CSharpUtf8 { get; init; }

    public byte[]? CppUtf8 { get; init; }

    public byte[]? DocumentationUtf8 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        CSharpUtf8 is not null &&
        CppUtf8 is not null &&
        DocumentationUtf8 is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}
