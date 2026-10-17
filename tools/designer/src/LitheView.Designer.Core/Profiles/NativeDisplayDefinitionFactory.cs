// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Preview;

namespace LitheView.Designer.Core.Profiles;

// Wizard-created definitions use the same format-derived layout as the compiler.
// A developer's reference is retained without claiming physical verification.
public static class NativeDisplayDefinitionFactory
{
    public static DisplayDefinitionLoadResult Create(string id, string name, SurfacePixelFormat format,
        int width, int height, SurfaceShape shape, SafeAreaProfile? safeArea, string sourceReference)
    {
        if (!DisplayDefinitionValidator.IsNativePacked(format))
        {
            return Invalid("Choose a supported native packed format.");
        }
        int scale = format == SurfacePixelFormat.R2Packed2x2 ? 1 : 2;
        if (width is < 1 or > PreviewLimits.MaximumSurfaceDimension ||
            height is < 1 or > PreviewLimits.MaximumSurfaceDimension ||
            (long)width * height * scale * scale > 4_194_304)
        {
            return Invalid("Use positive logical dimensions up to 4096 and at most 4,194,304 presented emitters.");
        }
        int inset = shape == SurfaceShape.Circle
            ? (width - (int)Math.Floor(width / Math.Sqrt(2)) + 1) / 2 : 0;
        DisplayDefinitionProfile definition = new()
        {
            Schema = "https://litheview.dev/schema/display-definition.schema.json", SchemaVersion = 2,
            Id = id, DisplayName = name, Format = format,
            Geometry = new()
            {
                Width = width, Height = height, Shape = shape, RotationDegrees = 0,
                SafeArea = safeArea ?? new() { X = inset, Y = inset, Width = width - 2 * inset, Height = height - 2 * inset }
            },
            AddressSpace = DisplayDefinitionValidator.AddressSpaceFor(format),
            StorageBlock = DisplayDefinitionValidator.StorageBlockFor(format),
            ChannelModel = DisplayDefinitionValidator.ChannelModelFor(format),
            Fields = DisplayDefinitionValidator.FieldsFor(format),
            Composition = DisplayDefinitionValidator.CompositionFor(format),
            ResourceFormats = [ResourceFormat.Alpha1, ResourceFormat.Indexed4, ResourceFormat.R2Packed2x2],
            Source = new() { Status = DisplaySourceStatus.Unverified, Reference = sourceReference, Sha256 = null }
        };
        return DisplayDefinitionProfileLoader.Load(DisplayDefinitionCanonicalizer.Canonicalize(definition));
    }

    public static DisplayDefinitionLoadResult[] LoadCatalog(string directory) => !Directory.Exists(directory) ? [] :
        Directory.EnumerateFiles(directory, "*.json").Select(DisplayDefinitionProfileLoader.LoadFile)
            .Where(item => item.IsValid && !item.RequiresMigration && item.Profile is { } definition &&
                DisplayDefinitionValidator.IsNativePacked(definition.Format))
            .OrderBy(item => item.Profile!.DisplayName, StringComparer.Ordinal).ToArray();

    private static DisplayDefinitionLoadResult Invalid(string message) => new()
    {
        Diagnostics = [new("LVDD1201", ProfileDiagnosticSeverity.Error, "$.geometry", message)]
    };
}
