// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Documents;

public static class FormDocumentLimits
{
    public const int MaximumThemeColors = 32;
    public const uint UnspecifiedStyleStateColor = 0xffc35aa5U;
}

public enum FormImportMode
{
    Strict,
    Compatibility
}

public enum FormReferenceKind
{
    Action,
    Binding,
    Font,
    Bitmap
}

public enum FormResourceKind
{
    Font,
    Bitmap
}

public enum FormBindingKind
{
    MutableText
}

public readonly record struct FormRectangle(int X, int Y, int Width, int Height);

public abstract record FormPropertyValue(string PropertyId);

public sealed record FormBooleanValue(string PropertyId, bool Value) : FormPropertyValue(PropertyId);

public sealed record FormSignedIntegerValue(string PropertyId, long Value) :
    FormPropertyValue(PropertyId);

public sealed record FormUnsignedIntegerValue(string PropertyId, ulong Value) :
    FormPropertyValue(PropertyId);

public sealed record FormTextValue(string PropertyId, string Value) : FormPropertyValue(PropertyId);

public sealed record FormRectangleValue(string PropertyId, FormRectangle Value) :
    FormPropertyValue(PropertyId);

public sealed record FormColorValue : FormPropertyValue
{
    public FormColorValue(string propertyId, uint encodedColor) : base(propertyId)
    {
        EncodedColor = encodedColor;
    }

    public FormColorValue(string propertyId, string themeTokenId) : base(propertyId)
    {
        ThemeTokenId = themeTokenId;
    }

    public uint? EncodedColor { get; }

    public string? ThemeTokenId { get; }
}

public sealed record FormReferenceValue(
    string PropertyId,
    FormReferenceKind ReferenceKind,
    string ReferenceId) : FormPropertyValue(PropertyId);

public sealed record FormReferenceListValue(
    string PropertyId,
    FormReferenceKind ReferenceKind,
    string[] ReferenceIds) : FormPropertyValue(PropertyId);

public sealed record FormExtensionProperty
{
    public required string Name { get; init; }

    public required string Value { get; init; }
}

public sealed record FormComponent
{
    public required string ComponentId { get; init; }

    public required string Name { get; init; }

    public required string WidgetId { get; init; }

    public required string? ParentComponentId { get; init; }

    public required int SiblingOrder { get; init; }

    public required FormPropertyValue[] Properties { get; init; }

    public required FormExtensionProperty[] HistoricalAttributes { get; init; }
}

public sealed record FormAction
{
    public required string Id { get; init; }

    public required ushort RuntimeId { get; init; }

    public required string Symbol { get; init; }
}

public sealed record FormBinding
{
    public required string Id { get; init; }

    public required string RuntimeName { get; init; }

    public required string Symbol { get; init; }

    public required FormBindingKind Kind { get; init; }

    public required int? CapacityBytes { get; init; }
}

public sealed record FormResource
{
    public required string Id { get; init; }

    public required string RuntimeName { get; init; }

    public required string Symbol { get; init; }

    public required FormResourceKind Kind { get; init; }
}

public sealed record FormThemeColor
{
    public required string Id { get; init; }

    public required uint EncodedColor { get; init; }
}

public sealed record FormDocument
{
    public required int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string? SurfaceProfileId { get; init; }

    public required FormComponent[] Components { get; init; }

    public required FormAction[] Actions { get; init; }

    public required FormBinding[] Bindings { get; init; }

    public required FormResource[] Resources { get; init; }

    public required FormThemeColor[] ThemeColors { get; init; }
}

public sealed record ResolvedFormProperty
{
    public required WidgetPropertyDefinition Descriptor { get; init; }

    public required FormPropertyValue Value { get; init; }
}

public sealed record ResolvedFormComponent
{
    public required FormComponent Source { get; init; }

    public required WidgetDefinition Widget { get; init; }

    public required int Index { get; init; }

    public required int? ParentIndex { get; init; }

    public required string CppSymbol { get; init; }

    public required ResolvedFormProperty[] Properties { get; init; }
}

public sealed record ResolvedFormDocument
{
    public required FormDocument Source { get; init; }

    public required string CppSymbol { get; init; }

    public required ResolvedFormComponent[] Components { get; init; }

    public required IReadOnlyDictionary<string, FormAction> Actions { get; init; }

    public required IReadOnlyDictionary<string, FormBinding> Bindings { get; init; }

    public required IReadOnlyDictionary<string, FormResource> Resources { get; init; }

    public required IReadOnlyDictionary<string, FormThemeColor> ThemeColors { get; init; }
}
