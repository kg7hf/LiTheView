// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public static class ResourceManifestCanonicalizer
{
    public static byte[] Canonicalize(ResourceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", manifest.Schema);
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("id", manifest.Id);
            writer.WriteString("displayName", manifest.DisplayName);
            WriteEnum(writer, "kind", manifest.Kind);
            WriteSource(writer, manifest.Source);
            WriteCompliance(writer, manifest.Compliance);
            writer.WritePropertyName("variants");
            writer.WriteStartArray();
            foreach (ResourceVariant variant in manifest.Variants.OrderBy(
                         item => item.Id,
                         StringComparer.Ordinal))
            {
                WriteVariant(writer, variant);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    public static string ComputeSha256(ReadOnlySpan<byte> canonicalUtf8)
    {
        return DisplaySurfaceCanonicalizer.ComputeSha256(canonicalUtf8);
    }

    private static void WriteSource(Utf8JsonWriter writer, ResourceSource source)
    {
        writer.WritePropertyName("source");
        writer.WriteStartObject();
        writer.WriteString("path", source.Path);
        writer.WriteString("sha256", source.Sha256);
        writer.WriteNumber("byteCount", source.ByteCount);
        writer.WriteString("mediaType", source.MediaType);
        writer.WritePropertyName("provenance");
        if (source.Provenance is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            WriteProvenance(writer, source.Provenance);
        }
        writer.WriteEndObject();
    }

    private static void WriteProvenance(Utf8JsonWriter writer, ResourceProvenance provenance)
    {
        writer.WriteStartObject();
        writer.WriteString("origin", provenance.Origin);
        WriteNullableString(writer, "author", provenance.Author);
        WriteNullableString(writer, "sourceUri", provenance.SourceUri);
        WriteNullableString(writer, "notes", provenance.Notes);
        writer.WritePropertyName("license");
        if (provenance.License is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            ResourceLicense license = provenance.License;
            writer.WriteStartObject();
            WriteNullableString(writer, "spdxId", license.SpdxId);
            WriteNullableString(writer, "licenseTextPath", license.LicenseTextPath);
            WriteNullableString(writer, "licenseTextSha256", license.LicenseTextSha256);
            WriteNullableString(writer, "attribution", license.Attribution);
            writer.WriteBoolean("redistributionApproved", license.RedistributionApproved);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteCompliance(
        Utf8JsonWriter writer,
        ResourceCompliancePolicy compliance)
    {
        writer.WritePropertyName("compliance");
        writer.WriteStartObject();
        WriteEnum(writer, "missingProvenance", compliance.MissingProvenance);
        WriteEnum(writer, "missingLicense", compliance.MissingLicense);
        WriteEnum(writer, "unapprovedRedistribution", compliance.UnapprovedRedistribution);
        writer.WriteEndObject();
    }

    private static void WriteVariant(Utf8JsonWriter writer, ResourceVariant variant)
    {
        writer.WriteStartObject();
        writer.WriteString("id", variant.Id);
        writer.WritePropertyName("compatiblePainterFormats");
        writer.WriteStartArray();
        foreach (SurfacePixelFormat format in variant.CompatiblePainterFormats.OrderBy(
                     item => StrictCamelCaseEnumConverter<SurfacePixelFormat>.ToJsonName(item),
                     StringComparer.Ordinal))
        {
            writer.WriteStringValue(StrictCamelCaseEnumConverter<SurfacePixelFormat>.ToJsonName(format));
        }
        writer.WriteEndArray();
        WriteConverter(writer, variant.Converter);
        WriteOutput(writer, variant.Output);
        WriteFont(writer, variant.Font);
        WriteImage(writer, variant.Image);
        WriteAnimation(writer, variant.Animation);
        WriteIntegration(writer, variant.Integration);
        writer.WriteEndObject();
    }

    private static void WriteFont(Utf8JsonWriter writer, FontVariantMetadata? font)
    {
        if (font is null)
        {
            return;
        }

        writer.WritePropertyName("font");
        writer.WriteStartObject();
        writer.WriteString("family", font.Family);
        writer.WriteString("style", font.Style);
        writer.WriteNumber("weight", font.Weight);
        writer.WriteNumber("pixelSize", font.PixelSize);
        WriteEnum(writer, "coverage", font.Coverage);
        writer.WriteString("repertoire", font.Repertoire);
        writer.WriteString("fallback", font.Fallback);
        writer.WriteNumber("glyphCount", font.GlyphCount);
        writer.WriteNumber("coverageBytes", font.CoverageBytes);
        writer.WriteNumber("recordBytes", font.RecordBytes);
        writer.WriteNumber("estimatedRomBytes", font.EstimatedRomBytes);
        writer.WriteString("dataChecksumFnv1a32", font.DataChecksumFnv1a32);
        writer.WriteNumber("lineHeight", font.LineHeight);
        writer.WriteNumber("baseline", font.Baseline);
        writer.WriteNumber("underlinePosition", font.UnderlinePosition);
        writer.WriteNumber("underlineThickness", font.UnderlineThickness);
        writer.WriteEndObject();
    }

    private static void WriteImage(Utf8JsonWriter writer, ImageVariantMetadata? image)
    {
        if (image is null)
        {
            return;
        }

        writer.WritePropertyName("image");
        writer.WriteStartObject();
        writer.WriteBoolean("tintable", image.Tintable);
        writer.WritePropertyName("sourcePixelSize");
        writer.WriteStartObject();
        writer.WriteNumber("width", image.SourcePixelSize.Width);
        writer.WriteNumber("height", image.SourcePixelSize.Height);
        writer.WriteEndObject();
        writer.WritePropertyName("crop");
        writer.WriteStartObject();
        writer.WriteNumber("x", image.Crop.X);
        writer.WriteNumber("y", image.Crop.Y);
        writer.WriteNumber("width", image.Crop.Width);
        writer.WriteNumber("height", image.Crop.Height);
        writer.WriteEndObject();
        WriteEnum(writer, "scaleMode", image.ScaleMode);
        WriteEnum(writer, "alphaMode", image.AlphaMode);
        writer.WriteNumber("alphaThreshold", image.AlphaThreshold);
        WriteEnum(writer, "dither", image.Dither);
        writer.WriteString("tintArgb", image.TintArgb);
        writer.WriteString("matteArgb", image.MatteArgb);
        writer.WriteString("paletteArgb", image.PaletteArgb);
        writer.WriteNumber("strideBytes", image.StrideBytes);
        writer.WriteNumber("pixelBytes", image.PixelBytes);
        writer.WriteNumber("paletteBytes", image.PaletteBytes);
        writer.WriteNumber("estimatedRomBytes", image.EstimatedRomBytes);
        writer.WriteString("pixelChecksumFnv1a32", image.PixelChecksumFnv1a32);
        writer.WriteString("paletteChecksumFnv1a32", image.PaletteChecksumFnv1a32);
        writer.WriteString("dataChecksumFnv1a32", image.DataChecksumFnv1a32);
        writer.WriteEndObject();
    }

    private static void WriteAnimation(Utf8JsonWriter writer, AnimationVariantMetadata? animation)
    {
        if (animation is null)
        {
            return;
        }

        writer.WritePropertyName("animation");
        writer.WriteStartObject();
        writer.WriteBoolean("tintable", animation.Tintable);
        writer.WritePropertyName("sourcePixelSize");
        writer.WriteStartObject();
        writer.WriteNumber("width", animation.SourcePixelSize.Width);
        writer.WriteNumber("height", animation.SourcePixelSize.Height);
        writer.WriteEndObject();
        writer.WritePropertyName("sourceGrid");
        writer.WriteStartObject();
        writer.WriteNumber("x", animation.SourceGrid.X);
        writer.WriteNumber("y", animation.SourceGrid.Y);
        writer.WriteNumber("width", animation.SourceGrid.Width);
        writer.WriteNumber("height", animation.SourceGrid.Height);
        writer.WriteEndObject();
        writer.WritePropertyName("sourceFramePixelSize");
        writer.WriteStartObject();
        writer.WriteNumber("width", animation.SourceFramePixelSize.Width);
        writer.WriteNumber("height", animation.SourceFramePixelSize.Height);
        writer.WriteEndObject();
        writer.WriteNumber("columns", animation.Columns);
        WriteEnum(writer, "scaleMode", animation.ScaleMode);
        WriteEnum(writer, "alphaMode", animation.AlphaMode);
        writer.WriteNumber("alphaThreshold", animation.AlphaThreshold);
        WriteEnum(writer, "dither", animation.Dither);
        writer.WriteString("tintArgb", animation.TintArgb);
        writer.WriteString("matteArgb", animation.MatteArgb);
        writer.WriteString("paletteArgb", animation.PaletteArgb);
        writer.WriteNumber("frameIntervalMilliseconds", animation.FrameIntervalMilliseconds);
        writer.WriteNumber("repeatIndex", animation.RepeatIndex);
        writer.WriteBoolean("autoplay", animation.Autoplay);
        writer.WriteNumber("strideBytes", animation.StrideBytes);
        writer.WriteNumber("framePixelBytes", animation.FramePixelBytes);
        writer.WriteNumber("pixelBytes", animation.PixelBytes);
        writer.WriteNumber("paletteBytes", animation.PaletteBytes);
        writer.WriteNumber("estimatedRomBytes", animation.EstimatedRomBytes);
        writer.WriteString("pixelChecksumFnv1a32", animation.PixelChecksumFnv1a32);
        writer.WriteString("paletteChecksumFnv1a32", animation.PaletteChecksumFnv1a32);
        writer.WriteString("dataChecksumFnv1a32", animation.DataChecksumFnv1a32);
        writer.WriteEndObject();
    }

    private static void WriteConverter(Utf8JsonWriter writer, ResourceConverter converter)
    {
        writer.WritePropertyName("converter");
        writer.WriteStartObject();
        writer.WriteString("id", converter.Id);
        writer.WriteString("version", converter.Version);
        writer.WritePropertyName("options");
        writer.WriteStartObject();
        foreach ((string key, string value) in converter.Options.OrderBy(
                     item => item.Key,
                     StringComparer.Ordinal))
        {
            writer.WriteString(key, value);
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteOutput(Utf8JsonWriter writer, ResourceOutput output)
    {
        writer.WritePropertyName("output");
        writer.WriteStartObject();
        writer.WriteString("path", output.Path);
        writer.WriteString("sha256", output.Sha256);
        writer.WriteNumber("byteCount", output.ByteCount);
        WriteEnum(writer, "format", output.Format);
        writer.WritePropertyName("pixelSize");
        if (output.PixelSize is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteNumber("width", output.PixelSize.Width);
            writer.WriteNumber("height", output.PixelSize.Height);
            writer.WriteEndObject();
        }
        writer.WriteNumber("frameCount", output.FrameCount);
        writer.WriteEndObject();
    }

    private static void WriteIntegration(Utf8JsonWriter writer, ResourceIntegration integration)
    {
        writer.WritePropertyName("integration");
        writer.WriteStartObject();
        WriteEnum(writer, "ownership", integration.Ownership);
        WriteEnum(writer, "lifetime", integration.Lifetime);
        WriteNullableString(writer, "providerId", integration.ProviderId);
        WriteNullableString(writer, "notes", integration.Notes);
        writer.WriteEndObject();
    }

    private static void WriteEnum<TEnum>(
        Utf8JsonWriter writer,
        string name,
        TEnum value)
        where TEnum : struct, Enum
    {
        writer.WriteString(name, StrictCamelCaseEnumConverter<TEnum>.ToJsonName(value));
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }
}
