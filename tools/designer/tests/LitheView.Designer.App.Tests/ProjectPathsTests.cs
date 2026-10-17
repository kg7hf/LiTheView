// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Nodes;
using LitheView.Designer.App;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.App.Tests;

internal static partial class Program
{
    private static void WizardExistingFolderPreservesApplication(DesignerRepositoryContext repository)
    {
        string application = Path.Combine(repository.RepositoryRoot, "build", "designer-app-path-tests",
            Guid.NewGuid().ToString("N"));
        string project = Path.Combine(application, "Tasks", "UI");
        Directory.CreateDirectory(project);
        string existingSource = Path.Combine(project, "existing.cpp");
        File.WriteAllText(existingSource, "// application-owned source\n");
        using NewProjectWizard wizard = new(repository);
        CheckBox existing = Descendants(wizard).OfType<CheckBox>()
            .Single(control => control.AccessibleName == "Use an existing application folder");
        Assert(!existing.Checked && !wizard.ConfigurePathsAfterCreation,
            "Standalone creation must remain the default.");
        SelectWizardSurface(Descendants(wizard).OfType<ListBox>()
            .Single(control => control.AccessibleName == "Checked display surfaces"),
            PathTestSurface(repository).Profile!.DisplayName);
        FindPathTextBox(wizard, "Project name").Text = "Existing Application";
        existing.Checked = true;
        FindPathTextBox(wizard, "Project folder").Text = project;
        Assert(wizard.ConfigurePathsAfterCreation, "Existing-folder creation must offer integration paths next.");
        Assert(wizard.TryCreateWorkspace(), Descendants(wizard).OfType<Label>()
            .Single(control => control.AccessibleName == "Wizard validation message").Text);
        Assert(wizard.CreatedWorkspace?.ManifestPath == Path.Combine(project, DesignerWorkspaceService.ManifestFileName),
            "Existing-folder mode must place the project in the selected folder, without a name subdirectory.");
        Assert(File.ReadAllText(existingSource) == "// application-owned source\n",
            "Existing-folder creation changed an unrelated application file.");
        string settings = Path.ChangeExtension(wizard.CreatedWorkspace!.ManifestPath, ".paths.json");
        using (ProjectPathsDialog cancelled = new(wizard.CreatedWorkspace.ManifestPath, repository.RepositoryRoot))
        {
            FindPathTextBox(cancelled, "Generated UI component").Text = "other-component";
        }
        Assert(!File.Exists(settings) && File.Exists(wizard.CreatedWorkspace.ManifestPath),
            "Cancelling path setup must leave the new project and its default paths intact.");
    }

    private static void ProjectPathsConfigureCheckedExport(DesignerRepositoryContext repository)
    {
        string application = Path.Combine(repository.RepositoryRoot, "build", "designer-app-path-tests",
            Guid.NewGuid().ToString("N"));
        string project = Path.Combine(application, "Tasks", "UI");
        DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = project, ProjectId = "litheview.project.paths-ui-test", DisplayName = "Paths UI Test",
            SchemaReference = "litheview://schema/project-manifest/v1", Surface = PathTestSurface(repository),
            Catalog = repository.Catalog, IncludeTutorialStarter = true
        }, repository.Profiles);
        Assert(created.IsValid && created.Workspace is not null,
            string.Join("; ", created.Diagnostics.Select(item => item.Message)));
        DesignerWorkspace workspace = created.Workspace!;
        string settingsPath = Path.ChangeExtension(workspace.ManifestPath, ".paths.json");
        // Existing command-line tooling metadata survives editing the UI output paths.
        DesignerProjectPathService.Save(workspace.ManifestPath, new()
        {
            ApplicationRoot = "../..", LibraryRoot = repository.RepositoryRoot,
            ComponentDirectory = "Tasks/UI/generated", BuildDirectory = "host-build"
        });
        using (ProjectPathsDialog dialog = new(workspace.ManifestPath, repository.RepositoryRoot))
        {
            Assert(Descendants(dialog).OfType<CheckBox>()
                .Single(control => control.AccessibleName == "Store relative project paths").Checked,
                "Browsed paths should default to relocatable relative values.");
            FindPathTextBox(dialog, "Application root").Text = "../..";
            FindPathTextBox(dialog, "LitheView library").Text =
                Path.GetRelativePath(application, repository.RepositoryRoot).Replace('\\', '/');
            FindPathTextBox(dialog, "Generated UI component").Text = "Tasks/UI/generated";
            Assert(!Descendants(dialog).OfType<TextBox>().Any(control => control.AccessibleName == "Simulator build root"),
                "The application build directory is not configured by the designer.");
            Assert(dialog.TrySavePaths(), Descendants(dialog).OfType<Label>()
                .Single(control => control.AccessibleName == "Project paths validation").Text);
            byte[] saved = File.ReadAllBytes(settingsPath);
            FindPathTextBox(dialog, "Generated UI component").Text = "Tasks/UI/forms";
            Assert(!dialog.TrySavePaths() && saved.AsSpan().SequenceEqual(File.ReadAllBytes(settingsPath)),
                "Invalid protected-input overlap must not replace saved path settings.");
        }
        ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(workspace.ManifestPath, repository.RepositoryRoot);
        Assert(paths.Settings.ApplicationRoot == "../.." && paths.Settings.ComponentDirectory == "Tasks/UI/generated" &&
            paths.ApplicationRoot == application && paths.LibraryRoot == repository.RepositoryRoot,
            "Saved relative paths did not retain their declared bases.");
        Directory.CreateDirectory(paths.ComponentDirectory);
        string unrelated = Path.Combine(paths.ComponentDirectory, "application-owned.txt");
        File.WriteAllText(unrelated, "preserve me");
        using DesignerMainForm form = new(repository with
        {
            PreviewWorkerPath = Path.Combine(application, "absent-preview-worker.exe")
        }, startupProjectPath: null);
        form.LoadWorkspace(workspace);
        Dictionary<string, ToolStripMenuItem> commands = SimulationMenus(form);
        Assert(commands["Project Paths..."].Enabled, "Active projects must expose the paths dialog.");
        commands["Generate UI"].PerformClick();
        ListView output = Descendants(form).OfType<ListView>()
            .Single(control => control.AccessibleName == "Designer diagnostics and output");
        string report = Path.Combine(paths.ComponentDirectory, "component.json");
        Assert(File.Exists(report), "Custom component export failed: " +
            string.Join("; ", output.Items.Cast<ListViewItem>().Select(item => item.SubItems[2].Text)));
        Assert(File.ReadAllText(unrelated) == "preserve me", "Component export replaced an unrelated file.");
        Assert(!File.Exists(Path.Combine(paths.ComponentDirectory, "application.hpp")),
            "Component generation must not create an unused application hook.");
        Assert(!Directory.Exists(Path.Combine(paths.ComponentDirectory, ".vscode")),
            "Component generation must leave build and debug configuration to the application.");
        byte[] reportBefore = File.ReadAllBytes(report);
        int successCount = output.Items.Cast<ListViewItem>().Count(item => item.SubItems[1].Text == "LVUI4001");
        JsonObject settings = JsonNode.Parse(File.ReadAllBytes(settingsPath))!.AsObject();
        settings["componentDirectory"] = "Tasks/UI/forms";
        File.WriteAllText(settingsPath, settings.ToJsonString());
        using (ProjectPathsDialog repair = new(workspace.ManifestPath, repository.RepositoryRoot))
        {
            Assert(FindPathTextBox(repair, "Generated UI component").Text == "Tasks/UI/forms",
                "Well-formed invalid settings must remain editable in the dialog.");
            Assert(!string.IsNullOrEmpty(Descendants(repair).OfType<Label>()
                .Single(control => control.AccessibleName == "Project paths validation").Text),
                "Invalid settings need an actionable validation message.");
        }
        commands["Generate UI"].PerformClick();
        Assert(output.Items.Cast<ListViewItem>().Count(item => item.SubItems[1].Text == "LVUI4001") == successCount &&
            output.Items.Cast<ListViewItem>().Any(item => item.SubItems[1].Text == "LVUI4005"),
            "Invalid configured paths must fail in Output before export.");
        File.WriteAllText(settingsPath, "null");
        commands["Generate UI"].PerformClick();
        Assert(reportBefore.AsSpan().SequenceEqual(File.ReadAllBytes(report)),
            "Invalid or malformed settings must preserve the last successful export.");
    }

    private static TextBox FindPathTextBox(Control parent, string name) => Descendants(parent).OfType<TextBox>()
        .Single(control => control.AccessibleName == name);

    private static DisplaySurfaceLoadResult PathTestSurface(DesignerRepositoryContext repository) =>
        repository.Profiles.Surfaces.First(item => item.IsValid && item.Profile is
        {
            NativeDisplay.ColorModel: NativeColorModel.Rgb, PainterSurface.Format: SurfacePixelFormat.Rgb565,
            Shape: SurfaceShape.Rectangle, PixelSize.Width: >= 320, PixelSize.Height: >= 240
        });
}
