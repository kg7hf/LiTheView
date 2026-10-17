// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.App;

internal sealed partial class DesignerMainForm
{
    private ToolStripMenuItem _generateUiMenu = null!;
    private ToolStripMenuItem _projectPathsMenu = null!;
    private ToolStripButton _generateUiButton = null!;

    private void AddGenerationCommands(ToolStripMenuItem project)
    {
        project.DropDownItems.Add(new ToolStripSeparator());
        _projectPathsMenu = MenuItem("Project Paths...", Keys.None, EditProjectPaths);
        _generateUiMenu = MenuItem("Generate UI", Keys.None, GenerateUi);
        project.DropDownItems.AddRange([_projectPathsMenu, _generateUiMenu]);
    }

    private void UpdateGenerationCommandState()
    {
        _projectPathsMenu.Enabled = _workspace is not null;
        _generateUiMenu.Enabled = _workspace is not null;
        _generateUiButton.Enabled = _workspace is not null;
    }

    private void GenerateUi(object? sender, EventArgs eventArgs)
    {
        try
        {
            GenerateUiCore();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException)
        {
            AppendOutput("error", "LVUI4005", $"Cannot generate the UI component: {exception.Message}");
            SetStatus("UI generation failed. See Output for the required correction.");
        }
    }

    private void GenerateUiCore()
    {
        if (_workspace is null || !TrySaveProject())
        {
            return;
        }
        // Reopen the saved project so changed snapshots and resources are checked before export.
        DesignerWorkspaceLoadResult reopened = DesignerWorkspaceService.Open(
            _workspace.ManifestPath, _repository.Profiles, _repository.Catalog);
        AppendDiagnostics(reopened.Diagnostics);
        if (!reopened.IsValid || reopened.Workspace is null)
        {
            SetStatus("UI generation failed project validation. See Output.");
            return;
        }
        ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(
            reopened.Workspace.ManifestPath, _repository.RepositoryRoot);
        ComponentExportResult result = ComponentProjectExporter.Export(
            reopened.Workspace, paths.ComponentDirectory);
        AppendDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.OutputDirectory is null)
        {
            SetStatus("UI generation failed. See Output for the required correction.");
            return;
        }
        AppendOutput("info", "LVUI4001", $"Generated UI: {result.OutputDirectory}");
        AppendOutput("info", "LVUI4002", "Build and debug your application with its existing IDE/CMake configuration.");
        SetStatus($"UI component generated in {result.OutputDirectory}");
    }

    private void EditProjectPaths(object? sender, EventArgs eventArgs)
    {
        if (_workspace is null)
        {
            return;
        }
        try
        {
            using ProjectPathsDialog dialog = new(_workspace.ManifestPath, _repository.RepositoryRoot);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SetStatus("Project paths saved. The next generation uses the selected locations.");
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or
            UnauthorizedAccessException or ArgumentException)
        {
            AppendOutput("error", "LVUI4007", $"Cannot load project paths: {exception.Message}");
            SetStatus("Project paths could not be opened. See Output and repair the reported settings file.");
        }
    }

}
