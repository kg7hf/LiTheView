# Theme colors and widget states

This guide explains the Phase 5.7 theme/state vertical slices from the point of view of a
developer embedding LitheView in a new product. The first slice added color tokens and the common
state resolver; the second added the retained `selected` state used by toggle buttons, checkboxes,
and radio buttons (see [BUTTONS.md](BUTTONS.md)); the third resolves the same states per tab, so the
selected tab of a tab bar or tab view uses the selected colors and the tab under a held contact uses
the pressed colors (see [TABS.md](TABS.md)); the fourth draws a wheel picker's band with the
selected colors, the focused colors while it has focus, and the pressed colors while it is dragged or
being edited (see [WHEEL_PICKER.md](WHEEL_PICKER.md)). The feature is deliberately small: a form may name a
bounded set of colors, focusable widgets share one deterministic state policy, and generated output
still consists of ordinary static C++ and strict XML. There is no theme engine, string lookup, heap,
clock, driver, or desktop dependency in the embedded runtime.

## Mental model

A theme color is a design-time name for one encoded color, for example `theme.primary`. A widget
property may contain that name instead of repeating a literal. During validation and export, the
designer resolves the name to the concrete color expected by `lithe::Style`.

A widget state selects a background and foreground pair. The common precedence is fixed:

1. disabled;
2. pressed;
3. focused;
4. selected; and
5. normal.

This means a disabled button cannot accidentally retain a pressed or focused presentation. Focus
ranks above selection so keyboard and encoder users can always find the focused control; a selected
widget therefore also shows its value without relying on color alone (a toggle's latch bar, a check
mark, or a radio dot). The runtime stores four bits already required by the retained view (`enabled`,
`focused`, `pressed`, and `selected`) and resolves the state in constant time. It does not allocate or
search a token table.

`selected` is a value, not an interaction state. Pointer cancellation, focus loss, hiding, and
disabling clear `pressed` or `focused` but never change it; only the widget's own activation or an
explicit API call does.

## Designer workflow

1. Open a project and choose **Resources > Edit Theme Color Tokens** or press `Ctrl+Alt+T`.
2. Add a stable `theme.*` ID and an eight-digit legacy encoded color, or add the standard starter set.
3. Select a focusable component on the canvas.
4. In Properties, choose a token or enter a literal for the normal, focused, pressed, disabled, or
   selected background/foreground slot. Buttons, checkboxes, and radio buttons expose the selected
   pair.
5. Save and preview. Reopening the form retains the token selection rather than replacing it with an
   anonymous literal.

Forms accept at most 32 theme colors. IDs are ordinal, case-sensitive, stable identifiers and must
start with `theme.`. The standard starter set is only a useful default; a product may replace every
name and color. Theme edits are one undoable designer transaction.

## Runtime C++

Handwritten code does not need to opt into a theme system. Existing styles retain their original
meaning:

```cpp
lithe::Style style{};
style.background = lithe::Color::rgb(0x101820U);
style.foreground = lithe::Color::rgb(0xf4f7ffU);
style.accent = lithe::Color::rgb(0x2d6cdfU);

lithe::ButtonView button{{8, 8, 120, 32}, font, "OPEN", {}, style};
```

With no explicit state colors, focused and pressed use `accent` with white content, disabled uses
the established neutral disabled pair, and selected keeps the normal pair. That compatibility rule
is important for application styles created before Phase 5.7: a checked checkbox or radio button
renders exactly as before unless selected colors are supplied.

State overrides live in a separate caller-owned table, so panels, labels, and widgets that use only
the compatibility policy do not carry six colors apiece. Set only the states that should differ and
borrow that table from the style:

```cpp
lithe::WidgetStateStyle states{};
states.focusedBackground = lithe::Color::rgb(0x2d6cdfU);
states.focusedForeground = lithe::Color::rgb(0xffffffU);
states.pressedBackground = lithe::Color::rgb(0x2457b3U);
states.pressedForeground = lithe::Color::rgb(0xffffffU);
states.disabledBackground = lithe::Color::rgb(0xd0d4dcU);
states.disabledForeground = lithe::Color::rgb(0x6a6f78U);
states.selectedBackground = lithe::Color::rgb(0xd6e4faU);
states.selectedForeground = lithe::Color::rgb(0x1a3d80U);
style.states = &states;
```

`states` must outlive `style` and every view constructed from it. Generated forms emit a static
`WidgetStateStyle` array, so that lifetime is automatic.

Explicit transparent state colors are supported. Internally, one otherwise transparent chroma value
(`0xffc35aa5` in the legacy encoding) is reserved to represent an omitted field inside
`WidgetStateStyle`. The strict XML parser and designer reject that one value as explicit input;
transparent black and every visually distinct color remain available. Borrowing the optional table,
packing view flags, and bounding parent indexes preserve the existing Cortex-M `View`, `ViewSpec`,
`PickerView`, and `StaticLayout<16>` RAM ceilings instead of adding 32 bytes to every parsed or
constructed widget. The selected state reuses a spare `View` flag bit, so `View` stays at 80 bytes on
a 32-bit target; each `WidgetStateStyle` entry is 32 bytes and exists only for views that override a
state color.

`resolveWidgetState()` and `resolveWidgetStyle()` are public, constexpr helpers. Custom widgets should
call the protected `View::resolvedStyle()` while drawing instead of copying state precedence:

```cpp
void StatusButton::drawSelf(lithe::Painter& painter, lithe::Rect frame) noexcept
{
    const lithe::Style current = resolvedStyle();
    painter.fillRect(frame, current.background);
    painter.drawText(frame, text(), font(), current.foreground, current.textAlign, current.textScale);
}
```

Pointer press, move, release, cancellation, visibility, and disable transitions clear or update the
common pressed state. A transition invalidates only when its resolved colors change. Consequently, a
legacy button whose pressed and focused colors are identical does not cause a redundant display
present, while an explicitly different pressed token produces visible feedback.

## XML and generated C++

Runtime XML contains concrete values, so parsing does not require the desktop theme table:

```xml
<object className="LCDBUTTONVIEW"
        frame="8,8,120,32"
        focusedBackgroundColor="0x00DF6C2D"
        pressedBackgroundColor="0x00B35724"
        disabledForegroundColor="0x00786F6A"
        text="OPEN"/>
<button frame="8,48,120,32" text="ALARM" behavior="toggle" checked="1"
        selectedBackgroundColor="0x00FAE4D6" selectedForegroundColor="0x00803D1A"/>
```

Canonical designer XML also writes bounded `litheview-theme-reference` comments. Each comment binds a
component/property pair back to its stable token. Native parsers ignore the comments; the designer
validates them against the concrete attribute and restores the token selection when reopening the
form. Missing, duplicate, mismatched, or unbound metadata fails closed instead of silently changing
the authored model.

Generated C++ assigns the resolved colors to a static per-view `WidgetStateStyle` array and makes the
corresponding `ViewSpec::style` borrow its entry. Each overridden entry is built as a local
`WidgetStateStyle state{}` and copied into the table. Writing a sparse entry through a reference
into a value-initialized table is avoided deliberately: GCC 13 constant evaluation (host 13.3 and
Arm 13.2 were checked) zero-fills the omitted members of an element reached after untouched
predecessors, which turns every omitted state color into explicit transparent black. Re-export any
form generated before this change; the checked fixtures and their equivalence tests already use
the corrected output. Theme names do not enter the target binary unless the
application separately chooses to keep them. XML parsing and generated C++ therefore render
equivalent state styles.

When parsing XML that contains state-color attributes, supply one caller-owned `WidgetStateStyle`
entry per possible `ViewSpec` through `XmlParseOptions`. The parser binds a style only when that node
actually contains an override and fails with `stateStyleCapacity` if required storage is absent. The
XML text, `ViewSpec` array, state-style array, and referenced resources must all outlive the built
layout.

## Ownership, limits, and extension guidance

- The form owns design-time token names and encoded values.
- `Style` owns normal colors by value and optionally borrows one immutable-in-use state-style entry;
  it never borrows or searches the design-time theme-name table.
- The caller still owns fonts, bitmaps, views, storage, input delivery, and the display.
- State resolution is constant-time and has no failure path after a valid style is constructed.
- The designer never infers a board, bus, RTOS, internal/external flash placement, or transfer policy
  from a theme.
- A custom focusable widget should expose the same six optional state-color properties through its
  schema and use `resolvedStyle()` in its renderer.
- Typography, spacing, radius, and motion tokens are later bounded theme slices. They must follow the
  same rule: resolve at construction/export or through fixed borrowed data, never through an
  allocation-backed runtime string map.

When adding a new state, first define its precedence and cancellation behavior, then update runtime,
schema, XML reader/writer, generated C++, native preview, designer properties, examples, goldens, and
U545 budgets together. A state that exists in only one path is not a completed widget feature.
