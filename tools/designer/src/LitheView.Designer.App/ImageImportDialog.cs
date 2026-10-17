// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.App;

internal sealed class ImageImportDialog : Form
{
    private const string DefaultPalette =
        "00000000,ff000000,ffffffff,ffff0000,ff00ff00,ff0000ff,ffffff00,ffff00ff," +
        "ff00ffff,ff808080,ff800000,ff008000,ff000080,ff808000,ff800080,ff008080";

    private readonly string _projectManifestPath;
    private readonly SurfacePixelFormat _painterFormat;
    private readonly ResourceKind _kind;
    private readonly TextBox _sourceImage = new();
    private readonly TextBox _displayName = new();
    private readonly TextBox _resourceId = new();
    private readonly TextBox _symbol = new();
    private readonly TextBox _entryPoint = new();
    private readonly NumericUpDown _cropX = new();
    private readonly NumericUpDown _cropY = new();
    private readonly NumericUpDown _cropWidth = new();
    private readonly NumericUpDown _cropHeight = new();
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
    private readonly ImageConversionPreviewPanel _preview = new();
    private Size _sourcePixelSize;

    public ImageImportDialog(
        string projectManifestPath,
        SurfacePixelFormat painterFormat,
        ResourceKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectManifestPath);
        if (kind is not ResourceKind.Image and not ResourceKind.Icon)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        _projectManifestPath = Path.GetFullPath(projectManifestPath);
        _painterFormat = painterFormat;
        _kind = kind;
        Text = $"Import deterministic {kind.ToString().ToLowerInvariant()} resource";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 720);
        Size = new Size(1040, 820);
        ShowInTaskbar = false;
        InitializeDefaults();
        BuildUi();
    }

    public ImageResourceImportRequest? Request { get; private set; }

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
            Text = _kind == ResourceKind.Icon
                ? "Prepare a fixed-color icon or reusable tintable alpha mask for the allocation-free Bitmap runtime."
                : "Crop, scale, and pack reviewed raster art into an allocation-free Bitmap resource.",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Padding = new Padding(4, 4, 4, 12)
        }, 0, 0);

        TabControl tabs = new() { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildIdentityPage());
        tabs.TabPages.Add(BuildConversionPage());
        tabs.TabPages.Add(BuildProvenancePage());
        root.Controls.Add(tabs, 0, 1);

        _validation.AutoSize = true;
        _validation.ForeColor = Color.Firebrick;
        _validation.MaximumSize = new Size(940, 0);
        _validation.Padding = new Padding(4, 8, 4, 8);
        _validation.AccessibleName = "Image import validation";
        root.Controls.Add(_validation, 0, 2);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        Button import = new() { Text = $"Import {_kind}", AutoSize = true };
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
        _sourceImage.AccessibleName = "Source raster image file";
        AddFileField(fields, 0, "PNG/BMP/JPEG/GIF/TIFF file", _sourceImage, BrowseImage);
        _displayName.AccessibleName = "Image resource display name";
        AddField(fields, 1, "Display name", _displayName);
        _resourceId.AccessibleName = "Image resource ID";
        AddField(fields, 2, "Stable resource ID", _resourceId);
        _symbol.AccessibleName = "Generated bitmap table symbol";
        AddField(fields, 3, "Generated C++ table symbol", _symbol);
        _entryPoint.AccessibleName = "Generated bitmap entry point";
        AddField(fields, 4, "Generated C++ entry point", _entryPoint);
        fields.Controls.Add(new Label
        {
            Text = $"The current preview painter is {_painterFormat}, but a LitheView Bitmap representation " +
                "can be sampled by RGB565, RGB888, XRGB8888, or ARGB8888 painters. The import is not " +
                "locked to the display selected when it was created.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(720, 0),
            Padding = new Padding(0, 12, 0, 0)
        }, 1, 5);
        fields.Controls.Add(new Label
        {
            Text = "The original art and generated C++ are copied into a contained project resource " +
                "directory. Existing files are never overwritten or merged.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(720, 0)
        }, 1, 6);
        fields.Controls.Add(new Label
        {
            Text = "SVG remains a valid source-manifest format, but this checked GDI+ raster converter " +
                "accepts PNG, BMP, JPEG, GIF, and TIFF. Export SVG art to a reviewed lossless PNG before import.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(720, 0)
        }, 1, 7);
        page.Controls.Add(fields);
        return page;
    }

    private TabPage BuildConversionPage()
    {
        TabPage page = new("Crop, representation, and preview");
        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(980, 500),
            SplitterDistance = 420,
            Panel1MinSize = 360,
            Panel2MinSize = 360
        };
        TableLayoutPanel fields = FieldTable(16);
        _cropX.AccessibleName = "Image crop X";
        _cropY.AccessibleName = "Image crop Y";
        _cropWidth.AccessibleName = "Image crop width";
        _cropHeight.AccessibleName = "Image crop height";
        _outputWidth.AccessibleName = "Image output width";
        _outputHeight.AccessibleName = "Image output height";
        _format.AccessibleName = "Image bitmap format";
        _scaleMode.AccessibleName = "Image scale sampling";
        _alphaMode.AccessibleName = "Image alpha handling";
        _alphaThreshold.AccessibleName = "Image alpha threshold";
        _dither.AccessibleName = "Image dither mode";
        _tintArgb.AccessibleName = "Icon preview tint";
        _matteArgb.AccessibleName = "Image opaque matte";
        AddField(fields, 0, "Crop X", _cropX);
        AddField(fields, 1, "Crop Y", _cropY);
        AddField(fields, 2, "Crop width", _cropWidth);
        AddField(fields, 3, "Crop height", _cropHeight);
        AddField(fields, 4, "Output width", _outputWidth);
        AddField(fields, 5, "Output height", _outputHeight);
        AddField(fields, 6, "Bitmap format", _format);
        AddField(fields, 7, "Scale sampling", _scaleMode);
        AddField(fields, 8, "Alpha handling", _alphaMode);
        AddField(fields, 9, "Alpha threshold", _alphaThreshold);
        AddField(fields, 10, "Dither", _dither);
        _tintable.Text = "Reusable tintable icon mask";
        _tintable.AutoSize = true;
        _tintable.AccessibleName = "Tintable icon mask";
        fields.Controls.Add(_tintable, 1, 11);
        AddField(fields, 12, "Preview tint AARRGGBB", _tintArgb);
        AddField(fields, 13, "Opaque matte AARRGGBB", _matteArgb);
        _paletteArgb.Multiline = true;
        _paletteArgb.Height = 62;
        _paletteArgb.AccessibleName = "Indexed4 ARGB palette";
        AddField(fields, 14, "Indexed4 palette", _paletteArgb);
        _payload.AutoSize = true;
        _payload.ForeColor = SystemColors.GrayText;
        _payload.MaximumSize = new Size(300, 0);
        fields.Controls.Add(_payload, 1, 15);
        split.Panel1.Controls.Add(fields);

        _preview.Dock = DockStyle.Fill;
        _preview.MinimumSize = new Size(360, 360);
        _preview.AccessibleName = "Image crop and tint preview";
        split.Panel2.Controls.Add(_preview);
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildProvenancePage()
    {
        TabPage page = new("Provenance and license");
        TableLayoutPanel fields = FieldTable(9);
        _origin.AccessibleName = "Image origin";
        _spdxId.AccessibleName = "Image SPDX license ID";
        _redistributionApproved.AccessibleName = "Image redistribution approval";
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
            $"I verified this {_kind.ToString().ToLowerInvariant()} may be copied into and redistributed with this project";
        _redistributionApproved.AutoSize = true;
        fields.Controls.Add(_redistributionApproved, 1, 7);
        fields.Controls.Add(new Label
        {
            Text = "Origin and explicit redistribution approval are required. Provide either an SPDX ID " +
                "or a license text file; LitheView does not infer licensing from a file name.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(720, 0),
            Padding = new Padding(0, 10, 0, 0)
        }, 1, 8);
        page.Controls.Add(fields);
        return page;
    }

    private void InitializeDefaults()
    {
        string noun = _kind.ToString();
        _displayName.Text = $"UI {noun}";
        _resourceId.Text = $"litheview.resource.ui-{noun.ToLowerInvariant()}";
        _symbol.Text = $"Ui{noun}";
        _entryPoint.Text = $"ui{noun}";
        ConfigureDimension(_cropX, 0, 16383, 0);
        ConfigureDimension(_cropY, 0, 16383, 0);
        ConfigureDimension(_cropWidth, 1, 16384, 32);
        ConfigureDimension(_cropHeight, 1, 16384, 32);
        ConfigureDimension(_outputWidth, 1, 4096, 32);
        ConfigureDimension(_outputHeight, 1, 4096, 32);
        _format.DropDownStyle = ComboBoxStyle.DropDownList;
        _format.DataSource = Enum.GetValues<ResourceFormat>()
            .Where(ImageConversionRequestValidator.IsBitmapFormat)
            .ToArray();
        _format.SelectedItem = _kind == ResourceKind.Icon ? ResourceFormat.Alpha8 : ResourceFormat.Argb8888;
        _scaleMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _scaleMode.DataSource = Enum.GetValues<ImageScaleMode>();
        _scaleMode.SelectedItem = ImageScaleMode.Bilinear;
        _alphaMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _alphaMode.DataSource = Enum.GetValues<ImageAlphaMode>();
        _alphaMode.SelectedItem = ImageAlphaMode.Preserve;
        ConfigureDimension(_alphaThreshold, 0, 255, 128);
        _dither.DropDownStyle = ComboBoxStyle.DropDownList;
        _dither.DataSource = Enum.GetValues<ImageDitherMode>();
        _dither.SelectedItem = ImageDitherMode.None;
        _tintable.Checked = _kind == ResourceKind.Icon;
        _tintArgb.Text = "ffffffff";
        _matteArgb.Text = "ff000000";
        _paletteArgb.Text = DefaultPalette;

        foreach (NumericUpDown control in new[]
                 {
                     _cropX, _cropY, _cropWidth, _cropHeight, _outputWidth, _outputHeight, _alphaThreshold
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

    private void BrowseImage(object? sender, EventArgs eventArgs)
    {
        using OpenFileDialog dialog = new()
        {
            Title = $"Select source {_kind.ToString().ToLowerInvariant()}",
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
            _sourceImage.Text = dialog.FileName;
            _cropX.Value = 0;
            _cropY.Value = 0;
            _cropWidth.Value = Math.Min(_cropWidth.Maximum, _sourcePixelSize.Width);
            _cropHeight.Value = Math.Min(_cropHeight.Maximum, _sourcePixelSize.Height);
            int desiredWidth = _kind == ResourceKind.Icon ? Math.Min(64, _sourcePixelSize.Width) :
                Math.Min(4096, _sourcePixelSize.Width);
            int desiredHeight = _kind == ResourceKind.Icon ? Math.Min(64, _sourcePixelSize.Height) :
                Math.Min(4096, _sourcePixelSize.Height);
            _outputWidth.Value = Math.Max(1, desiredWidth);
            _outputHeight.Value = Math.Max(1, desiredHeight);
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
            _validation.Text = $"The selected image could not be decoded: {exception.Message}";
        }
    }

    private void BrowseLicense(object? sender, EventArgs eventArgs)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Select image license text",
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
        _tintable.Enabled = _kind == ResourceKind.Icon && alphaMask;
        if (!_tintable.Enabled)
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
        ResourceFormat format = SelectedFormat();
        int width = Decimal.ToInt32(_outputWidth.Value);
        int height = Decimal.ToInt32(_outputHeight.Value);
        int stride = ImageConversionRequestValidator.MinimumStride(format, width);
        ImageColorText.TryCanonicalizePalette(_paletteArgb.Text, out _, out int paletteCount);
        long pixelBytes = (long)stride * height;
        long paletteBytes = format == ResourceFormat.Indexed4 ? (long)paletteCount * 4L : 0L;
        _payload.Text = $"Measured representation before C++ syntax: {pixelBytes:N0} pixel bytes" +
            (paletteBytes == 0 ? string.Empty : $" + {paletteBytes:N0} palette bytes") +
            $" = {pixelBytes + paletteBytes:N0} bytes. This is an artifact measurement, not a target-memory placement.";

        Rectangle crop = new(
            Decimal.ToInt32(_cropX.Value),
            Decimal.ToInt32(_cropY.Value),
            Decimal.ToInt32(_cropWidth.Value),
            Decimal.ToInt32(_cropHeight.Value));
        ImageColorText.TryCanonicalizeArgb(_tintArgb.Text, out string tint);
        _preview.Configure(
            crop,
            new Size(width, height),
            _scaleMode.SelectedItem is ImageScaleMode mode ? mode : ImageScaleMode.Bilinear,
            _tintable.Checked,
            tint);
    }

    private void AcceptImport(object? sender, EventArgs eventArgs)
    {
        ImageColorText.TryCanonicalizeArgb(_tintArgb.Text, out string tintArgb);
        ImageColorText.TryCanonicalizeArgb(_matteArgb.Text, out string matteArgb);
        ImageColorText.TryCanonicalizePalette(_paletteArgb.Text, out string paletteArgb, out _);
        ImageConversionRequest conversion = new()
        {
            InputImagePath = _sourceImage.Text.Trim(),
            OutputSourcePath = "generated.cpp",
            OutputMetricsPath = "metrics.json",
            Symbol = _symbol.Text.Trim(),
            EntryPoint = _entryPoint.Text.Trim(),
            Kind = _kind,
            Tintable = _tintable.Checked,
            Format = SelectedFormat(),
            CropX = Decimal.ToInt32(_cropX.Value),
            CropY = Decimal.ToInt32(_cropY.Value),
            CropWidth = Decimal.ToInt32(_cropWidth.Value),
            CropHeight = Decimal.ToInt32(_cropHeight.Value),
            OutputWidth = Decimal.ToInt32(_outputWidth.Value),
            OutputHeight = Decimal.ToInt32(_outputHeight.Value),
            ScaleMode = (ImageScaleMode)_scaleMode.SelectedItem!,
            AlphaMode = (ImageAlphaMode)_alphaMode.SelectedItem!,
            AlphaThreshold = Decimal.ToInt32(_alphaThreshold.Value),
            Dither = (ImageDitherMode)_dither.SelectedItem!,
            TintArgb = tintArgb,
            MatteArgb = matteArgb,
            PaletteArgb = SelectedFormat() == ResourceFormat.Indexed4 ? paletteArgb : string.Empty
        };
        List<string> errors = [.. ImageConversionRequestValidator.Validate(conversion)
            .Where(item => item.Severity == ProfileDiagnosticSeverity.Error)
            .Select(item => item.Message)];
        if (!File.Exists(conversion.InputImagePath))
        {
            errors.Add("Select an existing raster image file.");
        }
        if (_sourcePixelSize.IsEmpty || conversion.CropX > _sourcePixelSize.Width - conversion.CropWidth ||
            conversion.CropY > _sourcePixelSize.Height - conversion.CropHeight)
        {
            errors.Add("The crop rectangle must fit the decoded source image.");
        }
        if (!IsResourceIdentifier(_resourceId.Text.Trim()) || string.IsNullOrWhiteSpace(_displayName.Text))
        {
            errors.Add("Enter a valid stable resource ID and display name.");
        }
        if (string.IsNullOrWhiteSpace(_origin.Text))
        {
            errors.Add("Record where the image came from.");
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
            errors.Add("Confirm that the image may be redistributed with the project.");
        }
        if (errors.Count > 0)
        {
            _validation.Text = string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal));
            return;
        }

        Request = new ImageResourceImportRequest
        {
            ProjectManifestPath = _projectManifestPath,
            SourceImagePath = conversion.InputImagePath,
            SourceLicensePath = File.Exists(_licenseFile.Text.Trim()) ? _licenseFile.Text.Trim() : null,
            ResourceId = _resourceId.Text.Trim(),
            DisplayName = _displayName.Text.Trim(),
            GeneratedSymbol = conversion.Symbol,
            EntryPoint = conversion.EntryPoint,
            Kind = conversion.Kind,
            Tintable = conversion.Tintable,
            Format = conversion.Format,
            CropX = conversion.CropX,
            CropY = conversion.CropY,
            CropWidth = conversion.CropWidth,
            CropHeight = conversion.CropHeight,
            OutputWidth = conversion.OutputWidth,
            OutputHeight = conversion.OutputHeight,
            ScaleMode = conversion.ScaleMode,
            AlphaMode = conversion.AlphaMode,
            AlphaThreshold = conversion.AlphaThreshold,
            Dither = conversion.Dither,
            TintArgb = conversion.TintArgb,
            MatteArgb = conversion.MatteArgb,
            PaletteArgb = conversion.PaletteArgb,
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
        return _format.SelectedItem is ResourceFormat format ? format : ResourceFormat.Argb8888;
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
        textBox.Width = 560;
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
            result = $"Image{result}";
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
        return string.IsNullOrEmpty(stem) ? "image" : stem;
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

internal sealed class ImageConversionPreviewPanel : Panel
{
    private Bitmap? _source;
    private string? _error;
    private Rectangle _crop;
    private Size _outputSize;
    private ImageScaleMode _scaleMode;
    private bool _tintable;
    private string _tintArgb = "ffffffff";

    public ImageConversionPreviewPanel()
    {
        BackColor = Color.FromArgb(42, 42, 46);
        BorderStyle = BorderStyle.FixedSingle;
        DoubleBuffered = true;
    }

    public Size SourcePixelSize => _source?.Size ?? Size.Empty;

    public void LoadImage(string path)
    {
        _source?.Dispose();
        _source = null;
        _error = null;
        using Image loaded = Image.FromFile(path);
        _source = new Bitmap(loaded);
        _crop = new Rectangle(Point.Empty, _source.Size);
        Invalidate();
    }

    public void Configure(
        Rectangle crop,
        Size outputSize,
        ImageScaleMode scaleMode,
        bool tintable,
        string tintArgb)
    {
        _crop = crop;
        _outputSize = outputSize;
        _scaleMode = scaleMode;
        _tintable = tintable;
        _tintArgb = tintArgb;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        DrawCheckerboard(eventArgs.Graphics, ClientRectangle);
        Rectangle content = Rectangle.Inflate(ClientRectangle, -18, -42);
        content.Y += 22;
        if (_source is null)
        {
            TextRenderer.DrawText(
                eventArgs.Graphics,
                _error ?? "Select a raster image to preview the crop, scale, alpha-mask tint, and output geometry.",
                Font,
                content,
                Color.WhiteSmoke,
                TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        Rectangle sourceBounds = new(Point.Empty, _source.Size);
        Rectangle crop = Rectangle.Intersect(sourceBounds, _crop);
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            TextRenderer.DrawText(eventArgs.Graphics, "Crop is outside the source.", Font, content, Color.MistyRose);
            return;
        }

        float scale = Math.Min((float)content.Width / crop.Width, (float)content.Height / crop.Height);
        Rectangle destination = new(
            content.X + ((content.Width - (int)(crop.Width * scale)) / 2),
            content.Y + ((content.Height - (int)(crop.Height * scale)) / 2),
            Math.Max(1, (int)(crop.Width * scale)),
            Math.Max(1, (int)(crop.Height * scale)));
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
                crop.X,
                crop.Y,
                crop.Width,
                crop.Height,
                GraphicsUnit.Pixel,
                attributes);
        }
        else
        {
            eventArgs.Graphics.DrawImage(_source, destination, crop, GraphicsUnit.Pixel);
        }
        using Pen outline = new(Color.WhiteSmoke);
        eventArgs.Graphics.DrawRectangle(outline, destination);
        string caption = $"source {_source.Width}x{_source.Height}  crop {crop.X},{crop.Y} " +
            $"{crop.Width}x{crop.Height}  output {_outputSize.Width}x{_outputSize.Height}";
        TextRenderer.DrawText(
            eventArgs.Graphics,
            caption,
            Font,
            new Rectangle(8, 8, Math.Max(1, ClientSize.Width - 16), 22),
            Color.WhiteSmoke,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _source?.Dispose();
        }
        base.Dispose(disposing);
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
