// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.App;

internal sealed partial class DesignerMainForm : Form
{
    private static readonly HashSet<string> InitialPaletteWidgets = new(StringComparer.Ordinal)
    {
        "lithe.widget.panel",
        "lithe.widget.label",
        "lithe.widget.button",
        "lithe.widget.image",
        "lithe.widget.line",
        "lithe.widget.progress"
    };
    private static readonly int[] GridSizes = [4, 8, 16];

    private readonly DesignerRepositoryContext _repository;
    private readonly string? _startupProjectPath;
    private readonly TreeView _projectExplorer = new();
    private readonly ListView _palette = new();
    private readonly TreeView _hierarchy = new();
    private readonly PropertyGrid _properties = new();
    private readonly ListView _actions = new();
    private readonly ListView _output = new();
    private readonly FlowLayoutPanel _componentTray = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ToolStripStatusLabel _surfaceStatus = new();
    private readonly PreviewCanvas _canvas;
    private readonly ToolStripComboBox _zoom = new();
    private readonly HashSet<string> _selectedComponentIds = new(StringComparer.Ordinal);
    private ToolStripMenuItem _undoMenu = null!;
    private ToolStripMenuItem _redoMenu = null!;
    private ToolStripMenuItem _deleteMenu = null!;
    private ToolStripMenuItem _duplicateMenu = null!;
    private ToolStripMenuItem _orderMenu = null!;
    private ToolStripMenuItem _importFontMenu = null!;
    private ToolStripMenuItem _importImageMenu = null!;
    private ToolStripMenuItem _importIconMenu = null!;
    private ToolStripMenuItem _importAnimationMenu = null!;
    private ToolStripMenuItem _editThemeMenu = null!;
    private ToolStripButton _undoButton = null!;
    private ToolStripButton _redoButton = null!;
    private DesignerWorkspace? _workspace;
    private DesignerDocumentSession? _session;
    private string? _selectedComponentId;
    private bool _startupHandled;
    private bool _synchronizingHierarchy;

    public DesignerMainForm(DesignerRepositoryContext repository, string? startupProjectPath)
    {
        _repository = repository;
        _startupProjectPath = startupProjectPath;
        _canvas = new PreviewCanvas(repository.PreviewWorkerPath);
        Text = "LitheView Designer";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Size = new Size(1440, 900);
        MinimumSize = new Size(1120, 720);
        Font = new Font("Segoe UI", 9F);
        AccessibleName = "LitheView classic RAD designer";

        BuildShell();
        UpdateGenerationCommandState();
        Shown += HandleFirstShown;
        FormClosing += ConfirmClose;
        _canvas.RenderStatusChanged += (_, message) => SetStatus(message);
        _canvas.ComponentSelectionRequested += (_, selection) =>
        {
            if (selection.ComponentId is null)
            {
                ClearSelection();
            }
            else
            {
                SelectComponent(selection.ComponentId, selection.Toggle);
            }
        };
        _canvas.ComponentFramesEditRequested += (_, edit) =>
        {
            if (_session is not null)
            {
                ReportEdit(_session.SetFrames(edit.Frames));
            }
        };
        _canvas.ComponentTextEditRequested += (_, edit) =>
        {
            if (_session is not null)
            {
                ReportEdit(_session.SetText(edit.ComponentId, edit.Text));
            }
        };
    }

    private void BuildShell()
    {
        MainMenuStrip = BuildMenu();
        Controls.Add(MainMenuStrip);
        ToolStrip tools = BuildToolbar();
        Controls.Add(tools);
        StatusStrip status = BuildStatus();
        Controls.Add(status);

        SplitContainer vertical = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Size = new Size(1440, 820),
            SplitterDistance = 610,
            Panel2MinSize = 100
        };
        vertical.Panel1.Controls.Add(BuildWorkspace());
        vertical.Panel2.Controls.Add(BuildOutputPanel());
        Controls.Add(vertical);
        vertical.BringToFront();
        tools.BringToFront();
        MainMenuStrip.BringToFront();
        status.BringToFront();
    }

    private MenuStrip BuildMenu()
    {
        MenuStrip menu = new() { Dock = DockStyle.Top };
        ToolStripMenuItem file = new("&File");
        file.DropDownItems.Add(MenuItem("&New Project...", Keys.Control | Keys.N, NewProject));
        file.DropDownItems.Add(MenuItem("&Open Project...", Keys.Control | Keys.O, OpenProject));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("&Save", Keys.Control | Keys.S, SaveProject));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("E&xit", Keys.Alt | Keys.F4, (_, _) => Close()));

        ToolStripMenuItem edit = new("&Edit");
        _undoMenu = MenuItem("&Undo", Keys.Control | Keys.Z, Undo);
        _redoMenu = MenuItem("&Redo", Keys.Control | Keys.Y, Redo);
        edit.DropDownItems.Add(_undoMenu);
        edit.DropDownItems.Add(_redoMenu);
        edit.DropDownItems.Add(new ToolStripSeparator());
        _deleteMenu = MenuItem("&Delete", Keys.Delete, DeleteSelected);
        edit.DropDownItems.Add(_deleteMenu);
        _duplicateMenu = MenuItem("D&uplicate", Keys.Control | Keys.D, DuplicateSelected);
        edit.DropDownItems.Add(_duplicateMenu);
        edit.DropDownItems.Add(MenuItem("Edit &Text", Keys.F2, EditSelectedText));
        _orderMenu = new ToolStripMenuItem("&Order");
        _orderMenu.DropDownItems.Add(MenuItem("Bring to &Front", Keys.Control | Keys.Shift | Keys.OemCloseBrackets,
            (_, _) => ChangeSelectedOrder(FormZOrderOperation.BringToFront)));
        _orderMenu.DropDownItems.Add(MenuItem("Bring &Forward", Keys.Control | Keys.OemCloseBrackets,
            (_, _) => ChangeSelectedOrder(FormZOrderOperation.BringForward)));
        _orderMenu.DropDownItems.Add(MenuItem("Send &Backward", Keys.Control | Keys.OemOpenBrackets,
            (_, _) => ChangeSelectedOrder(FormZOrderOperation.SendBackward)));
        _orderMenu.DropDownItems.Add(MenuItem("Send to &Back", Keys.Control | Keys.Shift | Keys.OemOpenBrackets,
            (_, _) => ChangeSelectedOrder(FormZOrderOperation.SendToBack)));
        edit.DropDownItems.Add(_orderMenu);

        ToolStripMenuItem view = new("&View");
        ToolStripMenuItem showGrid = new("Show &Grid")
        {
            Checked = true,
            CheckOnClick = true
        };
        showGrid.CheckedChanged += (_, _) =>
        {
            _canvas.ShowGrid = showGrid.Checked;
            _canvas.Invalidate();
        };
        ToolStripMenuItem snapToGrid = new("&Snap to Grid")
        {
            Checked = true,
            CheckOnClick = true
        };
        snapToGrid.CheckedChanged += (_, _) => _canvas.SnapToGrid = snapToGrid.Checked;
        ToolStripMenuItem gridSize = new("Grid &Size");
        foreach (int size in GridSizes)
        {
            ToolStripMenuItem sizeItem = new($"{size} pixels")
            {
                Checked = size == _canvas.GridSize,
                CheckOnClick = false
            };
            sizeItem.Click += (_, _) =>
            {
                _canvas.GridSize = size;
                foreach (ToolStripMenuItem sibling in gridSize.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    sibling.Checked = ReferenceEquals(sibling, sizeItem);
                }
                _canvas.Invalidate();
            };
            gridSize.DropDownItems.Add(sizeItem);
        }
        view.DropDownItems.Add(showGrid);
        view.DropDownItems.Add(snapToGrid);
        view.DropDownItems.Add(gridSize);
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownItems.Add(MenuItem("Refresh Native Preview", Keys.F5, RefreshPreview));
        ToolStripMenuItem resources = new("&Resources");
        _editThemeMenu = MenuItem(
            "Edit &Theme Color Tokens...",
            Keys.Control | Keys.Alt | Keys.T,
            EditThemeColors);
        _editThemeMenu.Enabled = false;
        resources.DropDownItems.Add(_editThemeMenu);
        resources.DropDownItems.Add(new ToolStripSeparator());
        _importFontMenu = MenuItem(
            "Import &Font...",
            Keys.Control | Keys.Alt | Keys.F,
            ImportFont);
        _importFontMenu.Enabled = false;
        resources.DropDownItems.Add(_importFontMenu);
        _importImageMenu = MenuItem(
            "Import &Image...",
            Keys.Control | Keys.Alt | Keys.I,
            (_, _) => ImportImage(ResourceKind.Image));
        _importImageMenu.Enabled = false;
        resources.DropDownItems.Add(_importImageMenu);
        _importIconMenu = MenuItem(
            "Import Ic&on...",
            Keys.Control | Keys.Alt | Keys.K,
            (_, _) => ImportImage(ResourceKind.Icon));
        _importIconMenu.Enabled = false;
        resources.DropDownItems.Add(_importIconMenu);
        _importAnimationMenu = MenuItem(
            "Import &Animation...",
            Keys.Control | Keys.Alt | Keys.A,
            ImportAnimation);
        _importAnimationMenu.Enabled = false;
        resources.DropDownItems.Add(_importAnimationMenu);
        ToolStripMenuItem project = new("&Project");
        project.DropDownItems.Add(MenuItem("Validate Project", Keys.Control | Keys.Shift | Keys.B, ValidateProject));
        project.DropDownItems.Add(MenuItem("Export C++...", Keys.None, ExportCpp));
        AddGenerationCommands(project);
        ToolStripMenuItem help = new("&Help");
        help.DropDownItems.Add(MenuItem("Designer Workflow", Keys.F1, ShowWorkflowHelp));

        menu.Items.AddRange([file, edit, view, resources, project, help]);
        return menu;
    }

    private ToolStrip BuildToolbar()
    {
        ToolStrip tools = new()
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            ImageScalingSize = new Size(20, 20)
        };
        tools.Items.Add(ToolButton("New", NewProject));
        tools.Items.Add(ToolButton("Open", OpenProject));
        tools.Items.Add(ToolButton("Save", SaveProject));
        tools.Items.Add(new ToolStripSeparator());
        _undoButton = ToolButton("Undo", Undo);
        _redoButton = ToolButton("Redo", Redo);
        tools.Items.Add(_undoButton);
        tools.Items.Add(_redoButton);
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add(ToolButton("Refresh Preview", RefreshPreview));
        _generateUiButton = ToolButton("Generate UI", GenerateUi);
        _generateUiButton.AccessibleName = "Generate the UI component";
        tools.Items.Add(_generateUiButton);
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add(new ToolStripLabel("Zoom"));
        _zoom.DropDownStyle = ComboBoxStyle.DropDownList;
        _zoom.Items.AddRange(["25%", "50%", "75%", "100%", "150%", "200%", "400%"]);
        _zoom.SelectedItem = "100%";
        _zoom.SelectedIndexChanged += (_, _) =>
        {
            if (_zoom.SelectedItem is string text && int.TryParse(text.TrimEnd('%'), out int value))
            {
                _canvas.ZoomPercent = value;
            }
        };
        tools.Items.Add(_zoom);
        return tools;
    }

    private StatusStrip BuildStatus()
    {
        StatusStrip strip = new() { Dock = DockStyle.Bottom };
        _status.Spring = true;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Text = "Ready";
        _surfaceStatus.BorderSides = ToolStripStatusLabelBorderSides.Left;
        strip.Items.Add(_status);
        strip.Items.Add(_surfaceStatus);
        return strip;
    }

    private SplitContainer BuildWorkspace()
    {
        SplitContainer outer = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(1400, 610),
            SplitterDistance = 250,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 190,
            Panel2MinSize = 700
        };
        outer.Panel1.Controls.Add(BuildLeftDock());

        SplitContainer centerRight = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(1140, 610),
            SplitterDistance = 760,
            Panel2MinSize = 260
        };
        centerRight.Panel1.Controls.Add(BuildCenterDock());
        centerRight.Panel2.Controls.Add(BuildRightDock());
        outer.Panel2.Controls.Add(centerRight);
        return outer;
    }

    private SplitContainer BuildLeftDock()
    {
        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Size = new Size(250, 610),
            SplitterDistance = 300
        };
        _projectExplorer.Dock = DockStyle.Fill;
        _projectExplorer.HideSelection = false;
        _projectExplorer.AccessibleName = "Project explorer";
        split.Panel1.Controls.Add(DockPanel("PROJECT", _projectExplorer));

        _palette.Dock = DockStyle.Fill;
        _palette.View = View.List;
        _palette.MultiSelect = false;
        _palette.HideSelection = false;
        _palette.AccessibleName = "Widget palette";
        _palette.DoubleClick += PaletteActivated;
        split.Panel2.Controls.Add(DockPanel("TOOLBOX", _palette));
        return split;
    }

    private TableLayoutPanel BuildCenterDock()
    {
        TableLayoutPanel center = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(46, 49, 55)
        };
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        center.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        center.Controls.Add(_canvas, 0, 0);

        _componentTray.Dock = DockStyle.Fill;
        _componentTray.AutoSize = true;
        _componentTray.MinimumSize = new Size(0, 44);
        _componentTray.Padding = new Padding(8, 6, 8, 6);
        _componentTray.BackColor = SystemColors.Control;
        _componentTray.AccessibleName = "Nonvisual component tray";
        _componentTray.Controls.Add(new Label
        {
            Text = "COMPONENT TRAY   No nonvisual components",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(4, 5, 4, 4)
        });
        center.Controls.Add(_componentTray, 0, 1);
        return center;
    }

    private SplitContainer BuildRightDock()
    {
        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Size = new Size(370, 610),
            SplitterDistance = 240
        };
        _hierarchy.Dock = DockStyle.Fill;
        _hierarchy.HideSelection = false;
        _hierarchy.AllowDrop = true;
        _hierarchy.AccessibleName = "Form component hierarchy";
        _hierarchy.AfterSelect += HierarchySelectionChanged;
        _hierarchy.ItemDrag += HierarchyItemDrag;
        _hierarchy.DragEnter += HierarchyDragEnter;
        _hierarchy.DragOver += HierarchyDragOver;
        _hierarchy.DragDrop += HierarchyDragDrop;
        split.Panel1.Controls.Add(DockPanel("HIERARCHY", _hierarchy));

        TabControl inspector = new() { Dock = DockStyle.Fill };
        TabPage propertyPage = new("Properties");
        _properties.Dock = DockStyle.Fill;
        _properties.HelpVisible = true;
        _properties.ToolbarVisible = false;
        _properties.AccessibleName = "Selected component properties";
        propertyPage.Controls.Add(_properties);
        TabPage actionsPage = new("Actions");
        _actions.Dock = DockStyle.Fill;
        _actions.View = View.Details;
        _actions.FullRowSelect = true;
        _actions.Columns.Add("Event", 115);
        _actions.Columns.Add("Handler", 150);
        _actions.AccessibleName = "Selected component actions";
        _actions.AccessibleDescription = "Double-click an event to assign or create a stable form action.";
        _actions.DoubleClick += ActionActivated;
        actionsPage.Controls.Add(_actions);
        inspector.TabPages.Add(propertyPage);
        inspector.TabPages.Add(actionsPage);
        split.Panel2.Controls.Add(inspector);
        return split;
    }

    private TabControl BuildOutputPanel()
    {
        TabControl tabs = new() { Dock = DockStyle.Fill };
        TabPage outputPage = new("Output");
        _output.Dock = DockStyle.Fill;
        _output.View = View.Details;
        _output.FullRowSelect = true;
        _output.Columns.Add("Level", 85);
        _output.Columns.Add("Code", 100);
        _output.Columns.Add("Message", 900);
        _output.AccessibleName = "Designer diagnostics and output";
        outputPage.Controls.Add(_output);
        tabs.TabPages.Add(outputPage);
        return tabs;
    }

    private void HandleFirstShown(object? sender, EventArgs eventArgs)
    {
        if (_startupHandled)
        {
            return;
        }
        _startupHandled = true;
        if (!string.IsNullOrWhiteSpace(_startupProjectPath))
        {
            OpenWorkspace(_startupProjectPath);
        }
        else
        {
            NewProject(this, EventArgs.Empty);
        }
    }

    private void NewProject(object? sender, EventArgs eventArgs)
    {
        if (!ConfirmProjectTransition())
        {
            return;
        }
        using NewProjectWizard wizard = new(_repository);
        if (wizard.ShowDialog(this) == DialogResult.OK && wizard.CreatedWorkspace is not null)
        {
            LoadWorkspace(wizard.CreatedWorkspace);
            AppendOutput("info", "LVDW0000", "Project created with canonical manifest and form XML.");
            if (wizard.ConfigurePathsAfterCreation)
            {
                EditProjectPaths(this, EventArgs.Empty);
            }
        }
    }

    private void OpenProject(object? sender, EventArgs eventArgs)
    {
        if (!ConfirmProjectTransition())
        {
            return;
        }
        using OpenFileDialog dialog = new()
        {
            Title = "Open LitheView Project",
            Filter = "LitheView projects (project.litheview.json)|project.litheview.json|JSON files (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenWorkspace(dialog.FileName);
        }
    }

    private void OpenWorkspace(string manifestPath)
    {
        DesignerWorkspaceLoadResult result = DesignerWorkspaceService.Open(
            manifestPath,
            _repository.Profiles,
            _repository.Catalog);
        AppendDiagnostics(result.Diagnostics);
        if (!result.IsValid || result.Workspace is null)
        {
            MessageBox.Show(
                this,
                "The project failed strict validation. See the Output panel for details.",
                "Project not opened",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }
        LoadWorkspace(result.Workspace);
    }

    internal void LoadWorkspace(DesignerWorkspace workspace)
    {
        _workspace = workspace;
        _session = new DesignerDocumentSession(workspace.Form, workspace.Catalog);
        _session.Changed += DocumentChanged;
        _selectedComponentId = workspace.Form.Components[0].ComponentId;
        _selectedComponentIds.Clear();
        _selectedComponentIds.Add(_selectedComponentId);
        UpdateTitle();
        PopulateProjectExplorer();
        PopulatePalette();
        PopulateHierarchy();
        DesignerDisplay display = workspace.ResolvedProject.Display;
        _surfaceStatus.Text =
            $"{display.Shape}  {display.Width} x {display.Height}  " +
            $"{display.Format}";
        UpdateCommandState();
        _ = RenderCurrentFormAsync();
    }

    private void PopulateProjectExplorer()
    {
        _projectExplorer.BeginUpdate();
        _projectExplorer.Nodes.Clear();
        TreeNode project = new(_workspace!.Manifest.DisplayName) { Tag = _workspace };
        TreeNode forms = new("Forms");
        forms.Nodes.Add(new TreeNode(_workspace.Form.DisplayName) { Tag = _workspace.Form });
        project.Nodes.Add(forms);
        project.Nodes.Add(new TreeNode($"Display: {_workspace.ResolvedProject.Display.DisplayName}"));
        TreeNode resources = new("Resources");
        foreach (LoadedProjectResource resource in _workspace.ResolvedProject.Resources)
        {
            resources.Nodes.Add(new TreeNode(resource.Manifest.DisplayName) { Tag = resource });
        }
        project.Nodes.Add(resources);
        TreeNode themeColors = new("Theme Color Tokens");
        foreach (FormThemeColor token in _session!.Document.ThemeColors.OrderBy(
                     item => item.Id,
                     StringComparer.Ordinal))
        {
            themeColors.Nodes.Add(new TreeNode($"{token.Id} = {token.EncodedColor:x8}") { Tag = token });
        }
        project.Nodes.Add(themeColors);
        project.Nodes.Add(new TreeNode("Extensions"));
        _projectExplorer.Nodes.Add(project);
        project.Expand();
        forms.Expand();
        resources.Expand();
        themeColors.Expand();
        _projectExplorer.EndUpdate();
    }

    private void PopulatePalette()
    {
        _palette.BeginUpdate();
        _palette.Items.Clear();
        foreach (WidgetDefinition widget in _repository.Catalog.Widgets
                     .Where(item => InitialPaletteWidgets.Contains(item.Id))
                     .OrderBy(item => item.DisplayName, StringComparer.CurrentCulture))
        {
            _palette.Items.Add(new ListViewItem(widget.DisplayName) { Tag = widget, ToolTipText = widget.Help });
        }
        _palette.EndUpdate();
    }

    private void PopulateHierarchy()
    {
        _hierarchy.BeginUpdate();
        _hierarchy.Nodes.Clear();
        Dictionary<string, TreeNode> nodes = new(StringComparer.Ordinal);
        foreach (FormComponent component in _workspace!.Form.Components)
        {
            nodes.Add(component.ComponentId, new TreeNode(component.Name) { Tag = component });
        }
        foreach (FormComponent component in _workspace.Form.Components.OrderBy(item => item.SiblingOrder))
        {
            TreeNode node = nodes[component.ComponentId];
            if (component.ParentComponentId is null)
            {
                _hierarchy.Nodes.Add(node);
            }
            else
            {
                nodes[component.ParentComponentId].Nodes.Add(node);
            }
        }
        _hierarchy.ExpandAll();
        _synchronizingHierarchy = true;
        try
        {
            if (_hierarchy.Nodes.Count > 0)
            {
                TreeNode? selected = FindNode(_hierarchy.Nodes, _selectedComponentId);
                _hierarchy.SelectedNode = selected ?? _hierarchy.Nodes[0];
            }
        }
        finally
        {
            _synchronizingHierarchy = false;
        }
        _hierarchy.EndUpdate();
        _canvas.SetSelection(_selectedComponentIds, _selectedComponentId);
        UpdateInspector();
    }

    private void HierarchySelectionChanged(object? sender, TreeViewEventArgs eventArgs)
    {
        if (_synchronizingHierarchy)
        {
            return;
        }
        if (eventArgs.Node?.Tag is FormComponent component)
        {
            SelectComponent(component.ComponentId, toggle: false, synchronizeHierarchy: false);
        }
        else
        {
            ClearSelection();
        }
    }

    private void HierarchyItemDrag(object? sender, ItemDragEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left &&
            eventArgs.Item is TreeNode node &&
            node.Tag is FormComponent component &&
            component.ParentComponentId is not null)
        {
            _ = _hierarchy.DoDragDrop(node, DragDropEffects.Move);
        }
    }

    private void HierarchyDragEnter(object? sender, DragEventArgs eventArgs)
    {
        eventArgs.Effect = eventArgs.Data?.GetDataPresent(typeof(TreeNode)) == true
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void HierarchyDragOver(object? sender, DragEventArgs eventArgs)
    {
        Point location = _hierarchy.PointToClient(new Point(eventArgs.X, eventArgs.Y));
        TreeNode? target = _hierarchy.GetNodeAt(location);
        eventArgs.Effect = eventArgs.Data?.GetData(typeof(TreeNode)) is TreeNode source &&
            source != target && source.Tag is FormComponent && target?.Tag is FormComponent
                ? DragDropEffects.Move
                : DragDropEffects.None;
        if (target is not null)
        {
            _synchronizingHierarchy = true;
            try
            {
                _hierarchy.SelectedNode = target;
            }
            finally
            {
                _synchronizingHierarchy = false;
            }
        }
    }

    private void HierarchyDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (_session is null || eventArgs.Data?.GetData(typeof(TreeNode)) is not TreeNode source ||
            source.Tag is not FormComponent component)
        {
            return;
        }
        Point location = _hierarchy.PointToClient(new Point(eventArgs.X, eventArgs.Y));
        if (_hierarchy.GetNodeAt(location)?.Tag is not FormComponent parent)
        {
            return;
        }
        FormEditResult result = _session.Reparent(component.ComponentId, parent.ComponentId);
        ReportEdit(result);
        SelectComponent(component.ComponentId);
        if (result.IsValid && result.Changed)
        {
            SetStatus($"Reparented {component.Name} under {parent.Name}.");
        }
    }

    private void UpdateInspector()
    {
        if (_selectedComponentId is null || _session is null)
        {
            _properties.SelectedObject = null;
            _actions.Items.Clear();
            return;
        }
        FormComponent component = _session.Document.Components.Single(
            item => item.ComponentId == _selectedComponentId);
        _properties.SelectedObject = _session is null
            ? null
            : new ComponentInspectorModel(
                _session,
                _repository.Catalog,
                component.ComponentId,
                ReportEdit);
        _actions.BeginUpdate();
        _actions.Items.Clear();
        WidgetDefinition? widget = _repository.Catalog.Widgets.SingleOrDefault(item => item.Id == component.WidgetId);
        if (widget is not null)
        {
            foreach (WidgetActionDefinition action in widget.Actions)
            {
                (string label, _) = ResolveAssignedAction(component, action);
                _actions.Items.Add(new ListViewItem([action.DisplayName, label]) { Tag = action });
            }
        }
        _actions.EndUpdate();
    }

    private void ActionActivated(object? sender, EventArgs eventArgs)
    {
        if (_session is null || _selectedComponentId is null ||
            _actions.SelectedItems.Count == 0 ||
            _actions.SelectedItems[0].Tag is not WidgetActionDefinition widgetAction)
        {
            return;
        }
        FormComponent component = _session.Document.Components.Single(
            item => item.ComponentId == _selectedComponentId);
        (_, string? currentActionId) = ResolveAssignedAction(component, widgetAction);
        using ActionAssignmentDialog dialog = new(
            widgetAction.DisplayName,
            _session.Document.Actions,
            currentActionId,
            _session.Document.Components
                .SelectMany(item => item.Properties.OfType<FormUnsignedIntegerValue>())
                .Where(item => item.PropertyId == "runtime-id" && item.Value <= ushort.MaxValue)
                .Select(item => checked((ushort)item.Value)));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        FormEditResult result = dialog.CreatedAction is null
            ? _session.AssignAction(
                component.ComponentId,
                widgetAction.Id,
                dialog.SelectedActionId)
            : _session.CreateAndAssignAction(
                component.ComponentId,
                widgetAction.Id,
                dialog.CreatedAction);
        ReportEdit(result);
        if (result.IsValid && result.Changed)
        {
            SetStatus(dialog.CreatedAction is null
                ? $"Updated {widgetAction.DisplayName} action assignment."
                : $"Created and assigned action {dialog.CreatedAction.Id}.");
        }
    }

    private (string Label, string? ActionId) ResolveAssignedAction(
        FormComponent component,
        WidgetActionDefinition widgetAction)
    {
        FormPropertyValue? source = component.Properties.SingleOrDefault(
            item => item.PropertyId == widgetAction.RuntimeActionSource);
        if (source is FormReferenceValue reference && reference.ReferenceKind == FormReferenceKind.Action)
        {
            FormAction? action = _session!.Document.Actions.SingleOrDefault(
                item => item.Id == reference.ReferenceId);
            return action is null
                ? ($"missing: {reference.ReferenceId}", null)
                : ($"{action.Symbol} [{action.RuntimeId}]", action.Id);
        }
        if (source is FormUnsignedIntegerValue runtime && runtime.Value != 0U)
        {
            FormAction? action = _session!.Document.Actions.SingleOrDefault(
                item => item.RuntimeId == runtime.Value);
            return action is null
                ? ($"runtime {runtime.Value} (not declared)", null)
                : ($"{action.Symbol} [{action.RuntimeId}]", action.Id);
        }
        return ("(unassigned)", null);
    }

    private void PaletteActivated(object? sender, EventArgs eventArgs)
    {
        if (_palette.SelectedItems.Count == 0 || _palette.SelectedItems[0].Tag is not WidgetDefinition widget)
        {
            return;
        }
        if (_session is null)
        {
            return;
        }
        string parentId = PlacementParentId();
        HashSet<string> before = [.. _session.Document.Components.Select(item => item.ComponentId)];
        FormEditResult result = _session.AddWidget(widget.Id, parentId);
        ReportEdit(result);
        if (result.IsValid && result.Changed)
        {
            string added = _session.Document.Components.Single(item => !before.Contains(item.ComponentId)).ComponentId;
            SelectComponent(added);
            SetStatus($"Placed {widget.DisplayName}. Double-click another toolbox item to continue.");
        }
    }

    private void SaveProject(object? sender, EventArgs eventArgs)
    {
        _ = TrySaveProject();
    }

    private bool TrySaveProject()
    {
        if (_workspace is null)
        {
            return true;
        }
        FormXmlWriteResult result = DesignerWorkspaceService.SaveForm(_workspace, _workspace.Form);
        AppendDiagnostics(result.Diagnostics);
        if (!result.IsValid)
        {
            SetStatus("Save failed strict validation.");
            return false;
        }
        _session?.MarkClean();
        SetStatus($"Saved {_workspace.FormPath}");
        AppendOutput("info", "LVDW0006", "Form saved atomically.");
        return true;
    }

    private void ValidateProject(object? sender, EventArgs eventArgs)
    {
        if (_workspace is null)
        {
            return;
        }
        DesignerWorkspaceLoadResult result = DesignerWorkspaceService.Open(
            _workspace.ManifestPath,
            _repository.Profiles,
            _repository.Catalog);
        AppendDiagnostics(result.Diagnostics);
        string message = result.IsValid ? "Project validation passed." : "Project validation failed.";
        AppendOutput(result.IsValid ? "info" : "error", "LVDW0007", message);
        SetStatus(message);
    }

    private void ExportCpp(object? sender, EventArgs eventArgs)
    {
        if (_workspace is null || !TrySaveProject())
        {
            return;
        }
        string outputDirectory = Path.Combine(_workspace.ProjectDirectory, "generated");
        DesignerCppExportResult result = DesignerWorkspaceService.ExportCpp(
            _workspace,
            outputDirectory);
        AppendDiagnostics(result.Diagnostics);
        if (!result.IsValid)
        {
            SetStatus(result.CandidatePath is null
                ? "C++ export failed. See Output."
                : $"C++ export requires user-region reconciliation: {result.CandidatePath}");
            return;
        }
        AppendOutput(
            "info",
            "LVDW0011",
            $"Generated {result.HeaderPath} and {result.IntegrationPath}; source {result.SourceXmlSha256}.");
        SetStatus($"Exported deterministic C++ to {outputDirectory}");
    }

    private void ImportFont(object? sender, EventArgs eventArgs)
    {
        _ = ImportFontAsync();
    }

    private async Task ImportFontAsync()
    {
        if (_workspace is null || _session is null)
        {
            return;
        }
        if (_session.IsDirty && !TrySaveProject())
        {
            return;
        }

        using FontImportDialog dialog = new(
            _workspace.ManifestPath,
            _workspace.ResolvedProject.Display.Format);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Request is null)
        {
            return;
        }
        FontResourceImportRequest request = dialog.Request;
        FormResource logicalResource = new()
        {
            Id = request.ResourceId,
            RuntimeName = request.EntryPoint,
            Symbol = request.GeneratedSymbol,
            Kind = FormResourceKind.Font
        };
        DesignerDocumentSession preflight = new(_session.Document, _repository.Catalog);
        FormEditResult preflightResult = preflight.AddResource(logicalResource);
        AppendDiagnostics(preflightResult.Diagnostics);
        if (!preflightResult.IsValid)
        {
            SetStatus("Font import conflicts with an existing logical form resource.");
            return;
        }

        _importFontMenu.Enabled = false;
        SetStatus("Rasterizing and locking font resource...");
        try
        {
            PowerShellFontConversionRunner runner = new(
                Path.Combine(_repository.RepositoryRoot, "tools", "convert-font.ps1"));
            FontResourceImportResult result = await FontResourceImporter.ImportAsync(
                request,
                runner);
            AppendDiagnostics(result.Diagnostics);
            if (!result.IsValid || result.Metrics is null)
            {
                SetStatus("Font import failed. See Output for exact diagnostics.");
                MessageBox.Show(
                    this,
                    "The font was not imported. See the Output panel for exact validation or converter diagnostics.",
                    "Font import failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            FormEditResult addResult = _session.AddResource(logicalResource);
            ReportEdit(addResult);
            if (!addResult.IsValid || !TrySaveProject())
            {
                SetStatus("The project resource was created, but the logical form reference did not save.");
                return;
            }

            AppendOutput(
                "info",
                "LVFI0001",
                $"Imported {request.DisplayName}: {result.Metrics.GlyphCount} glyphs, " +
                $"{result.Metrics.EstimatedRomBytes} measured ROM bytes, manifest " +
                $"{result.ResourceManifestSha256}.");
            string manifestPath = _workspace.ManifestPath;
            OpenWorkspace(manifestPath);
            SetStatus($"Imported deterministic font resource {request.ResourceId}.");
        }
        finally
        {
            _importFontMenu.Enabled = _workspace is not null;
        }
    }

    private void ImportImage(ResourceKind kind)
    {
        _ = ImportImageAsync(kind);
    }

    private void EditThemeColors(object? sender, EventArgs eventArgs)
    {
        if (_session is null)
        {
            return;
        }
        using ThemeColorEditorDialog dialog = new(_session.Document.ThemeColors);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ReportEdit(_session.SetThemeColors(dialog.ThemeColors));
        }
    }

    private async Task ImportImageAsync(ResourceKind kind)
    {
        if (_workspace is null || _session is null)
        {
            return;
        }
        if (_session.IsDirty && !TrySaveProject())
        {
            return;
        }

        using ImageImportDialog dialog = new(
            _workspace.ManifestPath,
            _workspace.ResolvedProject.Display.Format,
            kind);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Request is null)
        {
            return;
        }
        ImageResourceImportRequest request = dialog.Request;
        FormResource logicalResource = new()
        {
            Id = request.ResourceId,
            RuntimeName = request.EntryPoint,
            Symbol = request.GeneratedSymbol,
            Kind = FormResourceKind.Bitmap
        };
        DesignerDocumentSession preflight = new(_session.Document, _repository.Catalog);
        FormEditResult preflightResult = preflight.AddResource(logicalResource);
        AppendDiagnostics(preflightResult.Diagnostics);
        if (!preflightResult.IsValid)
        {
            SetStatus("Image import conflicts with an existing logical form resource.");
            return;
        }

        SetImageImportEnabled(enabled: false);
        SetStatus($"Converting and locking {kind.ToString().ToLowerInvariant()} resource...");
        try
        {
            PowerShellImageConversionRunner runner = new(
                Path.Combine(_repository.RepositoryRoot, "tools", "convert-image.ps1"));
            ImageResourceImportResult result = await ImageResourceImporter.ImportAsync(request, runner);
            AppendDiagnostics(result.Diagnostics);
            if (!result.IsValid || result.Metrics is null)
            {
                SetStatus("Image import failed. See Output for exact diagnostics.");
                MessageBox.Show(
                    this,
                    "The image was not imported. See the Output panel for exact validation or converter diagnostics.",
                    "Image import failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            FormEditResult addResult = _session.AddResource(logicalResource);
            ReportEdit(addResult);
            if (!addResult.IsValid || !TrySaveProject())
            {
                SetStatus("The project resource was created, but the logical form reference did not save.");
                return;
            }

            AppendOutput(
                "info",
                "LVII0001",
                $"Imported {request.DisplayName}: {result.Metrics.Format} " +
                    $"{result.Metrics.OutputWidth}x{result.Metrics.OutputHeight}, " +
                    $"{result.Metrics.EstimatedRomBytes} measured representation bytes, manifest " +
                    $"{result.ResourceManifestSha256}.");
            string manifestPath = _workspace.ManifestPath;
            OpenWorkspace(manifestPath);
            SetStatus($"Imported deterministic {kind.ToString().ToLowerInvariant()} resource {request.ResourceId}.");
        }
        finally
        {
            SetImageImportEnabled(_workspace is not null);
        }
    }

    private void SetImageImportEnabled(bool enabled)
    {
        _importImageMenu.Enabled = enabled;
        _importIconMenu.Enabled = enabled;
    }

    private void ImportAnimation(object? sender, EventArgs eventArgs)
    {
        _ = ImportAnimationAsync();
    }

    private async Task ImportAnimationAsync()
    {
        if (_workspace is null || _session is null)
        {
            return;
        }
        if (_session.IsDirty && !TrySaveProject())
        {
            return;
        }

        using AnimationImportDialog dialog = new(
            _workspace.ManifestPath,
            _workspace.ResolvedProject.Display.Format);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Request is null)
        {
            return;
        }
        AnimationResourceImportRequest request = dialog.Request;
        FormResource[] logicalFrames = CreateAnimationFrameResources(request);
        DesignerDocumentSession preflight = new(_session.Document, _repository.Catalog);
        FormEditResult preflightResult = preflight.AddResources(
            logicalFrames,
            $"Add {request.DisplayName} animation frames");
        AppendDiagnostics(preflightResult.Diagnostics);
        if (!preflightResult.IsValid)
        {
            SetStatus("Animation import conflicts with existing logical form resources.");
            return;
        }

        _importAnimationMenu.Enabled = false;
        SetStatus("Extracting, converting, and locking animation frames...");
        try
        {
            PowerShellAnimationConversionRunner runner = new(
                Path.Combine(_repository.RepositoryRoot, "tools", "convert-animation.ps1"));
            AnimationResourceImportResult result = await AnimationResourceImporter.ImportAsync(request, runner);
            AppendDiagnostics(result.Diagnostics);
            if (!result.IsValid || result.Metrics is null)
            {
                SetStatus("Animation import failed. See Output for exact diagnostics.");
                MessageBox.Show(
                    this,
                    "The animation was not imported. See the Output panel for exact validation or converter diagnostics.",
                    "Animation import failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            FormEditResult addResult = _session.AddResources(
                logicalFrames,
                $"Add {request.DisplayName} animation frames");
            ReportEdit(addResult);
            if (!addResult.IsValid || !TrySaveProject())
            {
                SetStatus("The project resource was created, but the logical frame references did not save.");
                return;
            }

            AppendOutput(
                "info",
                "LVAI0001",
                $"Imported {request.DisplayName}: {result.Metrics.FrameCount} " +
                    $"{result.Metrics.Format} frames at " +
                    $"{result.Metrics.OutputWidth}x{result.Metrics.OutputHeight}, " +
                    $"{result.Metrics.EstimatedRomBytes} measured representation bytes, manifest " +
                    $"{result.ResourceManifestSha256}.");
            string manifestPath = _workspace.ManifestPath;
            OpenWorkspace(manifestPath);
            SetStatus($"Imported deterministic animation resource {request.ResourceId}.");
        }
        finally
        {
            _importAnimationMenu.Enabled = _workspace is not null;
        }
    }

    private static FormResource[] CreateAnimationFrameResources(AnimationResourceImportRequest request)
    {
        return [.. Enumerable.Range(0, request.FrameCount).Select(index => new FormResource
        {
            Id = $"{request.ResourceId}.frame-{index}",
            RuntimeName = $"{request.EntryPoint}-f{index}",
            Symbol = $"{request.GeneratedSymbol}Frame{index}",
            Kind = FormResourceKind.Bitmap
        })];
    }

    private void RefreshPreview(object? sender, EventArgs eventArgs)
    {
        _ = RenderCurrentFormAsync();
    }

    private async Task RenderCurrentFormAsync()
    {
        if (_workspace is null)
        {
            return;
        }
        await _canvas.RenderAsync(_workspace);
    }

    private void DocumentChanged(object? sender, EventArgs eventArgs)
    {
        if (_workspace is null || _session is null)
        {
            return;
        }
        _workspace = _workspace with { Form = _session.Document };
        _selectedComponentIds.RemoveWhere(componentId =>
            _session.Document.Components.All(item => item.ComponentId != componentId));
        if (_selectedComponentId is not null && !_selectedComponentIds.Contains(_selectedComponentId))
        {
            _selectedComponentId = _selectedComponentIds.LastOrDefault();
        }
        if (_selectedComponentIds.Count == 0)
        {
            _selectedComponentId = _session.Document.Components[0].ComponentId;
            _selectedComponentIds.Add(_selectedComponentId);
        }
        UpdateTitle();
        PopulateProjectExplorer();
        PopulateHierarchy();
        UpdateCommandState();
        _ = RenderCurrentFormAsync();
    }

    private void Undo(object? sender, EventArgs eventArgs)
    {
        if (_session?.Undo() == true)
        {
            SetStatus("Undid one atomic designer transaction.");
        }
    }

    private void Redo(object? sender, EventArgs eventArgs)
    {
        if (_session?.Redo() == true)
        {
            SetStatus("Redid one atomic designer transaction.");
        }
    }

    private void DeleteSelected(object? sender, EventArgs eventArgs)
    {
        if (_session is null || _selectedComponentIds.Count == 0)
        {
            return;
        }
        ReportEdit(_session.Delete(_selectedComponentIds));
    }

    private void DuplicateSelected(object? sender, EventArgs eventArgs)
    {
        if (_session is null || _selectedComponentIds.Count == 0)
        {
            return;
        }
        HashSet<string> before = [.. _session.Document.Components.Select(item => item.ComponentId)];
        FormEditResult result = _session.Duplicate(_selectedComponentIds);
        ReportEdit(result);
        if (!result.IsValid || !result.Changed)
        {
            return;
        }
        HashSet<string> added = [.. _session.Document.Components
            .Where(item => !before.Contains(item.ComponentId))
            .Select(item => item.ComponentId)];
        string[] roots = [.. _session.Document.Components
            .Where(item => added.Contains(item.ComponentId) &&
                (item.ParentComponentId is null || !added.Contains(item.ParentComponentId)))
            .Select(item => item.ComponentId)];
        SetSelection(roots, roots.LastOrDefault());
        SetStatus(roots.Length == 1 ? "Duplicated one component subtree." : $"Duplicated {roots.Length} selections.");
    }

    private void ChangeSelectedOrder(FormZOrderOperation operation)
    {
        if (_session is null || _selectedComponentId is null)
        {
            return;
        }
        ReportEdit(_session.ChangeZOrder(_selectedComponentId, operation));
    }

    private void EditSelectedText(object? sender, EventArgs eventArgs)
    {
        if (!_canvas.BeginTextEditSelected())
        {
            SetStatus("The selected component has no direct text property.");
        }
    }

    private void ReportEdit(FormEditResult result)
    {
        AppendDiagnostics(result.Diagnostics);
        if (!result.IsValid)
        {
            string message = result.Diagnostics.Count > 0
                ? result.Diagnostics[0].Message
                : "The edit was rejected.";
            SetStatus(message);
        }
    }

    private string PlacementParentId()
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No form is open.");
        }
        FormComponent selected = _session.Document.Components.SingleOrDefault(
            item => item.ComponentId == _selectedComponentId) ?? _session.Document.Components[0];
        WidgetDefinition selectedWidget = _repository.Catalog.Widgets.Single(
            item => item.Id == selected.WidgetId);
        if (selectedWidget.ChildPolicy == WidgetChildPolicy.Many)
        {
            return selected.ComponentId;
        }
        return selected.ParentComponentId ?? _session.Document.Components[0].ComponentId;
    }

    private void SelectComponent(
        string componentId,
        bool toggle = false,
        bool synchronizeHierarchy = true)
    {
        if (_session is null ||
            _session.Document.Components.All(item => item.ComponentId != componentId))
        {
            return;
        }
        if (toggle)
        {
            if (!_selectedComponentIds.Add(componentId))
            {
                _selectedComponentIds.Remove(componentId);
            }
        }
        else
        {
            _selectedComponentIds.Clear();
            _selectedComponentIds.Add(componentId);
        }
        _selectedComponentId = _selectedComponentIds.Contains(componentId)
            ? componentId
            : _selectedComponentIds.LastOrDefault();
        _canvas.SetSelection(_selectedComponentIds, _selectedComponentId);
        if (synchronizeHierarchy)
        {
            _synchronizingHierarchy = true;
            try
            {
                TreeNode? node = FindNode(_hierarchy.Nodes, _selectedComponentId);
                _hierarchy.SelectedNode = node;
                node?.EnsureVisible();
            }
            finally
            {
                _synchronizingHierarchy = false;
            }
        }
        UpdateInspector();
        UpdateCommandState();
        if (_selectedComponentIds.Count > 1)
        {
            SetStatus($"{_selectedComponentIds.Count} components selected. Drag or use the arrow keys to move them together.");
        }
    }

    private void SetSelection(IEnumerable<string> componentIds, string? primaryComponentId)
    {
        _selectedComponentIds.Clear();
        _selectedComponentIds.UnionWith(componentIds);
        _selectedComponentId = primaryComponentId;
        _canvas.SetSelection(_selectedComponentIds, _selectedComponentId);
        PopulateHierarchy();
        UpdateCommandState();
    }

    private void ClearSelection()
    {
        _selectedComponentIds.Clear();
        _selectedComponentId = null;
        _canvas.SetSelection(_selectedComponentIds, primaryComponentId: null);
        _synchronizingHierarchy = true;
        try
        {
            _hierarchy.SelectedNode = null;
        }
        finally
        {
            _synchronizingHierarchy = false;
        }
        UpdateInspector();
        UpdateCommandState();
    }

    private void UpdateTitle()
    {
        if (_workspace is null)
        {
            Text = "LitheView Designer";
            return;
        }
        string dirty = _session?.IsDirty == true ? "*" : string.Empty;
        Text = $"{dirty}{_workspace.Manifest.DisplayName} - LitheView Designer";
    }

    private void UpdateCommandState()
    {
        bool canUndo = _session?.CanUndo == true;
        bool canRedo = _session?.CanRedo == true;
        _undoMenu.Enabled = canUndo;
        _redoMenu.Enabled = canRedo;
        _undoButton.Enabled = canUndo;
        _redoButton.Enabled = canRedo;
        _undoMenu.Text = canUndo ? $"&Undo {_session!.UndoDescription}" : "&Undo";
        _redoMenu.Text = canRedo ? $"&Redo {_session!.RedoDescription}" : "&Redo";
        bool hasSelection = _session is not null && _selectedComponentIds.Count > 0;
        bool rootOnly = _selectedComponentIds.Count == 1 &&
            _session?.Document.Components.Single(item => item.ComponentId == _selectedComponentId)
                .ParentComponentId is null;
        _deleteMenu.Enabled = hasSelection && !rootOnly;
        _duplicateMenu.Enabled = hasSelection && !rootOnly;
        _orderMenu.Enabled = hasSelection && !rootOnly && _selectedComponentIds.Count == 1;
        _importFontMenu.Enabled = _workspace is not null;
        SetImageImportEnabled(_workspace is not null);
        _importAnimationMenu.Enabled = _workspace is not null;
        _editThemeMenu.Enabled = _workspace is not null;
        UpdateGenerationCommandState();
    }

    private static TreeNode? FindNode(TreeNodeCollection nodes, string? componentId)
    {
        if (componentId is null)
        {
            return null;
        }
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is FormComponent component && component.ComponentId == componentId)
            {
                return node;
            }
            TreeNode? child = FindNode(node.Nodes, componentId);
            if (child is not null)
            {
                return child;
            }
        }
        return null;
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        Keys keyCode = keyData & Keys.KeyCode;
        if (keyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down &&
            _session is not null && _selectedComponentIds.Count > 0)
        {
            int step = keyData.HasFlag(Keys.Shift) ? 10 : 1;
            int deltaX = keyCode == Keys.Left ? -step : keyCode == Keys.Right ? step : 0;
            int deltaY = keyCode == Keys.Up ? -step : keyCode == Keys.Down ? step : 0;
            Dictionary<string, FormRectangle> frames = new(StringComparer.Ordinal);
            foreach (string componentId in SelectedTopLevelComponentIds())
            {
                FormComponent component = _session.Document.Components.Single(
                    item => item.ComponentId == componentId);
                FormRectangle frame = component.Properties
                    .OfType<FormRectangleValue>()
                    .Single(item => item.PropertyId == "frame").Value;
                frames.Add(
                    component.ComponentId,
                    frame with { X = frame.X + deltaX, Y = frame.Y + deltaY });
            }
            if (frames.Count > 0)
            {
                ReportEdit(_session.SetFrames(frames));
                return true;
            }
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    private IEnumerable<string> SelectedTopLevelComponentIds()
    {
        if (_session is null)
        {
            yield break;
        }
        Dictionary<string, FormComponent> components = _session.Document.Components.ToDictionary(
            item => item.ComponentId,
            StringComparer.Ordinal);
        foreach (string componentId in _selectedComponentIds)
        {
            FormComponent component = components[componentId];
            if (component.ParentComponentId is null)
            {
                continue;
            }
            string? parentId = component.ParentComponentId;
            bool selectedAncestor = false;
            while (parentId is not null)
            {
                if (_selectedComponentIds.Contains(parentId))
                {
                    selectedAncestor = true;
                    break;
                }
                parentId = components[parentId].ParentComponentId;
            }
            if (!selectedAncestor)
            {
                yield return componentId;
            }
        }
    }

    private bool ConfirmProjectTransition()
    {
        if (_session?.IsDirty != true)
        {
            return true;
        }
        DialogResult answer = MessageBox.Show(
            this,
            "Save changes to the current form before continuing?",
            "Unsaved LitheView form",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);
        return answer switch
        {
            DialogResult.Yes => TrySaveProject(),
            DialogResult.No => true,
            _ => false
        };
    }

    private void ConfirmClose(object? sender, FormClosingEventArgs eventArgs)
    {
        if (!ConfirmProjectTransition())
        {
            eventArgs.Cancel = true;
        }
    }

    private void ShowWorkflowHelp(object? sender, EventArgs eventArgs)
    {
        MessageBox.Show(
            this,
            "Start with File > New Project. Choose the real display surface first, inspect its engineering " +
            "properties, then design the UI independently of the eventual board, runtime, and storage " +
            "integration. Choose logical input, edit the form, then save and use Project > Generate UI. " +
            "The native canvas previews your layout. Build and debug the application with your IDE's " +
            "normal Windows or embedded configuration. Generation preserves application-owned code.",
            "LitheView Designer workflow",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void AppendDiagnostics(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        foreach (ProfileDiagnostic diagnostic in diagnostics)
        {
            AppendOutput(
                diagnostic.Severity.ToString().ToLowerInvariant(),
                diagnostic.Code,
                $"{diagnostic.Path}: {diagnostic.Message}");
        }
    }

    private void AppendOutput(string level, string code, string message)
    {
        _output.Items.Add(new ListViewItem([level, code, message]));
        if (_output.Items.Count > 0)
        {
            _output.EnsureVisible(_output.Items.Count - 1);
        }
    }

    private void SetStatus(string message)
    {
        _status.Text = message;
    }

    private static TableLayoutPanel DockPanel(string title, Control content)
    {
        TableLayoutPanel panel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(228, 231, 235),
            ForeColor = Color.FromArgb(45, 50, 57),
            Font = new Font("Segoe UI Semibold", 8F),
            Padding = new Padding(8, 5, 4, 5)
        }, 0, 0);
        panel.Controls.Add(content, 0, 1);
        return panel;
    }

    private static ToolStripMenuItem MenuItem(
        string text,
        Keys shortcut,
        EventHandler handler)
    {
        ToolStripMenuItem item = new(text) { ShortcutKeys = shortcut };
        item.Click += handler;
        return item;
    }

    private static ToolStripButton ToolButton(string text, EventHandler handler)
    {
        ToolStripButton button = new(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += handler;
        return button;
    }
}
