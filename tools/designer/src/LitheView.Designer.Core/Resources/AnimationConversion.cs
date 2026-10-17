// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public sealed record AnimationConversionRequest
{
    public required string InputSpriteSheetPath { get; init; }

    public required string OutputSourcePath { get; init; }

    public required string OutputMetricsPath { get; init; }

    public required string Symbol { get; init; }

    public required string EntryPoint { get; init; }

    public required bool Tintable { get; init; }

    public required ResourceFormat Format { get; init; }

    public required int SheetX { get; init; }

    public required int SheetY { get; init; }

    public required int SourceFrameWidth { get; init; }

    public required int SourceFrameHeight { get; init; }

    public required int Columns { get; init; }

    public required int FrameCount { get; init; }

    public required int OutputWidth { get; init; }

    public required int OutputHeight { get; init; }

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
}

public sealed record AnimationConversionMetrics
{
    [JsonPropertyName("schema_version")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("converter_id")]
    public required string ConverterId { get; init; }

    [JsonPropertyName("converter_version")]
    public required string ConverterVersion { get; init; }

    [JsonPropertyName("tintable")]
    public required bool Tintable { get; init; }

    [JsonPropertyName("format")]
    public required string Format { get; init; }

    [JsonPropertyName("source_width")]
    public required int SourceWidth { get; init; }

    [JsonPropertyName("source_height")]
    public required int SourceHeight { get; init; }

    [JsonPropertyName("sheet_x")]
    public required int SheetX { get; init; }

    [JsonPropertyName("sheet_y")]
    public required int SheetY { get; init; }

    [JsonPropertyName("source_frame_width")]
    public required int SourceFrameWidth { get; init; }

    [JsonPropertyName("source_frame_height")]
    public required int SourceFrameHeight { get; init; }

    [JsonPropertyName("columns")]
    public required int Columns { get; init; }

    [JsonPropertyName("frame_count")]
    public required int FrameCount { get; init; }

    [JsonPropertyName("output_width")]
    public required int OutputWidth { get; init; }

    [JsonPropertyName("output_height")]
    public required int OutputHeight { get; init; }

    [JsonPropertyName("stride_bytes")]
    public required int StrideBytes { get; init; }

    [JsonPropertyName("scale_mode")]
    public required string ScaleMode { get; init; }

    [JsonPropertyName("alpha_mode")]
    public required string AlphaMode { get; init; }

    [JsonPropertyName("alpha_threshold")]
    public required int AlphaThreshold { get; init; }

    [JsonPropertyName("dither")]
    public required string Dither { get; init; }

    [JsonPropertyName("tint_argb")]
    public required string TintArgb { get; init; }

    [JsonPropertyName("matte_argb")]
    public required string MatteArgb { get; init; }

    [JsonPropertyName("palette_argb")]
    public required string PaletteArgb { get; init; }

    [JsonPropertyName("frame_interval_milliseconds")]
    public required int FrameIntervalMilliseconds { get; init; }

    [JsonPropertyName("repeat_index")]
    public required int RepeatIndex { get; init; }

    [JsonPropertyName("autoplay")]
    public required bool Autoplay { get; init; }

    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    [JsonPropertyName("entry_point")]
    public required string EntryPoint { get; init; }

    [JsonPropertyName("frame_pixel_bytes")]
    public required long FramePixelBytes { get; init; }

    [JsonPropertyName("pixel_bytes")]
    public required long PixelBytes { get; init; }

    [JsonPropertyName("palette_bytes")]
    public required long PaletteBytes { get; init; }

    [JsonPropertyName("estimated_rom_bytes")]
    public required long EstimatedRomBytes { get; init; }

    [JsonPropertyName("pixel_checksum_fnv1a32")]
    public required string PixelChecksumFnv1a32 { get; init; }

    [JsonPropertyName("palette_checksum_fnv1a32")]
    public required string PaletteChecksumFnv1a32 { get; init; }

    [JsonPropertyName("data_checksum_fnv1a32")]
    public required string DataChecksumFnv1a32 { get; init; }

    [JsonPropertyName("source_sha256")]
    public required string SourceSha256 { get; init; }
}

public sealed record AnimationConversionMetricsLoadResult
{
    public AnimationConversionMetrics? Metrics { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record AnimationConversionResult
{
    public AnimationConversionMetrics? Metrics { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public interface IAnimationConversionRunner
{
    Task<AnimationConversionResult> ConvertAsync(
        AnimationConversionRequest request,
        CancellationToken cancellationToken = default);
}

public static class AnimationConversionRequestValidator
{
    public static IReadOnlyList<ProfileDiagnostic> Validate(AnimationConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ProfileDiagnostic> diagnostics = [.. ImageConversionRequestValidator.Validate(
            new ImageConversionRequest
            {
                InputImagePath = request.InputSpriteSheetPath,
                OutputSourcePath = request.OutputSourcePath,
                OutputMetricsPath = request.OutputMetricsPath,
                Symbol = request.Symbol,
                EntryPoint = request.EntryPoint,
                Kind = request.Tintable ? ResourceKind.Icon : ResourceKind.Image,
                Tintable = request.Tintable,
                Format = request.Format,
                CropX = request.SheetX,
                CropY = request.SheetY,
                CropWidth = request.SourceFrameWidth,
                CropHeight = request.SourceFrameHeight,
                OutputWidth = request.OutputWidth,
                OutputHeight = request.OutputHeight,
                ScaleMode = request.ScaleMode,
                AlphaMode = request.AlphaMode,
                AlphaThreshold = request.AlphaThreshold,
                Dither = request.Dither,
                TintArgb = request.TintArgb,
                MatteArgb = request.MatteArgb,
                PaletteArgb = request.PaletteArgb
            })];
        if (request.Format == ResourceFormat.R2Packed2x2)
        {
            diagnostics.Add(Error("LVAC1004", "$.format",
                "Native R2 conversion currently supports images and icons, not animation frames."));
        }
        if (request.Columns is < 1 or > 256 ||
            request.FrameCount is < 2 or > 256 || request.Columns > request.FrameCount ||
            request.SheetX < 0 || request.SheetY < 0)
        {
            diagnostics.Add(Error(
                "LVAC1001",
                "$.grid",
                "Animation conversion requires 2-256 row-major frames and 1-256 columns."));
        }
        if (request.FrameIntervalMilliseconds is < 1 or > ushort.MaxValue ||
            request.RepeatIndex < 0 || request.RepeatIndex >= request.FrameCount)
        {
            diagnostics.Add(Error(
                "LVAC1002",
                "$.timing",
                "Frame interval must fit 16 bits and the repeat index must identify a frame."));
        }
        if (request.Tintable &&
            request.Format is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8)
        {
            diagnostics.Add(Error(
                "LVAC1003",
                "$.tintable",
                "Tintable animations require alpha1 or alpha8 coverage frames."));
        }
        return diagnostics;
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }
}

public static class AnimationConversionMetricsLoader
{
    private const int CurrentSchemaVersion = 1;

    public static AnimationConversionMetricsLoadResult LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            byte[] utf8 = File.ReadAllBytes(path);
            if (utf8.Length > ProfileRules.MaximumSerializedByteCount)
            {
                return Failed("LVAC2001", path, "The animation metrics file exceeds the profile size limit.");
            }
            return Load(utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed("LVAC2002", path, $"Unable to read animation metrics: {exception.Message}");
        }
    }

    public static AnimationConversionMetricsLoadResult Load(ReadOnlyMemory<byte> utf8)
    {
        List<ProfileDiagnostic> diagnostics = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8, ProfileJson.DocumentOptions);
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    diagnostics.Add(Error(
                        "LVAC2003",
                        $"$.{property.Name}",
                        $"Animation metrics property '{property.Name}' is duplicated."));
                }
            }
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("LVAC2004", exception.Path ?? "$", exception.Message));
        }
        if (HasErrors(diagnostics))
        {
            return new AnimationConversionMetricsLoadResult { Diagnostics = diagnostics };
        }

        AnimationConversionMetrics? metrics;
        try
        {
            metrics = JsonSerializer.Deserialize<AnimationConversionMetrics>(
                utf8.Span,
                ProfileJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("LVAC2005", exception.Path ?? "$", exception.Message));
            return new AnimationConversionMetricsLoadResult { Diagnostics = diagnostics };
        }
        if (metrics is null)
        {
            return Failed("LVAC2006", "$", "The animation metrics file is empty.");
        }

        ValidateMetrics(metrics, diagnostics);
        return new AnimationConversionMetricsLoadResult
        {
            Metrics = HasErrors(diagnostics) ? null : metrics,
            Diagnostics = diagnostics
        };
    }

    private static void ValidateMetrics(
        AnimationConversionMetrics metrics,
        List<ProfileDiagnostic> diagnostics)
    {
        if (metrics.SchemaVersion != CurrentSchemaVersion ||
            metrics.ConverterId != "litheview.converter.animation.gdiplus" ||
            metrics.ConverterVersion != "1.0.0")
        {
            diagnostics.Add(Error(
                "LVAC2101",
                "$",
                "Animation metrics require the checked GDI+ converter contract version 1.0.0."));
        }
        if (!Enum.TryParse(metrics.Format, ignoreCase: true, out ResourceFormat format) ||
            !ImageConversionRequestValidator.IsBitmapFormat(format) || format == ResourceFormat.R2Packed2x2)
        {
            diagnostics.Add(Error("LVAC2102", "$.format", "Animation format is invalid."));
            return;
        }
        int rows = metrics.Columns > 0
            ? (metrics.FrameCount + metrics.Columns - 1) / metrics.Columns
            : 0;
        long gridWidth = (long)metrics.Columns * metrics.SourceFrameWidth;
        long gridHeight = (long)rows * metrics.SourceFrameHeight;
        if (metrics.SourceWidth < 1 || metrics.SourceHeight < 1 ||
            metrics.SourceFrameWidth < 1 || metrics.SourceFrameHeight < 1 ||
            metrics.SheetX < 0 || metrics.SheetY < 0 ||
            metrics.Columns is < 1 or > 256 || metrics.FrameCount is < 2 or > 256 ||
            metrics.Columns > metrics.FrameCount ||
            (long)metrics.SheetX + gridWidth > metrics.SourceWidth ||
            (long)metrics.SheetY + gridHeight > metrics.SourceHeight ||
            metrics.OutputWidth is < 1 or > 4096 || metrics.OutputHeight is < 1 or > 4096)
        {
            diagnostics.Add(Error("LVAC2103", "$.grid", "Animation source grid or output geometry is invalid."));
        }
        int expectedStride;
        long expectedFrameBytes;
        long expectedPixelBytes;
        try
        {
            expectedStride = ImageConversionRequestValidator.MinimumStride(format, metrics.OutputWidth);
            expectedFrameBytes = checked((long)expectedStride * metrics.OutputHeight);
            expectedPixelBytes = checked(expectedFrameBytes * metrics.FrameCount);
        }
        catch (OverflowException)
        {
            expectedStride = 0;
            expectedFrameBytes = 0;
            expectedPixelBytes = 0;
        }
        ImageColorText.TryCanonicalizePalette(
            metrics.PaletteArgb,
            out string canonicalPalette,
            out int paletteCount);
        long expectedPaletteBytes = (long)paletteCount * 4L;
        if (metrics.StrideBytes != expectedStride ||
            metrics.FramePixelBytes != expectedFrameBytes ||
            metrics.PixelBytes != expectedPixelBytes ||
            metrics.PaletteBytes != expectedPaletteBytes ||
            metrics.EstimatedRomBytes != metrics.PixelBytes + metrics.PaletteBytes ||
            metrics.EstimatedRomBytes is < 1 or > 64L * 1024L * 1024L)
        {
            diagnostics.Add(Error(
                "LVAC2104",
                "$.payload",
                "Animation stride, frame, pixel, palette, or ROM measurements are inconsistent."));
        }
        if (canonicalPalette != metrics.PaletteArgb ||
            format == ResourceFormat.Indexed4 && paletteCount is < 1 or > 16 ||
            format != ResourceFormat.Indexed4 && paletteCount != 0 ||
            !ImageColorText.IsCanonicalArgb(metrics.TintArgb) ||
            !ImageColorText.IsCanonicalArgb(metrics.MatteArgb))
        {
            diagnostics.Add(Error("LVAC2105", "$.colors", "Animation colors or palette are not canonical."));
        }
        if (!Enum.TryParse(metrics.ScaleMode, out ImageScaleMode scaleMode) || !Enum.IsDefined(scaleMode) ||
            !Enum.TryParse(metrics.AlphaMode, out ImageAlphaMode alphaMode) || !Enum.IsDefined(alphaMode) ||
            !Enum.TryParse(metrics.Dither, out ImageDitherMode dither) || !Enum.IsDefined(dither) ||
            metrics.AlphaThreshold is < 0 or > byte.MaxValue ||
            dither != ImageDitherMode.None &&
                format is not ResourceFormat.Indexed4 and not ResourceFormat.Rgb565)
        {
            diagnostics.Add(Error("LVAC2106", "$.sampling", "Animation sampling or dither metadata is invalid."));
        }
        if (metrics.FrameIntervalMilliseconds is < 1 or > ushort.MaxValue ||
            metrics.RepeatIndex < 0 || metrics.RepeatIndex >= metrics.FrameCount ||
            metrics.Tintable && format is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8 ||
            !FontConversionRequestValidator.IsCppIdentifier(metrics.Symbol) ||
            metrics.Symbol[0] is not (>= 'A' and <= 'Z') ||
            !FontConversionRequestValidator.IsCppIdentifier(metrics.EntryPoint) ||
            metrics.EntryPoint[0] is not (>= 'a' and <= 'z'))
        {
            diagnostics.Add(Error("LVAC2107", "$.integration", "Animation timing, tint, or symbols are invalid."));
        }
        if (!ProfileRules.IsLowercaseSha256(metrics.SourceSha256) ||
            !IsFnv1a32(metrics.PixelChecksumFnv1a32) ||
            !IsFnv1a32(metrics.PaletteChecksumFnv1a32) ||
            !IsFnv1a32(metrics.DataChecksumFnv1a32))
        {
            diagnostics.Add(Error("LVAC2108", "$.identity", "Animation hashes or checksums are invalid."));
        }
    }

    private static bool IsFnv1a32(string value)
    {
        return value.Length == 10 && value.StartsWith("0x", StringComparison.Ordinal) &&
            value.Skip(2).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static AnimationConversionMetricsLoadResult Failed(string code, string path, string message)
    {
        return new AnimationConversionMetricsLoadResult { Diagnostics = [Error(code, path, message)] };
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }
}

public sealed class PowerShellAnimationConversionRunner : IAnimationConversionRunner
{
    private readonly string _scriptPath;
    private readonly string _powerShellPath;

    public PowerShellAnimationConversionRunner(string scriptPath, string powerShellPath = "powershell.exe")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(powerShellPath);
        _scriptPath = Path.GetFullPath(scriptPath);
        _powerShellPath = powerShellPath;
    }

    public async Task<AnimationConversionResult> ConvertAsync(
        AnimationConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProfileDiagnostic> validation = AnimationConversionRequestValidator.Validate(request);
        if (validation.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return Failed(validation);
        }
        if (!File.Exists(_scriptPath) || !File.Exists(request.InputSpriteSheetPath))
        {
            return Failed([Error("LVAC3001", "$.input", "The converter script or sprite sheet does not exist.")]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputSourcePath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputMetricsPath))!);
        ProcessStartInfo start = new()
        {
            FileName = _powerShellPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        string[] arguments = BuildArguments(request);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start) ??
                throw new InvalidOperationException("PowerShell animation converter did not start.");
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                throw;
            }
            string standardOutput = await outputTask.ConfigureAwait(false);
            string standardError = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                return new AnimationConversionResult
                {
                    StandardOutput = standardOutput,
                    StandardError = standardError,
                    Diagnostics =
                    [
                        Error(
                            "LVAC3002",
                            "$.converter",
                            $"Animation conversion exited with code {process.ExitCode}: {standardError.Trim()}")
                    ]
                };
            }

            AnimationConversionMetricsLoadResult metrics = AnimationConversionMetricsLoader.LoadFile(
                request.OutputMetricsPath);
            return new AnimationConversionResult
            {
                Metrics = metrics.Metrics,
                StandardOutput = standardOutput,
                StandardError = standardError,
                Diagnostics = metrics.Diagnostics
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return Failed([
                Error("LVAC3003", "$.converter", $"Animation conversion could not run: {exception.Message}")
            ]);
        }
    }

    private string[] BuildArguments(AnimationConversionRequest request)
    {
        return
        [
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-File",
            _scriptPath,
            "-InputSpriteSheet",
            Path.GetFullPath(request.InputSpriteSheetPath),
            "-OutputSource",
            Path.GetFullPath(request.OutputSourcePath),
            "-OutputManifest",
            Path.GetFullPath(request.OutputMetricsPath),
            "-Symbol",
            request.Symbol,
            "-EntryPoint",
            request.EntryPoint,
            "-Tintable",
            request.Tintable ? "True" : "False",
            "-Format",
            request.Format.ToString(),
            "-SheetX",
            Number(request.SheetX),
            "-SheetY",
            Number(request.SheetY),
            "-SourceFrameWidth",
            Number(request.SourceFrameWidth),
            "-SourceFrameHeight",
            Number(request.SourceFrameHeight),
            "-Columns",
            Number(request.Columns),
            "-FrameCount",
            Number(request.FrameCount),
            "-OutputWidth",
            Number(request.OutputWidth),
            "-OutputHeight",
            Number(request.OutputHeight),
            "-ScaleMode",
            request.ScaleMode.ToString(),
            "-AlphaMode",
            request.AlphaMode.ToString(),
            "-AlphaThreshold",
            Number(request.AlphaThreshold),
            "-Dither",
            request.Dither.ToString(),
            "-TintArgb",
            request.TintArgb,
            "-MatteArgb",
            request.MatteArgb,
            "-FrameIntervalMilliseconds",
            Number(request.FrameIntervalMilliseconds),
            "-RepeatIndex",
            Number(request.RepeatIndex),
            "-Autoplay",
            request.Autoplay ? "True" : "False",
            "-PaletteArgb",
            request.PaletteArgb
        ];
    }

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static AnimationConversionResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new AnimationConversionResult
        {
            StandardOutput = string.Empty,
            StandardError = string.Empty,
            Diagnostics = diagnostics
        };
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }
}
