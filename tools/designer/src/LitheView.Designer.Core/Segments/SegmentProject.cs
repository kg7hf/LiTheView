// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Segments;

public sealed record SegmentProject(string ManifestPath, DesignerProjectManifest Manifest,
    SegmentGlass Glass, LogicalInputConfigurationProfile Input, IReadOnlyDictionary<string, SegmentForm> Forms)
{
    public const string GlassPath = "profiles/glass.segment-display.json";
    public const string InputPath = "profiles/logical-input.json";
    public string Directory => Path.GetDirectoryName(ManifestPath)!;

    public static string Create(string directory, byte[] glassBytes, byte[] inputBytes,
        string projectId = "application.segmented", string displayName = "Segmented display", DesignerProjectPaths? paths = null)
    {
        SegmentGlass glass = SegmentDocuments.ReadGlass(glassBytes);
        var input = LogicalInputConfigurationLoader.Load(inputBytes);
        if (!input.IsValid) throw new InvalidDataException("A valid logical input definition is required.");
        directory = Path.GetFullPath(directory);
        GeneratedFileAccess.RejectReparsePath(directory);
        if (File.Exists(directory) || System.IO.Directory.Exists(directory))
            throw new IOException("Create a segmented project in a new directory; existing directories are never overwritten.");
        var manifest = new DesignerProjectManifest
        {
            Schema = "https://litheview.dev/schema/project-manifest.schema.json", SchemaVersion = 4,
            DisplayKind = "segmented", Id = projectId, DisplayName = displayName,
            SchemaCompatibility = new() { ProjectManifest = 4, SegmentGlass = 1, LogicalInput = 1, Form = 1, ResourceManifest = 1, ExtensionManifest = 1 },
            SegmentGlass = new() { Id = glass.Id, Sha256 = SegmentDocuments.GlassHash(glass) },
            LogicalInput = new() { Id = input.Configuration!.Id, Sha256 = input.Sha256! },
            Forms = ["forms/home.litheview.xml"], ResourceManifests = [], Extensions = []
        };
        string path = Path.Combine(directory, "project.litheview.json");
        var validated = DesignerProjectLoader.Load(DesignerProjectCanonicalizer.Canonicalize(manifest));
        if (!validated.IsValid) throw new InvalidDataException("Invalid segmented project manifest.");
        if (paths is not null) DesignerProjectPathService.Resolve(path, paths, manifest);
        string parent = Path.GetDirectoryName(directory)!;
        string staging = Path.Combine(parent, ".segment-project-" + Guid.NewGuid().ToString("N"));
        try
        {
            AtomicFileWriter.WriteAllBytes(Path.Combine(staging, GlassPath), SegmentDocuments.CanonicalGlass(glass));
            AtomicFileWriter.WriteAllBytes(Path.Combine(staging, InputPath), input.CanonicalUtf8!);
            AtomicFileWriter.WriteAllBytes(Path.Combine(staging, manifest.Forms[0]), SegmentDocuments.WriteForm(new("home", glass.Id, manifest.SegmentGlass.Sha256, []), glass));
            AtomicFileWriter.WriteAllBytes(Path.Combine(staging, "project.litheview.json"), validated.CanonicalUtf8!);
            if (paths is not null) AtomicFileWriter.WriteAllBytes(Path.Combine(staging, "project.litheview.paths.json"), DesignerProjectPathService.Serialize(paths));
            System.IO.Directory.Move(staging, directory);
        }
        finally { if (System.IO.Directory.Exists(staging)) System.IO.Directory.Delete(staging, recursive: true); }
        return path;
    }

    public static byte[] Read(string path)
    {
        GeneratedFileAccess.RejectReparsePath(path);
        using FileStream file = File.OpenRead(path);
        if (file.Length > SegmentDocuments.MaximumBytes) throw new InvalidDataException("Segment project file exceeds 1 MiB.");
        byte[] bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        return bytes;
    }

    public static SegmentProject Load(string path, string? editingPath = null, byte[]? editingXml = null)
    {
        path = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(path)!;
        DesignerProjectLoadResult loaded = DesignerProjectLoader.Load(Read(path));
        if (!loaded.IsValid || loaded.Manifest is not { SchemaVersion: 4 } manifest)
            throw new InvalidDataException("Expected a valid segmented project. " + string.Join("; ", loaded.Diagnostics.Select(d => d.Message)));
        SegmentGlass glass = SegmentDocuments.ReadGlass(Read(GeneratedFileAccess.Contained(directory, GlassPath)));
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.Load(Read(GeneratedFileAccess.Contained(directory, InputPath)));
        if (manifest.SegmentGlass!.Id != glass.Id || manifest.SegmentGlass.Sha256 != SegmentDocuments.GlassHash(glass) ||
            !input.IsValid || input.Configuration?.Id != manifest.LogicalInput!.Id || input.Sha256 != manifest.LogicalInput.Sha256)
            throw new InvalidDataException("Glass or logical input snapshot no longer matches its project lock.");
        StringComparer paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, SegmentForm> forms = new(paths);
        // Generated identifiers must also be unique on case-insensitive filesystems.
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (string relative in manifest.Forms)
        {
            string formPath = GeneratedFileAccess.Contained(directory, relative);
            SegmentForm form = SegmentDocuments.ReadForm(editingPath is not null && paths.Equals(Path.GetFullPath(editingPath), formPath)
                ? editingXml ?? throw new ArgumentException("Editing XML is required.") : Read(formPath), glass);
            if (!names.Add(form.Id) || !forms.TryAdd(formPath, form)) throw new InvalidDataException("Duplicate segmented screen identity or path.");
        }
        if (editingPath is not null && !forms.ContainsKey(Path.GetFullPath(editingPath))) throw new InvalidDataException("Form is not listed in this project.");
        return new(path, manifest, glass, input.Configuration!, forms);
    }

    public static string Export(string manifestPath, string libraryRoot)
    {
        SegmentProject project = Load(manifestPath);
        var sources = new[] { project.ManifestPath, Path.Combine(project.Directory, GlassPath), Path.Combine(project.Directory, InputPath) }
            .Concat(project.Forms.Keys).ToDictionary(path => path, Read);
        // Resolve again from the captured generation revision before preparing output.
        var captured = Load(manifestPath);
        if (DesignerProjectCanonicalizer.ComputeSha256(DesignerProjectCanonicalizer.Canonicalize(captured.Manifest)) !=
            DesignerProjectCanonicalizer.ComputeSha256(DesignerProjectCanonicalizer.Canonicalize(project.Manifest)))
            throw new IOException("Project changed before generation; retry after saving.");
        string settingsPath = Path.ChangeExtension(project.ManifestPath, ".paths.json");
        byte[]? settingsBefore = File.Exists(settingsPath) ? Read(settingsPath) : null;
        ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(manifestPath, libraryRoot);
        byte[] glass = SegmentDocuments.CanonicalGlass(project.Glass);
        List<(string Path, byte[] After, byte[]? Before)> outputs = [];
        foreach ((string sourcePath, SegmentForm form) in project.Forms)
        {
            byte[] source = sources[sourcePath];
            SegmentCompilation compiled = SegmentCppCompiler.Compile(glass, source);
            string output = GeneratedFileAccess.Contained(paths.ComponentDirectory.Replace("{form}", form.Id, StringComparison.Ordinal), form.Id + ".hpp");
            if (File.Exists(output) && File.ReadLines(output).FirstOrDefault() != compiled.OwnershipLine.TrimEnd('\n'))
                throw new IOException("Refusing to replace an unowned segmented output: " + output);
            if (System.IO.Directory.Exists(output)) throw new IOException("Output header is a directory.");
            if (File.Exists(output) && new FileInfo(output).Length > 8 * SegmentDocuments.MaximumBytes)
                throw new IOException("Existing generated output is unexpectedly large.");
            // Portable build guards use LF-normalized text, so Git line-ending conversion
            // does not require the designer on a machine that only builds the application.
            string HashText(byte[] bytes) => GeneratedFileAccess.Hash(Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal)));
            string stamps = $"// Project input (LF) SHA-256: {HashText(sources[project.ManifestPath])}\n" +
                $"// Glass input (LF) SHA-256: {HashText(sources[Path.Combine(project.Directory, GlassPath)])}\n" +
                $"// Input input (LF) SHA-256: {HashText(sources[Path.Combine(project.Directory, InputPath)])}\n" +
                $"// Form input (LF) SHA-256: {HashText(source)}\n";
            string header = Encoding.UTF8.GetString(compiled.HeaderUtf8);
            int firstLine = header.IndexOf('\n') + 1;
            outputs.Add((output, Encoding.UTF8.GetBytes(header.Insert(firstLine, stamps)), File.Exists(output) ? File.ReadAllBytes(output) : null));
        }
        if (sources.Any(source => !Read(source.Key).AsSpan().SequenceEqual(source.Value)))
            throw new IOException("Project sources changed during generation; no output was published.");
        if (File.Exists(settingsPath) != (settingsBefore is not null) ||
            (settingsBefore is not null && !Read(settingsPath).AsSpan().SequenceEqual(settingsBefore)))
            throw new IOException("Output paths changed during generation; retry after saving.");
        // Per-file atomic replacement with compensating rollback, not a filesystem-wide transaction.
        var published = new List<(string Path, byte[] After, byte[]? Before)>();
        try
        {
            foreach (var output in outputs)
            {
                if (File.Exists(output.Path) != (output.Before is not null) ||
                    (output.Before is not null && !File.ReadAllBytes(output.Path).AsSpan().SequenceEqual(output.Before)))
                    throw new IOException("An output changed while generating; preserve the external edit and retry.");
                AtomicFileWriter.WriteAllBytes(output.Path, output.After);
                published.Add(output);
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            List<Exception> failures = [failure];
            foreach (var output in published.AsEnumerable().Reverse())
            {
                try
                {
                    if (!File.ReadAllBytes(output.Path).AsSpan().SequenceEqual(output.After))
                        throw new IOException("Rollback left an externally changed output intact: " + output.Path);
                    if (output.Before is null) File.Delete(output.Path);
                    else AtomicFileWriter.WriteAllBytes(output.Path, output.Before);
                }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException) { failures.Add(rollback); }
            }
            if (failures.Count > 1) throw new IOException("Generation failed and rollback was incomplete. Inspect generated outputs before building.", new AggregateException(failures));
            throw new IOException("Generation failed; previously published files were restored.", failure);
        }
        return paths.ComponentDirectory;
    }
}
