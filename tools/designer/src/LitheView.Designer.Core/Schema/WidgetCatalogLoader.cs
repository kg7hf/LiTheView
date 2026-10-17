// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Schema;

public static class WidgetCatalogLoader
{
    public static WidgetCatalogLoadResult LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            if (stream.Length > ProfileRules.MaximumSerializedByteCount)
            {
                return TooLarge(path, stream.Length);
            }
            byte[] utf8 = GC.AllocateUninitializedArray<byte>((int)stream.Length);
            stream.ReadExactly(utf8);
            return Load(utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(
                new ProfileDiagnostic(
                    "LVWS0001",
                    ProfileDiagnosticSeverity.Error,
                    path,
                    $"Unable to read the widget catalog: {exception.Message}"));
        }
    }

    public static WidgetCatalogLoadResult Load(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length > ProfileRules.MaximumSerializedByteCount)
        {
            return TooLarge("$", utf8.Length);
        }

        List<ProfileDiagnostic> diagnostics = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8, ProfileJson.DocumentOptions);
            FindDuplicateProperties(document.RootElement, "$", diagnostics);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVWS0002",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Invalid JSON: {exception.Message}"));
        }
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        WidgetCatalog? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<WidgetCatalog>(utf8.Span, ProfileJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVWS0003",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Widget catalog structure is invalid: {exception.Message}"));
            return Failed(diagnostics);
        }
        if (catalog is null)
        {
            return Failed(
                new ProfileDiagnostic(
                    "LVWS0004",
                    ProfileDiagnosticSeverity.Error,
                    "$",
                    "The widget catalog is empty."));
        }

        diagnostics.AddRange(WidgetCatalogValidator.Validate(catalog));
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }
        byte[] canonical = WidgetCatalogCanonicalizer.Canonicalize(catalog);
        return new WidgetCatalogLoadResult
        {
            Catalog = catalog,
            CanonicalUtf8 = canonical,
            Sha256 = WidgetCatalogCanonicalizer.ComputeSha256(canonical),
            Diagnostics = diagnostics
        };
    }

    private static void FindDuplicateProperties(
        JsonElement element,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string propertyPath = $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    diagnostics.Add(
                        new ProfileDiagnostic(
                            "LVWS0005",
                            ProfileDiagnosticSeverity.Error,
                            propertyPath,
                            $"Property '{property.Name}' is duplicated."));
                }
                FindDuplicateProperties(property.Value, propertyPath, diagnostics);
            }
            return;
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                FindDuplicateProperties(item, $"{path}[{index}]", diagnostics);
                ++index;
            }
        }
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private static WidgetCatalogLoadResult Failed(params ProfileDiagnostic[] diagnostics)
    {
        return Failed((IReadOnlyList<ProfileDiagnostic>)diagnostics);
    }

    private static WidgetCatalogLoadResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new WidgetCatalogLoadResult { Diagnostics = diagnostics };
    }

    private static WidgetCatalogLoadResult TooLarge(string path, long byteCount)
    {
        return Failed(
            new ProfileDiagnostic(
                "LVWS0007",
                ProfileDiagnosticSeverity.Error,
                path,
                $"The widget catalog contains {byteCount} bytes; the limit is " +
                    $"{ProfileRules.MaximumSerializedByteCount} bytes."));
    }
}
