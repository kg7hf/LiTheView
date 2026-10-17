// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text.Json;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Schema;

public static class WidgetCatalogCanonicalizer
{
    public static byte[] Canonicalize(WidgetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", catalog.Schema);
            writer.WriteNumber("schemaVersion", catalog.SchemaVersion);
            writer.WriteNumber("compatibilityVersion", catalog.CompatibilityVersion);
            writer.WriteString("id", catalog.Id);
            writer.WriteString("displayName", catalog.DisplayName);
            WriteProperties(writer, catalog.Properties);
            WriteWidgets(writer, catalog.Widgets);
            WriteMigrations(writer, catalog.Migrations);
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

    private static void WriteProperties(
        Utf8JsonWriter writer,
        IEnumerable<WidgetPropertyDefinition> properties)
    {
        writer.WritePropertyName("properties");
        writer.WriteStartArray();
        foreach (WidgetPropertyDefinition property in properties.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", property.Id);
            writer.WriteString("displayName", property.DisplayName);
            WriteEnum(writer, "kind", property.Kind);
            writer.WriteString("xmlName", property.XmlName);
            WriteStrings(writer, "xmlAliases", property.XmlAliases);
            WriteEnum(writer, "xmlTransform", property.XmlTransform);
            writer.WriteString("cppMember", property.CppMember);
            writer.WritePropertyName("defaultValue");
            property.DefaultValue.WriteTo(writer);
            WriteOptionalNumber(writer, "minimum", property.Minimum);
            WriteOptionalNumber(writer, "maximum", property.Maximum);
            if (property.Units is not null)
            {
                writer.WriteString("units", property.Units);
            }
            writer.WriteBoolean("required", property.Required);
            WriteStrings(writer, "enumValues", property.EnumValues);
            writer.WriteString("documentation", property.Documentation);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteWidgets(
        Utf8JsonWriter writer,
        IEnumerable<WidgetDefinition> widgets)
    {
        writer.WritePropertyName("widgets");
        writer.WriteStartArray();
        foreach (WidgetDefinition widget in widgets.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", widget.Id);
            writer.WriteString("displayName", widget.DisplayName);
            writer.WriteString("runtimeKind", widget.RuntimeKind);
            writer.WriteString("cppType", widget.CppType);
            writer.WriteString("paletteGroup", widget.PaletteGroup);
            writer.WriteString("help", widget.Help);
            writer.WritePropertyName("defaultGeometry");
            writer.WriteStartObject();
            writer.WriteNumber("width", widget.DefaultGeometry.Width);
            writer.WriteNumber("height", widget.DefaultGeometry.Height);
            writer.WriteEndObject();
            WriteXml(writer, widget.Xml);
            WriteParentRule(writer, widget.ParentRule);
            WriteEnum(writer, "childPolicy", widget.ChildPolicy);
            WriteStrings(writer, "propertyIds", widget.PropertyIds);
            WriteActions(writer, widget.Actions);
            WriteBindings(writer, widget.Bindings);
            WriteStates(writer, widget.States);
            WriteEnums(writer, "inputCapabilities", widget.InputCapabilities);
            WriteResources(writer, widget.Resources);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteXml(Utf8JsonWriter writer, WidgetXmlContract xml)
    {
        writer.WritePropertyName("xml");
        writer.WriteStartObject();
        writer.WriteString("element", xml.Element);
        WriteStrings(writer, "elementAliases", xml.ElementAliases);
        writer.WriteString("legacyClass", xml.LegacyClass);
        WriteStrings(writer, "legacyClassAliases", xml.LegacyClassAliases);
        if (xml.DiscriminatorPropertyId is not null)
        {
            writer.WriteString("discriminatorPropertyId", xml.DiscriminatorPropertyId);
        }
        writer.WriteEndObject();
    }

    private static void WriteParentRule(Utf8JsonWriter writer, WidgetParentRule rule)
    {
        writer.WritePropertyName("parentRule");
        writer.WriteStartObject();
        writer.WriteBoolean("rootAllowed", rule.RootAllowed);
        WriteStrings(writer, "allowedParentIds", rule.AllowedParentIds);
        writer.WriteEndObject();
    }

    private static void WriteActions(
        Utf8JsonWriter writer,
        IEnumerable<WidgetActionDefinition> actions)
    {
        writer.WritePropertyName("actions");
        writer.WriteStartArray();
        foreach (WidgetActionDefinition action in actions.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", action.Id);
            writer.WriteString("displayName", action.DisplayName);
            writer.WriteString("event", action.Event);
            writer.WriteString("runtimeActionSource", action.RuntimeActionSource);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteBindings(
        Utf8JsonWriter writer,
        IEnumerable<WidgetBindingDefinition> bindings)
    {
        writer.WritePropertyName("bindings");
        writer.WriteStartArray();
        foreach (WidgetBindingDefinition binding in bindings.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", binding.Id);
            writer.WriteString("displayName", binding.DisplayName);
            writer.WriteString("propertyId", binding.PropertyId);
            WriteEnum(writer, "direction", binding.Direction);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteStates(
        Utf8JsonWriter writer,
        IEnumerable<WidgetStateDefinition> states)
    {
        writer.WritePropertyName("states");
        writer.WriteStartArray();
        foreach (WidgetStateDefinition state in states.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("displayName", state.DisplayName);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteResources(
        Utf8JsonWriter writer,
        IEnumerable<WidgetResourceDependency> resources)
    {
        writer.WritePropertyName("resources");
        writer.WriteStartArray();
        foreach (WidgetResourceDependency resource in resources
                     .OrderBy(item => item.PropertyId, StringComparer.Ordinal)
                     .ThenBy(item => item.Kind))
        {
            writer.WriteStartObject();
            WriteEnum(writer, "kind", resource.Kind);
            writer.WriteString("propertyId", resource.PropertyId);
            writer.WriteBoolean("required", resource.Required);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteMigrations(
        Utf8JsonWriter writer,
        IEnumerable<WidgetMigration> migrations)
    {
        writer.WritePropertyName("migrations");
        writer.WriteStartArray();
        foreach (WidgetMigration migration in migrations
                     .OrderBy(item => item.FromCompatibilityVersion)
                     .ThenBy(item => item.ToCompatibilityVersion)
                     .ThenBy(item => item.Kind)
                     .ThenBy(item => item.WidgetId, StringComparer.Ordinal)
                     .ThenBy(item => item.FromId, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteNumber("fromCompatibilityVersion", migration.FromCompatibilityVersion);
            writer.WriteNumber("toCompatibilityVersion", migration.ToCompatibilityVersion);
            WriteEnum(writer, "kind", migration.Kind);
            if (migration.WidgetId is not null)
            {
                writer.WriteString("widgetId", migration.WidgetId);
            }
            writer.WriteString("fromId", migration.FromId);
            writer.WriteString("toId", migration.ToId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteStrings(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (string value in values.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }

    private static void WriteEnums<TEnum>(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<TEnum> values)
        where TEnum : struct, Enum
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (TEnum value in values.OrderBy(StrictCamelCaseEnumConverter<TEnum>.ToJsonName))
        {
            writer.WriteStringValue(StrictCamelCaseEnumConverter<TEnum>.ToJsonName(value));
        }
        writer.WriteEndArray();
    }

    private static void WriteEnum<TEnum>(
        Utf8JsonWriter writer,
        string propertyName,
        TEnum value)
        where TEnum : struct, Enum
    {
        writer.WriteString(propertyName, StrictCamelCaseEnumConverter<TEnum>.ToJsonName(value));
    }

    private static void WriteOptionalNumber(Utf8JsonWriter writer, string name, long? value)
    {
        if (value.HasValue)
        {
            writer.WriteNumber(name, value.Value);
        }
    }
}
