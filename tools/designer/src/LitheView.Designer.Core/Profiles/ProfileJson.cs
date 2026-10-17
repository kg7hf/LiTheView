// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Profiles;

internal static class ProfileJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32
    };

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new StrictCamelCaseEnumConverter<SurfaceShape>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<SurfacePixelFormat>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<NativeColorModel>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<NativeTransferFormat>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<CoordinateOrigin>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<InputCapability>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<TargetArchitecture>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<OperatingEnvironment>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<RenderingMode>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ResourcePlacement>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ResourceFormat>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<BudgetEnforcement>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<WidgetPropertyKind>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<WidgetChildPolicy>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<WidgetInputCapability>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<WidgetResourceKind>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<WidgetBindingDirection>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<WidgetMigrationKind>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<WidgetXmlTransform>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ResourceKind>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ResourcePolicyAction>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ResourceOwnership>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ResourceLifetime>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ImageScaleMode>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ImageAlphaMode>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<ImageDitherMode>());
        return options;
    }
}

internal sealed class StrictCamelCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> sValuesByName =
        Enum.GetValues<TEnum>().ToDictionary(ToJsonName, StringComparer.Ordinal);

    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string value for {typeof(TEnum).Name}.");
        }

        string? text = reader.GetString();
        if (text is null || !sValuesByName.TryGetValue(text, out TEnum result))
        {
            throw new JsonException($"Unknown {typeof(TEnum).Name} value '{text}'.");
        }
        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToJsonName(value));
    }

    public static string ToJsonName(TEnum value)
    {
        string name = Enum.GetName(value) ?? throw new ArgumentOutOfRangeException(nameof(value));
        return JsonNamingPolicy.CamelCase.ConvertName(name);
    }
}
