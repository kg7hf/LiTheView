// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public static class ResourceManifestValidator
{
    private static readonly SearchValues<char> sLowerHexadecimal =
        SearchValues.Create("0123456789abcdef");
    private const int CurrentSchemaVersion = 1;
    private const int MaximumVariantCount = 64;
    private const int MaximumPainterFormatCount = 8;
    private const int MaximumOptionCount = 64;
    private const int MaximumTextLength = 1024;

    public static IReadOnlyList<ProfileDiagnostic> Validate(ResourceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<ProfileDiagnostic> diagnostics = [];
        ValidateIdentity(manifest, diagnostics);
        ValidateSource(manifest.Source, manifest.Compliance, diagnostics);
        ValidateVariants(manifest, diagnostics);
        return diagnostics;
    }

    private static void ValidateIdentity(
        ResourceManifest manifest,
        List<ProfileDiagnostic> diagnostics)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            AddError(
                diagnostics,
                "LVRM1001",
                "$.schemaVersion",
                $"Schema version {manifest.SchemaVersion} is not supported; expected {CurrentSchemaVersion}.");
        }
        if (string.IsNullOrWhiteSpace(manifest.Schema))
        {
            AddError(diagnostics, "LVRM1002", "$.$schema", "The schema reference must not be empty.");
        }
        if (!ProfileRules.IsValidIdentifier(manifest.Id))
        {
            AddError(
                diagnostics,
                "LVRM1101",
                "$.id",
                "The resource ID must be a bounded lowercase dotted or hyphenated identifier.");
        }
        if (!IsBoundedText(manifest.DisplayName, ProfileRules.MaximumDisplayNameLength))
        {
            AddError(
                diagnostics,
                "LVRM1102",
                "$.displayName",
                $"The display name must contain 1 through {ProfileRules.MaximumDisplayNameLength} characters.");
        }
        if (!Enum.IsDefined(manifest.Kind))
        {
            AddError(diagnostics, "LVRM1103", "$.kind", "The resource kind is not supported.");
        }
    }

    private static void ValidateSource(
        ResourceSource source,
        ResourceCompliancePolicy compliance,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!ProfileRules.IsSafeProjectPath(source.Path))
        {
            AddError(
                diagnostics,
                "LVRM1201",
                "$.source.path",
                "The source path must be relative, normalized, and contained by the project root.");
        }
        if (!ProfileRules.IsLowercaseSha256(source.Sha256))
        {
            AddError(
                diagnostics,
                "LVRM1202",
                "$.source.sha256",
                "The source lock must contain 64 lowercase hexadecimal characters.");
        }
        if (source.ByteCount < 0)
        {
            AddError(diagnostics, "LVRM1203", "$.source.byteCount", "The source byte count cannot be negative.");
        }
        if (!IsBoundedText(source.MediaType, 128))
        {
            AddError(diagnostics, "LVRM1204", "$.source.mediaType", "The media type must contain 1 through 128 characters.");
        }

        if (source.Provenance is null)
        {
            AddPolicyDiagnostic(
                diagnostics,
                compliance.MissingProvenance,
                "LVRM1205",
                "$.source.provenance",
                "Source provenance is missing.");
            AddPolicyDiagnostic(
                diagnostics,
                compliance.MissingLicense,
                "LVRM1206",
                "$.source.provenance.license",
                "Source license information is missing.");
            return;
        }

        ResourceProvenance provenance = source.Provenance;
        if (!IsBoundedText(provenance.Origin, MaximumTextLength))
        {
            AddPolicyDiagnostic(
                diagnostics,
                compliance.MissingProvenance,
                "LVRM1205",
                "$.source.provenance.origin",
                "Source provenance requires a bounded origin description.");
        }
        ValidateOptionalText(provenance.Author, "$.source.provenance.author", diagnostics);
        ValidateOptionalText(provenance.Notes, "$.source.provenance.notes", diagnostics);
        if (provenance.SourceUri is not null &&
            (!IsBoundedText(provenance.SourceUri, 2048) ||
             !Uri.TryCreate(provenance.SourceUri, UriKind.Absolute, out _)))
        {
            AddError(
                diagnostics,
                "LVRM1207",
                "$.source.provenance.sourceUri",
                "The source URI must be an absolute URI containing at most 2048 characters.");
        }
        ValidateLicense(provenance.License, compliance, diagnostics);
    }

    private static void ValidateLicense(
        ResourceLicense? license,
        ResourceCompliancePolicy compliance,
        List<ProfileDiagnostic> diagnostics)
    {
        if (license is null)
        {
            AddPolicyDiagnostic(
                diagnostics,
                compliance.MissingLicense,
                "LVRM1206",
                "$.source.provenance.license",
                "Source license information is missing.");
            return;
        }

        bool hasSpdx = IsBoundedText(license.SpdxId, 128);
        bool hasLicenseFile = license.LicenseTextPath is not null &&
            ProfileRules.IsSafeProjectPath(license.LicenseTextPath) &&
            ProfileRules.IsLowercaseSha256(license.LicenseTextSha256);
        if (!hasSpdx && !hasLicenseFile)
        {
            AddPolicyDiagnostic(
                diagnostics,
                compliance.MissingLicense,
                "LVRM1208",
                "$.source.provenance.license",
                "A license requires an SPDX identifier or a contained license-text path.");
        }
        else if (license.LicenseTextPath is not null && !hasLicenseFile)
        {
            AddError(
                diagnostics,
                "LVRM1209",
                "$.source.provenance.license.licenseTextPath",
                "The license-text path must be relative, normalized, and contained.");
        }
        if (license.LicenseTextPath is null != license.LicenseTextSha256 is null)
        {
            AddError(
                diagnostics,
                "LVRM1212",
                "$.source.provenance.license",
                "A license-text path and its lowercase SHA-256 lock must be supplied together.");
        }
        if (license.SpdxId is not null && !hasSpdx)
        {
            AddError(
                diagnostics,
                "LVRM1210",
                "$.source.provenance.license.spdxId",
                "The SPDX identifier must contain 1 through 128 characters.");
        }
        ValidateOptionalText(
            license.Attribution,
            "$.source.provenance.license.attribution",
            diagnostics);
        if (!license.RedistributionApproved)
        {
            AddPolicyDiagnostic(
                diagnostics,
                compliance.UnapprovedRedistribution,
                "LVRM1211",
                "$.source.provenance.license.redistributionApproved",
                "Resource redistribution has not been approved.");
        }
    }

    private static void ValidateVariants(
        ResourceManifest manifest,
        List<ProfileDiagnostic> diagnostics)
    {
        if (manifest.Variants.Length is < 1 or > MaximumVariantCount)
        {
            AddError(
                diagnostics,
                "LVRM1301",
                "$.variants",
                $"A resource requires 1 through {MaximumVariantCount} generated variants.");
            return;
        }

        HashSet<string> variantIds = new(StringComparer.Ordinal);
        HashSet<string> outputPaths = new(StringComparer.Ordinal);
        foreach (ResourceVariant variant in manifest.Variants)
        {
            string path = $"$.variants[{variant.Id}]";
            if (!ProfileRules.IsValidIdentifier(variant.Id) || !variantIds.Add(variant.Id))
            {
                AddError(
                    diagnostics,
                    "LVRM1302",
                    $"{path}.id",
                    $"Variant ID '{variant.Id}' is invalid or duplicated.");
            }
            ValidatePainterFormats(variant, path, diagnostics);
            ValidateConverter(variant.Converter, path, diagnostics);
            ValidateOutput(manifest.Kind, variant.Output, path, outputPaths, diagnostics);
            ValidateFont(manifest.Kind, variant, path, diagnostics);
            ValidateImage(manifest.Kind, variant, path, diagnostics);
            ValidateAnimation(manifest.Kind, variant, path, diagnostics);
            ValidateIntegration(variant.Integration, path, diagnostics);
        }
    }

    private static void ValidatePainterFormats(
        ResourceVariant variant,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (variant.CompatiblePainterFormats.Length is < 1 or > MaximumPainterFormatCount)
        {
            AddError(
                diagnostics,
                "LVRM1303",
                $"{path}.compatiblePainterFormats",
                $"A variant requires 1 through {MaximumPainterFormatCount} compatible painter formats.");
            return;
        }
        HashSet<SurfacePixelFormat> formats = [];
        foreach (SurfacePixelFormat format in variant.CompatiblePainterFormats)
        {
            if (!Enum.IsDefined(format) || !formats.Add(format))
            {
                AddError(
                    diagnostics,
                    "LVRM1304",
                    $"{path}.compatiblePainterFormats",
                    $"Painter format '{format}' is invalid or duplicated.");
            }
        }
    }

    private static void ValidateConverter(
        ResourceConverter converter,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!ProfileRules.IsValidIdentifier(converter.Id))
        {
            AddError(diagnostics, "LVRM1401", $"{path}.converter.id", "The converter ID is invalid.");
        }
        if (!IsBoundedText(converter.Version, 64))
        {
            AddError(
                diagnostics,
                "LVRM1402",
                $"{path}.converter.version",
                "The converter version must contain 1 through 64 characters.");
        }
        if (converter.Options.Count > MaximumOptionCount)
        {
            AddError(
                diagnostics,
                "LVRM1403",
                $"{path}.converter.options",
                $"A converter may define at most {MaximumOptionCount} options.");
        }
        foreach ((string key, string value) in converter.Options)
        {
            if (!IsOptionName(key) || value.Length > 256)
            {
                AddError(
                    diagnostics,
                    "LVRM1404",
                    $"{path}.converter.options.{key}",
                    "Option names and values must use the bounded canonical converter format.");
            }
        }
    }

    private static void ValidateOutput(
        ResourceKind kind,
        ResourceOutput output,
        string path,
        HashSet<string> outputPaths,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!ProfileRules.IsSafeProjectPath(output.Path) || !outputPaths.Add(output.Path))
        {
            AddError(
                diagnostics,
                "LVRM1501",
                $"{path}.output.path",
                "The output path must be contained and unique in the resource manifest.");
        }
        if (!ProfileRules.IsLowercaseSha256(output.Sha256))
        {
            AddError(diagnostics, "LVRM1502", $"{path}.output.sha256", "The output hash is invalid.");
        }
        if (output.ByteCount < 0)
        {
            AddError(diagnostics, "LVRM1503", $"{path}.output.byteCount", "The output byte count cannot be negative.");
        }
        if (output.PixelSize is not null &&
            (output.PixelSize.Width is < 1 or > 16384 || output.PixelSize.Height is < 1 or > 16384 ||
             !ImageConversionRequestValidator.IsAlignedExtent(
                 output.Format, output.PixelSize.Width, output.PixelSize.Height)))
        {
            AddError(
                diagnostics,
                "LVRM1504",
                $"{path}.output.pixelSize",
                "Output pixel dimensions must be between 1 and 16384 and native R2 requires complete 2-by-2 blocks.");
        }
        bool frameCountValid = kind == ResourceKind.Animation
            ? output.FrameCount is >= 2 and <= 65535
            : output.FrameCount == 1;
        if (!frameCountValid)
        {
            AddError(
                diagnostics,
                "LVRM1505",
                $"{path}.output.frameCount",
                "Animations require 2 through 65535 frames; other resource kinds require exactly one.");
        }
        if (kind == ResourceKind.Font && output.Format is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8)
        {
            AddError(
                diagnostics,
                "LVRM1506",
                $"{path}.output.format",
                "Font variants must use alpha1 or alpha8 output.");
        }
        if (kind == ResourceKind.Animation && output.Format == ResourceFormat.R2Packed2x2)
        {
            AddError(diagnostics, "LVRM1507", $"{path}.output.format",
                "Native R2 resource conversion currently supports images and icons only.");
        }
    }

    private static void ValidateFont(
        ResourceKind kind,
        ResourceVariant variant,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        FontVariantMetadata? font = variant.Font;
        if (kind == ResourceKind.Font && font is null)
        {
            AddError(
                diagnostics,
                "LVRM1701",
                $"{path}.font",
                "A font resource variant requires explicit face, repertoire, and measured payload metadata.");
            return;
        }
        if (kind != ResourceKind.Font && font is not null)
        {
            AddError(
                diagnostics,
                "LVRM1702",
                $"{path}.font",
                "Only font resources may contain font-variant metadata.");
            return;
        }
        if (font is null)
        {
            return;
        }

        if (!IsBoundedText(font.Family, ProfileRules.MaximumDisplayNameLength) ||
            !IsBoundedText(font.Style, ProfileRules.MaximumDisplayNameLength))
        {
            AddError(
                diagnostics,
                "LVRM1703",
                $"{path}.font",
                "Font family and style must each contain 1 through 160 characters.");
        }
        if (font.Weight is < 1 or > 1000 || font.PixelSize is < 4 or > 64)
        {
            AddError(
                diagnostics,
                "LVRM1704",
                $"{path}.font",
                "Font weight must be 1 through 1000 and pixel size must be 4 through 64.");
        }
        if (font.Coverage is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8 ||
            font.Coverage != variant.Output.Format)
        {
            AddError(
                diagnostics,
                "LVRM1705",
                $"{path}.font.coverage",
                "Font coverage must be alpha1 or alpha8 and must match the primary output format.");
        }
        if (!FontRepertoireParser.TryParse(font.Repertoire, out FontRepertoire? repertoire, out _) ||
            repertoire?.Canonical != font.Repertoire ||
            !string.Equals(font.Fallback, "U+003F", StringComparison.Ordinal))
        {
            AddError(
                diagnostics,
                "LVRM1706",
                $"{path}.font.repertoire",
                "The repertoire must be canonical and must use U+003F as its included fallback.");
        }
        if (font.GlyphCount is < 1 or > ushort.MaxValue ||
            repertoire is not null && font.GlyphCount != repertoire.Codepoints.Length)
        {
            AddError(
                diagnostics,
                "LVRM1707",
                $"{path}.font.glyphCount",
                "The measured glyph count must match the selected repertoire.");
        }
        long expectedRecordBytes = (long)font.GlyphCount * 16L;
        if (font.CoverageBytes < 0 ||
            font.RecordBytes != expectedRecordBytes ||
            font.EstimatedRomBytes != font.CoverageBytes + font.RecordBytes)
        {
            AddError(
                diagnostics,
                "LVRM1708",
                $"{path}.font",
                "Coverage, fixed-record, and estimated ROM byte measurements are inconsistent.");
        }
        if (!IsFnv1a32(font.DataChecksumFnv1a32))
        {
            AddError(
                diagnostics,
                "LVRM1709",
                $"{path}.font.dataChecksumFnv1a32",
                "The generated coverage checksum must be lowercase 0x followed by eight hexadecimal digits.");
        }
        if (font.LineHeight is < 1 or > byte.MaxValue ||
            font.Baseline < 0 || font.Baseline >= font.LineHeight ||
            font.UnderlinePosition < 0 || font.UnderlinePosition >= font.LineHeight ||
            font.UnderlineThickness < 1 ||
            font.UnderlineThickness > font.LineHeight - font.UnderlinePosition)
        {
            AddError(
                diagnostics,
                "LVRM1710",
                $"{path}.font",
                "Font line, baseline, and underline metrics do not fit the bounded line height.");
        }
        if (variant.Output.PixelSize is not null)
        {
            AddError(
                diagnostics,
                "LVRM1711",
                $"{path}.output.pixelSize",
                "A proportional font has per-glyph rectangles; its primary output pixel size must be null.");
        }
        ValidateFontConverterOptions(variant, font, path, diagnostics);
    }

    private static void ValidateFontConverterOptions(
        ResourceVariant variant,
        FontVariantMetadata font,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["codepoints"] = font.Repertoire,
            ["coverage"] = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(font.Coverage),
            ["pixelSize"] = font.PixelSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["style"] = font.Style,
            ["weight"] = font.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach ((string name, string value) in expected)
        {
            if (!variant.Converter.Options.TryGetValue(name, out string? actual) || actual != value)
            {
                AddError(
                    diagnostics,
                    "LVRM1712",
                    $"{path}.converter.options.{name}",
                    "Font converter options must exactly reproduce the checked font metadata.");
            }
        }
    }

    private static void ValidateImage(
        ResourceKind kind,
        ResourceVariant variant,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        ImageVariantMetadata? image = variant.Image;
        bool imageKind = kind is ResourceKind.Image or ResourceKind.Icon;
        if (imageKind && image is null)
        {
            AddError(
                diagnostics,
                "LVRM1720",
                $"{path}.image",
                "An image or icon variant requires explicit crop, representation, and measured payload metadata.");
            return;
        }
        if (!imageKind && image is not null)
        {
            AddError(
                diagnostics,
                "LVRM1721",
                $"{path}.image",
                "Only image and icon resources may contain image-variant metadata.");
            return;
        }
        if (image is null)
        {
            return;
        }

        PixelSize source = image.SourcePixelSize;
        ResourceCropRectangle crop = image.Crop;
        if (source.Width is < 1 or > 16384 || source.Height is < 1 or > 16384 ||
            crop.X < 0 || crop.Y < 0 || crop.Width < 1 || crop.Height < 1 ||
            crop.X > source.Width - crop.Width || crop.Y > source.Height - crop.Height)
        {
            AddError(
                diagnostics,
                "LVRM1722",
                $"{path}.image.crop",
                "The crop rectangle must fit the bounded source pixel dimensions.");
        }
        if (!Enum.IsDefined(image.ScaleMode) || !Enum.IsDefined(image.AlphaMode) ||
            !Enum.IsDefined(image.Dither) || image.AlphaThreshold is < 0 or > byte.MaxValue ||
            image.Dither != ImageDitherMode.None &&
                variant.Output.Format is not ResourceFormat.Indexed4 and not ResourceFormat.Rgb565)
        {
            AddError(
                diagnostics,
                "LVRM1723",
                $"{path}.image",
                "Image scale, alpha, threshold, or dither metadata is invalid.");
        }
        bool paletteValid = ImageColorText.TryCanonicalizePalette(
            image.PaletteArgb,
            out string canonicalPalette,
            out int paletteCount);
        if (!ImageColorText.IsCanonicalArgb(image.TintArgb) ||
            !ImageColorText.IsCanonicalArgb(image.MatteArgb) ||
            !paletteValid ||
            canonicalPalette != image.PaletteArgb ||
            variant.Output.Format == ResourceFormat.Indexed4 && paletteCount is < 1 or > 16 ||
            variant.Output.Format != ResourceFormat.Indexed4 && paletteCount != 0)
        {
            AddError(
                diagnostics,
                "LVRM1724",
                $"{path}.image",
                "Image tint, matte, or indexed palette is not canonical for the selected format.");
        }
        if (variant.Output.PixelSize is not PixelSize output ||
            !ImageConversionRequestValidator.IsBitmapFormat(variant.Output.Format))
        {
            AddError(
                diagnostics,
                "LVRM1725",
                $"{path}.output",
                "Image and icon outputs require pixel dimensions and a supported Bitmap format.");
            return;
        }

        int expectedStride;
        long expectedPixelBytes;
        try
        {
            expectedStride = ImageConversionRequestValidator.MinimumStride(
                variant.Output.Format,
                output.Width);
            expectedPixelBytes = checked((long)expectedStride *
                ImageConversionRequestValidator.StorageRowCount(variant.Output.Format, output.Height));
        }
        catch (OverflowException)
        {
            expectedStride = 0;
            expectedPixelBytes = 0;
        }
        long expectedPaletteBytes = (long)paletteCount * 4L;
        if (image.StrideBytes != expectedStride || image.PixelBytes != expectedPixelBytes ||
            image.PaletteBytes != expectedPaletteBytes ||
            image.EstimatedRomBytes != image.PixelBytes + image.PaletteBytes)
        {
            AddError(
                diagnostics,
                "LVRM1726",
                $"{path}.image",
                "Image stride, pixel, palette, and estimated ROM measurements are inconsistent.");
        }
        if (!IsFnv1a32(image.PixelChecksumFnv1a32) ||
            !IsFnv1a32(image.PaletteChecksumFnv1a32) ||
            !IsFnv1a32(image.DataChecksumFnv1a32))
        {
            AddError(
                diagnostics,
                "LVRM1727",
                $"{path}.image",
                "Image payload checksums must be lowercase FNV-1a values.");
        }
        if (image.Tintable &&
            (kind != ResourceKind.Icon ||
             variant.Output.Format is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8))
        {
            AddError(
                diagnostics,
                "LVRM1728",
                $"{path}.image.tintable",
                "Only alpha1 or alpha8 icon masks can be tintable.");
        }
        ValidateImageConverterOptions(variant, image, kind, output, path, diagnostics);
    }

    private static void ValidateImageConverterOptions(
        ResourceVariant variant,
        ImageVariantMetadata image,
        ResourceKind kind,
        PixelSize output,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["alphaMode"] = image.AlphaMode.ToString(),
            ["alphaThreshold"] = image.AlphaThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cropHeight"] = image.Crop.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cropWidth"] = image.Crop.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cropX"] = image.Crop.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cropY"] = image.Crop.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["dither"] = image.Dither.ToString(),
            ["format"] = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(variant.Output.Format),
            ["height"] = output.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["kind"] = StrictCamelCaseEnumConverter<ResourceKind>.ToJsonName(kind),
            ["matteArgb"] = image.MatteArgb,
            ["paletteArgb"] = image.PaletteArgb,
            ["scaleMode"] = image.ScaleMode.ToString(),
            ["tintArgb"] = image.TintArgb,
            ["tintable"] = image.Tintable ? "true" : "false",
            ["width"] = output.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach ((string name, string value) in expected)
        {
            if (!variant.Converter.Options.TryGetValue(name, out string? actual) || actual != value)
            {
                AddError(
                    diagnostics,
                    "LVRM1729",
                    $"{path}.converter.options.{name}",
                    "Image converter options must exactly reproduce the checked image metadata.");
            }
        }
    }

    private static void ValidateAnimation(
        ResourceKind kind,
        ResourceVariant variant,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        AnimationVariantMetadata? animation = variant.Animation;
        if (kind == ResourceKind.Animation && animation is null)
        {
            AddError(
                diagnostics,
                "LVRM1740",
                $"{path}.animation",
                "An animation variant requires explicit grid, timing, representation, and payload metadata.");
            return;
        }
        if (kind != ResourceKind.Animation && animation is not null)
        {
            AddError(
                diagnostics,
                "LVRM1741",
                $"{path}.animation",
                "Only animation resources may contain animation-variant metadata.");
            return;
        }
        if (animation is null)
        {
            return;
        }

        PixelSize source = animation.SourcePixelSize;
        ResourceCropRectangle grid = animation.SourceGrid;
        PixelSize sourceFrame = animation.SourceFramePixelSize;
        int frameCount = variant.Output.FrameCount;
        int expectedRows = animation.Columns > 0
            ? (frameCount + animation.Columns - 1) / animation.Columns
            : 0;
        int expectedGridWidth = 0;
        int expectedGridHeight = 0;
        try
        {
            expectedGridWidth = checked(animation.Columns * sourceFrame.Width);
            expectedGridHeight = checked(expectedRows * sourceFrame.Height);
        }
        catch (OverflowException)
        {
            // Invalid dimensions are reported by the single bounded-grid diagnostic below.
        }
        if (source.Width is < 1 or > 16384 || source.Height is < 1 or > 16384 ||
            sourceFrame.Width is < 1 or > 16384 || sourceFrame.Height is < 1 or > 16384 ||
            frameCount is < 2 or > 256 || animation.Columns is < 1 or > 256 ||
            animation.Columns > frameCount ||
            grid.X < 0 || grid.Y < 0 || grid.Width != expectedGridWidth ||
            grid.Height != expectedGridHeight || grid.X > source.Width - grid.Width ||
            grid.Y > source.Height - grid.Height)
        {
            AddError(
                diagnostics,
                "LVRM1742",
                $"{path}.animation.sourceGrid",
                "The bounded row-major frame grid must exactly fit the declared source image and frame count.");
        }
        if (!Enum.IsDefined(animation.ScaleMode) || !Enum.IsDefined(animation.AlphaMode) ||
            !Enum.IsDefined(animation.Dither) || animation.AlphaThreshold is < 0 or > byte.MaxValue ||
            animation.Dither != ImageDitherMode.None &&
                variant.Output.Format is not ResourceFormat.Indexed4 and not ResourceFormat.Rgb565)
        {
            AddError(
                diagnostics,
                "LVRM1743",
                $"{path}.animation",
                "Animation scale, alpha, threshold, or dither metadata is invalid.");
        }
        bool paletteValid = ImageColorText.TryCanonicalizePalette(
            animation.PaletteArgb,
            out string canonicalPalette,
            out int paletteCount);
        if (!ImageColorText.IsCanonicalArgb(animation.TintArgb) ||
            !ImageColorText.IsCanonicalArgb(animation.MatteArgb) ||
            !paletteValid || canonicalPalette != animation.PaletteArgb ||
            variant.Output.Format == ResourceFormat.Indexed4 && paletteCount is < 1 or > 16 ||
            variant.Output.Format != ResourceFormat.Indexed4 && paletteCount != 0)
        {
            AddError(
                diagnostics,
                "LVRM1744",
                $"{path}.animation",
                "Animation tint, matte, or indexed palette is not canonical for the selected format.");
        }
        if (variant.Output.PixelSize is not PixelSize output ||
            !ImageConversionRequestValidator.IsBitmapFormat(variant.Output.Format))
        {
            AddError(
                diagnostics,
                "LVRM1745",
                $"{path}.output",
                "Animation outputs require per-frame pixel dimensions and a supported Bitmap format.");
            return;
        }

        int expectedStride;
        long expectedFrameBytes;
        long expectedPixelBytes;
        try
        {
            expectedStride = ImageConversionRequestValidator.MinimumStride(
                variant.Output.Format,
                output.Width);
            expectedFrameBytes = checked((long)expectedStride *
                ImageConversionRequestValidator.StorageRowCount(variant.Output.Format, output.Height));
            expectedPixelBytes = checked(expectedFrameBytes * frameCount);
        }
        catch (OverflowException)
        {
            expectedStride = 0;
            expectedFrameBytes = 0;
            expectedPixelBytes = 0;
        }
        long expectedPaletteBytes = (long)paletteCount * 4L;
        if (animation.StrideBytes != expectedStride ||
            animation.FramePixelBytes != expectedFrameBytes ||
            animation.PixelBytes != expectedPixelBytes ||
            animation.PaletteBytes != expectedPaletteBytes ||
            animation.EstimatedRomBytes != animation.PixelBytes + animation.PaletteBytes)
        {
            AddError(
                diagnostics,
                "LVRM1746",
                $"{path}.animation",
                "Animation stride, per-frame, total-pixel, palette, and ROM measurements are inconsistent.");
        }
        if (!IsFnv1a32(animation.PixelChecksumFnv1a32) ||
            !IsFnv1a32(animation.PaletteChecksumFnv1a32) ||
            !IsFnv1a32(animation.DataChecksumFnv1a32))
        {
            AddError(
                diagnostics,
                "LVRM1747",
                $"{path}.animation",
                "Animation payload checksums must be lowercase FNV-1a values.");
        }
        if (animation.FrameIntervalMilliseconds is < 1 or > ushort.MaxValue ||
            animation.RepeatIndex < 0 || animation.RepeatIndex >= frameCount)
        {
            AddError(
                diagnostics,
                "LVRM1748",
                $"{path}.animation",
                "Animation timing must be bounded and the repeat index must identify a frame.");
        }
        if (animation.Tintable &&
            variant.Output.Format is not ResourceFormat.Alpha1 and not ResourceFormat.Alpha8)
        {
            AddError(
                diagnostics,
                "LVRM1749",
                $"{path}.animation.tintable",
                "Tintable animations require alpha1 or alpha8 coverage frames.");
        }
        ValidateAnimationConverterOptions(variant, animation, output, path, diagnostics);
    }

    private static void ValidateAnimationConverterOptions(
        ResourceVariant variant,
        AnimationVariantMetadata animation,
        PixelSize output,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["alphaMode"] = animation.AlphaMode.ToString(),
            ["alphaThreshold"] = animation.AlphaThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["autoplay"] = animation.Autoplay ? "true" : "false",
            ["columns"] = animation.Columns.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["dither"] = animation.Dither.ToString(),
            ["format"] = StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName(variant.Output.Format),
            ["frameCount"] = variant.Output.FrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["frameIntervalMilliseconds"] = animation.FrameIntervalMilliseconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["height"] = output.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["kind"] = "animation",
            ["matteArgb"] = animation.MatteArgb,
            ["paletteArgb"] = animation.PaletteArgb,
            ["repeatIndex"] = animation.RepeatIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["scaleMode"] = animation.ScaleMode.ToString(),
            ["sheetX"] = animation.SourceGrid.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sheetY"] = animation.SourceGrid.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sourceFrameHeight"] = animation.SourceFramePixelSize.Height.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["sourceFrameWidth"] = animation.SourceFramePixelSize.Width.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["tintArgb"] = animation.TintArgb,
            ["tintable"] = animation.Tintable ? "true" : "false",
            ["width"] = output.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach ((string name, string value) in expected)
        {
            if (!variant.Converter.Options.TryGetValue(name, out string? actual) || actual != value)
            {
                AddError(
                    diagnostics,
                    "LVRM1750",
                    $"{path}.converter.options.{name}",
                    "Animation converter options must exactly reproduce the checked grid, representation, and timing metadata.");
            }
        }
    }

    private static bool IsFnv1a32(string value)
    {
        return value.Length == 10 &&
            value.StartsWith("0x", StringComparison.Ordinal) &&
            value.AsSpan(2).IndexOfAnyExcept(sLowerHexadecimal) < 0;
    }

    private static void ValidateIntegration(
        ResourceIntegration integration,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (integration.ProviderId is not null && !ProfileRules.IsValidIdentifier(integration.ProviderId))
        {
            AddError(diagnostics, "LVRM1601", $"{path}.integration.providerId", "The provider ID is invalid.");
        }
        if (integration.Ownership == ResourceOwnership.Provider && integration.ProviderId is null)
        {
            AddError(
                diagnostics,
                "LVRM1602",
                $"{path}.integration.providerId",
                "Provider-owned resources require a provider ID.");
        }
        if (integration.Ownership == ResourceOwnership.Provider &&
            integration.Lifetime != ResourceLifetime.ProviderManaged)
        {
            AddError(
                diagnostics,
                "LVRM1603",
                $"{path}.integration.lifetime",
                "Provider-owned resources require provider-managed lifetime.");
        }
        ValidateOptionalText(integration.Notes, $"{path}.integration.notes", diagnostics);
    }

    private static bool IsOptionName(string value)
    {
        return value.Length is >= 1 and <= 64 &&
            value[0] is >= 'a' and <= 'z' &&
            value.All(character =>
                character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-');
    }

    private static bool IsBoundedText(string? value, int maximumLength)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
    }

    private static void ValidateOptionalText(
        string? value,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (value is not null && !IsBoundedText(value, MaximumTextLength))
        {
            AddError(
                diagnostics,
                "LVRM1801",
                path,
                $"Optional text must be null or contain 1 through {MaximumTextLength} characters.");
        }
    }

    private static void AddPolicyDiagnostic(
        List<ProfileDiagnostic> diagnostics,
        ResourcePolicyAction action,
        string code,
        string path,
        string message)
    {
        if (action == ResourcePolicyAction.Allow)
        {
            return;
        }
        diagnostics.Add(new ProfileDiagnostic(
            code,
            action == ResourcePolicyAction.Error
                ? ProfileDiagnosticSeverity.Error
                : ProfileDiagnosticSeverity.Warning,
            path,
            message));
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
