// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;

namespace LitheView.Designer.Core.Profiles;

internal static class DisplayDefinitionJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static T? Read<T>(ReadOnlyMemory<byte> utf8, string prefix,
                            List<ProfileDiagnostic> diagnostics) where T : class
    {
        if (utf8.Length > ProfileRules.MaximumSerializedByteCount)
        {
            Error(diagnostics, prefix + "0007", "$", "Profile exceeds the bounded serialized size.");
            return null;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8, ProfileJson.DocumentOptions);
            FindDuplicates(document.RootElement, "$", prefix, diagnostics);
            if (diagnostics.Count != 0)
            {
                return null;
            }
            T? value = JsonSerializer.Deserialize<T>(utf8.Span, Options);
            if (value is null)
            {
                Error(diagnostics, prefix + "0004", "$", "A profile object is required.");
            }
            return value;
        }
        catch (JsonException exception)
        {
            Error(diagnostics, prefix + "0003", exception.Path ?? "$",
                  $"Invalid profile structure: {exception.Message}");
            return null;
        }
    }

    public static byte[]? ReadFile(string path, string prefix, List<ProfileDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > ProfileRules.MaximumSerializedByteCount)
            {
                Error(diagnostics, prefix + "0007", path, "Profile exceeds the bounded serialized size.");
                return null;
            }
            byte[] bytes = GC.AllocateUninitializedArray<byte>((int)stream.Length);
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Error(diagnostics, prefix + "0001", path, $"Unable to read profile: {exception.Message}");
            return null;
        }
    }

    public static void Error(List<ProfileDiagnostic> diagnostics, string code, string path, string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(ProfileJson.SerializerOptions);
        options.Converters.Add(new StrictCamelCaseEnumConverter<DisplayAddressSpace>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<DisplayChannelModel>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<DisplayFieldChannel>());
        options.Converters.Add(new StrictCamelCaseEnumConverter<DisplaySourceStatus>());
        return options;
    }

    private static void FindDuplicates(JsonElement element, string path, string prefix,
                                       List<ProfileDiagnostic> diagnostics)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string child = $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    Error(diagnostics, prefix + "0005", child, "Duplicate JSON property.");
                }
                FindDuplicates(property.Value, child, prefix, diagnostics);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                {
                    Error(diagnostics, prefix + "0003", $"{path}[{index}]", "Null array elements are invalid.");
                }
                FindDuplicates(item, $"{path}[{index}]", prefix, diagnostics);
                ++index;
            }
        }
    }
}
