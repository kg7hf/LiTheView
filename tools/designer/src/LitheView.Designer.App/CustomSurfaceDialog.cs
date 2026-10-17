// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.App;

internal sealed class CustomSurfaceDialog : Form
{
    private readonly DesignerRepositoryContext _repository;
    private readonly TextBox _name = new();
    private readonly TextBox _id = new();
    private readonly ComboBox _family = new();
    private readonly ComboBox _shape = new();
    private readonly NumericUpDown _physicalWidth = PhysicalDimension();
    private readonly NumericUpDown _physicalHeight = PhysicalDimension();
    private readonly NumericUpDown _diameter = PhysicalDimension();
    private readonly NumericUpDown _pixelWidth = PixelDimension();
    private readonly NumericUpDown _pixelHeight = PixelDimension();
    private readonly ComboBox _format = new();
    private readonly CheckedListBox _inputs = new();
    private readonly TextBox _manufacturer = new();
    private readonly TextBox _model = new();
    private readonly TextBox _sourceUrl = new();
    private readonly TextBox _sourceDocument = new();
    private readonly Label _validation = new();
    private readonly Label _formatHint = new();

    public CustomSurfaceDialog(DesignerRepositoryContext repository)
    {
        _repository = repository;
        Text = "Define Display Surface";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(700, 650);
        Size = new Size(780, 720);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AccessibleName = "Define a custom physical display surface";

        BuildLayout();
        InitializeChoices();
    }

    public DisplaySurfaceLoadResult? Surface { get; private set; }
    public DisplayDefinitionLoadResult? Definition { get; private set; }

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            Text = "Define the engineering display contract",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        }, 0, 0);

        TabControl tabs = new() { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildIdentityPage());
        tabs.TabPages.Add(BuildGeometryPage());
        tabs.TabPages.Add(BuildRenderingPage());
        tabs.TabPages.Add(BuildProvenancePage());
        root.Controls.Add(tabs, 0, 1);

        _validation.AutoSize = true;
        _validation.ForeColor = Color.Firebrick;
        _validation.MaximumSize = new Size(700, 0);
        _validation.Padding = new Padding(4, 8, 4, 8);
        root.Controls.Add(_validation, 0, 2);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        Button create = new() { Text = "Use This Surface", AutoSize = true };
        create.Click += CreateSurface;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(create);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        AcceptButton = create;
        CancelButton = cancel;
    }

    private TabPage BuildIdentityPage()
    {
        TabPage page = new("Identity");
        TableLayoutPanel fields = FieldTable(3);
        AddField(fields, 0, "Display name", _name);
        AddField(fields, 1, "Profile ID", _id);
        AddField(fields, 2, "Surface compatibility family", _family);
        page.Controls.Add(fields);
        _name.TextChanged += (_, _) => _id.Text = ProfileId(_name.Text);
        return page;
    }

    private TabPage BuildGeometryPage()
    {
        TabPage page = new("Geometry");
        TableLayoutPanel fields = FieldTable(7);
        AddField(fields, 0, "Shape", _shape);
        AddField(fields, 1, "Physical width (mm)", _physicalWidth);
        AddField(fields, 2, "Physical height (mm)", _physicalHeight);
        AddField(fields, 3, "Diameter (mm)", _diameter);
        AddField(fields, 4, "Logical width", _pixelWidth);
        AddField(fields, 5, "Logical height", _pixelHeight);
        fields.Controls.Add(new Label
        {
            Text = "Dimensions use pixels for RGB, emitters for R2, and cells for RRRR/RYRY. The initial safe area fits inside the display shape.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(520, 0),
            Padding = new Padding(0, 12, 0, 0)
        }, 1, 6);
        page.Controls.Add(fields);
        _shape.SelectedIndexChanged += (_, _) => ShapeChanged();
        _pixelWidth.ValueChanged += (_, _) => SynchronizeSquarePixels();
        _physicalWidth.ValueChanged += (_, _) => SynchronizeSquarePhysicalSize();
        return page;
    }

    private TabPage BuildRenderingPage()
    {
        TabPage page = new("Rendering and input");
        TableLayoutPanel fields = FieldTable(3);
        AddField(fields, 0, "Painter color format", _format);
        Label inputLabel = new()
        {
            Text = "Input capabilities",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Padding = new Padding(0, 6, 12, 0)
        };
        fields.Controls.Add(inputLabel, 0, 1);
        _inputs.Dock = DockStyle.Fill;
        _inputs.CheckOnClick = true;
        _inputs.AccessibleName = "Display input capabilities";
        fields.Controls.Add(_inputs, 1, 1);
        _formatHint.AutoSize = true;
        _formatHint.ForeColor = SystemColors.GrayText;
        _formatHint.MaximumSize = new Size(520, 0);
        _formatHint.Padding = new Padding(0, 12, 0, 0);
        fields.Controls.Add(_formatHint, 1, 2);
        fields.RowStyles[1] = new RowStyle(SizeType.Percent, 100F);
        page.Controls.Add(fields);
        return page;
    }

    private TabPage BuildProvenancePage()
    {
        TabPage page = new("Source and provenance");
        TableLayoutPanel fields = FieldTable(5);
        AddField(fields, 0, "Manufacturer", _manufacturer);
        AddField(fields, 1, "Model", _model);
        AddField(fields, 2, "Source HTTPS URL", _sourceUrl);
        AddField(fields, 3, "Source document", _sourceDocument);
        fields.Controls.Add(new Label
        {
            Text = "Use the manufacturer page or datasheet URL and the exact source-document identity. " +
                "LitheView does not invent provenance for a custom surface.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(520, 0),
            Padding = new Padding(0, 12, 0, 0)
        }, 1, 4);
        page.Controls.Add(fields);
        return page;
    }

    private void InitializeChoices()
    {
        _family.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string family in _repository.Profiles.Surfaces
                     .Where(item => item.Profile is not null)
                     .Select(item => item.Profile!.CompatibilityFamily)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            _family.Items.Add(family);
        }
        if (_family.Items.Count > 0)
        {
            _family.SelectedIndex = 0;
        }

        _shape.DropDownStyle = ComboBoxStyle.DropDownList;
        _shape.DataSource = Enum.GetValues<SurfaceShape>();
        _format.DropDownStyle = ComboBoxStyle.DropDownList;
        _format.DataSource = Enum.GetValues<SurfacePixelFormat>();
        _format.SelectedIndexChanged += (_, _) => ShapeChanged();
        foreach (InputCapability input in Enum.GetValues<InputCapability>())
        {
            _inputs.Items.Add(input, input is InputCapability.Keyboard or InputCapability.Encoder);
        }
        _physicalWidth.Value = 100M;
        _physicalHeight.Value = 60M;
        _diameter.Value = 40M;
        _pixelWidth.Value = 480;
        _pixelHeight.Value = 272;
        ShapeChanged();
    }

    private void ShapeChanged()
    {
        SurfaceShape shape = (SurfaceShape)_shape.SelectedItem!;
        bool circle = shape == SurfaceShape.Circle;
        _diameter.Enabled = circle;
        _physicalWidth.Enabled = !circle;
        _physicalHeight.Enabled = !circle;
        if (shape is SurfaceShape.Circle or SurfaceShape.Square)
        {
            _pixelHeight.Value = _pixelWidth.Value;
        }
        if (shape == SurfaceShape.Square)
        {
            _physicalHeight.Value = _physicalWidth.Value;
        }
        bool native = _format.SelectedItem is SurfacePixelFormat format && DisplayDefinitionValidator.IsNativePacked(format);
        foreach (Control control in new Control[] { _physicalWidth, _physicalHeight, _diameter })
        {
            if (native) control.Enabled = false;
        }
        _family.Enabled = !native;
        _inputs.Enabled = !native;
        _manufacturer.Enabled = !native;
        _model.Enabled = !native;
        _sourceUrl.Enabled = !native;
        _formatHint.Text = native
            ? "R2 uses individual red emitters with even dimensions. RRRR/RYRY use 2 x 2 emitter cells: 256 x 192 cells present 512 x 384 emitters. Input is selected separately in the project wizard. Enter a specification reference on the Source page; physical verification remains separate."
            : "The native RGB transfer contract is derived from the selected storage format. Input is selected separately in the project wizard.";
    }

    private void SynchronizeSquarePixels()
    {
        if (_shape.SelectedItem is SurfaceShape.Circle or SurfaceShape.Square)
        {
            _pixelHeight.Value = _pixelWidth.Value;
        }
    }

    private void SynchronizeSquarePhysicalSize()
    {
        if (_shape.SelectedItem is SurfaceShape.Square)
        {
            _physicalHeight.Value = _physicalWidth.Value;
        }
    }

    private void CreateSurface(object? sender, EventArgs eventArgs)
    {
        if (TryCreateSurface())
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal bool TryCreateSurface()
    {
        Surface = null;
        Definition = null;
        SurfacePixelFormat format = (SurfacePixelFormat)_format.SelectedItem!;
        if (DisplayDefinitionValidator.IsNativePacked(format))
        {
            DisplayDefinitionLoadResult native = NativeDisplayDefinitionFactory.Create(_id.Text.Trim(), _name.Text.Trim(), format,
                Decimal.ToInt32(_pixelWidth.Value), Decimal.ToInt32(_pixelHeight.Value), (SurfaceShape)_shape.SelectedItem!, null,
                _sourceDocument.Text.Trim());
            if (!native.IsValid)
            {
                _validation.Text = string.Join(Environment.NewLine, native.Diagnostics.Select(item => item.Message));
                return false;
            }
            Definition = native;
            return true;
        }
        (int painterColorBits, int storageBits, int nativeColorBits, int transferBits,
            NativeTransferFormat transfer) = format switch
            {
                SurfacePixelFormat.Rgb565 => (16, 16, 16, 16, NativeTransferFormat.Rgb565),
                SurfacePixelFormat.Rgb888 => (24, 24, 24, 24, NativeTransferFormat.Rgb888Parallel),
                SurfacePixelFormat.Xrgb8888 => (24, 32, 32, 32, NativeTransferFormat.Argb8888),
                SurfacePixelFormat.Argb8888 => (32, 32, 32, 32, NativeTransferFormat.Argb8888),
                _ => throw new InvalidOperationException("The selected painter format is not supported.")
            };
        SurfaceShape shape = (SurfaceShape)_shape.SelectedItem!;
        int pixelWidth = Decimal.ToInt32(_pixelWidth.Value);
        int pixelHeight = Decimal.ToInt32(_pixelHeight.Value);
        DisplaySurfaceProfile profile = new()
        {
            Schema = "litheview://schema/display-surface/v1",
            SchemaVersion = 1,
            Id = _id.Text.Trim(),
            DisplayName = _name.Text.Trim(),
            CompatibilityFamily = _family.SelectedItem?.ToString() ?? string.Empty,
            Shape = shape,
            PhysicalSize = shape == SurfaceShape.Circle
                ? new PhysicalSize { DiameterMillimeters = _diameter.Value }
                : new PhysicalSize
                {
                    WidthMillimeters = _physicalWidth.Value,
                    HeightMillimeters = _physicalHeight.Value
                },
            PixelSize = new PixelSize { Width = pixelWidth, Height = pixelHeight },
            CoordinateSystem = new CoordinateSystemProfile
            {
                Origin = CoordinateOrigin.TopLeft,
                RotationDegrees = 0
            },
            PainterSurface = new PainterSurfaceProfile
            {
                Format = format,
                ColorDepthBits = painterColorBits,
                StorageBitsPerPixel = storageBits
            },
            NativeDisplay = new NativeDisplayProfile
            {
                ColorModel = NativeColorModel.Rgb,
                ColorDepthBits = nativeColorBits,
                TransferUnitBits = transferBits,
                TransferFormat = transfer
            },
            SafeArea = shape == SurfaceShape.Circle
                ? CircleSafeArea(pixelWidth)
                : new SafeAreaProfile { X = 0, Y = 0, Width = pixelWidth, Height = pixelHeight },
            InputCapabilities = [.. _inputs.CheckedItems.Cast<InputCapability>()],
            Provenance = new ProfileProvenance
            {
                Manufacturer = _manufacturer.Text.Trim(),
                Model = _model.Text.Trim(),
                SourceUrl = _sourceUrl.Text.Trim(),
                SourceDocument = _sourceDocument.Text.Trim(),
                SourceSha256 = null,
                Notes = "Defined interactively by the LitheView project owner."
            }
        };
        DisplaySurfaceLoadResult loaded = DisplaySurfaceProfileLoader.Load(
            DisplaySurfaceCanonicalizer.Canonicalize(profile));
        if (!loaded.IsValid)
        {
            _validation.Text = string.Join(
                Environment.NewLine,
                loaded.Diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));
            return false;
        }
        Surface = loaded;
        return true;
    }

    private static TableLayoutPanel FieldTable(int rows)
    {
        TableLayoutPanel fields = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows,
            Padding = new Padding(12)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int row = 0; row < rows; ++row)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        return fields;
    }

    private static SafeAreaProfile CircleSafeArea(int diameter)
    {
        int inset = (diameter - (int)Math.Floor(diameter / Math.Sqrt(2)) + 1) / 2;
        return new() { X = inset, Y = inset, Width = diameter - 2 * inset, Height = diameter - 2 * inset };
    }

    private static void AddField(TableLayoutPanel fields, int row, string label, Control control)
    {
        fields.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 12, 0)
        }, 0, row);
        control.Dock = DockStyle.Top;
        control.AccessibleName ??= label;
        control.Margin = new Padding(3, 3, 3, 8);
        fields.Controls.Add(control, 1, row);
    }

    private static NumericUpDown PhysicalDimension()
    {
        return new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.01M,
            Maximum = 2_000M,
            Increment = 0.1M
        };
    }

    private static NumericUpDown PixelDimension()
    {
        return new NumericUpDown { Minimum = 1, Maximum = 16_384, Increment = 1 };
    }

    private static string ProfileId(string name)
    {
        string slug = string.Join(
            '-',
            name.ToLowerInvariant().Split(
                [' ', '_', '.', '/', '\\'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new string([.. part.Where(character => char.IsLetterOrDigit(character) || character == '-')])))
            .Trim('-');
        return string.IsNullOrEmpty(slug) ? string.Empty : $"litheview.surface.custom.{slug}";
    }
}
