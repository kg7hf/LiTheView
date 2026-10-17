// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text.Json;

namespace LitheView.Designer.Core.Profiles;

public static class DisplaySurfaceCanonicalizer
{
    public static byte[] Canonicalize(DisplaySurfaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", profile.Schema);
            writer.WriteNumber("schemaVersion", profile.SchemaVersion);
            writer.WriteString("id", profile.Id);
            writer.WriteString("displayName", profile.DisplayName);
            writer.WriteString("compatibilityFamily", profile.CompatibilityFamily);
            writer.WriteString("shape", StrictCamelCaseEnumConverter<SurfaceShape>.ToJsonName(profile.Shape));

            writer.WritePropertyName("physicalSize");
            writer.WriteStartObject();
            WriteOptionalNumber(writer, "widthMillimeters", profile.PhysicalSize.WidthMillimeters);
            WriteOptionalNumber(writer, "heightMillimeters", profile.PhysicalSize.HeightMillimeters);
            WriteOptionalNumber(writer, "diameterMillimeters", profile.PhysicalSize.DiameterMillimeters);
            writer.WriteEndObject();

            writer.WritePropertyName("pixelSize");
            writer.WriteStartObject();
            writer.WriteNumber("width", profile.PixelSize.Width);
            writer.WriteNumber("height", profile.PixelSize.Height);
            writer.WriteEndObject();

            writer.WritePropertyName("coordinateSystem");
            writer.WriteStartObject();
            writer.WriteString(
                "origin",
                StrictCamelCaseEnumConverter<CoordinateOrigin>.ToJsonName(profile.CoordinateSystem.Origin));
            writer.WriteNumber("rotationDegrees", profile.CoordinateSystem.RotationDegrees);
            writer.WriteEndObject();

            writer.WritePropertyName("painterSurface");
            writer.WriteStartObject();
            writer.WriteString(
                "format",
                StrictCamelCaseEnumConverter<SurfacePixelFormat>.ToJsonName(profile.PainterSurface.Format));
            writer.WriteNumber("colorDepthBits", profile.PainterSurface.ColorDepthBits);
            writer.WriteNumber("storageBitsPerPixel", profile.PainterSurface.StorageBitsPerPixel);
            writer.WriteEndObject();

            writer.WritePropertyName("nativeDisplay");
            writer.WriteStartObject();
            writer.WriteString(
                "colorModel",
                StrictCamelCaseEnumConverter<NativeColorModel>.ToJsonName(profile.NativeDisplay.ColorModel));
            writer.WriteNumber("colorDepthBits", profile.NativeDisplay.ColorDepthBits);
            writer.WriteNumber("transferUnitBits", profile.NativeDisplay.TransferUnitBits);
            writer.WriteString(
                "transferFormat",
                StrictCamelCaseEnumConverter<NativeTransferFormat>.ToJsonName(
                    profile.NativeDisplay.TransferFormat));
            writer.WriteEndObject();

            writer.WritePropertyName("safeArea");
            writer.WriteStartObject();
            writer.WriteNumber("x", profile.SafeArea.X);
            writer.WriteNumber("y", profile.SafeArea.Y);
            writer.WriteNumber("width", profile.SafeArea.Width);
            writer.WriteNumber("height", profile.SafeArea.Height);
            writer.WriteEndObject();

            writer.WritePropertyName("inputCapabilities");
            writer.WriteStartArray();
            foreach (InputCapability capability in profile.InputCapabilities.OrderBy(ToInputName))
            {
                writer.WriteStringValue(ToInputName(capability));
            }
            writer.WriteEndArray();

            writer.WritePropertyName("provenance");
            writer.WriteStartObject();
            writer.WriteString("manufacturer", profile.Provenance.Manufacturer);
            writer.WriteString("model", profile.Provenance.Model);
            writer.WriteString("sourceUrl", profile.Provenance.SourceUrl);
            writer.WriteString("sourceDocument", profile.Provenance.SourceDocument);
            writer.WritePropertyName("sourceSha256");
            if (profile.Provenance.SourceSha256 is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(profile.Provenance.SourceSha256);
            }
            writer.WriteString("notes", profile.Provenance.Notes);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    public static string ComputeSha256(ReadOnlySpan<byte> canonicalUtf8)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(canonicalUtf8, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteOptionalNumber(
        Utf8JsonWriter writer,
        string name,
        decimal? value)
    {
        if (value.HasValue)
        {
            writer.WriteNumber(name, value.Value);
        }
    }

    private static string ToInputName(InputCapability capability)
    {
        return StrictCamelCaseEnumConverter<InputCapability>.ToJsonName(capability);
    }
}
