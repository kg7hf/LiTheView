// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public static class DisplayDefinitionProfileLoader
{
    public static DisplayDefinitionLoadResult LoadFile(string path)
    {
        List<ProfileDiagnostic> diagnostics = [];
        byte[]? bytes = DisplayDefinitionJson.ReadFile(path, "LVDD", diagnostics);
        return bytes is null ? new() { Diagnostics = diagnostics } : Load(bytes);
    }

    public static DisplayDefinitionLoadResult Load(ReadOnlyMemory<byte> utf8)
    {
        List<ProfileDiagnostic> diagnostics = [];
        DisplayDefinitionProfile? profile = DisplayDefinitionJson.Read<DisplayDefinitionProfile>(utf8, "LVDD", diagnostics);
        return profile is null ? new() { Diagnostics = diagnostics } : Finish(profile, diagnostics);
    }

    // Explicit legacy adaptation preserves the original renderer extent/format
    // and records the old transfer interpretation separately. It never changes
    // 608x512 forms into 304x256 forms or promotes RGB staging to native storage.
    public static DisplayDefinitionLoadResult AdaptV1(DisplaySurfaceLoadResult legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        List<ProfileDiagnostic> diagnostics = [.. legacy.Diagnostics];
        if (!legacy.IsValid || legacy.Profile is null || legacy.Sha256 is null)
        {
            DisplayDefinitionJson.Error(diagnostics, "LVDD3001", "$", "A valid hash-locked version-1 surface is required.");
            return new() { Diagnostics = diagnostics };
        }
        DisplaySurfaceProfile source = legacy.Profile;
        SurfacePixelFormat format = source.PainterSurface.Format;
        DisplayDefinitionProfile profile = new()
        {
            Schema = "https://litheview.dev/schema/display-definition.schema.json",
            SchemaVersion = 2,
            Id = source.Id,
            DisplayName = source.DisplayName,
            Geometry = new()
            {
                Width = source.PixelSize.Width,
                Height = source.PixelSize.Height,
                Shape = source.Shape,
                RotationDegrees = source.CoordinateSystem.RotationDegrees,
                SafeArea = source.SafeArea
            },
            AddressSpace = DisplayDefinitionValidator.AddressSpaceFor(format),
            Format = format,
            StorageBlock = DisplayDefinitionValidator.StorageBlockFor(format),
            ChannelModel = DisplayDefinitionValidator.ChannelModelFor(format),
            Fields = DisplayDefinitionValidator.FieldsFor(format),
            Composition = DisplayDefinitionValidator.CompositionFor(format),
            ResourceFormats = Enum.GetValues<ResourceFormat>(),
            Source = new()
            {
                Status = DisplaySourceStatus.Unverified,
                Reference = source.Provenance.SourceDocument,
                Sha256 = source.Provenance.SourceSha256
            }
        };
        diagnostics.Add(new("LVDD3002", ProfileDiagnosticSeverity.Warning, "$",
            "Explicit version-1 adaptation preserves renderer geometry and format; original profile hash and native interpretation are retained."));
        if (source.NativeDisplay.ColorModel != NativeColorModel.Rgb)
        {
            diagnostics.Add(new("LVDD3003", ProfileDiagnosticSeverity.Warning, "$.nativeDisplay",
                "Legacy synthetic packed display RGB staging requires its historical presentation adapter; select an explicit native version-2 definition before C++ export."));
        }
        DisplayDefinitionLoadResult result = Finish(profile, diagnostics);
        return result with { LegacySurface = source, LegacySurfaceSha256 = legacy.Sha256 };
    }

    private static DisplayDefinitionLoadResult Finish(DisplayDefinitionProfile profile, List<ProfileDiagnostic> diagnostics)
    {
        diagnostics.AddRange(DisplayDefinitionValidator.Validate(profile));
        if (diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return new() { Diagnostics = diagnostics };
        }
        byte[] canonical = DisplayDefinitionCanonicalizer.Canonicalize(profile);
        return new()
        {
            Profile = profile,
            CanonicalUtf8 = canonical,
            Sha256 = DisplaySurfaceCanonicalizer.ComputeSha256(canonical),
            Diagnostics = diagnostics
        };
    }
}
