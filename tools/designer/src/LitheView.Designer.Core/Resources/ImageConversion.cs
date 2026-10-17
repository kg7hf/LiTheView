// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public enum ImageScaleMode
{
    NearestNeighbor,
    Bilinear
}

public enum ImageAlphaMode
{
    Preserve,
    Threshold,
    Opaque
}

public enum ImageDitherMode
{
    None,
    Ordered4x4
}

public sealed record ImageConversionRequest
{
    public required string InputImagePath { get; init; }

    public required string OutputSourcePath { get; init; }

    public required string OutputMetricsPath { get; init; }

    public required string Symbol { get; init; }

    public required string EntryPoint { get; init; }

    public required ResourceKind Kind { get; init; }

    public required bool Tintable { get; init; }

    public required ResourceFormat Format { get; init; }

    public required int CropX { get; init; }

    public required int CropY { get; init; }

    public required int CropWidth { get; init; }

    public required int CropHeight { get; init; }

    public required int OutputWidth { get; init; }

    public required int OutputHeight { get; init; }

    public required ImageScaleMode ScaleMode { get; init; }

    public required ImageAlphaMode AlphaMode { get; init; }

    public required int AlphaThreshold { get; init; }

    public required ImageDitherMode Dither { get; init; }

    public required string TintArgb { get; init; }

    public required string MatteArgb { get; init; }

    public required string PaletteArgb { get; init; }
}

public sealed record ImageConversionMetrics
{
    [JsonPropertyName("schema_version")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("converter_id")]
    public required string ConverterId { get; init; }

    [JsonPropertyName("converter_version")]
    public required string ConverterVersion { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("tintable")]
    public required bool Tintable { get; init; }

    [JsonPropertyName("format")]
    public required string Format { get; init; }

    [JsonPropertyName("source_width")]
    public required int SourceWidth { get; init; }

    [JsonPropertyName("source_height")]
    public required int SourceHeight { get; init; }

    [JsonPropertyName("crop_x")]
    public required int CropX { get; init; }

    [JsonPropertyName("crop_y")]
    public required int CropY { get; init; }

    [JsonPropertyName("crop_width")]
    public required int CropWidth { get; init; }

    [JsonPropertyName("crop_height")]
    public required int CropHeight { get; init; }

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

    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    [JsonPropertyName("entry_point")]
    public required string EntryPoint { get; init; }

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

public sealed record ImageConversionMetricsLoadResult
{
    public ImageConversionMetrics? Metrics { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record ImageConversionResult
{
    public ImageConversionMetrics? Metrics { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public interface IImageConversionRunner
{
    Task<ImageConversionResult> ConvertAsync(
        ImageConversionRequest request,
        CancellationToken cancellationToken = default);
}

public static class ImageConversionRequestValidator
{
    private const int MaximumOutputDimension = 4096;

    public static IReadOnlyList<ProfileDiagnostic> Validate(ImageConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ProfileDiagnostic> diagnostics = [];
        if (string.IsNullOrWhiteSpace(request.InputImagePath))
        {
            AddError(diagnostics, "LVIC1001", "$.inputImagePath", "A source raster-image path is required.");
        }
        if (string.IsNullOrWhiteSpace(request.OutputSourcePath) ||
            string.IsNullOrWhiteSpace(request.OutputMetricsPath))
        {
            AddError(
                diagnostics,
                "LVIC1002",
                "$.output",
                "Generated C++ and conversion-metrics paths are required.");
        }
        if (!FontConversionRequestValidator.IsCppIdentifier(request.Symbol) ||
            request.Symbol[0] is not (>= 'A' and <= 'Z') ||
            !FontConversionRequestValidator.IsCppIdentifier(request.EntryPoint) ||
            request.EntryPoint[0] is not (>= 'a' and <= 'z'))
        {
            AddError(
                diagnostics,
                "LVIC1003",
                "$.integration",
                "Use an upper-camel C++ table symbol and lower-camel C++ entry point.");
        }
        if (request.Kind is not ResourceKind.Image and not ResourceKind.Icon ||
            !IsBitmapFormat(request.Format))
        {
            AddError(
                diagnostics,
                "LVIC1004",
                "$.representation",
                "Image conversion requires image/icon kind and a supported Bitmap format.");
        }
        if (request.CropX < 0 || request.CropY < 0 ||
            request.CropWidth is < 1 or > 16384 || request.CropHeight is < 1 or > 16384 ||
            request.OutputWidth is < 1 or > MaximumOutputDimension ||
            request.OutputHeight is < 1 or > MaximumOutputDimension)
        {
            AddError(
                diagnostics,
                "LVIC1005",
                "$.geometry",
                "Crop coordinates must be nonnegative; crop dimensions are limited to 16384 and output to 4096.");
        }
        if (!IsAlignedExtent(request.Format, request.OutputWidth, request.OutputHeight))
        {
            AddError(diagnostics, "LVIC1010", "$.geometry",
                "Native R2 output requires complete 2-by-2 blocks and even logical dimensions.");
        }
        if (!Enum.IsDefined(request.ScaleMode) || !Enum.IsDefined(request.AlphaMode) ||
            !Enum.IsDefined(request.Dither) || request.AlphaThreshold is < 0 or > byte.MaxValue)
        {
            AddError(
                diagnostics,
                "LVIC1006",
                "$.sampling",
                "Scale, alpha, threshold, and dither selections are invalid.");
        }
        if (request.Dither != ImageDitherMode.None &&
            request.Format is not ResourceFormat.Indexed4 and not ResourceFormat.Rgb565)
        {
            AddError(
                diagnostics,
                "LVIC1007",
                "$.dither",
                "Ordered dithering is available only for indexed4 and RGB565 conversion.");
        }
        if (!ImageColorText.IsCanonicalArgb(request.TintArgb) ||
            !ImageColorText.IsCanonicalArgb(request.MatteArgb) ||
            !ImageColorText.TryCanonicalizePalette(
                request.PaletteArgb,
                out string canonicalPalette,
                out int paletteCount) ||
            canonicalPalette != request.PaletteArgb ||
            request.Format == ResourceFormat.Indexed4 && paletteCount is < 1 or > 16 ||
            request.Format != ResourceFormat.Indexed4 && paletteCount != 0)
        {
            AddError(
                diagnostics,
                "LVIC1008",
                "$.colors",
                "Tint/matte must be lowercase AARRGGBB; indexed4 requires 1-16 canonical palette entries and other formats require none.");
        }
        if (request.Tintable &&
            (request.Kind != ResourceKind.Icon ||
             request.Format is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8))
        {
            AddError(
                diagnostics,
                "LVIC1009",
                "$.tintable",
                "A tintable resource must be an alpha1 or alpha8 icon mask.");
        }
        return diagnostics;
    }

    public static bool IsBitmapFormat(ResourceFormat format)
    {
        return format is ResourceFormat.Alpha1 or ResourceFormat.Alpha8 or
            ResourceFormat.Indexed4 or ResourceFormat.Rgb565 or ResourceFormat.Rgb888 or
            ResourceFormat.Xrgb8888 or ResourceFormat.Argb8888 or ResourceFormat.R2Packed2x2;
    }

    public static int MinimumStride(ResourceFormat format, int width)
    {
        return format switch
        {
            ResourceFormat.Alpha1 => (width + 7) / 8,
            ResourceFormat.Alpha8 => width,
            ResourceFormat.Indexed4 => (width + 1) / 2,
            ResourceFormat.Rgb565 => checked(width * 2),
            ResourceFormat.Rgb888 => checked(width * 3),
            ResourceFormat.Xrgb8888 or ResourceFormat.Argb8888 => checked(width * 4),
            ResourceFormat.R2Packed2x2 => width > 0 && width % 2 == 0 ? width / 2 : 0,
            _ => 0
        };
    }

    public static bool IsAlignedExtent(ResourceFormat format, int width, int height)
    {
        return format != ResourceFormat.R2Packed2x2 ||
            width > 0 && height > 0 && width % 2 == 0 && height % 2 == 0;
    }

    public static int StorageRowCount(ResourceFormat format, int height)
    {
        return format == ResourceFormat.R2Packed2x2
            ? height > 0 && height % 2 == 0 ? height / 2 : 0
            : height;
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }
}

public static class ImageColorText
{
    public static bool IsCanonicalArgb(string value)
    {
        return value.Length == 8 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    public static bool TryCanonicalizeArgb(string value, out string canonical)
    {
        canonical = value.Trim().TrimStart('#').ToLowerInvariant();
        return IsCanonicalArgb(canonical);
    }

    public static bool TryCanonicalizePalette(
        string value,
        out string canonical,
        out int entryCount)
    {
        canonical = string.Empty;
        entryCount = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string[] entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length is < 1 or > 16)
        {
            return false;
        }
        string[] normalized = new string[entries.Length];
        for (int index = 0; index < entries.Length; ++index)
        {
            if (!TryCanonicalizeArgb(entries[index], out normalized[index]))
            {
                return false;
            }
        }
        canonical = string.Join(',', normalized);
        entryCount = normalized.Length;
        return true;
    }
}

public static class ImageConversionMetricsLoader
{
    private const int CurrentSchemaVersion = 1;

    public static ImageConversionMetricsLoadResult LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            byte[] utf8 = File.ReadAllBytes(path);
            if (utf8.Length > ProfileRules.MaximumSerializedByteCount)
            {
                return Failed("LVIC2001", path, "The image metrics file exceeds the profile size limit.");
            }
            return Load(utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed("LVIC2002", path, $"Unable to read image metrics: {exception.Message}");
        }
    }

    public static ImageConversionMetricsLoadResult Load(ReadOnlyMemory<byte> utf8)
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
                        "LVIC2003",
                        $"$.{property.Name}",
                        $"Image metrics property '{property.Name}' is duplicated."));
                }
            }
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("LVIC2004", exception.Path ?? "$", exception.Message));
        }
        if (HasErrors(diagnostics))
        {
            return new ImageConversionMetricsLoadResult { Diagnostics = diagnostics };
        }

        ImageConversionMetrics? metrics;
        try
        {
            metrics = JsonSerializer.Deserialize<ImageConversionMetrics>(utf8.Span, ProfileJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("LVIC2005", exception.Path ?? "$", exception.Message));
            return new ImageConversionMetricsLoadResult { Diagnostics = diagnostics };
        }
        if (metrics is null)
        {
            return Failed("LVIC2006", "$", "The image metrics file is empty.");
        }

        ValidateMetrics(metrics, diagnostics);
        return new ImageConversionMetricsLoadResult
        {
            Metrics = HasErrors(diagnostics) ? null : metrics,
            Diagnostics = diagnostics
        };
    }

    private static void ValidateMetrics(
        ImageConversionMetrics metrics,
        List<ProfileDiagnostic> diagnostics)
    {
        if (metrics.SchemaVersion != CurrentSchemaVersion ||
            metrics.ConverterId != "litheview.converter.image.gdiplus" ||
            metrics.ConverterVersion != "1.0.0")
        {
            diagnostics.Add(Error(
                "LVIC2101",
                "$",
                "Image metrics require the checked GDI+ converter contract version 1.0.0."));
        }
        if (!Enum.TryParse(metrics.Kind, ignoreCase: true, out ResourceKind kind) ||
            kind is not ResourceKind.Image and not ResourceKind.Icon ||
            !Enum.TryParse(metrics.Format, ignoreCase: true, out ResourceFormat format) ||
            !ImageConversionRequestValidator.IsBitmapFormat(format))
        {
            diagnostics.Add(Error("LVIC2102", "$.representation", "Image metrics kind or format is invalid."));
            return;
        }
        if (metrics.SourceWidth < 1 || metrics.SourceHeight < 1 ||
            metrics.CropX < 0 || metrics.CropY < 0 ||
            metrics.CropWidth < 1 || metrics.CropHeight < 1 ||
            metrics.CropX > metrics.SourceWidth - metrics.CropWidth ||
            metrics.CropY > metrics.SourceHeight - metrics.CropHeight ||
            metrics.OutputWidth is < 1 or > 4096 || metrics.OutputHeight is < 1 or > 4096 ||
            !ImageConversionRequestValidator.IsAlignedExtent(format, metrics.OutputWidth, metrics.OutputHeight))
        {
            diagnostics.Add(Error("LVIC2103", "$.geometry", "Image metrics crop or output geometry is invalid."));
        }
        int expectedStride;
        long expectedPixelBytes;
        try
        {
            expectedStride = ImageConversionRequestValidator.MinimumStride(format, metrics.OutputWidth);
            expectedPixelBytes = checked((long)expectedStride *
                ImageConversionRequestValidator.StorageRowCount(format, metrics.OutputHeight));
        }
        catch (OverflowException)
        {
            expectedStride = 0;
            expectedPixelBytes = 0;
        }
        ImageColorText.TryCanonicalizePalette(metrics.PaletteArgb, out string canonicalPalette, out int paletteCount);
        long expectedPaletteBytes = (long)paletteCount * 4L;
        if (metrics.StrideBytes != expectedStride || metrics.PixelBytes != expectedPixelBytes ||
            metrics.PaletteBytes != expectedPaletteBytes ||
            metrics.EstimatedRomBytes != metrics.PixelBytes + metrics.PaletteBytes ||
            metrics.PixelBytes is < 1 or > 64L * 1024L * 1024L)
        {
            diagnostics.Add(Error(
                "LVIC2104",
                "$.payload",
                "Image stride, pixel, palette, or ROM measurements are inconsistent."));
        }
        if (canonicalPalette != metrics.PaletteArgb ||
            format == ResourceFormat.Indexed4 && paletteCount is < 1 or > 16 ||
            format != ResourceFormat.Indexed4 && paletteCount != 0 ||
            !ImageColorText.IsCanonicalArgb(metrics.TintArgb) ||
            !ImageColorText.IsCanonicalArgb(metrics.MatteArgb))
        {
            diagnostics.Add(Error("LVIC2105", "$.colors", "Image colors or indexed palette are not canonical."));
        }
        if (!Enum.TryParse(metrics.ScaleMode, out ImageScaleMode scaleMode) || !Enum.IsDefined(scaleMode) ||
            !Enum.TryParse(metrics.AlphaMode, out ImageAlphaMode alphaMode) || !Enum.IsDefined(alphaMode) ||
            !Enum.TryParse(metrics.Dither, out ImageDitherMode dither) || !Enum.IsDefined(dither) ||
            metrics.AlphaThreshold is < 0 or > byte.MaxValue ||
            dither != ImageDitherMode.None &&
                format is not ResourceFormat.Indexed4 and not ResourceFormat.Rgb565)
        {
            diagnostics.Add(Error("LVIC2106", "$.sampling", "Image sampling and dither metadata is invalid."));
        }
        if (!FontConversionRequestValidator.IsCppIdentifier(metrics.Symbol) ||
            metrics.Symbol[0] is not (>= 'A' and <= 'Z') ||
            !FontConversionRequestValidator.IsCppIdentifier(metrics.EntryPoint) ||
            metrics.EntryPoint[0] is not (>= 'a' and <= 'z') ||
            metrics.Tintable &&
                (kind != ResourceKind.Icon ||
                 format is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8))
        {
            diagnostics.Add(Error("LVIC2107", "$.integration", "Image symbols or tintable-mask identity is invalid."));
        }
        if (!ProfileRules.IsLowercaseSha256(metrics.SourceSha256) ||
            !IsFnv1a32(metrics.PixelChecksumFnv1a32) ||
            !IsFnv1a32(metrics.PaletteChecksumFnv1a32) ||
            !IsFnv1a32(metrics.DataChecksumFnv1a32))
        {
            diagnostics.Add(Error("LVIC2108", "$.identity", "Image metrics hashes or checksums are invalid."));
        }
    }

    private static bool IsFnv1a32(string value)
    {
        return value.Length == 10 && value.StartsWith("0x", StringComparison.Ordinal) &&
            value.Skip(2).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static ImageConversionMetricsLoadResult Failed(string code, string path, string message)
    {
        return new ImageConversionMetricsLoadResult { Diagnostics = [Error(code, path, message)] };
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

public sealed class PowerShellImageConversionRunner : IImageConversionRunner
{
    private readonly string _scriptPath;
    private readonly string _powerShellPath;

    public PowerShellImageConversionRunner(string scriptPath, string powerShellPath = "powershell.exe")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(powerShellPath);
        _scriptPath = Path.GetFullPath(scriptPath);
        _powerShellPath = powerShellPath;
    }

    public async Task<ImageConversionResult> ConvertAsync(
        ImageConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProfileDiagnostic> validation = ImageConversionRequestValidator.Validate(request);
        if (validation.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return Failed(validation);
        }
        if (!File.Exists(_scriptPath) || !File.Exists(request.InputImagePath))
        {
            return Failed([Error("LVIC3001", "$.input", "The converter script or source image does not exist.")]);
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
        string[] arguments =
        [
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-File",
            _scriptPath,
            "-InputImage",
            Path.GetFullPath(request.InputImagePath),
            "-OutputSource",
            Path.GetFullPath(request.OutputSourcePath),
            "-OutputManifest",
            Path.GetFullPath(request.OutputMetricsPath),
            "-Symbol",
            request.Symbol,
            "-EntryPoint",
            request.EntryPoint,
            "-Kind",
            request.Kind.ToString(),
            "-Tintable",
            request.Tintable ? "True" : "False",
            "-Format",
            request.Format.ToString(),
            "-CropX",
            request.CropX.ToString(CultureInfo.InvariantCulture),
            "-CropY",
            request.CropY.ToString(CultureInfo.InvariantCulture),
            "-CropWidth",
            request.CropWidth.ToString(CultureInfo.InvariantCulture),
            "-CropHeight",
            request.CropHeight.ToString(CultureInfo.InvariantCulture),
            "-OutputWidth",
            request.OutputWidth.ToString(CultureInfo.InvariantCulture),
            "-OutputHeight",
            request.OutputHeight.ToString(CultureInfo.InvariantCulture),
            "-ScaleMode",
            request.ScaleMode.ToString(),
            "-AlphaMode",
            request.AlphaMode.ToString(),
            "-AlphaThreshold",
            request.AlphaThreshold.ToString(CultureInfo.InvariantCulture),
            "-Dither",
            request.Dither.ToString(),
            "-TintArgb",
            request.TintArgb,
            "-MatteArgb",
            request.MatteArgb,
            "-PaletteArgb",
            request.PaletteArgb
        ];
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start) ??
                throw new InvalidOperationException("PowerShell image converter did not start.");
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
                return new ImageConversionResult
                {
                    StandardOutput = standardOutput,
                    StandardError = standardError,
                    Diagnostics =
                    [
                        Error(
                            "LVIC3002",
                            "$.converter",
                            $"Image conversion exited with code {process.ExitCode}: {standardError.Trim()}")
                    ]
                };
            }

            ImageConversionMetricsLoadResult metrics = ImageConversionMetricsLoader.LoadFile(
                request.OutputMetricsPath);
            return new ImageConversionResult
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
                Error("LVIC3003", "$.converter", $"Image conversion could not run: {exception.Message}")
            ]);
        }
    }

    private static ImageConversionResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new ImageConversionResult
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
