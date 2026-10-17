# Tab view fixture

This checked designer project demonstrates the Phase 5.7 tab slice on the 480 by 272 H750 RGB565
surface. One form, **Tab Settings**, contains both widgets described in [TABS.md](../../../docs/TABS.md):

| Component | Widget | Runtime ID | Contents |
| --- | --- | ---: | --- |
| `settingsTabs` | Tab View, tabs `DISPLAY`, `NETWORK`, `ABOUT`, 32-pixel strip | 20 | One panel page per tab |
| `displayPage` | Panel (page 0) | none | Brightness label, picker (21), Auto Dim checkbox (22) |
| `networkPage` | Panel (page 1) | none | Address label, Connect button (23) |
| `aboutPage` | Panel (page 2) | none | Version label |
| `modeBar` | Tab Bar, tabs `AUTO`, `MANUAL`, `SERVICE`, no wrap | 30 | none |

Both tab widgets use theme tokens for their normal, focused, pressed, and selected colors. The form
XML is the editable artifact; `generated/tab_settings.hpp` and `generated/tab_settings.cpp` are
deterministic exports that must stay fresh. The form is named "Tab Settings" so that the generated
`lithe::generated::TabSettings` is not confused with the `lithe::TabView` widget.

## Edit in VS Code

Open `forms/main.litheview.xml` with the LiTheView designer. Select `settingsTabs` and change
**Selected Index** to edit another page; the other pages are marked "hidden page" in the hierarchy.
To add a page, select `settingsTabs`, click **Panel** in the palette, and add a title to **Items**.

## Validate and export

From the repository root, using the .NET 10 SDK selected by the VS Code designer tasks:

```powershell
$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form validate `
    tests/designer-fixtures/tab-view-smoke/forms/main.litheview.xml `
    schema/widgets.json

& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form export-cpp `
    tests/designer-fixtures/tab-view-smoke/forms/main.litheview.xml `
    schema/widgets.json `
    tests/designer-fixtures/tab-view-smoke/generated/tab_settings.hpp `
    tests/designer-fixtures/tab-view-smoke/generated/tab_settings.cpp
```

The designer's freshness test and the host equivalence test fail if the form and generated files
drift apart.

## Use the generated form

The form needs no resources:

```cpp
// Form::Layout builds only the widget kinds this form uses.
lithe::generated::TabSettings::Layout layout{};
const lithe::LayoutResult built = layout.build(lithe::generated::TabSettings::views());
```

`build()` shows the Display page and hides the other two. In `onAction()`, runtime ID 20 or 30
means a tab was chosen; read `selectedIndex()` from `layout.find<lithe::TabView>(20U)` or
`layout.find<lithe::TabBarView>(30U)`.

## Input

With the controller active, focus starts on the tab view. `Key::right`/`Key::left` switch pages;
`Key::select` or an encoder push advances and wraps. `Key::down` or one encoder step moves focus to
the controls on the visible page and then to the mode bar, where left/right stop at the first and
last tab because Wrap is off. A tap selects the tab under the release.

This fixture is host and cross-build evidence only. It does not select a target, storage section,
or board, and no hardware is required.
