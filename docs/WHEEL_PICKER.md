# Wheel picker

This guide describes the fourth Phase 5.7 vertical slice, the touch and encoder scroll wheel, for a
developer who has not used it before. It covers the runtime C++ API, XML, the VS Code designer,
generated C++, input, rendering, memory, and the checked example. Theme tokens and the common state
resolver are described in [THEMES_AND_WIDGET_STATES.md](THEMES_AND_WIDGET_STATES.md).

## What it is

| Widget | `ViewKind` | C++ type | Children | Use it when |
| --- | --- | --- | --- | --- |
| Wheel Picker | `wheelPicker` | `lithe::WheelPickerView` | none | One value from a list that is too long for a picker or tab bar: hours, minutes, a channel, a recipe. |

The wheel shows its items in a vertical column with the selected item centered in a highlighted
band of one row. Neighbouring items fade with distance. Items are one pipe-delimited string in
caller-owned immutable storage, the same convention as picker items and tab titles
(`"00|05|10|...|55"`). A wheel holds 1 through 255 items, and each row is `rowHeight` pixels high
(default 28).

`PickerView` remains the compact choice for a few options on one line. A wheel is taller and shows
the neighbouring values, so it suits touch screens and encoder-only products alike.

## Input

The wheel separates *focus* from *editing*, so an encoder can still move between controls:

| Input | Result |
| --- | --- |
| `Key::up` / `Key::down`, encoder rotation, while focused but not editing | Not consumed: focus moves to the previous or next control as usual. |
| `Key::select`, encoder push, or push button | Starts editing (the band shows the pressed colors and two edit bars). Pressing again ends editing and keeps the value. Neither reports an action. |
| `Key::up` / `Key::left`, or rotation, while editing | Selects the previous item and reports the widget's action once per step. |
| `Key::down` / `Key::right`, or rotation, while editing | Selects the next item and reports the action once per step. |
| `Key::back` while editing | Restores the value from before editing, ends editing, and reports the action if the value changed. |
| Focus leaves the wheel while editing | Editing ends and the value chosen so far is kept. A wheel that becomes disabled or unfocusable does the same. |
| Pointer drag | The column follows the contact. On release it snaps to the nearest item and reports the action, even if the contact lifts outside the wheel. |
| Pointer tap (travel of 6 pixels or less) | Selects the row under the release: a tap on the row above the band steps back one item, two rows above steps back two. A tap that lifts outside the wheel changes nothing. |
| Cancellation | Any drag offset is discarded; the selection is unchanged. |

With `wrap` true (the default) the wheel steps and drags from the last item to the first and back.
With `wrap` false it stops at either end: the key is consumed without an action, and a drag cannot
pull the first or last item past the band.

Key-driven changes use the same one-bit action request as the tab bar: the wheel sets
`View::setActionRequested(true)` for a key that changed the value, and `ViewController::handleKey()`
calls `ControllerDelegate::onAction()` with the wheel's `actionId()`. A pointer release that lifts
outside the wheel after a drag that changed the value also reports the action; this is a small,
general controller change, and widgets that never request an action are unaffected. As for any
focusable view, a release inside the wheel reports the action even when the value did not change, so
compare `selectedIndex()` if that matters.

Inside `onAction()` the selection already holds the new value:

```cpp
void onAction(lithe::ViewController& controller, lithe::ViewId action) noexcept override
{
    if (action == kHourAction)
    {
        if (const auto* hour = mLayout.find<lithe::WheelPickerView>(kHourAction))
        {
            mAlarm.setHour(hour->selectedIndex());
        }
    }
}
```

## Rendering and state colors

The wheel fills its frame with the normal (or disabled) background and draws the band across the
center row. The band resolves its state with the common precedence disabled, pressed, focused,
selected, normal: it is always "selected", it is "focused" while the wheel has focus, and it is
"pressed" while a contact is held or while editing. Editing also draws a 3-pixel bar at each end of
the band, so editing never depends on color alone. The band has 1-pixel rules above and below in the
accent color.

The selected item uses the band's foreground. Items above and below use the normal foreground with
alpha 176 for the nearest rows and 96 beyond, blended over the normal background. Text is centered
with a 6-pixel horizontal inset and uses the widget's font and text scale. A nonzero border is
stroked around the frame. Rows are clipped to the frame, so a partly visible row at the top or
bottom is normal.

## Runtime C++

```cpp
static constexpr std::string_view kHours{
    "00|01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|16|17|18|19|20|21|22|23"};
lithe::WheelPickerView hour{{16, 44, 128, 168}, font, kHours, 7U};
lithe::WheelPickerView repeat{{304, 44, 160, 168}, font, "ONCE|DAILY|WEEKDAYS|WEEKENDS", 1U,
                              32U /* rowHeight */, false /* wrap */};
hour.setId(kHourAction);
hour.setSelectedIndex(8U);   // clamps to the last item; returns whether the selection changed
repeat.selectNext();         // stops at the last item because wrap is false
std::string_view value = hour.selectedItem(); // "08"
```

Items beyond the 255th are ignored at run time; strict XML and the designer reject them. A row
height of 0 falls back to the default. `editing()` reports whether the wheel is in editing mode.

## XML

```xml
<wheelpicker frame="16,44,128,168" runtimeID="40" items="00|01|02|...|23" selectedIndex="7"/>
<wheelpicker frame="304,44,160,168" runtimeID="42" items="ONCE|DAILY|WEEKDAYS|WEEKENDS"
             selectedIndex="1" wrap="0" rowHeight="32"
             selectedBackgroundColor="0x00FAE4D6" selectedForegroundColor="0x00803D1A"/>
```

The element is `wheelpicker` (alias `wheel-picker`); historical object markup uses
`LCDWHEELPICKERVIEW`. `items`, `selectedIndex`, and `wrap` are the existing picker attributes.
`rowHeight` accepts 12 through 255 pixels (default 28) in both parsing modes. Strict XML requires
1 through 255 items and a `selectedIndex` that names one of them, and rejects a non-default
`rowHeight` on any other widget; compatibility XML clamps an out-of-range selection.
`XmlLayoutWriter` writes all four attributes.

## Designer workflow (VS Code)

1. Click **Wheel Picker** (Input) in the palette. A new wheel is 120 by 140 pixels, centered in the
   selected container or the form, and starts with the items `ITEM 1|ITEM 2|ITEM 3|ITEM 4|ITEM 5`.
   The palette now sizes every new widget from its schema default instead of a fixed 100 by 36.
2. Edit **Items**, **Selected Index**, **Wrap**, and **Row Height** in the Properties panel. The native
   preview shows the selected item in the band.
3. Set the background, foreground, accent (band rules), focused, pressed (editing), and selected
   (band) colors, or theme tokens, as for other widgets.
4. Give it a **Runtime ID** or assign **Selection Changed** to an action so the application hears
   about changes.

Validation:

| Code | Severity | Meaning |
| --- | --- | --- |
| `LVFD1136` | error | A wheel picker needs 1 through 255 items, and Selected Index must name one of them. |

Row Height outside 12 through 255 is rejected by the schema range check, as for any bounded property.
The standalone designer validates and exports the same forms but receives no new editors.

## Generated C++

```cpp
{
    ViewSpec& spec = result[4U];
    spec.kind = ViewKind::wheelPicker;
    spec.style.states = &kStateStyles[4U];
    spec.parent = 0U;
    spec.style.accent = Color::fromLegacyValue(0x00df6c2dU);
    spec.style.background = Color::fromLegacyValue(0x00402818U);
    spec.style.foreground = Color::fromLegacyValue(0x00ffffffU);
    spec.frame = {304, 44, 160, 168};
    spec.text = "ONCE|DAILY|WEEKDAYS|WEEKENDS";
    spec.rowHeight = 32U;
    spec.id = 42U;
    spec.selectedIndex = 1U;
    spec.style.textScale = 2U;
    spec.wraps = false;
}
```

`ViewSpec::rowHeight` is one byte in padding that already existed after `buttonBehavior`, so
`ViewSpec` stays 120 bytes.

## Memory and ownership

| Object (32-bit Cortex-M) | Size | Ceiling |
| --- | ---: | ---: |
| `View` | 80 (unchanged; `focusChanged()` is a virtual function, not a member) | 80 |
| `WheelPickerView` | 108 | 112 |
| `ViewSpec` | 120 (unchanged) | 124 |
| `StaticLayout<16>` | 1,816 (unchanged; `AnimatedImageView` is still the largest member) | 1,900 |

Items are borrowed; the wheel owns no strings, timers, or heap storage. Item count, selection, the
value saved when editing started, and the row height are bytes; the drag offset and press position
are two 32-bit values. Parsing, layout, input, and rendering allocate nothing, and drawing visits only
the rows that can be visible. An all-kinds `StaticLayout` links the widget (about 2.1 KB of
Cortex-M33 text in a linked image); a layout whose `WidgetSet` omits it, such as the `Layout` of a
generated form without a wheel, links none of it. The core archive grows by about 2.6 KB; measured
sizes are recorded in [AGENT_HANDOFF.md](AGENT_HANDOFF.md).

`View` gains a protected `virtual void focusChanged() noexcept`, called after a view gains or loses
focus (including through `setEnabled(false)` and `setFocusable(false)`). The default does nothing;
the wheel uses it to end editing.

## Example and tests

[`tests/designer-fixtures/wheel-picker-smoke`](../tests/designer-fixtures/wheel-picker-smoke/README.md) is a
checked designer project, **Alarm Time**, with hour and minute wheels, a non-wrapping repeat wheel
with 32-pixel rows, a hint label, and a Save button. Host tests cover strict and compatibility XML,
the writer round trip, focus traversal past a wheel, editing with select/push, steps and their
actions, back to cancel, wrapping and clamped ends, focus loss ending editing, taps, drags that snap
(including one that lifts outside), cancellation, a clamped drag that is not a tap, disabled wheels,
RGB565 and XRGB8888 goldens (`0x784208d4`, `0xcc7c5099`) with tile equality, and XML-versus-generated
equivalence. Native preview tests drive focus, push buttons, taps, and encoder steps through the
real renderer.

## Limits and follow-ups

- There is no inertia (fling) or animated snap; the column snaps on release.
- Items are text only; there is no icon column or per-item disabled state.
- A wheel is one column. Multi-column pickers (date, time) are separate wheels side by side, as in
  the example.
- The WinForms canvas draws the wheel through the native preview only; it receives no new editors.
