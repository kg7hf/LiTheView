// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Tests;

internal static class DisplayDefinitionTests
{
    public static void Run(string repositoryRoot)
    {
        string root = Path.Combine(repositoryRoot, "profiles", "display-definitions");
        string inputPath = Path.Combine(repositoryRoot, "profiles", "logical-inputs", "encoder-select.json");
        LogicalInputConfigurationLoadResult inputs = LogicalInputConfigurationLoader.LoadFile(inputPath);
        Require(inputs.IsValid, "Separate logical input fixture did not load.");
        string[] files = Directory.GetFiles(root, "*.json").Order(StringComparer.Ordinal).ToArray();
        Require(files.Length == 6, "Six explicit display definition fixtures are required.");
        foreach (string file in files)
        {
            DisplayDefinitionLoadResult loaded = DisplayDefinitionProfileLoader.LoadFile(file);
            Require(loaded.IsValid, $"Display definition did not load: {file}");
            DisplayDefinitionLoadResult repeated = DisplayDefinitionProfileLoader.Load(loaded.CanonicalUtf8!);
            Require(repeated.IsValid && loaded.Sha256 == repeated.Sha256,
                "Canonical display definitions must reach a stable fixed point.");
            DisplayDefinitionProfile profile = loaded.Profile!;
            DisplayDefinitionProfile reordered = profile with { ResourceFormats = [.. profile.ResourceFormats.Reverse()] };
            Require(DisplayDefinitionCanonicalizer.Canonicalize(profile).AsSpan().SequenceEqual(
                    DisplayDefinitionCanonicalizer.Canonicalize(reordered)),
                "Resource capability ordering must not change display identity.");
            DisplayDefinitionCppCompilationResult first = DisplayDefinitionCppCompiler.Compile(loaded, inputs.Configuration!);
            Require(first.IsValid, "A target-free fixed display must export C++.");
            CultureInfo oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                DisplayDefinitionCppCompilationResult otherCulture = DisplayDefinitionCppCompiler.Compile(loaded, inputs.Configuration!);
                Require(otherCulture.IsValid && first.HeaderUtf8!.AsSpan().SequenceEqual(otherCulture.HeaderUtf8),
                    "Compiled definition bytes must be culture-independent.");
            }
            finally
            {
                CultureInfo.CurrentCulture = oldCulture;
            }
            Require(!Encoding.UTF8.GetString(first.HeaderUtf8!).Contains('\r'), "Generated C++ requires canonical LF newlines.");
        }
        CheckStrictness(DisplayDefinitionProfileLoader.LoadFile(Path.Combine(root, "synthetic-r2.json")), inputs.Configuration!);
        CheckLegacy(repositoryRoot, inputs.Configuration!);
        CheckInputs(inputs);
        CheckGeometry(DisplayDefinitionProfileLoader.LoadFile(Path.Combine(root, "round-240-rgb565.json")).Profile!);
    }

    private static void CheckStrictness(DisplayDefinitionLoadResult loaded, LogicalInputConfigurationProfile inputs)
    {
        DisplayDefinitionProfile profile = loaded.Profile!;
        Require(profile.Geometry.Width == 512 && profile.Geometry.Height == 384 &&
                profile.AddressSpace == DisplayAddressSpace.Emitters &&
                profile.Format == SurfacePixelFormat.R2Packed2x2 &&
                profile.StorageBlock == new DisplayStorageBlock { Width = 2, Height = 2, Bytes = 1 } &&
                profile.Source.Status == DisplaySourceStatus.SoftwareContract,
            "The native red fixture must preserve its logical geometry and unverified software identity.");
        Require(profile.Fields.Select(field => field.BitOffset).SequenceEqual([2, 6, 0, 4]),
            "The independently specified row-major native bit offsets changed.");
        string json = Encoding.UTF8.GetString(loaded.CanonicalUtf8!);
        foreach (string invalid in new[]
        {
            json.Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"schemaVersion\":2", StringComparison.Ordinal),
            json.Replace("\"geometry\":{", "\"target\":{},\"geometry\":{", StringComparison.Ordinal),
            json.Replace("\"geometry\":{", "\"pointer\":true,\"geometry\":{", StringComparison.Ordinal),
            json.Replace("\"position\":{\"x\":0", "\"position\":{\"x\":0,\"x\":0", StringComparison.Ordinal),
            json.Replace("\"fields\":[{", "\"fields\":[null,{", StringComparison.Ordinal),
            json.Replace("\"format\":\"r2Packed2x2\"", "\"format\":\"R2Packed2x2\"", StringComparison.Ordinal)
        })
        {
            Require(invalid != json && !DisplayDefinitionProfileLoader.Load(Encoding.UTF8.GetBytes(invalid)).IsValid,
                "Duplicate, unknown, misplaced, null, or noncanonical properties must fail closed.");
        }
        ExpectCode(profile with { Geometry = profile.Geometry with { Width = 511 } }, "LVDS2007");
        ExpectCode(profile with { AddressSpace = DisplayAddressSpace.Cells }, "LVDS2006");
        ExpectCode(profile with { Fields = [profile.Fields[1], profile.Fields[0], profile.Fields[2], profile.Fields[3]] }, "LVDD1101");
        ExpectCode(profile with { Composition = profile.Composition with { DestinationAlpha = true } }, "LVDD1102");
        ExpectCode(profile with { ResourceFormats = [ResourceFormat.Alpha1, ResourceFormat.Alpha1] }, "LVDD1103");
        ExpectCode(profile with { Source = profile.Source with { Status = DisplaySourceStatus.Verified } }, "LVDD1104");
        ExpectCode(profile with { Geometry = profile.Geometry with { RotationDegrees = 90 } }, "LVDD1003");
        Require(!DisplayDefinitionCppCompiler.Compile(loaded, inputs, "class").IsValid,
            "C++ reserved names must be rejected.");
        Require(!DisplayDefinitionCppCompiler.Compile(loaded with { Sha256 = new string('0', 64) }, inputs).IsValid,
            "Stale definition hashes must fail before C++ generation.");
    }

    private static void CheckLegacy(string repositoryRoot, LogicalInputConfigurationProfile inputs)
    {
        string root = Path.Combine(repositoryRoot, "profiles", "display-surfaces");
        foreach (string name in new[] { "stm32h750b-dk-rk043fn48h-rgb565.json",
                     "stm32h750b-dk-rk043fn48h-xrgb8888.json", "gc9a01-1.28-round-240-rgb565.json",
                     "synthetic-red-staging.json", "synthetic-red-yellow-staging.json" })
        {
            DisplaySurfaceLoadResult legacy = DisplaySurfaceProfileLoader.LoadFile(Path.Combine(root, name));
            Require(legacy.IsValid, "Existing version-1 profiles must remain valid.");
            DisplayDefinitionLoadResult migrated = DisplayDefinitionProfileLoader.AdaptV1(legacy);
            Require(migrated.IsValid && migrated.RequiresMigration && migrated.LegacySurfaceSha256 == legacy.Sha256 &&
                    migrated.Profile!.Id == legacy.Profile!.Id &&
                    migrated.Profile.Geometry.Width == legacy.Profile.PixelSize.Width &&
                    migrated.Profile.Geometry.Height == legacy.Profile.PixelSize.Height &&
                    migrated.Profile.Format == legacy.Profile.PainterSurface.Format &&
                    migrated.LegacySurface == legacy.Profile,
                "Explicit v1 adaptation must preserve ID, geometry, RGB staging and original native metadata/hash.");
            bool needsPresentation = legacy.Profile!.NativeDisplay.ColorModel != NativeColorModel.Rgb;
            DisplayDefinitionCppCompilationResult exported = DisplayDefinitionCppCompiler.Compile(migrated, inputs);
            Require(exported.IsValid != needsPresentation,
                "Legacy synthetic packed display export needs explicit native migration; equivalent H750/round definitions export.");
            if (needsPresentation)
            {
                Require(exported.Diagnostics.Any(item => item.Code == "LVDD4002"),
                    "Legacy native mapping loss must have its own stable diagnostic.");
            }
            DisplaySurfaceProfile invalid = legacy.Profile with
            {
                PainterSurface = new() { Format = SurfacePixelFormat.R2Packed2x2, ColorDepthBits = 0, StorageBitsPerPixel = 0 }
            };
            Require(DisplaySurfaceValidator.Validate(invalid).Any(item => item.Code == "LVDS1501"),
                "Version-1 validation must reject appended native formats even with zero depths.");
        }
    }

    private static void CheckInputs(LogicalInputConfigurationLoadResult inputs)
    {
        LogicalInputConfigurationProfile configuration = inputs.Configuration!;
        Require(!configuration.Pointer && !configuration.Keys && !configuration.Text &&
                configuration.Encoder && configuration.Select, "Encoder/select is independent of pointer/keyboard input.");
        Require(LogicalInputConfigurationLoader.Validate(configuration with
                { Encoder = false, Select = false, Text = true }).Count == 0,
            "Logical capabilities must not infer a physical keyboard or driver.");
        string json = Encoding.UTF8.GetString(inputs.CanonicalUtf8!);
        Require(!LogicalInputConfigurationLoader.Load(Encoding.UTF8.GetBytes(
                json.Replace("\"pointer\":", "\"driver\":\"gpio\",\"pointer\":", StringComparison.Ordinal))).IsValid,
            "Logical input must reject physical driver fields.");
        Require(LogicalInputConfigurationLoader.Load(inputs.CanonicalUtf8!).Sha256 == inputs.Sha256,
            "Logical input identity must be stable independently of a display.");
    }

    private static void CheckGeometry(DisplayDefinitionProfile circle)
    {
        ExpectCode(circle with { Id = "Uppercase.Id" }, "LVDS2001");
        ExpectCode(circle with { Geometry = circle.Geometry with { Width = 0 } }, "LVDS2002");
        ExpectCode(circle with { Geometry = circle.Geometry with { Width = 239 } }, "LVDS2003");
        ExpectCode(circle with { Geometry = circle.Geometry with
            { SafeArea = new() { X = 0, Y = 0, Width = 240, Height = 240 } } }, "LVDS2004");
    }

    private static void ExpectCode(DisplayDefinitionProfile profile, string code) =>
        Require(DisplayDefinitionValidator.Validate(profile).Any(item => item.Code == code),
            $"Invalid definition did not produce {code}.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
