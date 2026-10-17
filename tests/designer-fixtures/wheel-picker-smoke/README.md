# Wheel picker fixture

This checked designer project demonstrates the Phase 5.7 wheel picker slice on the 480 by 272 H750
RGB565 surface. One form, **Alarm Time**, uses the widget described in
[WHEEL_PICKER.md](../../../docs/WHEEL_PICKER.md):

| Component | Widget | Runtime ID | Contents |
| --- | --- | ---: | --- |
| `titleLabel` | Label | none | `ALARM TIME` |
| `hourWheel` | Wheel Picker, `00` through `23`, wraps, 28-pixel rows | 40 | Starts at `07` |
| `minuteWheel` | Wheel Picker, `00` through `55` in steps of 5, wraps | 41 | Starts at `30` |
| `repeatWheel` | Wheel Picker, `ONCE`, `DAILY`, `WEEKDAYS`, `WEEKENDS`, no wrap, 32-pixel rows | 42 | Starts at `DAILY` |
| `hintLabel` | Label | none | Encoder hint |
| `saveButton` | Button | 43 | `SAVE` |

The wheels use theme tokens for their normal, accent, focused, pressed, and selected colors and a
text scale of 2. The form XML is the editable artifact; `generated/alarm_time.hpp` and
`generated/alarm_time.cpp` are deterministic exports that must stay fresh.

## Edit in VS Code

Open `forms/main.litheview.xml` with the LiTheView designer. Select a wheel to edit **Items**,
**Selected Index**, **Wrap**, or **Row Height**; the native preview shows the selected item in the
band. Click **Wheel Picker** in the palette to add another wheel with starter items.

## Validate and export

From the repository root, using the .NET 10 SDK selected by the VS Code designer tasks:

```powershell
$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form validate `
    tests/designer-fixtures/wheel-picker-smoke/forms/main.litheview.xml `
    schema/widgets.json

& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form export-cpp `
    tests/designer-fixtures/wheel-picker-smoke/forms/main.litheview.xml `
    schema/widgets.json `
    tests/designer-fixtures/wheel-picker-smoke/generated/alarm_time.hpp `
    tests/designer-fixtures/wheel-picker-smoke/generated/alarm_time.cpp
```

The designer's freshness test and the host equivalence test fail if the form and generated files
drift apart.

## Use the generated form

The form needs no resources:

```cpp
// Form::Layout builds only the widget kinds this form uses.
lithe::generated::AlarmTime::Layout layout{};
const lithe::LayoutResult built = layout.build(lithe::generated::AlarmTime::views());
```

In `onAction()`, runtime ID 40, 41, or 42 means a wheel value changed; read `selectedIndex()` or
`selectedItem()` from `layout.find<lithe::WheelPickerView>(id)`. Runtime ID 43 is the Save button.

## Input

With the controller active, focus starts on the hour wheel. `Key::down` or one encoder step moves
focus to the minute wheel, the repeat wheel, and the Save button. `Key::select` or an encoder push
starts editing the focused wheel; rotation then changes its value and reports each step, `Key::back`
restores the value from before editing, and another push confirms it. On a touch screen, drag a wheel
and release to snap to an item, or tap the row you want. The repeat wheel stops at `ONCE` and
`WEEKENDS` because Wrap is off.

This fixture is host and cross-build evidence only. It does not select a target, storage section,
or board, and no hardware is required.
