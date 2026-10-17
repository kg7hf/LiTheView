// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;

namespace LitheView.Designer.App;

internal sealed class ActionAssignmentDialog : Form
{
    private const string UnassignedLabel = "(unassigned)";
    private const string CreateLabel = "<create new action>";

    private readonly ComboBox _assignment = new();
    private readonly TextBox _id = new();
    private readonly TextBox _symbol = new();
    private readonly NumericUpDown _runtimeId = new();
    private readonly GroupBox _newAction = new();
    private readonly FormAction[] _actions;

    public ActionAssignmentDialog(
        string eventName,
        IEnumerable<FormAction> actions,
        string? currentActionId,
        IEnumerable<ushort>? reservedRuntimeIds = null)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(actions);
        _actions = [.. actions.OrderBy(item => item.RuntimeId)];
        Text = $"Assign {eventName} action";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 300);
        Font = new Font("Segoe UI", 9F);
        AccessibleName = "Assign form action";

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            Text = "Choose an application-owned action ID for this widget event.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        });
        _assignment.Dock = DockStyle.Top;
        _assignment.DropDownStyle = ComboBoxStyle.DropDownList;
        _assignment.AccessibleName = "Form action assignment";
        _assignment.Items.Add(UnassignedLabel);
        foreach (FormAction action in _actions)
        {
            _assignment.Items.Add(FormatAction(action));
        }
        _assignment.Items.Add(CreateLabel);
        _assignment.SelectedIndexChanged += (_, _) => UpdateNewActionState();
        int selectedIndex = Array.FindIndex(_actions, item => item.Id == currentActionId);
        _assignment.SelectedIndex = selectedIndex < 0 ? 0 : selectedIndex + 1;
        layout.Controls.Add(_assignment);

        _newAction.Text = "New stable action";
        _newAction.Dock = DockStyle.Fill;
        _newAction.Margin = new Padding(0, 12, 0, 12);
        TableLayoutPanel fields = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        AddField(fields, 0, "Action ID", _id);
        AddField(fields, 1, "C++ symbol", _symbol);
        _runtimeId.Minimum = 1;
        _runtimeId.Maximum = ushort.MaxValue;
        _runtimeId.Value = NextRuntimeId(_actions, reservedRuntimeIds ?? []);
        _runtimeId.ThousandsSeparator = true;
        AddField(fields, 2, "Runtime ID", _runtimeId);
        _newAction.Controls.Add(fields);
        layout.Controls.Add(_newAction);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false
        };
        Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        Button accept = new() { Text = "Assign", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(accept);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = accept;
        CancelButton = cancel;
        FormClosing += ValidateSelection;
        UpdateNewActionState();
    }

    public string? SelectedActionId { get; private set; }

    public FormAction? CreatedAction { get; private set; }

    private bool IsCreating => _assignment.SelectedItem as string == CreateLabel;

    private void UpdateNewActionState()
    {
        _newAction.Enabled = IsCreating;
        if (IsCreating && string.IsNullOrWhiteSpace(_id.Text))
        {
            _id.Text = "action.new-action";
            _symbol.Text = "NewAction";
        }
    }

    private void ValidateSelection(object? sender, FormClosingEventArgs eventArgs)
    {
        if (DialogResult != DialogResult.OK)
        {
            return;
        }
        CreatedAction = null;
        if (_assignment.SelectedIndex == 0)
        {
            SelectedActionId = null;
            return;
        }
        if (!IsCreating)
        {
            FormAction action = _actions[_assignment.SelectedIndex - 1];
            SelectedActionId = action.Id;
            return;
        }
        string id = _id.Text.Trim();
        string symbol = _symbol.Text.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(symbol))
        {
            MessageBox.Show(
                this,
                "Action ID and C++ symbol are required.",
                "Incomplete action",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            eventArgs.Cancel = true;
            return;
        }
        CreatedAction = new FormAction
        {
            Id = id,
            RuntimeId = checked((ushort)_runtimeId.Value),
            Symbol = symbol
        };
        SelectedActionId = id;
    }

    private static string FormatAction(FormAction action)
    {
        return $"{action.Symbol}  [{action.RuntimeId}]  {action.Id}";
    }

    private static decimal NextRuntimeId(
        IEnumerable<FormAction> actions,
        IEnumerable<ushort> reservedRuntimeIds)
    {
        HashSet<ushort> used = [.. actions.Select(item => item.RuntimeId)];
        used.UnionWith(reservedRuntimeIds);
        for (int candidate = 1; candidate <= ushort.MaxValue; ++candidate)
        {
            ushort runtimeId = checked((ushort)candidate);
            if (!used.Contains(runtimeId))
            {
                return runtimeId;
            }
        }
        throw new InvalidOperationException("No unassigned 16-bit action ID remains.");
    }

    private static void AddField(TableLayoutPanel fields, int row, string label, Control editor)
    {
        editor.Dock = DockStyle.Fill;
        editor.Margin = new Padding(8, 4, 0, 4);
        fields.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 4)
        }, 0, row);
        fields.Controls.Add(editor, 1, row);
    }
}
