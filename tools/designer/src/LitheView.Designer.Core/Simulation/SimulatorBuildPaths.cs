// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Simulation;

// Host build placement is independent of both the UI sources and Core checkout.
internal sealed record SimulatorBuildPaths
{
    internal const string FileName = "simulator-build.json";

    public int SchemaVersion { get; init; } = 1;

    public required string BuildDirectory { get; init; }

    internal static byte[] Serialize(string componentDirectory, string buildDirectory)
    {
        Validate(componentDirectory, buildDirectory);
        return JsonSerializer.SerializeToUtf8Bytes(new SimulatorBuildPaths
        {
            BuildDirectory = Path.GetRelativePath(componentDirectory, buildDirectory).Replace('\\', '/')
        }, SimulatorJson.Options);
    }

    internal static string Resolve(string componentDirectory, string? overrideDirectory)
    {
        string root = Path.GetFullPath(componentDirectory);
        string result;
        if (overrideDirectory is not null)
        {
            result = Path.GetFullPath(overrideDirectory);
        }
        else if (File.Exists(Path.Combine(root, FileName)))
        {
            List<ProfileDiagnostic> diagnostics = [];
            SimulatorBuildPaths? paths = DisplayDefinitionJson.Read<SimulatorBuildPaths>(
                File.ReadAllBytes(GeneratedFileAccess.Contained(root, FileName)), "LVSIM", diagnostics);
            if (paths is null || paths.SchemaVersion != 1 || string.IsNullOrWhiteSpace(paths.BuildDirectory) ||
                diagnostics.Count != 0)
            {
                throw new InvalidDataException("The exported simulator build paths are invalid; export again.");
            }
            result = Path.GetFullPath(paths.BuildDirectory, root);
        }
        else
        {
            result = Path.Combine(root, "build");
        }
        Validate(root, result);
        return result;
    }

    private static void Validate(string componentDirectory, string buildDirectory)
    {
        GeneratedFileAccess.RejectReparsePath(buildDirectory);
        string component = Path.TrimEndingDirectorySeparator(Path.GetFullPath(componentDirectory));
        string build = Path.TrimEndingDirectorySeparator(Path.GetFullPath(buildDirectory));
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (component.Equals(build, comparison) || component.StartsWith(build + Path.DirectorySeparatorChar, comparison))
        {
            throw new ArgumentException("The simulator build directory must not contain the generated UI component.");
        }
    }
}
