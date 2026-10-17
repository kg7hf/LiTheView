// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public static class ResourceManifestLoader
{
    public static ResourceManifestLoadResult LoadFile(string path)
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
                    "LVRM0001",
                    ProfileDiagnosticSeverity.Error,
                    path,
                    $"Unable to read the resource manifest: {exception.Message}"));
        }
    }

    public static ResourceManifestLoadResult Load(ReadOnlyMemory<byte> utf8)
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
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVRM0002",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Invalid JSON: {exception.Message}"));
        }

        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        ResourceManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ResourceManifest>(utf8.Span, ProfileJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVRM0003",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Resource-manifest structure is invalid: {exception.Message}"));
            return Failed(diagnostics);
        }

        if (manifest is null)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVRM0004",
                    ProfileDiagnosticSeverity.Error,
                    "$",
                    "The resource manifest is empty."));
            return Failed(diagnostics);
        }

        diagnostics.AddRange(ResourceManifestValidator.Validate(manifest));
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        byte[] canonical = ResourceManifestCanonicalizer.Canonicalize(manifest);
        return new ResourceManifestLoadResult
        {
            Manifest = manifest,
            CanonicalUtf8 = canonical,
            Sha256 = ResourceManifestCanonicalizer.ComputeSha256(canonical),
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
                            "LVRM0005",
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

    private static ResourceManifestLoadResult Failed(params ProfileDiagnostic[] diagnostics)
    {
        return Failed((IReadOnlyList<ProfileDiagnostic>)diagnostics);
    }

    private static ResourceManifestLoadResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new ResourceManifestLoadResult { Diagnostics = diagnostics };
    }

    private static ResourceManifestLoadResult ManifestTooLarge(string path, long byteCount)
    {
        return Failed(
            new ProfileDiagnostic(
                "LVRM0007",
                ProfileDiagnosticSeverity.Error,
                path,
                $"The resource manifest contains {byteCount} bytes; the limit is " +
                    $"{ProfileRules.MaximumSerializedByteCount} bytes."));
    }
}
