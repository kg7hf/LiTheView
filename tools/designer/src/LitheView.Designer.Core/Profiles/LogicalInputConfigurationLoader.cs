// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public static class LogicalInputConfigurationLoader
{
    public static LogicalInputConfigurationLoadResult LoadFile(string path)
    {
        List<ProfileDiagnostic> diagnostics = [];
        byte[]? bytes = DisplayDefinitionJson.ReadFile(path, "LVIN", diagnostics);
        return bytes is null ? new() { Diagnostics = diagnostics } : Load(bytes);
    }

    public static LogicalInputConfigurationLoadResult Load(ReadOnlyMemory<byte> utf8)
    {
        List<ProfileDiagnostic> diagnostics = [];
        LogicalInputConfigurationProfile? configuration = DisplayDefinitionJson.Read<LogicalInputConfigurationProfile>(
            utf8, "LVIN", diagnostics);
        if (configuration is null)
        {
            return new() { Diagnostics = diagnostics };
        }
        diagnostics.AddRange(Validate(configuration));
        if (diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return new() { Diagnostics = diagnostics };
        }
        byte[] canonical = LogicalInputConfigurationCanonicalizer.Canonicalize(configuration);
        return new()
        {
            Configuration = configuration,
            CanonicalUtf8 = canonical,
            Sha256 = DisplaySurfaceCanonicalizer.ComputeSha256(canonical),
            Diagnostics = diagnostics
        };
    }

    public static IReadOnlyList<ProfileDiagnostic> Validate(LogicalInputConfigurationProfile configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        List<ProfileDiagnostic> diagnostics = [];
        if (configuration.SchemaVersion != 1 || string.IsNullOrWhiteSpace(configuration.Schema))
        {
            DisplayDefinitionJson.Error(diagnostics, "LVIN1001", "$.schemaVersion", "Logical input requires schema version 1.");
        }
        if (!ProfileRules.IsValidIdentifier(configuration.Id))
        {
            DisplayDefinitionJson.Error(diagnostics, "LVIN1002", "$.id", "Logical input ID must be a bounded lowercase identifier.");
        }
        return diagnostics;
    }
}
