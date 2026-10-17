// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Service;

/// <summary>
/// Converts schema-typed form property values to and from the JSON shapes the VS Code inspector
/// uses. Every edit still passes through the shared document session and strict resolver, so
/// this class only decodes syntax; it never decides whether a value is valid for a widget.
/// </summary>
internal static class PropertyValues
{
    private const string ThemePrefix = "theme.";
    private const int EncodedColorDigits = 8;

    public static bool IsEditable(WidgetPropertyDefinition definition)
    {
        // Geometry has its own frame operation; animation frame lists need a resource-list editor.
        return definition.Kind is not (WidgetPropertyKind.Rectangle or WidgetPropertyKind.AnimationFrames);
    }

    public static PropertyDescriptorView Describe(WidgetPropertyDefinition definition)
    {
        return new(
            definition.Id,
            definition.DisplayName,
            KindName(definition.Kind),
            Category(definition.Kind),
            definition.DefaultValue,
            definition.Minimum,
            definition.Maximum,
            definition.Units,
            definition.Required,
            definition.EnumValues,
            definition.Documentation,
            IsEditable(definition));
    }

    public static ChoiceView[] Choices(FormDocument form)
    {
        return
        [
            .. form.ThemeColors.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item =>
                new ChoiceView(item.Id, item.Id, "themeColor", FormatColor(item.EncodedColor))),
            .. form.Resources.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item =>
                new ChoiceView(item.Id, item.RuntimeName, item.Kind == FormResourceKind.Font ? "font" : "bitmap")),
            .. form.Actions.OrderBy(item => item.RuntimeId).Select(item =>
                new ChoiceView(item.Id, $"{item.Symbol} ({item.RuntimeId.ToString(CultureInfo.InvariantCulture)})", "action",
                    RuntimeId: item.RuntimeId, Symbol: item.Symbol)),
            .. form.Bindings.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item =>
                new ChoiceView(item.Id, item.RuntimeName, "binding"))
        ];
    }

    public static object ToView(FormPropertyValue value)
    {
        return value switch
        {
            FormBooleanValue boolean => boolean.Value,
            FormSignedIntegerValue signed => signed.Value,
            FormUnsignedIntegerValue unsignedValue => unsignedValue.Value,
            FormTextValue text => text.Value,
            FormColorValue { ThemeTokenId: string token } => token,
            FormColorValue { EncodedColor: uint encoded } => FormatColor(encoded),
            FormReferenceValue reference => reference.ReferenceId,
            FormReferenceListValue references => references.ReferenceIds,
            _ => throw new InvalidOperationException($"Property '{value.PropertyId}' has no inspector view.")
        };
    }

    public static FormPropertyValue Parse(WidgetPropertyDefinition definition, JsonElement value)
    {
        string id = definition.Id;
        return definition.Kind switch
        {
            WidgetPropertyKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                new FormBooleanValue(id, value.GetBoolean()),
            WidgetPropertyKind.SignedInteger when value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out long signed) => new FormSignedIntegerValue(id, signed),
            WidgetPropertyKind.UnsignedInteger or WidgetPropertyKind.Duration
                when value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out ulong unsignedValue) =>
                new FormUnsignedIntegerValue(id, unsignedValue),
            WidgetPropertyKind.Enum or WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText
                when value.ValueKind == JsonValueKind.String => new FormTextValue(id, value.GetString()!),
            WidgetPropertyKind.Color when value.ValueKind == JsonValueKind.String =>
                ParseColor(id, value.GetString()!),
            WidgetPropertyKind.Font when value.ValueKind == JsonValueKind.String =>
                new FormReferenceValue(id, FormReferenceKind.Font, value.GetString()!),
            WidgetPropertyKind.Bitmap when value.ValueKind == JsonValueKind.String =>
                new FormReferenceValue(id, FormReferenceKind.Bitmap, value.GetString()!),
            WidgetPropertyKind.ActionId when value.ValueKind == JsonValueKind.String =>
                new FormReferenceValue(id, FormReferenceKind.Action, value.GetString()!),
            WidgetPropertyKind.BindingId when value.ValueKind == JsonValueKind.String =>
                new FormReferenceValue(id, FormReferenceKind.Binding, value.GetString()!),
            _ => throw new ServiceException(
                "InvalidEdit",
                $"Property '{id}' requires a {KindName(definition.Kind)} value.")
        };
    }

    private static FormColorValue ParseColor(string propertyId, string text)
    {
        if (text.StartsWith(ThemePrefix, StringComparison.Ordinal))
        {
            return new FormColorValue(propertyId, text);
        }
        return new FormColorValue(propertyId, ParseEncodedColor(text));
    }

    /// <summary>Parses eight hexadecimal digits in the legacy encoding, with an optional # or 0x.</summary>
    public static uint ParseEncodedColor(string text)
    {
        string digits = text.StartsWith('#') ? text[1..]
            : text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..]
            : text;
        if (digits.Length != EncodedColorDigits ||
            !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint encoded))
        {
            throw new ServiceException(
                "InvalidEdit",
                $"Color '{text}' must be a theme.* token or eight hexadecimal digits in the legacy encoding.");
        }
        return encoded;
    }

    /// <summary>
    /// The declared form action a widget action reports: a referenced action, or for runtime-ID
    /// sources, the action with that runtime ID. Undeclared runtime IDs read as unassigned.
    /// </summary>
    public static string? AssignedAction(FormDocument form, FormComponent component, WidgetActionDefinition action)
    {
        FormPropertyValue? source = component.Properties.SingleOrDefault(item => item.PropertyId == action.RuntimeActionSource);
        return source switch
        {
            FormReferenceValue { ReferenceKind: FormReferenceKind.Action } reference => reference.ReferenceId,
            FormUnsignedIntegerValue { Value: > 0U } runtime =>
                form.Actions.SingleOrDefault(item => item.RuntimeId == runtime.Value)?.Id,
            _ => null
        };
    }

    private static string FormatColor(uint encoded)
    {
        return "#" + encoded.ToString("x8", CultureInfo.InvariantCulture);
    }

    private static string KindName(WidgetPropertyKind kind)
    {
        string name = kind.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Category(WidgetPropertyKind kind)
    {
        return kind switch
        {
            WidgetPropertyKind.Color => "Appearance",
            WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText => "Content",
            WidgetPropertyKind.Boolean or WidgetPropertyKind.Enum => "Behavior",
            WidgetPropertyKind.Font or WidgetPropertyKind.Bitmap or WidgetPropertyKind.AnimationFrames => "Resources",
            WidgetPropertyKind.ActionId or WidgetPropertyKind.BindingId => "Actions and bindings",
            _ => "Layout"
        };
    }
}
