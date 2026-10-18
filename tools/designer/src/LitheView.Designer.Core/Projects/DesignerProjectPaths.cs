// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.Core.Projects;

// Integration paths are separate from the portable, hash-locked project
// manifest. Relative settings can be shared and relocate with the application.
public sealed record DesignerProjectPaths
{
    public int SchemaVersion { get; init; } = 1;

    public string ApplicationRoot { get; init; } = ".";

    public string LibraryRoot { get; init; } = string.Empty;

    public string ComponentDirectory { get; init; } = "simulation";

    public string BuildDirectory { get; init; } = "simulation/build";
}

public sealed record ResolvedDesignerProjectPaths
{
    public required DesignerProjectPaths Settings { get; init; }

    public required string ApplicationRoot { get; init; }

    public required string LibraryRoot { get; init; }

    public required string ComponentDirectory { get; init; }

    public required string BuildDirectory { get; init; }

    public required string SettingsPath { get; init; }
}

public static class DesignerProjectPathService
{
    public static ResolvedDesignerProjectPaths Load(string manifestPath, string defaultLibraryRoot)
    {
        return Resolve(manifestPath, ReadSettings(manifestPath, defaultLibraryRoot));
    }

    // Editors may load well-formed settings even when a moved directory needs
    // repair. Execution always uses Resolve or Load to validate the locations.
    public static DesignerProjectPaths ReadSettings(string manifestPath, string defaultLibraryRoot)
    {
        string manifest = AbsoluteManifestPath(manifestPath);
        string settingsPath = Path.ChangeExtension(manifest, ".paths.json");
        RejectReparseAncestors(settingsPath);
        DesignerProjectPaths settings;
        if (File.Exists(settingsPath))
        {
            settings = ReadSettings(settingsPath);
        }
        else
        {
            if (Directory.Exists(settingsPath))
            {
                throw new InvalidDataException("The project paths sidecar must be a regular file.");
            }
            ArgumentException.ThrowIfNullOrWhiteSpace(defaultLibraryRoot);
            string project = Path.GetDirectoryName(manifest)!;
            string library = ResolvePath(defaultLibraryRoot, project, "Default library root");
            settings = new()
            {
                LibraryRoot = Path.GetRelativePath(project, library).Replace('\\', '/')
            };
            // A project kept inside the LitheView checkout, such as a repository example, may not
            // write next to its sources; without a sidecar its outputs default to the checkout's
            // build workspace, mirroring the project's location there.
            string workspace = Path.Combine(library, "build");
            if (IsSameOrInside(project, library) && !PathsEqual(project, library) && !IsSameOrInside(project, workspace))
            {
                string component = Path.Combine(workspace, "designer-projects", Path.GetRelativePath(library, project), "simulation");
                settings = settings with
                {
                    ComponentDirectory = Path.GetRelativePath(project, component).Replace('\\', '/'),
                    BuildDirectory = Path.GetRelativePath(project, Path.Combine(component, "build")).Replace('\\', '/')
                };
            }
        }
        return settings;
    }

    public static ResolvedDesignerProjectPaths Save(string manifestPath, DesignerProjectPaths settings)
    {
        ResolvedDesignerProjectPaths resolved = Resolve(manifestPath, settings);
        AtomicFileWriter.WriteAllBytes(resolved.SettingsPath, Serialize(settings));
        return resolved;
    }

    internal static byte[] Serialize(DesignerProjectPaths settings)
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", settings.SchemaVersion);
            writer.WriteString("applicationRoot", settings.ApplicationRoot);
            writer.WriteString("libraryRoot", settings.LibraryRoot);
            writer.WriteString("componentDirectory", settings.ComponentDirectory);
            writer.WriteString("buildDirectory", settings.BuildDirectory);
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    // No directory or file is created until Save has resolved and checked every
    // path. Export/build services still enforce ownership of individual outputs.
    public static ResolvedDesignerProjectPaths Resolve(string manifestPath, DesignerProjectPaths settings)
        => Resolve(manifestPath, settings, null);

    // Creation validates final paths before publishing its manifest and sidecar together.
    internal static ResolvedDesignerProjectPaths Resolve(string manifestPath, DesignerProjectPaths settings,
        DesignerProjectManifest? newManifest)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != 1)
        {
            throw new InvalidDataException("Project paths schemaVersion must be 1.");
        }
        string manifest = AbsoluteManifestPath(manifestPath);
        string project = Path.GetDirectoryName(manifest)!;
        string settingsPath = Path.ChangeExtension(manifest, ".paths.json");
        RejectReparseAncestors(settingsPath);
        if (Directory.Exists(settingsPath))
        {
            throw new InvalidDataException("The project paths sidecar must be a regular file.");
        }
        string application = ResolvePath(settings.ApplicationRoot, project, "Application root");
        string library = ResolvePath(settings.LibraryRoot, application, "Library root");
        string component = ResolvePath(settings.ComponentDirectory, application, "Component directory");
        string build = ResolvePath(settings.BuildDirectory, application, "Build directory");
        foreach (string directory in new[] { application, library, component, build })
        {
            ValidateDirectoryPath(directory);
        }
        ValidateLibrary(library);
        if (IsSameOrInside(component, build))
        {
            throw new ArgumentException("The build directory must not equal or contain the component directory.");
        }
        DesignerProjectLoadResult loaded = newManifest is null
            ? DesignerProjectLoader.LoadFile(manifest)
            : DesignerProjectLoader.Load(DesignerProjectCanonicalizer.Canonicalize(newManifest));
        if (!loaded.IsValid || loaded.Manifest is null)
        {
            throw new InvalidDataException("Project paths require a valid project manifest: " +
                string.Join("; ", loaded.Diagnostics.Select(item => item.Message)));
        }
        string[] protectedInputs = ProtectedInputRoots(loaded.Manifest, project).ToArray();
        foreach (string output in new[] { component, build })
        {
            if (IsSameOrInside(project, output))
            {
                throw new ArgumentException("Output directories must not replace the project manifest directory or its ancestors.");
            }
            ValidateLibrarySeparation(output, library, application, project);
            if (protectedInputs.Any(input => Overlaps(output, input)))
            {
                throw new ArgumentException("Output directories must not overlap project form, profile, resource or extension inputs.");
            }
        }
        return new()
        {
            Settings = settings,
            ApplicationRoot = application,
            LibraryRoot = library,
            ComponentDirectory = component,
            BuildDirectory = build,
            SettingsPath = settingsPath
        };
    }

    private static DesignerProjectPaths ReadSettings(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > ProfileRules.MaximumSerializedByteCount)
        {
            throw new InvalidDataException("Project paths sidecar exceeds the 1 MiB limit.");
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(stream, ProfileJson.DocumentOptions);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Project paths must be a JSON object.");
            }
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name) || property.Name is not
                    ("schemaVersion" or "applicationRoot" or "libraryRoot" or "componentDirectory" or "buildDirectory"))
                {
                    throw new InvalidDataException($"Unknown or duplicate project paths property '{property.Name}'.");
                }
            }
            if (names.Count != 5 || root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number ||
                !root.GetProperty("schemaVersion").TryGetInt32(out int version))
            {
                throw new InvalidDataException("Project paths require schemaVersion and all four path strings.");
            }
            if (version != 1)
            {
                throw new InvalidDataException("Project paths schemaVersion must be 1.");
            }
            return new()
            {
                SchemaVersion = version,
                ApplicationRoot = ReadPath(root, "applicationRoot"),
                LibraryRoot = ReadPath(root, "libraryRoot"),
                ComponentDirectory = ReadPath(root, "componentDirectory"),
                BuildDirectory = ReadPath(root, "buildDirectory")
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid project paths JSON: " + exception.Message, exception);
        }
    }

    private static string ReadPath(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Project paths '{name}' must be a nonempty string.");
        }
        return value.GetString()!;
    }

    private static string AbsoluteManifestPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string manifest = ResolvePath(path, Directory.GetCurrentDirectory(), "Project manifest");
        RejectReparseAncestors(manifest);
        return manifest;
    }

    private static string ResolvePath(string path, string basis, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl) ||
            (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path)) ||
            path.StartsWith("\\\\?\\", StringComparison.Ordinal) || path.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{label} must be a normal relative path or a fully qualified absolute path.");
        }
        if (OperatingSystem.IsWindows())
        {
            foreach (string part in path.Split(['/', '\\']))
            {
                if (part is not ("." or "..") && (part.EndsWith(' ') || part.EndsWith('.')))
                {
                    throw new ArgumentException($"{label} must not contain ambiguous trailing spaces or dots.");
                }
            }
        }
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path, basis));
    }

    private static void ValidateDirectoryPath(string path)
    {
        RejectReparseAncestors(path);
        string? current = path;
        while (current is not null)
        {
            FileAttributes? attributes = ExistingAttributes(current);
            if (attributes is not null && (attributes & FileAttributes.Directory) == 0)
            {
                throw new ArgumentException($"Directory path '{path}' passes through the file '{current}'.");
            }
            current = Path.GetDirectoryName(current);
        }
    }

    private static void RejectReparseAncestors(string path)
    {
        string? current = path;
        while (current is not null)
        {
            FileAttributes? attributes = ExistingAttributes(current);
            if (attributes is not null && (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException($"Project path '{current}' is a symbolic link or reparse point.");
            }
            current = Path.GetDirectoryName(current);
        }
    }

    private static FileAttributes? ExistingAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static void ValidateLibrary(string library)
    {
        foreach (string relative in new[] { "CMakeLists.txt", "include/lithe/application.hpp" })
        {
            string path = Path.Combine(library, relative.Replace('/', Path.DirectorySeparatorChar));
            RejectReparseAncestors(path);
            if (!File.Exists(path))
            {
                throw new ArgumentException("The library root must contain LitheView CMakeLists.txt and include/lithe/application.hpp.");
            }
        }
    }

    private static void ValidateLibrarySeparation(string output, string library, string application, string project)
    {
        if (IsSameOrInside(library, output))
        {
            throw new ArgumentException("Output directories must not equal or contain the LitheView library.");
        }
        if (IsSameOrInside(output, library))
        {
            // A contributor example is an application, despite living in this checkout.
            // Scope this exception to its own subtree; never another example or library sources.
            string examples = Path.Combine(library, "examples");
            if (PathsEqual(Path.GetDirectoryName(application) ?? "", examples) &&
                IsSameOrInside(project, application) && IsSameOrInside(output, application) &&
                !PathsEqual(output, application) && File.Exists(Path.Combine(application, "CMakeLists.txt")))
            {
                return;
            }
            // Existing tutorials and retained checks use this explicit local
            // build workspace. No other library source subtree is writable.
            string workspace = Path.Combine(library, "build");
            if (!IsSameOrInside(output, workspace) || PathsEqual(output, workspace))
            {
                throw new ArgumentException("Outputs inside LitheView must be beneath its build workspace.");
            }
        }
    }

    private static IEnumerable<string> ProtectedInputRoots(DesignerProjectManifest manifest, string project)
    {
        string profiles = Path.Combine(project, "profiles");
        RejectReparseAncestors(profiles);
        yield return profiles;
        foreach (string path in manifest.Forms.Concat(manifest.ResourceManifests).Concat(
            manifest.Extensions.Select(item => item.PackagePath)))
        {
            yield return InputRoot(project, path);
        }
        foreach (string relative in manifest.ResourceManifests)
        {
            string path = Path.Combine(project, relative.Replace('/', Path.DirectorySeparatorChar));
            RejectReparseAncestors(path);
            ResourceManifestLoadResult loaded = ResourceManifestLoader.LoadFile(path);
            if (!loaded.IsValid || loaded.Manifest is null)
            {
                throw new InvalidDataException("Unable to determine resource input paths: " +
                    string.Join("; ", loaded.Diagnostics.Select(item => item.Message)));
            }
            ResourceManifest resource = loaded.Manifest;
            yield return InputRoot(project, resource.Source.Path);
            if (resource.Source.Provenance?.License?.LicenseTextPath is string license)
            {
                yield return InputRoot(project, license);
            }
            foreach (ResourceVariant variant in resource.Variants)
            {
                yield return InputRoot(project, variant.Output.Path);
            }
        }
    }

    private static string InputRoot(string project, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(project, relative.Replace('/', Path.DirectorySeparatorChar)));
        RejectReparseAncestors(path);
        if (Directory.Exists(path))
        {
            return path;
        }
        string parent = Path.GetDirectoryName(path)!;
        // A root-level form protects that file, not every sibling directory.
        return PathsEqual(parent, project) ? path : parent;
    }

    private static bool Overlaps(string first, string second) =>
        IsSameOrInside(first, second) || IsSameOrInside(second, first);

    private static bool IsSameOrInside(string candidate, string parent) =>
        PathsEqual(candidate, parent) || candidate.StartsWith(
            Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool PathsEqual(string first, string second) => string.Equals(first, second,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
