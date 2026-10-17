// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Schema;

public static class WidgetCatalogValidator
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumCatalogItems = 512;

    public static IReadOnlyList<ProfileDiagnostic> Validate(WidgetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        List<ProfileDiagnostic> diagnostics = [];
        if (catalog.SchemaVersion != CurrentSchemaVersion)
        {
            AddError(diagnostics, "LVWS1001", "$.schemaVersion", "Only widget schema version 1 is supported.");
        }
        if (catalog.CompatibilityVersion < 1)
        {
            AddError(
                diagnostics,
                "LVWS1002",
                "$.compatibilityVersion",
                "The compatibility version must be positive.");
        }
        ValidateIdentity(catalog.Id, catalog.DisplayName, "catalog", diagnostics);
        if (catalog.Properties.Length is 0 or > MaximumCatalogItems)
        {
            AddError(
                diagnostics,
                "LVWS1003",
                "$.properties",
                $"A widget catalog requires 1 through {MaximumCatalogItems} properties.");
        }
        if (catalog.Widgets.Length is 0 or > MaximumCatalogItems)
        {
            AddError(
                diagnostics,
                "LVWS1004",
                "$.widgets",
                $"A widget catalog requires 1 through {MaximumCatalogItems} widgets.");
        }

        Dictionary<string, WidgetPropertyDefinition> properties = ValidateProperties(
            catalog.Properties,
            diagnostics);
        Dictionary<string, WidgetDefinition> widgets = ValidateWidgets(
            catalog.Widgets,
            properties,
            diagnostics);
        ValidateParentRules(widgets, diagnostics);
        ValidateXmlIdentities(catalog.Widgets, diagnostics);
        ValidateMigrations(catalog, properties, widgets, diagnostics);
        return diagnostics;
    }

    private static Dictionary<string, WidgetPropertyDefinition> ValidateProperties(
        IEnumerable<WidgetPropertyDefinition> properties,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, WidgetPropertyDefinition> result = new(StringComparer.Ordinal);
        int index = 0;
        foreach (WidgetPropertyDefinition property in properties)
        {
            string path = $"$.properties[{index}]";
            ValidateIdentity(property.Id, property.DisplayName, path, diagnostics);
            if (!result.TryAdd(property.Id, property))
            {
                AddError(diagnostics, "LVWS1101", $"{path}.id", $"Property ID '{property.Id}' is duplicated.");
            }
            if (!IsXmlName(property.XmlName, allowWildcard: true))
            {
                AddError(diagnostics, "LVWS1102", $"{path}.xmlName", "The XML name is invalid.");
            }
            bool transformValid = property.XmlTransform switch
            {
                WidgetXmlTransform.Direct => !property.XmlName.EndsWith('*'),
                WidgetXmlTransform.InvertBoolean => property.Kind == WidgetPropertyKind.Boolean,
                WidgetXmlTransform.ConsecutiveSequence =>
                    property.Kind == WidgetPropertyKind.AnimationFrames && property.XmlName.EndsWith('*'),
                WidgetXmlTransform.TriStateBoolean =>
                    property.Kind == WidgetPropertyKind.Enum &&
                    property.EnumValues.Order(StringComparer.Ordinal).SequenceEqual(
                        ["auto", "false", "true"],
                        StringComparer.Ordinal),
                _ => false
            };
            if (!transformValid)
            {
                AddError(
                    diagnostics,
                    "LVWS1110",
                    $"{path}.xmlTransform",
                    "The XML transform is incompatible with the property kind or name.");
            }
            ValidateUniqueNames(property.XmlAliases, $"{path}.xmlAliases", "LVWS1103", diagnostics, IsXmlAlias);
            if (!IsCppMember(property.CppMember))
            {
                AddError(
                    diagnostics,
                    "LVWS1104",
                    $"{path}.cppMember",
                    "The C++ member path must begin with 'spec.' or 'state.' and contain identifiers only.");
            }
            if (property.Minimum.HasValue && property.Maximum.HasValue &&
                property.Minimum.Value > property.Maximum.Value)
            {
                AddError(diagnostics, "LVWS1105", path, "The property minimum exceeds its maximum.");
            }
            bool numeric = IsNumeric(property.Kind);
            if (!numeric && (property.Minimum.HasValue || property.Maximum.HasValue))
            {
                AddError(
                    diagnostics,
                    "LVWS1106",
                    path,
                    "Only numeric property kinds may declare a numeric range.");
            }
            ValidateUniqueNames(
                property.EnumValues,
                $"{path}.enumValues",
                "LVWS1107",
                diagnostics,
                value => ProfileRules.IsValidIdentifier(value));
            if (property.Kind == WidgetPropertyKind.Enum && property.EnumValues.Length == 0 ||
                property.Kind != WidgetPropertyKind.Enum && property.EnumValues.Length != 0)
            {
                AddError(
                    diagnostics,
                    "LVWS1108",
                    $"{path}.enumValues",
                    "Enum values must be nonempty only for enum properties.");
            }
            if (string.IsNullOrWhiteSpace(property.Documentation) || property.Documentation.Length > 640)
            {
                AddError(
                    diagnostics,
                    "LVWS1109",
                    $"{path}.documentation",
                    "Property documentation must contain 1 through 640 characters.");
            }
            ValidateDefault(property, path, diagnostics);
            ++index;
        }
        return result;
    }

    private static Dictionary<string, WidgetDefinition> ValidateWidgets(
        IEnumerable<WidgetDefinition> widgets,
        Dictionary<string, WidgetPropertyDefinition> properties,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, WidgetDefinition> result = new(StringComparer.Ordinal);
        HashSet<string> runtimeKinds = new(StringComparer.Ordinal);
        int index = 0;
        foreach (WidgetDefinition widget in widgets)
        {
            string path = $"$.widgets[{index}]";
            ValidateIdentity(widget.Id, widget.DisplayName, path, diagnostics);
            if (!result.TryAdd(widget.Id, widget))
            {
                AddError(diagnostics, "LVWS1201", $"{path}.id", $"Widget ID '{widget.Id}' is duplicated.");
            }
            if (!IsCodeIdentifier(widget.RuntimeKind) || !runtimeKinds.Add(widget.RuntimeKind))
            {
                AddError(
                    diagnostics,
                    "LVWS1202",
                    $"{path}.runtimeKind",
                    "The runtime kind must be a unique C++ identifier.");
            }
            if (!IsCppType(widget.CppType))
            {
                AddError(diagnostics, "LVWS1203", $"{path}.cppType", "The C++ type name is invalid.");
            }
            if (string.IsNullOrWhiteSpace(widget.PaletteGroup) ||
                string.IsNullOrWhiteSpace(widget.Help) || widget.Help.Length > 640)
            {
                AddError(
                    diagnostics,
                    "LVWS1204",
                    path,
                    "Palette group and bounded help text are required.");
            }
            if (widget.DefaultGeometry.Width < 1 || widget.DefaultGeometry.Height < 1)
            {
                AddError(
                    diagnostics,
                    "LVWS1205",
                    $"{path}.defaultGeometry",
                    "Default widget dimensions must be positive.");
            }
            ValidateXml(widget.Xml, path, diagnostics);
            HashSet<string> propertyIds = ValidateReferences(
                widget.PropertyIds,
                properties,
                $"{path}.propertyIds",
                diagnostics);
            if (widget.Xml.DiscriminatorPropertyId is not null &&
                !propertyIds.Contains(widget.Xml.DiscriminatorPropertyId))
            {
                AddError(
                    diagnostics,
                    "LVWS1206",
                    $"{path}.xml.discriminatorPropertyId",
                    "The XML discriminator must be one of the widget's properties.");
            }
            ValidateActions(widget.Actions, path, diagnostics);
            ValidateBindings(widget.Bindings, propertyIds, properties, path, diagnostics);
            ValidateStates(widget.States, path, diagnostics);
            ValidateDistinctEnums(
                widget.InputCapabilities,
                $"{path}.inputCapabilities",
                "LVWS1207",
                diagnostics);
            ValidateResources(widget.Resources, propertyIds, properties, path, diagnostics);
            ++index;
        }
        return result;
    }

    private static void ValidateParentRules(
        Dictionary<string, WidgetDefinition> widgets,
        List<ProfileDiagnostic> diagnostics)
    {
        bool hasRoot = false;
        foreach ((string id, WidgetDefinition widget) in widgets)
        {
            hasRoot |= widget.ParentRule.RootAllowed;
            HashSet<string> parents = new(StringComparer.Ordinal);
            foreach (string parentId in widget.ParentRule.AllowedParentIds)
            {
                if (!parents.Add(parentId))
                {
                    AddError(
                        diagnostics,
                        "LVWS1301",
                        $"$.widgets.{id}.parentRule",
                        $"Parent ID '{parentId}' is duplicated.");
                }
                else if (!widgets.TryGetValue(parentId, out WidgetDefinition? parent))
                {
                    AddError(
                        diagnostics,
                        "LVWS1302",
                        $"$.widgets.{id}.parentRule",
                        $"Parent ID '{parentId}' does not exist.");
                }
                else if (parent.ChildPolicy == WidgetChildPolicy.None)
                {
                    AddError(
                        diagnostics,
                        "LVWS1303",
                        $"$.widgets.{id}.parentRule",
                        $"Parent '{parentId}' does not accept children.");
                }
            }
        }
        if (!hasRoot)
        {
            AddError(diagnostics, "LVWS1304", "$.widgets", "At least one widget must be permitted as a root.");
        }
    }

    private static void ValidateXmlIdentities(
        IEnumerable<WidgetDefinition> widgets,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, WidgetDefinition> elements = new(StringComparer.Ordinal);
        Dictionary<string, WidgetDefinition> classes = new(StringComparer.Ordinal);
        foreach (WidgetDefinition widget in widgets)
        {
            AddXmlIdentity(elements, widget.Xml.Element, widget, "element", diagnostics);
            foreach (string alias in widget.Xml.ElementAliases)
            {
                AddXmlIdentity(elements, alias, widget, "element alias", diagnostics);
            }
            AddXmlIdentity(classes, widget.Xml.LegacyClass, widget, "legacy class", diagnostics);
            foreach (string alias in widget.Xml.LegacyClassAliases)
            {
                AddXmlIdentity(classes, alias, widget, "legacy class alias", diagnostics);
            }
        }
    }

    private static void AddXmlIdentity(
        Dictionary<string, WidgetDefinition> identities,
        string name,
        WidgetDefinition widget,
        string kind,
        List<ProfileDiagnostic> diagnostics)
    {
        if (identities.TryGetValue(name, out WidgetDefinition? previous) &&
            (previous.Xml.DiscriminatorPropertyId is null || widget.Xml.DiscriminatorPropertyId is null))
        {
            AddError(
                diagnostics,
                "LVWS1305",
                "$.widgets",
                $"XML {kind} '{name}' is ambiguous between '{previous.Id}' and '{widget.Id}'.");
            return;
        }
        identities.TryAdd(name, widget);
    }

    private static void ValidateXml(
        WidgetXmlContract xml,
        string widgetPath,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!IsXmlName(xml.Element, allowWildcard: false) || !IsLegacyClass(xml.LegacyClass))
        {
            AddError(
                diagnostics,
                "LVWS1401",
                $"{widgetPath}.xml",
                "The concise XML element or legacy class name is invalid.");
        }
        ValidateUniqueNames(
            xml.ElementAliases,
            $"{widgetPath}.xml.elementAliases",
            "LVWS1402",
            diagnostics,
            IsXmlAlias);
        ValidateUniqueNames(
            xml.LegacyClassAliases,
            $"{widgetPath}.xml.legacyClassAliases",
            "LVWS1403",
            diagnostics,
            IsLegacyClass);
    }

    private static HashSet<string> ValidateReferences(
        IEnumerable<string> references,
        Dictionary<string, WidgetPropertyDefinition> properties,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (string propertyId in references)
        {
            if (!result.Add(propertyId))
            {
                AddError(diagnostics, "LVWS1501", path, $"Property '{propertyId}' is duplicated.");
            }
            else if (!properties.ContainsKey(propertyId))
            {
                AddError(diagnostics, "LVWS1502", path, $"Property '{propertyId}' does not exist.");
            }
        }
        return result;
    }

    private static void ValidateActions(
        IEnumerable<WidgetActionDefinition> actions,
        string widgetPath,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (WidgetActionDefinition action in actions)
        {
            if (!ProfileRules.IsValidIdentifier(action.Id) || !ids.Add(action.Id) ||
                string.IsNullOrWhiteSpace(action.DisplayName) ||
                !ProfileRules.IsValidIdentifier(action.Event) ||
                string.IsNullOrWhiteSpace(action.RuntimeActionSource))
            {
                AddError(
                    diagnostics,
                    "LVWS1601",
                    $"{widgetPath}.actions",
                    $"Action '{action.Id}' is invalid or duplicated.");
            }
        }
    }

    private static void ValidateBindings(
        IEnumerable<WidgetBindingDefinition> bindings,
        HashSet<string> widgetProperties,
        Dictionary<string, WidgetPropertyDefinition> properties,
        string widgetPath,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (WidgetBindingDefinition binding in bindings)
        {
            bool validProperty = widgetProperties.Contains(binding.PropertyId) &&
                properties.TryGetValue(binding.PropertyId, out WidgetPropertyDefinition? property) &&
                property.Kind == WidgetPropertyKind.BindingId;
            if (!ProfileRules.IsValidIdentifier(binding.Id) || !ids.Add(binding.Id) ||
                string.IsNullOrWhiteSpace(binding.DisplayName) || !validProperty)
            {
                AddError(
                    diagnostics,
                    "LVWS1602",
                    $"{widgetPath}.bindings",
                    $"Binding '{binding.Id}' is invalid, duplicated, or references a non-binding property.");
            }
        }
    }

    private static void ValidateStates(
        IEnumerable<WidgetStateDefinition> states,
        string widgetPath,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (WidgetStateDefinition state in states)
        {
            if (!ProfileRules.IsValidIdentifier(state.Id) || !ids.Add(state.Id) ||
                string.IsNullOrWhiteSpace(state.DisplayName))
            {
                AddError(
                    diagnostics,
                    "LVWS1603",
                    $"{widgetPath}.states",
                    $"State '{state.Id}' is invalid or duplicated.");
            }
        }
    }

    private static void ValidateResources(
        IEnumerable<WidgetResourceDependency> resources,
        HashSet<string> widgetProperties,
        Dictionary<string, WidgetPropertyDefinition> properties,
        string widgetPath,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<(WidgetResourceKind Kind, string Property)> seen = [];
        foreach (WidgetResourceDependency resource in resources)
        {
            bool kindMatches = false;
            if (widgetProperties.Contains(resource.PropertyId) &&
                properties.TryGetValue(resource.PropertyId, out WidgetPropertyDefinition? property))
            {
                kindMatches = ResourceKindMatches(resource.Kind, property.Kind);
            }
            if (!seen.Add((resource.Kind, resource.PropertyId)) || !kindMatches)
            {
                AddError(
                    diagnostics,
                    "LVWS1604",
                    $"{widgetPath}.resources",
                    $"Resource dependency '{resource.Kind}:{resource.PropertyId}' is invalid or duplicated.");
            }
        }
    }

    private static void ValidateMigrations(
        WidgetCatalog catalog,
        Dictionary<string, WidgetPropertyDefinition> properties,
        Dictionary<string, WidgetDefinition> widgets,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<(int From, WidgetMigrationKind Kind, string? Widget, string Id)> seen = [];
        int index = 0;
        foreach (WidgetMigration migration in catalog.Migrations)
        {
            string path = $"$.migrations[{index}]";
            bool versionsValid = migration.FromCompatibilityVersion >= 1 &&
                migration.ToCompatibilityVersion == migration.FromCompatibilityVersion + 1 &&
                migration.ToCompatibilityVersion <= catalog.CompatibilityVersion;
            if (!versionsValid)
            {
                AddError(
                    diagnostics,
                    "LVWS1701",
                    path,
                    "A migration must advance exactly one compatibility version and end at a known version.");
            }
            if (!seen.Add((migration.FromCompatibilityVersion, migration.Kind, migration.WidgetId, migration.FromId)))
            {
                AddError(diagnostics, "LVWS1702", path, "The migration source is duplicated.");
            }
            bool idsValid = ProfileRules.IsValidIdentifier(migration.FromId) &&
                ProfileRules.IsValidIdentifier(migration.ToId);
            if (migration.Kind == WidgetMigrationKind.RenameWidget)
            {
                idsValid &= migration.WidgetId is null && widgets.ContainsKey(migration.ToId);
            }
            else
            {
                idsValid &= (migration.WidgetId is null || widgets.ContainsKey(migration.WidgetId)) &&
                    properties.ContainsKey(migration.ToId);
            }
            if (!idsValid)
            {
                AddError(
                    diagnostics,
                    "LVWS1703",
                    path,
                    "The migration IDs or destination are invalid for the selected migration kind.");
            }
            ++index;
        }
    }

    private static void ValidateDefault(
        WidgetPropertyDefinition property,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        JsonElement value = property.DefaultValue;
        if (value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        bool valid = property.Kind switch
        {
            WidgetPropertyKind.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            WidgetPropertyKind.SignedInteger or
            WidgetPropertyKind.UnsignedInteger or
            WidgetPropertyKind.Duration or
            WidgetPropertyKind.ActionId => ValidateInteger(value, property),
            WidgetPropertyKind.Enum =>
                value.ValueKind == JsonValueKind.String &&
                property.EnumValues.Contains(value.GetString(), StringComparer.Ordinal),
            WidgetPropertyKind.Color =>
                value.ValueKind == JsonValueKind.String && IsColor(value.GetString()),
            WidgetPropertyKind.Text or
            WidgetPropertyKind.BoundedText or
            WidgetPropertyKind.BindingId or
            WidgetPropertyKind.Font or
            WidgetPropertyKind.Bitmap => value.ValueKind == JsonValueKind.String,
            WidgetPropertyKind.Rectangle => IsRectangle(value),
            WidgetPropertyKind.AnimationFrames => IsStringArray(value),
            _ => false
        };
        if (!valid)
        {
            AddError(
                diagnostics,
                "LVWS1801",
                $"{path}.defaultValue",
                $"The default does not satisfy the {property.Kind} property contract or range.");
        }
    }

    private static bool ValidateInteger(JsonElement value, WidgetPropertyDefinition property)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long number))
        {
            return false;
        }
        if (property.Kind is WidgetPropertyKind.UnsignedInteger or
            WidgetPropertyKind.Duration or
            WidgetPropertyKind.ActionId && number < 0)
        {
            return false;
        }
        return (!property.Minimum.HasValue || number >= property.Minimum.Value) &&
            (!property.Maximum.HasValue || number <= property.Maximum.Value);
    }

    private static bool IsRectangle(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 4)
        {
            return false;
        }
        JsonElement.ArrayEnumerator items = value.EnumerateArray();
        int index = 0;
        foreach (JsonElement item in items)
        {
            if (!item.TryGetInt32(out int number) || index >= 2 && number < 1)
            {
                return false;
            }
            ++index;
        }
        return true;
    }

    private static bool IsStringArray(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Array &&
            value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String);
    }

    private static bool IsColor(string? value)
    {
        return value is { Length: 9 } && value[0] == '#' &&
            value.AsSpan(1).ToString().All(character =>
                character is >= '0' and <= '9' or >= 'A' and <= 'F');
    }

    private static bool IsNumeric(WidgetPropertyKind kind)
    {
        return kind is WidgetPropertyKind.SignedInteger or
            WidgetPropertyKind.UnsignedInteger or
            WidgetPropertyKind.Duration or
            WidgetPropertyKind.ActionId;
    }

    private static bool ResourceKindMatches(WidgetResourceKind resource, WidgetPropertyKind property)
    {
        return resource switch
        {
            WidgetResourceKind.Font => property == WidgetPropertyKind.Font,
            WidgetResourceKind.Bitmap => property == WidgetPropertyKind.Bitmap,
            WidgetResourceKind.AnimationFrames => property == WidgetPropertyKind.AnimationFrames,
            WidgetResourceKind.MutableTextBuffer => property == WidgetPropertyKind.BindingId,
            _ => false
        };
    }

    private static void ValidateIdentity(
        string id,
        string displayName,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!ProfileRules.IsValidIdentifier(id))
        {
            AddError(diagnostics, "LVWS1901", $"{path}.id", $"ID '{id}' is invalid.");
        }
        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName.Length > ProfileRules.MaximumDisplayNameLength)
        {
            AddError(
                diagnostics,
                "LVWS1902",
                $"{path}.displayName",
                "The display name is empty or exceeds the bounded length.");
        }
    }

    private static void ValidateUniqueNames(
        IEnumerable<string> values,
        string path,
        string code,
        List<ProfileDiagnostic> diagnostics,
        Func<string, bool> validator)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (!validator(value) || !seen.Add(value))
            {
                AddError(diagnostics, code, path, $"Value '{value}' is invalid or duplicated.");
            }
        }
    }

    private static void ValidateDistinctEnums<TEnum>(
        IEnumerable<TEnum> values,
        string path,
        string code,
        List<ProfileDiagnostic> diagnostics)
        where TEnum : struct, Enum
    {
        HashSet<TEnum> seen = [];
        foreach (TEnum value in values)
        {
            if (!seen.Add(value))
            {
                AddError(diagnostics, code, path, $"Capability '{value}' is duplicated.");
            }
        }
    }

    private static bool IsXmlAlias(string value)
    {
        return IsXmlName(value, allowWildcard: false);
    }

    private static bool IsXmlName(string value, bool allowWildcard)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        int length = allowWildcard && value[^1] == '*' ? value.Length - 1 : value.Length;
        if (length == 0 || !char.IsAsciiLetter(value[0]))
        {
            return false;
        }
        return value.AsSpan(0, length).ToString().All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    }

    private static bool IsLegacyClass(string value)
    {
        return !string.IsNullOrEmpty(value) && value.All(character =>
            character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
    }

    private static bool IsCodeIdentifier(string value)
    {
        return !string.IsNullOrEmpty(value) &&
            (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }

    private static bool IsCppType(string value)
    {
        return !string.IsNullOrEmpty(value) && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or ':');
    }

    private static bool IsCppMember(string value)
    {
        const string specPrefix = "spec.";
        const string statePrefix = "state.";
        int prefixLength = 0;
        if (value.StartsWith(specPrefix, StringComparison.Ordinal))
        {
            prefixLength = specPrefix.Length;
        }
        else if (value.StartsWith(statePrefix, StringComparison.Ordinal))
        {
            prefixLength = statePrefix.Length;
        }
        return prefixLength != 0 && value.AsSpan(prefixLength).ToString().All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '_' or '.');
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
