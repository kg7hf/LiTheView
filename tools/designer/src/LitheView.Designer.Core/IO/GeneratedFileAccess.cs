// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;

namespace LitheView.Designer.Core.IO;

// Generated outputs must stay in ordinary contained files, without redirected ancestors.
internal static class GeneratedFileAccess
{
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static string Contained(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\', StringComparison.Ordinal) ||
            Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':')))
        {
            throw new ArgumentException($"Invalid generated file path '{relative}'.");
        }
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException($"Generated file '{relative}' leaves its directory.");
        }
        RejectReparsePath(candidate);
        RejectFileAncestors(candidate);
        return candidate;
    }

    internal static void RejectFileAncestors(string path)
    {
        string? current = Path.GetDirectoryName(Path.GetFullPath(path));
        while (current is not null)
        {
            if (File.Exists(current))
            {
                throw new ArgumentException($"Output parent '{current}' is a regular file, not a directory.");
            }
            current = Path.GetDirectoryName(current);
        }
    }

    internal static void RejectReparsePath(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (FileNotFoundException)
            {
                current = Path.GetDirectoryName(current);
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                current = Path.GetDirectoryName(current);
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException($"Generated path '{current}' is a symbolic link or reparse point.");
            }
            current = Path.GetDirectoryName(current);
        }
    }
}
