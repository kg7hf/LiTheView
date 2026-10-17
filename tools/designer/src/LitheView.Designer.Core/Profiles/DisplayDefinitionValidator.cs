// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public static class DisplayDefinitionValidator
{
    public static IReadOnlyList<ProfileDiagnostic> Validate(DisplayDefinitionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        List<ProfileDiagnostic> diagnostics = [];
        if (profile.SchemaVersion != 2 || string.IsNullOrWhiteSpace(profile.Schema))
        {
            Error(diagnostics, "LVDD1001", "$.schemaVersion", "Display definitions require schema version 2.");
        }
        if (!ProfileRules.IsValidIdentifier(profile.Id))
        {
            Error(diagnostics, "LVDS2001", "$.id", "Display ID must be a bounded lowercase profile identifier.");
        }
        if (string.IsNullOrWhiteSpace(profile.DisplayName) ||
            profile.DisplayName.Length > ProfileRules.MaximumDisplayNameLength)
        {
            Error(diagnostics, "LVDD1002", "$.displayName", "A bounded display name is required.");
        }
        ValidateGeometry(profile.Geometry, diagnostics);
        if (!Enum.IsDefined(profile.Format))
        {
            Error(diagnostics, "LVDS2005", "$.format", "Unsupported renderer format.");
            return diagnostics;
        }
        DisplayAddressSpace expectedAddressSpace = AddressSpaceFor(profile.Format);
        if (profile.AddressSpace != expectedAddressSpace)
        {
            Error(diagnostics, "LVDS2006", "$.addressSpace", "Address space does not match the compiled format contract.");
        }
        DisplayStorageBlock block = StorageBlockFor(profile.Format);
        if (profile.StorageBlock != block ||
            profile.Geometry.Width % block.Width != 0 || profile.Geometry.Height % block.Height != 0)
        {
            Error(diagnostics, "LVDS2007", "$.storageBlock", "Storage blocks must match the format and divide the logical extent.");
        }
        if (profile.ChannelModel != ChannelModelFor(profile.Format) ||
            !profile.Fields.SequenceEqual(FieldsFor(profile.Format)))
        {
            Error(diagnostics, "LVDD1101", "$.fields", "Channel fields must exactly match the supported format layout.");
        }
        if (profile.Composition != CompositionFor(profile.Format))
        {
            Error(diagnostics, "LVDD1102", "$.composition", "Composition capabilities must match the renderer contract.");
        }
        if (profile.ResourceFormats.Length is < 1 or > 8 ||
            profile.ResourceFormats.Distinct().Count() != profile.ResourceFormats.Length ||
            profile.ResourceFormats.Any(format => !Enum.IsDefined(format)))
        {
            Error(diagnostics, "LVDD1103", "$.resourceFormats", "Resource formats must be known, unique and bounded.");
        }
        DisplayDefinitionSource source = profile.Source;
        if (!Enum.IsDefined(source.Status) || string.IsNullOrWhiteSpace(source.Reference) ||
            source.Reference.Length > 1024 ||
            source.Sha256 is not null && !ProfileRules.IsLowercaseSha256(source.Sha256) ||
            source.Status == DisplaySourceStatus.Verified && source.Sha256 is null)
        {
            Error(diagnostics, "LVDD1104", "$.source", "Source evidence must be explicit; verified sources require a hash.");
        }
        return diagnostics;
    }

    public static bool IsNativePacked(SurfacePixelFormat format) =>
        format is SurfacePixelFormat.R2Packed2x2 or SurfacePixelFormat.Rrrr2222 or SurfacePixelFormat.Ryry2222;

    public static DisplayAddressSpace AddressSpaceFor(SurfacePixelFormat format) => format switch
    {
        SurfacePixelFormat.R2Packed2x2 => DisplayAddressSpace.Emitters,
        SurfacePixelFormat.Rrrr2222 or SurfacePixelFormat.Ryry2222 => DisplayAddressSpace.Cells,
        _ => DisplayAddressSpace.Pixels
    };

    public static DisplayStorageBlock StorageBlockFor(SurfacePixelFormat format) => format switch
    {
        SurfacePixelFormat.R2Packed2x2 => new() { Width = 2, Height = 2, Bytes = 1 },
        SurfacePixelFormat.Rrrr2222 or SurfacePixelFormat.Ryry2222 => new() { Width = 1, Height = 1, Bytes = 1 },
        SurfacePixelFormat.Rgb565 => new() { Width = 1, Height = 1, Bytes = 2 },
        SurfacePixelFormat.Rgb888 => new() { Width = 1, Height = 1, Bytes = 3 },
        SurfacePixelFormat.Xrgb8888 or SurfacePixelFormat.Argb8888 => new() { Width = 1, Height = 1, Bytes = 4 },
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static DisplayChannelModel ChannelModelFor(SurfacePixelFormat format) => format switch
    {
        SurfacePixelFormat.R2Packed2x2 or SurfacePixelFormat.Rrrr2222 => DisplayChannelModel.Red,
        SurfacePixelFormat.Ryry2222 => DisplayChannelModel.RedYellow,
        _ => DisplayChannelModel.Rgb
    };

    public static DisplayComposition CompositionFor(SurfacePixelFormat format) => new()
    {
        SourceAlpha = true,
        Coverage = true,
        SourceOver = true,
        DestinationAlpha = format == SurfacePixelFormat.Argb8888
    };

    public static DisplayField[] FieldsFor(SurfacePixelFormat format) => format switch
    {
        SurfacePixelFormat.Rgb565 => [Field(DisplayFieldChannel.Red, 11, 5),
            Field(DisplayFieldChannel.Green, 5, 6), Field(DisplayFieldChannel.Blue, 0, 5)],
        SurfacePixelFormat.Rgb888 => [Field(DisplayFieldChannel.Red, 0, 8),
            Field(DisplayFieldChannel.Green, 8, 8), Field(DisplayFieldChannel.Blue, 16, 8)],
        SurfacePixelFormat.Xrgb8888 or SurfacePixelFormat.Argb8888 => [Field(DisplayFieldChannel.Red, 16, 8),
            Field(DisplayFieldChannel.Green, 8, 8), Field(DisplayFieldChannel.Blue, 0, 8),
            Field(format == SurfacePixelFormat.Argb8888 ? DisplayFieldChannel.Alpha : DisplayFieldChannel.Unused, 24, 8)],
        SurfacePixelFormat.R2Packed2x2 or SurfacePixelFormat.Rrrr2222 or SurfacePixelFormat.Ryry2222 =>
            [Field(format == SurfacePixelFormat.Ryry2222 ? DisplayFieldChannel.Yellow : DisplayFieldChannel.Red, 2, 2),
             Field(DisplayFieldChannel.Red, 6, 2, 1, 0), Field(DisplayFieldChannel.Red, 0, 2, 0, 1),
             Field(format == SurfacePixelFormat.Ryry2222 ? DisplayFieldChannel.Yellow : DisplayFieldChannel.Red, 4, 2, 1, 1)],
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static DisplayField Field(DisplayFieldChannel channel, int bitOffset, int bitWidth, int x = 0, int y = 0) => new()
    {
        Channel = channel, BitOffset = bitOffset, BitWidth = bitWidth,
        Position = new DisplayFieldPosition { X = x, Y = y }
    };

    private static void ValidateGeometry(DisplayDefinitionGeometry geometry, List<ProfileDiagnostic> diagnostics)
    {
        if (geometry.Width is <= 0 or > 16384 || geometry.Height is <= 0 or > 16384)
        {
            Error(diagnostics, "LVDS2002", "$.geometry", "Logical extents must be positive and fit host profile limits.");
            return;
        }
        if (!Enum.IsDefined(geometry.Shape) ||
            geometry.Shape is SurfaceShape.Circle or SurfaceShape.Square && geometry.Width != geometry.Height)
        {
            Error(diagnostics, "LVDS2003", "$.geometry.shape", "Square and circular shapes require equal dimensions.");
        }
        if (geometry.RotationDegrees != 0)
        {
            Error(diagnostics, "LVDD1003", "$.geometry.rotationDegrees", "Compiled display definitions currently require zero rotation.");
        }
        SafeAreaProfile area = geometry.SafeArea;
        if (area.X < 0 || area.Y < 0 || area.Width <= 0 || area.Height <= 0 ||
            (long)area.X + area.Width > geometry.Width || (long)area.Y + area.Height > geometry.Height)
        {
            Error(diagnostics, "LVDS2004", "$.geometry.safeArea", "Safe area must be a positive rectangle contained in the viewport.");
            return;
        }
        if (geometry.Shape == SurfaceShape.Circle &&
            (!InsideCircle(area.X, area.Y, geometry.Width) ||
             !InsideCircle(area.X + area.Width - 1, area.Y, geometry.Width) ||
             !InsideCircle(area.X, area.Y + area.Height - 1, geometry.Width) ||
             !InsideCircle(area.X + area.Width - 1, area.Y + area.Height - 1, geometry.Width)))
        {
            Error(diagnostics, "LVDS2004", "$.geometry.safeArea", "Safe-area corner pixel centers must lie inside the circle.");
        }
    }

    private static bool InsideCircle(int x, int y, int diameter)
    {
        long dx = 2L * x + 1 - diameter;
        long dy = 2L * y + 1 - diameter;
        return dx * dx + dy * dy <= (long)diameter * diameter;
    }

    private static void Error(List<ProfileDiagnostic> diagnostics, string code, string path, string message) =>
        DisplayDefinitionJson.Error(diagnostics, code, path, message);
}
