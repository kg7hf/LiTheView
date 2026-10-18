// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Preview;

public static class PreviewLimits
{
    public const uint AbiVersion = 1;
    public const uint SchemaVersion = 1;
    public const int MaximumViews = 64;
    public const int MaximumInputs = 64;
    public const int MaximumDamageRegions = 8;
    public const int MaximumDiagnosticBytes = 512;
    public const int MaximumSnapshotBytes = 1_048_576;
    public const int MaximumSurfaceDimension = 4_096;
}

public enum PreviewPixelFormat : uint
{
    Rgb565 = 0,
    Rgb888 = 1,
    Xrgb8888 = 2,
    Argb8888 = 3,
    // Native packed storage: 2x2 red emitters per byte, or one red/red-yellow cell per byte.
    R2Packed2x2 = 4,
    Rrrr2222 = 5,
    Ryry2222 = 6
}

public enum PreviewShape : uint
{
    Rectangle = 0,
    Circle = 1
}

public enum PreviewQuantization : uint
{
    Native = 0,
    Rgb565 = 1,
    Red2Bpp = 2,
    RedYellow2Bpp = 3
}

public enum PreviewInputKind : uint
{
    PointerEvent = 0,
    Key = 1,
    Encoder = 2,
    PushButton = 3,
    Focus = 4,
    ElapsedTime = 5
}

public enum PreviewKey : int
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3,
    Select = 4,
    Back = 5
}

public enum PreviewPointerPhase : int
{
    Pressed = 0,
    Moved = 1,
    Released = 2,
    Cancelled = 3
}

public enum PreviewStatus : uint
{
    Ok = 0,
    InvalidArgument = 1,
    AbiMismatch = 2,
    SchemaMismatch = 3,
    SizeLimit = 4,
    UnsupportedFormat = 5,
    InvalidSnapshot = 6,
    MissingResource = 7,
    InvalidInput = 8,
    OutputTooSmall = 9,
    InternalError = 10
}

public enum PreviewRenderMode : uint
{
    FullFrame = 0,
    Tiled = 1
}

// Width and height are logical pixels, emitters (R2) or cells (RRRR/RYRY). StrideBytes
// advances one storage-block row; RGB formats have one-pixel blocks.
public readonly record struct PreviewSurface(
    PreviewPixelFormat PixelFormat,
    PreviewShape Shape,
    PreviewQuantization Quantization,
    int Width,
    int Height,
    int StrideBytes)
{
    public bool IsNativePacked => PixelFormat is PreviewPixelFormat.R2Packed2x2 or
        PreviewPixelFormat.Rrrr2222 or PreviewPixelFormat.Ryry2222;

    public int BlockWidth => PixelFormat == PreviewPixelFormat.R2Packed2x2 ? 2 : 1;

    public int BlockHeight => PixelFormat == PreviewPixelFormat.R2Packed2x2 ? 2 : 1;

    public int BytesPerBlock => PixelFormat switch
    {
        PreviewPixelFormat.Rgb565 => 2,
        PreviewPixelFormat.Rgb888 => 3,
        PreviewPixelFormat.Xrgb8888 or PreviewPixelFormat.Argb8888 => 4,
        PreviewPixelFormat.R2Packed2x2 or PreviewPixelFormat.Rrrr2222 or PreviewPixelFormat.Ryry2222 => 1,
        _ => 0
    };

    // Bytes in one storage-block row of the logical extent, excluding stride padding.
    public int RowBytes => checked(Width / BlockWidth * BytesPerBlock);

    public int RequiredFrameBytes()
    {
        if (Width <= 0 || Height <= 0 ||
            Width > PreviewLimits.MaximumSurfaceDimension ||
            Height > PreviewLimits.MaximumSurfaceDimension ||
            BytesPerBlock == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Preview dimensions or format are invalid.");
        }
        if (Width % BlockWidth != 0 || Height % BlockHeight != 0)
        {
            throw new ArgumentException("A native packed preview extent must contain whole storage blocks.", nameof(Width));
        }
        if (IsNativePacked && Quantization != PreviewQuantization.Native)
        {
            throw new ArgumentException("Native packed previews are rendered natively, never quantized.", nameof(Quantization));
        }
        if (StrideBytes < RowBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(StrideBytes), "Preview stride is smaller than one row.");
        }
        if (Shape == PreviewShape.Circle && Width != Height)
        {
            throw new ArgumentException("A circular preview surface must be square.", nameof(Shape));
        }
        return checked(StrideBytes * (Height / BlockHeight));
    }
}

public readonly record struct PreviewInput(
    PreviewInputKind Kind,
    int Value,
    int X = 0,
    int Y = 0,
    uint TimestampMilliseconds = 0,
    uint ContactId = 0);

public readonly record struct PreviewRect(int X, int Y, int Width, int Height);

public sealed record PreviewRenderRequest(
    PreviewSurface Surface,
    ReadOnlyMemory<byte> Snapshot,
    IReadOnlyList<PreviewInput>? Inputs = null,
    uint AbiVersion = PreviewLimits.AbiVersion,
    uint SchemaVersion = PreviewLimits.SchemaVersion,
    PreviewRenderMode RenderMode = PreviewRenderMode.FullFrame);

public sealed record PreviewFrame(
    ulong RequestId,
    PreviewSurface Surface,
    byte[] Pixels,
    IReadOnlyList<PreviewRect> DamageRegions,
    PreviewStatus Status,
    uint DiagnosticCode,
    string Diagnostic,
    bool AnimationRunning,
    uint NextWakeupMilliseconds,
    uint AcceptedInputCount,
    TimeSpan NativeRenderTime);

public sealed record PreviewWorkerCapabilities(
    ushort ProtocolMajor,
    ushort ProtocolMinor,
    uint AbiVersion,
    uint SchemaVersion,
    uint MaximumControlPayloadBytes,
    uint MaximumSnapshotBytes,
    uint MaximumSurfaceDimension,
    string WidgetCatalogSha256)
{
    // The worker can return frames inline on its output instead of through shared memory.
    public bool InlinePixels { get; init; }

    // The worker renders the native packed pixel formats directly into their storage.
    public bool SegmentedDisplays { get; init; }
    public bool NativePackedSurfaces { get; init; }
}

public sealed class PreviewProtocolException(string message) : InvalidOperationException(message)
{
}

public sealed class PreviewWorkerException(
    string message,
    ulong requestId,
    Exception? innerException = null) : InvalidOperationException(message, innerException)
{
    public ulong RequestId { get; } = requestId;
}
