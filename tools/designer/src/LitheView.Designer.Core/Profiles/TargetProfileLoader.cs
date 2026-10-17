// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;

namespace LitheView.Designer.Core.Profiles;

public static class TargetProfileLoader
{
    public static TargetProfileLoadResult LoadFile(string path)
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
                return ProfileTooLarge(path, stream.Length);
            }

            byte[] utf8 = GC.AllocateUninitializedArray<byte>((int)stream.Length);
            stream.ReadExactly(utf8);
            return Load(utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(
                new ProfileDiagnostic(
                    "LVTP0001",
                    ProfileDiagnosticSeverity.Error,
                    path,
                    $"Unable to read the target profile: {exception.Message}"));
        }
    }

    public static TargetProfileLoadResult Load(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length > ProfileRules.MaximumSerializedByteCount)
        {
            return ProfileTooLarge("$", utf8.Length);
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
                    "LVTP0002",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Invalid JSON: {exception.Message}"));
        }

        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        TargetProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<TargetProfile>(utf8.Span, ProfileJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVTP0003",
                    ProfileDiagnosticSeverity.Error,
                    exception.Path ?? "$",
                    $"Target-profile structure is invalid: {exception.Message}"));
            return Failed(diagnostics);
        }

        if (profile is null)
        {
            diagnostics.Add(
                new ProfileDiagnostic(
                    "LVTP0004",
                    ProfileDiagnosticSeverity.Error,
                    "$",
                    "The target profile is empty."));
            return Failed(diagnostics);
        }

        diagnostics.AddRange(TargetProfileValidator.Validate(profile));
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        byte[] canonical = TargetProfileCanonicalizer.Canonicalize(profile);
        return new TargetProfileLoadResult
        {
            Profile = profile,
            CanonicalUtf8 = canonical,
            Sha256 = TargetProfileCanonicalizer.ComputeSha256(canonical),
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
                            "LVTP0005",
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

    private static TargetProfileLoadResult Failed(params ProfileDiagnostic[] diagnostics)
    {
        return Failed((IReadOnlyList<ProfileDiagnostic>)diagnostics);
    }

    private static TargetProfileLoadResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new TargetProfileLoadResult { Diagnostics = diagnostics };
    }

    private static TargetProfileLoadResult ProfileTooLarge(string path, long byteCount)
    {
        return Failed(
            new ProfileDiagnostic(
                "LVTP0007",
                ProfileDiagnosticSeverity.Error,
                path,
                $"The target profile contains {byteCount} bytes; the limit is " +
                    $"{ProfileRules.MaximumSerializedByteCount} bytes."));
    }
}
