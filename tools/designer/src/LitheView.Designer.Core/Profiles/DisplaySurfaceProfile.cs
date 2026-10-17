// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Serialization;

namespace LitheView.Designer.Core.Profiles;

public enum SurfaceShape
{
    Rectangle,
    Square,
    Circle
}

public enum SurfacePixelFormat
{
    Rgb565,
    Rgb888,
    Xrgb8888,
    Argb8888,
    R2Packed2x2,
    Rrrr2222,
    Ryry2222
}

public enum NativeColorModel
{
    Rgb,
    RedLuminanceSubpixels,
    RedYellowFullPixel
}

public enum NativeTransferFormat
{
    Rgb565,
    Rgb888Parallel,
    Argb8888,
    Packed8
}

public enum CoordinateOrigin
{
    TopLeft
}

public enum InputCapability
{
    Touch,
    Keyboard,
    Encoder,
    PushButton
}

public sealed record PhysicalSize
{
    public decimal? WidthMillimeters { get; init; }

    public decimal? HeightMillimeters { get; init; }

    public decimal? DiameterMillimeters { get; init; }
}

public sealed record PixelSize
{
    public required int Width { get; init; }

    public required int Height { get; init; }
}

public sealed record CoordinateSystemProfile
{
    public required CoordinateOrigin Origin { get; init; }

    public required int RotationDegrees { get; init; }
}

public sealed record PainterSurfaceProfile
{
    public required SurfacePixelFormat Format { get; init; }

    public required int ColorDepthBits { get; init; }

    public required int StorageBitsPerPixel { get; init; }
}

public sealed record NativeDisplayProfile
{
    public required NativeColorModel ColorModel { get; init; }

    public required int ColorDepthBits { get; init; }

    public required int TransferUnitBits { get; init; }

    public required NativeTransferFormat TransferFormat { get; init; }
}

public sealed record SafeAreaProfile
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }
}

public sealed record ProfileProvenance
{
    public required string Manufacturer { get; init; }

    public required string Model { get; init; }

    public required string SourceUrl { get; init; }

    public required string SourceDocument { get; init; }

    public required string? SourceSha256 { get; init; }

    public required string Notes { get; init; }
}

public sealed record DisplaySurfaceProfile
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public required int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string CompatibilityFamily { get; init; }

    public required SurfaceShape Shape { get; init; }

    public required PhysicalSize PhysicalSize { get; init; }

    public required PixelSize PixelSize { get; init; }

    public required CoordinateSystemProfile CoordinateSystem { get; init; }

    public required PainterSurfaceProfile PainterSurface { get; init; }

    public required NativeDisplayProfile NativeDisplay { get; init; }

    public required SafeAreaProfile SafeArea { get; init; }

    public required InputCapability[] InputCapabilities { get; init; }

    public required ProfileProvenance Provenance { get; init; }
}
