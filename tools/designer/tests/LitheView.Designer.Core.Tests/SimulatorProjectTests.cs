// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;
using LitheView.Designer.Core.Simulation;

namespace LitheView.Designer.Core.Tests;

internal static class SimulatorProjectTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static void Run(string repositoryRoot)
    {
        string root = Path.Combine(repositoryRoot, "build", "designer-simulator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(
            Path.Combine(repositoryRoot, "profiles", "display-surfaces"));
        DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.LoadFile(Path.Combine(repositoryRoot,
            "profiles", "display-surfaces", "stm32h750b-dk-rk043fn48h-rgb565.json"));
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(Path.Combine(repositoryRoot, "schema", "widgets.json"));
        Require(profiles.IsValid && surface.IsValid && catalog.IsValid, "Checked contracts must load.");
        DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = Path.Combine(root, "product"), ProjectId = "test.simulator",
            DisplayName = "Simulator tutorial", SchemaReference = "project-manifest.schema.json",
            Surface = surface, Catalog = catalog.Catalog!, IncludeTutorialStarter = true
        }, profiles);
        Require(created.IsValid && created.Workspace is not null, Messages(created.Diagnostics));
        DesignerWorkspace workspace = created.Workspace!;
        string output = Path.Combine(workspace.ProjectDirectory, "simulation");
        SimulatorExportResult first = SimulatorProjectExporter.Export(workspace, output);
        Require(first.IsValid, Messages(first.Diagnostics));
        SimulatorProjectManifest manifest = ReadManifest(output);
        Require(manifest.GenerationStatus == "complete" && manifest.SurfaceStatus == "passed" &&
            manifest.TargetStatus == "unevaluated" && manifest.DestinationFormat == "RGB565",
            "The simulator must remain usable without selecting or qualifying an MCU.");
        string[] required = ["display.hpp", "form.hpp", "resources.hpp", "product_config.hpp", "runtime_types.hpp",
            "runtime.hpp", "CMakeLists.txt", "resources.cmake", ".vscode/tasks.json", ".vscode/launch.json",
            "display-definition.json", "logical-input.json", "form.litheview.xml", "headless.cpp", "win32.cpp", "README.md"];
        Require(required.All(manifest.GeneratedFiles.ContainsKey) &&
            !manifest.GeneratedFiles.ContainsKey("application.hpp"),
            "Generated build inputs and developer-owned behavior must have separate ownership.");
        string application = Path.Combine(output, "application.hpp");
        string tutorial = File.ReadAllText(application);
        Require(tutorial.Contains("ACTIVE", StringComparison.Ordinal) && tutorial.Contains("100U", StringComparison.Ordinal),
            "The explicit tutorial starter supplies real typed application behavior.");
        File.AppendAllText(application, "\n// Developer-owned marker.\n", Encoding.UTF8);
        byte[] editedApplication = File.ReadAllBytes(application);
        SimulatorExportResult repeated = SimulatorProjectExporter.Export(workspace, output);
        Require(repeated.IsValid && File.ReadAllBytes(application).AsSpan().SequenceEqual(editedApplication),
            "Regeneration must preserve application code byte for byte.");
        Require(JsonSerializer.Serialize(ReadManifest(output).GeneratedFiles) == JsonSerializer.Serialize(manifest.GeneratedFiles),
            "Unchanged project inputs must produce deterministic generated files.");

        string standalone = Path.Combine(root, "standalone");
        Require(SimulatorProjectExporter.Export(workspace, standalone).IsValid &&
            File.ReadAllBytes(Path.Combine(standalone, "application.hpp")).AsSpan().SequenceEqual(editedApplication),
            "A new standalone export must start with the project's real application hook.");
        string formHeader = Path.Combine(output, "form.hpp");
        byte[] originalForm = File.ReadAllBytes(formHeader);
        File.AppendAllText(formHeader, "\n// Conflicting generated edit.\n", Encoding.UTF8);
        byte[] conflictingForm = File.ReadAllBytes(formHeader);
        Require(!SimulatorProjectExporter.Export(workspace, output).IsValid &&
            File.ReadAllBytes(formHeader).AsSpan().SequenceEqual(conflictingForm),
            "A conflicting generated edit must not be overwritten.");
        File.WriteAllBytes(formHeader, originalForm);
        Require(!SimulatorProjectExporter.Export(workspace, workspace.ProjectDirectory).IsValid,
            "Export must not replace its source project.");
        Require(!SimulatorProjectExporter.Export(workspace, Path.Combine(workspace.ProjectDirectory, "forms", "output")).IsValid,
            "Export must not write into source forms.");
        string unrelated = Path.Combine(root, "unrelated");
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(unrelated, "CMakeLists.txt"), "project(KeepMe)");
        Require(!SimulatorProjectExporter.Export(workspace, unrelated).IsValid &&
            File.ReadAllText(Path.Combine(unrelated, "CMakeLists.txt")) == "project(KeepMe)",
            "Unrelated build projects must remain untouched.");
        string existingUi = Path.Combine(root, "Control Panel", "Tasks", "UI");
        Directory.CreateDirectory(existingUi);
        string ownerCode = Path.Combine(existingUi, "ui_task.cpp");
        File.WriteAllText(ownerCode, "// Existing application task.\n");
        DesignerWorkspaceLoadResult integrated = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = existingUi, ProjectId = "test.existing", DisplayName = "Existing panel",
            SchemaReference = "project-manifest.schema.json", Surface = surface, Catalog = catalog.Catalog!,
            IncludeTutorialStarter = true, AllowExistingDirectory = true
        }, profiles);
        Require(integrated.IsValid && File.ReadAllText(ownerCode) == "// Existing application task.\n",
            "Adding a UI project must preserve existing application sources: " + Messages(integrated.Diagnostics));
        string component = Path.Combine(existingUi, "Generated UI");
        string externalBuild = Path.Combine(root, "Control Panel", "Build Output", "UI");
        Directory.CreateDirectory(component);
        string adapter = Path.Combine(component, "device_adapter.hpp");
        File.WriteAllText(adapter, "// Developer-owned device adapter.\n");
        SimulatorExportResult mixed = SimulatorProjectExporter.Export(integrated.Workspace!, component, externalBuild);
        Require(mixed.IsValid && File.ReadAllText(adapter) == "// Developer-owned device adapter.\n",
            "Export must track individual files and preserve unrelated neighbors: " + Messages(mixed.Diagnostics));
        using (JsonDocument launch = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(component, ".vscode", "launch.json"))))
        {
            string expected = "${workspaceFolder}/" + Path.GetRelativePath(component, externalBuild).Replace('\\', '/') +
                "/debug/host/litheview_generated_simulator.exe";
            Require(launch.RootElement.GetProperty("configurations")[0].GetProperty("program").GetString() == expected,
                "Generated debugger paths must follow the configured build directory and remain relocatable.");
        }
        CheckCreationCollision(root, profiles, surface, catalog.Catalog!);
        string parentConflict = Path.Combine(root, "Conflicting parent");
        Directory.CreateDirectory(parentConflict);
        File.WriteAllText(Path.Combine(parentConflict, ".vscode"), "preserve");
        Require(!SimulatorProjectExporter.Export(workspace, parentConflict).IsValid &&
            File.ReadAllText(Path.Combine(parentConflict, ".vscode")) == "preserve" &&
            !File.Exists(Path.Combine(parentConflict, "simulation.json")),
            "An existing file used as a generated parent must fail before publication starts.");
        Require(SimulatorProjectExporter.Export(workspace, output).IsValid, "The reconciled project must export again.");
        string manifestPath = Path.Combine(output, "simulation.json");
        string canonicalManifest = File.ReadAllText(manifestPath);
        File.WriteAllText(manifestPath, canonicalManifest.Insert(canonicalManifest.IndexOf('{') + 1, "\"schemaVersion\":1,"));
        Require(!SimulatorProjectExporter.Export(workspace, output).IsValid,
            "Duplicate manifest properties must fail before generated files can be replaced.");
        File.WriteAllText(manifestPath, canonicalManifest);
        if (OperatingSystem.IsWindows())
        {
            CheckCancelledBuild(output, repositoryRoot);
            CheckCancelledBuild(component, repositoryRoot, externalBuild);
            string report = Path.Combine(externalBuild, "debug", "build-result.json");
            File.WriteAllText(report, "{\"passed\":true}");
            string ownedHeader = Path.Combine(component, "form.hpp");
            byte[] ownedBytes = File.ReadAllBytes(ownedHeader);
            File.AppendAllText(ownedHeader, "// Edited generated header.\n");
            SimulatorBuildResult invalid = SimulatorBuildService.BuildAsync(component, repositoryRoot, "Debug",
                _ => { }, CancellationToken.None, externalBuild).GetAwaiter().GetResult();
            File.WriteAllBytes(ownedHeader, ownedBytes);
            using (JsonDocument failed = JsonDocument.Parse(File.ReadAllBytes(report)))
            {
                Require(!invalid.IsValid && !failed.RootElement.GetProperty("passed").GetBoolean(),
                    "Failed preflight must invalidate an already-owned successful build report.");
            }
            SimulatorBuildResult otherOwner = SimulatorBuildService.BuildAsync(output, repositoryRoot, "Debug",
                _ => { }, CancellationToken.None, externalBuild).GetAwaiter().GetResult();
            Require(!otherOwner.IsValid && otherOwner.Diagnostics.Any(item =>
                item.Message.Contains("different UI component", StringComparison.Ordinal)),
                "Two UI components must not reuse or clean each other's build directory.");
            string occupiedBuild = Path.Combine(root, "Firmware Build");
            Directory.CreateDirectory(Path.Combine(occupiedBuild, "debug"));
            string keep = Path.Combine(occupiedBuild, "debug", "firmware.o");
            File.WriteAllText(keep, "preserve");
            SimulatorBuildResult blocked = SimulatorBuildService.BuildAsync(component, repositoryRoot, "Debug",
                _ => { }, CancellationToken.None, occupiedBuild).GetAwaiter().GetResult();
            Require(!blocked.IsValid && File.ReadAllText(keep) == "preserve" &&
                !File.Exists(Path.Combine(occupiedBuild, "debug", "build-result.json")),
                "A configured simulator must not adopt or clean an existing firmware build.");
        }
        Console.WriteLine($"Simulator contract fixture: {workspace.ManifestPath}");
    }

    private static void CheckCreationCollision(string root, DisplaySurfaceCatalogLoadResult profiles,
        DisplaySurfaceLoadResult surface, WidgetCatalog catalog)
    {
        string existing = Path.Combine(root, "Existing source collision");
        Directory.CreateDirectory(Path.Combine(existing, "profiles"));
        string snapshot = Path.Combine(existing, "profiles", "display-surface.json");
        File.WriteAllText(snapshot, "preserve");
        DesignerWorkspaceLoadResult result = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = existing, ProjectId = "test.collision", DisplayName = "Collision",
            SchemaReference = "project-manifest.schema.json", Surface = surface, Catalog = catalog,
            AllowExistingDirectory = true
        }, profiles);
        Require(!result.IsValid && File.ReadAllText(snapshot) == "preserve" &&
            !File.Exists(Path.Combine(existing, DesignerWorkspaceService.ManifestFileName)) &&
            !Directory.Exists(Path.Combine(existing, "forms")),
            "Existing-folder creation must preflight every filename before writing any new source file.");
    }

    private static void CheckCancelledBuild(string output, string repositoryRoot, string? buildDirectory = null)
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        SimulatorBuildResult stopped = SimulatorBuildService.BuildAsync(output, repositoryRoot, "Debug", _ => { }, cancelled.Token, buildDirectory)
            .GetAwaiter().GetResult();
        Require(!stopped.IsValid && stopped.ExecutablePath is null && stopped.Diagnostics.Any(item => item.Code == "LVSIM2002"),
            "Cancellation must not return a stale executable or invoke build tools.");
        using JsonDocument build = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(buildDirectory ?? Path.Combine(output, "build"), "debug", "build-result.json")));
        Require(!build.RootElement.GetProperty("passed").GetBoolean(), "A cancelled attempt must not retain a successful build marker.");
    }

    private static SimulatorProjectManifest ReadManifest(string output) =>
        JsonSerializer.Deserialize<SimulatorProjectManifest>(File.ReadAllBytes(Path.Combine(output, "simulation.json")),
            JsonOptions)!;

    private static string Messages(IEnumerable<ProfileDiagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
