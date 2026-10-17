// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Security.Cryptography;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Resources;

/*
 * Image and icon resources follow the same desktop-build/embedded-borrow boundary as fonts:
 *
 *   reviewed PNG/BMP/JPEG + crop/scale/alpha/palette choices
 *       -> convert-image.ps1
 *       -> immutable Bitmap pixel bytes and optional indexed4 Color palette in C++
 *       -> strict conversion metrics with exact hashes, checksums, and payload measurements
 *       -> canonical resource.json with provenance, license, ownership, and lifetime
 *       -> one logical FormResource naming the generated const Bitmap entry point
 *
 * The conversion controls describe representation, not hardware. Alpha1 and Alpha8 are reusable
 * masks: their shape is stored once and Bitmap::tint can supply a different normal/focused/pressed
 * color at draw time. Indexed4 keeps two explicit palette indexes per byte; entry zero is transparent
 * only if the selected palette says so. Direct-color variants use the byte order already consumed by
 * Painter. All variants can therefore be drawn by every supported painter surface format.
 *
 * Crop is expressed in immutable source pixels. Scaling occurs offline and records nearest-neighbor
 * or bilinear sampling. Non-alpha formats composite source transparency onto the selected matte;
 * ARGB8888 and indexed palettes can retain alpha. Ordered 4-by-4 dithering is limited to the two
 * representations where it is useful: indexed4 and RGB565. Generated code contains a detailed
 * integration comment and requires no decoder, filesystem, heap, or startup conversion.
 *
 * Import is transactional. Source, optional license, generated C++, measurements, and canonical
 * manifest are staged and verified before a new resource directory is moved into the project. The
 * project manifest is written last. Existing resource directories are never merged or overwritten.
 * Linker section, internal/external flash, QSPI, alignment, target budgets, and provider wiring remain
 * choices for the consuming application and cannot alter the visual project identity.
 */
public sealed record ImageResourceImportRequest
{
    public required string ProjectManifestPath { get; init; }

    public required string SourceImagePath { get; init; }

    public required string? SourceLicensePath { get; init; }

    public required string ResourceId { get; init; }

    public required string DisplayName { get; init; }

    public required string GeneratedSymbol { get; init; }

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

    public required string Origin { get; init; }

    public required string? Author { get; init; }

    public required string? SourceUri { get; init; }

    public required string? ProvenanceNotes { get; init; }

    public required string? SpdxId { get; init; }

    public required string? Attribution { get; init; }

    public required bool RedistributionApproved { get; init; }
}

public sealed record ImageResourceImportResult
{
    public string? ResourceManifestPath { get; init; }

    public string? ResourceManifestSha256 { get; init; }

    public string? GeneratedSourcePath { get; init; }

    public ImageConversionMetrics? Metrics { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => ResourceManifestPath is not null &&
        ResourceManifestSha256 is not null &&
        GeneratedSourcePath is not null &&
        Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class ImageResourceImporter
{
    private const long MaximumSourceImageBytes = 64L * 1024L * 1024L;
    private const string ResourceSchema = "https://litheview.dev/schema/resource-manifest.schema.json";

    public static async Task<ImageResourceImportResult> ImportAsync(
        ImageResourceImportRequest request,
        IImageConversionRunner runner,
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
                "LVII1001",
                "$.resourceId",
                $"Resource ID '{request.ResourceId}' already exists in the project."));
            return Failed(diagnostics);
        }

        string stem = request.ResourceId.Replace('.', '-');
        string resourceRelativeDirectory = $"resources/{stem}";
        string resourceManifestRelativePath = $"{resourceRelativeDirectory}/resource.json";
        string sourceExtension = Path.GetExtension(request.SourceImagePath).ToLowerInvariant();
        string sourceRelativePath = $"{resourceRelativeDirectory}/source/{request.Kind.ToString().ToLowerInvariant()}" +
            sourceExtension;
        string licenseRelativePath = $"{resourceRelativeDirectory}/license/license.txt";
        string formatName = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(request.Format);
        string outputRelativePath = $"{resourceRelativeDirectory}/generated/" +
            $"{request.Kind.ToString().ToLowerInvariant()}-{formatName}.cpp";
        string stagingRoot = Path.Combine(projectRoot, $".{stem}.{Guid.NewGuid():N}.image-import");
        string stagingResourceDirectory = Resolve(stagingRoot, resourceRelativeDirectory);
        string destinationResourceDirectory = Resolve(projectRoot, resourceRelativeDirectory);
        if (Directory.Exists(destinationResourceDirectory) || File.Exists(destinationResourceDirectory))
        {
            diagnostics.Add(Error(
                "LVII1002",
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
                $"{resourceRelativeDirectory}/generated/image.metrics.json");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedSource)!);
            File.Copy(request.SourceImagePath, stagedSource, overwrite: false);
            string? stagedLicense = null;
            if (request.SourceLicensePath is not null)
            {
                stagedLicense = Resolve(stagingRoot, licenseRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedLicense)!);
                File.Copy(request.SourceLicensePath, stagedLicense, overwrite: false);
            }

            ImageConversionRequest conversionRequest = ToConversionRequest(
                request,
                stagedSource,
                stagedOutput,
                stagedMetrics);
            ImageConversionResult conversion = await runner.ConvertAsync(
                conversionRequest,
                cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(conversion.Diagnostics);
            if (!conversion.IsValid || conversion.Metrics is null || !File.Exists(stagedOutput))
            {
                return Failed(diagnostics);
            }
            ImageConversionMetrics metrics = conversion.Metrics;
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
            return new ImageResourceImportResult
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
            diagnostics.Add(Error("LVII1003", "$", $"Image import failed: {exception.Message}"));
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

    private static List<ProfileDiagnostic> ValidateRequest(ImageResourceImportRequest request)
    {
        List<ProfileDiagnostic> diagnostics = [];
        diagnostics.AddRange(ImageConversionRequestValidator.Validate(ToConversionRequest(
            request,
            request.SourceImagePath,
            "generated.cpp",
            "metrics.json")));
        if (string.IsNullOrWhiteSpace(request.ProjectManifestPath) ||
            !File.Exists(request.ProjectManifestPath))
        {
            diagnostics.Add(Error("LVII1101", "$.projectManifestPath", "A readable designer project is required."));
        }
        if (!ProfileRules.IsValidIdentifier(request.ResourceId) ||
            string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 160)
        {
            diagnostics.Add(Error(
                "LVII1102",
                "$.resource",
                "Resource ID and display name must follow the bounded resource-manifest contract."));
        }
        if (!File.Exists(request.SourceImagePath) ||
            new FileInfo(request.SourceImagePath).Length is <= 0 or > MaximumSourceImageBytes ||
            Path.GetExtension(request.SourceImagePath).ToLowerInvariant() is not
                (".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif" or ".tif" or ".tiff"))
        {
            diagnostics.Add(Error(
                "LVII1103",
                "$.sourceImagePath",
                "The source must be a nonempty PNG, BMP, JPEG, GIF, or TIFF no larger than 64 MiB."));
        }
        if (request.SourceLicensePath is not null && !File.Exists(request.SourceLicensePath))
        {
            diagnostics.Add(Error("LVII1104", "$.sourceLicensePath", "The selected license file does not exist."));
        }
        if (string.IsNullOrWhiteSpace(request.Origin) || request.Origin.Length > 1024 ||
            request.SpdxId is null && request.SourceLicensePath is null ||
            !request.RedistributionApproved)
        {
            diagnostics.Add(Error(
                "LVII1105",
                "$.provenance",
                "Import requires origin, an SPDX ID or license file, and explicit redistribution approval."));
        }
        if (request.SourceUri is not null && !Uri.TryCreate(request.SourceUri, UriKind.Absolute, out _))
        {
            diagnostics.Add(Error("LVII1106", "$.sourceUri", "The optional source URI must be absolute."));
        }
        return diagnostics;
    }

    private static ImageConversionRequest ToConversionRequest(
        ImageResourceImportRequest request,
        string input,
        string output,
        string metrics)
    {
        return new ImageConversionRequest
        {
            InputImagePath = input,
            OutputSourcePath = output,
            OutputMetricsPath = metrics,
            Symbol = request.GeneratedSymbol,
            EntryPoint = request.EntryPoint,
            Kind = request.Kind,
            Tintable = request.Tintable,
            Format = request.Format,
            CropX = request.CropX,
            CropY = request.CropY,
            CropWidth = request.CropWidth,
            CropHeight = request.CropHeight,
            OutputWidth = request.OutputWidth,
            OutputHeight = request.OutputHeight,
            ScaleMode = request.ScaleMode,
            AlphaMode = request.AlphaMode,
            AlphaThreshold = request.AlphaThreshold,
            Dither = request.Dither,
            TintArgb = request.TintArgb,
            MatteArgb = request.MatteArgb,
            PaletteArgb = request.PaletteArgb
        };
    }

    private static void ValidateConversion(
        ImageResourceImportRequest request,
        string stagedSource,
        ImageConversionMetrics metrics,
        List<ProfileDiagnostic> diagnostics)
    {
        string expectedKind = StrictCamelCaseEnumConverter<ResourceKind>.ToJsonName(request.Kind);
        string expectedFormat = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(request.Format);
        if (metrics.SourceSha256 != ComputeSha256(stagedSource) ||
            metrics.Kind != expectedKind || metrics.Tintable != request.Tintable ||
            metrics.Format != expectedFormat || metrics.CropX != request.CropX ||
            metrics.CropY != request.CropY || metrics.CropWidth != request.CropWidth ||
            metrics.CropHeight != request.CropHeight || metrics.OutputWidth != request.OutputWidth ||
            metrics.OutputHeight != request.OutputHeight || metrics.ScaleMode != request.ScaleMode.ToString() ||
            metrics.AlphaMode != request.AlphaMode.ToString() ||
            metrics.AlphaThreshold != request.AlphaThreshold || metrics.Dither != request.Dither.ToString() ||
            metrics.TintArgb != request.TintArgb || metrics.MatteArgb != request.MatteArgb ||
            metrics.PaletteArgb != request.PaletteArgb || metrics.Symbol != request.GeneratedSymbol ||
            metrics.EntryPoint != request.EntryPoint)
        {
            diagnostics.Add(Error(
                "LVII1201",
                "$.converter",
                "Converter output does not exactly match the selected source, crop, format, colors, or integration symbols."));
        }
    }

    private static ResourceManifest CreateManifest(
        ImageResourceImportRequest request,
        ImageConversionMetrics metrics,
        string stagedSource,
        string? stagedLicense,
        string stagedOutput,
        string sourceRelativePath,
        string? licenseRelativePath,
        string outputRelativePath,
        string formatName)
    {
        return new ResourceManifest
        {
            Schema = ResourceSchema,
            SchemaVersion = 1,
            Id = request.ResourceId,
            DisplayName = request.DisplayName,
            Kind = request.Kind,
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
                        FrameCount = 1
                    },
                    Image = new ImageVariantMetadata
                    {
                        Tintable = metrics.Tintable,
                        SourcePixelSize = new PixelSize
                        {
                            Width = metrics.SourceWidth,
                            Height = metrics.SourceHeight
                        },
                        Crop = new ResourceCropRectangle
                        {
                            X = metrics.CropX,
                            Y = metrics.CropY,
                            Width = metrics.CropWidth,
                            Height = metrics.CropHeight
                        },
                        ScaleMode = request.ScaleMode,
                        AlphaMode = request.AlphaMode,
                        AlphaThreshold = metrics.AlphaThreshold,
                        Dither = request.Dither,
                        TintArgb = metrics.TintArgb,
                        MatteArgb = metrics.MatteArgb,
                        PaletteArgb = metrics.PaletteArgb,
                        StrideBytes = metrics.StrideBytes,
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
                        Notes = $"Compile the generated source and expose lithe::{metrics.EntryPoint}() through the " +
                            "application-owned ResourceProvider bitmap lookup."
                    }
                }
            ]
        };
    }

    private static Dictionary<string, string> ConverterOptions(ImageResourceImportRequest request)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alphaMode"] = request.AlphaMode.ToString(),
            ["alphaThreshold"] = request.AlphaThreshold.ToString(CultureInfo.InvariantCulture),
            ["cropHeight"] = request.CropHeight.ToString(CultureInfo.InvariantCulture),
            ["cropWidth"] = request.CropWidth.ToString(CultureInfo.InvariantCulture),
            ["cropX"] = request.CropX.ToString(CultureInfo.InvariantCulture),
            ["cropY"] = request.CropY.ToString(CultureInfo.InvariantCulture),
            ["dither"] = request.Dither.ToString(),
            ["entryPoint"] = request.EntryPoint,
            ["format"] = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(request.Format),
            ["height"] = request.OutputHeight.ToString(CultureInfo.InvariantCulture),
            ["kind"] = StrictCamelCaseEnumConverter<ResourceKind>.ToJsonName(request.Kind),
            ["matteArgb"] = request.MatteArgb,
            ["paletteArgb"] = request.PaletteArgb,
            ["scaleMode"] = request.ScaleMode.ToString(),
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
            // An unreferenced staging directory is safer than broad cleanup. A later import uses a
            // distinct GUID and never consumes it.
        }
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private static ImageResourceImportResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new ImageResourceImportResult { Diagnostics = diagnostics };
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }
}
