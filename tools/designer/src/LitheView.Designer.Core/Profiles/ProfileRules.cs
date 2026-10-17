// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

internal static class ProfileRules
{
    public const int MaximumIdentifierLength = 128;
    public const int MaximumDisplayNameLength = 160;
    public const int MaximumSerializedByteCount = 1_048_576;

    public static bool IsValidIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumIdentifierLength)
        {
            return false;
        }

        bool previousWasSeparator = true;
        foreach (char character in value)
        {
            bool isSeparator = character is '.' or '-';
            bool isAllowed = character is >= 'a' and <= 'z' ||
                character is >= '0' and <= '9' || isSeparator;
            if (!isAllowed || isSeparator && previousWasSeparator)
            {
                return false;
            }
            previousWasSeparator = isSeparator;
        }
        return !previousWasSeparator;
    }

    public static bool IsLowercaseSha256(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        return value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    public static bool IsSafeProjectPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 240 ||
            value[0] == '/' || value.Contains('\\', StringComparison.Ordinal) ||
            value.Contains(':', StringComparison.Ordinal) ||
            value.Any(char.IsControl))
        {
            return false;
        }

        string[] segments = value.Split('/');
        return segments.All(segment =>
            segment.Length > 0 && segment is not "." and not "..");
    }
}
