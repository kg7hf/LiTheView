// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public enum ProfileDiagnosticSeverity
{
    Error,
    Warning
}

public sealed record ProfileDiagnostic(
    string Code,
    ProfileDiagnosticSeverity Severity,
    string Path,
    string Message);
