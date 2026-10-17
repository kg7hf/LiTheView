# Buttons: text, icon, momentary, and toggle

This guide describes the second Phase 5.7 vertical slice for a developer who has not used LitheView
buttons before. It covers the runtime C++ API, XML, the designer, generated C++, input, rendering,
memory, and the checked example. Theme tokens and the common state resolver are described in
[THEMES_AND_WIDGET_STATES.md](THEMES_AND_WIDGET_STATES.md).

## One widget, two independent choices

All four variants are the same retained `lithe::ButtonView` and the same `ViewKind::button`. A
button makes two independent choices:

| Choice | Values | Meaning |
| --- | --- | --- |
| Content | text, icon, or icon plus text | What is drawn inside the frame. |
| Behavior | `momentary` (default) or `toggle` | Whether activation latches a value. |

| Variant | How to get it | Result |
| --- | --- | --- |
| Text button | `text`, optional `detail` | Text at the left inset, detail right-aligned. Unchanged from earlier releases. |
| Icon button | `icon`, no `text` or `detail` | The icon centered in the frame. |
| Icon and text | `icon` and `text` | Icon at the left inset, text 6 pixels after it. |
| Momentary button | `behavior="momentary"` or omitted | Non-latching. Shows the pressed state while a contact is held and reports one action per completed activation. |
| Toggle button | `behavior="toggle"` | Latching. Each activation flips the shared `selected` state before the action is reported, and a latch bar shows the value. |

One widget keeps the existing one-schema-widget-per-`ViewKind` contract, keeps one `StaticLayout`
union member, and lets any content combine with either behavior. The designer palette therefore
has one **Button** entry; Behavior, Checked, and Icon are ordinary properties.

"Momentary" here is the switch-gear sense of non-latching, matching the earlier catalog wording
for this button. It is not a hold-to-run control: actions are reported when an activation completes,
and keys and push buttons have no release event in the portable input contract. Do not drive
motion or other safety-relevant actuation from how long a button is held.

## Input

Every variant uses the existing controller rules. No new input path was added.

| Input | Momentary | Toggle |
| --- | --- | --- |
| Pointer press inside | Focuses the button and shows the pressed state. | Same; the value does not change yet. |
| Pointer release inside | Reports the action. | Flips `selected()`, then reports the action. |
| Moving outside, release outside, or cancellation | Clears pressed; no action. | Clears pressed; the value is unchanged. |
| `Key::select`, encoder push, or push button | Reports the focused button's action. | Flips the focused toggle, then reports the action. |
| Arrow keys or encoder rotation | Move focus. | Move focus. |

Inside `ControllerDelegate::onAction()`, a toggle's `selected()` already holds the new value:

```cpp
void onAction(lithe::ViewController& controller, lithe::ViewId action) noexcept override
{
    if (action == kAlarmAction)
    {
        if (const auto* alarm = mLayout.find<lithe::ButtonView>(kAlarmAction))
        {
            mModel.setAlarmEnabled(alarm->selected());
        }
    }
}
```

## Rendering and state colors

The resolved state follows the fixed precedence disabled, pressed, focused, selected, normal. A
selected toggle that is not focused uses the optional selected colors; a focused toggle uses the
focus colors. The value never depends on color alone: a toggle draws a 3-pixel latch bar 4 pixels
above its bottom edge, filled while selected and outlined while not, in the resolved foreground.
The bar is omitted when the frame is shorter than 16 pixels or too narrow, and text is centered in
the area above it.

Alpha1 and alpha8 icons are tinted with the resolved foreground, so an icon follows the focused,
pressed, selected, and disabled text color. RGB, ARGB, indexed, and packed icons keep their authored
pixels in every state. A disabled toggle keeps its latched value and still shows its latch bar.

## Runtime C++

```cpp
lithe::ButtonView alarm{{8, 48, 120, 32}, font, "ALARM"};
alarm.setBehavior(lithe::ButtonBehavior::toggle);
const bool latched = alarm.setSelected(true); // true; a momentary button returns false

lithe::ButtonView home{{136, 48, 40, 32}, font};
home.setIcon(kHomeIcon); // borrowed; kHomeIcon must outlive the button
```

`setBehavior(ButtonBehavior::momentary)` clears any latched value. `setIcon()` rejects temporaries
at compile time and `clearIcon()` removes the icon. Checkbox and radio buttons now keep their value
in the same `View` selected bit, so `CheckboxView::checked()` and `RadioButtonView::selected()` keep
their existing meaning and can use the selected colors.

## XML

```xml
<button frame="16,48,208,40" runtimeID="10" text="START" detail="F1"/>
<button frame="240,48,48,40" runtimeID="11" icon="home-icon"/>
<button frame="16,100,208,40" runtimeID="12" text="ALARM" behavior="toggle" checked="true"
        selectedBackgroundColor="0x00FAE4D6" selectedForegroundColor="0x00803D1A"/>
```

`behavior` accepts `momentary` or `toggle`. `icon` names a bitmap resolved by the application's
`ResourceProvider`; an unresolved icon fails layout construction with `missingResource`. Strict XML
rejects `checked` on a momentary button and `behavior` on any other widget. Compatibility XML keeps
importing such historical markup, and the momentary button stays unselected. Selected colors, like
the other state colors, need a caller-owned `WidgetStateStyle` entry through `XmlParseOptions`. The
C++ `XmlLayoutWriter` writes `behavior`, `checked`, `icon`, and selected colors back out.

## Designer workflow

In VS Code, add **Button** from the palette, select it, and set **Behavior**, **Checked**,
**Icon**, and the selected colors in the Properties panel; each change is one undo step.
The standalone designer offers the same properties:

1. Double-click **Button** in the toolbox.
2. In **Properties > Behavior**, choose `momentary` or `toggle`. Choose **Checked** for a toggle that
   starts latched. Checking a momentary button is rejected with `LVFD1132`; changing a checked toggle
   to momentary clears Checked in the same undoable edit.
3. Import an icon with **Resources > Import Icon** (`Ctrl+Alt+K`), then choose it in
   **Properties > Resources > Icon**. The dropdown lists the form's declared bitmap resources and
   now also serves the Image widget's bitmap and every widget's font.
4. Choose theme tokens for the selected pair the same way as for the other state colors. The
   starter theme set now includes `theme.selected` and `theme.on-selected`.
5. Save and export. Canonical XML keeps token references; generated C++ materializes the colors.

The canvas shows the authored state, so a checked toggle appears latched. As for Image and Icon
widgets, an icon button's editing preview fails closed with `LVPR1007` because the worker protocol
does not yet transport bitmap payloads. The standalone simulator export compiles the real imported
resources. To exercise input through the real worker without the GUI, add input steps to the CLI:

```powershell
LitheView.Designer.Cli preview render forms/main.litheview.xml surface.json worker.exe frame.raw `
  --input focus --input next --input select --input press:20,60 --input release:20,60
```

Supported steps are `focus`, `next`, `previous`, `select`, `key:<up|down|left|right|select|back>`,
`press|move|release|cancel:<x>,<y>`, and `elapsed:<milliseconds>`, up to 64 per request.

## Generated C++

```cpp
{
    ViewSpec& spec = result[5U];
    spec.kind = ViewKind::button;
    spec.style.states = &kStateStyles[5U];
    spec.parent = 0U;
    spec.style.background = Color::fromLegacyValue(0x00553a28U);
    spec.buttonBehavior = ButtonBehavior::toggle;
    spec.checked = true;
    spec.style.foreground = Color::fromLegacyValue(0x00ffffffU);
    spec.frame = {16, 100, 208, 40};
    spec.id = 12U;
    spec.text = "ALARM";
}
```

Each overridden state-style entry is built as a local `WidgetStateStyle state{}` and copied into
the table; the reason is recorded in the theme guide. Generated C++ and strict XML produce
identical `ViewSpec` fields, state tables, and pixels; the host suite checks this for the example.

## Memory and ownership

| Object (32-bit Cortex-M) | Before | After | Ceiling |
| --- | ---: | ---: | ---: |
| `View` | 80 | 80 | 80 |
| `ButtonView` | 100 | 108 | 112 (largest `StaticLayout` member) |
| `CheckboxView`, `RadioButtonView` | 96 | 92 | none |
| `ViewSpec` | 120 | 120 | 124 |
| `StaticLayout<16>` | 1,816 | 1,816 | 1,900 |
| `WidgetStateStyle` (only for overriding views) | 24 | 32 | 32 |

The icon is a borrowed pointer; the button owns no bitmap, string, timer, or heap storage. Parsing,
layout, input, and rendering allocate nothing. Measured archive sizes for this slice are recorded in
[AGENT_HANDOFF.md](AGENT_HANDOFF.md).

## Example and tests

[`tests/designer-fixtures/button-variants-smoke`](../tests/designer-fixtures/button-variants-smoke/README.md)
is a checked designer project containing text, icon, icon-and-text toggle, latched toggle, and
disabled buttons with theme tokens. Its generated header compiles in the host, Cortex-M33, and
Cortex-M7 gates. Host tests cover strict XML, the writer round trip, layout and icon resolution,
pointer/key/encoder/push-button input, bounded damage, state precedence, icon tinting, RGB565 and
XRGB8888 goldens with tile equality, and XML-versus-generated equivalence. The native preview tests
drive toggles and momentary buttons through the real worker.

## Limits and follow-ups

- No hold-to-run or auto-repeat behavior; both need a key-release input contract first.
- No radio-group or exclusive toggle-group policy; application code decides grouping.
- Icon payloads are not yet sent to the editing preview worker.
- The WinForms canvas and VS Code view render authored state only; interactive state simulation
  uses the CLI input steps above or the exported simulator.
