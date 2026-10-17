// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Serialization;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public enum ResourceKind
{
    Font,
    Image,
    Icon,
    Animation
}

public enum ResourcePolicyAction
{
    Allow,
    Warning,
    Error
}

public enum ResourceOwnership
{
    Application,
    Library,
    Provider
}

public enum ResourceLifetime
{
    Static,
    CallerOwned,
    ProviderManaged
}

public sealed record ResourceLicense
{
    public required string? SpdxId { get; init; }

    public required string? LicenseTextPath { get; init; }

    public required string? LicenseTextSha256 { get; init; }

    public required string? Attribution { get; init; }

    public required bool RedistributionApproved { get; init; }
}

public sealed record ResourceProvenance
{
    public required string Origin { get; init; }

    public required string? Author { get; init; }

    public required string? SourceUri { get; init; }

    public required string? Notes { get; init; }

    public required ResourceLicense? License { get; init; }
}

public sealed record ResourceSource
{
    public required string Path { get; init; }

    public required string Sha256 { get; init; }

    public required long ByteCount { get; init; }

    public required string MediaType { get; init; }

    public required ResourceProvenance? Provenance { get; init; }
}

public sealed record ResourceCompliancePolicy
{
    public required ResourcePolicyAction MissingProvenance { get; init; }

    public required ResourcePolicyAction MissingLicense { get; init; }

    public required ResourcePolicyAction UnapprovedRedistribution { get; init; }
}

public sealed record ResourceConverter
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required Dictionary<string, string> Options { get; init; }
}

public sealed record ResourceOutput
{
    public required string Path { get; init; }

    public required string Sha256 { get; init; }

    public required long ByteCount { get; init; }

    public required ResourceFormat Format { get; init; }

    public required PixelSize? PixelSize { get; init; }

    public required int FrameCount { get; init; }
}

public sealed record ResourceIntegration
{
    public required ResourceOwnership Ownership { get; init; }

    public required ResourceLifetime Lifetime { get; init; }

    public required string? ProviderId { get; init; }

    public required string? Notes { get; init; }
}

public sealed record FontVariantMetadata
{
    public required string Family { get; init; }

    public required string Style { get; init; }

    public required int Weight { get; init; }

    public required int PixelSize { get; init; }

    public required ResourceFormat Coverage { get; init; }

    public required string Repertoire { get; init; }

    public required string Fallback { get; init; }

    public required int GlyphCount { get; init; }

    public required long CoverageBytes { get; init; }

    public required long RecordBytes { get; init; }

    public required long EstimatedRomBytes { get; init; }

    public required string DataChecksumFnv1a32 { get; init; }

    public required int LineHeight { get; init; }

    public required int Baseline { get; init; }

    public required int UnderlinePosition { get; init; }

    public required int UnderlineThickness { get; init; }
}

public sealed record ResourceCropRectangle
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }
}

public sealed record ImageVariantMetadata
{
    public required bool Tintable { get; init; }

    public required PixelSize SourcePixelSize { get; init; }

    public required ResourceCropRectangle Crop { get; init; }

    public required ImageScaleMode ScaleMode { get; init; }

    public required ImageAlphaMode AlphaMode { get; init; }

    public required int AlphaThreshold { get; init; }

    public required ImageDitherMode Dither { get; init; }

    public required string TintArgb { get; init; }

    public required string MatteArgb { get; init; }

    public required string PaletteArgb { get; init; }

    public required int StrideBytes { get; init; }

    public required long PixelBytes { get; init; }

    public required long PaletteBytes { get; init; }

    public required long EstimatedRomBytes { get; init; }

    public required string PixelChecksumFnv1a32 { get; init; }

    public required string PaletteChecksumFnv1a32 { get; init; }

    public required string DataChecksumFnv1a32 { get; init; }
}

public sealed record AnimationVariantMetadata
{
    public required bool Tintable { get; init; }

    public required PixelSize SourcePixelSize { get; init; }

    public required ResourceCropRectangle SourceGrid { get; init; }

    public required PixelSize SourceFramePixelSize { get; init; }

    public required int Columns { get; init; }

    public required ImageScaleMode ScaleMode { get; init; }

    public required ImageAlphaMode AlphaMode { get; init; }

    public required int AlphaThreshold { get; init; }

    public required ImageDitherMode Dither { get; init; }

    public required string TintArgb { get; init; }

    public required string MatteArgb { get; init; }

    public required string PaletteArgb { get; init; }

    public required int FrameIntervalMilliseconds { get; init; }

    public required int RepeatIndex { get; init; }

    public required bool Autoplay { get; init; }

    public required int StrideBytes { get; init; }

    public required long FramePixelBytes { get; init; }

    public required long PixelBytes { get; init; }

    public required long PaletteBytes { get; init; }

    public required long EstimatedRomBytes { get; init; }

    public required string PixelChecksumFnv1a32 { get; init; }

    public required string PaletteChecksumFnv1a32 { get; init; }

    public required string DataChecksumFnv1a32 { get; init; }
}

public sealed record ResourceVariant
{
    public required string Id { get; init; }

    public required SurfacePixelFormat[] CompatiblePainterFormats { get; init; }

    public required ResourceConverter Converter { get; init; }

    public required ResourceOutput Output { get; init; }

    public FontVariantMetadata? Font { get; init; }

    public ImageVariantMetadata? Image { get; init; }

    public AnimationVariantMetadata? Animation { get; init; }

    public required ResourceIntegration Integration { get; init; }
}

public sealed record ResourceManifest
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public required int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required ResourceKind Kind { get; init; }

    public required ResourceSource Source { get; init; }

    public required ResourceCompliancePolicy Compliance { get; init; }

    public required ResourceVariant[] Variants { get; init; }
}
