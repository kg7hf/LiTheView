// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.App;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.App.Tests;

internal static partial class Program
{
    private static void SimulationCommandsExposeLifecycle(DesignerRepositoryContext repository)
    {
        using DesignerMainForm form = new(repository, startupProjectPath: null);
        Dictionary<string, ToolStripMenuItem> commands = SimulationMenus(form);
        Assert(commands.ContainsKey("Export C++..."), "Existing C++ export was removed.");
        Assert(!commands["Project Paths..."].Enabled, "Paths require an active project.");
        Assert(!commands["Generate UI"].Enabled, "Export requires an active project.");
        Assert(!commands.ContainsKey("Build and Run Simulator") && !commands.ContainsKey("Cancel Simulator Build"),
            "The designer must not build or launch applications.");
        ToolStripButton generate = Descendants(form).OfType<ToolStrip>()
            .SelectMany(strip => strip.Items.OfType<ToolStripButton>())
            .Single(button => button.AccessibleName == "Generate the UI component");
        Assert(!generate.Enabled, "Generation requires an active project.");
    }

    private static void WizardSeparatesInputAndTutorial(DesignerRepositoryContext repository)
    {
        using NewProjectWizard wizard = new(repository);
        ListBox surfaces = Descendants(wizard).OfType<ListBox>()
            .Single(control => control.AccessibleName == "Checked display surfaces");
        ComboBox inputs = Descendants(wizard).OfType<ComboBox>()
            .Single(control => control.AccessibleName == "Application logical input");
        CheckBox starter = Descendants(wizard).OfType<CheckBox>()
            .Single(control => control.AccessibleName == "Include button and status tutorial");
        DisplaySurfaceProfile rgb = repository.Profiles.Surfaces.First(item =>
            item.IsValid && item.Profile?.NativeDisplay.ColorModel == NativeColorModel.Rgb).Profile!;
        SelectWizardSurface(surfaces, rgb.DisplayName);
        Assert(inputs.Enabled && inputs.Items.Count == 4, "Supported surfaces need independent input choices.");
        Assert(starter.Enabled && starter.Checked, "The working tutorial should be offered by default.");
        inputs.SelectedIndex = 3;
        Assert(!starter.Enabled && !starter.Checked, "Output-only must not silently require control input.");
        inputs.SelectedIndex = 2;
        Assert(starter.Enabled, "Encoder plus select must support the optional tutorial.");
        DisplaySurfaceProfile legacy = repository.Profiles.Surfaces.First(item =>
            item.IsValid && item.Profile is not null &&
            item.Profile.NativeDisplay.ColorModel != NativeColorModel.Rgb).Profile!;
        SelectWizardSurface(surfaces, legacy.DisplayName);
        Assert(!inputs.Enabled && !starter.Enabled && !starter.Checked,
            "Legacy staging projects must not imply a native simulation contract.");
    }

    private static void SimulationMenuExportsCheckedProject(DesignerRepositoryContext repository)
    {
        // A unique retained fixture avoids touching a developer project and aids failed-test diagnosis.
        string directory = Path.Combine(repository.RepositoryRoot, "build", "designer-app-simulation-tests",
            Guid.NewGuid().ToString("N"));
        DisplaySurfaceLoadResult surface = repository.Profiles.Surfaces.First(item => item.IsValid &&
            item.Profile is { NativeDisplay.ColorModel: NativeColorModel.Rgb,
                PainterSurface.Format: SurfacePixelFormat.Rgb565, Shape: SurfaceShape.Rectangle } &&
            item.Profile.PixelSize.Width >= 320 && item.Profile.PixelSize.Height >= 240);
        DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = directory,
            ProjectId = "litheview.project.simulation-ui-test",
            DisplayName = "Simulation UI Test",
            SchemaReference = "litheview://schema/project-manifest/v1",
            Surface = surface,
            Catalog = repository.Catalog,
            Input = new()
            {
                Schema = "https://litheview.dev/schema/logical-input-configuration.schema.json",
                SchemaVersion = 1,
                Id = "litheview.input.pointer-keys",
                Pointer = true, Keys = true, Text = false, Encoder = false, Select = false
            },
            IncludeTutorialStarter = true
        }, repository.Profiles);
        Assert(created.IsValid && created.Workspace is not null,
            string.Join(Environment.NewLine, created.Diagnostics.Select(item => item.Message)));
        // The menu test must never launch a native worker, compiler, or generated program.
        using DesignerMainForm form = new(repository with
        {
            PreviewWorkerPath = Path.Combine(directory, "absent-preview-worker.exe")
        }, startupProjectPath: null);
        form.LoadWorkspace(created.Workspace!);
        Dictionary<string, ToolStripMenuItem> commands = SimulationMenus(form);
        Assert(commands["Generate UI"].Enabled,
            "A checked active project must enable simulation commands.");
        string application = Path.Combine(directory, "simulation", "application.hpp");
        Assert(File.Exists(application), "The tutorial must seed developer-owned application behavior.");
        File.AppendAllText(application, "\n// developer-owned App test marker\n");
        byte[] applicationBefore = File.ReadAllBytes(application);
        commands["Generate UI"].PerformClick();
        string manifest = Path.Combine(directory, "simulation", "component.json");
        ListView output = Descendants(form).OfType<ListView>()
            .Single(control => control.AccessibleName == "Designer diagnostics and output");
        Assert(File.Exists(manifest), "Export menu did not produce a simulation manifest: " +
            string.Join("; ", output.Items.Cast<ListViewItem>().Select(item => item.SubItems[2].Text)));
        foreach (string source in new[] { "headless.cpp", "win32.cpp", "runtime.hpp", "runtime_types.hpp" })
        {
            Assert(!File.Exists(Path.Combine(directory, "simulation", source)),
                $"Component generation emitted a simulator project file '{source}'.");
        }
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(manifest));
        Assert(json.RootElement.GetProperty("generationStatus").GetString() == "complete",
            "The exporter did not complete its checked generation.");
        Assert(json.RootElement.GetProperty("targetStatus").GetString() == "unevaluated",
            "Simulator export must not claim target feasibility.");
        commands["Generate UI"].PerformClick();
        Assert(applicationBefore.AsSpan().SequenceEqual(File.ReadAllBytes(application)),
            "Repeated UI export overwrote developer-owned application code.");
        Assert(output.Items.Cast<ListViewItem>().Any(item => item.SubItems[1].Text == "LVUI4001"),
            "The output pane did not report the exported project directory.");
        Assert(!commands.ContainsKey("Cancel Simulator Build"), "Generation must not add build actions.");
        byte[] manifestBefore = File.ReadAllBytes(manifest);
        int successesBefore = output.Items.Cast<ListViewItem>().Count(item => item.SubItems[1].Text == "LVUI4001");
        string inputPath = Path.Combine(directory, DesignerProductResolver.InputRelativePath);
        string inputJson = File.ReadAllText(inputPath);
        Assert(inputJson.Contains("litheview.input.pointer-keys", StringComparison.Ordinal),
            "Test input identity was not stored in the project snapshot.");
        File.WriteAllText(inputPath, inputJson.Replace("litheview.input.pointer-keys", "litheview.input.changed",
            StringComparison.Ordinal));
        commands["Generate UI"].PerformClick();
        Assert(output.Items.Cast<ListViewItem>().Count(item => item.SubItems[1].Text == "LVUI4001") == successesBefore &&
            output.Items.Cast<ListViewItem>().Any(item => item.Text == "error"),
            "The menu must reopen and reject a changed input snapshot before export.");
        Assert(manifestBefore.AsSpan().SequenceEqual(File.ReadAllBytes(manifest)),
            "A rejected snapshot must not replace the last successful simulation manifest.");
    }

    private static Dictionary<string, ToolStripMenuItem> SimulationMenus(DesignerMainForm form) =>
        Descendants(form).OfType<MenuStrip>()
            .SelectMany(menu => menu.Items.OfType<ToolStripMenuItem>()).SelectMany(Flatten)
            .ToDictionary(item => item.Text!.Replace("&", string.Empty, StringComparison.Ordinal),
                StringComparer.Ordinal);

    private static void SelectWizardSurface(ListBox surfaces, string displayName)
    {
        for (int index = 0; index < surfaces.Items.Count; ++index)
        {
            if (surfaces.GetItemText(surfaces.Items[index]) == displayName)
            {
                surfaces.SelectedIndex = index;
                return;
            }
        }
        throw new InvalidOperationException($"Wizard surface '{displayName}' was not found.");
    }
}
