// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace LitheView.Designer.Core.Resources;

public sealed record FontRepertoire
{
    public required string Canonical { get; init; }

    public required int[] Codepoints { get; init; }

    public bool Contains(int codepoint)
    {
        return Array.BinarySearch(Codepoints, codepoint) >= 0;
    }
}

public static class FontRepertoireParser
{
    private const int MaximumCodepoint = 0x10ffff;
    private const int SurrogateFirst = 0xd800;
    private const int SurrogateLast = 0xdfff;
    private const int RequiredFallback = 0x003f;
    private const int MaximumGlyphCount = ushort.MaxValue;
    private const int MaximumSerializedLength = 4096;

    public static bool TryParse(
        string? value,
        out FontRepertoire? repertoire,
        out string? error)
    {
        repertoire = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "The repertoire must contain at least one U+XXXX code point or range.";
            return false;
        }
        if (value.Length > MaximumSerializedLength)
        {
            error = $"The repertoire exceeds the {MaximumSerializedLength}-character contract limit.";
            return false;
        }

        SortedSet<int> codepoints = [];
        foreach (string sourceToken in value.Split(','))
        {
            string token = sourceToken.Trim();
            string[] bounds = token.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length is < 1 or > 2 ||
                !TryParseCodepoint(bounds[0], out int first) ||
                (bounds.Length == 2 && !TryParseCodepoint(bounds[1], out _)))
            {
                error = $"Invalid repertoire token '{sourceToken}'. Use U+0020 or U+0020-U+007E.";
                return false;
            }
            int last = first;
            if (bounds.Length == 2)
            {
                _ = TryParseCodepoint(bounds[1], out last);
            }
            if (first > last || IntersectsSurrogateRange(first, last))
            {
                error = $"Invalid Unicode range '{sourceToken}'.";
                return false;
            }
            for (int codepoint = first; codepoint <= last; ++codepoint)
            {
                codepoints.Add(codepoint);
                if (codepoints.Count > MaximumGlyphCount)
                {
                    error = $"The repertoire exceeds the {MaximumGlyphCount:N0}-glyph limit.";
                    return false;
                }
            }
        }

        if (!codepoints.Contains(RequiredFallback))
        {
            error = "The repertoire must include U+003F for deterministic missing-glyph fallback.";
            return false;
        }

        int[] ordered = [.. codepoints];
        repertoire = new FontRepertoire
        {
            Canonical = FormatCanonical(ordered),
            Codepoints = ordered
        };
        return true;
    }

    private static bool TryParseCodepoint(string token, out int codepoint)
    {
        codepoint = 0;
        if (token.Length is < 6 or > 8 ||
            token[0] is not ('U' or 'u') ||
            token[1] != '+' ||
            !int.TryParse(token.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                out codepoint))
        {
            return false;
        }
        return codepoint is >= 0 and <= MaximumCodepoint &&
            codepoint is not (>= SurrogateFirst and <= SurrogateLast);
    }

    private static bool IntersectsSurrogateRange(int first, int last)
    {
        return first <= SurrogateLast && last >= SurrogateFirst;
    }

    private static string FormatCanonical(int[] codepoints)
    {
        List<string> ranges = [];
        int first = codepoints[0];
        int last = first;
        for (int index = 1; index < codepoints.Length; ++index)
        {
            int codepoint = codepoints[index];
            if (codepoint == last + 1)
            {
                last = codepoint;
                continue;
            }
            ranges.Add(FormatRange(first, last));
            first = codepoint;
            last = codepoint;
        }
        ranges.Add(FormatRange(first, last));
        return string.Join(',', ranges);
    }

    private static string FormatRange(int first, int last)
    {
        string firstText = FormatCodepoint(first);
        return first == last ? firstText : $"{firstText}-{FormatCodepoint(last)}";
    }

    private static string FormatCodepoint(int codepoint)
    {
        return $"U+{codepoint.ToString(codepoint <= 0xffff ? "X4" : "X6", CultureInfo.InvariantCulture)}";
    }
}
