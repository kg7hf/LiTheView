// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Security.Cryptography;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Resources;

/*
 * Font resources are prepared offline because an embedded LitheView application must not open a
 * TTF/OTF file, run an outline engine, allocate a glyph cache, or depend on a filesystem. The import
 * pipeline deliberately keeps each decision visible and reviewable:
 *
 *   source TTF/OTF + selected face/size/weight/repertoire
 *       -> convert-font.ps1
 *       -> immutable coverage bytes + fixed GeneratedGlyphRecord table in C++
 *       -> measured v2 conversion metrics
 *       -> canonical resource.json with hashes, provenance, and integration ownership
 *       -> one logical FormResource that names the generated C++ entry point
 *
 * RasterStyle selects the actual Regular or Bold outline exposed by the font file. StyleLabel and
 * Weight record the visual identity (for example, "Semibold" and 600) without pretending that GDI+
 * can synthesize an arbitrary OpenType weight. Repertoire is an explicit, canonical set such as
 * "U+0020-U+007E,U+00B0". U+003F is mandatory and becomes the bounded missing-glyph fallback.
 * Alpha1 packs eight thresholded coverage samples per byte; Alpha8 retains antialias coverage. The
 * glyph is still only a shape: the runtime draw call supplies color and PixelFormatTraits encodes it
 * for RGB565, RGB888, XRGB8888, or ARGB8888.
 *
 * Import is staged below the project root. Source, optional license, generated source, metrics, and
 * canonical manifest are validated before the resource directory is moved into place. Existing
 * directories are never merged or overwritten. The project manifest is the last durable write; if
 * it fails after the move, the unreferenced resource directory is removed. This makes interruption
 * fail closed without broad cleanup.
 *
 * The resource contract stops at logical application ownership and static lifetime. A consuming
 * application compiles the generated C++, exposes the entry point through its ResourceProvider, and
 * separately decides linker section, internal/external flash, QSPI, alignment, and target budgets.
 * Those deployment choices do not belong in a visual UI project and do not alter its identity.
 */
public sealed record FontResourceImportRequest
{
    public required string ProjectManifestPath { get; init; }

    public required string SourceFontPath { get; init; }

    public required string? SourceLicensePath { get; init; }

    public required string ResourceId { get; init; }

    public required string DisplayName { get; init; }

    public required string GeneratedSymbol { get; init; }

    public required string EntryPoint { get; init; }

    public required int PixelSize { get; init; }

    public required FontRasterStyle RasterStyle { get; init; }

    public required string StyleLabel { get; init; }

    public required int Weight { get; init; }

    public required ResourceFormat Coverage { get; init; }

    public required string Codepoints { get; init; }

    public required string Origin { get; init; }

    public required string? Author { get; init; }

    public required string? SourceUri { get; init; }

    public required string? ProvenanceNotes { get; init; }

    public required string? SpdxId { get; init; }

    public required string? Attribution { get; init; }

    public required bool RedistributionApproved { get; init; }
}

public sealed record FontResourceImportResult
{
    public string? ResourceManifestPath { get; init; }

    public string? ResourceManifestSha256 { get; init; }

    public string? GeneratedSourcePath { get; init; }

    public FontConversionMetrics? Metrics { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        ResourceManifestPath is not null &&
        ResourceManifestSha256 is not null &&
        GeneratedSourcePath is not null &&
        Metrics is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class FontResourceImporter
{
    private const long MaximumSourceFontBytes = 32L * 1024L * 1024L;
    private const string ResourceSchema = "https://litheview.dev/schema/resource-manifest.schema.json";

    public static async Task<FontResourceImportResult> ImportAsync(
        FontResourceImportRequest request,
        IFontConversionRunner runner,
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
                "LVFI1001",
                "$.resourceId",
                $"Resource ID '{request.ResourceId}' already exists in the project."));
            return Failed(diagnostics);
        }

        string stem = request.ResourceId.Replace('.', '-');
        string resourceRelativeDirectory = $"resources/{stem}";
        string resourceManifestRelativePath = $"{resourceRelativeDirectory}/resource.json";
        string sourceExtension = Path.GetExtension(request.SourceFontPath).ToLowerInvariant();
        string sourceRelativePath = $"{resourceRelativeDirectory}/source/font{sourceExtension}";
        string licenseRelativePath = $"{resourceRelativeDirectory}/license/license.txt";
        string coverageName = request.Coverage == ResourceFormat.Alpha1 ? "alpha1" : "alpha8";
        string outputRelativePath = $"{resourceRelativeDirectory}/generated/font-{coverageName}.cpp";
        string stagingRoot = Path.Combine(projectRoot, $".{stem}.{Guid.NewGuid():N}.font-import");
        string stagingResourceDirectory = Resolve(stagingRoot, resourceRelativeDirectory);
        string destinationResourceDirectory = Resolve(projectRoot, resourceRelativeDirectory);
        if (Directory.Exists(destinationResourceDirectory) || File.Exists(destinationResourceDirectory))
        {
            diagnostics.Add(Error(
                "LVFI1002",
                resourceRelativeDirectory,
                "The deterministic resource directory already exists; import never merges or overwrites it."));
            return Failed(diagnostics);
        }

        bool resourceMoved = false;
        try
        {
            string stagedSource = Resolve(stagingRoot, sourceRelativePath);
            string stagedOutput = Resolve(stagingRoot, outputRelativePath);
            string stagedMetrics = Resolve(stagingRoot, $"{resourceRelativeDirectory}/generated/font.metrics.json");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedSource)!);
            File.Copy(request.SourceFontPath, stagedSource, overwrite: false);
            string? stagedLicense = null;
            if (request.SourceLicensePath is not null)
            {
                stagedLicense = Resolve(stagingRoot, licenseRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedLicense)!);
                File.Copy(request.SourceLicensePath, stagedLicense, overwrite: false);
            }

            FontRepertoire repertoire = FontRepertoireParser.TryParse(
                request.Codepoints,
                out FontRepertoire? parsed,
                out _)
                ? parsed!
                : throw new InvalidOperationException("Validated font repertoire did not parse.");
            FontConversionRequest conversionRequest = new()
            {
                InputFontPath = stagedSource,
                OutputSourcePath = stagedOutput,
                OutputMetricsPath = stagedMetrics,
                Symbol = request.GeneratedSymbol,
                EntryPoint = request.EntryPoint,
                PixelSize = request.PixelSize,
                RasterStyle = request.RasterStyle,
                StyleLabel = request.StyleLabel,
                Weight = request.Weight,
                Coverage = request.Coverage,
                Codepoints = repertoire.Canonical
            };
            FontConversionResult conversion = await runner.ConvertAsync(
                conversionRequest,
                cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(conversion.Diagnostics);
            if (!conversion.IsValid || conversion.Metrics is null || !File.Exists(stagedOutput))
            {
                return Failed(diagnostics);
            }
            FontConversionMetrics metrics = conversion.Metrics;
            ValidateConversion(request, repertoire, stagedSource, metrics, diagnostics);
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
                coverageName);
            ResourceManifestLoadResult loadedManifest = ResourceManifestLoader.Load(
                ResourceManifestCanonicalizer.Canonicalize(manifest));
            diagnostics.AddRange(loadedManifest.Diagnostics);
            if (!loadedManifest.IsValid ||
                loadedManifest.Manifest is null ||
                loadedManifest.CanonicalUtf8 is null ||
                loadedManifest.Sha256 is null)
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
            return new FontResourceImportResult
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
            diagnostics.Add(Error("LVFI1003", "$", $"Font import failed: {exception.Message}"));
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

    private static List<ProfileDiagnostic> ValidateRequest(FontResourceImportRequest request)
    {
        List<ProfileDiagnostic> diagnostics = [];
        FontConversionRequest conversion = new()
        {
            InputFontPath = request.SourceFontPath,
            OutputSourcePath = "generated.cpp",
            OutputMetricsPath = "metrics.json",
            Symbol = request.GeneratedSymbol,
            EntryPoint = request.EntryPoint,
            PixelSize = request.PixelSize,
            RasterStyle = request.RasterStyle,
            StyleLabel = request.StyleLabel,
            Weight = request.Weight,
            Coverage = request.Coverage,
            Codepoints = request.Codepoints
        };
        diagnostics.AddRange(FontConversionRequestValidator.Validate(conversion));
        if (string.IsNullOrWhiteSpace(request.ProjectManifestPath) ||
            !File.Exists(request.ProjectManifestPath))
        {
            diagnostics.Add(Error("LVFI1101", "$.projectManifestPath", "A readable designer project is required."));
        }
        if (!ProfileRules.IsValidIdentifier(request.ResourceId) ||
            string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 160)
        {
            diagnostics.Add(Error(
                "LVFI1102",
                "$.resource",
                "Resource ID and display name must follow the bounded resource-manifest contract."));
        }
        if (!File.Exists(request.SourceFontPath) ||
            new FileInfo(request.SourceFontPath).Length is <= 0 or > MaximumSourceFontBytes ||
            Path.GetExtension(request.SourceFontPath).ToLowerInvariant() is not (".ttf" or ".otf"))
        {
            diagnostics.Add(Error(
                "LVFI1103",
                "$.sourceFontPath",
                "The source must be a nonempty TTF or OTF file no larger than 32 MiB."));
        }
        if (request.SourceLicensePath is not null && !File.Exists(request.SourceLicensePath))
        {
            diagnostics.Add(Error("LVFI1104", "$.sourceLicensePath", "The selected license file does not exist."));
        }
        if (string.IsNullOrWhiteSpace(request.Origin) || request.Origin.Length > 1024 ||
            request.SpdxId is null && request.SourceLicensePath is null ||
            !request.RedistributionApproved)
        {
            diagnostics.Add(Error(
                "LVFI1105",
                "$.provenance",
                "Import requires origin, an SPDX ID or license file, and explicit redistribution approval."));
        }
        if (request.SourceUri is not null &&
            !Uri.TryCreate(request.SourceUri, UriKind.Absolute, out _))
        {
            diagnostics.Add(Error("LVFI1106", "$.sourceUri", "The optional source URI must be absolute."));
        }
        return diagnostics;
    }

    private static void ValidateConversion(
        FontResourceImportRequest request,
        FontRepertoire repertoire,
        string stagedSource,
        FontConversionMetrics metrics,
        List<ProfileDiagnostic> diagnostics)
    {
        string sourceSha256 = ComputeSha256(stagedSource);
        string expectedCoverage = request.Coverage == ResourceFormat.Alpha1 ? "alpha1" : "alpha8";
        if (metrics.SourceSha256 != sourceSha256 ||
            metrics.Style != request.StyleLabel ||
            metrics.RasterStyle != request.RasterStyle.ToString() ||
            metrics.Weight != request.Weight ||
            metrics.PixelSize != request.PixelSize ||
            metrics.Coverage != expectedCoverage ||
            metrics.Repertoire != repertoire.Canonical ||
            metrics.Symbol != request.GeneratedSymbol ||
            metrics.EntryPoint != request.EntryPoint)
        {
            diagnostics.Add(Error(
                "LVFI1201",
                "$.converter",
                "Converter output does not exactly match the selected source, face, size, repertoire, or coverage."));
        }
    }

    private static ResourceManifest CreateManifest(
        FontResourceImportRequest request,
        FontConversionMetrics metrics,
        string stagedSource,
        string? stagedLicense,
        string stagedOutput,
        string sourceRelativePath,
        string? licenseRelativePath,
        string outputRelativePath,
        string coverageName)
    {
        ResourceFormat coverage = coverageName == "alpha1" ? ResourceFormat.Alpha1 : ResourceFormat.Alpha8;
        return new ResourceManifest
        {
            Schema = ResourceSchema,
            SchemaVersion = 1,
            Id = request.ResourceId,
            DisplayName = request.DisplayName,
            Kind = ResourceKind.Font,
            Source = new ResourceSource
            {
                Path = sourceRelativePath,
                Sha256 = ComputeSha256(stagedSource),
                ByteCount = new FileInfo(stagedSource).Length,
                MediaType = Path.GetExtension(stagedSource).Equals(".otf", StringComparison.OrdinalIgnoreCase)
                    ? "font/otf"
                    : "font/ttf",
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
                    Id = coverageName,
                    CompatiblePainterFormats = Enum.GetValues<SurfacePixelFormat>(),
                    Converter = new ResourceConverter
                    {
                        Id = metrics.ConverterId,
                        Version = metrics.ConverterVersion,
                        Options = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["codepoints"] = metrics.Repertoire,
                            ["coverage"] = metrics.Coverage,
                            ["entryPoint"] = metrics.EntryPoint,
                            ["pixelSize"] = metrics.PixelSize.ToString(CultureInfo.InvariantCulture),
                            ["rasterStyle"] = metrics.RasterStyle,
                            ["style"] = metrics.Style,
                            ["symbol"] = metrics.Symbol,
                            ["weight"] = metrics.Weight.ToString(CultureInfo.InvariantCulture)
                        }
                    },
                    Output = new ResourceOutput
                    {
                        Path = outputRelativePath,
                        Sha256 = ComputeSha256(stagedOutput),
                        ByteCount = new FileInfo(stagedOutput).Length,
                        Format = coverage,
                        PixelSize = null,
                        FrameCount = 1
                    },
                    Font = new FontVariantMetadata
                    {
                        Family = metrics.Family,
                        Style = metrics.Style,
                        Weight = metrics.Weight,
                        PixelSize = metrics.PixelSize,
                        Coverage = coverage,
                        Repertoire = metrics.Repertoire,
                        Fallback = metrics.Fallback,
                        GlyphCount = metrics.GlyphCount,
                        CoverageBytes = metrics.CoverageBytes,
                        RecordBytes = metrics.RecordBytes,
                        EstimatedRomBytes = metrics.EstimatedRomBytes,
                        DataChecksumFnv1a32 = metrics.DataChecksumFnv1a32,
                        LineHeight = metrics.LineHeight,
                        Baseline = metrics.Baseline,
                        UnderlinePosition = metrics.UnderlinePosition,
                        UnderlineThickness = metrics.UnderlineThickness
                    },
                    Integration = new ResourceIntegration
                    {
                        Ownership = ResourceOwnership.Application,
                        Lifetime = ResourceLifetime.Static,
                        ProviderId = null,
                        Notes = $"Compile the generated source and expose lithe::{metrics.EntryPoint}() through the " +
                            "application-owned ResourceProvider font lookup."
                    }
                }
            ]
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
            // A stale hidden staging directory or unreferenced resource directory is safer than
            // broad cleanup. A later import will use a distinct GUID and never consume it.
        }
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private static FontResourceImportResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new FontResourceImportResult { Diagnostics = diagnostics };
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }
}
