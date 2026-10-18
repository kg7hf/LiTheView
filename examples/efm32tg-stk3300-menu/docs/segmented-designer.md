# Describe the glass, then bind the menu values

The Tiny Gecko has fixed electrodes. We can turn each one on or off, but we cannot
move a button or draw arbitrary text on the glass. The designer needs that physical
description before it can show us the fields we can use.

This lesson uses the VS Code plug-in, version 0.2.15 or later, with its matching
service and native worker. Windows and the Tiny Gecko now consume the generated
encoder through the same handwritten presenter. The application-owned FSM in
`Application/menu.hpp` stays unchanged.

## Open the example

Prepare and install the current extension using the
[plug-in setup instructions](../../../../tools/vscode/README.md).
Open `examples/efm32tg-stk3300-menu` as the VS Code application folder, then open
`UI/designer/forms/home.litheview.xml` with **LiTheView Designer**.

The form shows PLAY, numeric zero and four pieces of the ring. The palette contains
glass fields, not raster widgets. Click `volumeRing` to see its application input
name, `volumePercent`, and its preview value, 50.

The source layout is:

```text
Application/menu.hpp                        # Your FSM
UI/presenter.hpp                            # State -> presentation values
UI/designer/project.litheview.json           # One project, multiple screens
UI/designer/project.litheview.paths.json     # Application/library/output paths
UI/designer/profiles/glass.segment-display.json
UI/designer/profiles/logical-input.json
UI/designer/forms/home.litheview.xml
UI/Generated/segmented/home.hpp              # Written by Generate UI
Platform/Win32/                              # Host drawing and input
Platform/EFM32/                              # LCD registers and board input
```

The `UI/design` files from the first compiler exercise remain independent comparison
inputs. The designer project owns a locked snapshot of that glass under `profiles`.
Editing one copy does not silently change the other.

LiTheView is a dependency of your application. In your own layout it might be
`Middlewares/Third_Party/LiTheView`, `externals/LiTheView` or another location. The
paths sidecar selects that location and the generated output directory. This
example points back to the contributor checkout; it does not require your
application to live inside LiTheView.

## Change a field

1. Select `volumeRing` under **Glass fields**.
2. Keep the input name `volumePercent`. Change **Preview value** to 75.
3. Click **Apply field**. Six ring pieces should be on.
4. Save, then click **Generate UI**. Both the field edit and ordinary source edits
   use VS Code's document and undo history.

The generated `Values::field_volumePercent` is a bounded numeric input. The sample
75 is for the editor; it is not an initial volume or a new FSM state in your program.
Your presenter supplies the saved or draft volume when it calls `encode()`.

Select `label` to change its preview text. Only the glass's declared glyphs fit;
unsupported characters or more than seven characters are rejected. `value` holds
an unsigned decimal number from 0 through 9999.

To control pieces separately, clear the input name for `volumeRing` and apply that
change. Select `ringPiece0`, give it an input name such as `topPiece`, set its
Boolean preview to `true`, and apply. Repeat for other pieces. You cannot bind
the aggregate ring and a piece of that ring on the same form: that would create
two writers for one electrode.

## Add another screen

Click **Add screen** and enter `volume`. The new form starts with all electrodes
off. Bind `label` to `label` with preview `VOLUME`, `value` to `value` with preview
75, and `volumeRing` to `volumePercent` with preview 75.

Both forms appear in the screen navigator. They share one glass and one input
definition. Save your forms and click **Generate UI** to generate `home.hpp` and
`volume.hpp`. There is no task per form and no automatically generated navigation.
The presenter chooses an encoder according to the application's FSM state.

Passive fields have no click handlers. PB0, PB1 and the slider remain devices that
send logical events to the FSM. A generated icon is a Boolean presentation value;
it does not read a GPIO, own a timer or change the application state.

## Configure the glass and input

Click **Configure**. The segmented dialog shows the glass JSON and separate input
capabilities. You can edit that description or use **Import glass JSON** to load
your own definition. The imported file is read as data; it is not executed.

For this board, keys, encoder and select describe the logical button/slider input
available to the application. Configure does not assign GPIOs or calibrate the
capacitive slider. Those operations remain in the board adapter.

Apply validates every screen before proposing changes. A valid change updates the
glass/input snapshots, the project locks and each form's glass lock through one
VS Code document edit. Dirty documents or stale snapshots prevent Apply. Removing
a field used by an existing screen also fails; first remove or change that binding.
No incompatible form is silently rewritten.

The host viewport describes drawing geometry. It is not target framebuffer RAM.
For this glass, the encoded frame is 160 bits, or 20 bytes. Native preview first
encodes those bits, then fills the fixed polygons for the host canvas. The preview
worker supports viewports up to 1024 by 1024 and bounds polygon work; larger or
overly complex geometry reports a preview error rather than an unbounded render.

## Start a custom segmented project

Initial segmented project creation currently uses the CLI. The New Project wizard
and standalone desktop designer have not gained a segmented creation page.
Configure, Add screen, field editing and generation work in the VS Code plug-in
after the project exists.

From your LiTheView dependency folder, build the CLI:

```powershell
dotnet build tools/designer/src/LitheView.Designer.Cli -m:1
$Cli = 'tools/designer/src/LitheView.Designer.Cli/bin/Debug/net10.0/LitheView.Designer.Cli.dll'
dotnet $Cli segment create-project path/to/my-glass.json path/to/my-input.json path/to/new-ui-folder
```

Use a **new** output folder. Creation will not overwrite an existing folder. The
glass follows [segment-glass.schema.json](../../../../schema/segment-glass.schema.json);
input follows [logical-input-configuration.schema.json](../../../../schema/logical-input-configuration.schema.json).
Start with a small glass: a few individually addressable icons are enough.

Beside the new manifest, create `project.litheview.paths.json`. For a UI project at
`MyApplication/UI` and the library at `MyApplication/Middlewares/Third_Party/LiTheView`:

```json
{
  "schemaVersion": 1,
  "applicationRoot": "..",
  "libraryRoot": "Middlewares/Third_Party/LiTheView",
  "componentDirectory": "UI/Generated",
  "buildDirectory": "build/ui"
}
```

Open your application root in VS Code and open `UI/forms/home.litheview.xml`.
The form starts blank. Bind only the fields you need. Generate creates one owned
C++ header per screen; handwritten files are never adopted as generated output.
No UI callback, menu FSM, board mapping, build preset or launch configuration is
created by this step.

## Connect the generated values to the presenter

Open `UI/presenter.hpp`. `present(menu)` decides the label, number and saved or
draft volume from application state. `encodePresentation(view)` passes those
values to the generated encoder:

```cpp
return lithe_generated::screen_home::encode({
    .field_label = view.label,
    .field_value = view.value,
    .field_volumePercent = view.volumePercent});
```

The names come from the form's application input names. If you rename or remove
one, update this adapter; the compiler catches a missing field. The sample value
shown in the designer never replaces a value supplied by your application.

One form is enough here because Home, Volume, Setup and Info use the same three
physical fields. The FSM changes their values. Add a separate form when a screen
needs different bindings; choose its generated encoder in the presenter, without
moving menu transitions into generated code.

Both platform mains call `present(menu)` and `encodePresentation(view)`. Windows
draws the resulting bits. The hardware adapter maps them to LCD registers. A failed
encode retains the previous valid display; the generated encoder never publishes
a partially updated frame. The old handwritten encoder is in
`Tests/reference_encoder.hpp` for comparison only.

## Generate, build Windows, then build hardware

1. Edit the bindings or Configure values, save, and click **Generate UI**.
2. Select `win32-debug`, build and run `ctest --preset win32-debug`.
3. Start **Tiny Gecko: Windows** with F5. Try PLAY/PAUSE, browse with the slider,
   edit VOLUME, cancel with PB1 and save with PB0. Check the number and ring together.
4. Select `efm32-debug`, build and use **Tiny Gecko: EFM32 (J-Link)** to load
   the board. Leave the slider untouched while startup calibration runs.
5. Repeat the same input checks on the physical kit. Record physical observations
   separately from test results and debugger counters.

The checked-in generated header lets another developer build without installing
.NET or the designer. CMake checks LF-normalized hashes of the form, manifest,
glass and input snapshots before compiling. A changed source without Generate UI
fails with a regeneration message. CRLF/LF checkout differences do not cause a
false stale-output error. Generation remains an explicit authoring step, not a
hidden code generator running inside every firmware build.

In this contributor checkout, the explicit application root is the Tiny Gecko
example. Generation can write inside that example, but cannot write into another
example or the library's `src` or `include` folders. An external application with
LiTheView as a dependency continues to use its own configured output paths.

The generated build uses 7,740 bytes flash and 64 bytes static RAM, with a 1,024-byte
stack reservation. This is 700 bytes more flash than the handwritten baseline.
The measured startup stack watermark is 400 bytes; it is not a worst-case bound.
See [validation](../VALIDATION.md) for exact tests, firmware identity and current
physical acceptance status.
