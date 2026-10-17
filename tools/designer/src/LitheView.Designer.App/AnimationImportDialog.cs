// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.App;

internal sealed class AnimationImportDialog : Form
{
    private const string DefaultPalette =
        "00000000,ff141519,ff2c2d35,ff543422,ff7d321f,ffa94a22,ffc96936,ffe6a548," +
        "fff5c96a,ff2bb7ad";

    private readonly string _projectManifestPath;
    private readonly SurfacePixelFormat _painterFormat;
    private readonly TextBox _sourceSheet = new();
    private readonly TextBox _displayName = new();
    private readonly TextBox _resourceId = new();
    private readonly TextBox _symbol = new();
    private readonly TextBox _entryPoint = new();
    private readonly NumericUpDown _sheetX = new();
    private readonly NumericUpDown _sheetY = new();
    private readonly NumericUpDown _sourceFrameWidth = new();
    private readonly NumericUpDown _sourceFrameHeight = new();
    private readonly NumericUpDown _columns = new();
    private readonly NumericUpDown _frameCount = new();
    private readonly NumericUpDown _outputWidth = new();
    private readonly NumericUpDown _outputHeight = new();
    private readonly ComboBox _format = new();
    private readonly ComboBox _scaleMode = new();
    private readonly ComboBox _alphaMode = new();
    private readonly NumericUpDown _alphaThreshold = new();
    private readonly ComboBox _dither = new();
    private readonly CheckBox _tintable = new();
    private readonly TextBox _tintArgb = new();
    private readonly TextBox _matteArgb = new();
    private readonly TextBox _paletteArgb = new();
    private readonly NumericUpDown _interval = new();
    private readonly NumericUpDown _repeatIndex = new();
    private readonly CheckBox _autoplay = new();
    private readonly TextBox _origin = new();
    private readonly TextBox _author = new();
    private readonly TextBox _sourceUri = new();
    private readonly TextBox _spdxId = new();
    private readonly TextBox _licenseFile = new();
    private readonly TextBox _attribution = new();
    private readonly TextBox _notes = new();
    private readonly CheckBox _redistributionApproved = new();
    private readonly Label _validation = new();
    private readonly Label _payload = new();
    private readonly AnimationConversionPreviewPanel _preview = new();
    private Size _sourcePixelSize;

    public AnimationImportDialog(string projectManifestPath, SurfacePixelFormat painterFormat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectManifestPath);
        _projectManifestPath = Path.GetFullPath(projectManifestPath);
        _painterFormat = painterFormat;
        Text = "Import deterministic animation resource";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 760);
        Size = new Size(1160, 900);
        ShowInTaskbar = false;
        InitializeDefaults();
        BuildUi();
    }

    public AnimationResourceImportRequest? Request { get; private set; }

    private void BuildUi()
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
            Text = "Extract, preview, time, and pack a row-major sprite sheet into an allocation-free animation.",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Padding = new Padding(4, 4, 4, 12)
        }, 0, 0);

        TabControl tabs = new() { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildIdentityPage());
        tabs.TabPages.Add(BuildFramesPage());
        tabs.TabPages.Add(BuildProvenancePage());
        root.Controls.Add(tabs, 0, 1);

        _validation.AutoSize = true;
        _validation.ForeColor = Color.Firebrick;
        _validation.MaximumSize = new Size(1040, 0);
        _validation.Padding = new Padding(4, 8, 4, 8);
        _validation.AccessibleName = "Animation import validation";
        root.Controls.Add(_validation, 0, 2);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        Button import = new() { Text = "Import Animation", AutoSize = true };
        import.Click += AcceptImport;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(import);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        AcceptButton = import;
        CancelButton = cancel;
    }

    private TabPage BuildIdentityPage()
    {
        TabPage page = new("Source and identity");
        TableLayoutPanel fields = FieldTable(8);
        _sourceSheet.AccessibleName = "Source animation sprite sheet";
        _resourceId.AccessibleName = "Animation resource ID";
        AddFileField(fields, 0, "Sprite sheet image", _sourceSheet, BrowseSpriteSheet);
        AddField(fields, 1, "Display name", _displayName);
        AddField(fields, 2, "Stable resource ID", _resourceId);
        AddField(fields, 3, "Generated C++ table symbol", _symbol);
        AddField(fields, 4, "Generated C++ entry point", _entryPoint);
        fields.Controls.Add(new Label
        {
            Text = $"The current preview painter is {_painterFormat}. The packed Bitmap sequence is a " +
                "logical visual resource usable from every supported painter surface; it is not tied to this display.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(780, 0),
            Padding = new Padding(0, 12, 0, 0)
        }, 1, 5);
        fields.Controls.Add(new Label
        {
            Text = "The source sheet, optional license, generated C++, and manifest are copied into a " +
                "contained project directory. Existing resources are never merged or overwritten.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(780, 0)
        }, 1, 6);
        fields.Controls.Add(new Label
        {
            Text = "The designer records visual representation and timing defaults only. The application " +
                "chooses the clock, ResourceProvider names, memory placement, and delivery path.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(780, 0)
        }, 1, 7);
        page.Controls.Add(fields);
        return page;
    }

    private TabPage BuildFramesPage()
    {
        TabPage page = new("Frames, timing, and preview");
        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(1080, 580),
            SplitterDistance = 470,
            Panel1MinSize = 420,
            Panel2MinSize = 420
        };
        TableLayoutPanel fields = FieldTable(22);
        _sheetX.AccessibleName = "Animation sheet X";
        _sourceFrameWidth.AccessibleName = "Animation source frame width";
        _columns.AccessibleName = "Animation grid columns";
        _frameCount.AccessibleName = "Animation frame count";
        _outputWidth.AccessibleName = "Animation output frame width";
        _format.AccessibleName = "Animation bitmap format";
        _scaleMode.AccessibleName = "Animation scale sampling";
        _alphaMode.AccessibleName = "Animation alpha handling";
        _dither.AccessibleName = "Animation dither mode";
        _tintArgb.AccessibleName = "Animation preview tint";
        _paletteArgb.AccessibleName = "Animation indexed4 palette";
        _interval.AccessibleName = "Animation frame interval";
        _repeatIndex.AccessibleName = "Animation repeat index";
        AddField(fields, 0, "Sheet X", _sheetX);
        AddField(fields, 1, "Sheet Y", _sheetY);
        AddField(fields, 2, "Source frame width", _sourceFrameWidth);
        AddField(fields, 3, "Source frame height", _sourceFrameHeight);
        AddField(fields, 4, "Columns", _columns);
        AddField(fields, 5, "Frame count", _frameCount);
        AddField(fields, 6, "Output frame width", _outputWidth);
        AddField(fields, 7, "Output frame height", _outputHeight);
        AddField(fields, 8, "Bitmap format", _format);
        AddField(fields, 9, "Scale sampling", _scaleMode);
        AddField(fields, 10, "Alpha handling", _alphaMode);
        AddField(fields, 11, "Alpha threshold", _alphaThreshold);
        AddField(fields, 12, "Dither", _dither);
        _tintable.Text = "Reusable tintable alpha frames";
        _tintable.AutoSize = true;
        _tintable.AccessibleName = "Tintable animation frames";
        fields.Controls.Add(_tintable, 1, 13);
        AddField(fields, 14, "Preview tint AARRGGBB", _tintArgb);
        AddField(fields, 15, "Opaque matte AARRGGBB", _matteArgb);
        _paletteArgb.Multiline = true;
        _paletteArgb.Height = 55;
        AddField(fields, 16, "Indexed4 palette", _paletteArgb);
        AddField(fields, 17, "Frame interval (ms)", _interval);
        AddField(fields, 18, "Repeat index", _repeatIndex);
        _autoplay.Text = "Autoplay when the form opens";
        _autoplay.AutoSize = true;
        _autoplay.AccessibleName = "Animation autoplay";
        fields.Controls.Add(_autoplay, 1, 19);
        _payload.AutoSize = true;
        _payload.ForeColor = SystemColors.GrayText;
        _payload.MaximumSize = new Size(340, 0);
        fields.Controls.Add(_payload, 1, 20);
        fields.Controls.Add(new Label
        {
            Text = "Preview playback uses a desktop timer only. Generated frames remain caller-clocked and " +
                "contain no thread, timer, allocation, or decoder.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(340, 0)
        }, 1, 21);
        split.Panel1.Controls.Add(fields);

        _preview.Dock = DockStyle.Fill;
        _preview.MinimumSize = new Size(420, 420);
        _preview.AccessibleName = "Animation frame-grid and playback preview";
        split.Panel2.Controls.Add(_preview);
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildProvenancePage()
    {
        TabPage page = new("Provenance and license");
        TableLayoutPanel fields = FieldTable(9);
        _origin.AccessibleName = "Animation origin";
        _spdxId.AccessibleName = "Animation SPDX license ID";
        _redistributionApproved.AccessibleName = "Animation redistribution approval";
        AddField(fields, 0, "Origin", _origin);
        AddField(fields, 1, "Author or studio", _author);
        AddField(fields, 2, "Source URI", _sourceUri);
        AddField(fields, 3, "SPDX ID", _spdxId);
        AddFileField(fields, 4, "License text file", _licenseFile, BrowseLicense);
        AddField(fields, 5, "Attribution", _attribution);
        _notes.Multiline = true;
        _notes.Height = 70;
        AddField(fields, 6, "Notes", _notes);
        _redistributionApproved.Text =
            "I verified this animation may be copied into and redistributed with this project";
        _redistributionApproved.AutoSize = true;
        fields.Controls.Add(_redistributionApproved, 1, 7);
        fields.Controls.Add(new Label
        {
            Text = "Origin and explicit redistribution approval are required. Provide either an SPDX ID " +
                "or a license text file; LitheView does not infer licensing from a file name.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(780, 0),
            Padding = new Padding(0, 10, 0, 0)
        }, 1, 8);
        page.Controls.Add(fields);
        return page;
    }

    private void InitializeDefaults()
    {
        _displayName.Text = "UI Animation";
        _resourceId.Text = "litheview.resource.ui-animation";
        _symbol.Text = "UiAnimation";
        _entryPoint.Text = "uiAnimation";
        ConfigureDimension(_sheetX, 0, 16383, 0);
        ConfigureDimension(_sheetY, 0, 16383, 0);
        ConfigureDimension(_sourceFrameWidth, 1, 16384, 96);
        ConfigureDimension(_sourceFrameHeight, 1, 16384, 72);
        ConfigureDimension(_columns, 1, 256, 4);
        ConfigureDimension(_frameCount, 2, 256, 8);
        ConfigureDimension(_outputWidth, 1, 4096, 96);
        ConfigureDimension(_outputHeight, 1, 4096, 72);
        ConfigureDimension(_alphaThreshold, 0, 255, 64);
        ConfigureDimension(_interval, 1, ushort.MaxValue, 100);
        ConfigureDimension(_repeatIndex, 0, 255, 0);
        _format.DropDownStyle = ComboBoxStyle.DropDownList;
        _format.DataSource = Enum.GetValues<ResourceFormat>()
            .Where(ImageConversionRequestValidator.IsBitmapFormat)
            .ToArray();
        _format.SelectedItem = ResourceFormat.Indexed4;
        _scaleMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _scaleMode.DataSource = Enum.GetValues<ImageScaleMode>();
        _scaleMode.SelectedItem = ImageScaleMode.Bilinear;
        _alphaMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _alphaMode.DataSource = Enum.GetValues<ImageAlphaMode>();
        _alphaMode.SelectedItem = ImageAlphaMode.Preserve;
        _dither.DropDownStyle = ComboBoxStyle.DropDownList;
        _dither.DataSource = Enum.GetValues<ImageDitherMode>();
        _dither.SelectedItem = ImageDitherMode.Ordered4x4;
        _tintArgb.Text = "ffffffff";
        _matteArgb.Text = "ff000000";
        _paletteArgb.Text = DefaultPalette;
        _autoplay.Checked = true;

        foreach (NumericUpDown control in new[]
                 {
                     _sheetX, _sheetY, _sourceFrameWidth, _sourceFrameHeight, _columns, _frameCount,
                     _outputWidth, _outputHeight, _alphaThreshold, _interval, _repeatIndex
                 })
        {
            control.ValueChanged += (_, _) => UpdatePreview();
        }
        _format.SelectedIndexChanged += FormatChanged;
        _scaleMode.SelectedIndexChanged += (_, _) => UpdatePreview();
        _alphaMode.SelectedIndexChanged += (_, _) => UpdatePreview();
        _dither.SelectedIndexChanged += (_, _) => UpdatePreview();
        _tintable.CheckedChanged += (_, _) => UpdatePreview();
        _tintArgb.TextChanged += (_, _) => UpdatePreview();
        _matteArgb.TextChanged += (_, _) => UpdatePreview();
        _paletteArgb.TextChanged += (_, _) => UpdatePreview();
        FormatChanged(this, EventArgs.Empty);
    }

    private void BrowseSpriteSheet(object? sender, EventArgs eventArgs)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Select source animation sprite sheet",
            Filter = "Raster images (*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif;*.tiff)|" +
                "*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif;*.tiff|PNG images (*.png)|*.png|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _preview.LoadImage(dialog.FileName);
            _sourcePixelSize = _preview.SourcePixelSize;
            _sourceSheet.Text = dialog.FileName;
            _sheetX.Value = 0;
            _sheetY.Value = 0;
            int desiredColumns = _sourcePixelSize.Width % 4 == 0 ? 4 : 1;
            int desiredRows = _sourcePixelSize.Height % 2 == 0 ? 2 : 1;
            int desiredFrames = Math.Max(2, desiredColumns * desiredRows);
            _columns.Value = Math.Min(256, desiredColumns);
            _frameCount.Value = Math.Min(256, desiredFrames);
            int frameWidth = Math.Max(1, _sourcePixelSize.Width / desiredColumns);
            int frameHeight = Math.Max(1, _sourcePixelSize.Height / desiredRows);
            _sourceFrameWidth.Value = Math.Min(_sourceFrameWidth.Maximum, frameWidth);
            _sourceFrameHeight.Value = Math.Min(_sourceFrameHeight.Maximum, frameHeight);
            _outputWidth.Value = Math.Min(_outputWidth.Maximum, frameWidth);
            _outputHeight.Value = Math.Min(_outputHeight.Maximum, frameHeight);
            string stem = Path.GetFileNameWithoutExtension(dialog.FileName);
            string symbol = ToCppIdentifier(stem);
            _displayName.Text = stem;
            _resourceId.Text = $"litheview.resource.{ToResourceStem(stem)}";
            _symbol.Text = symbol;
            _entryPoint.Text = char.ToLowerInvariant(symbol[0]) + symbol[1..];
            UpdatePreview();
        }
        catch (Exception exception) when (
            exception is ArgumentException or OutOfMemoryException or FileNotFoundException or
                UnauthorizedAccessException)
        {
            _validation.Text = $"The selected sprite sheet could not be decoded: {exception.Message}";
        }
    }

    private void BrowseLicense(object? sender, EventArgs eventArgs)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Select animation license text",
            Filter = "Text files (*.txt;*.md)|*.txt;*.md|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _licenseFile.Text = dialog.FileName;
        }
    }

    private void FormatChanged(object? sender, EventArgs eventArgs)
    {
        ResourceFormat format = SelectedFormat();
        bool alphaMask = format is ResourceFormat.Alpha1 or ResourceFormat.Alpha8;
        _tintable.Enabled = alphaMask;
        if (!alphaMask)
        {
            _tintable.Checked = false;
        }
        _paletteArgb.Enabled = format == ResourceFormat.Indexed4;
        if (format is not ResourceFormat.Indexed4 and not ResourceFormat.Rgb565 &&
            _dither.SelectedItem is ImageDitherMode.Ordered4x4)
        {
            _dither.SelectedItem = ImageDitherMode.None;
        }
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        int frameCount = Decimal.ToInt32(_frameCount.Value);
        int repeatIndex = Math.Min(Decimal.ToInt32(_repeatIndex.Value), frameCount - 1);
        _repeatIndex.Maximum = Math.Max(0, frameCount - 1);
        if (_repeatIndex.Value > _repeatIndex.Maximum)
        {
            _repeatIndex.Value = _repeatIndex.Maximum;
        }
        ResourceFormat format = SelectedFormat();
        int width = Decimal.ToInt32(_outputWidth.Value);
        int height = Decimal.ToInt32(_outputHeight.Value);
        int stride = ImageConversionRequestValidator.MinimumStride(format, width);
        ImageColorText.TryCanonicalizePalette(_paletteArgb.Text, out _, out int paletteCount);
        long frameBytes = (long)stride * height;
        long pixelBytes = frameBytes * frameCount;
        long paletteBytes = format == ResourceFormat.Indexed4 ? (long)paletteCount * 4L : 0L;
        _payload.Text = $"Measured representation before C++ syntax: {frameCount} × {frameBytes:N0} = " +
            $"{pixelBytes:N0} pixel bytes" +
            (paletteBytes == 0 ? string.Empty : $" + {paletteBytes:N0} shared palette bytes") +
            $" = {pixelBytes + paletteBytes:N0} bytes. This is not a target-memory placement.";

        ImageColorText.TryCanonicalizeArgb(_tintArgb.Text, out string tint);
        _preview.Configure(
            new Point(Decimal.ToInt32(_sheetX.Value), Decimal.ToInt32(_sheetY.Value)),
            new Size(Decimal.ToInt32(_sourceFrameWidth.Value), Decimal.ToInt32(_sourceFrameHeight.Value)),
            Decimal.ToInt32(_columns.Value),
            frameCount,
            new Size(width, height),
            _scaleMode.SelectedItem is ImageScaleMode mode ? mode : ImageScaleMode.Bilinear,
            _tintable.Checked,
            tint,
            Decimal.ToInt32(_interval.Value),
            repeatIndex);
    }

    private void AcceptImport(object? sender, EventArgs eventArgs)
    {
        ImageColorText.TryCanonicalizeArgb(_tintArgb.Text, out string tintArgb);
        ImageColorText.TryCanonicalizeArgb(_matteArgb.Text, out string matteArgb);
        ImageColorText.TryCanonicalizePalette(_paletteArgb.Text, out string paletteArgb, out _);
        AnimationConversionRequest conversion = new()
        {
            InputSpriteSheetPath = _sourceSheet.Text.Trim(),
            OutputSourcePath = "generated.cpp",
            OutputMetricsPath = "metrics.json",
            Symbol = _symbol.Text.Trim(),
            EntryPoint = _entryPoint.Text.Trim(),
            Tintable = _tintable.Checked,
            Format = SelectedFormat(),
            SheetX = Decimal.ToInt32(_sheetX.Value),
            SheetY = Decimal.ToInt32(_sheetY.Value),
            SourceFrameWidth = Decimal.ToInt32(_sourceFrameWidth.Value),
            SourceFrameHeight = Decimal.ToInt32(_sourceFrameHeight.Value),
            Columns = Decimal.ToInt32(_columns.Value),
            FrameCount = Decimal.ToInt32(_frameCount.Value),
            OutputWidth = Decimal.ToInt32(_outputWidth.Value),
            OutputHeight = Decimal.ToInt32(_outputHeight.Value),
            ScaleMode = (ImageScaleMode)_scaleMode.SelectedItem!,
            AlphaMode = (ImageAlphaMode)_alphaMode.SelectedItem!,
            AlphaThreshold = Decimal.ToInt32(_alphaThreshold.Value),
            Dither = (ImageDitherMode)_dither.SelectedItem!,
            TintArgb = tintArgb,
            MatteArgb = matteArgb,
            PaletteArgb = SelectedFormat() == ResourceFormat.Indexed4 ? paletteArgb : string.Empty,
            FrameIntervalMilliseconds = Decimal.ToInt32(_interval.Value),
            RepeatIndex = Decimal.ToInt32(_repeatIndex.Value),
            Autoplay = _autoplay.Checked
        };
        List<string> errors = [.. AnimationConversionRequestValidator.Validate(conversion)
            .Where(item => item.Severity == ProfileDiagnosticSeverity.Error)
            .Select(item => item.Message)];
        if (!File.Exists(conversion.InputSpriteSheetPath))
        {
            errors.Add("Select an existing raster sprite-sheet file.");
        }
        int rows = (conversion.FrameCount + conversion.Columns - 1) / conversion.Columns;
        long gridWidth = (long)conversion.Columns * conversion.SourceFrameWidth;
        long gridHeight = (long)rows * conversion.SourceFrameHeight;
        if (_sourcePixelSize.IsEmpty ||
            conversion.SheetX + gridWidth > _sourcePixelSize.Width ||
            conversion.SheetY + gridHeight > _sourcePixelSize.Height)
        {
            errors.Add("The complete row-major frame grid must fit the decoded sprite sheet.");
        }
        if (!IsResourceIdentifier(_resourceId.Text.Trim()) ||
            _resourceId.Text.Trim().Length > 118 ||
            string.IsNullOrWhiteSpace(_displayName.Text))
        {
            errors.Add("Enter a valid stable resource ID of at most 118 characters and a display name.");
        }
        if (string.IsNullOrWhiteSpace(_origin.Text))
        {
            errors.Add("Record where the sprite sheet came from.");
        }
        if (string.IsNullOrWhiteSpace(_spdxId.Text) && !File.Exists(_licenseFile.Text.Trim()))
        {
            errors.Add("Provide an SPDX license ID or an existing license text file.");
        }
        string? sourceUri = TrimOrNull(_sourceUri.Text);
        if (sourceUri is not null && !Uri.TryCreate(sourceUri, UriKind.Absolute, out _))
        {
            errors.Add("The optional source URI must be absolute.");
        }
        if (!_redistributionApproved.Checked)
        {
            errors.Add("Confirm that the animation may be redistributed with the project.");
        }
        if (errors.Count > 0)
        {
            _validation.Text = string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal));
            return;
        }

        Request = new AnimationResourceImportRequest
        {
            ProjectManifestPath = _projectManifestPath,
            SourceSpriteSheetPath = conversion.InputSpriteSheetPath,
            SourceLicensePath = File.Exists(_licenseFile.Text.Trim()) ? _licenseFile.Text.Trim() : null,
            ResourceId = _resourceId.Text.Trim(),
            DisplayName = _displayName.Text.Trim(),
            GeneratedSymbol = conversion.Symbol,
            EntryPoint = conversion.EntryPoint,
            Tintable = conversion.Tintable,
            Format = conversion.Format,
            SheetX = conversion.SheetX,
            SheetY = conversion.SheetY,
            SourceFrameWidth = conversion.SourceFrameWidth,
            SourceFrameHeight = conversion.SourceFrameHeight,
            Columns = conversion.Columns,
            FrameCount = conversion.FrameCount,
            OutputWidth = conversion.OutputWidth,
            OutputHeight = conversion.OutputHeight,
            ScaleMode = conversion.ScaleMode,
            AlphaMode = conversion.AlphaMode,
            AlphaThreshold = conversion.AlphaThreshold,
            Dither = conversion.Dither,
            TintArgb = conversion.TintArgb,
            MatteArgb = conversion.MatteArgb,
            PaletteArgb = conversion.PaletteArgb,
            FrameIntervalMilliseconds = conversion.FrameIntervalMilliseconds,
            RepeatIndex = conversion.RepeatIndex,
            Autoplay = conversion.Autoplay,
            Origin = _origin.Text.Trim(),
            Author = TrimOrNull(_author.Text),
            SourceUri = sourceUri,
            ProvenanceNotes = TrimOrNull(_notes.Text),
            SpdxId = TrimOrNull(_spdxId.Text),
            Attribution = TrimOrNull(_attribution.Text),
            RedistributionApproved = true
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private ResourceFormat SelectedFormat()
    {
        return _format.SelectedItem is ResourceFormat format ? format : ResourceFormat.Indexed4;
    }

    private static void ConfigureDimension(NumericUpDown control, int minimum, int maximum, int value)
    {
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.Value = value;
        control.ThousandsSeparator = true;
    }

    private static TableLayoutPanel FieldTable(int rows)
    {
        TableLayoutPanel fields = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
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

    private static void AddField(TableLayoutPanel fields, int row, string label, Control control)
    {
        fields.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Padding = new Padding(0, 6, 12, 0)
        }, 0, row);
        control.Dock = DockStyle.Top;
        fields.Controls.Add(control, 1, row);
    }

    private static void AddFileField(
        TableLayoutPanel fields,
        int row,
        string label,
        TextBox textBox,
        EventHandler browse)
    {
        FlowLayoutPanel value = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        textBox.Width = 620;
        Button button = new() { Text = "Browse...", AutoSize = true };
        button.Click += browse;
        value.Controls.Add(textBox);
        value.Controls.Add(button);
        AddField(fields, row, label, value);
    }

    private static string ToCppIdentifier(string value)
    {
        string[] parts = value.Split(
            value.Where(character => !char.IsAsciiLetterOrDigit(character)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries);
        string result = string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        if (string.IsNullOrEmpty(result) || !char.IsAsciiLetter(result[0]))
        {
            result = $"Animation{result}";
        }
        return result.Length <= 128 ? result : result[..128];
    }

    private static string ToResourceStem(string value)
    {
        string stem = new([.. value.ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')]);
        while (stem.Contains("--", StringComparison.Ordinal))
        {
            stem = stem.Replace("--", "-", StringComparison.Ordinal);
        }
        stem = stem.Trim('-');
        return string.IsNullOrEmpty(stem) ? "animation" : stem;
    }

    private static bool IsResourceIdentifier(string value)
    {
        if (value.Length is < 1 or > 128)
        {
            return false;
        }
        bool previousWasSeparator = true;
        foreach (char character in value)
        {
            bool separator = character is '.' or '-';
            bool allowed = character is >= 'a' and <= 'z' or >= '0' and <= '9' || separator;
            if (!allowed || separator && previousWasSeparator)
            {
                return false;
            }
            previousWasSeparator = separator;
        }
        return !previousWasSeparator;
    }

    private static string? TrimOrNull(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

internal sealed class AnimationConversionPreviewPanel : Panel
{
    private readonly System.Windows.Forms.Timer _timer = new();
    private Bitmap? _source;
    private Point _sheetOrigin;
    private Size _sourceFrameSize;
    private int _columns = 1;
    private int _frameCount = 2;
    private int _frameIndex;
    private int _repeatIndex;
    private Size _outputSize;
    private ImageScaleMode _scaleMode;
    private bool _tintable;
    private string _tintArgb = "ffffffff";
    private bool _playing = true;

    public AnimationConversionPreviewPanel()
    {
        BackColor = Color.FromArgb(42, 42, 46);
        BorderStyle = BorderStyle.FixedSingle;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        _timer.Interval = 100;
        _timer.Tick += (_, _) => AdvanceFrame();
        _timer.Start();
    }

    public Size SourcePixelSize => _source?.Size ?? Size.Empty;

    public void LoadImage(string path)
    {
        _source?.Dispose();
        _source = null;
        using Image loaded = Image.FromFile(path);
        _source = new Bitmap(loaded);
        _frameIndex = 0;
        Invalidate();
    }

    public void Configure(
        Point sheetOrigin,
        Size sourceFrameSize,
        int columns,
        int frameCount,
        Size outputSize,
        ImageScaleMode scaleMode,
        bool tintable,
        string tintArgb,
        int intervalMilliseconds,
        int repeatIndex)
    {
        _sheetOrigin = sheetOrigin;
        _sourceFrameSize = sourceFrameSize;
        _columns = Math.Max(1, columns);
        _frameCount = Math.Max(2, frameCount);
        _outputSize = outputSize;
        _scaleMode = scaleMode;
        _tintable = tintable;
        _tintArgb = tintArgb;
        _repeatIndex = Math.Clamp(repeatIndex, 0, _frameCount - 1);
        _frameIndex = Math.Clamp(_frameIndex, 0, _frameCount - 1);
        _timer.Interval = Math.Clamp(intervalMilliseconds, 16, 60_000);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }
        _playing = !_playing;
        if (_playing)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        DrawCheckerboard(eventArgs.Graphics, ClientRectangle);
        Rectangle content = Rectangle.Inflate(ClientRectangle, -22, -58);
        content.Y += 28;
        if (_source is null)
        {
            TextRenderer.DrawText(
                eventArgs.Graphics,
                "Select a sprite sheet to preview the declared row-major grid and timing. Click the preview to pause or play.",
                Font,
                content,
                Color.WhiteSmoke,
                TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        Rectangle sourceFrame = FrameRectangle(_frameIndex);
        Rectangle sourceBounds = new(Point.Empty, _source.Size);
        if (!sourceBounds.Contains(sourceFrame))
        {
            TextRenderer.DrawText(
                eventArgs.Graphics,
                "The current frame grid extends outside the source sprite sheet.",
                Font,
                content,
                Color.MistyRose,
                TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        float scale = Math.Min(
            (float)content.Width / Math.Max(1, _outputSize.Width),
            (float)content.Height / Math.Max(1, _outputSize.Height));
        Rectangle destination = new(
            content.X + ((content.Width - (int)(_outputSize.Width * scale)) / 2),
            content.Y + ((content.Height - (int)(_outputSize.Height * scale)) / 2),
            Math.Max(1, (int)(_outputSize.Width * scale)),
            Math.Max(1, (int)(_outputSize.Height * scale)));
        eventArgs.Graphics.InterpolationMode = _scaleMode == ImageScaleMode.NearestNeighbor
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBilinear;
        eventArgs.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        if (_tintable && TryColor(_tintArgb, out Color tint))
        {
            using ImageAttributes attributes = new();
            ColorMatrix matrix = new(
            [
                [0F, 0F, 0F, 0F, 0F],
                [0F, 0F, 0F, 0F, 0F],
                [0F, 0F, 0F, 0F, 0F],
                [0F, 0F, 0F, 1F, 0F],
                [tint.R / 255F, tint.G / 255F, tint.B / 255F, 0F, 1F]
            ]);
            attributes.SetColorMatrix(matrix);
            eventArgs.Graphics.DrawImage(
                _source,
                destination,
                sourceFrame.X,
                sourceFrame.Y,
                sourceFrame.Width,
                sourceFrame.Height,
                GraphicsUnit.Pixel,
                attributes);
        }
        else
        {
            eventArgs.Graphics.DrawImage(_source, destination, sourceFrame, GraphicsUnit.Pixel);
        }
        using Pen outline = new(Color.WhiteSmoke);
        eventArgs.Graphics.DrawRectangle(outline, destination);
        string caption = $"frame {_frameIndex + 1}/{_frameCount}  source {sourceFrame.X},{sourceFrame.Y} " +
            $"{sourceFrame.Width}x{sourceFrame.Height}  output {_outputSize.Width}x{_outputSize.Height}  " +
            (_playing ? "playing" : "paused");
        TextRenderer.DrawText(
            eventArgs.Graphics,
            caption,
            Font,
            new Rectangle(8, 8, Math.Max(1, ClientSize.Width - 16), 24),
            Color.WhiteSmoke,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            "Click to pause/play",
            Font,
            new Rectangle(8, Math.Max(8, ClientSize.Height - 30), Math.Max(1, ClientSize.Width - 16), 22),
            Color.Gainsboro,
            TextFormatFlags.HorizontalCenter);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _source?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void AdvanceFrame()
    {
        if (_source is null)
        {
            return;
        }
        _frameIndex = _frameIndex + 1 < _frameCount ? _frameIndex + 1 : _repeatIndex;
        Invalidate();
    }

    private Rectangle FrameRectangle(int index)
    {
        return new Rectangle(
            _sheetOrigin.X + ((index % _columns) * _sourceFrameSize.Width),
            _sheetOrigin.Y + ((index / _columns) * _sourceFrameSize.Height),
            _sourceFrameSize.Width,
            _sourceFrameSize.Height);
    }

    private static void DrawCheckerboard(Graphics graphics, Rectangle bounds)
    {
        const int cell = 12;
        using SolidBrush dark = new(Color.FromArgb(54, 54, 59));
        using SolidBrush light = new(Color.FromArgb(76, 76, 82));
        for (int y = bounds.Top; y < bounds.Bottom; y += cell)
        {
            for (int x = bounds.Left; x < bounds.Right; x += cell)
            {
                graphics.FillRectangle(
                    (((x / cell) + (y / cell)) & 1) == 0 ? dark : light,
                    x,
                    y,
                    cell,
                    cell);
            }
        }
    }

    private static bool TryColor(string argb, out Color color)
    {
        color = Color.White;
        if (!ImageColorText.IsCanonicalArgb(argb))
        {
            return false;
        }
        color = Color.FromArgb(
            Convert.ToInt32(argb[..2], 16),
            Convert.ToInt32(argb[2..4], 16),
            Convert.ToInt32(argb[4..6], 16),
            Convert.ToInt32(argb[6..8], 16));
        return true;
    }
}
