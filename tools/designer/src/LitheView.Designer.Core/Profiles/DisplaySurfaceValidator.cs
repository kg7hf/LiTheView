// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public static class DisplaySurfaceValidator
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumPixelDimension = 16_384;
    private const decimal MaximumPhysicalDimensionMillimeters = 2_000M;

    public static IReadOnlyList<ProfileDiagnostic> Validate(DisplaySurfaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        List<ProfileDiagnostic> diagnostics = [];
        ValidateSchema(profile, diagnostics);
        ValidateIdentity(profile, diagnostics);
        ValidatePixelSize(profile, diagnostics);
        ValidatePhysicalSize(profile, diagnostics);
        ValidateCoordinateSystem(profile, diagnostics);
        ValidatePainterSurface(profile, diagnostics);
        ValidateNativeDisplay(profile, diagnostics);
        ValidateSafeArea(profile, diagnostics);
        ValidateInputs(profile, diagnostics);
        ValidateProvenance(profile, diagnostics);
        return diagnostics;
    }

    private static void ValidateSchema(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        if (profile.SchemaVersion != CurrentSchemaVersion)
        {
            AddError(
                diagnostics,
                "LVDS1001",
                "$.schemaVersion",
                $"Schema version {profile.SchemaVersion} is not supported; expected {CurrentSchemaVersion}.");
        }
        if (string.IsNullOrWhiteSpace(profile.Schema))
        {
            AddError(diagnostics, "LVDS1002", "$.$schema", "The schema reference must not be empty.");
        }
    }

    private static void ValidateIdentity(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        if (profile.Id.Length > ProfileRules.MaximumIdentifierLength)
        {
            AddError(
                diagnostics,
                "LVDS1102",
                "$.id",
                $"The profile ID must not exceed {ProfileRules.MaximumIdentifierLength} characters.");
        }
        else if (!ProfileRules.IsValidIdentifier(profile.Id))
        {
            AddError(
                diagnostics,
                "LVDS1101",
                "$.id",
                "The profile ID must contain lowercase letters, digits, dots, or hyphens, with no " +
                    "leading, trailing, or consecutive separators.");
        }
        if (string.IsNullOrWhiteSpace(profile.DisplayName) ||
            profile.DisplayName.Length > ProfileRules.MaximumDisplayNameLength)
        {
            AddError(
                diagnostics,
                "LVDS1103",
                "$.displayName",
                $"The display name must contain 1 through " +
                    $"{ProfileRules.MaximumDisplayNameLength} characters.");
        }
        if (!ProfileRules.IsValidIdentifier(profile.CompatibilityFamily))
        {
            AddError(
                diagnostics,
                "LVDS1104",
                "$.compatibilityFamily",
                "The compatibility family must be a lowercase dotted or hyphenated identifier.");
        }
    }

    private static void ValidatePixelSize(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        ValidatePixelDimension(profile.PixelSize.Width, "$.pixelSize.width", diagnostics);
        ValidatePixelDimension(profile.PixelSize.Height, "$.pixelSize.height", diagnostics);

        if ((profile.Shape == SurfaceShape.Square || profile.Shape == SurfaceShape.Circle) &&
            profile.PixelSize.Width != profile.PixelSize.Height)
        {
            AddError(
                diagnostics,
                "LVDS1202",
                "$.pixelSize",
                "Square and circular surfaces require equal pixel width and height.");
        }
    }

    private static void ValidatePhysicalSize(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        PhysicalSize size = profile.PhysicalSize;
        bool hasWidth = size.WidthMillimeters.HasValue;
        bool hasHeight = size.HeightMillimeters.HasValue;
        bool hasDiameter = size.DiameterMillimeters.HasValue;

        if (profile.Shape == SurfaceShape.Circle)
        {
            if (!hasDiameter || hasWidth || hasHeight)
            {
                AddError(
                    diagnostics,
                    "LVDS1301",
                    "$.physicalSize",
                    "A circular surface requires only diameterMillimeters.");
            }
            ValidatePhysicalDimension(size.DiameterMillimeters, "$.physicalSize.diameterMillimeters", diagnostics);
            return;
        }

        if (!hasWidth || !hasHeight || hasDiameter)
        {
            AddError(
                diagnostics,
                "LVDS1302",
                "$.physicalSize",
                "A rectangular or square surface requires widthMillimeters and heightMillimeters only.");
        }
        ValidatePhysicalDimension(size.WidthMillimeters, "$.physicalSize.widthMillimeters", diagnostics);
        ValidatePhysicalDimension(size.HeightMillimeters, "$.physicalSize.heightMillimeters", diagnostics);

        if (profile.Shape == SurfaceShape.Square && hasWidth && hasHeight &&
            size.WidthMillimeters != size.HeightMillimeters)
        {
            AddError(
                diagnostics,
                "LVDS1303",
                "$.physicalSize",
                "A square surface requires equal physical width and height.");
        }
    }

    private static void ValidateCoordinateSystem(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        int rotation = profile.CoordinateSystem.RotationDegrees;
        if (rotation is not (0 or 90 or 180 or 270))
        {
            AddError(
                diagnostics,
                "LVDS1401",
                "$.coordinateSystem.rotationDegrees",
                "Rotation must be 0, 90, 180, or 270 degrees.");
        }
    }

    private static void ValidatePainterSurface(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        (int expectedColorDepth, int expectedStorageDepth) = profile.PainterSurface.Format switch
        {
            SurfacePixelFormat.Rgb565 => (16, 16),
            SurfacePixelFormat.Rgb888 => (24, 24),
            SurfacePixelFormat.Xrgb8888 => (24, 32),
            SurfacePixelFormat.Argb8888 => (32, 32),
            _ => (0, 0)
        };
        if (expectedColorDepth == 0 ||
            profile.PainterSurface.ColorDepthBits != expectedColorDepth ||
            profile.PainterSurface.StorageBitsPerPixel != expectedStorageDepth)
        {
            AddError(
                diagnostics,
                "LVDS1501",
                "$.painterSurface",
                $"{profile.PainterSurface.Format} requires colorDepthBits={expectedColorDepth} and " +
                    $"storageBitsPerPixel={expectedStorageDepth}.");
        }
    }

    private static void ValidateNativeDisplay(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        NativeDisplayProfile native = profile.NativeDisplay;
        if (native.ColorDepthBits is < 1 or > 32)
        {
            AddError(
                diagnostics,
                "LVDS1601",
                "$.nativeDisplay.colorDepthBits",
                "Native color depth must contain 1 through 32 bits.");
        }

        bool usesPackedStorage = native.TransferFormat == NativeTransferFormat.Packed8;
        bool usesPackedColor = native.ColorModel is
            NativeColorModel.RedLuminanceSubpixels or NativeColorModel.RedYellowFullPixel;
        if (usesPackedStorage != usesPackedColor)
        {
            AddError(
                diagnostics,
                "LVDS1602",
                "$.nativeDisplay",
                "synthetic packed display color models and packed8 transfer format must be selected together.");
        }

        int expectedColorDepth = (native.ColorModel, native.TransferFormat) switch
        {
            (NativeColorModel.Rgb, NativeTransferFormat.Rgb565) => 16,
            (NativeColorModel.Rgb, NativeTransferFormat.Rgb888Parallel) => 24,
            (NativeColorModel.Rgb, NativeTransferFormat.Argb8888) => 32,
            (NativeColorModel.RedLuminanceSubpixels, NativeTransferFormat.Packed8) => 2,
            (NativeColorModel.RedYellowFullPixel, NativeTransferFormat.Packed8) => 4,
            _ => 0
        };
        if (native.ColorDepthBits != expectedColorDepth)
        {
            AddError(
                diagnostics,
                "LVDS1603",
                "$.nativeDisplay",
                $"{native.TransferFormat} requires colorDepthBits={expectedColorDepth}.");
        }
        int expectedTransferUnitBits = native.TransferFormat switch
        {
            NativeTransferFormat.Rgb565 => 16,
            NativeTransferFormat.Rgb888Parallel => 24,
            NativeTransferFormat.Argb8888 => 32,
            NativeTransferFormat.Packed8 => 8,
            _ => 0
        };
        if (native.TransferUnitBits != expectedTransferUnitBits)
        {
            AddError(
                diagnostics,
                "LVDS1604",
                "$.nativeDisplay.transferUnitBits",
                $"{native.TransferFormat} requires transferUnitBits={expectedTransferUnitBits}.");
        }
    }

    private static void ValidateSafeArea(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        SafeAreaProfile safe = profile.SafeArea;
        bool positive = safe.X >= 0 && safe.Y >= 0 && safe.Width > 0 && safe.Height > 0;
        bool inside = (long)safe.X + safe.Width <= profile.PixelSize.Width &&
            (long)safe.Y + safe.Height <= profile.PixelSize.Height;
        if (!positive || !inside)
        {
            AddError(
                diagnostics,
                "LVDS1701",
                "$.safeArea",
                "The safe area must be positive and fully contained by the pixel surface.");
        }
    }

    private static void ValidateInputs(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<InputCapability> seen = [];
        foreach (InputCapability capability in profile.InputCapabilities)
        {
            if (!seen.Add(capability))
            {
                AddError(
                    diagnostics,
                    "LVDS1801",
                    "$.inputCapabilities",
                    $"Input capability '{capability}' is duplicated.");
            }
        }
    }

    private static void ValidateProvenance(
        DisplaySurfaceProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(profile.Provenance.Manufacturer) ||
            string.IsNullOrWhiteSpace(profile.Provenance.Model))
        {
            AddError(
                diagnostics,
                "LVDS1901",
                "$.provenance",
                "Manufacturer and model provenance are required.");
        }
        if (!Uri.TryCreate(profile.Provenance.SourceUrl, UriKind.Absolute, out Uri? source) ||
            source.Scheme != Uri.UriSchemeHttps)
        {
            AddError(
                diagnostics,
                "LVDS1902",
                "$.provenance.sourceUrl",
                "The provenance source must be an absolute HTTPS URL.");
        }
        if (string.IsNullOrWhiteSpace(profile.Provenance.SourceDocument) ||
            profile.Provenance.SourceDocument.Length > 240)
        {
            AddError(
                diagnostics,
                "LVDS1903",
                "$.provenance.sourceDocument",
                "The source-document identity must contain 1 through 240 characters.");
        }
        if (profile.Provenance.SourceSha256 is not null &&
            !ProfileRules.IsLowercaseSha256(profile.Provenance.SourceSha256))
        {
            AddError(
                diagnostics,
                "LVDS1904",
                "$.provenance.sourceSha256",
                "A source-document hash must contain 64 lowercase hexadecimal characters.");
        }
    }

    private static void ValidatePixelDimension(
        int value,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (value is < 1 or > MaximumPixelDimension)
        {
            AddError(
                diagnostics,
                "LVDS1201",
                path,
                $"A pixel dimension must contain 1 through {MaximumPixelDimension} pixels.");
        }
    }

    private static void ValidatePhysicalDimension(
        decimal? value,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!value.HasValue || value.Value <= 0M || value.Value > MaximumPhysicalDimensionMillimeters)
        {
            AddError(
                diagnostics,
                "LVDS1304",
                path,
                $"A physical dimension must be greater than zero and no more than " +
                    $"{MaximumPhysicalDimensionMillimeters} millimeters.");
        }
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }
}
