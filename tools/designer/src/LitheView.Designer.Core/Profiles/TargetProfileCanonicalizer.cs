// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;

namespace LitheView.Designer.Core.Profiles;

public static class TargetProfileCanonicalizer
{
    public static byte[] Canonicalize(TargetProfile profile)
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
            WriteBaseProfile(writer, profile.BaseProfile);
            WriteSurfaceCompatibility(writer, profile.SurfaceCompatibility);
            WriteRuntime(writer, profile.Runtime);
            WriteCapacities(writer, profile.Capacities);
            WriteBudgets(writer, profile.Budgets);
            WriteResourceStorage(writer, profile.ResourceStorage);

            writer.WritePropertyName("supportedResourceFormats");
            writer.WriteStartArray();
            foreach (ResourceFormat format in profile.SupportedResourceFormats.OrderBy(ToJsonName))
            {
                writer.WriteStringValue(ToJsonName(format));
            }
            writer.WriteEndArray();

            writer.WritePropertyName("provenance");
            writer.WriteStartObject();
            writer.WritePropertyName("sourcePaths");
            writer.WriteStartArray();
            foreach (string sourcePath in profile.Provenance.SourcePaths.Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(sourcePath);
            }
            writer.WriteEndArray();
            writer.WriteString("notes", profile.Provenance.Notes);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    public static string ComputeSha256(ReadOnlySpan<byte> canonicalUtf8)
    {
        return DisplaySurfaceCanonicalizer.ComputeSha256(canonicalUtf8);
    }

    private static void WriteBaseProfile(
        Utf8JsonWriter writer,
        TargetProfileBaseReference? baseProfile)
    {
        writer.WritePropertyName("baseProfile");
        if (baseProfile is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("profileId", baseProfile.ProfileId);
        writer.WriteString("sha256", baseProfile.Sha256);
        writer.WritePropertyName("explicitOverrides");
        writer.WriteStartArray();
        foreach (string item in baseProfile.ExplicitOverrides.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSurfaceCompatibility(
        Utf8JsonWriter writer,
        SurfaceCompatibilityProfile compatibility)
    {
        writer.WritePropertyName("surfaceCompatibility");
        writer.WriteStartObject();
        writer.WriteString("family", compatibility.Family);
        writer.WritePropertyName("painterFormats");
        writer.WriteStartArray();
        foreach (SurfacePixelFormat format in compatibility.PainterFormats.OrderBy(ToJsonName))
        {
            writer.WriteStringValue(ToJsonName(format));
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRuntime(Utf8JsonWriter writer, TargetRuntimeProfile runtime)
    {
        writer.WritePropertyName("runtime");
        writer.WriteStartObject();
        writer.WriteString("architecture", ToJsonName(runtime.Architecture));
        writer.WriteString("environment", ToJsonName(runtime.Environment));
        writer.WriteString("renderingMode", ToJsonName(runtime.RenderingMode));
        writer.WritePropertyName("tile");
        if (runtime.Tile is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteNumber("width", runtime.Tile.Width);
            writer.WriteNumber("height", runtime.Tile.Height);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteCapacities(Utf8JsonWriter writer, RuntimeCapacityProfile capacities)
    {
        writer.WritePropertyName("capacities");
        writer.WriteStartObject();
        writer.WriteNumber("maxChildrenPerView", capacities.MaxChildrenPerView);
        writer.WriteNumber("maxDamageRegions", capacities.MaxDamageRegions);
        writer.WriteNumber("maxControllerDepth", capacities.MaxControllerDepth);
        writer.WriteNumber("maxFocusableViews", capacities.MaxFocusableViews);
        writer.WriteEndObject();
    }

    private static void WriteBudgets(Utf8JsonWriter writer, TargetBudgetProfile budgets)
    {
        writer.WritePropertyName("budgets");
        writer.WriteStartObject();
        WriteBudget(writer, "archiveText", budgets.ArchiveText);
        WriteBudget(writer, "initializedData", budgets.InitializedData);
        WriteBudget(writer, "bss", budgets.Bss);
        WriteBudget(writer, "stackFrame", budgets.StackFrame);
        WriteBudget(writer, "staticObject", budgets.StaticObject);
        WriteBudget(writer, "tileWorkspace", budgets.TileWorkspace);
        WriteBudget(writer, "framebuffer", budgets.Framebuffer);
        WriteBudget(writer, "internalFlash", budgets.InternalFlash);
        WriteBudget(writer, "internalRam", budgets.InternalRam);
        WriteBudget(writer, "externalRam", budgets.ExternalRam);
        writer.WriteEndObject();
    }

    private static void WriteBudget(Utf8JsonWriter writer, string name, BudgetLimit? budget)
    {
        writer.WritePropertyName(name);
        if (budget is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("maximumBytes", budget.MaximumBytes);
        writer.WriteString("enforcement", ToJsonName(budget.Enforcement));
        writer.WriteString("source", budget.Source);
        writer.WriteEndObject();
    }

    private static void WriteResourceStorage(
        Utf8JsonWriter writer,
        ResourceStorageProfile storage)
    {
        writer.WritePropertyName("resourceStorage");
        writer.WriteStartObject();
        writer.WriteString("defaultPlacement", ToJsonName(storage.DefaultPlacement));
        writer.WritePropertyName("allowedPlacements");
        writer.WriteStartArray();
        foreach (ResourcePlacement placement in storage.AllowedPlacements.OrderBy(ToJsonName))
        {
            writer.WriteStringValue(ToJsonName(placement));
        }
        writer.WriteEndArray();
        writer.WriteNumber("alignmentBytes", storage.AlignmentBytes);
        writer.WriteEndObject();
    }

    private static string ToJsonName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return StrictCamelCaseEnumConverter<TEnum>.ToJsonName(value);
    }
}
