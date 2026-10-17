// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Cli;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int ValidationExitCode = 1;
    private const int UsageExitCode = 2;
    private const int MigrationExitCode = 3;
    private const int CompatibilityExitCode = 4;

    public static int Main(string[] arguments)
    {
        if (arguments.Length >= 2 && arguments[0] == "component")
        {
            return ComponentCommands.Run(arguments);
        }
        if (arguments.Length >= 2 && arguments[0] == "simulator")
        {
            return SimulatorCommands.Run(arguments);
        }
        if (arguments.Length >= 2 && arguments[0] == "display")
        {
            return DisplayCommands.Run(arguments);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "form" &&
            arguments[1] == "validate")
        {
            return ValidateForm(arguments[2], arguments[3]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "form" &&
            arguments[1] == "diagnose-json")
        {
            return DiagnoseFormJson(arguments[2], arguments[3]);
        }
        if (arguments.Length == 5 &&
            arguments[0] == "form" &&
            arguments[1] is "import" or "normalize" or "export-xml")
        {
            FormImportMode mode = arguments[1] == "import"
                ? FormImportMode.Compatibility
                : FormImportMode.Strict;
            return NormalizeForm(arguments[2], arguments[3], arguments[4], mode);
        }
        if (arguments.Length == 5 &&
            arguments[0] == "form" &&
            arguments[1] == "surface-check")
        {
            return CheckFormSurface(arguments[2], arguments[3], arguments[4]);
        }
        if (arguments.Length == 6 &&
            arguments[0] == "form" &&
            arguments[1] == "export-cpp")
        {
            return ExportFormCpp(
                arguments[2],
                arguments[3],
                arguments[4],
                arguments[5]);
        }
        if (arguments.Length == 6 &&
            arguments[0] == "form" &&
            arguments[1] == "budget")
        {
            return BudgetForm(
                arguments[2],
                arguments[3],
                arguments[4],
                arguments[5]);
        }
        if (arguments.Length == 3 &&
            arguments[0] == "profile" &&
            arguments[1] == "validate")
        {
            return ValidateProfile(arguments[2]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "profile" &&
            arguments[1] == "canonicalize")
        {
            return CanonicalizeProfile(arguments[2], arguments[3]);
        }
        if (arguments.Length == 3 &&
            arguments[0] == "widget-schema" &&
            arguments[1] == "validate")
        {
            return ValidateWidgetCatalog(arguments[2]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "widget-schema" &&
            arguments[1] == "canonicalize")
        {
            return CanonicalizeWidgetCatalog(arguments[2], arguments[3]);
        }
        if (arguments.Length == 6 &&
            arguments[0] == "widget-schema" &&
            arguments[1] == "compile")
        {
            return CompileWidgetCatalog(arguments[2], arguments[3], arguments[4], arguments[5]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "widget-schema" &&
            arguments[1] == "check-compatibility")
        {
            return CheckWidgetCatalogCompatibility(arguments[2], arguments[3]);
        }
        if (arguments.Length == 3 &&
            arguments[0] == "target" &&
            arguments[1] == "validate")
        {
            return ValidateTarget(arguments[2]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "target" &&
            arguments[1] == "canonicalize")
        {
            return CanonicalizeTarget(arguments[2], arguments[3]);
        }
        if (arguments.Length == 3 &&
            arguments[0] == "resource" &&
            arguments[1] == "validate")
        {
            return ValidateResource(arguments[2]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "resource" &&
            arguments[1] == "canonicalize")
        {
            return CanonicalizeResource(arguments[2], arguments[3]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "resource" &&
            arguments[1] == "verify")
        {
            return VerifyResource(arguments[2], arguments[3]);
        }
        if (arguments.Length == 14 &&
            arguments[0] == "font" &&
            arguments[1] == "convert")
        {
            return ConvertFontAsync(arguments).GetAwaiter().GetResult();
        }
        if (arguments.Length == 24 &&
            arguments[0] == "image" &&
            arguments[1] == "convert")
        {
            return ConvertImageAsync(arguments).GetAwaiter().GetResult();
        }
        if (arguments.Length == 28 &&
            arguments[0] == "animation" &&
            arguments[1] == "convert")
        {
            return ConvertAnimationAsync(arguments).GetAwaiter().GetResult();
        }
        if (arguments.Length == 4 &&
            arguments[0] == "catalog" &&
            arguments[1] == "list")
        {
            return ListCatalog(arguments[2], arguments[3]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "surface" &&
            arguments[1] == "compare")
        {
            return CompareSurfaces(arguments[2], arguments[3]);
        }
        if (arguments.Length == 3 &&
            arguments[0] == "project" &&
            arguments[1] == "validate")
        {
            return ValidateProject(arguments[2]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "project" &&
            arguments[1] == "canonicalize")
        {
            return CanonicalizeProject(arguments[2], arguments[3]);
        }
        if (arguments.Length == 4 &&
            arguments[0] == "project" &&
            arguments[1] == "resolve")
        {
            return ResolveProject(arguments[2], arguments[3]);
        }
        if (arguments.Length == 8 &&
            arguments[0] == "project" &&
            arguments[1] == "create")
        {
            return CreateProject(arguments);
        }
        if (arguments.Length == 8 &&
            arguments[0] == "project" &&
            arguments[1] == "create-native")
        {
            return CreateNativeDisplayProject(arguments);
        }
        if (arguments.Length >= 6 &&
            arguments.Length % 2 == 0 &&
            arguments[0] == "preview" &&
            arguments[1] == "render")
        {
            List<string> inputSpecifications = [];
            for (int index = 6; index < arguments.Length; index += 2)
            {
                if (arguments[index] != "--input")
                {
                    WriteUsage();
                    return UsageExitCode;
                }
                inputSpecifications.Add(arguments[index + 1]);
            }
            if (!PreviewInputScript.TryParse(inputSpecifications, out PreviewInput[] inputs, out string inputError))
            {
                Console.Error.WriteLine($"LVPW0103 error: {inputError}");
                return UsageExitCode;
            }
            return RenderPreviewAsync(
                arguments[2],
                arguments[3],
                arguments[4],
                arguments[5],
                inputs).GetAwaiter().GetResult();
        }

        WriteUsage();
        return UsageExitCode;
    }

    private static async Task<int> ConvertFontAsync(string[] arguments)
    {
        if (!int.TryParse(arguments[7], NumberStyles.None, CultureInfo.InvariantCulture, out int pixelSize) ||
            !Enum.TryParse(arguments[8], ignoreCase: true, out FontRasterStyle rasterStyle) ||
            !Enum.IsDefined(rasterStyle) ||
            !int.TryParse(arguments[10], NumberStyles.None, CultureInfo.InvariantCulture, out int weight) ||
            !Enum.TryParse(arguments[11], ignoreCase: true, out ResourceFormat coverage) ||
            coverage is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8)
        {
            Console.Error.WriteLine(
                "LVFC0001 error $.arguments: Pixel size, raster style, weight, or coverage is invalid.");
            return UsageExitCode;
        }

        FontConversionRequest request = new()
        {
            InputFontPath = arguments[2],
            OutputSourcePath = arguments[3],
            OutputMetricsPath = arguments[4],
            Symbol = arguments[5],
            EntryPoint = arguments[6],
            PixelSize = pixelSize,
            RasterStyle = rasterStyle,
            StyleLabel = arguments[9],
            Weight = weight,
            Coverage = coverage,
            Codepoints = arguments[12]
        };
        PowerShellFontConversionRunner runner = new(arguments[13]);
        FontConversionResult result = await runner.ConvertAsync(request).ConfigureAwait(false);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.Metrics is null)
        {
            return ValidationExitCode;
        }

        FontConversionMetrics metrics = result.Metrics;
        Console.WriteLine("CONVERTED");
        Console.WriteLine($"family: {metrics.Family}");
        Console.WriteLine($"style: {metrics.Style}");
        Console.WriteLine($"repertoire: {metrics.Repertoire}");
        Console.WriteLine($"glyphs: {metrics.GlyphCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"estimated-rom-bytes: {metrics.EstimatedRomBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"source-sha256: {metrics.SourceSha256}");
        Console.WriteLine($"generated-source: {Path.GetFullPath(request.OutputSourcePath)}");
        Console.WriteLine($"metrics: {Path.GetFullPath(request.OutputMetricsPath)}");
        return SuccessExitCode;
    }

    private static async Task<int> ConvertImageAsync(string[] arguments)
    {
        bool tintable = false;
        ResourceFormat format = default;
        int cropX = 0;
        int cropY = 0;
        int cropWidth = 0;
        int cropHeight = 0;
        int outputWidth = 0;
        int outputHeight = 0;
        ImageScaleMode scaleMode = default;
        ImageAlphaMode alphaMode = default;
        int alphaThreshold = 0;
        ImageDitherMode dither = default;
        string tintArgb = string.Empty;
        string matteArgb = string.Empty;
        string paletteArgb = string.Empty;
        bool parsed = Enum.TryParse(arguments[7], ignoreCase: true, out ResourceKind kind) &&
            kind is ResourceKind.Image or ResourceKind.Icon &&
            bool.TryParse(arguments[8], out tintable) &&
            Enum.TryParse(arguments[9], ignoreCase: true, out format) &&
            ImageConversionRequestValidator.IsBitmapFormat(format) &&
            int.TryParse(arguments[10], NumberStyles.None, CultureInfo.InvariantCulture, out cropX) &&
            int.TryParse(arguments[11], NumberStyles.None, CultureInfo.InvariantCulture, out cropY) &&
            int.TryParse(arguments[12], NumberStyles.None, CultureInfo.InvariantCulture, out cropWidth) &&
            int.TryParse(arguments[13], NumberStyles.None, CultureInfo.InvariantCulture, out cropHeight) &&
            int.TryParse(arguments[14], NumberStyles.None, CultureInfo.InvariantCulture, out outputWidth) &&
            int.TryParse(arguments[15], NumberStyles.None, CultureInfo.InvariantCulture, out outputHeight) &&
            Enum.TryParse(arguments[16], ignoreCase: true, out scaleMode) &&
            Enum.IsDefined(scaleMode) &&
            Enum.TryParse(arguments[17], ignoreCase: true, out alphaMode) &&
            Enum.IsDefined(alphaMode) &&
            int.TryParse(arguments[18], NumberStyles.None, CultureInfo.InvariantCulture, out alphaThreshold) &&
            Enum.TryParse(arguments[19], ignoreCase: true, out dither) &&
            Enum.IsDefined(dither) &&
            ImageColorText.TryCanonicalizeArgb(arguments[20], out tintArgb) &&
            ImageColorText.TryCanonicalizeArgb(arguments[21], out matteArgb) &&
            ImageColorText.TryCanonicalizePalette(arguments[22], out paletteArgb, out _);
        if (!parsed)
        {
            Console.Error.WriteLine(
                "LVIC0001 error $.arguments: Kind, format, crop, output, sampling, or color arguments are invalid.");
            return UsageExitCode;
        }

        ImageConversionRequest request = new()
        {
            InputImagePath = arguments[2],
            OutputSourcePath = arguments[3],
            OutputMetricsPath = arguments[4],
            Symbol = arguments[5],
            EntryPoint = arguments[6],
            Kind = kind,
            Tintable = tintable,
            Format = format,
            CropX = cropX,
            CropY = cropY,
            CropWidth = cropWidth,
            CropHeight = cropHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight,
            ScaleMode = scaleMode,
            AlphaMode = alphaMode,
            AlphaThreshold = alphaThreshold,
            Dither = dither,
            TintArgb = tintArgb,
            MatteArgb = matteArgb,
            PaletteArgb = paletteArgb
        };
        PowerShellImageConversionRunner runner = new(arguments[23]);
        ImageConversionResult result = await runner.ConvertAsync(request).ConfigureAwait(false);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.Metrics is null)
        {
            return ValidationExitCode;
        }

        ImageConversionMetrics metrics = result.Metrics;
        Console.WriteLine("CONVERTED");
        Console.WriteLine($"kind: {metrics.Kind}");
        Console.WriteLine($"format: {metrics.Format}");
        Console.WriteLine($"dimensions: {metrics.OutputWidth} x {metrics.OutputHeight}");
        Console.WriteLine($"pixel-bytes: {metrics.PixelBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"palette-bytes: {metrics.PaletteBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"estimated-rom-bytes: {metrics.EstimatedRomBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"source-sha256: {metrics.SourceSha256}");
        Console.WriteLine($"generated-source: {Path.GetFullPath(request.OutputSourcePath)}");
        Console.WriteLine($"metrics: {Path.GetFullPath(request.OutputMetricsPath)}");
        return SuccessExitCode;
    }

    private static async Task<int> ConvertAnimationAsync(string[] arguments)
    {
        ResourceFormat format = default;
        int sheetX = 0;
        int sheetY = 0;
        int sourceFrameWidth = 0;
        int sourceFrameHeight = 0;
        int columns = 0;
        int frameCount = 0;
        int outputWidth = 0;
        int outputHeight = 0;
        ImageScaleMode scaleMode = default;
        ImageAlphaMode alphaMode = default;
        int alphaThreshold = 0;
        ImageDitherMode dither = default;
        string tintArgb = string.Empty;
        string matteArgb = string.Empty;
        string paletteArgb = string.Empty;
        int frameIntervalMilliseconds = 0;
        int repeatIndex = 0;
        bool autoplay = false;
        bool parsed = bool.TryParse(arguments[7], out bool tintable) &&
            Enum.TryParse(arguments[8], ignoreCase: true, out format) &&
            ImageConversionRequestValidator.IsBitmapFormat(format) &&
            int.TryParse(arguments[9], NumberStyles.None, CultureInfo.InvariantCulture, out sheetX) &&
            int.TryParse(arguments[10], NumberStyles.None, CultureInfo.InvariantCulture, out sheetY) &&
            int.TryParse(arguments[11], NumberStyles.None, CultureInfo.InvariantCulture, out sourceFrameWidth) &&
            int.TryParse(arguments[12], NumberStyles.None, CultureInfo.InvariantCulture, out sourceFrameHeight) &&
            int.TryParse(arguments[13], NumberStyles.None, CultureInfo.InvariantCulture, out columns) &&
            int.TryParse(arguments[14], NumberStyles.None, CultureInfo.InvariantCulture, out frameCount) &&
            int.TryParse(arguments[15], NumberStyles.None, CultureInfo.InvariantCulture, out outputWidth) &&
            int.TryParse(arguments[16], NumberStyles.None, CultureInfo.InvariantCulture, out outputHeight) &&
            Enum.TryParse(arguments[17], ignoreCase: true, out scaleMode) &&
            Enum.IsDefined(scaleMode) &&
            Enum.TryParse(arguments[18], ignoreCase: true, out alphaMode) &&
            Enum.IsDefined(alphaMode) &&
            int.TryParse(arguments[19], NumberStyles.None, CultureInfo.InvariantCulture, out alphaThreshold) &&
            Enum.TryParse(arguments[20], ignoreCase: true, out dither) &&
            Enum.IsDefined(dither) &&
            ImageColorText.TryCanonicalizeArgb(arguments[21], out tintArgb) &&
            ImageColorText.TryCanonicalizeArgb(arguments[22], out matteArgb) &&
            ImageColorText.TryCanonicalizePalette(arguments[23], out paletteArgb, out _) &&
            int.TryParse(
                arguments[24],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out frameIntervalMilliseconds) &&
            int.TryParse(arguments[25], NumberStyles.None, CultureInfo.InvariantCulture, out repeatIndex) &&
            bool.TryParse(arguments[26], out autoplay);
        if (!parsed)
        {
            Console.Error.WriteLine(
                "LVAC0001 error $.arguments: Grid, format, sampling, colors, or timing arguments are invalid.");
            return UsageExitCode;
        }

        AnimationConversionRequest request = new()
        {
            InputSpriteSheetPath = arguments[2],
            OutputSourcePath = arguments[3],
            OutputMetricsPath = arguments[4],
            Symbol = arguments[5],
            EntryPoint = arguments[6],
            Tintable = tintable,
            Format = format,
            SheetX = sheetX,
            SheetY = sheetY,
            SourceFrameWidth = sourceFrameWidth,
            SourceFrameHeight = sourceFrameHeight,
            Columns = columns,
            FrameCount = frameCount,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight,
            ScaleMode = scaleMode,
            AlphaMode = alphaMode,
            AlphaThreshold = alphaThreshold,
            Dither = dither,
            TintArgb = tintArgb,
            MatteArgb = matteArgb,
            PaletteArgb = paletteArgb,
            FrameIntervalMilliseconds = frameIntervalMilliseconds,
            RepeatIndex = repeatIndex,
            Autoplay = autoplay
        };
        PowerShellAnimationConversionRunner runner = new(arguments[27]);
        AnimationConversionResult result = await runner.ConvertAsync(request).ConfigureAwait(false);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.Metrics is null)
        {
            return ValidationExitCode;
        }

        AnimationConversionMetrics metrics = result.Metrics;
        Console.WriteLine("CONVERTED");
        Console.WriteLine($"format: {metrics.Format}");
        Console.WriteLine(
            $"frames: {metrics.FrameCount.ToString(CultureInfo.InvariantCulture)} x " +
            $"{metrics.OutputWidth.ToString(CultureInfo.InvariantCulture)} x " +
            metrics.OutputHeight.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine(
            $"interval-ms: {metrics.FrameIntervalMilliseconds.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"pixel-bytes: {metrics.PixelBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"palette-bytes: {metrics.PaletteBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"estimated-rom-bytes: {metrics.EstimatedRomBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"source-sha256: {metrics.SourceSha256}");
        Console.WriteLine($"generated-source: {Path.GetFullPath(request.OutputSourcePath)}");
        Console.WriteLine($"metrics: {Path.GetFullPath(request.OutputMetricsPath)}");
        return SuccessExitCode;
    }

    private static int ValidateForm(string formPath, string catalogPath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        WriteDiagnostics(catalog.Diagnostics);
        if (!catalog.IsValid || catalog.Catalog is null)
        {
            return ValidationExitCode;
        }
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(
            formPath,
            catalog.Catalog,
            FormImportMode.Strict);
        WriteDiagnostics(form.Diagnostics);
        if (!form.IsValid || form.Document is null || form.Sha256 is null)
        {
            return ValidationExitCode;
        }
        Console.WriteLine("VALID");
        Console.WriteLine($"id: {form.Document.Id}");
        Console.WriteLine($"components: {form.Document.Components.Length}");
        Console.WriteLine($"form-sha256: {form.Sha256}");
        return SuccessExitCode;
    }

    private static int DiagnoseFormJson(string formPath, string catalogPath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        FormDocumentLoadResult? form = catalog.IsValid && catalog.Catalog is not null
            ? FormXmlImporter.LoadFile(formPath, catalog.Catalog, FormImportMode.Strict)
            : null;
        ProfileDiagnostic[] diagnostics =
        [
            .. catalog.Diagnostics,
            .. form?.Diagnostics ?? []
        ];
        bool valid = catalog.IsValid && form?.IsValid == true;
        using (Utf8JsonWriter writer = new(Console.OpenStandardOutput(), new JsonWriterOptions
        {
            Indented = false
        }))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("valid", valid);
            if (form?.Document is not null)
            {
                writer.WriteString("formId", form.Document.Id);
            }
            if (form?.Sha256 is not null)
            {
                writer.WriteString("sha256", form.Sha256);
            }
            writer.WriteStartArray("diagnostics");
            foreach (ProfileDiagnostic diagnostic in diagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("severity", diagnostic.Severity.ToString().ToLowerInvariant());
                writer.WriteString("path", diagnostic.Path);
                writer.WriteString("message", diagnostic.Message);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        Console.WriteLine();
        return valid ? SuccessExitCode : ValidationExitCode;
    }

    private static int NormalizeForm(
        string formPath,
        string catalogPath,
        string outputPath,
        FormImportMode mode)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        WriteDiagnostics(catalog.Diagnostics);
        if (!catalog.IsValid || catalog.Catalog is null)
        {
            return ValidationExitCode;
        }
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(formPath, catalog.Catalog, mode);
        WriteDiagnostics(form.Diagnostics);
        if (!form.IsValid || form.CanonicalXmlUtf8 is null || form.Sha256 is null)
        {
            return ValidationExitCode;
        }
        if (!TryWrite(outputPath, form.CanonicalXmlUtf8, "LVFC0001"))
        {
            return ValidationExitCode;
        }
        Console.WriteLine($"form-sha256: {form.Sha256}");
        return SuccessExitCode;
    }

    private static int CheckFormSurface(
        string formPath,
        string catalogPath,
        string surfacePath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
        WriteDiagnostics(catalog.Diagnostics);
        WriteDiagnostics(surface.Diagnostics);
        if (!catalog.IsValid || catalog.Catalog is null ||
            !surface.IsValid || surface.Profile is null)
        {
            return ValidationExitCode;
        }
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(
            formPath,
            catalog.Catalog,
            FormImportMode.Strict);
        WriteDiagnostics(form.Diagnostics);
        if (!form.IsValid || form.Document is null)
        {
            return ValidationExitCode;
        }
        FormResolutionResult resolution = FormDocumentResolver.Resolve(form.Document, catalog.Catalog);
        WriteDiagnostics(resolution.Diagnostics);
        if (!resolution.IsValid || resolution.Document is null)
        {
            return ValidationExitCode;
        }
        FormSurfaceValidationResult result = FormSurfaceValidator.Validate(
            resolution.Document,
            surface.Profile);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid)
        {
            return ValidationExitCode;
        }
        Console.WriteLine("COMPATIBLE");
        Console.WriteLine($"surface: {surface.Profile.Id}");
        return SuccessExitCode;
    }

    private static int ExportFormCpp(
        string formPath,
        string catalogPath,
        string headerPath,
        string integrationPath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        WriteDiagnostics(catalog.Diagnostics);
        if (!catalog.IsValid || catalog.Catalog is null)
        {
            return ValidationExitCode;
        }
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(
            formPath,
            catalog.Catalog,
            FormImportMode.Strict);
        WriteDiagnostics(form.Diagnostics);
        if (!form.IsValid || form.Document is null)
        {
            return ValidationExitCode;
        }
        FormCppCompilationResult compilation = FormCppCompiler.Compile(form.Document, catalog.Catalog);
        WriteDiagnostics(compilation.Diagnostics);
        if (!compilation.IsValid)
        {
            return ValidationExitCode;
        }

        byte[]? existing = null;
        try
        {
            if (File.Exists(integrationPath))
            {
                existing = File.ReadAllBytes(integrationPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"LVFC0002 error {integrationPath}: {exception.Message}");
            return ValidationExitCode;
        }
        PreservedRegionMergeResult merged = PreservedRegionMerger.Merge(
            compilation.IntegrationUtf8!,
            existing);
        WriteDiagnostics(merged.Diagnostics);
        if (!merged.IsValid)
        {
            string candidatePath = integrationPath + ".candidate";
            TryWrite(candidatePath, compilation.IntegrationUtf8!, "LVFC0003");
            Console.Error.WriteLine(
                $"LVFC0004 error {integrationPath}: Existing user code was not overwritten; inspect the candidate.");
            return ValidationExitCode;
        }
        if (!TryWrite(headerPath, compilation.HeaderUtf8!, "LVFC0005") ||
            !TryWrite(integrationPath, merged.Utf8!, "LVFC0006"))
        {
            return ValidationExitCode;
        }
        Console.WriteLine($"source-form-sha256: {compilation.SourceXmlSha256}");
        return SuccessExitCode;
    }

    private static int BudgetForm(
        string formPath,
        string catalogPath,
        string targetPath,
        string workspaceText)
    {
        if (!long.TryParse(
                workspaceText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long workspaceBytes))
        {
            Console.Error.WriteLine("LVFC0007 error workspaceBytes: Expected a nonnegative integer.");
            return UsageExitCode;
        }
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        TargetProfileLoadResult target = TargetProfileLoader.LoadFile(targetPath);
        WriteDiagnostics(catalog.Diagnostics);
        WriteDiagnostics(target.Diagnostics);
        if (!catalog.IsValid || catalog.Catalog is null || !target.IsValid || target.Profile is null)
        {
            return ValidationExitCode;
        }
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(
            formPath,
            catalog.Catalog,
            FormImportMode.Strict);
        WriteDiagnostics(form.Diagnostics);
        if (!form.IsValid || form.Document is null)
        {
            return ValidationExitCode;
        }
        FormResolutionResult resolution = FormDocumentResolver.Resolve(form.Document, catalog.Catalog);
        FormCppCompilationResult compilation = FormCppCompiler.Compile(form.Document, catalog.Catalog);
        WriteDiagnostics(resolution.Diagnostics);
        WriteDiagnostics(compilation.Diagnostics);
        if (!resolution.IsValid || resolution.Document is null || !compilation.IsValid)
        {
            return ValidationExitCode;
        }
        long generatedBytes = checked(
            (long)compilation.HeaderUtf8!.Length + compilation.IntegrationUtf8!.Length);
        FormBudgetReport budget = FormBudgetAnalyzer.Analyze(
            resolution.Document,
            new FormBudgetRequest
            {
                Target = target.Profile,
                RequiredRenderWorkspaceBytes = workspaceBytes,
                GeneratedSourceBytes = generatedBytes
            });
        WriteDiagnostics(budget.Diagnostics);
        Console.WriteLine(budget.HasUnknownUses ? "budget-evidence: unevaluated" : "budget-evidence: known");
        foreach (FormBudgetUse use in budget.Uses)
        {
            string used = use.Used?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
            string limit = use.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "not-specified";
            Console.WriteLine($"{use.Name}: {used}/{limit} {use.Units}");
        }
        return budget.IsValid ? SuccessExitCode : ValidationExitCode;
    }

    private static int ValidateProfile(string path)
    {
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.LoadFile(path);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.Profile is null || result.Sha256 is null)
        {
            return ValidationExitCode;
        }

        DisplaySurfaceProfile profile = result.Profile;
        Console.WriteLine("VALID");
        Console.WriteLine($"id: {profile.Id}");
        Console.WriteLine($"name: {profile.DisplayName}");
        Console.WriteLine($"shape: {ToText(profile.Shape)}");
        Console.WriteLine($"pixels: {profile.PixelSize.Width}x{profile.PixelSize.Height}");
        Console.WriteLine($"active-area-mm: {DescribePhysicalSize(profile)}");
        Console.WriteLine($"pixel-density-ppi: {ComputePixelDensity(profile):0.##}");
        Console.WriteLine($"painter-format: {ToText(profile.PainterSurface.Format)}");
        Console.WriteLine($"profile-sha256: {result.Sha256}");
        return SuccessExitCode;
    }

    private static int CanonicalizeProfile(string inputPath, string outputPath)
    {
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.LoadFile(inputPath);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.CanonicalUtf8 is null || result.Sha256 is null)
        {
            return ValidationExitCode;
        }

        try
        {
            AtomicFileWriter.WriteAllBytes(outputPath, result.CanonicalUtf8);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"LVDS0006 error {outputPath}: {exception.Message}");
            return ValidationExitCode;
        }

        Console.WriteLine($"WROTE {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"profile-sha256: {result.Sha256}");
        return SuccessExitCode;
    }

    private static int ValidateWidgetCatalog(string path)
    {
        WidgetCatalogLoadResult result = WidgetCatalogLoader.LoadFile(path);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.Catalog is null || result.Sha256 is null)
        {
            return ValidationExitCode;
        }

        Console.WriteLine("VALID");
        Console.WriteLine($"id: {result.Catalog.Id}");
        Console.WriteLine($"schema-version: {result.Catalog.SchemaVersion}");
        Console.WriteLine($"compatibility-version: {result.Catalog.CompatibilityVersion}");
        Console.WriteLine($"widgets: {result.Catalog.Widgets.Length}");
        Console.WriteLine($"properties: {result.Catalog.Properties.Length}");
        Console.WriteLine($"widget-catalog-sha256: {result.Sha256}");
        return SuccessExitCode;
    }

    private static int CanonicalizeWidgetCatalog(string inputPath, string outputPath)
    {
        WidgetCatalogLoadResult result = WidgetCatalogLoader.LoadFile(inputPath);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.CanonicalUtf8 is null || result.Sha256 is null)
        {
            return ValidationExitCode;
        }
        if (!TryWrite(outputPath, result.CanonicalUtf8, "LVWS0006"))
        {
            return ValidationExitCode;
        }

        Console.WriteLine($"widget-catalog-sha256: {result.Sha256}");
        return SuccessExitCode;
    }

    private static int CompileWidgetCatalog(
        string catalogPath,
        string csharpOutputPath,
        string cppOutputPath,
        string documentationOutputPath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        WidgetSchemaCompilationResult compilation = WidgetSchemaCompiler.Compile(catalog);
        WriteDiagnostics(compilation.Diagnostics);
        if (!compilation.IsValid || catalog.Sha256 is null)
        {
            return ValidationExitCode;
        }
        if (!TryWrite(csharpOutputPath, compilation.CSharpUtf8!, "LVWG0001") ||
            !TryWrite(cppOutputPath, compilation.CppUtf8!, "LVWG0002") ||
            !TryWrite(documentationOutputPath, compilation.DocumentationUtf8!, "LVWG0003"))
        {
            return ValidationExitCode;
        }

        Console.WriteLine($"widget-catalog-sha256: {catalog.Sha256}");
        return SuccessExitCode;
    }

    private static int CheckWidgetCatalogCompatibility(string previousPath, string currentPath)
    {
        WidgetCatalogLoadResult previous = WidgetCatalogLoader.LoadFile(previousPath);
        WidgetCatalogLoadResult current = WidgetCatalogLoader.LoadFile(currentPath);
        WidgetCatalogCompatibilityResult compatibility =
            WidgetCatalogCompatibilityAnalyzer.Compare(previous, current);
        WriteDiagnostics(compatibility.Diagnostics);
        if (!previous.IsValid || !current.IsValid)
        {
            return ValidationExitCode;
        }
        if (!compatibility.IsCompatible)
        {
            Console.WriteLine("INCOMPATIBLE");
            return CompatibilityExitCode;
        }

        Console.WriteLine("COMPATIBLE");
        return SuccessExitCode;
    }

    private static int ValidateTarget(string path)
    {
        TargetProfileLoadResult result = TargetProfileLoader.LoadFile(path);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.Profile is null || result.Sha256 is null)
        {
            return ValidationExitCode;
        }

        TargetProfile profile = result.Profile;
        Console.WriteLine("VALID");
        Console.WriteLine($"id: {profile.Id}");
        Console.WriteLine($"name: {profile.DisplayName}");
        Console.WriteLine($"surface-family: {profile.SurfaceCompatibility.Family}");
        Console.WriteLine($"architecture: {ToText(profile.Runtime.Architecture)}");
        Console.WriteLine($"environment: {ToText(profile.Runtime.Environment)}");
        Console.WriteLine($"rendering-mode: {ToText(profile.Runtime.RenderingMode)}");
        if (profile.Runtime.Tile is not null)
        {
            Console.WriteLine($"tile: {profile.Runtime.Tile.Width}x{profile.Runtime.Tile.Height}");
        }
        Console.WriteLine($"target-sha256: {result.Sha256}");
        return SuccessExitCode;
    }

    private static int CanonicalizeTarget(string inputPath, string outputPath)
    {
        TargetProfileLoadResult result = TargetProfileLoader.LoadFile(inputPath);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.CanonicalUtf8 is null || result.Sha256 is null)
        {
            return ValidationExitCode;
        }

        if (!TryWrite(outputPath, result.CanonicalUtf8, "LVTP0006"))
        {
            return ValidationExitCode;
        }
        Console.WriteLine($"target-sha256: {result.Sha256}");
        return SuccessExitCode;
    }

    private static int CompareSurfaces(string currentPath, string proposedPath)
    {
        DisplaySurfaceLoadResult current = DisplaySurfaceProfileLoader.LoadFile(currentPath);
        DisplaySurfaceLoadResult proposed = DisplaySurfaceProfileLoader.LoadFile(proposedPath);
        WriteDiagnostics(current.Diagnostics);
        WriteDiagnostics(proposed.Diagnostics);
        if (!current.IsValid || current.Profile is null ||
            !proposed.IsValid || proposed.Profile is null)
        {
            return ValidationExitCode;
        }

        SurfaceMigrationResult migration = SurfaceMigrationAnalyzer.Compare(
            current.Profile,
            proposed.Profile);
        WriteDiagnostics(migration.Diagnostics);
        if (!migration.RequiresMigration)
        {
            Console.WriteLine("NO MIGRATION");
            return SuccessExitCode;
        }

        Console.WriteLine($"MIGRATION REQUIRED: {migration.Kind}");
        return MigrationExitCode;
    }

    private static int ListCatalog(string surfaceCatalogPath, string targetCatalogPath)
    {
        ProfileCatalogLoadResult catalog = ProfileCatalogLoader.Load(
            surfaceCatalogPath,
            targetCatalogPath);
        WriteCatalogDiagnostics(catalog);
        if (!catalog.IsValid)
        {
            return ValidationExitCode;
        }

        Console.WriteLine("VALID CATALOG");
        foreach (DisplaySurfaceLoadResult surface in catalog.Surfaces.OrderBy(
                     item => item.Profile!.Id,
                     StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"surface: {surface.Profile!.Id} {surface.Sha256} " +
                $"{surface.Profile.PixelSize.Width}x{surface.Profile.PixelSize.Height} " +
                $"format={ToText(surface.Profile.PainterSurface.Format)}");
        }
        foreach (TargetProfileLoadResult target in catalog.Targets.OrderBy(
                     item => item.Profile!.Id,
                     StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"target: {target.Profile!.Id} {target.Sha256} " +
                $"family={target.Profile.SurfaceCompatibility.Family} " +
                $"mode={ToText(target.Profile.Runtime.RenderingMode)}");
        }
        return SuccessExitCode;
    }

    private static int ValidateProject(string manifestPath)
    {
        DesignerProjectLoadResult project = DesignerProjectLoader.LoadFile(manifestPath);
        WriteDiagnostics(project.Diagnostics);
        if (!project.IsValid || project.Manifest is null || project.Sha256 is null)
        {
            return ValidationExitCode;
        }

        Console.WriteLine("VALID");
        Console.WriteLine($"id: {project.Manifest.Id}");
        Console.WriteLine($"project-sha256: {project.Sha256}");
        return SuccessExitCode;
    }

    private static int ValidateResource(string manifestPath)
    {
        ResourceManifestLoadResult resource = ResourceManifestLoader.LoadFile(manifestPath);
        WriteDiagnostics(resource.Diagnostics);
        if (!resource.IsValid || resource.Manifest is null || resource.Sha256 is null)
        {
            return ValidationExitCode;
        }

        Console.WriteLine("VALID");
        Console.WriteLine($"id: {resource.Manifest.Id}");
        Console.WriteLine($"resource-sha256: {resource.Sha256}");
        return SuccessExitCode;
    }

    private static int CanonicalizeResource(string inputPath, string outputPath)
    {
        ResourceManifestLoadResult resource = ResourceManifestLoader.LoadFile(inputPath);
        WriteDiagnostics(resource.Diagnostics);
        if (!resource.IsValid || resource.CanonicalUtf8 is null || resource.Sha256 is null)
        {
            return ValidationExitCode;
        }
        if (!TryWrite(outputPath, resource.CanonicalUtf8, "LVRM0006"))
        {
            return ValidationExitCode;
        }
        Console.WriteLine($"resource-sha256: {resource.Sha256}");
        return SuccessExitCode;
    }

    private static int VerifyResource(string manifestPath, string projectRoot)
    {
        ResourceManifestLoadResult resource = ResourceManifestLoader.LoadFile(manifestPath);
        WriteDiagnostics(resource.Diagnostics);
        if (!resource.IsValid || resource.Manifest is null || resource.Sha256 is null)
        {
            return ValidationExitCode;
        }

        ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
            resource.Manifest,
            projectRoot);
        WriteDiagnostics(verification.Diagnostics);
        if (!verification.IsValid)
        {
            return ValidationExitCode;
        }

        Console.WriteLine("VERIFIED");
        Console.WriteLine($"id: {resource.Manifest.Id}");
        Console.WriteLine($"resource-sha256: {resource.Sha256}");
        foreach (VerifiedResourceArtifact artifact in verification.Artifacts.OrderBy(
                     item => item.Path,
                     StringComparer.Ordinal))
        {
            Console.WriteLine($"artifact: {artifact.Path} {artifact.ByteCount} {artifact.Sha256}");
        }
        return SuccessExitCode;
    }

    private static int CanonicalizeProject(string inputPath, string outputPath)
    {
        DesignerProjectLoadResult result = DesignerProjectLoader.LoadFile(inputPath);
        WriteDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.CanonicalUtf8 is null || result.Sha256 is null)
        {
            return ValidationExitCode;
        }
        if (!TryWrite(outputPath, result.CanonicalUtf8, "LVPM0006"))
        {
            return ValidationExitCode;
        }
        Console.WriteLine($"project-sha256: {result.Sha256}");
        return SuccessExitCode;
    }

    private static int ResolveProject(
        string manifestPath,
        string surfaceCatalogPath)
    {
        DesignerProjectLoadResult project = DesignerProjectLoader.LoadFile(manifestPath);
        IReadOnlyList<DisplaySurfaceLoadResult> surfaces = [];
        // Version-3 native display projects have no editing surface to look up.
        if (project.Manifest?.SchemaVersion != 3)
        {
            DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(surfaceCatalogPath);
            WriteCatalogDiagnostics(catalog);
            if (!catalog.IsValid)
            {
                return ValidationExitCode;
            }
            surfaces = catalog.Surfaces;
        }

        DesignerProjectResolutionResult resolution = DesignerProjectResolver.Resolve(
            project,
            surfaces,
            Path.GetDirectoryName(Path.GetFullPath(manifestPath))!);
        WriteDiagnostics(resolution.Diagnostics);
        if (!resolution.IsValid || resolution.Project is null)
        {
            return ValidationExitCode;
        }

        WriteResolvedProject(resolution.Project);
        return SuccessExitCode;
    }

    private static int CreateNativeDisplayProject(string[] arguments)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(arguments[7]);
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(arguments[6]);
        WriteDiagnostics(catalog.Diagnostics);
        WriteDiagnostics(input.Diagnostics);
        if (!catalog.IsValid || catalog.Catalog is null || !input.IsValid || input.Configuration is null)
        {
            return ValidationExitCode;
        }

        DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNativeDisplayProject(new()
        {
            ProjectDirectory = arguments[2],
            ProjectId = arguments[3],
            DisplayName = arguments[4],
            Definition = DisplayDefinitionProfileLoader.LoadFile(arguments[5]),
            Input = input.Configuration,
            Catalog = catalog.Catalog
        });
        WriteDiagnostics(created.Diagnostics);
        if (!created.IsValid || created.Workspace is null)
        {
            return ValidationExitCode;
        }
        Console.WriteLine($"WROTE {created.Workspace.ManifestPath}");
        WriteResolvedProject(created.Workspace.ResolvedProject);
        return SuccessExitCode;
    }

    private static int CreateProject(string[] arguments)
    {
        string outputPath = arguments[2];
        string schemaReference = arguments[3];
        string projectId = arguments[4];
        string displayName = arguments[5];
        string surfaceCatalogPath = arguments[6];
        string surfaceId = arguments[7];

        DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(surfaceCatalogPath);
        WriteCatalogDiagnostics(catalog);
        if (!catalog.IsValid)
        {
            return ValidationExitCode;
        }

        DisplaySurfaceLoadResult? surface = FindUniqueSurface(catalog.Surfaces, surfaceId);
        if (surface is null)
        {
            return ValidationExitCode;
        }

        DesignerProjectLoadResult project = DesignerProjectFactory.Create(
            schemaReference,
            projectId,
            displayName,
            surface);
        WriteDiagnostics(project.Diagnostics);
        DesignerProjectResolutionResult resolution = DesignerProjectResolver.Resolve(
            project,
            catalog.Surfaces);
        WriteDiagnostics(resolution.Diagnostics);
        if (!resolution.IsValid || resolution.Project is null || project.CanonicalUtf8 is null ||
            project.Sha256 is null)
        {
            return ValidationExitCode;
        }
        if (!TryWrite(outputPath, project.CanonicalUtf8, "LVPM0006"))
        {
            return ValidationExitCode;
        }
        Console.WriteLine($"project-sha256: {project.Sha256}");
        WriteResolvedProject(resolution.Project);
        return SuccessExitCode;
    }

    private static DisplaySurfaceLoadResult? FindUniqueSurface(
        IEnumerable<DisplaySurfaceLoadResult> surfaces,
        string id)
    {
        DisplaySurfaceLoadResult[] matches = [.. surfaces
            .Where(surface => surface.Profile?.Id == id)
            .Take(2)];
        if (matches.Length == 1)
        {
            return matches[0];
        }
        Console.Error.WriteLine(
            $"LVCLI1001 error surfaceCatalog: Surface ID '{id}' must occur exactly once.");
        return null;
    }

    private static void WriteResolvedProject(ResolvedDesignerProject resolved)
    {
        Console.WriteLine("RESOLVED");
        Console.WriteLine($"id: {resolved.Manifest.Id}");
        if (resolved.Surface is null)
        {
            WriteNativeDisplay(resolved.Display);
        }
        else
        {
            Console.WriteLine($"surface: {resolved.Surface.Id}");
            Console.WriteLine($"surface-sha256: {resolved.SurfaceSha256}");
        }
        foreach (LoadedProjectResource resource in resolved.Resources.OrderBy(
                     item => item.RelativePath,
                     StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"resource: {resource.Manifest.Id} {resource.ManifestSha256} " +
                $"path={resource.RelativePath}");
        }
        foreach (ResolvedResourceVariant resource in resolved.ResourceVariants.OrderBy(
                     item => item.ResourceId,
                     StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"resource-variant: {resource.ResourceId} {resource.Variant.Id} " +
                $"format={resource.Variant.Output.Format} bytes={resource.Variant.Output.ByteCount}");
        }
        Console.WriteLine($"resolved-project-sha256: {resolved.ResolvedSha256}");
    }

    // Version-3 projects have no editing surface; their locked display contract is the identity.
    private static void WriteNativeDisplay(DesignerDisplay display)
    {
        Console.WriteLine($"display-definition: {display.Id} format={display.Format} " +
            $"extent={display.Width}x{display.Height} address-space={display.Definition?.AddressSpace}");
        Console.WriteLine($"display-definition-sha256: {display.DefinitionSha256}");
        Console.WriteLine($"logical-input: {display.Input.Id}");
        Console.WriteLine($"logical-input-sha256: {display.InputSha256}");
        Console.WriteLine("target: unevaluated");
    }

    private static bool TryWrite(string outputPath, ReadOnlySpan<byte> contents, string diagnosticCode)
    {
        try
        {
            AtomicFileWriter.WriteAllBytes(outputPath, contents);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"{diagnosticCode} error {outputPath}: {exception.Message}");
            return false;
        }

        Console.WriteLine($"WROTE {Path.GetFullPath(outputPath)}");
        return true;
    }

    private static decimal ComputePixelDensity(DisplaySurfaceProfile profile)
    {
        decimal physicalPixels;
        decimal physicalMillimeters;
        if (profile.Shape == SurfaceShape.Circle)
        {
            physicalPixels = profile.PixelSize.Width;
            physicalMillimeters = profile.PhysicalSize.DiameterMillimeters!.Value;
        }
        else
        {
            physicalPixels = profile.PixelSize.Width;
            physicalMillimeters = profile.PhysicalSize.WidthMillimeters!.Value;
        }
        return physicalPixels * 25.4M / physicalMillimeters;
    }

    private static string DescribePhysicalSize(DisplaySurfaceProfile profile)
    {
        if (profile.Shape == SurfaceShape.Circle)
        {
            return $"diameter {profile.PhysicalSize.DiameterMillimeters!.Value.ToString(CultureInfo.InvariantCulture)}";
        }
        return $"{profile.PhysicalSize.WidthMillimeters!.Value.ToString(CultureInfo.InvariantCulture)}x" +
            profile.PhysicalSize.HeightMillimeters!.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static async Task<int> RenderPreviewAsync(
        string formPath,
        string surfacePath,
        string workerPath,
        string outputPath,
        IReadOnlyList<PreviewInput> inputs)
    {
        DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
        WriteDiagnostics(surface.Diagnostics);
        if (!surface.IsValid || surface.Profile is null)
        {
            return ValidationExitCode;
        }
        byte[] snapshot;
        try
        {
            snapshot = File.ReadAllBytes(formPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"LVPW0101 error {formPath}: {exception.Message}");
            return ValidationExitCode;
        }
        PreviewSurface descriptor = PreviewSurfaceFactory.Create(surface.Profile);
        try
        {
            await using PreviewWorkerClient client = new(workerPath);
            PreviewFrame frame = await client.RenderAsync(
                new PreviewRenderRequest(descriptor, snapshot, inputs),
                timeout: TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            if (frame.Status != PreviewStatus.Ok)
            {
                Console.Error.WriteLine(
                    $"LVPW{frame.DiagnosticCode:D4} error {formPath}: {frame.Diagnostic}");
                return ValidationExitCode;
            }
            AtomicFileWriter.WriteAllBytes(outputPath, frame.Pixels);
            Console.WriteLine("RENDERED");
            Console.WriteLine($"request-id: {frame.RequestId.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"surface: {surface.Profile.Id}");
            Console.WriteLine($"format: {ToText(descriptor.PixelFormat)}");
            Console.WriteLine($"size: {descriptor.Width}x{descriptor.Height}");
            Console.WriteLine($"frame-bytes: {frame.Pixels.Length.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"damage-regions: {frame.DamageRegions.Count.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"accepted-inputs: {frame.AcceptedInputCount.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine(
                $"native-render-microseconds: {(frame.NativeRenderTime.Ticks / 10).ToString(CultureInfo.InvariantCulture)}");
            return SuccessExitCode;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
                PreviewWorkerException or PlatformNotSupportedException)
        {
            Console.Error.WriteLine($"LVPW0102 error {workerPath}: {exception.Message}");
            return ValidationExitCode;
        }
    }

    private static string ToText<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string name = Enum.GetName(value) ?? throw new ArgumentOutOfRangeException(nameof(value));
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static void WriteDiagnostics(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        foreach (ProfileDiagnostic diagnostic in diagnostics)
        {
            Console.Error.WriteLine(
                $"{diagnostic.Code} {diagnostic.Severity.ToString().ToLowerInvariant()} " +
                $"{diagnostic.Path}: {diagnostic.Message}");
        }
    }

    private static void WriteCatalogDiagnostics(ProfileCatalogLoadResult catalog)
    {
        WriteDiagnostics(catalog.Diagnostics);
        foreach (DisplaySurfaceLoadResult surface in catalog.Surfaces)
        {
            WriteDiagnostics(surface.Diagnostics);
        }
        foreach (TargetProfileLoadResult target in catalog.Targets)
        {
            WriteDiagnostics(target.Diagnostics);
        }
    }

    private static void WriteCatalogDiagnostics(DisplaySurfaceCatalogLoadResult catalog)
    {
        WriteDiagnostics(catalog.Diagnostics);
        foreach (DisplaySurfaceLoadResult surface in catalog.Surfaces)
        {
            WriteDiagnostics(surface.Diagnostics);
        }
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("LitheView Designer profile tools");
        Console.Error.WriteLine("  display check <form.xml> <widgets.json> <display.json> <input.json> [resource-manifest ...]");
        Console.Error.WriteLine("  display export-cpp <form.xml> <widgets.json> <display.json> <input.json> <output-directory> [resource-manifest ...]");
        Console.Error.WriteLine("  simulator export <project.json> <surface-catalog> <widgets.json> <output-directory>");
        Console.Error.WriteLine("  simulator build <simulation-directory> <LitheView-directory> [Debug|Release]");
        Console.Error.WriteLine("  simulator create-tutorial <project-directory> <surface.json> <input.json> <surface-catalog> <widgets.json> [--existing]");
        Console.Error.WriteLine("  simulator configure-paths <project.json> <application-root> <library> <component-output> <build-root>");
        Console.Error.WriteLine("  simulator paths <project.json> <designer-checkout>");
        Console.Error.WriteLine("  simulator export-project <project.json> <designer-checkout>");
        Console.Error.WriteLine("  simulator build-project <project.json> <designer-checkout> [Debug|Release]");
        Console.Error.WriteLine("  display check-target <form.xml> <widgets.json> <display.json> <input.json> <target.json> <evidence.json|-> [resource-manifest ...]");
        Console.Error.WriteLine("  form validate <form.xml> <widgets.json>");
        Console.Error.WriteLine("  form diagnose-json <form.xml> <widgets.json>");
        Console.Error.WriteLine("  form import <legacy.xml> <widgets.json> <canonical.xml>");
        Console.Error.WriteLine("  form normalize <input.xml> <widgets.json> <canonical.xml>");
        Console.Error.WriteLine("  form export-xml <input.xml> <widgets.json> <canonical.xml>");
        Console.Error.WriteLine("  form surface-check <form.xml> <widgets.json> <surface.json>");
        Console.Error.WriteLine(
            "  form export-cpp <form.xml> <widgets.json> <generated.hpp> <integration.cpp>");
        Console.Error.WriteLine(
            "  form budget <form.xml> <widgets.json> <target.json> <render-workspace-bytes>");
        Console.Error.WriteLine("  profile validate <display-surface.json>");
        Console.Error.WriteLine("  profile canonicalize <input.json> <output.json>");
        Console.Error.WriteLine("  widget-schema validate <widgets.json>");
        Console.Error.WriteLine("  widget-schema canonicalize <input.json> <output.json>");
        Console.Error.WriteLine(
            "  widget-schema compile <widgets.json> <generated.cs> <generated.hpp> <reference.md>");
        Console.Error.WriteLine(
            "  widget-schema check-compatibility <previous.json> <current.json>");
        Console.Error.WriteLine("  surface compare <current.json> <proposed.json>");
        Console.Error.WriteLine("  target validate <target-profile.json>");
        Console.Error.WriteLine("  target canonicalize <input.json> <output.json>");
        Console.Error.WriteLine("  resource validate <resource-manifest.json>");
        Console.Error.WriteLine("  resource canonicalize <input.json> <output.json>");
        Console.Error.WriteLine("  resource verify <resource-manifest.json> <project-root>");
        Console.Error.WriteLine(
            "  font convert <font.ttf|otf> <generated.cpp> <metrics.json> <Symbol> <entryPoint> " +
            "<pixel-size> <Regular|Bold> <style-label> <weight> <Alpha1|Alpha8> " +
            "<repertoire> <convert-font.ps1>");
        Console.Error.WriteLine(
            "  image convert <image> <generated.cpp> <metrics.json> <Symbol> <entryPoint> " +
            "<Image|Icon> <tintable> <format> <crop-x> <crop-y> <crop-width> <crop-height> " +
            "<output-width> <output-height> <NearestNeighbor|Bilinear> " +
            "<Preserve|Threshold|Opaque> <alpha-threshold> <None|Ordered4x4> " +
            "<tint-AARRGGBB> <matte-AARRGGBB> <palette-AARRGGBB,...> <convert-image.ps1>");
        Console.Error.WriteLine(
            "  animation convert <sprite-sheet> <generated.cpp> <metrics.json> <Symbol> <entryPoint> " +
            "<tintable> <format> <sheet-x> <sheet-y> <source-frame-width> <source-frame-height> " +
            "<columns> <frame-count> <output-width> <output-height> <NearestNeighbor|Bilinear> " +
            "<Preserve|Threshold|Opaque> <alpha-threshold> <None|Ordered4x4> " +
            "<tint-AARRGGBB> <matte-AARRGGBB> <palette-AARRGGBB,...> <interval-ms> " +
            "<repeat-index> <autoplay> <convert-animation.ps1>");
        Console.Error.WriteLine("  catalog list <surface-catalog-directory> <target-catalog-directory>");
        Console.Error.WriteLine("  project validate <project.json>");
        Console.Error.WriteLine("  project canonicalize <input.json> <output.json>");
        Console.Error.WriteLine(
            "  project resolve <project.json> <surface-catalog-directory>");
        Console.Error.WriteLine(
            "  project create <output.json> <schema-reference> <project-id> <display-name> " +
            "<surface-catalog-directory> <surface-id>");
        Console.Error.WriteLine(
            "  project create-native <project-directory> <project-id> <display-name> " +
            "<display-definition.json> <logical-input.json> <widgets.json>");
        Console.Error.WriteLine(
            "  preview render <form.xml> <display-surface.json> <worker.exe> <frame.raw> " +
            "[--input <focus|next|previous|select|key:K|press:X,Y|move:X,Y|release:X,Y|cancel:X,Y|elapsed:MS>]...");
    }
}
