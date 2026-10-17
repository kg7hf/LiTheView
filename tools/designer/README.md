# LitheView Designer tooling

This directory contains the Windows-first C#/.NET 10 tooling defined by
[`docs/DESIGNER.md`](../../docs/DESIGNER.md). It is an optional build-time tool and is never a
dependency of `LitheView::Core` or an embedded application.

The [VS Code extension](../vscode/README.md) reuses this engine for existing-form
editing inside the application's workspace. VS Code owns document history; the application's existing CMake presets and
debug launchers build and run the system independently of the designer. See the
[shared application plan](../../docs/SHARED_APPLICATION_PLAN.md) for the full host/MCU
workflow beyond isolated UI preview.

The designer owns the UI work product, not the hardware integration. A project selects a display
definition, separate logical input, forms, logical resources, and optional extensions. It does not select a board, MCU, RTOS,
driver, bus, linker section, internal/external flash location, DMA policy, or framebuffer placement.
Those decisions belong to the consuming application and its deployment/build configuration.

The library can live anywhere as a submodule. **Project > Project Paths...** configures application,
library and generated component locations independently. CMake owns build output paths. New Project can add UI
sources to an existing application folder without overwriting files. Generate UI emits portable form/display/resource headers and an interface CMake target.
The application owns its UI controller and behavior; no unused application hook is generated. Follow
[`docs/APPLICATION_INTEGRATION.md`](../../docs/APPLICATION_INTEGRATION.md) for the exact path bases,
ownership rules, source-submodule CMake, and device adapter interfaces.

The current implementation provides:

- strict display-surface and project contracts with canonical JSON and semantic SHA-256 identity;
- a versioned widget catalog shared by the visual shell, CLI, and generated C++ metadata;
- canonical form XML and deterministic Allman-style C++23 generation with preserved user-code zones;
- an isolated native preview worker linked to the real LitheView renderer;
- a classic RAD visual shell with project/toolbox/canvas/hierarchy/properties/actions/output areas; and
- C++ component generation and a two-button/status tutorial whose
  application hook remains developer-owned;
- deterministic resource manifests with source, provenance, license, converter, generated-output,
  display-format compatibility, ownership, and lifetime contracts;
- a font resource editor with local TTF/OTF preview, face/size/weight/repertoire selection,
  alpha1/alpha8 generation, strict provenance, measured payload, and logical form integration; and
- image and icon resource editors with live crop/tint preview, native bitmap-format, alpha, matte,
  palette and dither controls, deterministic conversion, and transactional contained import; and
- an animation resource editor with explicit sprite grid, native frame representation, timing,
  repeat/autoplay, click-to-pause playback, shared-palette packing, and atomic logical-frame import;
  and
- a bounded theme-color editor plus schema-driven normal/focused/pressed/disabled/selected color
  selection;
- text, icon, momentary, and toggle button variants through Button's Behavior, Checked, and Icon
  properties, with a resource-reference dropdown for bitmap and font properties.

Theme color names are design-time form data. Export resolves them to concrete `lithe::Style` values;
the embedded runtime performs no string-map lookup. Canonical XML retains the authored token choice in
validated metadata comments while remaining directly consumable by the ordinary native parser. See
[`docs/THEMES_AND_WIDGET_STATES.md`](../../docs/THEMES_AND_WIDGET_STATES.md) for the workflow and
custom-widget contract.

The wizard reads checked editing profiles under `profiles/display-surfaces`. New RGB projects retain
that snapshot and add separately locked v2 display-definition and v1 logical-input snapshots. The
portable definition excludes drivers, buses, and MCU limits. Standalone target profiles under
`profiles/targets` are optional downstream conformance/deployment inputs; the visual designer does not
load them or write them into a project. The normative designer contracts are
`schema/display-surface.schema.json`, `schema/display-definition.schema.json`,
`schema/logical-input-configuration.schema.json`, `schema/project-manifest.schema.json`,
`schema/resource-manifest.schema.json`, and `schema/widgets.schema.json`. The optional integration
sidecar follows `schema/project-paths.schema.json`; it does not change the portable project schema.

## Add forms to your application

Prefer the [VS Code designer](../vscode/README.md) in your application workspace.
The [blank H750 starter](../../examples/stm32h750b-dk-starter-1/README.md) walks from
custom display definition and a blank canvas to a generated border in the same
Windows and STM32 application builds. Its completed sibling contains the expected
result. No build/run controls live in either designer.

The standalone Windows shell still supports **File > New Project**, display/input
selection, resource editing and **Project > Project Paths...**. Edit the form, save,
and select **Project > Generate UI** or **Generate UI** on the toolbar. Generation
validates the locked profiles and writes the component to its configured directory,
protecting handwritten files. Build and debug from your application IDE.

**Refresh Native Preview** (`F5` in the standalone shell) refreshes the editing
canvas. **Project > Export C++...** retains the form/integration-shell export and
its preserved user-code regions. Neither command runs your application.

The [display-first tutorial](../../docs/NEW_HARDWARE_TUTORIAL.md) describes file
ownership, typed hooks, native bytes, equivalent handwritten files and later
hardware/target-budget work. The CLI's standalone simulation helpers remain useful
for generator development; they are not the application designer workflow.

## Build and test

From this directory:

```powershell
$Dotnet = 'dotnet' # Select the SDK required by tools/designer/global.json on PATH.

& $Dotnet restore LitheView.Designer.sln --ignore-failed-sources --disable-parallel
& $Dotnet build LitheView.Designer.sln -c Release --no-restore
& $Dotnet run --project tests/LitheView.Designer.Core.Tests -c Release --no-build
& $Dotnet run --project tests/LitheView.Designer.App.Tests -c Release --no-build
```

Paths such as `assets/source/DejaVuSans.ttf` and `assets/source/status.png` below
represent caller-supplied inputs; these files are not bundled. See the
[build environment guide](../../docs/BUILD_ENVIRONMENT.md) for prerequisites and path rules.

Useful contract commands:

```powershell
& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  profile validate ../../profiles/display-surfaces/stm32h750b-dk-rk043fn48h-rgb565.json

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  project resolve `
    ../../tests/designer-fixtures/display-profile-smoke/project.litheview.json `
    ../../profiles/display-surfaces

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  resource verify `
    ../../tests/designer-fixtures/resource-manifest-smoke/resources/status-icon.resource.json `
    ../../tests/designer-fixtures/resource-manifest-smoke

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  project resolve `
    ../../tests/designer-fixtures/resource-manifest-smoke/project.litheview.json `
    ../../profiles/display-surfaces

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  form validate `
    ../../tests/designer-fixtures/document-compiler-smoke/forms/main.litheview.xml `
    ../../schema/widgets.json

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  form export-cpp `
    ../../tests/designer-fixtures/document-compiler-smoke/forms/main.litheview.xml `
    ../../schema/widgets.json `
    ../../tests/designer-fixtures/document-compiler-smoke/generated/main_form.hpp `
    ../../tests/designer-fixtures/document-compiler-smoke/generated/main_form.cpp

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  font convert `
    assets\source\DejaVuSans.ttf `
    ../../build/designer-font.cpp `
    ../../build/designer-font.metrics.json `
    UiFont uiFont 18 Regular Regular 400 Alpha8 U+0020-U+007E `
    ../../tools/convert-font.ps1

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  image convert `
    assets\source\status.png `
    ../../build/status_icon.cpp `
    ../../build/status_icon.metrics.json `
    StatusIcon statusIcon Icon true Alpha8 `
    0 0 128 128 24 24 `
    Bilinear Preserve 96 None `
    ff2d6cdf ff000000 "" `
    ../../tools/convert-image.ps1

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  animation convert `
    ../../tests/rendering/assets/animations/gallop_sprite_sheet.png `
    ../../build/gallop_animation.cpp `
    ../../build/gallop_animation.metrics.json `
    GallopAnimation gallopAnimation false Indexed4 `
    0 0 128 96 4 8 96 72 `
    Bilinear Preserve 64 Ordered4x4 `
    ffffffff ff000000 `
    "00000000,ff141519,ff2c2d35,ff543422,ff7d321f,ffa94a22,ffc96936,ffe6a548,fff5c96a,ff2bb7ad" `
    100 0 true `
    ../../tools/convert-animation.ps1
```

The low-level manifest-only project-create form is:

```text
project create <output.json> <schema-reference> <project-id> <display-name>
  <surface-catalog-directory> <surface-id>
```

A native packed display project (manifest version 3) locks a packed display definition and an
explicit logical input, with no RGB editing surface or MCU target:

```text
project create-native <project-directory> <project-id> <display-name>
  <display-definition.json> <logical-input.json> <widgets.json>
```

The following legacy CLI scaffold is retained for generator testing, separately from Generate UI.
For application integration, use `component export-project <project.json> <designer-checkout>` and
your existing application builds. To exercise the standalone CLI test scaffold: From
`tools/designer`, after building Release as above, choose a new project directory:

```powershell
& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  simulator create-tutorial ../../build/designer-first-light `
    ../../profiles/display-surfaces/stm32h750b-dk-rk043fn48h-rgb565.json `
    ../../profiles/logical-inputs/encoder-select.json `
    ../../profiles/display-surfaces ../../schema/widgets.json

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  simulator export ../../build/designer-first-light/project.litheview.json `
    ../../profiles/display-surfaces ../../schema/widgets.json `
    ../../build/designer-first-light/simulation

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  simulator build ../../build/designer-first-light/simulation ../.. Debug

& ../../build/designer-first-light/simulation/build/debug/host/litheview_generated_simulator.exe
```

CLI `simulator build` builds and checks the application; it does not launch the GUI. Its optional last
argument is `Debug` or `Release`. Ctrl+C requests cancellation. `create-tutorial` creates a new
directory and never merges into an existing project. Export preserves an existing destination
`application.hpp`; a new separate destination starts with the source project's hook when present.

The repository also provides a combined runner. From the repository root:

```powershell
./tools/run-designer-tutorial.ps1 -Configuration Debug
```

Its default project is `build/designer-tutorial-debug/project/`, using RGB565 and Encoder + Select.
It builds the CLI, creates the project if absent, exports, builds, runs CTest, and writes logs plus
`build/designer-tutorial-debug/validation.json`. It prints the executable path without launching.
Parameters also include `-ProjectDirectory`, `-Surface`, and `-InputConfiguration`. Existing projects
keep their own display/input snapshots; use a new directory to test different choices.

Project resolution verifies the locked display-surface hash, loads only resource manifests declared
by the project, and selects exactly one resource variant compatible with the surface painter format.
The resolved identity includes the project, surface, resource-manifest, selected-variant, and output
hashes. It performs no network access.

Resource validation is deliberately hardware-independent. `resource validate` checks provenance and
license policy, deterministic converter identity/options, generated file metadata, compatible painter
formats, and logical ownership/lifetime. `resource verify` also requires every project-contained
source, output, and optional license file to match its declared byte count and SHA-256. It does not
choose flash, QSPI, a linker symbol, or a target memory budget.

The form contract records stable component, action, binding, resource-reference, theme, and surface
identities. Logical resource references contain only the identity needed by the runtime
`ResourceProvider` boundary; storage placement and payload accounting are not form properties.
`form export-cpp` emits a `constexpr ViewSpec` document and an integration shell. Only byte ranges
between matching `LITHEVIEW USER CODE BEGIN/END` markers are developer-owned. A malformed or orphaned
region leaves the existing integration file untouched and writes a `.candidate` file.

`form budget` and the target-profile CLI remain available as explicit downstream deployment analysis.
The CLI caller supplies the target profile and render-workspace byte count. Generated C++ source size
is informational; archive text, controller-stack depth, and complete RAM remain unknown until matching
evidence is supplied through the analyzer API. View nesting is not controller-stack depth, and mutable
text storage is only one RAM component. `budget-evidence: unevaluated` does not certify a target.
Those commands do not alter the designer project or form and are not used by the visual editor.

For v2 display definitions, `display check-target` consumes an already valid surface application and
adds a separate target report. See [the generated example workflow](../../tests/rendering/display-model-generated/README.md)
for commands, evidence types, constrained and sufficient fixtures, and unknown measurement handling.

## Visual designer workflow

In VS Code, select `LitheView: Item 5 Visual Designer` from Run and Debug. Its preparation task builds
the managed solution and isolated MinGW preview worker. The managed build restores its dependencies
automatically, including on the first launch from a fresh clone or submodule. It runs from
`tools/designer` so the checked `global.json` selects the required .NET SDK.

The new-project wizard follows the display-first workflow:

1. Choose or define the display: shape, logical dimensions and coordinate units, native storage
   format, safe area and source reference. RGB editing profiles also retain physical dimensions.
2. Name the UI project, choose its location, and select logical input separately. Choose the optional
   working tutorial or start with an empty form.
3. Build forms and logical resources against that display contract.
4. Generate UI and connect the application-owned handlers. Build and debug the application's
   simulator with its existing CMake preset and launcher, then use its embedded configuration.
5. Optionally run separate deployment/budget checks for a chosen application target.

Project creation validates the requested contracts and stages canonical files in a hidden temporary
directory, then publishes and strictly reopens the project. By default it creates a new directory.
**Use an existing application folder** instead adds the UI files to the selected directory after
checking every destination for conflicts; it never replaces existing files. A project-local immutable
display-surface snapshot lets custom surfaces resolve without a
repository-global profile. For new supported RGB projects, the manifest also locks independent
display-definition and logical-input snapshots; opening or exporting rejects changed hashes. The
historical input hints inside an editing surface do not override the v2 logical-input selection.

The walking skeleton supports schema-driven placement of panel, label/static text, button, image,
line, and progress widgets; multi-selection; move/resize/nudge/delete/duplicate/reparent/z-order;
in-place text; grid/snap/safe-area/zoom controls; schema-backed properties; stable action assignment;
transactional undo/redo; native-worker pixels with C# adorners; and deterministic C++ export.

Choose **Resources > Import Font** (`Ctrl+Alt+F`) to create a contained font resource. The editor
previews the caller-selected TTF/OTF, captures raster and visual style separately, selects an exact
Unicode repertoire and alpha1/alpha8 coverage, requires provenance/license approval, and reports the
measured immutable table payload. It adds the generated entry point as a logical form resource. The
application remains responsible for compiling that generated C++ and deciding its storage placement.

Choose **Resources > Import Image** (`Ctrl+Alt+I`) or **Import Icon** (`Ctrl+Alt+K`) for a contained
raster resource. The editor exposes reviewed crop/output geometry, sampling, native `Bitmap` format,
alpha/matte, optional ordered dither, tint, and explicit indexed4 palette with a live preview and
measured payload. Import accepts PNG/BMP/JPEG/GIF/TIFF; reviewed SVG artwork requires an explicit
lossless raster export. Tintable resources are alpha1/alpha8 icons. Generated direct/indexed color
resources keep their authored color. See [`docs/IMAGES.md`](../../docs/IMAGES.md) for byte layouts,
CLI details, provider integration, ownership, and compact/high-fidelity tradeoffs.

Simulator export compiles selected static font/image/animation resources and emits their provider
bindings. Reimport older animation resources whose manifests lack converter `entryPoint` and
`symbol` options before exporting; function names are not inferred from the generated C++ source.

The native worker supports RGB565, RGB888, XRGB8888, and ARGB8888 surfaces; rectangular and circular
viewports; shaped masks; quantization; and pointer, key, encoder, button, focus, and time input. Preview
requests contain a surface and immutable form snapshot, not a target profile. The headless
`preview render <form.xml> <surface.json> <worker.exe> <frame.raw>` command accepts up to 64
`--input` steps (`focus`, `next`, `previous`, `select`, `key:<name>`, `press|move|release|cancel:<x>,<y>`,
`elapsed:<ms>`) to render a button's focused, pressed, or latched state, or a tab change, through the
real worker. See [`docs/BUTTONS.md`](../../docs/BUTTONS.md), [`docs/TABS.md`](../../docs/TABS.md), and
[`docs/WHEEL_PICKER.md`](../../docs/WHEEL_PICKER.md). Tab pages and wheel pickers are edited in the
VS Code designer; this standalone designer validates and exports those forms but has no
widget-specific editing for them.

## VS Code entries

Open the application root in VS Code, edit its form, save and Generate UI. Use the
application's existing CMake preset and debug launcher. The designer neither
creates a build configuration per feature nor starts a separate preview application.

The legacy generated standalone scaffold has its own tasks for generator testing.
Those need a matching installed Core package and are not the H750 application's
build entry points. The CLI can prepare that scaffold independently of the editor.

Repository-level entries remain:

- `LitheView: Designer Tutorial Simulator (MinGW Debug)` (native debugger launch)
- `LitheView: prepare Designer Tutorial Debug` (project/export/build/check task)
- `LitheView: build Item 5 Designer Debug`
- `LitheView: build Item 5 Native Preview Worker`
- `LitheView: prepare Item 5 Native Preview`
- `LitheView: test Item 5 Designer`
- `LitheView: test Item 5 Designer Core`
- `LitheView: test Item 5 Designer UI`
- `LitheView: regenerate Item 5 widget metadata`
- `LitheView: validate Item 5 document fixture`
- `LitheView: regenerate Item 5 document fixture`
- `LitheView: render Item 5 native preview fixture`
- `LitheView: validate Item 5 resource manifest`
- `LitheView: verify Item 5 resource artifacts`
- `LitheView: run Item 5 Visual Designer`

The designer edits, previews and generates; it does not build host or embedded applications.
The explicit CLI simulator test scaffold records surface passed and target unevaluated;
it does not certify an MCU's flash, RAM, workspace, transfer timing, or resource placement.
