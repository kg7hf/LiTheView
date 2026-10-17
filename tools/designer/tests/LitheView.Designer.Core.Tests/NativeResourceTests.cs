// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Security.Cryptography;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.Core.Tests;

internal static class NativeResourceTests
{
    public static void LayoutAndMetricsAreChecked(string repositoryRoot)
    {
        Assert(ImageConversionRequestValidator.MinimumStride(ResourceFormat.R2Packed2x2, 512) == 256 &&
            ImageConversionRequestValidator.StorageRowCount(ResourceFormat.R2Packed2x2, 512) == 256,
            "R2 layout must measure storage block rows, not logical pixel rows.");
        Assert(!ImageConversionRequestValidator.IsAlignedExtent(ResourceFormat.R2Packed2x2, 3, 2) &&
            !ImageConversionRequestValidator.IsAlignedExtent(ResourceFormat.R2Packed2x2, 2, 3),
            "Partial native blocks were accepted.");
        ImageConversionRequest request = Request("source.png", "output.cpp", "metrics.json");
        Assert(!ImageConversionRequestValidator.Validate(request).Any(IsError),
            "The valid R2 conversion request was rejected.");
        Assert(ImageConversionRequestValidator.Validate(request with { OutputWidth = 3 }).Any(IsError) &&
            ImageConversionRequestValidator.Validate(request with { OutputHeight = 3 }).Any(IsError),
            "Odd native extents were accepted by conversion validation.");

        string root = FixtureRoot(repositoryRoot);
        ImageConversionMetricsLoadResult native = ImageConversionMetricsLoader.LoadFile(
            Path.Combine(root, "generated", "resources", "gallop-r2.metrics.json"));
        ImageConversionMetricsLoadResult alpha = ImageConversionMetricsLoader.LoadFile(
            Path.Combine(root, "generated", "resources", "gallop-alpha1.metrics.json"));
        Assert(native.IsValid && native.Metrics is { StrideBytes: 48, PixelBytes: 1728, EstimatedRomBytes: 1728 } &&
            alpha.IsValid && alpha.Metrics is { StrideBytes: 12, PixelBytes: 864, EstimatedRomBytes: 864 },
            "Native block-row or compact alpha measurements changed.");
        ResourceManifest manifest = LoadFixture(root).Resources[0].Manifest;
        ResourceVariant variant = manifest.Variants.Single(item => item.Output.Format == ResourceFormat.R2Packed2x2);
        Assert(!ResourceManifestValidator.Validate(manifest).Any(IsError), "The native manifest is invalid.");
        ResourceManifest odd = manifest with
        {
            Variants = [variant with { Output = variant.Output with { PixelSize = new PixelSize { Width = 95, Height = 72 } } }]
        };
        Assert(ResourceManifestValidator.Validate(odd).Any(item => item.Code == "LVRM1504"),
            "The manifest accepted an incomplete R2 block.");
        ResourceManifest wrongRows = manifest with
        {
            Variants = [variant with { Image = variant.Image! with { PixelBytes = 3456, EstimatedRomBytes = 3456 } }]
        };
        Assert(ResourceManifestValidator.Validate(wrongRows).Any(item => item.Code == "LVRM1726"),
            "Logical pixel rows were mistaken for native storage rows.");
        Assert(Hash(Path.Combine(root, "generated", "resources", "gallop-r2.bin")) ==
            "c65342db72675a19996b3a7aa0173531fe8072663faa85d972a6f6907a1f8caa" &&
            Hash(Path.Combine(root, "generated", "resources", "gallop-alpha1.bin")) ==
            "727b79f083903d2154f9f20154f92973ee27fe293bd7e409d73e313a73587dfb",
            "Retained source conversion payload hashes changed.");
        Assert(Hash(Path.Combine(root, "assets", "gallop_source.png")) ==
            Hash(Path.Combine(repositoryRoot, "tests", "rendering", "assets", "animations", "gallop_source.png")),
            "The fixture no longer uses the exact retained source asset.");
    }

    public static void SelectionAndProviderAreDeterministic(string repositoryRoot)
    {
        ProjectResourceSetLoadResult resources = LoadFixture(FixtureRoot(repositoryRoot));
        ResourceSurfaceResolutionResult native = ResourceSurfaceResolver.Resolve(
            resources, SurfacePixelFormat.Rrrr2222, [ResourceFormat.Alpha1, ResourceFormat.R2Packed2x2]);
        ResourceSurfaceResolutionResult rgb = ResourceSurfaceResolver.Resolve(resources, SurfacePixelFormat.Rgb565);
        Assert(native.IsValid && native.Variants.Single().Variant.Output.Format == ResourceFormat.R2Packed2x2 &&
            rgb.IsValid && rgb.Variants.Single().Variant.Output.Format == ResourceFormat.Alpha1,
            "Resource resolution did not select exactly one actual source representation.");
        Assert(!ResourceSurfaceResolver.Resolve(resources, SurfacePixelFormat.Rrrr2222, [ResourceFormat.Alpha1]).IsValid,
            "The profile source-format restriction was ignored.");
        Assert(ResourceSurfaceResolver.SupportsSourceFormat(SurfacePixelFormat.Rgb565, ResourceFormat.R2Packed2x2),
            "The core's native R2 source decoder was artificially restricted to native destinations.");
        LoadedProjectResource loaded = resources.Resources[0];
        ResourceVariant selected = native.Variants[0].Variant;
        ResourceVariant impostor = selected with { Output = selected.Output with { Format = (ResourceFormat)255 } };
        Assert(!ResourceSurfaceResolver.Resolve(resources with
        {
            Resources = [loaded with { Manifest = loaded.Manifest with { Variants = [impostor] } }]
        }, SurfacePixelFormat.Rrrr2222).IsValid, "A compatibility label authorized an unknown actual format.");
        Assert(!ResourceSurfaceResolver.Resolve(resources with
        {
            Resources = [loaded with { Manifest = loaded.Manifest with { Variants = [selected, selected] } }]
        }, SurfacePixelFormat.Rrrr2222).IsValid, "An ambiguous resource selection was accepted.");

        Dictionary<string, string> names = new(StringComparer.Ordinal) { ["gallop-icon"] = "Gallop \"icon\"" };
        SelectedResourceCppEmissionResult emitted = SelectedResourceCppEmitter.Emit(native, names);
        Assert(emitted.IsValid && emitted.EstimatedRomBytes == 1728 && emitted.RequiredSources.Count == 1 &&
            emitted.RequiredSources[0].Path == "generated/resources/gallop-r2.cpp" &&
            emitted.HeaderText!.Contains("gallopR2()", StringComparison.Ordinal) &&
            !emitted.HeaderText.Contains("gallopAlpha1()", StringComparison.Ordinal) &&
            emitted.HeaderText.Contains("Gallop \\\"icon\\\"", StringComparison.Ordinal),
            "Provider selection, measured cost, escaping, or source omission changed.");
        SelectedResourceCppEmissionResult repeated = SelectedResourceCppEmitter.Emit(
            native with { Variants = [.. native.Variants.Reverse()] }, names);
        Assert(repeated.HeaderText == emitted.HeaderText, "Resource order affected deterministic provider output.");
        Assert(SelectedResourceCppEmitter.Emit(native, new Dictionary<string, string>(StringComparer.Ordinal)).IsValid,
            "An empty requested-resource set should produce a valid empty provider.");
        Assert(!SelectedResourceCppEmitter.Emit(native,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["missing"] = "missing" }).IsValid,
            "A missing requested resource was silently ignored.");
        ResolvedResourceVariant unverified = native.Variants[0] with { OutputVerified = false };
        Assert(!SelectedResourceCppEmitter.Emit(native with { Variants = [unverified] }, names).IsValid,
            "An unverified source was emitted.");
        ResolvedResourceVariant binary = native.Variants[0] with
        {
            Variant = selected with { Output = selected.Output with { Path = "generated/native.bin" } }
        };
        Assert(!SelectedResourceCppEmitter.Emit(native with { Variants = [binary] }, names).IsValid,
            "A binary fixture was silently treated as a C++ converter output.");
        ResolvedResourceVariant unmatched = native.Variants[0] with
        {
            Variant = selected with { Converter = selected.Converter with { Options = [] } }
        };
        Assert(!SelectedResourceCppEmitter.Emit(native with { Variants = [unmatched] }, names).IsValid,
            "An unmatched converter entry point was emitted.");
    }

    public static void ConverterMatchesIndependentPackingOracle(string repositoryRoot)
    {
        string directory = Path.Combine(repositoryRoot, "build", "native-resource-converter-test");
        Directory.CreateDirectory(directory);
        string sourcePath = Path.Combine(directory, "levels.bmp");
        WriteLevelBitmap(sourcePath);
        string outputPath = Path.Combine(directory, "levels.cpp");
        string metricsPath = Path.Combine(directory, "levels.metrics.json");
        PowerShellImageConversionRunner runner = new(Path.Combine(repositoryRoot, "tools", "convert-image.ps1"));
        ImageConversionResult result = runner.ConvertAsync(Request(sourcePath, outputPath, metricsPath))
            .GetAwaiter().GetResult();
        Assert(result.IsValid && result.Metrics is { StrideBytes: 2, PixelBytes: 4, EstimatedRomBytes: 4 },
            "The real converter failed the bounded native bitmap fixture: " + result.StandardError);
        string source = File.ReadAllText(outputPath);
        // Independent literal oracle: each block has TL=0, TR=1, BL=2, BR=3,
        // yielding (0<<2)|(1<<6)|(2<<0)|(3<<4) = 0x72. There are four blocks.
        Assert(source.Split("std::byte{0x72U}", StringSplitOptions.None).Length == 5 &&
            source.Contains("BitmapFormat::r2Packed2x2", StringComparison.Ordinal),
            "Native output differs from the independent 2x2 emitter-order oracle.");
        byte[] initial = File.ReadAllBytes(outputPath);
        ImageConversionResult repeated = runner.ConvertAsync(Request(sourcePath, outputPath, metricsPath))
            .GetAwaiter().GetResult();
        Assert(repeated.IsValid && initial.AsSpan().SequenceEqual(File.ReadAllBytes(outputPath)),
            "Identical native conversion inputs did not produce byte-identical C++ output.");
    }

    private static ImageConversionRequest Request(string input, string output, string metrics) => new()
    {
        InputImagePath = input, OutputSourcePath = output, OutputMetricsPath = metrics,
        Symbol = "NativeLevels", EntryPoint = "nativeLevels", Kind = ResourceKind.Image,
        Tintable = false, Format = ResourceFormat.R2Packed2x2,
        CropX = 0, CropY = 0, CropWidth = 4, CropHeight = 4, OutputWidth = 4, OutputHeight = 4,
        ScaleMode = ImageScaleMode.NearestNeighbor, AlphaMode = ImageAlphaMode.Preserve,
        AlphaThreshold = 128, Dither = ImageDitherMode.None, TintArgb = "ffff0000",
        MatteArgb = "ff000000", PaletteArgb = string.Empty
    };

    private static void WriteLevelBitmap(string path)
    {
        // A topologically independent 24-bit BMP fixture; rows are stored bottom first.
        byte[] bytes = new byte[54 + 4 * 12];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), 4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 24);
        for (int y = 0; y < 4; ++y)
        {
            for (int x = 0; x < 4; ++x)
            {
                bytes[54 + (3 - y) * 12 + x * 3 + 2] = (byte)(((y % 2) * 2 + x % 2) * 85);
            }
        }
        File.WriteAllBytes(path, bytes);
    }

    private static ProjectResourceSetLoadResult LoadFixture(string root)
    {
        ProjectResourceSetLoadResult result = ProjectResourceSetLoader.Load(["resources/gallop.resource.json"], root);
        Assert(result.IsValid && result.Resources.Count == 1,
            "The retained asset manifest or verified artifacts did not load: " +
            string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        return result;
    }

    private static string FixtureRoot(string root) => Path.Combine(root, "tests", "rendering", "display-model-generated");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static bool IsError(ProfileDiagnostic diagnostic) => diagnostic.Severity == ProfileDiagnosticSeverity.Error;
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
