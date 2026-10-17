// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

public static class DesignerProjectLoader
{
    public static DesignerProjectLoadResult LoadFile(string path)
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
                return ManifestTooLarge(path, stream.Length);
            }

            byte[] utf8 = GC.AllocateUninitializedArray<byte>((int)stream.Length);
            stream.ReadExactly(utf8);
            return Load(utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(
                new ProfileDiagnostic(
                    "LVPM0001",
                    ProfileDiagnosticSeverity.Error,
                    path,
                    $"Unable to read the project manifest: {exception.Message}"));
        }
    }

    public static DesignerProjectLoadResult Load(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length > ProfileRules.MaximumSerializedByteCount)
        {
            return ManifestTooLarge("$", utf8.Length);
        }

        List<ProfileDiagnostic> diagnostics = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8, ProfileJson.DocumentOptions);
            FindDuplicateProperties(document.RootElement, "$", diagnostics);
            ValidateVersionedProperties(document.RootElement, diagnostics);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVPM0002",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Invalid JSON: {exception.Message}"));
        }

        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        DesignerProjectManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<DesignerProjectManifest>(
                utf8.Span,
                ProfileJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVPM0003",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Project-manifest structure is invalid: {exception.Message}"));
            return Failed(diagnostics);
        }

        if (manifest is null)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVPM0004",
                    ProfileDiagnosticSeverity.Error,
                    "$",
                    "The project manifest is empty."));
            return Failed(diagnostics);
        }

        diagnostics.AddRange(DesignerProjectValidator.Validate(manifest));
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        byte[] canonical = DesignerProjectCanonicalizer.Canonicalize(manifest);
        return new DesignerProjectLoadResult
        {
            Manifest = manifest,
            CanonicalUtf8 = canonical,
            Sha256 = DesignerProjectCanonicalizer.ComputeSha256(canonical),
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
                            "LVPM0005",
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

    private static void ValidateVersionedProperties(JsonElement root, List<ProfileDiagnostic> diagnostics)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out JsonElement version) ||
            version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) ||
            number is not (1 or 3))
        {
            return;
        }
        JsonElement compatibility = root.TryGetProperty("schemaCompatibility", out JsonElement value)
            ? value
            : default;
        if (number == 3)
        {
            if (HasProperty(root, "surfaceProfile") || HasProperty(compatibility, "displaySurface"))
            {
                diagnostics.Add(new("LVPM1203", ProfileDiagnosticSeverity.Error, "$",
                    "Version-3 native display projects do not contain editing-surface fields, including null placeholders."));
            }
            return;
        }
        bool hasProductFields = HasProperty(root, "displayDefinition") || HasProperty(root, "logicalInput") ||
            HasProperty(compatibility, "displayDefinition") || HasProperty(compatibility, "logicalInput");
        if (hasProductFields)
        {
            diagnostics.Add(new("LVPM1202", ProfileDiagnosticSeverity.Error, "$",
                "Version-1 projects do not contain product-snapshot fields, including null placeholders."));
        }
    }

    private static bool HasProperty(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private static DesignerProjectLoadResult Failed(params ProfileDiagnostic[] diagnostics)
    {
        return Failed((IReadOnlyList<ProfileDiagnostic>)diagnostics);
    }

    private static DesignerProjectLoadResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new DesignerProjectLoadResult { Diagnostics = diagnostics };
    }

    private static DesignerProjectLoadResult ManifestTooLarge(string path, long byteCount)
    {
        return Failed(
            new ProfileDiagnostic(
                "LVPM0007",
                ProfileDiagnosticSeverity.Error,
                path,
                $"The project manifest contains {byteCount} bytes; the limit is " +
                    $"{ProfileRules.MaximumSerializedByteCount} bytes."));
    }
}
