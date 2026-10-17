// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;
using LitheView.Designer.Core.Simulation;

namespace LitheView.Designer.Core.Tests;

internal static class ComponentExportTests
{
    public static void Run(string repositoryRoot)
    {
        string root = Path.Combine(repositoryRoot, "build", "component-export-tests", Guid.NewGuid().ToString("N"));
        var profiles = ProfileCatalogLoader.LoadSurfaces(Path.Combine(repositoryRoot, "profiles", "display-surfaces"));
        var catalog = WidgetCatalogLoader.LoadFile(Path.Combine(repositoryRoot, "schema", "widgets.json"));
        var surface = profiles.Surfaces.Single(item => item.Profile!.Id == "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565");
        var created = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = Path.Combine(root, "project"), ProjectId = "test.component",
            DisplayName = "Component ownership", SchemaReference = "project-manifest.schema.json",
            Surface = surface, Catalog = catalog.Catalog!
        }, profiles);
        Require(created.IsValid, "Component fixture must load.");
        var workspace = created.Workspace!;
        string output = Path.Combine(root, "component");
        var first = ComponentProjectExporter.Export(workspace, output);
        Require(first.IsValid, string.Join("; ", first.Diagnostics.Select(item => item.Message)));
        Require(!File.Exists(Path.Combine(output, "application.hpp")) &&
            !File.Exists(Path.Combine(output, "win32.cpp")) && !Directory.Exists(Path.Combine(output, ".vscode")),
            "A UI component must not contain a second application or build/debug workflow.");
        byte[] original = File.ReadAllBytes(first.ManifestPath!);
        using (JsonDocument report = JsonDocument.Parse(original))
        {
            Require(report.RootElement.GetProperty("surfaceStatus").GetString() == "passed" &&
                report.RootElement.GetProperty("targetStatus").GetString() == "unevaluated",
                "A component validates its surface without claiming hardware qualification.");
        }
        Require(ComponentProjectExporter.Export(workspace, output).IsValid &&
            original.SequenceEqual(File.ReadAllBytes(first.ManifestPath!)), "Repeated generation must be deterministic.");
        string duplicateManifest = File.ReadAllText(first.ManifestPath!).Replace("\"schemaVersion\": 1",
            "\"schemaVersion\": 1, \"schemaVersion\": 1", StringComparison.Ordinal);
        File.WriteAllText(first.ManifestPath!, duplicateManifest);
        Require(!ComponentProjectExporter.Export(workspace, output).IsValid &&
            File.ReadAllText(first.ManifestPath!) == duplicateManifest,
            "Duplicate ownership-manifest keys must fail without changing files.");
        File.WriteAllBytes(first.ManifestPath!, original);
        File.WriteAllText(Path.Combine(output, "application.hpp"), "// handwritten behavior\n");
        Require(ComponentProjectExporter.Export(workspace, output).IsValid &&
            File.ReadAllText(Path.Combine(output, "application.hpp")) == "// handwritten behavior\n",
            "Generation must preserve user files without adopting them.");
        string form = Path.Combine(output, "form.hpp");
        File.AppendAllText(form, "// changed generated code\n");
        Require(!ComponentProjectExporter.Export(workspace, output).IsValid &&
            original.SequenceEqual(File.ReadAllBytes(first.ManifestPath!)), "Conflicts must fail before publication.");
        Require(!ComponentProjectExporter.Export(workspace, workspace.ProjectDirectory).IsValid &&
            !ComponentProjectExporter.Export(workspace, Path.Combine(workspace.ProjectDirectory, "forms", "generated")).IsValid,
            "Source directories must remain protected.");

        string legacy = Path.Combine(root, "legacy");
        Require(SimulatorProjectExporter.Export(workspace, legacy).IsValid, "Legacy fixture must export.");
        File.AppendAllText(Path.Combine(legacy, "application.hpp"), "// owned legacy handler\n");
        byte[] user = File.ReadAllBytes(Path.Combine(legacy, "application.hpp"));
        byte[] oldForm = File.ReadAllBytes(Path.Combine(legacy, "form.hpp"));
        File.AppendAllText(Path.Combine(legacy, "form.hpp"), "// migration conflict\n");
        Require(!ComponentProjectExporter.Export(workspace, legacy).IsValid &&
            File.Exists(Path.Combine(legacy, "win32.cpp")) && !File.Exists(Path.Combine(legacy, "component.json")),
            "A conflicting legacy export must not be partially migrated.");
        File.WriteAllBytes(Path.Combine(legacy, "form.hpp"), oldForm);
        var migrated = ComponentProjectExporter.Export(workspace, legacy);
        Require(migrated.IsValid && migrated.Diagnostics.Any(item => item.Code == "LVUI4101") &&
            !File.Exists(Path.Combine(legacy, "simulation.json")) && !File.Exists(Path.Combine(legacy, "win32.cpp")) &&
            !File.Exists(Path.Combine(legacy, ".vscode", "launch.json")) &&
            user.SequenceEqual(File.ReadAllBytes(Path.Combine(legacy, "application.hpp"))),
            "Migration must retire verified scaffold files and preserve handwritten behavior.");
        Require(ComponentProjectExporter.Export(workspace, legacy).IsValid, "Migrated output must support repeat generation.");
        File.WriteAllText(Path.Combine(legacy, "simulation.json"), "// unrelated file\n");
        Require(!ComponentProjectExporter.Export(workspace, legacy).IsValid &&
            File.ReadAllText(Path.Combine(legacy, "simulation.json")) == "// unrelated file\n",
            "A component cannot silently delete an unowned legacy marker.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
