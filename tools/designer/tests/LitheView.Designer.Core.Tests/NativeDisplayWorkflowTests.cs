// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

// Version-3 projects created by `project create-native` go through the ordinary
// open -> edit -> save -> reopen -> preview -> Generate UI workflow with no MCU target.
internal static class NativeDisplayWorkflowTests
{
    private sealed record NativeCase(string Name, SurfacePixelFormat Format, PreviewPixelFormat PreviewFormat,
        int Width, int Height, string CppFormat, string CppAddressSpace);

    private static readonly NativeCase[] Cases =
    [
        new("synthetic-r2", SurfacePixelFormat.R2Packed2x2, PreviewPixelFormat.R2Packed2x2, 512, 384, "r2Packed2x2", "emitters"),
        new("synthetic-red-cell", SurfacePixelFormat.Rrrr2222, PreviewPixelFormat.Rrrr2222, 256, 192, "rrrr2222", "cells"),
        new("synthetic-red-yellow", SurfacePixelFormat.Ryry2222, PreviewPixelFormat.Ryry2222, 256, 192, "ryry2222", "cells")
    ];

    // The edited panels, in logical emitters or cells: legacy colors 0x000000ff (red),
    // 0x00000080 (half red) and 0x0000ffff (yellow). The second panel splits R2 bytes.
    private static readonly (FormRectangle Frame, uint Color, int Red, int Green)[] Panels =
    [
        (new(8, 4, 16, 8), 0x00000080U, 128, 0),
        (new(41, 33, 5, 3), 0x0000ffffU, 255, 255)
    ];

    private static readonly FormRectangle ButtonFrame = new(100, 100, 80, 30);

    private const string HandlerFile = """
        // Application-owned. Generate UI never rewrites this file.
        #pragma once
        #include "Generated/product_config.hpp"
        class Form : public lithe::simulator::Form::Instance<>
        {
        protected:
            // <litheview-handlers>
            // litheview-handler component.button.1 onButton1Clicked
            void onButton1Clicked() noexcept override
            {
                getButton1()->setText("PRESSED");
            }
            // </litheview-handlers>
        };

        """;

    public static void Run(string repositoryRoot, string workerPath)
    {
        string temporary = Path.Combine(repositoryRoot, "build", "native-display-workflow-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            WidgetCatalog widgets = WidgetCatalogLoader.LoadFile(Path.Combine(repositoryRoot, "schema", "widgets.json")).Catalog!;
            DisplaySurfaceCatalogLoadResult surfaces = ProfileCatalogLoader.LoadSurfaces(
                Path.Combine(repositoryRoot, "profiles", "display-surfaces"));
            LogicalInputConfigurationProfile input = LogicalInputConfigurationLoader.LoadFile(
                Path.Combine(repositoryRoot, "profiles", "logical-inputs", "encoder-select.json")).Configuration!;
            foreach (NativeCase nativeCase in Cases)
            {
                CompleteWorkflowAsync(repositoryRoot, temporary, widgets, surfaces, input, nativeCase, workerPath)
                    .GetAwaiter().GetResult();
            }
            StaleInputsBlockPublication(repositoryRoot, temporary, widgets, surfaces, input);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static async Task CompleteWorkflowAsync(string repositoryRoot, string root, WidgetCatalog widgets,
        DisplaySurfaceCatalogLoadResult surfaces, LogicalInputConfigurationProfile input, NativeCase nativeCase,
        string workerPath)
    {
        DesignerWorkspace created = Create(repositoryRoot, root, nativeCase.Name, widgets, input);
        DesignerDocumentSession session = new(created.Form, widgets);
        Require(session.SetProperty("component.main.root", new FormColorValue("background-color", 0x000000ffU)).IsValid,
            "The shared editor must recolor the native root panel.");
        foreach ((FormRectangle frame, uint color, _, _) in Panels)
        {
            Require(session.AddWidget("lithe.widget.panel", "component.main.root").IsValid, "The editor must add a panel.");
            string panel = session.Document.Components[^1].ComponentId;
            Require(session.SetFrame(panel, frame).IsValid &&
                session.SetProperty(panel, new FormColorValue("background-color", color)).IsValid,
                "The editor must place and color a native panel.");
        }
        Require(DesignerWorkspaceService.SaveForm(created, session.Document).IsValid, "The native form must save canonically.");
        DesignerWorkspace edited = Open(created.ManifestPath, surfaces, widgets);
        Require(edited.Form.Components.Length == 3, "Reopening must restore the edited native panels.");

        // The worker renders native storage; compare it with the independent packing oracle.
        NativeStorageOracle.Fill[] scene =
        [
            new(0, 0, nativeCase.Width, nativeCase.Height, 255, 0),
            .. Panels.Select(panel => new NativeStorageOracle.Fill(
                panel.Frame.X, panel.Frame.Y, panel.Frame.Width, panel.Frame.Height, panel.Red, panel.Green))
        ];
        await using PreviewWorkerClient client = new(workerPath);
        PreviewSurface surface = PreviewSurfaceFactory.Create(edited.ResolvedProject.Display);
        Require(surface.PixelFormat == nativeCase.PreviewFormat && surface.Quantization == PreviewQuantization.Native,
            "The native workspace must preview its own packed storage.");
        byte[] expected = NativeStorageOracle.Expected(nativeCase.PreviewFormat, nativeCase.Width, nativeCase.Height,
            surface.StrideBytes, scene);
        byte[] panelsSnapshot = FormXmlWriter.Write(edited.Form, widgets).Utf8!;
        PreviewFrame full = await client.RenderAsync(new PreviewRenderRequest(surface, panelsSnapshot),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        PreviewFrame tiled = await client.RenderAsync(new PreviewRenderRequest(surface, panelsSnapshot,
            RenderMode: PreviewRenderMode.Tiled), TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Require(full.Status == PreviewStatus.Ok && full.Pixels.AsSpan().SequenceEqual(expected) &&
            tiled.Status == PreviewStatus.Ok && tiled.Pixels.AsSpan().SequenceEqual(expected),
            $"Native {nativeCase.Name} preview bytes must equal the independent oracle: {full.Diagnostic}");

        session = new(edited.Form, widgets);
        Require(session.AddWidget("lithe.widget.button", "component.main.root").IsValid &&
            session.SetFrame("component.button.1", ButtonFrame).IsValid,
            $"The shared editor must add a button to the native {nativeCase.Name} form.");
        Require(DesignerWorkspaceService.SaveForm(edited, session.Document).IsValid, "The native form must save canonically.");
        DesignerWorkspace workspace = Open(created.ManifestPath, surfaces, widgets);
        Require(workspace.Form.Components.Any(item => item.ComponentId == "component.button.1") &&
            workspace.ResolvedProject.Display.Format == nativeCase.Format,
            "Reopening must restore the edited native form and its display contract.");
        PreviewFrame withButton = await client.RenderAsync(new PreviewRenderRequest(surface,
            FormXmlWriter.Write(workspace.Form, widgets).Utf8!), TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Require(withButton.Status == PreviewStatus.Ok && !withButton.Pixels.AsSpan().SequenceEqual(expected),
            "The reopened native form with its button must preview natively.");
        // The checked C++ consumer compiles this exact saved form for each native configuration.
        Require(File.ReadAllBytes(workspace.FormPath).SequenceEqual(File.ReadAllBytes(Path.Combine(repositoryRoot,
                "tests", "native_component_consumer", "forms", nativeCase.Name + ".litheview.xml"))),
            "The native component consumer form must match the edited workflow form.");

        string handlers = Path.Combine(workspace.ProjectDirectory, "form_handlers.hpp");
        File.WriteAllText(handlers, HandlerFile);
        string output = Path.Combine(workspace.ProjectDirectory, "Generated");
        ComponentExportResult exported = ComponentProjectExporter.Export(workspace, output);
        Require(exported.IsValid, $"Native {nativeCase.Name} generation failed: {Messages(exported.Diagnostics)}");
        using JsonDocument component = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, ComponentProjectExporter.ManifestName)));
        JsonElement manifest = component.RootElement;
        Require(manifest.GetProperty("generationStatus").GetString() == "complete" &&
            manifest.GetProperty("surfaceStatus").GetString() == "passed" &&
            manifest.GetProperty("targetStatus").GetString() == "unevaluated" &&
            manifest.GetProperty("destinationFormat").GetString() == nativeCase.Format.ToString() &&
            manifest.GetProperty("displayDefinitionSha256").GetString() == workspace.ResolvedProject.Display.DefinitionSha256 &&
            manifest.GetProperty("logicalInputSha256").GetString() == workspace.ResolvedProject.Display.InputSha256,
            "The component must record the locked native display and input with the target unevaluated.");
        string display = File.ReadAllText(Path.Combine(output, "display.hpp"));
        string form = File.ReadAllText(Path.Combine(output, "form.hpp"));
        Require(display.Contains($"PixelFormat::{nativeCase.CppFormat}, DisplayAddressSpace::{nativeCase.CppAddressSpace}", StringComparison.Ordinal),
            "The generated display configuration must select the native packed destination.");
        Require(form.Contains("auto* getButton1() noexcept", StringComparison.Ordinal) &&
            form.Contains("virtual void onButton1Clicked() noexcept", StringComparison.Ordinal),
            "Generated native forms must keep named control accessors and button-handler hooks.");
        string profiles = Path.Combine(workspace.ProjectDirectory, "profiles");
        Require(File.ReadAllBytes(Path.Combine(output, "display-definition.json"))
                .SequenceEqual(File.ReadAllBytes(Path.Combine(profiles, "display-definition.json"))) &&
            File.ReadAllBytes(Path.Combine(output, "logical-input.json"))
                .SequenceEqual(File.ReadAllBytes(Path.Combine(profiles, "logical-input.json"))),
            "The component must publish exactly the locked snapshots.");
        Require(File.ReadAllText(handlers) == HandlerFile, "Generation must not rewrite application-owned handler bodies.");

        Dictionary<string, byte[]> first = Snapshot(output);
        Require(ComponentProjectExporter.Export(Open(workspace.ManifestPath, surfaces, widgets), output).IsValid &&
            Same(first, Snapshot(output)), "Regenerating an unchanged native project must be byte-identical.");
    }

    private static void StaleInputsBlockPublication(string repositoryRoot, string root, WidgetCatalog widgets,
        DisplaySurfaceCatalogLoadResult surfaces, LogicalInputConfigurationProfile input)
    {
        // Snapshot changed after the workspace resolved it.
        DesignerWorkspace definitionChanged = Create(repositoryRoot, root, "synthetic-red-cell", widgets, input, "stale-definition");
        string definitionPath = Path.Combine(definitionChanged.ProjectDirectory, DesignerProductResolver.DefinitionRelativePath);
        DisplayDefinitionProfile definition = DisplayDefinitionProfileLoader.LoadFile(definitionPath).Profile!;
        File.WriteAllBytes(definitionPath, DisplayDefinitionCanonicalizer.Canonicalize(definition with { DisplayName = "changed" }));
        string output = Path.Combine(definitionChanged.ProjectDirectory, "Generated");
        ComponentExportResult stale = ComponentProjectExporter.Export(definitionChanged, output);
        Require(!stale.IsValid && Has(stale.Diagnostics, "LVDP1003") && !Directory.Exists(output),
            "A changed definition snapshot must block publication before any file is written.");

        // Manifest changed after the workspace resolved it.
        DesignerWorkspace manifestChanged = Create(repositoryRoot, root, "synthetic-red-yellow", widgets, input, "stale-manifest");
        File.WriteAllBytes(manifestChanged.ManifestPath, DesignerProjectCanonicalizer.Canonicalize(
            manifestChanged.Manifest with { DisplayName = "Changed after open" }));
        output = Path.Combine(manifestChanged.ProjectDirectory, "Generated");
        ComponentExportResult staleManifest = ComponentProjectExporter.Export(manifestChanged, output);
        Require(!staleManifest.IsValid && Has(staleManifest.Diagnostics, "LVUI4103") && !Directory.Exists(output),
            "A changed project manifest must block publication before any file is written.");

        // A selected native resource changed after the first generation.
        DesignerWorkspace project = Create(repositoryRoot, root, "synthetic-r2", widgets, input, "resource");
        string fixture = Path.Combine(repositoryRoot, "tests", "rendering", "display-model-generated");
        foreach (string relative in new[]
                 {
                     "resources/gallop.resource.json", "assets/gallop_source.png",
                     "generated/resources/gallop-alpha1.cpp", "generated/resources/gallop-r2.cpp"
                 })
        {
            string target = Path.Combine(project.ProjectDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(fixture, relative), target);
        }
        File.Copy(Path.Combine(fixture, "synthetic-r2.litheview.xml"), project.FormPath, overwrite: true);
        File.WriteAllBytes(project.ManifestPath, DesignerProjectCanonicalizer.Canonicalize(
            project.Manifest with { ResourceManifests = ["resources/gallop.resource.json"] }));
        DesignerWorkspace withResource = Open(project.ManifestPath, surfaces, widgets);
        output = Path.Combine(withResource.ProjectDirectory, "ComponentOutput");
        ComponentExportResult selected = ComponentProjectExporter.Export(withResource, output);
        Require(selected.IsValid && File.ReadAllText(Path.Combine(output, "resources.cmake")).Contains("assets/", StringComparison.Ordinal),
            "A native project must publish its selected native resource: " + Messages(selected.Diagnostics));
        Dictionary<string, byte[]> published = Snapshot(output);
        File.AppendAllText(Path.Combine(withResource.ProjectDirectory, "generated", "resources", "gallop-r2.cpp"), "\n");
        ComponentExportResult tampered = ComponentProjectExporter.Export(withResource, output);
        Require(!tampered.IsValid && Same(published, Snapshot(output)),
            "A changed selected resource must block publication and leave the previous output unchanged.");
    }

    private static DesignerWorkspace Create(string repositoryRoot, string root, string definition, WidgetCatalog widgets,
        LogicalInputConfigurationProfile input, string? name = null)
    {
        string projectName = name ?? definition;
        DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNativeDisplayProject(new()
        {
            ProjectDirectory = Path.Combine(root, projectName),
            ProjectId = "example.native-workflow." + projectName,
            DisplayName = "Native workflow " + projectName,
            Definition = DisplayDefinitionProfileLoader.LoadFile(
                Path.Combine(repositoryRoot, "profiles", "display-definitions", definition + ".json")),
            Input = input,
            Catalog = widgets
        });
        Require(created.IsValid, $"Native {projectName} creation failed: {Messages(created.Diagnostics)}");
        return created.Workspace!;
    }

    private static DesignerWorkspace Open(string manifestPath, DisplaySurfaceCatalogLoadResult surfaces, WidgetCatalog widgets)
    {
        DesignerWorkspaceLoadResult opened = DesignerWorkspaceService.Open(manifestPath, surfaces, widgets);
        Require(opened.IsValid, "The native project must open: " + Messages(opened.Diagnostics));
        return opened.Workspace!;
    }

    private static Dictionary<string, byte[]> Snapshot(string directory) =>
        Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(directory, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static bool Same(Dictionary<string, byte[]> first, Dictionary<string, byte[]> second) =>
        first.Count == second.Count && first.All(item =>
            second.TryGetValue(item.Key, out byte[]? bytes) && bytes.AsSpan().SequenceEqual(item.Value));

    private static bool Has(IEnumerable<ProfileDiagnostic> diagnostics, string code) =>
        diagnostics.Any(item => item.Code == code && item.Severity == ProfileDiagnosticSeverity.Error);

    private static string Messages(IEnumerable<ProfileDiagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
