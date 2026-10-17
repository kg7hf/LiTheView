// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Tests;

internal static class DesignerProjectPathTests
{
    public static void Run(string repositoryRoot)
    {
        string temporary = Path.Combine(repositoryRoot, "build", "designer-path-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            DesignerProjectManifest source = DesignerProjectLoader.LoadFile(Path.Combine(repositoryRoot,
                "tests", "designer-fixtures", "display-profile-smoke", "project.litheview.json")).Manifest!;
            string library = Library(Path.Combine(temporary, "Library with spaces"));
            DefaultsAndPersistence(temporary, source, library);
            InLibraryDefaults(source, library);
            Relocation(temporary, source);
            OutputBoundaries(temporary, source, library);
            InputRoots(repositoryRoot, temporary, source, library);
            StrictSidecar(temporary, source, library);
            ReparsePaths(temporary, source, library);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static void DefaultsAndPersistence(string root, DesignerProjectManifest source, string library)
    {
        string manifest = Project(root, "Defaults", source);
        byte[] original = File.ReadAllBytes(manifest);
        ResolvedDesignerProjectPaths defaults = DesignerProjectPathService.Load(manifest, library);
        string project = Path.GetDirectoryName(manifest)!;
        Assert(defaults.ApplicationRoot == project && defaults.LibraryRoot == library &&
            defaults.ComponentDirectory == Path.Combine(project, "simulation") &&
            defaults.BuildDirectory == Path.Combine(project, "simulation", "build"), "Legacy defaults changed.");
        Assert(defaults.SettingsPath == Path.ChangeExtension(manifest, ".paths.json") &&
            Path.GetFileName(defaults.SettingsPath) == "project.litheview.paths.json", "The sidecar name is incorrect.");
        Assert(!Path.IsPathRooted(defaults.Settings.LibraryRoot) && !File.Exists(defaults.SettingsPath) &&
            !Directory.Exists(defaults.ComponentDirectory), "Reading defaults must not write files or absolute defaults.");
        DesignerProjectPaths settings = defaults.Settings with
        {
            ApplicationRoot = "../Application with spaces",
            LibraryRoot = "../Library with spaces",
            ComponentDirectory = "components/User interface",
            BuildDirectory = "../Build with spaces"
        };
        ResolvedDesignerProjectPaths saved = DesignerProjectPathService.Save(manifest, settings);
        Assert(saved.Settings == settings && saved.ApplicationRoot == Path.Combine(root, "Application with spaces") &&
            saved.ComponentDirectory == Path.Combine(root, "Application with spaces", "components", "User interface") &&
            saved.BuildDirectory == Path.Combine(root, "Build with spaces"), "Relative path bases were mixed.");
        Assert(DesignerProjectPathService.Load(manifest, "unused default").Settings == settings &&
            File.ReadAllBytes(manifest).SequenceEqual(original), "Path settings must not change the portable manifest.");
        byte[] first = File.ReadAllBytes(saved.SettingsPath);
        DesignerProjectPathService.Save(manifest, settings);
        Assert(File.ReadAllBytes(saved.SettingsPath).SequenceEqual(first), "Path settings serialization is not deterministic.");
        DesignerProjectPaths absolute = settings with
        {
            ApplicationRoot = saved.ApplicationRoot,
            LibraryRoot = library,
            ComponentDirectory = Path.Combine(root, "Absolute component"),
            BuildDirectory = Path.Combine(root, "Absolute build")
        };
        Assert(DesignerProjectPathService.Save(manifest, absolute).ComponentDirectory == absolute.ComponentDirectory,
            "Explicit absolute output directories must be supported.");
        Assert(!Directory.Exists(absolute.ComponentDirectory) && !Directory.Exists(absolute.BuildDirectory),
            "Saving settings must not create output directories.");
    }

    // Repository examples live inside the checkout; their default outputs must stay beneath its
    // build workspace instead of being refused, while explicit settings keep the strict checks.
    private static void InLibraryDefaults(DesignerProjectManifest source, string library)
    {
        string manifest = Project(Path.Combine(library, "tests", "designer-fixtures"), "In Library", source);
        string project = Path.GetDirectoryName(manifest)!;
        ResolvedDesignerProjectPaths defaults = DesignerProjectPathService.Load(manifest, library);
        string component = Path.Combine(library, "build", "designer-projects", "tests", "designer-fixtures", "In Library", "simulation");
        Assert(defaults.ApplicationRoot == project && defaults.LibraryRoot == library &&
            defaults.ComponentDirectory == component &&
            defaults.BuildDirectory == Path.Combine(component, "build"),
            "An in-library project without a sidecar must default its outputs into the library build workspace.");
        Assert(!Path.IsPathRooted(defaults.Settings.ComponentDirectory) && !Path.IsPathRooted(defaults.Settings.BuildDirectory) &&
            !File.Exists(defaults.SettingsPath) && !Directory.Exists(component),
            "In-library defaults must be relative and must not write files.");
        Reject(() => DesignerProjectPathService.Resolve(manifest, new DesignerProjectPaths
        {
            LibraryRoot = defaults.Settings.LibraryRoot
        }), "An explicit in-library simulation directory beside the sources must still be refused.");

        string tutorial = Project(Path.Combine(library, "build", "tutorial"), "Project", source);
        ResolvedDesignerProjectPaths tutorialDefaults = DesignerProjectPathService.Load(tutorial, library);
        Assert(tutorialDefaults.ComponentDirectory == Path.Combine(Path.GetDirectoryName(tutorial)!, "simulation"),
            "A project already inside the build workspace keeps the legacy defaults.");
    }

    private static void Relocation(string root, DesignerProjectManifest source)
    {
        string first = Path.Combine(root, "First location");
        string second = Path.Combine(root, "Second location");
        string library = Library(Path.Combine(first, "dependency", "LitheView"));
        string manifest = Project(first, "designer", source);
        DesignerProjectPaths settings = new()
        {
            ApplicationRoot = "..",
            LibraryRoot = "dependency/LitheView",
            ComponentDirectory = "generated/UI",
            BuildDirectory = "artifacts/simulator"
        };
        DesignerProjectPathService.Save(manifest, settings);
        Assert(DesignerProjectPathService.Load(manifest, library).LibraryRoot == library, "Relocation fixture is invalid.");
        // Both paths are fixed descendants of this test's unique workspace.
        Directory.Move(first, second);
        string movedManifest = Path.Combine(second, "designer", Path.GetFileName(manifest));
        ResolvedDesignerProjectPaths moved = DesignerProjectPathService.Load(movedManifest, "unused default");
        Assert(moved.ApplicationRoot == second && moved.LibraryRoot == Path.Combine(second, "dependency", "LitheView") &&
            moved.ComponentDirectory == Path.Combine(second, "generated", "UI") &&
            moved.BuildDirectory == Path.Combine(second, "artifacts", "simulator"), "Relative settings did not relocate together.");
    }

    private static void OutputBoundaries(string root, DesignerProjectManifest source, string library)
    {
        string manifest = Project(root, "Boundaries", source);
        DesignerProjectPaths settings = new() { LibraryRoot = library };
        string project = Path.GetDirectoryName(manifest)!;
        foreach (string output in new[] { ".", "..", "profiles", "profiles/nested", library,
            Path.Combine(library, "include", "generated"), Path.Combine(library, "build") })
        {
            Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ComponentDirectory = output }),
                "Unsafe component directory was accepted: " + output);
            Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { BuildDirectory = output }),
                "Unsafe build directory was accepted: " + output);
        }
        Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { BuildDirectory = "simulation" }),
            "Build output must not equal the component.");
        Reject(() => DesignerProjectPathService.Resolve(manifest, settings with
        {
            ComponentDirectory = "out/component", BuildDirectory = "out"
        }), "Build output must not contain the component.");
        Assert(DesignerProjectPathService.Resolve(manifest, settings with
        {
            ComponentDirectory = "out", BuildDirectory = "out/build"
        }).BuildDirectory == Path.Combine(project, "out", "build"), "Build output may be below the component.");
        ResolvedDesignerProjectPaths localBuild = DesignerProjectPathService.Resolve(manifest, settings with
        {
            ComponentDirectory = Path.Combine(library, "build", "project", "component"),
            BuildDirectory = Path.Combine(library, "build", "project", "artifacts")
        });
        Assert(localBuild.LibraryRoot == library, "The library-local build workspace exception was rejected.");
        string file = Path.Combine(project, "source.cpp");
        File.WriteAllText(file, "// application source");
        Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ComponentDirectory = "source.cpp" }),
            "An existing source file must not become an output directory.");
        Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { BuildDirectory = "source.cpp/build" }),
            "An output path must not pass through a file.");
        Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { LibraryRoot = project }),
            "A library without the Core source markers must fail.");
        Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ComponentDirectory = " " }),
            "Blank output directories must fail.");
        Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ApplicationRoot = null! }),
            "Direct DTO null paths must fail.");
        if (OperatingSystem.IsWindows())
        {
            Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ComponentDirectory = "D:relative" }),
                "Drive-relative paths must not depend on per-drive process state.");
            Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ComponentDirectory = "profiles. /nested" }),
                "Windows path aliases must fail.");
            Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ComponentDirectory = "\\\\?\\" + project }),
                "Device paths must not bypass source guards.");
        }
    }

    private static void InputRoots(string repositoryRoot, string root, DesignerProjectManifest source, string library)
    {
        string manifest = Project(root, "Custom inputs", source with
        {
            Forms = ["screens/sub/main.xml"],
            ResourceManifests = ["metadata/status.resource.json"]
        });
        string project = Path.GetDirectoryName(manifest)!;
        Directory.CreateDirectory(Path.Combine(project, "metadata"));
        string resource = File.ReadAllText(Path.Combine(repositoryRoot, "tests", "designer-fixtures", "resource-manifest-smoke",
            "resources", "status-icon.resource.json"));
        File.WriteAllText(Path.Combine(project, "metadata", "status.resource.json"), resource);
        DesignerProjectPaths settings = new() { LibraryRoot = library };
        foreach (string input in new[] { "screens", "screens/sub", "screens/sub/new", "metadata", "assets", "generated" })
        {
            Reject(() => DesignerProjectPathService.Resolve(manifest, settings with { ComponentDirectory = input }),
                "Custom input root was accepted as generated component: " + input);
        }
        Assert(DesignerProjectPathService.Resolve(manifest, settings).Settings == settings,
            "Unrelated outputs must remain valid for resource projects.");
        string rootForm = Project(root, "Root form", source with { Forms = ["main.xml"] });
        Assert(DesignerProjectPathService.Resolve(rootForm, settings).Settings == settings,
            "A root-level input file must not protect all sibling output directories.");
    }

    private static void StrictSidecar(string root, DesignerProjectManifest source, string library)
    {
        string manifest = Project(root, "Strict JSON", source);
        DesignerProjectPaths settings = new() { LibraryRoot = library };
        string path = DesignerProjectPathService.Save(manifest, settings).SettingsPath;
        byte[] valid = File.ReadAllBytes(path);
        string text = Encoding.UTF8.GetString(valid);
        string[] invalid =
        [
            "null", "[]", "{}", "{", text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal),
            text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": \"1\"", StringComparison.Ordinal),
            text.Replace("\"applicationRoot\": \".\"", "\"applicationRoot\": null", StringComparison.Ordinal),
            text.Replace("\"applicationRoot\": \".\"", "\"applicationRoot\": []", StringComparison.Ordinal),
            text.Replace("\"applicationRoot\": \".\"", "\"applicationRoot\": \"\"", StringComparison.Ordinal),
            text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1", StringComparison.Ordinal),
            text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"unknown\": true", StringComparison.Ordinal),
            text.Replace("\"schemaVersion\": 1", "\"SchemaVersion\": 1", StringComparison.Ordinal),
            text.Replace("\"buildDirectory\": \"simulation/build\"", "\"unused\": \"simulation/build\"", StringComparison.Ordinal)
        ];
        foreach (string candidate in invalid)
        {
            File.WriteAllText(path, candidate);
            Reject(() => DesignerProjectPathService.Load(manifest, library), "Malformed sidecar was accepted: " + candidate);
        }
        File.WriteAllText(path, new string(' ', 1_048_577));
        Reject(() => DesignerProjectPathService.Load(manifest, library), "Oversized sidecar was accepted.");
        File.WriteAllBytes(path, valid);
        Reject(() => DesignerProjectPathService.Save(manifest, settings with { BuildDirectory = "." }),
            "Unsafe settings were published.");
        Assert(File.ReadAllBytes(path).SequenceEqual(valid), "A rejected save changed the existing sidecar.");
        File.WriteAllText(path, text.Replace("\"simulation/build\"", "\".\"", StringComparison.Ordinal));
        Assert(DesignerProjectPathService.ReadSettings(manifest, library).BuildDirectory == ".",
            "The editor must be able to read and repair well-formed but unsafe paths.");
        Reject(() => DesignerProjectPathService.Load(manifest, library), "Runtime Load must still reject unsafe paths.");
    }

    private static void ReparsePaths(string root, DesignerProjectManifest source, string library)
    {
        string manifest = Project(root, "Links", source);
        string project = Path.GetDirectoryName(manifest)!;
        string target = Path.Combine(root, "Link destination");
        string link = Path.Combine(project, "redirect");
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Symbolic link creation requires an OS privilege on some hosts.
            // The other ownership checks do not depend on that privilege.
            Console.WriteLine("SKIP project-path symbolic-link fixture: " + exception.Message);
            return;
        }
        try
        {
            Reject(() => DesignerProjectPathService.Resolve(manifest, new()
            {
                LibraryRoot = library, ComponentDirectory = "redirect/component", BuildDirectory = "out/build"
            }), "A reparse ancestor redirected the component output.");
            Reject(() => DesignerProjectPathService.Resolve(manifest, new()
            {
                LibraryRoot = library, BuildDirectory = "redirect/build"
            }), "A reparse ancestor redirected the build output.");
            string sidecar = Path.ChangeExtension(manifest, ".paths.json");
            File.CreateSymbolicLink(sidecar, Path.Combine(target, "missing-settings.json"));
            Reject(() => DesignerProjectPathService.Load(manifest, library), "A dangling sidecar link was treated as absent.");
            File.Delete(sidecar);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    private static string Project(string root, string name, DesignerProjectManifest source)
    {
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        string manifest = Path.Combine(directory, DesignerWorkspaceService.ManifestFileName);
        File.WriteAllBytes(manifest, DesignerProjectCanonicalizer.Canonicalize(source));
        return manifest;
    }

    private static string Library(string path)
    {
        Directory.CreateDirectory(Path.Combine(path, "include", "lithe"));
        File.WriteAllText(Path.Combine(path, "CMakeLists.txt"), "# Core source fixture");
        File.WriteAllText(Path.Combine(path, "include", "lithe", "application.hpp"), "// Core source fixture");
        return path;
    }

    private static void Reject(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
