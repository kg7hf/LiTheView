// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.DisplayBuild;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Simulation;

public static class SimulatorProjectExporter
{
    private const string TemplatePrefix = "SimulatorTemplates/";
    // VS Code's debugger schema uses the case-sensitive property MIMode.
    private static readonly JsonSerializerOptions LaunchJsonOptions = new(SimulatorJson.Options)
    {
        PropertyNamingPolicy = null
    };

    public static SimulatorExportResult Export(DesignerWorkspace workspace, string outputDirectory, string? buildDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        List<ProfileDiagnostic> diagnostics = [];
        try
        {
            DesignerProductResolutionResult product = DesignerProductResolver.Resolve(workspace);
            diagnostics.AddRange(product.Diagnostics);
            if (!product.IsValid || product.Definition?.Profile is null || product.Input?.Configuration is null ||
                product.ResolvedForm is null || product.Resources is null)
            {
                return Failed(diagnostics);
            }
            string? destination = FormatName(product.Definition.Profile.Format);
            if (destination is null)
            {
                diagnostics.Add(SimulatorJson.Error("LVSIM1001", "$.displayDefinition.format",
                    "This simulator supports RGB565, RGB888, XRGB8888 and ARGB8888. " +
                    "Native packed displays require a matching presentation adapter; no RGB substitute is generated."));
                return Failed(diagnostics);
            }
            SurfaceInputRequirements requirements = new()
            {
                Activation = workspace.Form.Actions.Length != 0,
                Navigation = workspace.Form.Actions.Length > 1,
                Text = product.ResolvedForm.Components.Any(item => item.Widget.RuntimeKind == "textField")
            };
            DisplayApplicationCompilationResult compiled = DisplayApplicationCompiler.Compile(
                workspace.Form, workspace.Catalog, product.Definition, product.Input, product.Resources,
                new FormSurfacePolicy { InputRequirements = requirements });
            diagnostics.AddRange(compiled.Diagnostics);
            if (!compiled.IsValid)
            {
                return Failed(diagnostics);
            }

            SortedDictionary<string, byte[]> files = LoadTemplates();
            if (!files.Remove("application.hpp", out byte[]? defaultApplication))
            {
                throw new InvalidDataException("The simulator application template is unavailable.");
            }
            string existingApplication = GeneratedFileAccess.Contained(workspace.ProjectDirectory, "simulation/application.hpp");
            if (File.Exists(existingApplication))
            {
                // A new standalone export starts with the project's application
                // hook. Subsequent exports preserve the destination's own edits.
                defaultApplication = File.ReadAllBytes(existingApplication);
            }
            foreach ((string path, byte[] bytes) in compiled.Files)
            {
                files.Add(path, bytes);
            }
            files.Add("product_config.hpp", ComponentExportSupport.ProductConfiguration(product.ResolvedForm.CppSymbol, requirements));
            files.Add("display-definition.json", product.Definition.CanonicalUtf8!);
            files.Add("logical-input.json", product.Input.CanonicalUtf8!);
            files.Add("form.litheview.xml", FormXmlWriter.Write(workspace.Form, workspace.Catalog).Utf8!);
            StringBuilder sources = new("# Selected, verified static resources.\nset(LITHEVIEW_SELECTED_RESOURCE_SOURCES\n");
            foreach (var resource in compiled.Resources)
            {
                string source = GeneratedFileAccess.Contained(workspace.ProjectDirectory, resource.Variant.Output.Path);
                byte[] bytes = File.ReadAllBytes(source);
                string hash = GeneratedFileAccess.Hash(bytes);
                if (hash != resource.Variant.Output.Sha256 || bytes.LongLength != resource.Variant.Output.ByteCount)
                {
                    throw new InvalidDataException($"Selected resource '{resource.ResourceId}' changed during export.");
                }
                string relative = $"assets/{hash}.cpp";
                if (files.TryAdd(relative, bytes))
                {
                    sources.Append("    \"${CMAKE_CURRENT_LIST_DIR}/").Append(relative).Append("\"\n");
                }
            }
            sources.Append(")\n");
            files.Add("resources.cmake", Encoding.UTF8.GetBytes(sources.ToString()));
            string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
            ComponentExportSupport.GuardOutputLocation(workspace, directory);
            string build = Path.GetFullPath(buildDirectory ?? Path.Combine(directory, "build"));
            files.Add(SimulatorBuildPaths.FileName, SimulatorBuildPaths.Serialize(directory, build));
            AddEditorFiles(files, directory, build);
            SimulatorProjectManifest? previous = SimulatorFiles.LoadManifest(directory);
            if (previous is not null)
            {
                SimulatorFiles.VerifyGeneratedFiles(directory, previous);
            }
            foreach (string relative in files.Keys)
            {
                string path = GeneratedFileAccess.Contained(directory, relative);
                if (Directory.Exists(path) || File.Exists(path) && previous?.GeneratedFiles.ContainsKey(relative) != true)
                {
                    throw new ArgumentException($"Export would overwrite an unowned file '{relative}'.");
                }
            }
            string applicationPath = GeneratedFileAccess.Contained(directory, "application.hpp");
            if (Directory.Exists(applicationPath))
            {
                throw new ArgumentException("application.hpp must be a regular developer-owned file.");
            }
            SimulatorProjectManifest manifest = new()
            {
                DestinationFormat = destination,
                FormSha256 = compiled.FormSha256!,
                DisplayDefinitionSha256 = compiled.DisplayDefinitionSha256!,
                LogicalInputSha256 = compiled.LogicalInputSha256!,
                GeneratedFiles = new SortedDictionary<string, string>(
                    files.ToDictionary(item => item.Key, item => GeneratedFileAccess.Hash(item.Value)), StringComparer.Ordinal)
            };
            string manifestPath = GeneratedFileAccess.Contained(directory, SimulatorJson.ManifestName);
            // Failure during publication leaves an incomplete marker. Never launch
            // an old successful executable after an incomplete or failed build.
            AtomicFileWriter.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, SimulatorJson.Options));
            foreach ((string relative, byte[] bytes) in files)
            {
                AtomicFileWriter.WriteAllBytes(GeneratedFileAccess.Contained(directory, relative), bytes);
            }
            if (!File.Exists(applicationPath))
            {
                AtomicFileWriter.WriteAllBytes(applicationPath, defaultApplication);
            }
            AtomicFileWriter.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(
                manifest with { GenerationStatus = "complete" }, SimulatorJson.Options));
            return new() { OutputDirectory = directory, ManifestPath = manifestPath, Diagnostics = diagnostics };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or JsonException or InvalidOperationException or InvalidDataException)
        {
            diagnostics.Add(SimulatorJson.Error("LVSIM1002", "$.simulation", exception.Message));
            return Failed(diagnostics);
        }
    }

    private static string? FormatName(SurfacePixelFormat format) => format switch
    {
        SurfacePixelFormat.Rgb565 => "RGB565",
        SurfacePixelFormat.Rgb888 => "RGB888",
        SurfacePixelFormat.Xrgb8888 => "XRGB8888",
        SurfacePixelFormat.Argb8888 => "ARGB8888",
        _ => null
    };

    private static SortedDictionary<string, byte[]> LoadTemplates()
    {
        Assembly assembly = typeof(SimulatorProjectExporter).Assembly;
        SortedDictionary<string, byte[]> files = new(StringComparer.Ordinal);
        foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(TemplatePrefix, StringComparison.Ordinal)))
        {
            using Stream source = assembly.GetManifestResourceStream(name)!;
            using MemoryStream output = new();
            source.CopyTo(output);
            files.Add(name[TemplatePrefix.Length..], output.ToArray());
        }
        string[] required = ["CMakeLists.txt", "runtime_types.hpp", "runtime.hpp", "application.hpp",
            "headless.cpp", "win32.cpp", "README.md"];
        if (!required.All(files.ContainsKey))
        {
            throw new InvalidDataException("The installed designer has no complete simulator templates.");
        }
        return files;
    }

    private static void AddEditorFiles(SortedDictionary<string, byte[]> files, string directory, string buildDirectory)
    {
        string relative = Path.GetRelativePath(directory, buildDirectory).Replace('\\', '/');
        string build = Path.IsPathRooted(relative) ? relative : "${workspaceFolder}/" + relative;
        var tasks = new
        {
            version = "2.0.0",
            tasks = new object[]
            {
                new { label = "Configure simulator", type = "process", command = "cmake",
                    args = new[] { "-S", "${workspaceFolder}", "-B", build + "/debug/host",
                        "-G", "MinGW Makefiles", "-DCMAKE_BUILD_TYPE=Debug",
                        "-DCMAKE_PREFIX_PATH=" + build + "/debug/installed" },
                    problemMatcher = "$gcc" },
                new { label = "Build simulator", type = "process", command = "cmake",
                    args = new[] { "--build", build + "/debug/host", "--parallel", "2" },
                    dependsOn = "Configure simulator",
                    problemMatcher = "$gcc", group = "build" }
            }
        };
        var launch = new
        {
            version = "0.2.0",
            configurations = new object[]
            {
                new { name = "Generated simulator (Debug)", type = "cppdbg", request = "launch",
                    program = build + "/debug/host/litheview_generated_simulator.exe",
                    cwd = build + "/debug/host", MIMode = "gdb", miDebuggerPath = "gdb.exe",
                    preLaunchTask = "Build simulator", externalConsole = false }
            }
        };
        files.Add(".vscode/tasks.json", JsonSerializer.SerializeToUtf8Bytes(tasks, SimulatorJson.Options));
        files.Add(".vscode/launch.json", JsonSerializer.SerializeToUtf8Bytes(launch, LaunchJsonOptions));
    }

    private static SimulatorExportResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics) => new() { Diagnostics = diagnostics };
}
