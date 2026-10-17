// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;

namespace LitheView.Designer.Core.Documents;

public static class FormSymbolRules
{
    public static bool IsStableId(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length > 128 ||
            !IsAsciiLetter(value[0]) ||
            value.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(character =>
            IsAsciiLetter(character) ||
            char.IsAsciiDigit(character) ||
            character is '.' or '_' or '-');
    }

    public static bool IsRuntimeName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <= 256 &&
               !value.Contains("--", StringComparison.Ordinal) &&
               value.All(character => character >= ' ' && character != '\u007f');
    }

    public static string ToCppIdentifier(string value)
    {
        StringBuilder result = new(value.Length + 1);
        bool capitalize = true;
        foreach (char character in value)
        {
            if (!IsAsciiLetter(character) && !char.IsAsciiDigit(character))
            {
                capitalize = true;
                continue;
            }

            char output = capitalize && IsAsciiLetter(character)
                ? char.ToUpperInvariant(character)
                : character;
            if (result.Length == 0 && char.IsAsciiDigit(output))
            {
                result.Append('N');
            }
            result.Append(output);
            capitalize = false;
        }
        return result.Length == 0 ? "Unnamed" : result.ToString();
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }
}
