// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Schema;

public sealed record WidgetCatalogCompatibilityResult
{
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsCompatible =>
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class WidgetCatalogCompatibilityAnalyzer
{
    public static WidgetCatalogCompatibilityResult Compare(
        WidgetCatalogLoadResult previous,
        WidgetCatalogLoadResult current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        List<ProfileDiagnostic> diagnostics = [.. previous.Diagnostics, .. current.Diagnostics];
        if (!previous.IsValid || !current.IsValid)
        {
            return new WidgetCatalogCompatibilityResult { Diagnostics = diagnostics };
        }

        WidgetCatalog before = previous.Catalog!;
        WidgetCatalog after = current.Catalog!;
        if (before.Id != after.Id)
        {
            AddError(diagnostics, "LVWC1001", "$.id", "Catalog identity cannot change in place.");
        }
        if (after.CompatibilityVersion < before.CompatibilityVersion)
        {
            AddError(
                diagnostics,
                "LVWC1002",
                "$.compatibilityVersion",
                "Compatibility versions cannot decrease.");
        }

        CompareProperties(before, after, diagnostics);
        CompareWidgets(before, after, diagnostics);
        return new WidgetCatalogCompatibilityResult { Diagnostics = diagnostics };
    }

    private static void CompareProperties(
        WidgetCatalog before,
        WidgetCatalog after,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, WidgetPropertyDefinition> current = after.Properties.ToDictionary(
            property => property.Id,
            StringComparer.Ordinal);
        foreach (WidgetPropertyDefinition property in before.Properties)
        {
            string resolvedId = WidgetCatalogMigrator.ResolvePropertyId(
                after,
                widgetId: null,
                property.Id,
                before.CompatibilityVersion);
            if (!current.TryGetValue(resolvedId, out WidgetPropertyDefinition? replacement))
            {
                AddError(
                    diagnostics,
                    "LVWC1101",
                    $"$.properties.{property.Id}",
                    "A property was removed without a deterministic rename migration.");
                continue;
            }
            if (property.Kind != replacement.Kind ||
                property.XmlName != replacement.XmlName ||
                property.CppMember != replacement.CppMember ||
                property.Required != replacement.Required ||
                property.Minimum != replacement.Minimum ||
                property.Maximum != replacement.Maximum ||
                !SameDefault(property, replacement))
            {
                AddError(
                    diagnostics,
                    "LVWC1102",
                    $"$.properties.{property.Id}",
                    "The property type, serialization, default, requirement, or range changed incompatibly.");
            }
        }
    }

    private static void CompareWidgets(
        WidgetCatalog before,
        WidgetCatalog after,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, WidgetDefinition> current = after.Widgets.ToDictionary(
            widget => widget.Id,
            StringComparer.Ordinal);
        Dictionary<string, WidgetPropertyDefinition> properties = after.Properties.ToDictionary(
            property => property.Id,
            StringComparer.Ordinal);
        foreach (WidgetDefinition widget in before.Widgets)
        {
            string resolvedId = WidgetCatalogMigrator.ResolveWidgetId(
                after,
                widget.Id,
                before.CompatibilityVersion);
            if (!current.TryGetValue(resolvedId, out WidgetDefinition? replacement))
            {
                AddError(
                    diagnostics,
                    "LVWC1201",
                    $"$.widgets.{widget.Id}",
                    "A widget was removed without a deterministic rename migration.");
                continue;
            }
            if (widget.RuntimeKind != replacement.RuntimeKind ||
                widget.CppType != replacement.CppType ||
                widget.Xml.Element != replacement.Xml.Element ||
                widget.Xml.LegacyClass != replacement.Xml.LegacyClass)
            {
                AddError(
                    diagnostics,
                    "LVWC1202",
                    $"$.widgets.{widget.Id}",
                    "The runtime or XML identity of a widget changed incompatibly.");
            }

            HashSet<string> currentProperties = replacement.PropertyIds.ToHashSet(StringComparer.Ordinal);
            foreach (string propertyId in widget.PropertyIds)
            {
                string migrated = WidgetCatalogMigrator.ResolvePropertyId(
                    after,
                    widget.Id,
                    propertyId,
                    before.CompatibilityVersion);
                if (!currentProperties.Contains(migrated))
                {
                    AddError(
                        diagnostics,
                        "LVWC1203",
                        $"$.widgets.{widget.Id}.propertyIds",
                        $"Property '{propertyId}' was removed from the widget.");
                }
            }
            foreach (string added in currentProperties.Except(widget.PropertyIds, StringComparer.Ordinal))
            {
                if (properties.TryGetValue(added, out WidgetPropertyDefinition? property) &&
                    property.Required && property.DefaultValue.ValueKind == System.Text.Json.JsonValueKind.Null)
                {
                    AddError(
                        diagnostics,
                        "LVWC1204",
                        $"$.widgets.{widget.Id}.propertyIds",
                        $"Required property '{added}' was added without a deterministic default.");
                }
            }
        }
    }

    private static bool SameDefault(
        WidgetPropertyDefinition before,
        WidgetPropertyDefinition after)
    {
        return System.Text.Json.JsonElement.DeepEquals(before.DefaultValue, after.DefaultValue);
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
