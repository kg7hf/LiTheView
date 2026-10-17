// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.Documents;

namespace LitheView.Designer.Service;

internal static class Protocol
{
    public const int Version = 1;
    public const int MaximumXmlBytes = 1_048_576;
    public const int MaximumLineCharacters = 8 * MaximumXmlBytes;
    public const int MaximumRgbaBytes = 16 * MaximumXmlBytes;
    // Matches the core form limit; a multi-selection never exceeds the form's components.
    public const int MaximumEditComponents = 512;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
}

internal sealed record ServiceRequest
{
    public required int ProtocolVersion { get; init; }
    public required long Id { get; init; }
    public required string Method { get; init; }
    public required RequestParameters Params { get; init; }
}

internal sealed record RequestParameters
{
    public int? ProtocolVersion { get; init; }
    public string? ManifestPath { get; init; }
    public string? FormPath { get; init; }
    public string? Xml { get; init; }
    public int? DocumentVersion { get; init; }
    public EditOperation? Operation { get; init; }
    public ProjectCreationRequest? Project { get; init; }
    public string? LogicalInput { get; init; }
    public ProjectSetupRequest? Setup { get; init; }
}

internal sealed record ProjectSetupRequest
{
    public required string Snapshot { get; init; }
    public JsonElement? CustomSurface { get; init; }
    public NativeDisplayCreationRequest? CustomNativeDisplay { get; init; }
    public LogicalInputSetupRequest? Input { get; init; }
}

internal sealed record LogicalInputSetupRequest
{
    public required bool Pointer { get; init; }
    public required bool Keys { get; init; }
    public required bool Text { get; init; }
    public required bool Encoder { get; init; }
    public required bool Select { get; init; }
}

internal sealed record ProjectCreationRequest
{
    public required string ApplicationRoot { get; init; }
    public required string ProjectDirectory { get; init; }
    public required string DisplayName { get; init; }
    public required string ProjectId { get; init; }
    public string? SurfaceId { get; init; }
    public JsonElement? CustomSurface { get; init; }
    public string? NativeDisplayId { get; init; }
    public NativeDisplayCreationRequest? CustomNativeDisplay { get; init; }
    public required string LogicalInput { get; init; }
    public required string ComponentDirectory { get; init; }
    public required string BuildDirectory { get; init; }
}

internal sealed record NativeDisplayCreationRequest
{
    public required string Format { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Shape { get; init; }
    public LitheView.Designer.Core.Profiles.SafeAreaProfile? SafeArea { get; init; }
    public required string SourceReference { get; init; }
}

internal sealed record EditOperation
{
    public required string Kind { get; init; }
    public string? ComponentId { get; init; }
    public string? ParentId { get; init; }
    public string? WidgetId { get; init; }
    public FormRectangle? Frame { get; init; }
    public string? Text { get; init; }
    public string? PropertyId { get; init; }
    // A typed JSON value for a "property" edit: Boolean, integer, or string. Strings carry enum
    // values, text, "theme.*" token IDs, legacy "#AABBGGRR" colors, and resource/action/binding IDs.
    public JsonElement? Value { get; init; }
    // Several components for "delete" and "duplicate"; parent-relative frames for "frames".
    public string[]? ComponentIds { get; init; }
    public Dictionary<string, FormRectangle>? Frames { get; init; }
    public string? Name { get; init; }
    // "bringForward", "sendBackward", "bringToFront", or "sendToBack".
    public string? Order { get; init; }
    // Theme color token edits; Color uses the same legacy "#AABBGGRR" form as property values.
    public string? TokenId { get; init; }
    public string? NewTokenId { get; init; }
    public string? Color { get; init; }
    // Action assignment: ActionId null clears the assignment; NewAction creates and assigns.
    public string? WidgetActionId { get; init; }
    public string? ActionId { get; init; }
    public NewActionView? NewAction { get; init; }
}

internal sealed record NewActionView
{
    public required string Id { get; init; }
    public required int RuntimeId { get; init; }
    public required string Symbol { get; init; }
}

internal sealed record ServiceResponse(long Id, bool Ok, object? Result = null, ServiceError? Error = null)
{
    public int ProtocolVersion { get; } = Protocol.Version;
}

internal sealed record ServiceError(string Code, string Message);
internal sealed class ServiceException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

internal sealed record DiagnosticView(string Code, string Severity, string Message, string Path);
// Explicitly authored values only; an absent property uses its descriptor default.
internal sealed record PropertyValueView(string Id, object Value);
// PreviewHidden marks a tab-view page other than the selected one, or anything inside it; the
// runtime hides those components, so the canvas neither draws nor hit-tests them.
internal sealed record ComponentView(string Id, string Name, string WidgetId, string? ParentId,
    FormRectangle Frame, string? Text, PropertyValueView[] Properties, bool PreviewHidden,
    ActionAssignmentView[] Actions);
// ActionId is the declared form action the widget action reports, or null when unassigned.
internal sealed record ActionAssignmentView(string WidgetActionId, string? ActionId);
internal sealed record WidgetActionView(string Id, string Name, string Source);
// Container widgets accept children; AllowedParentIds lets the palette add into a selected container.
internal sealed record WidgetView(string Id, string Name, string Group, string Help, string[] Properties,
    string[] AllowedParentIds, bool Container, WidgetActionView[] Actions, ControlEventDescriptor[] Events, string EventPolicy);
internal sealed record PropertyDescriptorView(string Id, string Name, string Kind, string Category,
    JsonElement DefaultValue, long? Minimum, long? Maximum, string? Units, bool Required, string[] EnumValues,
    string Documentation, bool Editable);
// Choices declared by the form: theme colors, resources, actions, and bindings.
internal sealed record ChoiceView(string Id, string Name, string Kind, string? Color = null, int? RuntimeId = null,
    string? Symbol = null);
internal sealed record SurfaceView(int Width, int Height, string Format, string Shape);
internal sealed record InspectResult(int DocumentVersion, ComponentView[] Components, WidgetView[] Widgets,
    PropertyDescriptorView[] Properties, ChoiceView[] Choices, SurfaceView Surface,
    DiagnosticView[] Diagnostics, string? Xml = null, string? SelectedComponentId = null);
