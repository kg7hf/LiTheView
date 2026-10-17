// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LitheView.Designer.Core.Schema;

public enum WidgetPropertyKind
{
    Boolean,
    SignedInteger,
    UnsignedInteger,
    Enum,
    Color,
    Text,
    BoundedText,
    Rectangle,
    Duration,
    ActionId,
    BindingId,
    Font,
    Bitmap,
    AnimationFrames
}

public enum WidgetChildPolicy
{
    None,
    Many
}

public enum WidgetInputCapability
{
    Keys,
    PointerInput,
    TextInput,
    ElapsedTime
}

public enum WidgetResourceKind
{
    Font,
    Bitmap,
    AnimationFrames,
    MutableTextBuffer
}

public enum WidgetBindingDirection
{
    OneWay,
    TwoWay
}

public enum WidgetMigrationKind
{
    RenameWidget,
    RenameProperty
}

public enum WidgetXmlTransform
{
    Direct,
    InvertBoolean,
    ConsecutiveSequence,
    TriStateBoolean
}

public sealed record WidgetCatalog
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public required int SchemaVersion { get; init; }

    public required int CompatibilityVersion { get; init; }

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required WidgetPropertyDefinition[] Properties { get; init; }

    public required WidgetDefinition[] Widgets { get; init; }

    public required WidgetMigration[] Migrations { get; init; }
}

public sealed record WidgetPropertyDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required WidgetPropertyKind Kind { get; init; }

    public required string XmlName { get; init; }

    public required string[] XmlAliases { get; init; }

    public required WidgetXmlTransform XmlTransform { get; init; }

    public required string CppMember { get; init; }

    public required JsonElement DefaultValue { get; init; }

    public long? Minimum { get; init; }

    public long? Maximum { get; init; }

    public string? Units { get; init; }

    public required bool Required { get; init; }

    public required string[] EnumValues { get; init; }

    public required string Documentation { get; init; }
}

public sealed record WidgetDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string RuntimeKind { get; init; }

    public required string CppType { get; init; }

    public required string PaletteGroup { get; init; }

    public required string Help { get; init; }

    public required WidgetGeometry DefaultGeometry { get; init; }

    public required WidgetXmlContract Xml { get; init; }

    public required WidgetParentRule ParentRule { get; init; }

    public required WidgetChildPolicy ChildPolicy { get; init; }

    public required string[] PropertyIds { get; init; }

    public required WidgetActionDefinition[] Actions { get; init; }

    public required WidgetBindingDefinition[] Bindings { get; init; }

    public required WidgetStateDefinition[] States { get; init; }

    public required WidgetInputCapability[] InputCapabilities { get; init; }

    public required WidgetResourceDependency[] Resources { get; init; }
}

public sealed record WidgetGeometry
{
    public required int Width { get; init; }

    public required int Height { get; init; }
}

public sealed record WidgetXmlContract
{
    public required string Element { get; init; }

    public required string[] ElementAliases { get; init; }

    public required string LegacyClass { get; init; }

    public required string[] LegacyClassAliases { get; init; }

    public string? DiscriminatorPropertyId { get; init; }
}

public sealed record WidgetParentRule
{
    public required bool RootAllowed { get; init; }

    public required string[] AllowedParentIds { get; init; }
}

public sealed record WidgetActionDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Event { get; init; }

    public required string RuntimeActionSource { get; init; }
}

public sealed record WidgetBindingDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string PropertyId { get; init; }

    public required WidgetBindingDirection Direction { get; init; }
}

public sealed record WidgetStateDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }
}

public sealed record WidgetResourceDependency
{
    public required WidgetResourceKind Kind { get; init; }

    public required string PropertyId { get; init; }

    public required bool Required { get; init; }
}

public sealed record WidgetMigration
{
    public required int FromCompatibilityVersion { get; init; }

    public required int ToCompatibilityVersion { get; init; }

    public required WidgetMigrationKind Kind { get; init; }

    public string? WidgetId { get; init; }

    public required string FromId { get; init; }

    public required string ToId { get; init; }
}
