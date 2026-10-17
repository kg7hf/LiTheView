// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.App;

internal sealed class NewProjectWizard : Form
{
    private readonly DesignerRepositoryContext _repository;
    private readonly TabControl _pages = new();
    private readonly ListBox _surfaceList = new();
    private readonly ListView _surfaceDetails = new();
    private readonly TextBox _name = new();
    private readonly TextBox _id = new();
    private readonly TextBox _parentDirectory = new();
    private readonly CheckBox _useExistingDirectory = new();
    private readonly Label _directoryHint = new();
    private Label _directoryLabel = null!;
    private readonly ComboBox _logicalInput = new();
    private readonly CheckBox _tutorialStarter = new();
    private readonly Label _simulationHint = new();
    private readonly Label _validation = new();
    private readonly Button _back = new();
    private readonly Button _next = new();
    private readonly Button _create = new();

    public NewProjectWizard(DesignerRepositoryContext repository)
    {
        _repository = repository;
        Text = "New LitheView Project";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 610);
        Size = new Size(920, 680);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AccessibleName = "New LitheView project wizard";

        BuildLayout();
        PopulateSurfaces();
        UpdateButtons();
    }

    public DesignerWorkspace? CreatedWorkspace { get; private set; }

    public bool ConfigurePathsAfterCreation => _useExistingDirectory.Checked;

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _pages.Dock = DockStyle.Fill;
        _pages.Appearance = TabAppearance.FlatButtons;
        _pages.ItemSize = new Size(0, 1);
        _pages.SizeMode = TabSizeMode.Fixed;
        _pages.TabPages.Add(BuildSurfacePage());
        _pages.TabPages.Add(BuildIdentityPage());
        root.Controls.Add(_pages, 0, 0);

        _validation.AutoSize = true;
        _validation.ForeColor = Color.Firebrick;
        _validation.Padding = new Padding(4, 8, 4, 8);
        _validation.AccessibleName = "Wizard validation message";
        root.Controls.Add(_validation, 0, 1);

        FlowLayoutPanel buttons = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        _create.Text = "Create Project";
        _create.AutoSize = true;
        _create.Click += CreateProject;
        _next.Text = "Next >";
        _next.AutoSize = true;
        _next.Click += (_, _) => MoveToPage(1);
        _back.Text = "< Back";
        _back.AutoSize = true;
        _back.Click += (_, _) => MoveToPage(0);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_create);
        buttons.Controls.Add(_next);
        buttons.Controls.Add(_back);
        root.Controls.Add(buttons, 0, 2);
        CancelButton = cancel;
    }

    private TabPage BuildSurfacePage()
    {
        TabPage page = new() { Text = "Display surface" };
        TableLayoutPanel layout = PageLayout(
            "1. Choose the physical display surface",
            "Choose the display geometry, color format, and safe area. Logical input is selected " +
            "separately on the next page. A simulator does not require an MCU selection.");
        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 330,
            FixedPanel = FixedPanel.Panel1
        };
        _surfaceList.Dock = DockStyle.Fill;
        _surfaceList.DisplayMember = nameof(SurfaceChoice.DisplayName);
        _surfaceList.AccessibleName = "Checked display surfaces";
        _surfaceList.SelectedIndexChanged += (_, _) => SurfaceChanged();
        split.Panel1.Controls.Add(_surfaceList);

        _surfaceDetails.Dock = DockStyle.Fill;
        _surfaceDetails.View = View.Details;
        _surfaceDetails.FullRowSelect = true;
        _surfaceDetails.HeaderStyle = ColumnHeaderStyle.None;
        _surfaceDetails.Columns.Add("Field", 190);
        _surfaceDetails.Columns.Add("Value", 300);
        _surfaceDetails.AccessibleName = "Selected surface engineering details";
        split.Panel2.Controls.Add(_surfaceDetails);
        TableLayoutPanel selector = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        selector.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        selector.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        selector.Controls.Add(split, 0, 0);
        Button define = new()
        {
            Text = "Define a new display surface...",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        define.Click += DefineSurface;
        selector.Controls.Add(define, 0, 1);
        layout.Controls.Add(selector, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildIdentityPage()
    {
        TabPage page = new() { Text = "Project" };
        TableLayoutPanel layout = PageLayout(
            "2. Name the hardware-independent UI project",
            "Choose logical input and an optional working tutorial. Generate UI creates " +
            "the C++ component. Build and debug with your application IDE.");
        TableLayoutPanel fields = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 8,
            Padding = new Padding(0, 8, 0, 0)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddField(fields, 0, "Project name", _name);
        AddField(fields, 1, "Project ID", _id);
        _directoryLabel = AddField(fields, 2, "Parent folder", _parentDirectory);
        Button browse = new() { Text = "Browse...", AutoSize = true };
        browse.Click += BrowseParentDirectory;
        fields.Controls.Add(browse, 2, 2);
        _logicalInput.DropDownStyle = ComboBoxStyle.DropDownList;
        _logicalInput.AccessibleName = "Application logical input";
        _logicalInput.Items.AddRange(new InputChoice[]
        {
            new("Touch + Keys", "pointer-keys", Pointer: true, Keys: true),
            new("Touch only", "pointer", Pointer: true),
            new("Encoder + Select", "encoder-select", Encoder: true, Select: true),
            new("Output only", "output-only")
        });
        _logicalInput.DisplayMember = nameof(InputChoice.Name);
        _logicalInput.SelectedIndex = 0;
        _logicalInput.SelectedIndexChanged += (_, _) => UpdateTutorialAvailability();
        AddField(fields, 3, "Logical input", _logicalInput);
        _tutorialStarter.Text = "Start with the button and status tutorial";
        _tutorialStarter.AccessibleName = "Include button and status tutorial";
        _tutorialStarter.AutoSize = true;
        _tutorialStarter.Checked = true;
        fields.Controls.Add(_tutorialStarter, 1, 4);
        _simulationHint.AutoSize = true;
        _simulationHint.MaximumSize = new Size(580, 0);
        _simulationHint.ForeColor = SystemColors.GrayText;
        _simulationHint.AccessibleName = "Simulation project guidance";
        fields.Controls.Add(_simulationHint, 1, 5);
        _useExistingDirectory.Text = "Use an existing application folder";
        _useExistingDirectory.AccessibleName = "Use an existing application folder";
        _useExistingDirectory.AutoSize = true;
        _useExistingDirectory.CheckedChanged += (_, _) => UpdateDirectoryMode();
        fields.Controls.Add(_useExistingDirectory, 1, 6);
        _directoryHint.AutoSize = true;
        _directoryHint.MaximumSize = new Size(580, 0);
        _directoryHint.ForeColor = SystemColors.GrayText;
        _directoryHint.AccessibleName = "Project folder guidance";
        fields.Controls.Add(_directoryHint, 1, 7);
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(fields, 0, 2);
        page.Controls.Add(layout);

        _name.TextChanged += ProjectNameChanged;
        _id.TextChanged += (_, _) => UpdateButtons();
        _parentDirectory.TextChanged += (_, _) => UpdateButtons();
        _parentDirectory.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LitheView Projects");
        UpdateDirectoryMode();
        return page;
    }

    private static TableLayoutPanel PageLayout(string title, string explanation)
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(new Label
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 6)
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = explanation,
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(0, 0, 0, 10)
        }, 0, 1);
        return layout;
    }

    private static Label AddField(TableLayoutPanel layout, int row, string label, Control control)
    {
        Label caption = new()
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 8, 0)
        };
        layout.Controls.Add(caption, 0, row);
        control.AccessibleName ??= label;
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(3, 3, 8, 8);
        layout.Controls.Add(control, 1, row);
        return caption;
    }

    private void PopulateSurfaces()
    {
        foreach (DisplaySurfaceLoadResult surface in _repository.Profiles.Surfaces
                     .Where(item => item.IsValid && item.Profile is not null)
                     .OrderBy(item => item.Profile!.DisplayName, StringComparer.CurrentCulture))
        {
            _surfaceList.Items.Add(new SurfaceChoice(surface));
        }
        foreach (DisplayDefinitionLoadResult definition in NativeDisplayDefinitionFactory.LoadCatalog(
                     Path.Combine(_repository.RepositoryRoot, "profiles", "display-definitions")))
        {
            _surfaceList.Items.Add(new NativeChoice(definition));
        }
        if (_surfaceList.Items.Count > 0)
        {
            _surfaceList.SelectedIndex = 0;
        }
    }

    private void DefineSurface(object? sender, EventArgs eventArgs)
    {
        using CustomSurfaceDialog dialog = new(_repository);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            DisplayChoice? choice = dialog.Definition is { } definition ? new NativeChoice(definition)
                : dialog.Surface is { } surface ? new SurfaceChoice(surface) : null;
            if (choice is null)
            {
                return;
            }
            int index = _surfaceList.Items.Add(choice);
            _surfaceList.SelectedIndex = index;
        }
    }

    private void SurfaceChanged()
    {
        _surfaceDetails.Items.Clear();
        if (_surfaceList.SelectedItem is NativeChoice native)
        {
            DisplayDefinitionProfile definition = native.Result.Profile!;
            DisplayDefinitionGeometry geometry = definition.Geometry;
            AddDetail("Shape", geometry.Shape.ToString());
            AddDetail("Logical extent", $"{geometry.Width} x {geometry.Height} {definition.AddressSpace}");
            AddDetail("Native storage", definition.Format.ToString());
            AddDetail("Storage block", $"{definition.StorageBlock.Width} x {definition.StorageBlock.Height}, {definition.StorageBlock.Bytes} byte");
            AddDetail("Safe area", $"{geometry.SafeArea.X}, {geometry.SafeArea.Y}, {geometry.SafeArea.Width}, {geometry.SafeArea.Height}");
            AddDetail("Source status", definition.Source.Status.ToString());
            AddDetail("Definition ID", definition.Id);
            _logicalInput.Enabled = true;
            UpdateTutorialAvailability();
            UpdateButtons();
            return;
        }
        if (_surfaceList.SelectedItem is not SurfaceChoice choice || choice.Result.Profile is null)
        {
            UpdateButtons();
            return;
        }
        DisplaySurfaceProfile surface = choice.Result.Profile;
        AddDetail("Shape", surface.Shape.ToString());
        AddDetail("Physical size", DescribePhysicalSize(surface));
        AddDetail("Pixels", $"{surface.PixelSize.Width} x {surface.PixelSize.Height}");
        AddDetail(
            "Painter surface",
            $"{surface.PainterSurface.Format}, {surface.PainterSurface.StorageBitsPerPixel} storage bpp");
        AddDetail(
            "Native display",
            $"{surface.NativeDisplay.ColorModel}, {surface.NativeDisplay.ColorDepthBits} color bits");
        AddDetail("Transfer", surface.NativeDisplay.TransferFormat.ToString());
        AddDetail(
            "Safe area",
            $"{surface.SafeArea.X}, {surface.SafeArea.Y}, {surface.SafeArea.Width}, {surface.SafeArea.Height}");
        AddDetail("Legacy input hint", string.Join(", ", surface.InputCapabilities));
        AddDetail("Profile ID", surface.Id);
        bool supportsSimulation = surface.NativeDisplay.ColorModel == NativeColorModel.Rgb;
        bool wasSupported = _logicalInput.Enabled;
        _logicalInput.Enabled = supportsSimulation;
        if (supportsSimulation && !wasSupported)
        {
            _tutorialStarter.Checked = true;
        }
        UpdateTutorialAvailability();
        UpdateButtons();
    }

    private void UpdateTutorialAvailability()
    {
        bool supportsInput = _logicalInput.Enabled && _logicalInput.SelectedItem is InputChoice
            { Pointer: true } or InputChoice { Keys: true } or InputChoice { Encoder: true, Select: true };
        bool native = _surfaceList.SelectedItem is NativeChoice;
        _tutorialStarter.Enabled = supportsInput && !native;
        if (!_tutorialStarter.Enabled)
        {
            _tutorialStarter.Checked = false;
        }
        _simulationHint.Text = native
            ? "Start with a blank form in native emitter or cell coordinates. Add controls, save, Generate UI, then build your application."
            : !_logicalInput.Enabled
            ? "This legacy display uses an RGB staging contract. It remains editable; simulation export requires a native display contract."
            : supportsInput
                ? "ACTIVATE and RESET update the status in the running C++ application. Edit simulation/application.hpp to change behavior."
                : "Output-only projects start with an empty form. Add controls after choosing suitable logical input.";
    }

    private void ProjectNameChanged(object? sender, EventArgs eventArgs)
    {
        string slug = string.Join(
            '-',
            _name.Text.ToLowerInvariant().Split(
                [' ', '_', '.', '/', '\\'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new string([.. part.Where(character => char.IsLetterOrDigit(character) || character == '-')])))
            .Trim('-');
        _id.Text = string.IsNullOrEmpty(slug) ? string.Empty : $"litheview.project.{slug}";
        UpdateButtons();
    }

    private void BrowseParentDirectory(object? sender, EventArgs eventArgs)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = _useExistingDirectory.Checked
                ? "Choose the existing folder that will receive project.litheview.json (for example Tasks/UI)"
                : "Choose the parent folder for the new LitheView project",
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(_parentDirectory.Text) ? _parentDirectory.Text : string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _parentDirectory.Text = dialog.SelectedPath;
        }
    }

    private void MoveToPage(int index)
    {
        _pages.SelectedIndex = index;
        UpdateButtons();
    }

    private void CreateProject(object? sender, EventArgs eventArgs)
    {
        if (TryCreateWorkspace())
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal bool TryCreateWorkspace()
    {
        if (_surfaceList.SelectedItem is not DisplayChoice choice)
        {
            return false;
        }
        string folderName = ProjectFolderName(_name.Text);
        string projectDirectory = _useExistingDirectory.Checked
            ? _parentDirectory.Text.Trim() : Path.Combine(_parentDirectory.Text, folderName);
        DesignerWorkspaceLoadResult result = choice is NativeChoice native
            ? DesignerWorkspaceService.CreateNativeDisplayProject(new()
            {
                ProjectDirectory = projectDirectory, AllowExistingDirectory = _useExistingDirectory.Checked,
                ProjectId = _id.Text.Trim(), DisplayName = _name.Text.Trim(), Definition = native.Result,
                Input = ((InputChoice)_logicalInput.SelectedItem!).ToProfile(), Catalog = _repository.Catalog
            }) : DesignerWorkspaceService.CreateNew(
            new NewDesignerProjectRequest
            {
                ProjectDirectory = projectDirectory,
                AllowExistingDirectory = _useExistingDirectory.Checked,
                ProjectId = _id.Text.Trim(),
                DisplayName = _name.Text.Trim(),
                SchemaReference = "litheview://schema/project-manifest/v1",
                Surface = ((SurfaceChoice)choice).Result,
                Input = _logicalInput.Enabled && _logicalInput.SelectedItem is InputChoice input
                    ? input.ToProfile() : null,
                IncludeTutorialStarter = _tutorialStarter.Enabled && _tutorialStarter.Checked,
                Catalog = _repository.Catalog
            },
            _repository.Profiles);
        if (!result.IsValid || result.Workspace is null)
        {
            _validation.Text = string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
            return false;
        }
        CreatedWorkspace = result.Workspace;
        return true;
    }

    private void UpdateDirectoryMode()
    {
        _directoryLabel.Text = _useExistingDirectory.Checked ? "Project folder" : "Parent folder";
        _parentDirectory.AccessibleName = _directoryLabel.Text;
        _directoryHint.Text = _useExistingDirectory.Checked
            ? "The selected folder receives project.litheview.json; unrelated files are preserved. " +
                "After creation, Project Paths selects the application, library, component, and build roots. " +
                "Cancel there keeps the created project and its default paths."
            : "A new project folder is created inside the parent folder. " +
                "Project > Project Paths can change its integration locations later.";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool surfaceSelected = _surfaceList.SelectedItem is DisplayChoice;
        bool identityComplete = !string.IsNullOrWhiteSpace(_name.Text) &&
            !string.IsNullOrWhiteSpace(_id.Text) &&
            !string.IsNullOrWhiteSpace(_parentDirectory.Text);
        _back.Enabled = _pages.SelectedIndex > 0;
        _next.Visible = _pages.SelectedIndex == 0;
        _next.Enabled = surfaceSelected;
        _create.Visible = _pages.SelectedIndex == 1;
        _create.Enabled = surfaceSelected && identityComplete;
        _validation.Text = string.Empty;
    }

    private void AddDetail(string field, string value)
    {
        _surfaceDetails.Items.Add(new ListViewItem([field, value]));
    }

    private static string DescribePhysicalSize(DisplaySurfaceProfile surface)
    {
        return surface.Shape == SurfaceShape.Circle
            ? $"{surface.PhysicalSize.DiameterMillimeters?.ToString(CultureInfo.CurrentCulture)} mm diameter"
            : $"{surface.PhysicalSize.WidthMillimeters?.ToString(CultureInfo.CurrentCulture)} x " +
                $"{surface.PhysicalSize.HeightMillimeters?.ToString(CultureInfo.CurrentCulture)} mm";
    }

    private static string ProjectFolderName(string name)
    {
        string folder = new([.. name.Where(character => !Path.GetInvalidFileNameChars().Contains(character))]);
        return string.IsNullOrWhiteSpace(folder) ? "LitheViewProject" : folder.Trim();
    }

    private abstract record DisplayChoice
    {
        public abstract string DisplayName { get; }
    }

    private sealed record SurfaceChoice(DisplaySurfaceLoadResult Result) : DisplayChoice
    {
        public override string DisplayName => Result.Profile!.DisplayName;
    }

    private sealed record NativeChoice(DisplayDefinitionLoadResult Result) : DisplayChoice
    {
        public override string DisplayName => Result.Profile!.DisplayName + " (native)";
    }

    private sealed record InputChoice(
        string Name, string IdSuffix, bool Pointer = false, bool Keys = false,
        bool Encoder = false, bool Select = false)
    {
        public LogicalInputConfigurationProfile ToProfile() => new()
        {
            Schema = "https://litheview.dev/schema/logical-input-configuration.schema.json",
            SchemaVersion = 1,
            Id = $"litheview.input.{IdSuffix}",
            Pointer = Pointer,
            Keys = Keys,
            Text = false,
            Encoder = Encoder,
            Select = Select
        };
    }
}
