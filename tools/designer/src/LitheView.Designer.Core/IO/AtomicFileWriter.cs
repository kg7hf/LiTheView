// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.IO;

public static class AtomicFileWriter
{
    public static void WriteAllBytes(string destinationPath, ReadOnlySpan<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string fullDestination = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullDestination);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("The destination must have a parent directory.", nameof(destinationPath));
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullDestination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullDestination))
            {
                File.Move(temporaryPath, fullDestination, overwrite: true);
            }
            else
            {
                File.Move(temporaryPath, fullDestination);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
