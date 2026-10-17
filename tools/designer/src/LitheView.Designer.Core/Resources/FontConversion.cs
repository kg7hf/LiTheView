// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public enum FontRasterStyle
{
    Regular,
    Bold
}

public sealed record FontConversionRequest
{
    public required string InputFontPath { get; init; }

    public required string OutputSourcePath { get; init; }

    public required string OutputMetricsPath { get; init; }

    public required string Symbol { get; init; }

    public required string EntryPoint { get; init; }

    public required int PixelSize { get; init; }

    public required FontRasterStyle RasterStyle { get; init; }

    public required string StyleLabel { get; init; }

    public required int Weight { get; init; }

    public required ResourceFormat Coverage { get; init; }

    public required string Codepoints { get; init; }
}

public sealed record FontConversionMetrics
{
    [JsonPropertyName("schema_version")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("converter_id")]
    public required string ConverterId { get; init; }

    [JsonPropertyName("converter_version")]
    public required string ConverterVersion { get; init; }

    [JsonPropertyName("family")]
    public required string Family { get; init; }

    [JsonPropertyName("style")]
    public required string Style { get; init; }

    [JsonPropertyName("raster_style")]
    public required string RasterStyle { get; init; }

    [JsonPropertyName("weight")]
    public required int Weight { get; init; }

    [JsonPropertyName("pixel_size")]
    public required int PixelSize { get; init; }

    [JsonPropertyName("coverage")]
    public required string Coverage { get; init; }

    [JsonPropertyName("repertoire")]
    public required string Repertoire { get; init; }

    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    [JsonPropertyName("entry_point")]
    public required string EntryPoint { get; init; }

    [JsonPropertyName("glyph_count")]
    public required int GlyphCount { get; init; }

    [JsonPropertyName("coverage_bytes")]
    public required long CoverageBytes { get; init; }

    [JsonPropertyName("record_bytes")]
    public required long RecordBytes { get; init; }

    [JsonPropertyName("estimated_rom_bytes")]
    public required long EstimatedRomBytes { get; init; }

    [JsonPropertyName("data_checksum_fnv1a32")]
    public required string DataChecksumFnv1a32 { get; init; }

    [JsonPropertyName("source_sha256")]
    public required string SourceSha256 { get; init; }

    [JsonPropertyName("fallback")]
    public required string Fallback { get; init; }

    [JsonPropertyName("line_height")]
    public required int LineHeight { get; init; }

    [JsonPropertyName("baseline")]
    public required int Baseline { get; init; }

    [JsonPropertyName("underline_position")]
    public required int UnderlinePosition { get; init; }

    [JsonPropertyName("underline_thickness")]
    public required int UnderlineThickness { get; init; }
}

public sealed record FontConversionMetricsLoadResult
{
    public FontConversionMetrics? Metrics { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record FontConversionResult
{
    public FontConversionMetrics? Metrics { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public interface IFontConversionRunner
{
    Task<FontConversionResult> ConvertAsync(
        FontConversionRequest request,
        CancellationToken cancellationToken = default);
}

public static class FontConversionRequestValidator
{
    public static IReadOnlyList<ProfileDiagnostic> Validate(FontConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ProfileDiagnostic> diagnostics = [];
        if (string.IsNullOrWhiteSpace(request.InputFontPath))
        {
            AddError(diagnostics, "LVFC1001", "$.inputFontPath", "A source TTF or OTF path is required.");
        }
        if (string.IsNullOrWhiteSpace(request.OutputSourcePath) ||
            string.IsNullOrWhiteSpace(request.OutputMetricsPath))
        {
            AddError(
                diagnostics,
                "LVFC1002",
                "$.output",
                "Generated C++ and metrics output paths are required.");
        }
        if (!IsCppIdentifier(request.Symbol) || request.Symbol[0] is not (>= 'A' and <= 'Z'))
        {
            AddError(
                diagnostics,
                "LVFC1003",
                "$.symbol",
                "The generated table symbol must be an upper-camel-case C++ identifier.");
        }
        if (!IsCppIdentifier(request.EntryPoint) || request.EntryPoint[0] is not (>= 'a' and <= 'z'))
        {
            AddError(
                diagnostics,
                "LVFC1004",
                "$.entryPoint",
                "The generated font entry point must be a lower-camel-case C++ identifier.");
        }
        if (request.PixelSize is < 4 or > 64 || request.Weight is < 1 or > 1000)
        {
            AddError(
                diagnostics,
                "LVFC1005",
                "$.font",
                "Pixel size must be 4 through 64 and visual weight must be 1 through 1000.");
        }
        if (!Enum.IsDefined(request.RasterStyle) ||
            string.IsNullOrWhiteSpace(request.StyleLabel) || request.StyleLabel.Length > 160)
        {
            AddError(
                diagnostics,
                "LVFC1006",
                "$.font",
                "Raster style and a bounded visual style label are required.");
        }
        if (request.Coverage is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8)
        {
            AddError(
                diagnostics,
                "LVFC1007",
                "$.coverage",
                "Generated fonts support alpha1 or alpha8 coverage.");
        }
        if (!FontRepertoireParser.TryParse(request.Codepoints, out _, out string? repertoireError))
        {
            AddError(diagnostics, "LVFC1008", "$.codepoints", repertoireError!);
        }
        return diagnostics;
    }

    internal static bool IsCppIdentifier(string value)
    {
        return value.Length is >= 1 and <= 128 &&
            value[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_' &&
            value.Skip(1).All(character =>
                character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
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

public static class FontConversionMetricsLoader
{
    private const int CurrentSchemaVersion = 2;

    public static FontConversionMetricsLoadResult LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            byte[] utf8 = File.ReadAllBytes(path);
            if (utf8.Length > ProfileRules.MaximumSerializedByteCount)
            {
                return Failed("LVFC2001", path, "The font metrics file exceeds the profile size limit.");
            }
            return Load(utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed("LVFC2002", path, $"Unable to read font metrics: {exception.Message}");
        }
    }

    public static FontConversionMetricsLoadResult Load(ReadOnlyMemory<byte> utf8)
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
                        "LVFC2003",
                        $"$.{property.Name}",
                        $"Font metrics property '{property.Name}' is duplicated."));
                }
            }
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("LVFC2004", exception.Path ?? "$", exception.Message));
        }
        if (HasErrors(diagnostics))
        {
            return new FontConversionMetricsLoadResult { Diagnostics = diagnostics };
        }

        FontConversionMetrics? metrics;
        try
        {
            metrics = JsonSerializer.Deserialize<FontConversionMetrics>(utf8.Span, ProfileJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("LVFC2005", exception.Path ?? "$", exception.Message));
            return new FontConversionMetricsLoadResult { Diagnostics = diagnostics };
        }
        if (metrics is null)
        {
            return Failed("LVFC2006", "$", "The font metrics file is empty.");
        }

        ValidateMetrics(metrics, diagnostics);
        return new FontConversionMetricsLoadResult
        {
            Metrics = HasErrors(diagnostics) ? null : metrics,
            Diagnostics = diagnostics
        };
    }

    private static void ValidateMetrics(
        FontConversionMetrics metrics,
        List<ProfileDiagnostic> diagnostics)
    {
        if (metrics.SchemaVersion != CurrentSchemaVersion ||
            metrics.ConverterId != "litheview.converter.font.gdiplus" ||
            metrics.ConverterVersion != "2.0.0")
        {
            diagnostics.Add(Error(
                "LVFC2101",
                "$",
                "Font metrics require the checked GDI+ converter contract version 2.0.0."));
        }
        if (!ProfileRules.IsLowercaseSha256(metrics.SourceSha256) ||
            string.IsNullOrWhiteSpace(metrics.Family) ||
            metrics.Family.Length > ProfileRules.MaximumDisplayNameLength ||
            string.IsNullOrWhiteSpace(metrics.Style) ||
            metrics.Style.Length > ProfileRules.MaximumDisplayNameLength)
        {
            diagnostics.Add(Error(
                "LVFC2102",
                "$",
                "Font metrics require source identity, family, and style."));
        }
        if (!FontRepertoireParser.TryParse(metrics.Repertoire, out FontRepertoire? repertoire, out _) ||
            repertoire?.Canonical != metrics.Repertoire ||
            metrics.GlyphCount != repertoire?.Codepoints.Length)
        {
            diagnostics.Add(Error(
                "LVFC2103",
                "$.repertoire",
                "Font metrics repertoire and glyph count are not canonical and consistent."));
        }
        if (metrics.RecordBytes != (long)metrics.GlyphCount * 16L ||
            metrics.CoverageBytes < 0 ||
            metrics.EstimatedRomBytes != metrics.CoverageBytes + metrics.RecordBytes)
        {
            diagnostics.Add(Error(
                "LVFC2104",
                "$",
                "Font metrics coverage, record, and ROM byte measurements are inconsistent."));
        }
        if (metrics.RasterStyle is not (nameof(FontRasterStyle.Regular) or nameof(FontRasterStyle.Bold)) ||
            metrics.Weight is < 1 or > 1000 ||
            metrics.PixelSize is < 4 or > 64 ||
            metrics.Coverage is not ("alpha1" or "alpha8"))
        {
            diagnostics.Add(Error(
                "LVFC2105",
                "$.font",
                "Font metrics raster style, visual weight, pixel size, or coverage is invalid."));
        }
        if (!FontConversionRequestValidator.IsCppIdentifier(metrics.Symbol) ||
            metrics.Symbol[0] is not (>= 'A' and <= 'Z') ||
            !FontConversionRequestValidator.IsCppIdentifier(metrics.EntryPoint) ||
            metrics.EntryPoint[0] is not (>= 'a' and <= 'z'))
        {
            diagnostics.Add(Error(
                "LVFC2106",
                "$.integration",
                "Font metrics require valid upper-camel table and lower-camel entry-point identifiers."));
        }
        if (!IsFnv1a32(metrics.DataChecksumFnv1a32) || metrics.Fallback != "U+003F")
        {
            diagnostics.Add(Error(
                "LVFC2107",
                "$.identity",
                "Font metrics require a lowercase FNV-1a checksum and U+003F fallback."));
        }
        if (metrics.LineHeight is < 1 or > byte.MaxValue ||
            metrics.Baseline < 0 || metrics.Baseline >= metrics.LineHeight ||
            metrics.UnderlinePosition < 0 || metrics.UnderlinePosition >= metrics.LineHeight ||
            metrics.UnderlineThickness < 1 ||
            metrics.UnderlineThickness > metrics.LineHeight - metrics.UnderlinePosition)
        {
            diagnostics.Add(Error(
                "LVFC2108",
                "$.lineMetrics",
                "Font metrics baseline and underline must fit the bounded line height."));
        }
    }

    private static bool IsFnv1a32(string value)
    {
        if (value.Length != 10 || !value.StartsWith("0x", StringComparison.Ordinal))
        {
            return false;
        }
        for (int index = 2; index < value.Length; ++index)
        {
            if (value[index] is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }

    private static FontConversionMetricsLoadResult Failed(
        string code,
        string path,
        string message)
    {
        return new FontConversionMetricsLoadResult { Diagnostics = [Error(code, path, message)] };
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

public sealed class PowerShellFontConversionRunner : IFontConversionRunner
{
    private readonly string _scriptPath;
    private readonly string _powerShellPath;

    public PowerShellFontConversionRunner(string scriptPath, string powerShellPath = "powershell.exe")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(powerShellPath);
        _scriptPath = Path.GetFullPath(scriptPath);
        _powerShellPath = powerShellPath;
    }

    public async Task<FontConversionResult> ConvertAsync(
        FontConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProfileDiagnostic> validation = FontConversionRequestValidator.Validate(request);
        if (validation.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return Failed(validation);
        }
        if (!File.Exists(_scriptPath) || !File.Exists(request.InputFontPath))
        {
            return Failed([
                Error("LVFC3001", "$.input", "The converter script or source font does not exist.")
            ]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputSourcePath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputMetricsPath))!);
        FontRepertoire repertoire = FontRepertoireParser.TryParse(request.Codepoints, out FontRepertoire? parsed, out _)
            ? parsed!
            : throw new InvalidOperationException("Validated font repertoire did not parse.");
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
            "-InputFont",
            Path.GetFullPath(request.InputFontPath),
            "-OutputSource",
            Path.GetFullPath(request.OutputSourcePath),
            "-OutputManifest",
            Path.GetFullPath(request.OutputMetricsPath),
            "-Symbol",
            request.Symbol,
            "-EntryPoint",
            request.EntryPoint,
            "-PixelSize",
            request.PixelSize.ToString(CultureInfo.InvariantCulture),
            "-FontStyle",
            request.RasterStyle.ToString(),
            "-StyleLabel",
            request.StyleLabel,
            "-Weight",
            request.Weight.ToString(CultureInfo.InvariantCulture),
            "-Coverage",
            request.Coverage == ResourceFormat.Alpha1 ? "Alpha1" : "Alpha8",
            "-Codepoints",
            repertoire.Canonical
        ];
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start) ??
                throw new InvalidOperationException("PowerShell font converter did not start.");
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
                return new FontConversionResult
                {
                    StandardOutput = standardOutput,
                    StandardError = standardError,
                    Diagnostics =
                    [
                        Error(
                            "LVFC3002",
                            "$.converter",
                            $"Font conversion exited with code {process.ExitCode}: {standardError.Trim()}")
                    ]
                };
            }

            FontConversionMetricsLoadResult metrics = FontConversionMetricsLoader.LoadFile(
                request.OutputMetricsPath);
            return new FontConversionResult
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
                Error("LVFC3003", "$.converter", $"Font conversion could not run: {exception.Message}")
            ]);
        }
    }

    private static FontConversionResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new FontConversionResult
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
