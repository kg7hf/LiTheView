// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Drawing.Text;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.App;

internal sealed class FontImportDialog : Form
{
    private readonly string _projectManifestPath;
    private readonly SurfacePixelFormat _painterFormat;
    private readonly TextBox _sourceFont = new();
    private readonly TextBox _displayName = new();
    private readonly TextBox _resourceId = new();
    private readonly TextBox _symbol = new();
    private readonly TextBox _entryPoint = new();
    private readonly NumericUpDown _pixelSize = new();
    private readonly ComboBox _rasterStyle = new();
    private readonly TextBox _styleLabel = new();
    private readonly NumericUpDown _weight = new();
    private readonly ComboBox _coverage = new();
    private readonly TextBox _repertoire = new();
    private readonly TextBox _origin = new();
    private readonly TextBox _author = new();
    private readonly TextBox _sourceUri = new();
    private readonly TextBox _spdxId = new();
    private readonly TextBox _licenseFile = new();
    private readonly TextBox _attribution = new();
    private readonly TextBox _notes = new();
    private readonly CheckBox _redistributionApproved = new();
    private readonly Label _validation = new();
    private readonly FontPreviewPanel _preview = new();

    public FontImportDialog(
        string projectManifestPath,
        SurfacePixelFormat painterFormat)
    {
        _projectManifestPath = projectManifestPath;
        _painterFormat = painterFormat;
        Text = "Import Font Resource";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(780, 700);
        Size = new Size(900, 820);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AccessibleName = "LitheView font resource importer";

        BuildLayout();
        InitializeDefaults();
    }

    public FontResourceImportRequest? Request { get; private set; }

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
            Text = "Create a deterministic embedded font resource",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        }, 0, 0);

        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Font import workflow"
        };
        tabs.TabPages.Add(BuildIdentityPage());
        tabs.TabPages.Add(BuildRasterizationPage());
        tabs.TabPages.Add(BuildProvenancePage());
        root.Controls.Add(tabs, 0, 1);

        _validation.AutoSize = true;
        _validation.ForeColor = Color.Firebrick;
        _validation.MaximumSize = new Size(820, 0);
        _validation.Padding = new Padding(4, 8, 4, 8);
        _validation.AccessibleName = "Font import validation";
        root.Controls.Add(_validation, 0, 2);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        Button import = new() { Text = "Import Font", AutoSize = true };
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
        TabPage page = new("Font file and identity");
        TableLayoutPanel fields = FieldTable(7);
        _sourceFont.AccessibleName = "Source font file";
        AddFileField(fields, 0, "TTF or OTF file", _sourceFont, BrowseFont);
        _displayName.AccessibleName = "Font resource display name";
        AddField(fields, 1, "Display name", _displayName);
        _resourceId.AccessibleName = "Font resource ID";
        AddField(fields, 2, "Stable resource ID", _resourceId);
        _symbol.AccessibleName = "Generated font table symbol";
        AddField(fields, 3, "Generated C++ table symbol", _symbol);
        _entryPoint.AccessibleName = "Generated font entry point";
        AddField(fields, 4, "Generated C++ entry point", _entryPoint);
        fields.Controls.Add(new Label
        {
            Text = $"The project preview uses {_painterFormat}. Font coverage is a painter-independent " +
                "shape and remains compatible with RGB565, RGB888, XRGB8888, and ARGB8888. Board, " +
                "bus, flash placement, and linker policy remain application concerns.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(620, 0),
            Padding = new Padding(0, 12, 0, 0)
        }, 1, 5);
        fields.Controls.Add(new Label
        {
            Text = "The original font and generated C++ are copied into a contained project resource " +
                "directory. Existing files are never overwritten or merged.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(620, 0)
        }, 1, 6);
        page.Controls.Add(fields);
        return page;
    }

    private TabPage BuildRasterizationPage()
    {
        TabPage page = new("Rasterization and repertoire");
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        TableLayoutPanel fields = FieldTable(6);
        _pixelSize.AccessibleName = "Font pixel size";
        AddField(fields, 0, "Pixel size", _pixelSize);
        _rasterStyle.AccessibleName = "Font raster style";
        AddField(fields, 1, "Raster style", _rasterStyle);
        _styleLabel.AccessibleName = "Font visual style label";
        AddField(fields, 2, "Visual style label", _styleLabel);
        _weight.AccessibleName = "Font visual weight";
        AddField(fields, 3, "Visual weight (1-1000)", _weight);
        _coverage.AccessibleName = "Font coverage";
        AddField(fields, 4, "Coverage fidelity", _coverage);
        _repertoire.AccessibleName = "Font repertoire";
        _repertoire.Multiline = true;
        _repertoire.Height = 54;
        AddField(fields, 5, "Unicode repertoire", _repertoire);
        root.Controls.Add(fields, 0, 0);

        _preview.Dock = DockStyle.Fill;
        _preview.MinimumSize = new Size(400, 180);
        _preview.AccessibleName = "Selected font repertoire preview";
        root.Controls.Add(_preview, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildProvenancePage()
    {
        TabPage page = new("Provenance and license");
        TableLayoutPanel fields = FieldTable(9);
        _origin.AccessibleName = "Font origin";
        AddField(fields, 0, "Origin", _origin);
        _author.AccessibleName = "Font author";
        AddField(fields, 1, "Author or foundry", _author);
        _sourceUri.AccessibleName = "Font source URI";
        AddField(fields, 2, "Source URI", _sourceUri);
        _spdxId.AccessibleName = "Font SPDX license ID";
        AddField(fields, 3, "SPDX ID", _spdxId);
        _licenseFile.AccessibleName = "Font license file";
        AddFileField(fields, 4, "License text file", _licenseFile, BrowseLicense);
        _attribution.AccessibleName = "Font attribution";
        AddField(fields, 5, "Attribution", _attribution);
        _notes.AccessibleName = "Font provenance notes";
        _notes.Multiline = true;
        _notes.Height = 70;
        AddField(fields, 6, "Notes", _notes);
        _redistributionApproved.Text = "I verified this font may be copied into and redistributed with this project";
        _redistributionApproved.AutoSize = true;
        _redistributionApproved.AccessibleName = "Font redistribution approval";
        fields.Controls.Add(_redistributionApproved, 1, 7);
        fields.Controls.Add(new Label
        {
            Text = "Origin and explicit redistribution approval are required. Provide either an SPDX ID " +
                "or a license text file; LitheView does not infer licensing from a file name.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(620, 0),
            Padding = new Padding(0, 10, 0, 0)
        }, 1, 8);
        page.Controls.Add(fields);
        return page;
    }

    private void InitializeDefaults()
    {
        _displayName.Text = "UI Font";
        _resourceId.Text = "litheview.resource.ui-font";
        _symbol.Text = "UiFont";
        _entryPoint.Text = "uiFont";
        _pixelSize.Minimum = 4;
        _pixelSize.Maximum = 64;
        _pixelSize.Value = 18;
        _rasterStyle.DropDownStyle = ComboBoxStyle.DropDownList;
        _rasterStyle.DataSource = Enum.GetValues<FontRasterStyle>();
        _styleLabel.Text = "Regular";
        _weight.Minimum = 1;
        _weight.Maximum = 1000;
        _weight.Value = 400;
        _coverage.DropDownStyle = ComboBoxStyle.DropDownList;
        _coverage.DataSource = new[] { ResourceFormat.Alpha1, ResourceFormat.Alpha8 };
        _coverage.SelectedItem = ResourceFormat.Alpha8;
        _repertoire.Text = "U+0020-U+007E";

        _pixelSize.ValueChanged += (_, _) => UpdatePreview();
        _rasterStyle.SelectedIndexChanged += RasterStyleChanged;
        _repertoire.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    private void BrowseFont(object? sender, EventArgs eventArgs)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Select Source Font",
            Filter = "OpenType fonts (*.ttf;*.otf)|*.ttf;*.otf|TrueType fonts (*.ttf)|*.ttf|" +
                "OpenType fonts (*.otf)|*.otf",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        _sourceFont.Text = dialog.FileName;
        _preview.LoadFont(dialog.FileName);
        string stem = Path.GetFileNameWithoutExtension(dialog.FileName);
        string symbol = ToCppIdentifier(stem);
        _displayName.Text = _preview.FamilyName ?? stem;
        _resourceId.Text = $"litheview.resource.{ToResourceStem(stem)}";
        _symbol.Text = symbol;
        _entryPoint.Text = char.ToLowerInvariant(symbol[0]) + symbol[1..];
        UpdatePreview();
    }

    private void BrowseLicense(object? sender, EventArgs eventArgs)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Select Font License Text",
            Filter = "Text files (*.txt;*.md)|*.txt;*.md|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _licenseFile.Text = dialog.FileName;
        }
    }

    private void RasterStyleChanged(object? sender, EventArgs eventArgs)
    {
        if (_rasterStyle.SelectedItem is FontRasterStyle.Bold)
        {
            if (_styleLabel.Text is "Regular" or "Bold")
            {
                _styleLabel.Text = "Bold";
            }
            if (_weight.Value is 400 or 700)
            {
                _weight.Value = 700;
            }
        }
        else
        {
            if (_styleLabel.Text is "Regular" or "Bold")
            {
                _styleLabel.Text = "Regular";
            }
            if (_weight.Value is 400 or 700)
            {
                _weight.Value = 400;
            }
        }
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        FontRasterStyle rasterStyle = _rasterStyle.SelectedItem is FontRasterStyle style
            ? style
            : FontRasterStyle.Regular;
        _preview.Configure(
            Decimal.ToInt32(_pixelSize.Value),
            rasterStyle,
            _repertoire.Text);
    }

    private void AcceptImport(object? sender, EventArgs eventArgs)
    {
        FontConversionRequest conversion = new()
        {
            InputFontPath = _sourceFont.Text.Trim(),
            OutputSourcePath = "generated.cpp",
            OutputMetricsPath = "metrics.json",
            Symbol = _symbol.Text.Trim(),
            EntryPoint = _entryPoint.Text.Trim(),
            PixelSize = Decimal.ToInt32(_pixelSize.Value),
            RasterStyle = (FontRasterStyle)_rasterStyle.SelectedItem!,
            StyleLabel = _styleLabel.Text.Trim(),
            Weight = Decimal.ToInt32(_weight.Value),
            Coverage = (ResourceFormat)_coverage.SelectedItem!,
            Codepoints = _repertoire.Text.Trim()
        };
        List<string> errors = [.. FontConversionRequestValidator.Validate(conversion)
            .Where(item => item.Severity == ProfileDiagnosticSeverity.Error)
            .Select(item => item.Message)];
        if (!File.Exists(conversion.InputFontPath))
        {
            errors.Add("Select an existing TTF or OTF font file.");
        }
        if (!IsResourceIdentifier(_resourceId.Text.Trim()) ||
            string.IsNullOrWhiteSpace(_displayName.Text))
        {
            errors.Add("Enter a valid stable resource ID and display name.");
        }
        if (string.IsNullOrWhiteSpace(_origin.Text))
        {
            errors.Add("Record where the font came from.");
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
            errors.Add("Confirm that the font may be redistributed with the project.");
        }
        if (errors.Count > 0)
        {
            _validation.Text = string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal));
            return;
        }

        Request = new FontResourceImportRequest
        {
            ProjectManifestPath = _projectManifestPath,
            SourceFontPath = conversion.InputFontPath,
            SourceLicensePath = File.Exists(_licenseFile.Text.Trim()) ? _licenseFile.Text.Trim() : null,
            ResourceId = _resourceId.Text.Trim(),
            DisplayName = _displayName.Text.Trim(),
            GeneratedSymbol = conversion.Symbol,
            EntryPoint = conversion.EntryPoint,
            PixelSize = conversion.PixelSize,
            RasterStyle = conversion.RasterStyle,
            StyleLabel = conversion.StyleLabel,
            Weight = conversion.Weight,
            Coverage = conversion.Coverage,
            Codepoints = conversion.Codepoints,
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
        textBox.Width = 480;
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
        string result = string.Concat(parts.Select(part =>
            char.ToUpperInvariant(part[0]) + part[1..]));
        if (string.IsNullOrEmpty(result) || !char.IsAsciiLetter(result[0]))
        {
            result = $"Font{result}";
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
        return string.IsNullOrEmpty(stem) ? "font" : stem;
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

internal sealed class FontPreviewPanel : Panel
{
    private PrivateFontCollection? _fontCollection;
    private string? _loadError;
    private int _previewPixelSize = 18;
    private FontRasterStyle _rasterStyle;
    private string _repertoire = string.Empty;

    public FontPreviewPanel()
    {
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        DoubleBuffered = true;
    }

    public string? FamilyName => _fontCollection?.Families.FirstOrDefault()?.Name;


    public void Configure(int pixelSize, FontRasterStyle rasterStyle, string repertoire)
    {
        _previewPixelSize = pixelSize;
        _rasterStyle = rasterStyle;
        _repertoire = repertoire;
        Invalidate();
    }

    public void LoadFont(string path)
    {
        _fontCollection?.Dispose();
        _fontCollection = null;
        _loadError = null;
        try
        {
            PrivateFontCollection fonts = new();
            fonts.AddFontFile(path);
            if (fonts.Families.Length == 0)
            {
                fonts.Dispose();
                _loadError = "The selected file contains no loadable font family.";
            }
            else
            {
                _fontCollection = fonts;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or FileNotFoundException or UnauthorizedAccessException)
        {
            _loadError = $"Preview unavailable: {exception.Message}";
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.Clear(BackColor);
        Rectangle content = Rectangle.Inflate(ClientRectangle, -16, -12);
        if (_fontCollection?.Families.FirstOrDefault() is not FontFamily family)
        {
            TextRenderer.DrawText(
                eventArgs.Graphics,
                _loadError ?? "Select a TTF or OTF file to preview the chosen face and repertoire.",
                Font,
                content,
                SystemColors.GrayText,
                TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);
            return;
        }

        FontStyle requested = _rasterStyle == FontRasterStyle.Bold ? FontStyle.Bold : FontStyle.Regular;
        FontStyle style = family.IsStyleAvailable(requested) ? requested : FontStyle.Regular;
        using Font previewFont = new(family, _previewPixelSize, style, GraphicsUnit.Pixel);
        string sample = $"LitheView  Aa Bb  0123456789  ?{Environment.NewLine}" +
            $"Repertoire: {_repertoire}";
        eventArgs.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        eventArgs.Graphics.DrawString(sample, previewFont, Brushes.Black, content);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fontCollection?.Dispose();
        }
        base.Dispose(disposing);
    }
}
