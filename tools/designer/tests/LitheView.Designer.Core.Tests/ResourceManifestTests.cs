// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.Core.Tests;

internal static class ResourceManifestTests
{
    private const string ExpectedManifestSha256 =
        "86d0b7df58111786ef3bad1d3e46514d4c7ba00a8e587a1d715fd1f2e2184a93";

    public static void CheckedManifestValidatesAndVerifies(string manifestPath, string projectRoot)
    {
        ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(manifestPath);
        Assert(loaded.IsValid, "The checked resource manifest must validate.");
        ResourceManifest manifest = RequireManifest(loaded);
        Assert(loaded.Sha256 == ExpectedManifestSha256, "The checked resource identity changed.");

        ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
            manifest,
            projectRoot);
        Assert(verification.IsValid, "The checked source, license, or generated output lock changed.");
        Assert(verification.Artifacts.Count == 3, "The checked resource artifact count changed.");
    }

    public static void CanonicalizationIsOrderIndependent(string manifestPath)
    {
        ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(manifestPath);
        Assert(loaded.IsValid, "The checked resource manifest is invalid.");
        ResourceManifest manifest = RequireManifest(loaded);

        ResourceManifest reordered = manifest with
        {
            Variants = [.. manifest.Variants.Reverse().Select(variant =>
            {
                Dictionary<string, string> reverseOptions = variant.Converter.Options
                    .Reverse()
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
                return variant with
                {
                    Converter = variant.Converter with { Options = reverseOptions },
                    CompatiblePainterFormats = [.. variant.CompatiblePainterFormats.Reverse()]
                };
            })]
        };

        byte[] canonical = ResourceManifestCanonicalizer.Canonicalize(reordered);
        Assert(
            loaded.CanonicalUtf8!.AsSpan().SequenceEqual(canonical),
            "Dictionary or painter-format order changed canonical resource bytes.");
        ResourceManifestLoadResult fixedPoint = ResourceManifestLoader.Load(canonical);
        Assert(fixedPoint.IsValid, "Canonical resource bytes did not reload.");
        Assert(fixedPoint.Sha256 == ExpectedManifestSha256, "Canonical resource identity is not stable.");
    }

    public static void CompliancePolicyCanFailProductValidation(string manifestPath)
    {
        ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(manifestPath);
        ResourceManifest manifest = RequireManifest(loaded);
        ResourceManifest missing = manifest with
        {
            Source = manifest.Source with { Provenance = null }
        };
        IReadOnlyList<ProfileDiagnostic> blocking = ResourceManifestValidator.Validate(missing);
        Assert(
            blocking.Any(item => item.Code == "LVRM1205" && item.Severity == ProfileDiagnosticSeverity.Error),
            "Missing provenance did not honor the product-build error policy.");
        Assert(
            blocking.Any(item => item.Code == "LVRM1206" && item.Severity == ProfileDiagnosticSeverity.Error),
            "Missing license data did not honor the product-build error policy.");

        ResourceManifest warningOnly = missing with
        {
            Compliance = missing.Compliance with
            {
                MissingProvenance = ResourcePolicyAction.Warning,
                MissingLicense = ResourcePolicyAction.Warning
            }
        };
        IReadOnlyList<ProfileDiagnostic> warnings = ResourceManifestValidator.Validate(warningOnly);
        Assert(
            warnings.All(item => item.Severity == ProfileDiagnosticSeverity.Warning),
            "Warning-only provenance policy produced a blocking diagnostic.");
    }

    public static void ArtifactDriftFailsClosed(string manifestPath, string projectRoot)
    {
        ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(manifestPath);
        ResourceManifest manifest = RequireManifest(loaded);
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "LitheView.ResourceManifestTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            CopyArtifact(projectRoot, manifest.Source.Path, temporaryRoot);
            foreach (ResourceVariant variant in manifest.Variants)
            {
                CopyArtifact(projectRoot, variant.Output.Path, temporaryRoot);
            }
            ResourceLicense? license = manifest.Source.Provenance?.License;
            if (license?.LicenseTextPath is not null)
            {
                CopyArtifact(projectRoot, license.LicenseTextPath, temporaryRoot);
            }

            string output = Resolve(temporaryRoot, manifest.Variants[0].Output.Path);
            File.AppendAllText(output, "drift", Encoding.UTF8);
            ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
                manifest,
                temporaryRoot);
            Assert(!verification.IsValid, "A changed generated artifact retained a valid lock.");
            Assert(
                verification.Diagnostics.Any(item => item.Code is "LVRM2002" or "LVRM2003"),
                "Changed artifact did not report its size or hash mismatch.");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    public static void DuplicatePropertiesAndUnsafePathsFail(string manifestPath)
    {
        string json = File.ReadAllText(manifestPath);
        string duplicate = json.Replace(
            "  \"displayName\":",
            "  \"id\": \"litheview.resource.duplicate\",\n  \"displayName\":",
            StringComparison.Ordinal);
        ResourceManifestLoadResult duplicateResult = ResourceManifestLoader.Load(
            Encoding.UTF8.GetBytes(duplicate));
        Assert(!duplicateResult.IsValid, "A duplicate resource property was accepted.");
        Assert(
            duplicateResult.Diagnostics.Any(item => item.Code == "LVRM0005"),
            "A duplicate resource property did not report LVRM0005.");

        ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(manifestPath);
        ResourceManifest manifest = RequireManifest(loaded);
        ResourceManifest unsafeManifest = manifest with
        {
            Source = manifest.Source with { Path = "../outside.svg" }
        };
        IReadOnlyList<ProfileDiagnostic> diagnostics = ResourceManifestValidator.Validate(unsafeManifest);
        Assert(
            diagnostics.Any(item => item.Code == "LVRM1201"),
            "A resource source path escaped the project root.");
    }

    public static void VariantOutputAndOwnershipFailClosed(string manifestPath)
    {
        ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(manifestPath);
        ResourceManifest manifest = RequireManifest(loaded);
        ResourceVariant source = manifest.Variants[0];
        ResourceVariant invalid = source with
        {
            Output = source.Output with { FrameCount = 2 },
            Integration = source.Integration with
            {
                Ownership = ResourceOwnership.Provider,
                Lifetime = ResourceLifetime.Static,
                ProviderId = null
            }
        };
        ResourceManifest invalidManifest = manifest with { Variants = [invalid] };
        IReadOnlyList<ProfileDiagnostic> diagnostics = ResourceManifestValidator.Validate(invalidManifest);
        Assert(
            diagnostics.Any(item => item.Code == "LVRM1505"),
            "A non-animation resource accepted multiple frames.");
        Assert(
            diagnostics.Any(item => item.Code is "LVRM1602" or "LVRM1603"),
            "Invalid provider ownership and lifetime were accepted.");
    }

    public static void ProjectResolutionIncludesLockedResources(
        string projectPath,
        string surfaceDirectory,
        string projectRoot)
    {
        DesignerProjectLoadResult project = DesignerProjectLoader.LoadFile(projectPath);
        DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
        DesignerProjectResolutionResult withoutRoot = DesignerProjectResolver.Resolve(
            project,
            catalog.Surfaces);
        Assert(!withoutRoot.IsValid, "A resource-bearing project resolved without a contained root.");
        Assert(
            withoutRoot.Diagnostics.Any(item => item.Code == "LVRP1601"),
            "Missing project-root resource resolution did not report LVRP1601.");

        DesignerProjectResolutionResult resolved = DesignerProjectResolver.Resolve(
            project,
            catalog.Surfaces,
            projectRoot);
        Assert(resolved.IsValid && resolved.Project is not null, "The checked resource project did not resolve.");
        ResolvedDesignerProject checkedProject = resolved.Project ??
            throw new InvalidOperationException("The checked resource project did not resolve.");
        Assert(checkedProject.Resources.Count == 1, "The resolved resource count changed.");
        Assert(checkedProject.ResourceVariants.Count == 1, "The selected resource-variant count changed.");
        Assert(
            checkedProject.ResourceVariants[0].Variant.Output.ByteCount == 8,
            "The resolved resource payload measurement changed.");
        Assert(
            checkedProject.ResolvedSha256 ==
                "f0ef81eb5a6a024b49ba40a38e149fd3261ffd4847ff9aef7bf60357951277dd",
            "The resolved project no longer includes the locked resource identity.");
    }

    public static void SurfaceCompatibilityFailsClosed(
        string projectPath,
        string projectRoot,
        string surfacePath)
    {
        DesignerProjectManifest project = DesignerProjectLoader.LoadFile(projectPath).Manifest ??
            throw new InvalidOperationException("The resource smoke project did not load.");
        ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(project, projectRoot);
        Assert(resources.IsValid, "The checked project resource set is invalid.");
        DisplaySurfaceLoadResult loadedSurface = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
        DisplaySurfaceProfile surface = loadedSurface.Profile ??
            throw new InvalidOperationException("The checked display surface did not load.");
        ResourceSurfaceResolutionResult checkedResult = ResourceSurfaceResolver.Resolve(resources, surface);
        Assert(checkedResult.IsValid, "The checked display-compatible resource variant is invalid.");

        LoadedProjectResource sourceResource = resources.Resources[0];
        ResourceVariant sourceVariant = sourceResource.Manifest.Variants[0];
        ResourceVariant incompatibleVariant = sourceVariant with
        {
            CompatiblePainterFormats = [SurfacePixelFormat.Xrgb8888]
        };
        ResourceSurfaceResolutionResult missingVariant = ResourceSurfaceResolver.Resolve(
            ReplaceVariant(resources, sourceResource, incompatibleVariant),
            surface);
        Assert(!missingVariant.IsValid, "A resource without a surface-compatible variant was accepted.");
        Assert(
            missingVariant.Diagnostics.Any(item => item.Code == "LVRM3101"),
            "A resource without a compatible painter variant did not report LVRM3101.");

        LoadedProjectResource duplicated = sourceResource with
        {
            Manifest = sourceResource.Manifest with
            {
                Variants = [sourceVariant, sourceVariant with { Id = "alpha1-duplicate" }]
            }
        };
        ResourceSurfaceResolutionResult ambiguous = ResourceSurfaceResolver.Resolve(
            resources with { Resources = [duplicated] },
            surface);
        Assert(
            ambiguous.Diagnostics.Any(item => item.Code == "LVRM3101"),
            "Multiple compatible painter variants did not fail closed.");
    }

    public static void FontRepertoireIsCanonicalAndBounded()
    {
        bool parsed = FontRepertoireParser.TryParse(
            "U+0043,U+003F,U+0041-U+0042,U+0040",
            out FontRepertoire? repertoire,
            out string? error);
        Assert(parsed && error is null, "A valid unordered repertoire did not parse.");
        Assert(
            repertoire?.Canonical == "U+003F-U+0043" && repertoire.Codepoints.Length == 5,
            "Font repertoire canonicalization is not deterministic.");
        Assert(
            !FontRepertoireParser.TryParse("U+0041-U+005A", out _, out _),
            "A repertoire without U+003F fallback was accepted.");
        Assert(
            !FontRepertoireParser.TryParse("U+003F,U+D7FF-U+E000", out _, out _),
            "A repertoire crossing the Unicode surrogate range was accepted.");
    }

    public static void CheckedFontMetricsUseStrictContract(string repositoryRoot)
    {
        string fontDirectory = Path.Combine(repositoryRoot, "resources", "fonts");
        FontConversionMetricsLoadResult regular = FontConversionMetricsLoader.LoadFile(
            Path.Combine(fontDirectory, "generated_sans_regular.json"));
        FontConversionMetricsLoadResult bold = FontConversionMetricsLoader.LoadFile(
            Path.Combine(fontDirectory, "generated_sans_bold.json"));
        Assert(regular.IsValid && bold.IsValid, "Checked font metrics do not use the strict v2 contract.");
        Assert(
            regular.Metrics is
            {
                ConverterId: "litheview.converter.font.gdiplus",
                ConverterVersion: "2.0.0",
                RasterStyle: "Regular",
                Weight: 400,
                EstimatedRomBytes: 9201
            },
            "Checked regular font identity or payload changed.");
        Assert(
            bold.Metrics is
            {
                RasterStyle: "Bold",
                Weight: 700,
                EstimatedRomBytes: 10318
            },
            "Checked bold font identity or payload changed.");

        byte[] unknownProperty = Encoding.UTF8.GetBytes(
            File.ReadAllText(Path.Combine(fontDirectory, "generated_sans_regular.json"), Encoding.UTF8)
                .Replace("\"schema_version\": 2,", "\"schema_version\": 2,\n  \"unknown\": true,",
                    StringComparison.Ordinal));
        FontConversionMetricsLoadResult rejected = FontConversionMetricsLoader.Load(unknownProperty);
        Assert(!rejected.IsValid, "Unknown font metrics properties did not fail closed.");
    }

    public static void FontResourceImportIsDeterministicAndTransactional(string checkedProjectPath, string repositoryRoot)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "LitheView.FontResourceImportTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        string inputFont = Path.Combine(temporaryRoot, "input.ttf");
        string license = Path.Combine(temporaryRoot, "LICENSE.txt");
        File.WriteAllBytes(inputFont, "deterministic fake outline"u8.ToArray());
        File.WriteAllText(license, "Test-only font license\n", Encoding.UTF8);
        try
        {
            string firstProject = CreateProjectCopy(temporaryRoot, "first", checkedProjectPath);
            string secondProject = CreateProjectCopy(temporaryRoot, "second", checkedProjectPath);
            FontResourceImportRequest firstRequest = FontRequest(firstProject, inputFont, license);
            FontResourceImportRequest secondRequest = FontRequest(secondProject, inputFont, license);
            FakeFontConversionRunner runner = new();

            FontResourceImportResult first = FontResourceImporter.ImportAsync(firstRequest, runner)
                .GetAwaiter()
                .GetResult();
            FontResourceImportResult second = FontResourceImporter.ImportAsync(secondRequest, runner)
                .GetAwaiter()
                .GetResult();
            Assert(first.IsValid && second.IsValid, "A deterministic font resource did not import.");
            Assert(
                first.ResourceManifestSha256 == second.ResourceManifestSha256,
                "Equivalent font imports produced different resource identities.");
            Assert(first.Metrics?.EstimatedRomBytes == 69, "Measured font ROM payload changed.");

            DesignerProjectLoadResult updated = DesignerProjectLoader.LoadFile(firstProject);
            Assert(
                updated.Manifest?.ResourceManifests.SequenceEqual(
                    ["resources/litheview-resource-ui-font/resource.json"]) == true,
                "Font import did not add the contained resource manifest to the project.");
            string firstRoot = Path.GetDirectoryName(firstProject)!;
            ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(
                updated.Manifest!,
                firstRoot);
            Assert(resources.IsValid && resources.Resources.Count == 1, "Imported font resource did not reload.");
            DisplayApplicationTests.CheckImportedResource(repositoryRoot, resources);
            FontVariantMetadata? font = resources.Resources[0].Manifest.Variants[0].Font;
            Assert(
                font is { Family: "Fixture Sans", Weight: 600, Coverage: ResourceFormat.Alpha8 },
                "Imported font selection and measurements were not preserved.");
            HashSet<SurfacePixelFormat> compatibleFormats =
                [.. resources.Resources[0].Manifest.Variants[0].CompatiblePainterFormats];
            Assert(
                compatibleFormats.SetEquals(Enum.GetValues<SurfacePixelFormat>()),
                "Painter-independent font coverage was locked to the import-time display format.");
            ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
                resources.Resources[0].Manifest,
                firstRoot);
            Assert(verification.IsValid, "Imported source, license, or generated C++ lock changed.");

            FontResourceImportResult duplicate = FontResourceImporter.ImportAsync(firstRequest, runner)
                .GetAwaiter()
                .GetResult();
            Assert(!duplicate.IsValid, "Font import merged with an existing resource directory.");
            Assert(
                duplicate.Diagnostics.Any(item => item.Code is "LVFI1001" or "LVFI1002"),
                "Duplicate font import did not fail with a stable diagnostic.");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    public static void ImageResourceImportIsDeterministicAndTransactional(string checkedProjectPath, string repositoryRoot)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "LitheView.ImageResourceImportTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        string inputImage = Path.Combine(temporaryRoot, "input.png");
        string license = Path.Combine(temporaryRoot, "LICENSE.txt");
        File.WriteAllBytes(inputImage, "deterministic fake raster"u8.ToArray());
        File.WriteAllText(license, "Test-only image license\n", Encoding.UTF8);
        try
        {
            string firstProject = CreateProjectCopy(temporaryRoot, "image-first", checkedProjectPath);
            string secondProject = CreateProjectCopy(temporaryRoot, "image-second", checkedProjectPath);
            ImageResourceImportRequest firstRequest = ImageRequest(firstProject, inputImage, license);
            ImageResourceImportRequest secondRequest = ImageRequest(secondProject, inputImage, license);
            FakeImageConversionRunner runner = new();

            ImageResourceImportResult first = ImageResourceImporter.ImportAsync(firstRequest, runner)
                .GetAwaiter()
                .GetResult();
            ImageResourceImportResult second = ImageResourceImporter.ImportAsync(secondRequest, runner)
                .GetAwaiter()
                .GetResult();
            Assert(first.IsValid && second.IsValid, "A deterministic image resource did not import.");
            Assert(
                first.ResourceManifestSha256 == second.ResourceManifestSha256,
                "Equivalent image imports produced different resource identities.");
            Assert(first.Metrics?.EstimatedRomBytes == 2, "Measured image ROM payload changed.");

            DesignerProjectLoadResult updated = DesignerProjectLoader.LoadFile(firstProject);
            Assert(
                updated.Manifest?.ResourceManifests.SequenceEqual(
                    ["resources/litheview-resource-status-icon/resource.json"]) == true,
                "Image import did not add the contained resource manifest to the project.");
            string firstRoot = Path.GetDirectoryName(firstProject)!;
            ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(updated.Manifest!, firstRoot);
            Assert(resources.IsValid && resources.Resources.Count == 1, "Imported image resource did not reload.");
            DisplayApplicationTests.CheckImportedResource(repositoryRoot, resources);
            ResourceVariant variant = resources.Resources[0].Manifest.Variants[0];
            Assert(
                variant.Image is
                {
                    Tintable: true,
                    PixelBytes: 2,
                    PaletteBytes: 0,
                    ScaleMode: ImageScaleMode.NearestNeighbor
                },
                "Imported crop, tint, and measured image metadata were not preserved.");
            Assert(
                variant.CompatiblePainterFormats.ToHashSet().SetEquals(Enum.GetValues<SurfacePixelFormat>()),
                "Painter-readable bitmap storage was locked to the import-time display format.");
            ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
                resources.Resources[0].Manifest,
                firstRoot);
            Assert(verification.IsValid, "Imported image source, license, or generated C++ lock changed.");

            ImageResourceImportRequest invalidTint = firstRequest with
            {
                ResourceId = "litheview.resource.invalid-tint",
                Format = ResourceFormat.Rgb565,
                Tintable = true
            };
            ImageResourceImportResult rejected = ImageResourceImporter.ImportAsync(invalidTint, runner)
                .GetAwaiter()
                .GetResult();
            Assert(
                !rejected.IsValid && rejected.Diagnostics.Any(item => item.Code == "LVIC1009"),
                "A tintable direct-color icon did not fail closed.");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    public static void AnimationResourceImportIsDeterministicAndTransactional(string checkedProjectPath, string repositoryRoot)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "LitheView.AnimationResourceImportTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        string inputSheet = Path.Combine(temporaryRoot, "sheet.png");
        string license = Path.Combine(temporaryRoot, "LICENSE.txt");
        File.WriteAllBytes(inputSheet, "deterministic fake sprite sheet"u8.ToArray());
        File.WriteAllText(license, "Test-only animation license\n", Encoding.UTF8);
        try
        {
            string firstProject = CreateProjectCopy(temporaryRoot, "animation-first", checkedProjectPath);
            string secondProject = CreateProjectCopy(temporaryRoot, "animation-second", checkedProjectPath);
            AnimationResourceImportRequest firstRequest = AnimationRequest(firstProject, inputSheet, license);
            AnimationResourceImportRequest secondRequest = AnimationRequest(secondProject, inputSheet, license);
            FakeAnimationConversionRunner runner = new();

            AnimationResourceImportResult first = AnimationResourceImporter.ImportAsync(firstRequest, runner)
                .GetAwaiter()
                .GetResult();
            AnimationResourceImportResult second = AnimationResourceImporter.ImportAsync(secondRequest, runner)
                .GetAwaiter()
                .GetResult();
            Assert(first.IsValid && second.IsValid, "A deterministic animation resource did not import.");
            Assert(
                first.ResourceManifestSha256 == second.ResourceManifestSha256,
                "Equivalent animation imports produced different resource identities.");
            Assert(first.Metrics?.EstimatedRomBytes == 8, "Measured animation ROM payload changed.");

            DesignerProjectLoadResult updated = DesignerProjectLoader.LoadFile(firstProject);
            Assert(
                updated.Manifest?.ResourceManifests.SequenceEqual(
                    ["resources/litheview-resource-busy/resource.json"]) == true,
                "Animation import did not add the contained resource manifest to the project.");
            string firstRoot = Path.GetDirectoryName(firstProject)!;
            ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(updated.Manifest!, firstRoot);
            Assert(resources.IsValid && resources.Resources.Count == 1, "Imported animation did not reload.");
            DisplayApplicationTests.CheckImportedResource(repositoryRoot, resources);
            ResourceVariant variant = resources.Resources[0].Manifest.Variants[0];
            Assert(
                variant.Animation is
                {
                    Tintable: true,
                    Columns: 2,
                    FramePixelBytes: 2,
                    PixelBytes: 8,
                    FrameIntervalMilliseconds: 75,
                    RepeatIndex: 1,
                    Autoplay: true
                } && variant.Output.FrameCount == 4,
                "Imported frame grid, timing, or measured animation metadata changed.");
            Assert(
                variant.CompatiblePainterFormats.ToHashSet().SetEquals(Enum.GetValues<SurfacePixelFormat>()),
                "Painter-readable animation storage was locked to the import-time display format.");
            ResourceArtifactVerificationResult verification = ResourceArtifactVerifier.Verify(
                resources.Resources[0].Manifest,
                firstRoot);
            Assert(verification.IsValid, "Imported animation source, license, or generated C++ lock changed.");

            AnimationResourceImportResult duplicate = AnimationResourceImporter.ImportAsync(firstRequest, runner)
                .GetAwaiter()
                .GetResult();
            Assert(
                !duplicate.IsValid && duplicate.Diagnostics.Any(item => item.Code is "LVAI1001" or "LVAI1002"),
                "Animation import merged with an existing resource directory.");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static string CreateProjectCopy(string root, string name, string checkedProjectPath)
    {
        string projectRoot = Path.Combine(root, name);
        Directory.CreateDirectory(projectRoot);
        string destination = Path.Combine(projectRoot, DesignerWorkspaceService.ManifestFileName);
        File.Copy(checkedProjectPath, destination);
        return destination;
    }

    private static FontResourceImportRequest FontRequest(
        string projectManifestPath,
        string inputFont,
        string license)
    {
        return new FontResourceImportRequest
        {
            ProjectManifestPath = projectManifestPath,
            SourceFontPath = inputFont,
            SourceLicensePath = license,
            ResourceId = "litheview.resource.ui-font",
            DisplayName = "UI Font",
            GeneratedSymbol = "UiFont",
            EntryPoint = "uiFont",
            PixelSize = 14,
            RasterStyle = FontRasterStyle.Regular,
            StyleLabel = "Semibold",
            Weight = 600,
            Coverage = ResourceFormat.Alpha8,
            Codepoints = "U+0041-U+0042,U+003F",
            Origin = "Deterministic test fixture",
            Author = "LitheView tests",
            SourceUri = null,
            ProvenanceNotes = "No external font data is used by this fake converter test.",
            SpdxId = "LicenseRef-LitheView-Test",
            Attribution = "LitheView deterministic font importer fixture",
            RedistributionApproved = true
        };
    }

    private static ImageResourceImportRequest ImageRequest(
        string projectManifestPath,
        string inputImage,
        string license)
    {
        return new ImageResourceImportRequest
        {
            ProjectManifestPath = projectManifestPath,
            SourceImagePath = inputImage,
            SourceLicensePath = license,
            ResourceId = "litheview.resource.status-icon",
            DisplayName = "Status Icon",
            GeneratedSymbol = "StatusIcon",
            EntryPoint = "statusIcon",
            Kind = ResourceKind.Icon,
            Tintable = true,
            Format = ResourceFormat.Alpha1,
            CropX = 0,
            CropY = 0,
            CropWidth = 4,
            CropHeight = 4,
            OutputWidth = 2,
            OutputHeight = 2,
            ScaleMode = ImageScaleMode.NearestNeighbor,
            AlphaMode = ImageAlphaMode.Threshold,
            AlphaThreshold = 128,
            Dither = ImageDitherMode.None,
            TintArgb = "ffffffff",
            MatteArgb = "ff000000",
            PaletteArgb = string.Empty,
            Origin = "Deterministic test fixture",
            Author = "LitheView tests",
            SourceUri = null,
            ProvenanceNotes = "No external image data is decoded by this fake converter test.",
            SpdxId = "LicenseRef-LitheView-Test",
            Attribution = "LitheView deterministic image importer fixture",
            RedistributionApproved = true
        };
    }

    private static AnimationResourceImportRequest AnimationRequest(
        string projectManifestPath,
        string inputSheet,
        string license)
    {
        return new AnimationResourceImportRequest
        {
            ProjectManifestPath = projectManifestPath,
            SourceSpriteSheetPath = inputSheet,
            SourceLicensePath = license,
            ResourceId = "litheview.resource.busy",
            DisplayName = "Busy Animation",
            GeneratedSymbol = "BusyAnimation",
            EntryPoint = "busyAnimation",
            Tintable = true,
            Format = ResourceFormat.Alpha1,
            SheetX = 0,
            SheetY = 0,
            SourceFrameWidth = 2,
            SourceFrameHeight = 2,
            Columns = 2,
            FrameCount = 4,
            OutputWidth = 2,
            OutputHeight = 2,
            ScaleMode = ImageScaleMode.NearestNeighbor,
            AlphaMode = ImageAlphaMode.Threshold,
            AlphaThreshold = 128,
            Dither = ImageDitherMode.None,
            TintArgb = "ffffffff",
            MatteArgb = "ff000000",
            PaletteArgb = string.Empty,
            FrameIntervalMilliseconds = 75,
            RepeatIndex = 1,
            Autoplay = true,
            Origin = "Deterministic test fixture",
            Author = "LitheView tests",
            SourceUri = null,
            ProvenanceNotes = "No external animation data is decoded by this fake converter test.",
            SpdxId = "LicenseRef-LitheView-Test",
            Attribution = "LitheView deterministic animation importer fixture",
            RedistributionApproved = true
        };
    }

    private sealed class FakeFontConversionRunner : IFontConversionRunner
    {
        public Task<FontConversionResult> ConvertAsync(
            FontConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputSourcePath)!);
            File.WriteAllText(
                request.OutputSourcePath,
                "// deterministic generated font fixture\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!FontRepertoireParser.TryParse(
                    request.Codepoints,
                    out FontRepertoire? repertoire,
                    out string? error))
            {
                throw new InvalidOperationException($"Invalid test repertoire: {error}");
            }
            int glyphCount = repertoire!.Codepoints.Length;
            FontConversionMetrics metrics = new()
            {
                SchemaVersion = 2,
                ConverterId = "litheview.converter.font.gdiplus",
                ConverterVersion = "2.0.0",
                Family = "Fixture Sans",
                Style = request.StyleLabel,
                RasterStyle = request.RasterStyle.ToString(),
                Weight = request.Weight,
                PixelSize = request.PixelSize,
                Coverage = request.Coverage == ResourceFormat.Alpha1 ? "alpha1" : "alpha8",
                Repertoire = repertoire.Canonical,
                Symbol = request.Symbol,
                EntryPoint = request.EntryPoint,
                GlyphCount = glyphCount,
                CoverageBytes = 21,
                RecordBytes = glyphCount * 16L,
                EstimatedRomBytes = 21 + (glyphCount * 16L),
                DataChecksumFnv1a32 = "0x1234abcd",
                SourceSha256 = ComputeSha256(request.InputFontPath),
                Fallback = "U+003F",
                LineHeight = 17,
                Baseline = 13,
                UnderlinePosition = 14,
                UnderlineThickness = 1
            };
            return Task.FromResult(new FontConversionResult
            {
                Metrics = metrics,
                StandardOutput = "fixture",
                StandardError = string.Empty,
                Diagnostics = []
            });
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }

    private sealed class FakeImageConversionRunner : IImageConversionRunner
    {
        public Task<ImageConversionResult> ConvertAsync(
            ImageConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputSourcePath)!);
            File.WriteAllText(
                request.OutputSourcePath,
                "// deterministic generated image fixture\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            ImageConversionMetrics metrics = new()
            {
                SchemaVersion = 1,
                ConverterId = "litheview.converter.image.gdiplus",
                ConverterVersion = "1.0.0",
                Kind = "icon",
                Tintable = request.Tintable,
                Format = "alpha1",
                SourceWidth = 4,
                SourceHeight = 4,
                CropX = request.CropX,
                CropY = request.CropY,
                CropWidth = request.CropWidth,
                CropHeight = request.CropHeight,
                OutputWidth = request.OutputWidth,
                OutputHeight = request.OutputHeight,
                StrideBytes = 1,
                ScaleMode = request.ScaleMode.ToString(),
                AlphaMode = request.AlphaMode.ToString(),
                AlphaThreshold = request.AlphaThreshold,
                Dither = request.Dither.ToString(),
                TintArgb = request.TintArgb,
                MatteArgb = request.MatteArgb,
                PaletteArgb = request.PaletteArgb,
                Symbol = request.Symbol,
                EntryPoint = request.EntryPoint,
                PixelBytes = 2,
                PaletteBytes = 0,
                EstimatedRomBytes = 2,
                PixelChecksumFnv1a32 = "0x1234abcd",
                PaletteChecksumFnv1a32 = "0x811c9dc5",
                DataChecksumFnv1a32 = "0x1234abcd",
                SourceSha256 = ComputeSha256(request.InputImagePath)
            };
            return Task.FromResult(new ImageConversionResult
            {
                Metrics = metrics,
                StandardOutput = "fixture",
                StandardError = string.Empty,
                Diagnostics = []
            });
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }

    private sealed class FakeAnimationConversionRunner : IAnimationConversionRunner
    {
        public Task<AnimationConversionResult> ConvertAsync(
            AnimationConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputSourcePath)!);
            File.WriteAllText(
                request.OutputSourcePath,
                "// deterministic generated animation fixture\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AnimationConversionMetrics metrics = new()
            {
                SchemaVersion = 1,
                ConverterId = "litheview.converter.animation.gdiplus",
                ConverterVersion = "1.0.0",
                Tintable = request.Tintable,
                Format = "alpha1",
                SourceWidth = 4,
                SourceHeight = 4,
                SheetX = request.SheetX,
                SheetY = request.SheetY,
                SourceFrameWidth = request.SourceFrameWidth,
                SourceFrameHeight = request.SourceFrameHeight,
                Columns = request.Columns,
                FrameCount = request.FrameCount,
                OutputWidth = request.OutputWidth,
                OutputHeight = request.OutputHeight,
                StrideBytes = 1,
                ScaleMode = request.ScaleMode.ToString(),
                AlphaMode = request.AlphaMode.ToString(),
                AlphaThreshold = request.AlphaThreshold,
                Dither = request.Dither.ToString(),
                TintArgb = request.TintArgb,
                MatteArgb = request.MatteArgb,
                PaletteArgb = request.PaletteArgb,
                FrameIntervalMilliseconds = request.FrameIntervalMilliseconds,
                RepeatIndex = request.RepeatIndex,
                Autoplay = request.Autoplay,
                Symbol = request.Symbol,
                EntryPoint = request.EntryPoint,
                FramePixelBytes = 2,
                PixelBytes = 8,
                PaletteBytes = 0,
                EstimatedRomBytes = 8,
                PixelChecksumFnv1a32 = "0x1234abcd",
                PaletteChecksumFnv1a32 = "0x811c9dc5",
                DataChecksumFnv1a32 = "0x1234abcd",
                SourceSha256 = ComputeSha256(request.InputSpriteSheetPath)
            };
            return Task.FromResult(new AnimationConversionResult
            {
                Metrics = metrics,
                StandardOutput = "fixture",
                StandardError = string.Empty,
                Diagnostics = []
            });
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }

    private static void CopyArtifact(string sourceRoot, string relativePath, string destinationRoot)
    {
        string source = Resolve(sourceRoot, relativePath);
        string destination = Resolve(destinationRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }

    private static string Resolve(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static ResourceManifest RequireManifest(ResourceManifestLoadResult loaded)
    {
        return loaded.Manifest ?? throw new InvalidOperationException(
            "The checked resource manifest was not loaded.");
    }

    private static ProjectResourceSetLoadResult ReplaceVariant(
        ProjectResourceSetLoadResult resources,
        LoadedProjectResource sourceResource,
        ResourceVariant replacement)
    {
        LoadedProjectResource resource = sourceResource with
        {
            Manifest = sourceResource.Manifest with { Variants = [replacement] }
        };
        return resources with { Resources = [resource] };
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
