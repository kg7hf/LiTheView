// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Serialization;

namespace LitheView.Designer.Core.Projects;

public sealed record LockedProfileReference
{
    public required string Id { get; init; }

    public required string Sha256 { get; init; }
}

public sealed record ProjectSchemaCompatibility
{
    // Absent for version-3 native display projects, which have no editing surface.
    public int? DisplaySurface { get; init; }

    public required int ProjectManifest { get; init; }

    public int? DisplayDefinition { get; init; }

    public int? LogicalInput { get; init; }

    public required int Form { get; init; }

    public required int ResourceManifest { get; init; }

    public required int ExtensionManifest { get; init; }
}

public sealed record ProjectExtensionReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string PackagePath { get; init; }

    public required string Sha256 { get; init; }

    public required bool Required { get; init; }
}

public sealed record DesignerProjectManifest
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public required int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required ProjectSchemaCompatibility SchemaCompatibility { get; init; }

    // Versions 1 and 2 lock an RGB editing surface. Version 3 locks a native packed
    // display definition directly and has no editing surface or RGB staging contract.
    public LockedProfileReference? SurfaceProfile { get; init; }

    // Version 2 adds product snapshots. The surface remains an editing projection;
    // its historical input hints do not override the separate logical input.
    public LockedProfileReference? DisplayDefinition { get; init; }

    public LockedProfileReference? LogicalInput { get; init; }

    public required string[] Forms { get; init; }

    public required string[] ResourceManifests { get; init; }

    public required ProjectExtensionReference[] Extensions { get; init; }
}
