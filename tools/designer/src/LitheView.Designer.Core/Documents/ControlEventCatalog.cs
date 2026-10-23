// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Documents;

public sealed record ControlEventDescriptor(
    string Id, string Name, string Suffix, string ParameterType, string DelegateMethod);

// Shared by generation, the service and its inspector response. The webview does
// not guess events from widget IDs. Empty entries are deliberate policies, not
// promises that an unimplemented interaction will produce a callback.
public static class ControlEventCatalog
{
    private static readonly ControlEventDescriptor Clicked =
        new("clicked", "Clicked", "Clicked", "", "onControlAction");
    private static readonly ControlEventDescriptor Checked =
        new("checkedChanged", "Checked changed", "CheckedChanged", "lithe::CheckedChange", "onControlCheckedChanged");
    private static readonly ControlEventDescriptor Selection =
        new("selectionChanged", "Selection changed", "SelectionChanged", "lithe::SelectionChange", "onControlSelectionChanged");
    private static readonly ControlEventDescriptor Activated =
        new("itemActivated", "Item activated", "ItemActivated", "lithe::ItemActivation", "onControlItemActivated");
    private static readonly ControlEventDescriptor RadioSelection =
        new("radioSelectionChanged", "Radio selection changed", "RadioSelectionChanged", "lithe::RadioSelectionChange", "onControlRadioSelectionChanged");
    private static readonly ControlEventDescriptor Committed =
        new("editCommitted", "Edit committed", "EditCommitted", "lithe::SelectionEdit", "onControlEditCommitted");
    private static readonly ControlEventDescriptor Cancelled =
        new("editCancelled", "Edit cancelled", "EditCancelled", "lithe::SelectionEdit", "onControlEditCancelled");

    private static readonly ControlEventDescriptor TextChanged =
        new("textChanged", "Text changed", "TextChanged", "lithe::TextChange", "onControlTextChanged");
    private static readonly ControlEventDescriptor TextCommitted =
        new("editCommitted", "Edit committed", "EditCommitted", "lithe::TextEdit", "onControlTextEditCommitted");
    private static readonly ControlEventDescriptor TextCancelled =
        new("editCancelled", "Edit cancelled", "EditCancelled", "lithe::TextEdit", "onControlTextEditCancelled");

    public static IReadOnlyList<ControlEventDescriptor> For(string runtimeKind) => runtimeKind switch
    {
        "button" => [Clicked, Checked],
        "checkbox" => [Checked],
        "radio" => [RadioSelection],
        "picker" => [Selection],
        "list" => [Selection, Activated],
        "dropDown" => [Selection, Committed, Cancelled],
        "wheelPicker" => [Selection, Committed, Cancelled],
        "textField" => [TextChanged, TextCommitted, TextCancelled],
        _ => []
    };

    public static string Policy(string runtimeKind) => runtimeKind switch
    {
        "button" => "Checked changed applies to toggle buttons. Value change precedes click.",
        "checkbox" or "radio" or "picker" or "wheelPicker" => "Typed user events; programmatic setters are silent.",
        "dropDown" => "Selection changed is tentative while open. Select or a row tap commits; Back, focus loss or an outside press cancels and restores the initial index. Programmatic setters are silent.",
        "list" => "Up/down and touch select; Select activates. Items are borrowed, and programmatic replacement is silent. The application maps an index to its stable item identity.",
        "textField" => "Text changed is immediate. Commit/cancel require caller-owned textEditBuffer storage. Select commits; Back or focus loss cancels. Programmatic setText is silent.",
        "table" => "Scrolling container; no row selection or cell editing contract yet.",
        "tabBar" or "tabView" or "softkey" => "Existing numeric actions remain supported; named events are not yet exposed.",
        "scroll" => "Scrolling is internal view behavior; no application scroll event.",
        _ => "No application interaction events."
    };
}
