# LiTheView Designer for VS Code

Open a LiTheView form inside your application workspace. This desktop extension
(Windows, Linux, and macOS) reuses the designer's document engine and native C++ renderer.
The standalone designer remains available for project creation, resource editing
and the broader tool set.

## Prepare and install a local development package

For a new machine, follow [Install tools, build and run LiTheView](../../docs/BUILD_ENVIRONMENT.md)
for installation links, SDK verification and the distinction between the x64 designer tooling and
the current H750 starter's i686 Windows application.

Prerequisites: Windows x64, Linux, or macOS; VS Code, PowerShell 7.2+, .NET 10 runtime.
Building the tooling additionally needs the pinned .NET SDK in `tools/designer/global.json`,
CMake, and a C++23 compiler on PATH: MinGW GCC on Windows (preset `windows-mingw-debug`), or
GCC/Clang with Ninja on Linux and macOS (preset `host-debug`). On Windows the native worker hands
each frame to the designer through named shared memory; on Linux and macOS it returns the frame
inline on its output, which renders identical pixels. The VSIX package script is Windows-only;
on Linux and macOS run the extension from the repository (**Run and Debug**, F5).

From the LiTheView repository:

```powershell
./tools/prepare-vscode-designer.ps1
./tools/package-vscode-designer.ps1
```

If your SDK is installed outside PATH, pass `-Dotnet path/to/dotnet.exe` to the
prepare script. It publishes the service under `build/vscode-designer/service`
and builds the existing native preview worker. The package script creates a local
VSIX in `build/vscode-designer`, including the published service and Windows native
worker/runtime dependencies. No npm dependencies or marketplace publication are
required for this development build.

The VSIX includes the third-party notices and upstream license texts in `extension/licenses`.
Packaging checks their recorded hashes and the three native runtime DLL hashes. A compiler from a
different distribution requires a reviewed `-RuntimeManifest`; see
[license provenance](../../licenses/README.md). It does not silently reuse another compiler's notices.
The checked compiler/runtime distribution is [WinLibs GCC 14.2.0 / MinGW-w64 12.0.0](https://github.com/brechtsanders/winlibs_mingw/releases/tag/14.2.0posix-19.1.1-12.0.0-ucrt-r2).
Its [GCC](https://github.com/gcc-mirror/gcc/tree/releases/gcc-14.2.0),
[MinGW-w64](https://github.com/mingw-w64/mingw-w64/tree/v12.0.0) and
[WinLibs recipe](https://github.com/brechtsanders/winlibs_recipes) sources remain upstream;
LiTheView includes notices and provenance links rather than maintaining copies of those sources.
`-ForRedistribution` records packaging intent; it does not certify licensing compliance.

To create an optional source-only extension that uses locally prepared tools, run:

```powershell
./tools/package-vscode-designer.ps1 -SourceOnly -ForRedistribution
```

The resulting `litheview-designer-<version>-source.vsix` contains readable extension source and
notices, with no runtime binaries. Packaging it needs PowerShell 7.2+, but no prepared tools or
compiler. Before using that installed extension, run `tools/prepare-vscode-designer.ps1` in your
application's LiTheView checkout with the tools listed above. The extension finds those local
tools using its existing fallback; CMake still builds your simulator and firmware. The source VSIX
is platform-neutral.
Packaging creates files only; it does not publish to the marketplace.

Use VS Code's **Extensions: Install from VSIX...** to install the resulting file.
Open your **application root**, not the LiTheView submodule. The application's
LiTheView checkout still supplies its widget catalog, profiles and Core sources.
The installed extension uses its matching bundled tooling. A source extension
falls back to prepared tools; explicit settings can select development tools.

### Updating an existing designer installation

Moving to another clone or updating a LiTheView submodule does not update an
extension already installed in VS Code. Check **Extensions > LiTheView Designer**
for the installed version. Install the matching VSIX, then run **Developer: Reload
Window**. Validation in an isolated editor does not change your normal editor.

For a source-only VSIX, prepare tools inside the application's actual LiTheView
submodule, not just an outer library checkout. From the application root:

```powershell
./Middlewares/Third_Party/LiTheView/tools/prepare-vscode-designer.ps1
```

If the required .NET SDK is installed separately, pass `-Dotnet` with its executable
path. `dotnet --list-sdks` in that environment must satisfy the submodule's
`tools/designer/global.json`; installing only the runtime does not provide an SDK.
The VS Code C# extension can also select a different SDK from the one used by the
preparation command.

**Widget catalog mismatch:** an older bundled service or an explicitly configured
`litheview.servicePath` can disagree with the application's pinned library. Install
the matching extension and prepare that library. Check the **LiTheView** Output
channel for the service actually selected, and remove obsolete tool overrides.

**Missing `libstdc++-6.dll`, `libgcc_s_seh-1.dll` or `libwinpthread-1.dll`:** the
native preview worker needs the runtime from the same compiler that built it.
The updated preparation script copies that matching runtime set beside the worker
in its ignored build directory. This is a local build operation; the source-only
VSIX still contains no DLLs. A terminal preview can succeed while VS Code fails
because their inherited PATH values differ. Do not download individual DLLs or
mix compiler distributions. Close any error dialogs, rerun preparation with the
updated script, then close and reopen the form. Reload the window when changing
the installed extension.

## Add UI to an application

Initialize LiTheView in your application's dependency folder, open the application
root in VS Code, and run **LiTheView: Add LiTheView UI** from the Command Palette.
The wizard finds the initialized submodule or lets you select another checkout.
Choose the UI source and generated component folders. **Custom display** is the
default: enter dimensions, shape and native storage format. For RGB displays, also
enter independent panel transfer format, physical size and datasheet provenance.
Catalog RGB profiles and native packed definitions are also available.
Choose logical input separately; **Output only** is sufficient for a first display.
Choose **Touch** when the application will route mouse or touch events to controls.

Designer **0.2.7** adds **Configure** to the form toolbar and **LiTheView:
Configure** to the Command Palette, replacing Configure Input. It reopens the
same display and input fields used in initial setup, filled with current values.
Select **Touch** under **Input**, then **Apply** to add mouse/touch input.
Input-only changes leave display profiles untouched. Cancel and unchanged Apply
preserve existing files, IDs and provenance.

Apply preserves all form coordinates and handwritten handlers. Resize the root panel
to the new extent and adjust controls/assets as needed before Generate UI;
incompatible geometry or resources fail validation. Application display drivers
and MCU budgets remain the application's responsibility. RGB projects retain
RGB storage; packed projects retain packed storage. Changing between packed
cell and emitter formats changes the meaning of a coordinate without scaling it.

Save existing JSON edits first. The extension checks the complete configuration
snapshot, clean buffers and paths, applies one workspace edit and saves changed
profiles before their manifest lock. If saving fails, save all named documents;
generation rejects dirty configurations and inconsistent saved locks. Version-2
RGB and version-3 packed projects are supported; legacy v1 migration is separate.
Then **Generate UI**, build and debug with the application's existing presets.

Input choice declares capabilities; application adapters supply them. The H750
starter already includes the Windows mouse queue and FT5336 touch implementation
behind one typed device interface. Other applications provide their own adapters.
The designer neither installs drivers nor enables hardware pins. General keys,
encoder and text-input adapters remain the consuming application's responsibility.

Designer **0.2.5** also creates native packed displays directly in this wizard:

| Format | Form coordinates | Example extent | Native storage |
| --- | --- | --- | --- |
| R2 | Individual red emitters, 2 bits each | 512 x 384 emitters | One byte per 2 x 2 block |
| RRRR2222 | Cells containing four red emitters | 256 x 192 cells | One byte per cell |
| RYRY2222 | Cells containing red/yellow emitters | 256 x 192 cells | One byte per cell |

Choose **Custom display**, select the format, enter your own logical width/height,
shape, optional safe area and specification reference. R2 dimensions must be even.
Cell formats present twice the logical width and height; choosing a format does not
silently resize entered dimensions. Packing is fixed by the chosen library format;
new bit layouts need library support. Custom references remain unverified until
checked against the physical panel. These projects store a version-3 manifest and
native display/input snapshots, with no RGB staging profile or MCU selection.

Designer 0.2.11 exposes an **Events** section in the selected control's inspector.
Use **Create/Open** for a button click, toggle/checkbox change, radio selection,
picker selection, wheel/drop-down selection/commit/cancel, or text changes/commit/cancel.
It creates a named, typed C++
override in application-owned `form_handlers.hpp`, for example
`onEnableCheckedChanged(lithe::CheckedChange event)`. Access other named controls
with methods such as `getLabel1()->setText(...)`.

Save the handler, save the form, Generate UI, then use the application's existing
build/debug workflow. Multiple handlers on a control have independent bindings.
Generation preserves bodies and diagnoses renamed/deleted bindings and signature
changes; existing click bindings remain compatible. The pinned C++ package must
contain `lithe/control_events.hpp` for newly generated typed events. Update the
application's LiTheView dependency when adopting this package; it does not change
the application's submodule pin automatically. The matching prepared service must
advertise `controlEvents`; re-prepare old tooling or use the bundled service.
See [control objects](../../docs/CONTROL_OBJECTS.md) and
the [starter lesson](../../examples/stm32h750b-dk-starter-1/README.md).

**Create UI** saves the manifest, locked display/input snapshots, paths sidecar
and a blank form, then opens the visual editor. It does not generate code yet.
Set the panel properties or add controls, save, then click **Generate UI**.
No MCU selection is needed. Cancelling before Create writes nothing. Existing
source files and unowned generated files remain protected.

The form editor provides the visual preview. To execute your system, select your
application's Win32 CMake preset, build it, select its Windows debug configuration
and press **F5**. Then select the embedded build and debug configuration. The
designer provides editing, preview and generation. It has no build/run buttons,
commands, task selection or debugger integration.
The wizard does not rewrite CMake, tasks or debug launchers. Creation prefers the
extension's bundled service; explicit development overrides still apply.

The [blank H750 starter](../../examples/stm32h750b-dk-starter-1/README.md) teaches
this sequence, including defining a custom display and adding its border yourself.
The [completed example](../../examples/stm32h750b-dk-starter-1-complete/README.md)
contains the expected result. This workflow requires Designer **0.2.2 or later**.

## Work on an existing form

1. Open a `*.litheview.xml` file listed in a `project.litheview.json` manifest.
   The custom editor opens in a normal editor tab. **LiTheView: Open Designer**
   can also select a project form.
2. Add controls using the palette, select them in the canvas or hierarchy, and
   edit their frame or text. Drag moves; the bottom-right handle resizes. The
   Properties panel lists every schema property of the selected widget, grouped as
   Behavior, Content, Appearance, Layout, Resources, and Actions and bindings.
   Colors accept a `theme.*` token, an eight-digit legacy value, or the swatch
   picker; **Reset** returns a property to its default. Each change is one undo step,
   and invalid combinations are reported in the validation panel.
   The palette adds into the selected container when the widget fits there, otherwise into the
   form, at the widget's default size (a **Wheel Picker** is 120 by 140 with five starter items;
   edit **Items** and **Row Height** next). To build tabs, add a **Tab View**, keep it selected, and add one **Panel** per tab; each
   page fills the area below the strip. **Selected Index** chooses the page you are editing, and
   the other pages are marked "hidden page" in the hierarchy.
   The **Component** section renames the selection, moves it to another container (**Parent**),
   changes its stacking order, and duplicates it. Ctrl- or Shift-click selects several components;
   drag or use the arrow keys to move them together, align or space them from the **Selection**
   section, or delete them. **Actions** assigns a declared action or creates a new one, and the
   **Theme colors** panel adds, recolors, renames (updating every use), and deletes unused tokens.
3. Save and undo with the normal VS Code commands. **View Source** opens the
   underlying XML. Source edits and visual edits share the same text document.
4. Use **Generate UI** to save and export through the existing generator. Paths
   come from `project.litheview.paths.json`; handwritten hooks and ownership
   checks retain the same protection as standalone exports. Without that file, a
   project writes to `simulation/` beside its manifest, except a project inside the
   LiTheView checkout (such as `tests/designer-fixtures/*`), which writes beneath
   `build/designer-projects/` so the library sources are never modified.
5. Select your application's Windows CMake preset, build, then use its Windows
   debug launcher and **F5**. Repeat with the embedded preset and launcher when
   ready. These are the same configurations used before adding a UI or after
   adding another control. Generate updates their inputs; it does not create a
   new build configuration for each feature.

The application simulator builds your whole application with host platform
implementations. Firmware builds the same application model, presenter and forms
with embedded platform implementations. The canvas previews layout; the Windows
application exercises the actual RTOS tasks and device adapters. Version 0.2.2
removes `litheview.simulatorTask`, `litheview.firmwareTask`, and the active-project
task variable. Use ordinary application-owned CMake/tasks/launchers instead.

Tool overrides are `litheview.libraryRoot`, `litheview.servicePath`,
`litheview.previewWorkerPath`, and `litheview.dotnetPath`. Relative library overrides
use the containing workspace folder; relative service/worker paths use the resolved
library root. Normally the project paths sidecar and packaged tools suffice.

The designer service needs the .NET 10 runtime. With `litheview.dotnetPath` left at
`dotnet`, the extension uses the first of `DOTNET_ROOT`, `%USERPROFILE%\.dotnet` (the
install the repository tasks use), `PATH`, and `Program Files\dotnet` whose
`dotnet --list-runtimes` includes it, and logs the choice in the **LiTheView** output
channel. If none has it, the editor says which runtime is missing and what each location
provides; install the runtime or set `litheview.dotnetPath`, then reopen the form. An
explicitly set path is checked but never replaced.

Malformed source remains editable in the text editor and is reported through
diagnostics. A missing native worker prevents preview, but does not erase the form.
The extension starts tooling only in a trusted workspace.

## Current scope

This is the existing-form editing milestone: native preview, widget addition,
selection and multi-selection, movement/resizing/alignment, text and schema-property
editing, rename/duplicate/order/reparent, theme tokens, action assignment, deletion,
custom-display project creation and generation. Resource import, binding
assignment and remote (SSH, WSL, container) workspaces are subsequent milestones. New designer features are added
to this extension; the standalone designer remains available but is not extended. The design and acceptance criteria are documented in
`docs/VSCODE_DESIGNER_PLAN.md` in the repository.

Editor RGBA pixels are a host presentation detail. The generated application still
renders directly into its selected native destination format.

## Development checks

**Configure → PC front view** (0.2.8+) accepts an application-owned PNG and lets
you drag an LCD area and named physical button rectangles, with numerical fields
for exact coordinates. Apply saves a `project.litheview.front-panel.json` sidecar
beside the UI project, with an application-relative image path and SHA-256 lock.
The schema is `schema/project-front-panel.schema.json`; bounds, non-overlap,
unique names, image containment and visible scaled areas are checked as well.

The sidecar describes host presentation only. The application consumes it in its
normal CMake build and connects named button regions to typed device inputs.
The H750 starter's locally retained bonus implementation demonstrates
`userButton`/B1 and a shared debounced counter; its README/photo are published
while its application adapters are held locally.
Other applications supply their own adapters. No image is added to firmware
assets, no hardware pin setup is inferred, and no build or run buttons are added.
PNG input is limited to 16 MiB, 16 megapixels and 8192 pixels per side. The PC
window is at most 4096 pixels per side. LCD-only simulation remains available by
unchecking the front-view option. A changed PNG must be reselected and applied.

From the repository, prepare the tools first, then run:

```powershell
node --test --test-isolation=none tools/vscode/tests/*.test.js
./tools/test-vscode-designer.ps1
```

The integration runner uses a private VS Code profile, extensions directory and
copied/generated application fixture below `build/vscode-designer`. It exercises
real custom-editor/native-preview behavior, save/undo/source changes, generation,
and verifies that generation leaves application tasks unchanged and starts no task.
It does not use a hardware build/flash task.
`-FixtureProject path/to/Tasks/UI` copies an existing tutorial UI into the fixture;
the source project remains untouched. `-Dotnet` and `-Code` select tool executables.

The repository's **LitheView: VS Code Designer Extension** launcher is for developing
the extension. Install the VSIX for everyday use inside an application's workspace.
