// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

public static class DesignerProjectCanonicalizer
{
    public static byte[] Canonicalize(DesignerProjectManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", manifest.Schema);
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            if (manifest.DisplayKind is not null) writer.WriteString("displayKind", manifest.DisplayKind);
            writer.WriteString("id", manifest.Id);
            writer.WriteString("displayName", manifest.DisplayName);

            writer.WritePropertyName("schemaCompatibility");
            writer.WriteStartObject();
            if (manifest.SchemaCompatibility.DisplaySurface is int surfaceVersion)
            {
                writer.WriteNumber("displaySurface", surfaceVersion);
            }
            writer.WriteNumber("projectManifest", manifest.SchemaCompatibility.ProjectManifest);
            if (manifest.SchemaCompatibility.SegmentGlass is int glassVersion) writer.WriteNumber("segmentGlass", glassVersion);
            if (manifest.SchemaCompatibility.DisplayDefinition is int definitionVersion)
            {
                writer.WriteNumber("displayDefinition", definitionVersion);
            }
            if (manifest.SchemaCompatibility.LogicalInput is int inputVersion)
            {
                writer.WriteNumber("logicalInput", inputVersion);
            }
            writer.WriteNumber("form", manifest.SchemaCompatibility.Form);
            writer.WriteNumber("resourceManifest", manifest.SchemaCompatibility.ResourceManifest);
            writer.WriteNumber("extensionManifest", manifest.SchemaCompatibility.ExtensionManifest);
            writer.WriteEndObject();

            if (manifest.SurfaceProfile is not null)
            {
                WriteProfileReference(writer, "surfaceProfile", manifest.SurfaceProfile);
            }
            if (manifest.SegmentGlass is not null) WriteProfileReference(writer, "segmentGlass", manifest.SegmentGlass);
            if (manifest.DisplayDefinition is not null)
            {
                WriteProfileReference(writer, "displayDefinition", manifest.DisplayDefinition);
            }
            if (manifest.LogicalInput is not null)
            {
                WriteProfileReference(writer, "logicalInput", manifest.LogicalInput);
            }

            WriteSortedStrings(writer, "forms", manifest.Forms);
            WriteSortedStrings(writer, "resourceManifests", manifest.ResourceManifests);

            writer.WritePropertyName("extensions");
            writer.WriteStartArray();
            foreach (ProjectExtensionReference extension in manifest.Extensions.OrderBy(
                         item => item.Id,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", extension.Id);
                writer.WriteString("version", extension.Version);
                writer.WriteString("packagePath", extension.PackagePath);
                writer.WriteString("sha256", extension.Sha256);
                writer.WriteBoolean("required", extension.Required);
                writer.WriteEndObject();
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

    private static void WriteProfileReference(
        Utf8JsonWriter writer,
        string name,
        LockedProfileReference reference)
    {
        writer.WritePropertyName(name);
        WriteProfileReferenceValue(writer, reference);
    }

    private static void WriteProfileReferenceValue(
        Utf8JsonWriter writer,
        LockedProfileReference reference)
    {
        writer.WriteStartObject();
        writer.WriteString("id", reference.Id);
        writer.WriteString("sha256", reference.Sha256);
        writer.WriteEndObject();
    }

    private static void WriteSortedStrings(
        Utf8JsonWriter writer,
        string name,
        IEnumerable<string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (string value in values.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }
}
