// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Serialization;

namespace LitheView.Designer.Core.Profiles;

public enum DisplayAddressSpace
{
    Pixels,
    Cells,
    Emitters
}

public enum DisplayChannelModel
{
    Rgb,
    Red,
    RedYellow
}

public enum DisplayFieldChannel
{
    Red,
    Green,
    Blue,
    Alpha,
    Yellow,
    Unused
}

public enum DisplaySourceStatus
{
    SoftwareContract,
    Unverified,
    Verified
}

public sealed record DisplayDefinitionGeometry
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required SurfaceShape Shape { get; init; }
    public required int RotationDegrees { get; init; }
    public required SafeAreaProfile SafeArea { get; init; }
}

public sealed record DisplayStorageBlock
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Bytes { get; init; }
}

public sealed record DisplayFieldPosition
{
    public required int X { get; init; }
    public required int Y { get; init; }
}

// Field order and positions are part of the format-derived storage contract.
public sealed record DisplayField
{
    public required DisplayFieldChannel Channel { get; init; }
    public required int BitOffset { get; init; }
    public required int BitWidth { get; init; }
    public required DisplayFieldPosition Position { get; init; }
}

public sealed record DisplayComposition
{
    public required bool SourceAlpha { get; init; }
    public required bool Coverage { get; init; }
    public required bool SourceOver { get; init; }
    public required bool DestinationAlpha { get; init; }
}

public sealed record DisplayDefinitionSource
{
    public required DisplaySourceStatus Status { get; init; }
    public required string Reference { get; init; }
    public required string? Sha256 { get; init; }
}

public sealed record DisplayDefinitionProfile
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required DisplayDefinitionGeometry Geometry { get; init; }
    public required DisplayAddressSpace AddressSpace { get; init; }
    public required SurfacePixelFormat Format { get; init; }
    public required DisplayStorageBlock StorageBlock { get; init; }
    public required DisplayChannelModel ChannelModel { get; init; }
    public required DisplayField[] Fields { get; init; }
    public required DisplayComposition Composition { get; init; }
    public required ResourceFormat[] ResourceFormats { get; init; }
    public required DisplayDefinitionSource Source { get; init; }
}

public sealed record DisplayDefinitionLoadResult
{
    public DisplayDefinitionProfile? Profile { get; init; }
    public byte[]? CanonicalUtf8 { get; init; }
    public string? Sha256 { get; init; }
    public DisplaySurfaceProfile? LegacySurface { get; init; }
    public string? LegacySurfaceSha256 { get; init; }
    public bool RequiresMigration => LegacySurface is not null;
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => Profile is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record LogicalInputConfigurationProfile
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name",
        Justification = "Pointer names the logical pointing-event capability, not a storage or pointer type.")]
    public required bool Pointer { get; init; }
    public required bool Keys { get; init; }
    public required bool Text { get; init; }
    public required bool Encoder { get; init; }
    public required bool Select { get; init; }
}

public sealed record LogicalInputConfigurationLoadResult
{
    public LogicalInputConfigurationProfile? Configuration { get; init; }
    public byte[]? CanonicalUtf8 { get; init; }
    public string? Sha256 { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => Configuration is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}
