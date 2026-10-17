// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Security.Cryptography;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Resources;

/*
 * Animation import turns one reviewed sprite sheet into a bounded, caller-clocked sequence:
 *
 *   source sheet + row-major grid + representation/timing choices
 *       -> convert-animation.ps1
 *       -> one immutable pixel payload, optional shared palette, and borrowed Bitmap descriptors
 *       -> strict metrics and canonical resource manifest
 *       -> an application ResourceProvider that gives the frames stable runtime names
 *
 * Frames have one size and one native Bitmap representation. They share a palette when indexed4 is
 * selected, so neither a decoder nor per-frame palette copies enter the embedded application.
 * Interval, repeat index, and autoplay are authored defaults; AnimatedImageView remains driven by the
 * consuming application's monotonic clock. The converter never introduces a thread or timer.
 *
 * Import is transactional and refuses overwrite or merge. As with every visual resource, this
 * contract deliberately says nothing about an MCU, bus, linker section, internal/external flash,
 * QSPI, RTOS, or deployment budget. Those decisions belong to the consuming application.
 */
public sealed record AnimationResourceImportRequest
{
    public required string ProjectManifestPath { get; init; }

    public required string SourceSpriteSheetPath { get; init; }

    public required string? SourceLicensePath { get; init; }

    public required string ResourceId { get; init; }

    public required string DisplayName { get; init; }

    public required string GeneratedSymbol { get; init; }

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

    public required string Origin { get; init; }

    public required string? Author { get; init; }

    public required string? SourceUri { get; init; }

    public required string? ProvenanceNotes { get; init; }

    public required string? SpdxId { get; init; }

    public required string? Attribution { get; init; }

    public required bool RedistributionApproved { get; init; }
}

public sealed record AnimationResourceImportResult
{
    public string? ResourceManifestPath { get; init; }

    public string? ResourceManifestSha256 { get; init; }

    public string? GeneratedSourcePath { get; init; }

    public AnimationConversionMetrics? Metrics { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => ResourceManifestPath is not null &&
        ResourceManifestSha256 is not null &&
        GeneratedSourcePath is not null &&
        Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class AnimationResourceImporter
{
    private const long MaximumSourceSpriteSheetBytes = 64L * 1024L * 1024L;
    private const string ResourceSchema = "https://litheview.dev/schema/resource-manifest.schema.json";

    public static async Task<AnimationResourceImportResult> ImportAsync(
        AnimationResourceImportRequest request,
        IAnimationConversionRunner runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(runner);

        List<ProfileDiagnostic> diagnostics = [.. ValidateRequest(request)];
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        DesignerProjectLoadResult project = DesignerProjectLoader.LoadFile(request.ProjectManifestPath);
        diagnostics.AddRange(project.Diagnostics);
        if (!project.IsValid || project.Manifest is null)
        {
            return Failed(diagnostics);
        }

        string fullProjectManifest = Path.GetFullPath(request.ProjectManifestPath);
        string projectRoot = Path.GetDirectoryName(fullProjectManifest)!;
        ProjectResourceSetLoadResult existing = ProjectResourceSetLoader.Load(project.Manifest, projectRoot);
        diagnostics.AddRange(existing.Diagnostics);
        if (!existing.IsValid)
        {
            return Failed(diagnostics);
        }
        if (existing.Resources.Any(item => item.Manifest.Id == request.ResourceId))
        {
            diagnostics.Add(Error(
                "LVAI1001",
                "$.resourceId",
                $"Resource ID '{request.ResourceId}' already exists in the project."));
            return Failed(diagnostics);
        }

        string stem = request.ResourceId.Replace('.', '-');
        string resourceRelativeDirectory = $"resources/{stem}";
        string resourceManifestRelativePath = $"{resourceRelativeDirectory}/resource.json";
        string sourceExtension = Path.GetExtension(request.SourceSpriteSheetPath).ToLowerInvariant();
        string sourceRelativePath = $"{resourceRelativeDirectory}/source/sprite-sheet{sourceExtension}";
        string licenseRelativePath = $"{resourceRelativeDirectory}/license/license.txt";
        string formatName = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(request.Format);
        string outputRelativePath = $"{resourceRelativeDirectory}/generated/animation-{formatName}.cpp";
        string stagingRoot = Path.Combine(projectRoot, $".{stem}.{Guid.NewGuid():N}.animation-import");
        string stagingResourceDirectory = Resolve(stagingRoot, resourceRelativeDirectory);
        string destinationResourceDirectory = Resolve(projectRoot, resourceRelativeDirectory);
        if (Directory.Exists(destinationResourceDirectory) || File.Exists(destinationResourceDirectory))
        {
            diagnostics.Add(Error(
                "LVAI1002",
                resourceRelativeDirectory,
                "The deterministic resource directory already exists; import never merges or overwrites it."));
            return Failed(diagnostics);
        }

        bool resourceMoved = false;
        try
        {
            string stagedSource = Resolve(stagingRoot, sourceRelativePath);
            string stagedOutput = Resolve(stagingRoot, outputRelativePath);
            string stagedMetrics = Resolve(
                stagingRoot,
                $"{resourceRelativeDirectory}/generated/animation.metrics.json");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedSource)!);
            File.Copy(request.SourceSpriteSheetPath, stagedSource, overwrite: false);
            string? stagedLicense = null;
            if (request.SourceLicensePath is not null)
            {
                stagedLicense = Resolve(stagingRoot, licenseRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedLicense)!);
                File.Copy(request.SourceLicensePath, stagedLicense, overwrite: false);
            }

            AnimationConversionResult conversion = await runner.ConvertAsync(
                ToConversionRequest(request, stagedSource, stagedOutput, stagedMetrics),
                cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(conversion.Diagnostics);
            if (!conversion.IsValid || conversion.Metrics is null || !File.Exists(stagedOutput))
            {
                return Failed(diagnostics);
            }
            AnimationConversionMetrics metrics = conversion.Metrics;
            ValidateConversion(request, stagedSource, metrics, diagnostics);
            if (HasErrors(diagnostics))
            {
                return Failed(diagnostics);
            }

            ResourceManifest manifest = CreateManifest(
                request,
                metrics,
                stagedSource,
                stagedLicense,
                stagedOutput,
                sourceRelativePath,
                request.SourceLicensePath is null ? null : licenseRelativePath,
                outputRelativePath,
                formatName);
            ResourceManifestLoadResult loadedManifest = ResourceManifestLoader.Load(
                ResourceManifestCanonicalizer.Canonicalize(manifest));
            diagnostics.AddRange(loadedManifest.Diagnostics);
            if (!loadedManifest.IsValid || loadedManifest.Manifest is null ||
                loadedManifest.CanonicalUtf8 is null || loadedManifest.Sha256 is null)
            {
                return Failed(diagnostics);
            }
            string stagedResourceManifest = Resolve(stagingRoot, resourceManifestRelativePath);
            AtomicFileWriter.WriteAllBytes(stagedResourceManifest, loadedManifest.CanonicalUtf8);
            ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
                loadedManifest.Manifest,
                stagingRoot);
            diagnostics.AddRange(verification.Diagnostics);
            if (!verification.IsValid)
            {
                return Failed(diagnostics);
            }

            DesignerProjectManifest updatedProject = project.Manifest with
            {
                ResourceManifests =
                [
                    .. project.Manifest.ResourceManifests,
                    resourceManifestRelativePath
                ]
            };
            DesignerProjectLoadResult checkedProject = DesignerProjectLoader.Load(
                DesignerProjectCanonicalizer.Canonicalize(updatedProject));
            diagnostics.AddRange(checkedProject.Diagnostics);
            if (!checkedProject.IsValid || checkedProject.CanonicalUtf8 is null)
            {
                return Failed(diagnostics);
            }

            File.Delete(stagedMetrics);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationResourceDirectory)!);
            Directory.Move(stagingResourceDirectory, destinationResourceDirectory);
            resourceMoved = true;
            AtomicFileWriter.WriteAllBytes(fullProjectManifest, checkedProject.CanonicalUtf8);
            return new AnimationResourceImportResult
            {
                ResourceManifestPath = Resolve(projectRoot, resourceManifestRelativePath),
                ResourceManifestSha256 = loadedManifest.Sha256,
                GeneratedSourcePath = Resolve(projectRoot, outputRelativePath),
                Metrics = metrics,
                Diagnostics = diagnostics
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
                InvalidOperationException or NotSupportedException)
        {
            diagnostics.Add(Error("LVAI1003", "$", $"Animation import failed: {exception.Message}"));
            if (resourceMoved)
            {
                TryDeleteDirectory(destinationResourceDirectory);
            }
            return Failed(diagnostics);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private static List<ProfileDiagnostic> ValidateRequest(AnimationResourceImportRequest request)
    {
        List<ProfileDiagnostic> diagnostics = [.. AnimationConversionRequestValidator.Validate(
            ToConversionRequest(
                request,
                request.SourceSpriteSheetPath,
                "generated.cpp",
                "metrics.json"))];
        if (string.IsNullOrWhiteSpace(request.ProjectManifestPath) ||
            !File.Exists(request.ProjectManifestPath))
        {
            diagnostics.Add(Error("LVAI1101", "$.projectManifestPath", "A readable designer project is required."));
        }
        if (!ProfileRules.IsValidIdentifier(request.ResourceId) ||
            string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 160)
        {
            diagnostics.Add(Error(
                "LVAI1102",
                "$.resource",
                "Resource ID and display name must follow the bounded resource-manifest contract."));
        }
        if (!File.Exists(request.SourceSpriteSheetPath) ||
            new FileInfo(request.SourceSpriteSheetPath).Length is <= 0 or > MaximumSourceSpriteSheetBytes ||
            Path.GetExtension(request.SourceSpriteSheetPath).ToLowerInvariant() is not
                (".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif" or ".tif" or ".tiff"))
        {
            diagnostics.Add(Error(
                "LVAI1103",
                "$.sourceSpriteSheetPath",
                "The sprite sheet must be a nonempty PNG, BMP, JPEG, GIF, or TIFF no larger than 64 MiB."));
        }
        if (request.SourceLicensePath is not null && !File.Exists(request.SourceLicensePath))
        {
            diagnostics.Add(Error("LVAI1104", "$.sourceLicensePath", "The selected license file does not exist."));
        }
        if (string.IsNullOrWhiteSpace(request.Origin) || request.Origin.Length > 1024 ||
            request.SpdxId is null && request.SourceLicensePath is null ||
            !request.RedistributionApproved)
        {
            diagnostics.Add(Error(
                "LVAI1105",
                "$.provenance",
                "Import requires origin, an SPDX ID or license file, and explicit redistribution approval."));
        }
        if (request.SourceUri is not null && !Uri.TryCreate(request.SourceUri, UriKind.Absolute, out _))
        {
            diagnostics.Add(Error("LVAI1106", "$.sourceUri", "The optional source URI must be absolute."));
        }
        return diagnostics;
    }

    private static AnimationConversionRequest ToConversionRequest(
        AnimationResourceImportRequest request,
        string input,
        string output,
        string metrics)
    {
        return new AnimationConversionRequest
        {
            InputSpriteSheetPath = input,
            OutputSourcePath = output,
            OutputMetricsPath = metrics,
            Symbol = request.GeneratedSymbol,
            EntryPoint = request.EntryPoint,
            Tintable = request.Tintable,
            Format = request.Format,
            SheetX = request.SheetX,
            SheetY = request.SheetY,
            SourceFrameWidth = request.SourceFrameWidth,
            SourceFrameHeight = request.SourceFrameHeight,
            Columns = request.Columns,
            FrameCount = request.FrameCount,
            OutputWidth = request.OutputWidth,
            OutputHeight = request.OutputHeight,
            ScaleMode = request.ScaleMode,
            AlphaMode = request.AlphaMode,
            AlphaThreshold = request.AlphaThreshold,
            Dither = request.Dither,
            TintArgb = request.TintArgb,
            MatteArgb = request.MatteArgb,
            PaletteArgb = request.PaletteArgb,
            FrameIntervalMilliseconds = request.FrameIntervalMilliseconds,
            RepeatIndex = request.RepeatIndex,
            Autoplay = request.Autoplay
        };
    }

    private static void ValidateConversion(
        AnimationResourceImportRequest request,
        string stagedSource,
        AnimationConversionMetrics metrics,
        List<ProfileDiagnostic> diagnostics)
    {
        string expectedFormat = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(request.Format);
        if (metrics.SourceSha256 != ComputeSha256(stagedSource) ||
            metrics.Tintable != request.Tintable || metrics.Format != expectedFormat ||
            metrics.SheetX != request.SheetX || metrics.SheetY != request.SheetY ||
            metrics.SourceFrameWidth != request.SourceFrameWidth ||
            metrics.SourceFrameHeight != request.SourceFrameHeight ||
            metrics.Columns != request.Columns || metrics.FrameCount != request.FrameCount ||
            metrics.OutputWidth != request.OutputWidth || metrics.OutputHeight != request.OutputHeight ||
            metrics.ScaleMode != request.ScaleMode.ToString() ||
            metrics.AlphaMode != request.AlphaMode.ToString() ||
            metrics.AlphaThreshold != request.AlphaThreshold || metrics.Dither != request.Dither.ToString() ||
            metrics.TintArgb != request.TintArgb || metrics.MatteArgb != request.MatteArgb ||
            metrics.PaletteArgb != request.PaletteArgb ||
            metrics.FrameIntervalMilliseconds != request.FrameIntervalMilliseconds ||
            metrics.RepeatIndex != request.RepeatIndex || metrics.Autoplay != request.Autoplay ||
            metrics.Symbol != request.GeneratedSymbol || metrics.EntryPoint != request.EntryPoint)
        {
            diagnostics.Add(Error(
                "LVAI1201",
                "$.converter",
                "Converter output does not exactly match the selected grid, representation, timing, or integration symbols."));
        }
    }

    private static ResourceManifest CreateManifest(
        AnimationResourceImportRequest request,
        AnimationConversionMetrics metrics,
        string stagedSource,
        string? stagedLicense,
        string stagedOutput,
        string sourceRelativePath,
        string? licenseRelativePath,
        string outputRelativePath,
        string formatName)
    {
        int rows = (metrics.FrameCount + metrics.Columns - 1) / metrics.Columns;
        return new ResourceManifest
        {
            Schema = ResourceSchema,
            SchemaVersion = 1,
            Id = request.ResourceId,
            DisplayName = request.DisplayName,
            Kind = ResourceKind.Animation,
            Source = new ResourceSource
            {
                Path = sourceRelativePath,
                Sha256 = ComputeSha256(stagedSource),
                ByteCount = new FileInfo(stagedSource).Length,
                MediaType = MediaType(stagedSource),
                Provenance = new ResourceProvenance
                {
                    Origin = request.Origin,
                    Author = request.Author,
                    SourceUri = request.SourceUri,
                    Notes = request.ProvenanceNotes,
                    License = new ResourceLicense
                    {
                        SpdxId = request.SpdxId,
                        LicenseTextPath = licenseRelativePath,
                        LicenseTextSha256 = stagedLicense is null ? null : ComputeSha256(stagedLicense),
                        Attribution = request.Attribution,
                        RedistributionApproved = request.RedistributionApproved
                    }
                }
            },
            Compliance = new ResourceCompliancePolicy
            {
                MissingProvenance = ResourcePolicyAction.Error,
                MissingLicense = ResourcePolicyAction.Error,
                UnapprovedRedistribution = ResourcePolicyAction.Error
            },
            Variants =
            [
                new ResourceVariant
                {
                    Id = formatName,
                    CompatiblePainterFormats = Enum.GetValues<SurfacePixelFormat>(),
                    Converter = new ResourceConverter
                    {
                        Id = metrics.ConverterId,
                        Version = metrics.ConverterVersion,
                        Options = ConverterOptions(request)
                    },
                    Output = new ResourceOutput
                    {
                        Path = outputRelativePath,
                        Sha256 = ComputeSha256(stagedOutput),
                        ByteCount = new FileInfo(stagedOutput).Length,
                        Format = request.Format,
                        PixelSize = new PixelSize
                        {
                            Width = metrics.OutputWidth,
                            Height = metrics.OutputHeight
                        },
                        FrameCount = metrics.FrameCount
                    },
                    Animation = new AnimationVariantMetadata
                    {
                        Tintable = metrics.Tintable,
                        SourcePixelSize = new PixelSize
                        {
                            Width = metrics.SourceWidth,
                            Height = metrics.SourceHeight
                        },
                        SourceGrid = new ResourceCropRectangle
                        {
                            X = metrics.SheetX,
                            Y = metrics.SheetY,
                            Width = checked(metrics.Columns * metrics.SourceFrameWidth),
                            Height = checked(rows * metrics.SourceFrameHeight)
                        },
                        SourceFramePixelSize = new PixelSize
                        {
                            Width = metrics.SourceFrameWidth,
                            Height = metrics.SourceFrameHeight
                        },
                        Columns = metrics.Columns,
                        ScaleMode = request.ScaleMode,
                        AlphaMode = request.AlphaMode,
                        AlphaThreshold = metrics.AlphaThreshold,
                        Dither = request.Dither,
                        TintArgb = metrics.TintArgb,
                        MatteArgb = metrics.MatteArgb,
                        PaletteArgb = metrics.PaletteArgb,
                        FrameIntervalMilliseconds = metrics.FrameIntervalMilliseconds,
                        RepeatIndex = metrics.RepeatIndex,
                        Autoplay = metrics.Autoplay,
                        StrideBytes = metrics.StrideBytes,
                        FramePixelBytes = metrics.FramePixelBytes,
                        PixelBytes = metrics.PixelBytes,
                        PaletteBytes = metrics.PaletteBytes,
                        EstimatedRomBytes = metrics.EstimatedRomBytes,
                        PixelChecksumFnv1a32 = metrics.PixelChecksumFnv1a32,
                        PaletteChecksumFnv1a32 = metrics.PaletteChecksumFnv1a32,
                        DataChecksumFnv1a32 = metrics.DataChecksumFnv1a32
                    },
                    Integration = new ResourceIntegration
                    {
                        Ownership = ResourceOwnership.Application,
                        Lifetime = ResourceLifetime.Static,
                        ProviderId = null,
                        Notes = $"Compile the generated source and map lithe::{metrics.EntryPoint}() frames to " +
                            "stable application-owned ResourceProvider bitmap names."
                    }
                }
            ]
        };
    }

    private static Dictionary<string, string> ConverterOptions(AnimationResourceImportRequest request)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alphaMode"] = request.AlphaMode.ToString(),
            ["alphaThreshold"] = request.AlphaThreshold.ToString(CultureInfo.InvariantCulture),
            ["autoplay"] = request.Autoplay ? "true" : "false",
            ["columns"] = request.Columns.ToString(CultureInfo.InvariantCulture),
            ["dither"] = request.Dither.ToString(),
            ["entryPoint"] = request.EntryPoint,
            ["format"] = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(request.Format),
            ["frameCount"] = request.FrameCount.ToString(CultureInfo.InvariantCulture),
            ["frameIntervalMilliseconds"] = request.FrameIntervalMilliseconds.ToString(
                CultureInfo.InvariantCulture),
            ["height"] = request.OutputHeight.ToString(CultureInfo.InvariantCulture),
            ["kind"] = "animation",
            ["matteArgb"] = request.MatteArgb,
            ["paletteArgb"] = request.PaletteArgb,
            ["repeatIndex"] = request.RepeatIndex.ToString(CultureInfo.InvariantCulture),
            ["scaleMode"] = request.ScaleMode.ToString(),
            ["sheetX"] = request.SheetX.ToString(CultureInfo.InvariantCulture),
            ["sheetY"] = request.SheetY.ToString(CultureInfo.InvariantCulture),
            ["sourceFrameHeight"] = request.SourceFrameHeight.ToString(CultureInfo.InvariantCulture),
            ["sourceFrameWidth"] = request.SourceFrameWidth.ToString(CultureInfo.InvariantCulture),
            ["symbol"] = request.GeneratedSymbol,
            ["tintArgb"] = request.TintArgb,
            ["tintable"] = request.Tintable ? "true" : "false",
            ["width"] = request.OutputWidth.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string MediaType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Resolve(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A unique, unreferenced staging directory is safer than broad cleanup.
        }
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private static AnimationResourceImportResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new AnimationResourceImportResult { Diagnostics = diagnostics };
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }
}
