// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Simulation;

public sealed record SimulatorExportResult
{
    public string? OutputDirectory { get; init; }
    public string? ManifestPath { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => OutputDirectory is not null && ManifestPath is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record SimulatorBuildResult
{
    public string? ExecutablePath { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => ExecutablePath is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

// This describes generated inputs, not a target budget or a claim about hardware.
public sealed record SimulatorProjectManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string GenerationStatus { get; init; } = "incomplete";
    public string SurfaceStatus { get; init; } = "passed";
    public string TargetStatus { get; init; } = "unevaluated";
    public required string DestinationFormat { get; init; }
    public required string FormSha256 { get; init; }
    public required string DisplayDefinitionSha256 { get; init; }
    public required string LogicalInputSha256 { get; init; }
    public required SortedDictionary<string, string> GeneratedFiles { get; init; }
    public string[] UserFiles { get; init; } = ["application.hpp"];
}

internal static class SimulatorJson
{
    internal const string ManifestName = "simulation.json";
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static ProfileDiagnostic Error(string code, string path, string message) =>
        new(code, ProfileDiagnosticSeverity.Error, path, message);
}
