// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.App;

internal sealed class ProjectPathsDialog : Form
{
    private readonly string _manifestPath;
    private readonly string _manifestDirectory;
    private readonly TextBox _applicationRoot = new();
    private readonly TextBox _libraryRoot = new();
    private readonly TextBox _componentDirectory = new();
    private readonly string _buildDirectory;
    private readonly CheckBox _relativePaths = new();
    private readonly TextBox _resolved = new();
    private readonly Label _validation = new();

    public ProjectPathsDialog(string manifestPath, string defaultLibraryRoot)
    {
        _manifestPath = Path.GetFullPath(manifestPath);
        _manifestDirectory = Path.GetDirectoryName(_manifestPath)!;
        DesignerProjectPaths settings = DesignerProjectPathService.ReadSettings(_manifestPath, defaultLibraryRoot);
        Text = "Project Paths";
        AccessibleName = "Project integration paths";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 590);
        MinimumSize = new Size(820, 540);
        Font = new Font("Segoe UI", 9F);
        ShowInTaskbar = false;
        BuildLayout();
        _applicationRoot.Text = settings.ApplicationRoot;
        _libraryRoot.Text = settings.LibraryRoot;
        _componentDirectory.Text = settings.ComponentDirectory;
        _buildDirectory = settings.BuildDirectory;
        foreach (TextBox input in new[] { _applicationRoot, _libraryRoot, _componentDirectory })
        {
            input.TextChanged += (_, _) => UpdateResolvedPaths();
        }
        UpdateResolvedPaths();
    }

    internal bool TrySavePaths()
    {
        try
        {
            _ = DesignerProjectPathService.Save(_manifestPath, ReadSettings());
            _validation.Text = string.Empty;
            return true;
        }
        catch (Exception exception) when (IsPathFailure(exception))
        {
            _validation.Text = exception.Message;
            return false;
        }
    }

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(900, 0), Padding = new Padding(0, 0, 0, 12),
            Text = "Application root is relative to the folder containing project.litheview.json. " +
                "The library and generated component locations are relative to Application root. " +
                "Absolute paths are also accepted. Your application owns its build configuration. " +
                "Cancel keeps the current settings; a newly created project keeps its defaults."
        }, 0, 0);
        TableLayoutPanel fields = new() { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, RowCount = 3 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddPath(fields, 0, "Application root", _applicationRoot, applicationBase: true);
        AddPath(fields, 1, "LitheView library", _libraryRoot, applicationBase: false);
        AddPath(fields, 2, "Generated UI component", _componentDirectory, applicationBase: false);
        root.Controls.Add(fields, 0, 1);
        _relativePaths.Text = "Store browsed paths relative to their base (recommended for relocation)";
        _relativePaths.AccessibleName = "Store relative project paths";
        _relativePaths.AutoSize = true;
        _relativePaths.Checked = true;
        root.Controls.Add(_relativePaths, 0, 2);
        _resolved.AccessibleName = "Resolved project locations";
        _resolved.Multiline = true;
        _resolved.ReadOnly = true;
        _resolved.WordWrap = false;
        _resolved.ScrollBars = ScrollBars.Both;
        _resolved.Dock = DockStyle.Fill;
        _resolved.Margin = new Padding(0, 12, 0, 8);
        root.Controls.Add(_resolved, 0, 3);
        _validation.AccessibleName = "Project paths validation";
        _validation.AutoSize = true;
        _validation.MaximumSize = new Size(900, 0);
        _validation.ForeColor = Color.Firebrick;
        root.Controls.Add(_validation, 0, 4);
        FlowLayoutPanel buttons = new()
        {
            AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft
        };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        Button save = new() { Text = "Save Paths", AutoSize = true, AccessibleName = "Save project paths" };
        save.Click += (_, _) =>
        {
            if (TrySavePaths())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        root.Controls.Add(buttons, 0, 5);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void AddPath(TableLayoutPanel fields, int row, string label, TextBox input, bool applicationBase)
    {
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.Controls.Add(new Label
        {
            Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 0, 8, 0)
        }, 0, row);
        input.AccessibleName = label;
        input.Dock = DockStyle.Top;
        input.Margin = new Padding(3, 3, 8, 8);
        fields.Controls.Add(input, 1, row);
        Button browse = new() { Text = "Browse...", AutoSize = true, AccessibleName = $"Browse {label}" };
        browse.Click += (_, _) => Browse(input, applicationBase);
        fields.Controls.Add(browse, 2, row);
    }

    private void Browse(TextBox input, bool applicationBase)
    {
        try
        {
            string baseDirectory = applicationBase ? _manifestDirectory : Path.GetFullPath(_applicationRoot.Text, _manifestDirectory);
            string selected = Path.GetFullPath(input.Text, baseDirectory);
            using FolderBrowserDialog browser = new()
            {
                Description = input.AccessibleName ?? "Choose a project directory",
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(selected) ? selected : baseDirectory
            };
            if (browser.ShowDialog(this) == DialogResult.OK)
            {
                input.Text = _relativePaths.Checked
                    ? Path.GetRelativePath(baseDirectory, browser.SelectedPath).Replace('\\', '/')
                    : browser.SelectedPath;
            }
        }
        catch (Exception exception) when (IsPathFailure(exception))
        {
            _validation.Text = exception.Message;
        }
    }

    private DesignerProjectPaths ReadSettings() => new()
    {
        SchemaVersion = 1,
        ApplicationRoot = _applicationRoot.Text.Trim(),
        LibraryRoot = _libraryRoot.Text.Trim(),
        ComponentDirectory = _componentDirectory.Text.Trim(),
        BuildDirectory = _buildDirectory.Trim()
    };

    private void UpdateResolvedPaths()
    {
        try
        {
            ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Resolve(_manifestPath, ReadSettings());
            _resolved.Text = $"Application: {paths.ApplicationRoot}{Environment.NewLine}" +
                $"Library: {paths.LibraryRoot}{Environment.NewLine}" +
                $"Component: {paths.ComponentDirectory}{Environment.NewLine}" +
                $"Settings: {paths.SettingsPath}";
            _validation.Text = string.Empty;
        }
        catch (Exception exception) when (IsPathFailure(exception))
        {
            _resolved.Text = string.Empty;
            _validation.Text = exception.Message;
        }
    }

    private static bool IsPathFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException;
}
