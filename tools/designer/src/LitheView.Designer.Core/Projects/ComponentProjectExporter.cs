// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.DisplayBuild;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Simulation;

namespace LitheView.Designer.Core.Projects;

public sealed record ComponentExportResult
{
    public string? OutputDirectory { get; init; }
    public string? ManifestPath { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => OutputDirectory is not null && ManifestPath is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

// The IDE/application owns startup, devices, tasks and builds. Generation publishes
// only portable UI and selected native assets; it never qualifies an MCU target.
public static class ComponentProjectExporter
{
    public const string ManifestName = "component.json";

    public static ComponentExportResult Export(DesignerWorkspace workspace, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        List<ProfileDiagnostic> diagnostics = [];
        try
        {
            _ = DesignerProjectForms.LoadAll(workspace);
            bool scoped = DesignerProjectForms.HasOwnDirectory(workspace, outputDirectory);
            outputDirectory = DesignerProjectForms.OutputDirectory(workspace, outputDirectory);
            DesignerProductResolutionResult product = DesignerProductResolver.Resolve(workspace);
            diagnostics.AddRange(product.Diagnostics);
            if (!product.IsValid || product.Definition?.Profile is null || product.Input?.Configuration is null ||
                product.ResolvedForm is null || product.Resources is null)
            {
                return new() { Diagnostics = diagnostics };
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
                return new() { Diagnostics = diagnostics };
            }
            SortedDictionary<string, byte[]> files = new(StringComparer.Ordinal);
            foreach ((string name, byte[] bytes) in compiled.Files)
            {
                files.Add(name, scoped && name is "display.hpp" or "resources.hpp"
                    ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("namespace lithe::generated",
                        "namespace lithe::generated::assets::" + product.ResolvedForm.CppSymbol, StringComparison.Ordinal))
                    : bytes);
            }
            // Retain the existing aliases for source compatibility with applications
            // that already include product_config.hpp. These aliases own no runtime.
            files.Add("product_config.hpp", ComponentExportSupport.ProductConfiguration(product.ResolvedForm.CppSymbol, requirements, scoped));
            files.Add("display-definition.json", product.Definition.CanonicalUtf8!);
            files.Add("logical-input.json", product.Input.CanonicalUtf8!);
            files.Add("form.litheview.xml", FormXmlWriter.Write(workspace.Form, workspace.Catalog).Utf8!);
            StringBuilder resources = new("# Generated native resources selected by this UI.\nset(LITHEVIEW_SELECTED_RESOURCE_SOURCES\n");
            StringBuilder sharedResources = new();
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
                    // Every form can refer to the same converter output. Give CMake
                        // one canonical source path per content hash to prevent duplicate
                        // definitions when several generated components link into one app.
                        sharedResources.Append("get_property(_lithe_resource GLOBAL PROPERTY LITHEVIEW_RESOURCE_").Append(hash).Append(" )\n")
                            .Append("if(NOT _lithe_resource)\n    set(_lithe_resource \"${CMAKE_CURRENT_LIST_DIR}/").Append(relative).Append("\")\n")
                            .Append("    set_property(GLOBAL PROPERTY LITHEVIEW_RESOURCE_").Append(hash).Append(" \"${_lithe_resource}\")\nendif()\n")
                            .Append("list(APPEND LITHEVIEW_SELECTED_RESOURCE_SOURCES \"${_lithe_resource}\")\nunset(_lithe_resource)\n");
                }
            }
            resources.Append(")\n").Append(sharedResources);
            files.Add("resources.cmake", Encoding.UTF8.GetBytes(resources.ToString()));
            files.Add("CMakeLists.txt", Encoding.UTF8.GetBytes(scoped ? ComponentCMake.Replace("litheview_generated_component", "litheview_generated_" + product.ResolvedForm.CppSymbol, StringComparison.Ordinal).Replace("LitheView::GeneratedComponent", "LitheView::Form" + product.ResolvedForm.CppSymbol, StringComparison.Ordinal) : ComponentCMake));
            string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
            ComponentExportSupport.GuardOutputLocation(workspace, directory);
            ComponentManifest manifest = new()
            {
                DestinationFormat = product.Definition.Profile.Format.ToString(),
                FormSha256 = compiled.FormSha256!,
                DisplayDefinitionSha256 = compiled.DisplayDefinitionSha256!,
                LogicalInputSha256 = compiled.LogicalInputSha256!,
                GeneratedFiles = new(files.ToDictionary(item => item.Key, item => GeneratedFileAccess.Hash(item.Value)), StringComparer.Ordinal)
            };
            if (!LockedInputsUnchanged(workspace, compiled, diagnostics))
            {
                return new() { Diagnostics = diagnostics };
            }
            Publish(directory, files, manifest, diagnostics);
            return new() { OutputDirectory = directory, ManifestPath = Path.Combine(directory, ManifestName), Diagnostics = diagnostics };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or JsonException or InvalidOperationException or InvalidDataException)
        {
            diagnostics.Add(new("LVUI4100", ProfileDiagnosticSeverity.Error, "$.component", exception.Message));
            return new() { Diagnostics = diagnostics };
        }
    }

    // Output is published only from what was resolved. Every version, including version-3
    // native display projects, re-checks its manifest and display/input snapshots here;
    // selected resource outputs were re-hashed while the files were assembled.
    private static bool LockedInputsUnchanged(DesignerWorkspace workspace,
        DisplayApplicationCompilationResult compiled, List<ProfileDiagnostic> diagnostics)
    {
        DesignerProjectLoadResult manifest = DesignerProjectLoader.LoadFile(workspace.ManifestPath);
        DesignerProductResolutionResult snapshots = DesignerProductResolver.ResolveSnapshots(workspace);
        bool snapshotsValid = snapshots.Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
        if (manifest.Sha256 == workspace.ResolvedProject.ManifestSha256 && snapshotsValid &&
            snapshots.Definition?.Sha256 == compiled.DisplayDefinitionSha256 &&
            snapshots.Input?.Sha256 == compiled.LogicalInputSha256)
        {
            return true;
        }
        diagnostics.AddRange(snapshots.Diagnostics.Where(item => item.Severity == ProfileDiagnosticSeverity.Error));
        diagnostics.Add(new("LVUI4103", ProfileDiagnosticSeverity.Error, "$.project",
            "The project manifest or its display/input snapshots changed after they were resolved. Reopen the project before generating."));
        return false;
    }

    private static void Publish(string directory, SortedDictionary<string, byte[]> files,
        ComponentManifest manifest, List<ProfileDiagnostic> diagnostics)
    {
        string manifestPath = GeneratedFileAccess.Contained(directory, ManifestName);
        SortedDictionary<string, string> previous = new(StringComparer.Ordinal);
        string legacyPath = GeneratedFileAccess.Contained(directory, SimulatorJson.ManifestName);
        bool migratedLegacy = false;
        if (File.Exists(manifestPath))
        {
            if (new FileInfo(manifestPath).Length > 1_048_576)
            {
                throw new InvalidDataException("Component manifest exceeds its size limit.");
            }
            if (File.Exists(legacyPath))
            {
                throw new InvalidDataException("Both component and simulator manifests exist. Reconcile their ownership before regenerating.");
            }
            List<ProfileDiagnostic> manifestDiagnostics = [];
            ComponentManifest prior = DisplayDefinitionJson.Read<ComponentManifest>(
                File.ReadAllBytes(manifestPath), "LVUI", manifestDiagnostics)
                ?? throw new InvalidDataException("Invalid component manifest.");
            if (prior.SchemaVersion != 1 || prior.GenerationStatus is not ("complete" or "incomplete") ||
                prior.SurfaceStatus != "passed" || prior.TargetStatus != "unevaluated" ||
                !Enum.TryParse<SurfacePixelFormat>(prior.DestinationFormat, out var format) || !Enum.IsDefined(format) ||
                prior.DestinationFormat != format.ToString() ||
                !ProfileRules.IsLowercaseSha256(prior.FormSha256) ||
                !ProfileRules.IsLowercaseSha256(prior.DisplayDefinitionSha256) ||
                !ProfileRules.IsLowercaseSha256(prior.LogicalInputSha256) ||
                prior.GeneratedFiles is null || prior.GeneratedFiles.Count is 0 or > 1024)
            {
                throw new InvalidDataException("Invalid component manifest. Reconcile it before regenerating.");
            }
            previous = prior.GeneratedFiles;
        }
        else if (SimulatorFiles.LoadManifest(directory) is { } legacy)
        {
            // Migration only removes verified generator-owned files. Handwritten
            // application.hpp was never owned by that exporter and is preserved.
            SimulatorFiles.VerifyGeneratedFiles(directory, legacy);
            previous = legacy.GeneratedFiles;
            migratedLegacy = true;
            diagnostics.Add(new("LVUI4101", ProfileDiagnosticSeverity.Warning, "$.component",
                "Migrated the legacy simulator scaffold to a UI component. Any existing application.hpp is preserved; connect behavior in your application's UI code."));
        }
        StringComparer fileComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        HashSet<string> ownedPaths = new(fileComparer);
        foreach ((string relative, string hash) in previous)
        {
            if (fileComparer.Equals(relative, ManifestName) || fileComparer.Equals(relative, "simulation.json") ||
                fileComparer.Equals(relative, "application.hpp") || !ProfileRules.IsLowercaseSha256(hash))
            {
                throw new InvalidDataException("Invalid generated-file ownership entry.");
            }
            string path = GeneratedFileAccess.Contained(directory, relative);
            if (!ownedPaths.Add(path) || !File.Exists(path) || GeneratedFileAccess.Hash(File.ReadAllBytes(path)) != hash)
            {
                throw new InvalidDataException($"Generated file '{relative}' is missing or edited. Reconcile it before regenerating.");
            }
        }
        foreach (string relative in files.Keys)
        {
            string path = GeneratedFileAccess.Contained(directory, relative);
            if (Directory.Exists(path) || File.Exists(path) && !previous.ContainsKey(relative))
            {
                throw new InvalidDataException($"Generation would overwrite an unowned file '{relative}'.");
            }
        }
        if (Directory.Exists(manifestPath))
        {
            throw new InvalidDataException("The component manifest path is a directory.");
        }
        // Once publication starts, an interrupted write is visibly incomplete.
        // Application builds must check this marker before consuming any output.
        AtomicFileWriter.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, SimulatorJson.Options));
        foreach ((string relative, byte[] bytes) in files)
        {
            AtomicFileWriter.WriteAllBytes(GeneratedFileAccess.Contained(directory, relative), bytes);
        }
        foreach (string obsolete in previous.Keys.Except(files.Keys, StringComparer.Ordinal))
        {
            File.Delete(GeneratedFileAccess.Contained(directory, obsolete));
        }
        if (migratedLegacy)
        {
            File.Delete(legacyPath);
        }
        AtomicFileWriter.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(
            manifest with { GenerationStatus = "complete" }, SimulatorJson.Options));
    }

    private sealed record ComponentManifest
    {
        public int SchemaVersion { get; init; } = 1;
        public string GenerationStatus { get; init; } = "incomplete";
        public string SurfaceStatus { get; init; } = "passed";
        public string TargetStatus { get; init; } = "unevaluated";
        public required string DestinationFormat { get; init; }
        public required string FormSha256 { get; init; }
        public required string DisplayDefinitionSha256 { get; init; }
        public required string LogicalInputSha256 { get; init; }
        public required SortedDictionary<string, string> GeneratedFiles { get; init; }
    }

    private const string ComponentCMake = """
        # Generated UI component. The parent application owns executables and devices.
        if(NOT TARGET LitheView::Core)
            find_package(LitheView CONFIG REQUIRED)
        endif()
        include("${CMAKE_CURRENT_LIST_DIR}/resources.cmake")
        add_library(litheview_generated_component INTERFACE)
        add_library(LitheView::GeneratedComponent ALIAS litheview_generated_component)
        target_link_libraries(litheview_generated_component INTERFACE LitheView::Core)
        target_include_directories(litheview_generated_component INTERFACE "${CMAKE_CURRENT_LIST_DIR}")
        if(LITHEVIEW_SELECTED_RESOURCE_SOURCES)
            target_sources(litheview_generated_component INTERFACE ${LITHEVIEW_SELECTED_RESOURCE_SOURCES})
        endif()

        """;
}
