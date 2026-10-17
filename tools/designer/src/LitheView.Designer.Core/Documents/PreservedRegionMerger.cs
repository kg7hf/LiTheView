// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.RegularExpressions;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public static partial class PreservedRegionMerger
{
    private const int MaximumSourceBytes = 4 * 1024 * 1024;

    private sealed record Region(string Id, int BodyStart, int BodyLength);

    public static PreservedRegionMergeResult Merge(
        ReadOnlyMemory<byte> generatedUtf8,
        ReadOnlyMemory<byte>? existingUtf8)
    {
        List<ProfileDiagnostic> diagnostics = [];
        if (generatedUtf8.Length > MaximumSourceBytes || existingUtf8?.Length > MaximumSourceBytes)
        {
            AddError(diagnostics, "LVFR1001", "$", "Generated integration files may not exceed 4 MiB.");
            return new PreservedRegionMergeResult { Diagnostics = diagnostics };
        }

        try
        {
            string generated = Decode(generatedUtf8.Span);
            Dictionary<string, Region>? generatedRegions = Parse(generated, "generated", diagnostics);
            if (generatedRegions is null)
            {
                return new PreservedRegionMergeResult { Diagnostics = diagnostics };
            }
            if (!existingUtf8.HasValue)
            {
                return new PreservedRegionMergeResult
                {
                    Utf8 = generatedUtf8.ToArray(),
                    Diagnostics = diagnostics
                };
            }

            string existing = Decode(existingUtf8.Value.Span);
            Dictionary<string, Region>? existingRegions = Parse(existing, "existing", diagnostics);
            if (existingRegions is null)
            {
                return new PreservedRegionMergeResult { Diagnostics = diagnostics };
            }
            string[] orphaned = [.. existingRegions.Keys.Except(generatedRegions.Keys, StringComparer.Ordinal)];
            if (orphaned.Length > 0)
            {
                AddError(
                    diagnostics,
                    "LVFR1002",
                    "$",
                    $"Existing file contains orphaned user-code region '{orphaned[0]}'.");
                return new PreservedRegionMergeResult { Diagnostics = diagnostics };
            }

            StringBuilder merged = new(generated.Length);
            int position = 0;
            foreach (Region target in generatedRegions.Values.OrderBy(item => item.BodyStart))
            {
                merged.Append(generated, position, target.BodyStart - position);
                if (existingRegions.TryGetValue(target.Id, out Region? source))
                {
                    merged.Append(existing, source.BodyStart, source.BodyLength);
                }
                else
                {
                    merged.Append(generated, target.BodyStart, target.BodyLength);
                }
                position = target.BodyStart + target.BodyLength;
            }
            merged.Append(generated, position, generated.Length - position);
            return new PreservedRegionMergeResult
            {
                Utf8 = Encode(merged.ToString()),
                Diagnostics = diagnostics
            };
        }
        catch (DecoderFallbackException exception)
        {
            AddError(diagnostics, "LVFR1003", "$", $"Integration source is not valid UTF-8: {exception.Message}");
            return new PreservedRegionMergeResult { Diagnostics = diagnostics };
        }
    }

    private static Dictionary<string, Region>? Parse(
        string source,
        string sourceName,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, Region> regions = new(StringComparer.Ordinal);
        string? activeId = null;
        int bodyStart = 0;
        int offset = 0;
        foreach (Match line in LineRegex().Matches(source))
        {
            string text = line.Groups["text"].Value.Trim();
            Match start = StartRegex().Match(text);
            Match end = EndRegex().Match(text);
            if (start.Success)
            {
                if (activeId is not null)
                {
                    AddError(diagnostics, "LVFR1101", sourceName, "User-code regions may not be nested.");
                    return null;
                }
                activeId = start.Groups["id"].Value;
                if (!FormSymbolRules.IsStableId(activeId) || regions.ContainsKey(activeId))
                {
                    AddError(
                        diagnostics,
                        "LVFR1102",
                        sourceName,
                        "User-code region IDs must be unique stable identifiers.");
                    return null;
                }
                bodyStart = line.Index + line.Length;
            }
            else if (end.Success)
            {
                if (activeId is null)
                {
                    AddError(diagnostics, "LVFR1103", sourceName, "User-code end marker has no start marker.");
                    return null;
                }
                if (activeId != end.Groups["id"].Value)
                {
                    AddError(
                        diagnostics,
                        "LVFR1106",
                        sourceName,
                        $"User-code region '{activeId}' ends as '{end.Groups["id"].Value}'.");
                    return null;
                }
                regions.Add(activeId, new Region(activeId, bodyStart, line.Index - bodyStart));
                activeId = null;
            }
            else if (text.Contains("LITHEVIEW USER CODE", StringComparison.Ordinal))
            {
                AddError(diagnostics, "LVFR1104", sourceName, "Malformed user-code marker encountered.");
                return null;
            }
            offset = line.Index + line.Length;
        }
        if (offset != source.Length || activeId is not null)
        {
            AddError(
                diagnostics,
                "LVFR1105",
                sourceName,
                activeId is null ? "Integration source could not be scanned completely." : "User-code region is not closed.");
            return null;
        }
        return regions;
    }

    private static string Decode(ReadOnlySpan<byte> utf8)
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(utf8);
    }

    private static byte[] Encode(string value)
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetBytes(value);
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }

    [GeneratedRegex("(?<text>[^\\r\\n]*)(?:\\r\\n|\\n|\\r|$)", RegexOptions.CultureInvariant)]
    private static partial Regex LineRegex();

    [GeneratedRegex(
        "^// LITHEVIEW USER CODE BEGIN (?<id>[^ ]+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex StartRegex();

    [GeneratedRegex("^// LITHEVIEW USER CODE END (?<id>[^ ]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex EndRegex();
}
