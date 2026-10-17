// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using LitheView.Designer.Core.Documents;

namespace LitheView.Designer.App;

internal sealed class ThemeColorEditorDialog : Form
{
    private static readonly FormThemeColor[] StandardTokens =
    [
        new() { Id = "theme.surface", EncodedColor = 0x00ffffffU },
        new() { Id = "theme.content", EncodedColor = 0x00282828U },
        new() { Id = "theme.primary", EncodedColor = 0x00df6c2dU },
        new() { Id = "theme.on-primary", EncodedColor = 0x00ffffffU },
        new() { Id = "theme.primary-pressed", EncodedColor = 0x00b35724U },
        new() { Id = "theme.disabled-surface", EncodedColor = 0x00dcd4d0U },
        new() { Id = "theme.disabled-content", EncodedColor = 0x00786f6aU },
        new() { Id = "theme.selected", EncodedColor = 0x00fae4d6U },
        new() { Id = "theme.on-selected", EncodedColor = 0x00803d1aU },
        new() { Id = "theme.outline", EncodedColor = 0x00b8b0a8U }
    ];

    private readonly ListView _tokens = new();
    private readonly TextBox _id = new();
    private readonly TextBox _color = new();
    private readonly Panel _swatch = new();
    private readonly Label _status = new();

    public ThemeColorEditorDialog(IReadOnlyList<FormThemeColor> themeColors)
    {
        ArgumentNullException.ThrowIfNull(themeColors);
        Text = "Theme Color Tokens";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 520);
        Size = new Size(780, 580);
        Font = new Font("Segoe UI", 9F);
        AccessibleName = "Bounded theme color token editor";

        Controls.Add(BuildLayout());
        foreach (FormThemeColor token in themeColors.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            AddListItem(token);
        }
        UpdateStatus();
    }

    public FormThemeColor[] ThemeColors =>
        [.. _tokens.Items.Cast<ListViewItem>().Select(item => new FormThemeColor
        {
            Id = item.Text,
            EncodedColor = uint.Parse(
                item.SubItems[1].Text,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture)
        }).OrderBy(item => item.Id, StringComparer.Ordinal)];

    private TableLayoutPanel BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Padding = new Padding(0, 0, 0, 10),
            Text = "Define up to 32 stable color tokens. In a component color property, choose a " +
                "theme.* token or enter a literal eight-digit legacy color. State slots resolve in " +
                "the fixed order disabled, pressed, focused, selected, normal."
        });

        _tokens.Dock = DockStyle.Fill;
        _tokens.View = View.Details;
        _tokens.FullRowSelect = true;
        _tokens.MultiSelect = false;
        _tokens.HideSelection = false;
        _tokens.AccessibleName = "Theme color token list";
        _tokens.Columns.Add("Stable token ID", 390);
        _tokens.Columns.Add("Encoded color", 170);
        _tokens.SelectedIndexChanged += (_, _) => LoadSelection();
        root.Controls.Add(_tokens);

        TableLayoutPanel editor = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 6,
            Padding = new Padding(0, 10, 0, 4)
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editor.Controls.Add(new Label { Text = "Token ID", AutoSize = true, Anchor = AnchorStyles.Left });
        _id.Dock = DockStyle.Fill;
        _id.AccessibleName = "Theme token stable ID";
        _id.PlaceholderText = "theme.primary";
        editor.Controls.Add(_id);
        editor.Controls.Add(new Label { Text = "Color", AutoSize = true, Anchor = AnchorStyles.Left });
        _color.Dock = DockStyle.Fill;
        _color.AccessibleName = "Theme token encoded color";
        _color.PlaceholderText = "00df6c2d";
        _color.TextChanged += (_, _) => UpdateSwatch();
        editor.Controls.Add(_color);
        _swatch.Dock = DockStyle.Fill;
        _swatch.BorderStyle = BorderStyle.FixedSingle;
        _swatch.AccessibleName = "Theme token color preview";
        editor.Controls.Add(_swatch);
        Button addOrUpdate = new()
        {
            Text = "Add / Update",
            AutoSize = true,
            AccessibleName = "Add or update theme token"
        };
        addOrUpdate.Click += (_, _) => AddOrUpdate();
        editor.Controls.Add(addOrUpdate);
        root.Controls.Add(editor);

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        Button standard = new()
        {
            Text = "Add Standard Tokens",
            AutoSize = true,
            AccessibleName = "Add standard theme tokens"
        };
        standard.Click += (_, _) => AddStandardTokens();
        actions.Controls.Add(standard);
        Button remove = new()
        {
            Text = "Remove Selected",
            AutoSize = true,
            AccessibleName = "Remove selected theme token"
        };
        remove.Click += (_, _) => RemoveSelected();
        actions.Controls.Add(remove);
        _status.AutoSize = true;
        _status.Padding = new Padding(12, 7, 0, 0);
        actions.Controls.Add(_status);
        root.Controls.Add(actions);

        FlowLayoutPanel close = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        Button ok = new() { Text = "OK", AutoSize = true, AccessibleName = "Accept theme tokens" };
        ok.Click += (_, _) => AcceptTokens();
        Button cancel = new()
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
            AccessibleName = "Cancel theme token editing"
        };
        close.Controls.Add(ok);
        close.Controls.Add(cancel);
        root.Controls.Add(close);
        AcceptButton = ok;
        CancelButton = cancel;
        return root;
    }

    private void AddOrUpdate()
    {
        if (!TryReadEditor(out FormThemeColor? token))
        {
            return;
        }
        ListViewItem? existing = _tokens.Items.Cast<ListViewItem>().SingleOrDefault(
            item => item.Text == token.Id);
        if (existing is null)
        {
            if (_tokens.Items.Count >= FormDocumentLimits.MaximumThemeColors)
            {
                ShowError($"A form supports at most {FormDocumentLimits.MaximumThemeColors} theme tokens.");
                return;
            }
            AddListItem(token);
        }
        else
        {
            existing.SubItems[1].Text = token.EncodedColor.ToString("x8", CultureInfo.InvariantCulture);
            existing.Selected = true;
        }
        UpdateStatus();
    }

    private void AddStandardTokens()
    {
        foreach (FormThemeColor token in StandardTokens)
        {
            ListViewItem? existing = _tokens.Items.Cast<ListViewItem>().SingleOrDefault(
                item => item.Text == token.Id);
            if (existing is null)
            {
                if (_tokens.Items.Count == FormDocumentLimits.MaximumThemeColors)
                {
                    break;
                }
                AddListItem(token);
            }
        }
        UpdateStatus();
    }

    private void RemoveSelected()
    {
        if (_tokens.SelectedItems.Count == 1)
        {
            _tokens.Items.Remove(_tokens.SelectedItems[0]);
            UpdateStatus();
        }
    }

    private void LoadSelection()
    {
        if (_tokens.SelectedItems.Count != 1)
        {
            return;
        }
        ListViewItem item = _tokens.SelectedItems[0];
        _id.Text = item.Text;
        _color.Text = item.SubItems[1].Text;
    }

    private void AcceptTokens()
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryReadEditor(out FormThemeColor token)
    {
        string id = _id.Text.Trim();
        string color = _color.Text.Trim().TrimStart('#');
        if (!id.StartsWith("theme.", StringComparison.Ordinal) || !FormSymbolRules.IsStableId(id))
        {
            ShowError("A token ID must be a stable theme.* identifier.");
            token = null!;
            return false;
        }
        if (color.Length != 8 ||
            !uint.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint encoded))
        {
            ShowError("Color must contain exactly eight hexadecimal digits.");
            token = null!;
            return false;
        }
        if (encoded == FormDocumentLimits.UnspecifiedStyleStateColor)
        {
            ShowError("That transparent chroma is reserved for an omitted widget-state color.");
            token = null!;
            return false;
        }
        token = new FormThemeColor { Id = id, EncodedColor = encoded };
        return true;
    }

    private void AddListItem(FormThemeColor token)
    {
        ListViewItem item = new(token.Id);
        item.SubItems.Add(token.EncodedColor.ToString("x8", CultureInfo.InvariantCulture));
        _tokens.Items.Add(item);
    }

    private void UpdateSwatch()
    {
        string color = _color.Text.Trim().TrimStart('#');
        if (color.Length == 8 &&
            uint.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint encoded))
        {
            int red = (int)(encoded & 0xffU);
            int green = (int)((encoded >> 8) & 0xffU);
            int blue = (int)((encoded >> 16) & 0xffU);
            _swatch.BackColor = Color.FromArgb(red, green, blue);
        }
    }

    private void UpdateStatus()
    {
        _status.Text = $"{_tokens.Items.Count} / {FormDocumentLimits.MaximumThemeColors} tokens";
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
