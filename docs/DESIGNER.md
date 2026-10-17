# LitheView Designer architecture

Status: item 5 architecture, with display/input projects, configurable application integration, and native simulation

This document defines the product, architecture, file contracts, extension boundary, and acceptance
criteria for LitheView Designer. The designer is a Windows-first visual development tool for the
portable LitheView graphics package. It is not part of the embedded runtime.

The usability reference is the classic Delphi/Visual Pascal and early Visual Studio form designer:
define the device surface, place a component, see the actual result, edit it directly, set typed
properties, connect named actions, and build. XML and generated C++ are reviewable build artifacts,
not the primary user interface. The designer must not recreate QML, XAML, a browser layout engine, or
a scripting language.

## Developer workflow is the organizing principle

The architecture follows the order in which a developer creates an embedded display product. The
tool's first question is not which widget to place. It is which display surface the product has.

### Step 1: define the display and select logical input separately

The wizard starts from a checked or custom display. RGB projects retain an editing profile with
physical provenance and legacy metadata; native packed projects use their display definition
directly. Both store a portable display definition and a separate logical-input configuration,
each locked by canonical SHA-256. The display definition contains:

- logical extent, rectangle/circle shape, and safe area;
- native destination format, address space, storage block, and derived channel fields; and
- composition and stored-alpha metadata plus allowed resource representations.

It contains no input device, bus, driver, MCU, linker region, or target limit. The editing profile's
physical measurements and source references remain useful provenance; its legacy input hints do not
override the new independent input snapshot. The wizard offers Touch + Keys, Touch only, Encoder +
Select, and Output only. Text is false in those choices; the underlying input contract can also
describe text delivery. These are product inputs:

- shape defines clipping, hit testing, safe placement, and usable corner area;
- pixel dimensions define the integer coordinate system and layout bounds;
- physical dimensions and pixel density inform text legibility and touch-target guidance;
- destination format defines quantization, stored-alpha capability, and native storage size;
- input capabilities determine which interactions the designer can validate and simulate.

Forms name an exact surface identity. Export checks geometry, input, and resource compatibility and
records the selected definitions and hashes. Changed locked snapshots fail validation; there is no
automatic rescaling or completed surface-migration wizard. A product can choose a different input
configuration without changing the display's native byte meaning.

### Step 2: create the form and interaction model

The developer drags static text, panels, buttons, lists, tabs, images, animations, and custom widgets
onto the exact surface. Parentage, focus order, action IDs, bindings, states, and overlays are edited
visually. The real LitheView renderer supplies the pixels.

The optional new-project tutorial supplies ACTIVATE and RESET controls and a status label. It seeds
developer-owned C++ behavior once; it does not attach invisible behavior to component names. The
starter needs activation/navigation input and a safe area at least 96x94 logical units. Output-only
or smaller displays can begin with an empty form.

### Step 3: import and tune resources

Fonts, icons, images, and animations are imported once, then converted into explicit variants for the
selected display color depths. The developer sees fidelity and generated size before export.

### Step 4: validate, export, and run the C++ application

The tool validates geometry, shape, color capability, resources, actions, bindings, and fixed document
capacities. **Project > Generate UI** saves and checks the active project and
writes its C++ component. Build and debug the application using its existing
CMake presets and IDE launchers. The designer has no build/run functionality;
its canvas is the visual preview.

Application behavior belongs in `application.hpp` in the configured component directory
(`simulation/` by default), which export preserves. Generated
files and their hashes belong to the exporter. The existing **Export C++...** form/integration-shell
path remains available, with its separate preserved-region rules. Neither route assigns target
flash, RAM, stack, transfer timing, or resource placement.

**Project > Project Paths...** saves application, library and component directories in
an optional `project.litheview.paths.json` sidecar. The application root is relative to the manifest;
the other paths are relative to the application root or absolute. This keeps deployment layout out
of display/form hashes. New Project can add sources to an existing UI folder without replacing
files. Exports track ownership per generated file and can coexist with unrelated application files.
The generated `Product<Hooks>` borrows a caller-owned typed handler; its CMake component reuses the
parent's `LitheView::Core` and defaults host executables off when embedded. See
[APPLICATION_INTEGRATION.md](APPLICATION_INTEGRATION.md) for the workflow and current granularity.

### Step 5: integrate and optionally verify a deployment

The same form is built through the runtime XML path or the parser-free C++ path. A separate application
build may apply board/runtime profiles, choose internal flash, external QSPI, host files, or a custom
resource provider, and run linker/stack/hardware gates. Those downstream decisions never become part
of the designer project identity.

## Architecture decisions

The following choices are the baseline for implementation. Changing one requires updating this
document and the affected compatibility tests before changing code.

| Area | Decision |
| --- | --- |
| Host language | C# on .NET 10 LTS, targeting `net10.0-windows` |
| Host shell | VS Code custom editor (webview canvas and inspector) over the designer service; the earlier Windows Forms shell remains buildable but is no longer extended |
| Preview | An out-of-process native C++ worker that renders through the real LitheView core |
| Standalone simulation | Generated CMake consumer of an installed fixed-format Core; real C++ hooks, host display/input adapter, and headless checks |
| Widget metadata | One versioned, declarative JSON schema generates C# and C++ metadata |
| Layout source | Canonical LitheView XML remains the portable/runtime interoperability format |
| Parser-free output | Deterministic C++23 `constexpr` layout and resource tables |
| Behavior boundary | Stable action and binding identifiers; application behavior remains C++ |
| Resource conversion | Deterministic offline conversion; no runtime font, PNG, SVG, or GIF decoder |
| Extension model | Versioned packages with declarative schema and optional trusted host/preview code |
| Embedded boundary | No designer, .NET, filesystem, heap, exception, RTTI, or plugin dependency |

.NET 10 is selected because item 5 is a multi-year tool effort and .NET 10 is the current LTS line.
Windows Forms supplies a mature Windows shell, accessibility, and typed property editing. The
LitheView canvas remains custom because its components are not Windows Forms controls and its pixels
must come from LitheView's native renderer. The implementation may reuse supported component-model
services such as `TypeDescriptor`, `PropertyDescriptor`, and `PropertyGrid`, but it must not make the
legacy Visual Studio designer serializer the document source of truth.

## Goals

- Match the immediacy and reliability of a classic RAD form designer.
- Preview the actual portable renderer rather than a visually similar desktop reimplementation.
- Make every display, widget, and resource property typed, discoverable, and validated.
- Keep output deterministic and suitable for safety-conscious review.
- Support compact and high-fidelity variants of fonts, icons, images, and animations.
- Expose deterministic sizes and identities that downstream deployment tooling can budget.
- Let third parties add widgets and specialized editors without modifying the designer core.
- Keep product logic, drivers, RTOS policy, persistence, and hardware ownership outside LitheView.
- Preserve the existing fixed-capacity, caller-owned embedded model.

## Non-goals

The first product is not:

- a general-purpose IDE or C++ compiler;
- a QML, XAML, HTML/CSS, or responsive-web designer;
- a JavaScript, expression, data-binding, or embedded-scripting runtime;
- a general application-logic generator; the optional tutorial seeds a small editable example hook;
- a display-driver, touch-driver, filesystem, settings, or RTOS generator;
- a live target debugger or replacement for STM32CubeIDE/VS Code;
- a runtime vector, font, PNG, SVG, GIF, or video decoder; or
- a promise that every desktop font or effect fits every embedded target.

The designer may launch an editor at a generated action stub or resource file. It does not own the
application's behavior.

## Classic RAD interaction model

### Main workspace

The default workspace contains:

- a project explorer for surfaces, forms, themes, resources, and extensions;
- a component palette grouped by Containers, Text, Input, Navigation, Data, Media, and Custom;
- a central native-rendered canvas with rulers, zoom, pan, guides, and display-safe bounds;
- a hierarchy tree showing parentage, sibling order, visibility, lock state, and stable names;
- a Properties/Actions inspector with categories, descriptions, reset-to-default, and validation;
- a resource studio for fonts, images, icons, animations, palettes, and generated variants;
- an output/diagnostics panel with source locations and one-click selection; and
- a nonvisual component tray for resources, providers, and action maps that have no frame.

Panels are dockable and their arrangement is user-local state, not part of exported forms.

### Direct manipulation

The canvas must support:

- single and multiple selection from either the canvas or hierarchy;
- drag placement from the palette with a keyboard-accessible insertion alternative;
- move, resize, reparent, duplicate, delete, and in-place text editing;
- handles, snap lines, grid snapping, guides, alignment, distribution, and same-size commands;
- exact geometry entry, keyboard nudging, z-order, focus order, and lock/unlock;
- container-aware coordinates, safe-area guidance, and shaped clipping;
- copy/paste with deterministic ID and resource-reference repair; and
- zoom that never changes the form's integer display-pixel geometry.

Design adorners are drawn by the C# canvas above the native pixel preview. They are never sent to the
embedded renderer and never appear in exported output.

### Editing transactions and undo

Every user-visible mutation is an explicit command against the typed document. Commands record enough
state to undo and redo without reparsing generated output. A drag is one transaction, not hundreds of
undo records. Property editors can open a transaction, preview intermediate values, then commit or
cancel atomically.

The document becomes dirty only after a committed semantic change. Selection, zoom, panel placement,
and preview input do not dirty the form.

### Forms, reusable parts, and inheritance

The initial implementation supports forms and reusable composite components. Later classic-RAD
productivity work adds templates and single-base form inheritance. Inherited elements retain stable
identities, clearly show inherited/local state, and permit only schema-authorized overrides. The tool
detects missing bases, cycles, incompatible schema versions, and sealed-property overrides before
export.

## System architecture

```text
LitheView Designer process (.NET 10 / C#)
  display surface -- typed document -- commands/undo -- resource studio
                |                    |                  |
                +---- schema-generated C# descriptors--+
                |
                +--> canonical XML importer/exporter
                +--> deterministic C++/resource generator
                +--> validation and export report
                |
                +--> versioned preview protocol
                              |
                              v
                    Native preview worker (C++23)
                    snapshot decoder -- StaticLayout -- LitheView renderer
                              |
                              v
                    caller-owned surface-format pixels

Embedded application
  canonical XML --> XmlLayoutParser --> ViewSpec[] --> StaticLayout<N>
       or
  generated constexpr ViewSpec[] -----------------> Form::Layout
                                                     (StaticLayout<N, Form::Widgets>)
```

A generated form also declares `Form::Widgets`, the widget kinds it uses in catalog order, and
`Form::Layout`, a `StaticLayout` limited to them. A product that builds the form with `Form::Layout`
links only those widgets' code; the exported simulator does so. XML parsed at run time keeps the
all-kinds `StaticLayout<N>`.

The designer process owns editing, files, undo, desktop allocation, and user interaction. The native
worker owns no project files. It accepts an immutable snapshot, renders into a bounded buffer, and
returns pixels plus explicit diagnostics. A malformed document or defective native extension may
terminate the worker, but must not corrupt the project or terminate the designer. The designer
restarts the worker and identifies the last failed request.

## Source and solution boundaries

The implementation should live under `tools/designer` and remain optional to every CMake consumer:

```text
tools/designer/
  LitheView.Designer.sln
  src/
    LitheView.Designer/              Windows Forms application
    LitheView.Designer.Core/         document, commands, validation, exporters
    LitheView.Designer.Schema/       generated C# descriptors
    LitheView.Designer.Cli/          headless import/validate/export tool
    LitheView.Designer.Extensibility/extension contracts
  tests/
schema/
  widgets.schema.json                schema-of-schema validation
  widgets.json                       built-in widget/property definitions
src/designer_preview/                native worker and versioned C ABI
include/lithe/designer/              generated/native preview contracts
```

Names may be adjusted when the skeleton is created, but ownership boundaries must remain. A normal
`LitheView::Core` consumer must not require .NET, the schema compiler, preview worker, or designer.

## Project and document model

### Project manifest and profiles

A designer project has a deterministic `project.litheview.json` manifest. It names one display
surface, forms, resource manifests, enabled extensions, and schema compatibility versions. New RGB
projects use manifest v2, which also locks a display definition and separate logical input. The
corresponding snapshots live in `profiles/display-definition.json` and `profiles/logical-input.json`.
Paths are project-relative, use forward slashes in serialized form, and may not escape the project
root. The embedded runtime never parses this build-time manifest.

Display-surface profiles are separate versioned JSON documents so manufacturers, teams, and extension
packages can supply reviewed profiles without copying settings between projects. The selected profile
is copied into the project as an immutable snapshot, hashed, and recorded in export reports. Optional
target/deployment profiles are separate build inputs owned by the consuming application. Existing v1
projects open without rewriting their bytes. Resolution explicitly adapts their old display/input
interpretation; non-RGB MDP staging metadata survives that adaptation and prevents unsupported native
export. Opening an older project does not silently migrate its coordinates or storage format.

Native packed displays use manifest v3, a display-definition project. It has no `surfaceProfile` or
`schemaCompatibility.displaySurface`, even as null placeholders, and locks only the display definition
and logical-input snapshots at the paths above. Only `r2Packed2x2`, `rrrr2222` and `ryry2222`
definitions are accepted (LVDP1008); RGB displays keep their v2 editing surface. Resolution reads and
hash-checks both snapshots, validates storage blocks against the logical extent, and needs the project
directory but no surface catalog or MCU target. Its identity hash (resolved identity version 2) covers
the definition and input instead of a surface; v1/v2 identities are unchanged. Creation
(`project create-native`) writes a root form at the definition's logical extent, so a 512 by 384
emitter project keeps 512 by 384 coordinates.

Every project version opens through one workspace and one display contract, `DesignerDisplay`: the
logical extent, shape, safe area, destination format, display definition and logical input. Versions 2
and 3 take it from their locked snapshots; version 1 from its explicit adaptation, falling back to the
editing surface only where a legacy surface (for example a rotated one) has no compiled form. The
desktop editor, VS Code service, preview and Generate UI therefore treat a v3 packed project like any
other project, with no separate editor and no RGB staging framebuffer. Preview renders packed storage
directly (see the native preview contract), and Generate UI re-checks the manifest and both snapshots
against what it compiled immediately before publishing (LVUI4103). Version-1 MDP projects keep their
historical RGB565 staging preview and are never relabeled: a v1 manifest marked v3 fails (LVPM1203),
and a v1 surface adaptation cannot seed a native project (LVDP1009). No automatic migration exists.
Create a v3 project from an explicit packed definition and port the form deliberately. Emitter
definitions keep 512 by 384 coordinates; cell definitions use 256 by 192.

Designer 0.2.5 creates v3 projects through both **Add LiTheView UI** in VS Code and
**File > New Project** in the Windows shell. Choose a native catalog definition or
define a custom native format, logical dimensions, shape and specification reference.
R2 coordinates address individual emitters and require even dimensions; RRRR/RYRY
coordinates address 2 x 2 emitter cells. Format-derived fields come from the same
checked contract as the compiler. Custom sources are recorded as unverified, without
invented hashes or claims about physical emitter wiring. Each project starts with a
blank panel and explicit, independently selected logical input. The VS Code wizard
publishes validated application path settings atomically with the form and snapshots;
invalid paths or conflicting files leave no partial project. Generate UI remains an
explicit next step. CMake and the application debugger still own builds and launches.
`project create-native` remains available for scripted creation.

### Forms

Each form is saved as canonical LitheView XML. The typed in-memory model preserves:

- a stable document identifier and schema version;
- a stable component identifier distinct from the optional 16-bit runtime/action ID;
- parent/child relationships and sibling order;
- integer frame geometry relative to the parent surface/container;
- typed widget properties and state overrides;
- named action, binding, theme, font, image, icon, and animation references; and
- explicitly preserved historical extension attributes when importing legacy XML.

The XML dialect remains the runtime interoperability contract described in
[XML_LAYOUT.md](XML_LAYOUT.md). Designer-only window placement, selection, zoom, and panel state live in
an ignored per-user settings file, never in the form.

Imported historical XML is normalized through the typed model. Compatibility mode may preserve known
extension attributes, but product export must pass strict validation.

### Stable identities

The editor's component identity is a persistent opaque ID used for undo, inheritance, references, and
diff repair. The embedded `ViewId` remains a bounded product-facing value. Renaming a component does
not change either identity. Copying creates a new component identity and resolves colliding runtime
IDs explicitly.

Human-readable names must be unique within a form. Generated C++ symbols are derived deterministically
and diagnose collisions rather than silently appending unstable numbers.

## One widget schema

`schema/widgets.json` is the only hand-maintained definition of built-in widget metadata. It is a
strict, versioned JSON document validated by `widgets.schema.json`. Users do not write it to design a
form; framework and extension authors use it to describe components.

The schema generator emits:

- C++ widget, property, XML-name, default, and validation metadata;
- C# immutable descriptor records and typed property adapters;
- canonical XML reader/writer and C++ generation maps;
- property reference documentation; and
- compatibility fixtures proving C# and C++ agreement.

A generated-artifact check fails when committed generated files differ from clean regeneration.

### Widget descriptors

Each widget descriptor contains a namespaced stable type ID, palette identity, help, default geometry,
runtime kind/factory, permitted parent/child relationships, display/input capabilities, fixed-capacity
contribution, properties, actions, bindings, states, child slots, and XML/C++ compatibility names.

### Property descriptors

Supported property kinds include Boolean, signed/unsigned integer, enum, flags, color, string,
bounded text, rectangle, point, size, duration, action ID, binding ID, theme token, font, bitmap, icon,
animation, palette, child collection, and extension-defined opaque data with an explicit validator.

Each property states its default, range, units, required/optional status, serialization name, editor,
surface restrictions, validation, and documentation. A property cannot exist only in C# or only in C++
without an explicit compatibility exemption and test.

### Evolution rules

- Adding an optional property with a deterministic default is backward compatible.
- Renaming a property requires an import alias and one canonical export name.
- Removing or changing meaning requires a schema-major version and deterministic migration.
- Unknown extension types or required properties fail closed.
- Files record the schema version they were normalized against.
- Migrations are pure, ordered transformations with golden tests.

## Layout output and application behavior

### Canonical XML

Export preserves stable hierarchy/sibling order, emits canonical class/property names and integer
geometry/colors, resolves resources/actions/bindings/themes, emits consecutive animation frames, and
rejects unsupported XML behavior. It then parses its output in strict mode and compares the resulting
typed document with the source.

### Deterministic C++

Parser-free export produces C++23 Allman-style immutable strings, animation-frame name arrays,
resource references, and `constexpr ViewSpec` arrays. Output:

- follows `docs/style_guide.md` and the formatting gate;
- records generator/schema versions, source hash, and surface profile;
- contains no machine-specific absolute paths or timestamps by default;
- uses stable symbol ordering and normalized line endings;
- never invents callbacks or hidden ownership; and
- regenerates byte-for-byte from the same project inputs, generator version, and preserved user-region
  contents.

Generated wrapper code is first-party code for warnings, formatting, and compile tests. Large immutable
payloads may use a documented generated-data exclusion while their wrappers remain checked.

### Preserved user-code regions

Generated integration headers and source files contain explicit save zones, called preserved user-code
regions, where developers may add application code that survives later designer exports. The syntax is
deliberately simple C++ comments:

```cpp
// LITHEVIEW USER CODE BEGIN form.main.additional_includes
#include "product/device_state.hpp"
// LITHEVIEW USER CODE END form.main.additional_includes
```

Region identifiers describe a stable semantic integration point rather than a display label or file
line number. Defined integration points include additional includes, forward declarations, controller
members, initialization before/after hooks, action-handler bodies, binding-provider hooks, and final
application declarations. Generated `constexpr` layout/resource data remains generator-owned; a region
is never placed inside an array initializer or other structure whose modification could invalidate the
generated model.

Regeneration follows a fail-closed preservation algorithm:

1. Read the existing generated file, if present, before producing replacement text.
2. Extract the exact bytes between every recognized begin/end marker.
3. Require unique, non-nested, balanced markers with matching stable region IDs.
4. Generate the new wrapper and reinsert each preserved region at the corresponding integration point.
5. Reparse the result, validate all markers, and write it through the atomic-save path.

The generator does not reformat, trim, interpret, or otherwise alter text inside a region. New regions
are emitted empty. If an existing region has no destination because a form, action, or extension was
removed, or if any marker is missing, duplicated, nested, mismatched, or malformed, the generator must
not overwrite the existing file. It writes a separately named candidate file, reports the exact region
problem, and requires the developer to reconcile or explicitly discard the orphaned code.

The region contents are explicit code-generation inputs for reproducibility and build reporting. They
remain first-party application code and therefore still compile with project warnings and participate
in formatting and Core Guidelines checks, even though the designer itself preserves their bytes.
Substantial product behavior should normally live in user-owned controller/source files; save zones are
stable integration seams, not a reason to put application architecture into a generated layout file.

### Actions, bindings, and controller hooks

The Actions inspector maps events such as click, toggle, selection change, edit commit, and validation
failure to stable named action IDs. The generator may emit an enum and optional controller method
stubs. Generated stub bodies contain stable user-code regions so a later export cannot clobber a
developer's handler implementation. The generator does not implement behavior.

Bindings name caller-owned storage or providers. Generated contracts document type, capacity,
lifetime, and mutability. No action or binding contains executable text, reflection paths, or
expressions.

## Native preview contract

The designer must display the same clipping, shaped viewport, font metrics, blending, pixel
quantization, scrolling, animation, and widget-state behavior as the product. Repainting an
approximation in C# would create a second graphics implementation and make WYSIWYG unreliable.

### Worker protocol

The designer starts a version-matched native worker. Requests carry protocol/schema versions, the
resolved display surface, an immutable form snapshot, resource hashes, simulated model values,
widget states, input events, elapsed time, and a monotonic request ID. Responses carry explicit status,
document/property diagnostics, dirty regions, animation wakeup state, timing counters, and pixels.

Control messages are length-prefixed and size-limited. On Windows, large pixel data uses named shared
memory created by the designer. Linux and macOS have no named shared memory, so a worker there
advertises the inline-pixels capability in its hello response and returns each frame after the render
response, bounded by the maximum surface; both transports render identical pixels. Neither side trusts lengths supplied by the other. The worker uses a versioned
C ABI internally and converts immediately to typed C++; C++ library types and compiler class layouts
never cross the boundary.

Native packed displays render natively. The C ABI pixel formats 4, 5 and 6 are `r2Packed2x2`,
`rrrr2222` and `ryry2222`, and a worker that supports them reports hello capability `0x2`. The
designer never sends a packed format to a worker without that capability; it tells the user to rebuild
the worker instead. For every format, width and height are logical pixels, emitters or cells, and
`stride_bytes` advances one storage-block row. A packed extent must contain whole storage blocks
(2 by 2 emitters per byte for R2, one cell per byte otherwise). A frame occupies block rows times
stride. The worker renders through the existing core painters straight into that storage; there is no
RGB framebuffer or quantization step, and packed formats accept only native quantization. Malformed
extents, short strides and insufficient buffers fail closed. The editor's displayed image is expanded
only at the final presentation boundary (`PreviewPresentation`), one pixel per emitter level: an R2
surface is shown at its own 512 by 384 size, a 256 by 192 cell surface at 512 by 384. The service's
render response reports that image size, and the VS Code canvas scales it to the logical stage.

### Preview modes

The canvas offers exact surface pixels, integer zoom, selected high-color/quantized format, shaped
masks, display-compatible resource variants, and pointer/key/encoder/button/focus/time simulation. Optional design
overlays show safe areas, bounds, clips, dirty regions, focus, and resource identities without changing
the framebuffer.

### Phase 5.4 implemented baseline

The checked Windows worker links the real XML parser, fixed-capacity layout, `Application`,
`ViewController`, full-frame renderer, and `TiledRenderer`; it does not contain a second widget or
painting implementation. A C-compatible ABI owns no storage and accepts caller-provided snapshot,
input, pixel, damage, and diagnostic spans. A generated-catalog SHA-256 is negotiated alongside the
protocol, ABI, and schema versions so stale managed/native metadata cannot be treated as compatible.

The control channel is little-endian, length-prefixed, and capped. Framebuffer bytes travel through a
designer-created `Local\\LitheView.Preview.*` mapping whose name and exact capacity are validated by
both sides. The worker receives no project path, opens no project file, and contains no network path.
Every request has a monotonically increasing identity. Deadline, cancellation, broken-pipe,
protocol-mutation, and process-death handling terminates the isolated process; the following request
performs a fresh negotiation.

### Phase 5.5 implemented walking skeleton

The Windows Forms application now starts from the developer workflow rather than an empty drawing
surface. The first new-project page selects a checked surface or defines a custom display contract with
shape, physical and pixel dimensions, painter color depth, native display properties, and source
provenance. The second page names the project, selects its folder and independent logical input, and
offers the optional tutorial. New projects are built in a unique staging directory with canonical
manifest/form XML and local snapshots. They are moved into place and strictly reopened. An
existing destination is never merged or overwritten.

The classic RAD shell exposes Project, Toolbox, native Canvas, Hierarchy, Properties, Actions,
Output, and Component Tray work areas. Its palette is generated from the widget schema and initially
places panel, label/static text, button, image, line, and progress widgets. Canvas and hierarchy
selection are synchronized. Ctrl/Shift extends canvas selection; a group drag or one/ten-pixel arrow
nudge is one document transaction and does not move a selected descendant twice. A developer can
drag-move, lower-right drag-resize, set exact geometry, edit text in place with F2/double-click,
duplicate complete subtrees, drag hierarchy nodes to reparent, change z-order, delete, and undo/redo
each edit as one immutable document transaction. Reparenting preserves absolute position and rejects
cycles or illegal container relationships before changing the document. Duplication regenerates
component IDs/names and clears assigned runtime IDs. Dirty state is tied to the save-point history
rather than the visual controls.

The canvas has visible 4/8/16-pixel grids, optional snapping, safe-area/shape adorners, and 25 through
400 percent zoom. Properties are generated from the checked widget schema, hide unsupported fields,
expose exact typed scalar/enum/color values with schema help, and allow component renaming. The Actions
tab binds widget events to application-owned stable form actions. Creating a new action and assigning
it is atomic; invalid IDs, symbols, duplicate runtime IDs, or unsupported event sources fail through
the same strict document resolver.

Every canvas framebuffer pixel comes from the isolated native worker. The managed canvas adds only
surface, safe-area, selection, and resize adorners. Save uses canonical XML and atomic replacement;
**Export C++...** writes deterministic Allman C++ and preserves exact user-code regions, falling back
to a separate candidate on malformed regions. The checked VS Code launch builds the managed solution
and worker before opening the shell. The current build/test commands are in the
[tooling README](../tools/designer/README.md#build-and-test); historical phase test counts are not the
current suite inventory. No embedded board is needed or accessed by this workflow.

### Standalone simulation workflow

The editing canvas and the running application share Core rendering but have different jobs. The
canvas worker renders the current immutable form for editing, with managed adorners above its pixels.
It does not execute the developer's `ApplicationHooks`. The exported simulator compiles the generated
definitions/layout/resources and the developer's C++ hook, then routes host input through the actual
portable application. Mouse coordinates map back to logical display coordinates; native destination
bytes are decoded for the host window after rendering.

The tutorial assigns status-label view ID 100, Activate action/control ID 1, and Reset action/control
ID 2. `ApplicationHooks::onReady(Views&)`, `onAction(Views&, ViewId)`, and `onBack(Views&)` are typed,
`noexcept` hooks. The seeded actions change the status between READY and ACTIVE. A developer edits
`simulation/application.hpp` and rebuilds to change that behavior; repeated export preserves it.
The [tutorial file map](NEW_HARDWARE_TUTORIAL.md#4-edit-the-exported-application-and-understand-file-ownership)
identifies each generated and developer-owned file and explains the equivalent handwritten route.

The host build service invokes CMake/CTest with MinGW Makefiles and tools inherited from the designer's
`PATH`. It verifies generated hashes, compiles exactly the selected RGB destination, installs Core,
builds the standalone consumer, and checks the input identities again before returning its executable.
The UI launches only that returned executable after a successful result. Build logs and source/binary
identities are retained under the configured build root's `debug/` or `release/` directory
(`simulation/build/debug/` by default).
The report deliberately keeps target feasibility unevaluated.

The UI retains the process handle returned by its own launch. Before rebuilding, it requests that
window's normal close, then uses bounded termination/wait if needed. Cancellation also completes
bounded cleanup of that owned process. It never locates or stops simulators by name or adopts an
external debugger's process. Form disposal releases the handle without terminating a successfully
running simulator; an external instance must be closed by its owner before relinking its executable.

Generated `.vscode` tasks configure and build against the configured build root's `debug/installed`;
they require the first successful service build to create that package. Open the exported component directory to use
**Generated simulator (Debug)**, with GDB and the C/C++ extension. The emitted C++ consumer has no
managed-runtime dependency. Its `headless.cpp` provides repeatable host checks while `win32.cpp`
supplies the current interactive presentation boundary.

## Resources and deployment separation

### Source and generated resources

Imported assets are copied into or referenced by a project-controlled resource root, hashed, and
recorded with licensing/provenance notes. Source assets are never silently rewritten. Generated
variants record converter version/options, source/output hashes, dimensions, format, and payload.

The resource system supports:

- TTF/OTF fonts with selected face, size, weight, repertoire, fallback, and alpha1/alpha8 variants;
- PNG/SVG source art converted to bounded bitmap/icon representations;
- alpha1, indexed4, RGB565, RGB888, XRGB8888, and ARGB8888 variants where supported;
- tintable mask icons and fixed-color icons;
- frame sequences/sprite sheets with geometry, timing, repeat, and palette; and
- application-, library-, or provider-owned logical resource lifetimes.

The embedded runtime never opens the source asset or runs a desktop decoder.

### Phase 5.6A implemented resource-contract foundation

The first resource-studio slice adds a strict, versioned resource manifest without adding a decoder,
filesystem dependency, or allocation policy to the embedded library. A manifest gives each original
asset a stable identity and records its byte count, SHA-256, media type, provenance, licensing, and
redistribution policy. Each generated variant records the exact converter identity/version/options,
output hash and size, format, dimensions/frame count, compatible painter formats, ownership, lifetime,
and the optional logical provider identity.

The resource CLI validates and canonicalizes manifests, then independently verifies the declared
source, generated output, and license-text artifacts against their locked byte counts and hashes.
Project resolution loads only project-declared resource manifests, rejects paths outside the project
root, and selects exactly one compatible variant for the selected display painter format. Resource
identities and selected variants contribute to the resolved project identity, so changing an asset,
converter option, display-format mapping, or ownership contract changes the reviewable project hash.

The manifest deliberately contains no board ID, target profile, flash/QSPI placement, linker symbol,
alignment rule, RTOS, driver, or target-cost evidence. A consuming application chooses those policies
after export. The checked alpha1 icon began as a small contract fixture. Production font, raster
image/icon, and sprite-animation converters now extend that contract.

### Phase 5.6B font resource workflow

The font slice connects the existing offline `tools/convert-font.ps1` rasterizer and embedded
`GeneratedFont` runtime to the resource contract. The visual editor selects a caller-owned TTF/OTF,
the actual Regular/Bold outline, a reviewed visual style and weight, 4-64 pixel size, an exact Unicode
repertoire, and alpha1 or alpha8 coverage. A private-font preview shows the selected face without
installing it. The same conversion service is available through `font convert` for headless builds.

U+003F is always present as the deterministic fallback. Repertoire input is parsed, de-duplicated,
sorted, and collapsed into canonical ranges before conversion. Alpha1 packs thresholded coverage;
alpha8 retains antialias coverage. Neither format encodes a text color, so the generated glyph shape
continues to work with any draw color and every supported painter format. Imported font variants are
therefore marked compatible with RGB565, RGB888, XRGB8888, and ARGB8888 rather than being needlessly
locked to the painter format active when they were created.

Import requires recorded origin, affirmative redistribution approval, and either an SPDX ID or a
copied license file. It stages the source, optional license, generated C++, and strict v2 measurements
under the project; verifies converter identity, selections, byte counts, checksums, and hashes; then
moves a new resource directory and atomically updates the project manifest. Existing directories are
never merged or overwritten. The current form receives only a logical font ID, generated runtime
entry point, and symbol.

The measured table payload is reported for engineering visibility, but the designer does not assign
it to storage. Linker section, internal or external flash, QSPI, alignment, target budgets, and
application provider wiring remain downstream integration choices.

### Phase 5.6C image and icon resource workflow

The image/icon slice adds **Resources > Import Image** (`Ctrl+Alt+I`) and **Import Icon**
(`Ctrl+Alt+K`). It previews the source crop and tint, exposes exact output geometry, nearest/bilinear
sampling, alpha handling and threshold, matte, ordered dither, explicit indexed4 palette, and every
native `Bitmap` representation from alpha1 through ARGB8888. A measured representation-size estimate
updates with the selected format; it is not a target-memory assignment.

The converter accepts reviewed PNG/BMP/JPEG/GIF/TIFF raster files. It emits immutable allocation-free
C++ bytes, an optional palette, a `const Bitmap`, a compile-time validity check, strict v1 metrics,
and an accessor for the application `ResourceProvider`. Tintable content is restricted to alpha1 or
alpha8 icons so the stored coverage shape can be recolored without duplicating it. Fixed-color image
and icon variants retain their authored direct pixels or explicit palette.

Conversion and import fail closed on invalid crop/geometry, unsupported format/option combinations,
unsafe identity, mismatched metrics, license/provenance policy, artifact drift, and an existing
resource directory. A real indexed4 golden performs two conversions and locks the source hash,
payload/palette sizes, three FNV-1a checksums, byte-identical C++/metrics, generated formatter guards,
and compile-time `Bitmap` validity. Headless builds use the same service through `image convert`.

The current deterministic GDI+ converter does not rasterize SVG. Vector source may be retained as
review evidence, but a developer must make and review an explicit lossless PNG export before this
conversion step. This avoids machine-dependent SVG engines, fonts, filters, and color management.
The full cold-start workflow, byte layouts, integration example, and tradeoffs are documented in
[`IMAGES.md`](IMAGES.md).

### Phase 5.6D animation resource workflow

The animation slice adds **Resources > Import Animation** (`Ctrl+Alt+A`). It accepts a reviewed raster
sprite sheet and an explicit row-major grid of equal-sized frames. The visual editor exposes frame
origin/cell geometry, columns/count, output size, native `Bitmap` format, alpha/tint/matte/palette,
sampling, dither, frame interval, repeat index, and autoplay default. A click-to-pause preview plays
the exact declared frame order and reports the measured pixel plus shared-palette payload.

The checked converter invokes the single-image packer for every frame, requires common layout and
palette, concatenates immutable bytes, and emits a static `std::array<Bitmap>` whose descriptors borrow
the generated storage. Timing accessors carry authoring defaults; no embedded timer, thread, decoder,
filesystem, or allocation is introduced. The consuming application maps stable per-frame names to the
borrowed span through its ordinary `ResourceProvider` and continues to own monotonic time.

Import stages and verifies source/license/C++/metrics/manifest artifacts and adds the logical frame
resources as one undoable form edit. The headless `animation convert` command uses the same strict
runner. A two-pass indexed4 golden locks eight 96-by-72 frames, 27,648 pixel bytes, one 40-byte
palette, source and FNV-1a identities, formatter guards, native compile validity, and byte-identical
C++/metrics. See [`ANIMATIONS.md`](ANIMATIONS.md) for the complete cold-start workflow, provider
adapter, lifetime rules, timing model, and fidelity/storage tradeoffs.

### Resource editors

The designer provides a font/repertoire preview, image crop/scale/format/dither controls, icon tint and
state preview, animation timeline and palette preview, and per-display-format fidelity/payload
comparisons.
Changing a conversion option invalidates only affected variants and produces a reviewable manifest
diff.

### Optional downstream deployment profiles

Separate build/conformance tooling may apply profiles such as:

- a constrained U545 tiled profile whose resource and size gates remain hard;
- H750 RGB565 and high-color profiles with SDRAM framebuffers;
- an H750 high-fidelity application that may choose external QSPI resources; and
- Win32 integration used for host-side application testing.

These profiles do not appear in the visual project, form XML, preview request, or generated layout
identity. Linker/archive/stack gates remain authoritative for an application build, and an estimate is
never labeled a measured embedded result.

## Widget states, themes, and runtime scope

Item 5 adds tab navigation, button variants, a touch scroll-wheel picker, overlay panels, a slide-up
keyboard, virtualized lists, visual states, and bounded theme tokens. These are graphics framework
components, not product application behavior.

Every widget is a vertical slice containing bounded C++ runtime behavior, schema, generated C#/C++
metadata, strict XML, parser-free generation, preview, appropriate input modes, states, docs, examples,
goldens, target budgets, and an item-gate style/Core Guidelines ratchet.

Themes contain bounded color, typography, spacing, radius, and motion tokens. Resolution happens at
build/layout construction or through borrowed bounded tables; it must not add runtime string maps or
allocation.

The first Phase 5.7 slice implements form-local color tokens and the common normal/focused/pressed/
disabled resolver. A form contains at most 32 stable `theme.*` color IDs. The designer exposes a
visual token editor and non-exclusive token choices for focusable widget color properties. Export
materializes concrete `Style` colors; canonical XML preserves the authored token reference in strict
metadata comments that the native parser safely ignores. Handwritten styles with omitted state colors
retain the original accent/white/disabled policy. See
[`THEMES_AND_WIDGET_STATES.md`](THEMES_AND_WIDGET_STATES.md) for the complete cold-start contract.

The second Phase 5.7 slice standardizes text, icon, momentary, and toggle buttons as variants of the
one schema `Button` widget and runtime `ButtonView`. Behavior (`momentary` or `toggle`), Checked, and
an optional Icon bitmap are ordinary schema properties, so the existing palette entry, XML reader and
writer, parser-free C++ generator, and native preview carry them. A toggle latches the new retained
`selected` widget state, which ranks below disabled, pressed, and focused and has its own optional
token-capable color pair; checkboxes and radio buttons share the same state bit. Strict validation
rejects a checked momentary button (`LVFD1132`), and changing a toggle to momentary clears Checked
in the same undoable edit. The Properties grid gains a resource-reference dropdown for bitmap and
font properties, and `preview render` accepts a bounded `--input` sequence that drives focus,
encoder, push-button, key, pointer, and time input through the real worker. See
[`BUTTONS.md`](BUTTONS.md) for the cold-start guide and the checked `button-variants-smoke` example.

The third Phase 5.7 slice adds the Tab Bar and Tab View widgets, the first slice built VS Code first.
A tab view's direct children are its pages; the VS Code palette adds into the selected container, so
selecting a tab view and adding a Panel creates a page that fills the content area below the strip.
Selected Index chooses both the page shown at start-up and the page being edited: the service marks
the other pages `previewHidden`, the native preview hides them, and the canvas does not hit-test
them. Strict validation requires 1 through 16 titles and a valid selection (`LVFD1133`); a page-count
mismatch (`LVFD1134`) or a page covering the strip (`LVFD1135`) is a warning so pages can be added
one at a time. See [`TABS.md`](TABS.md) and the checked `tab-view-smoke` example.

The fourth Phase 5.7 slice adds the Wheel Picker, a vertical touch and encoder scroll wheel with the
selection centered in a band and a `row-height` property (12 through 255 pixels). Select or an
encoder push starts and ends editing, so rotation still moves focus between controls until the user
commits to changing a value. A new wheel starts with five starter items; strict validation requires
1 through 255 items and a valid selection (`LVFD1136`). The VS Code palette now leaves every new
widget's size to the schema's default geometry. See [`WHEEL_PICKER.md`](WHEEL_PICKER.md) and the
checked `wheel-picker-smoke` example.

VS Code increment 4 brings existing-form editing to parity with the standalone designer: rename,
duplicate, stacking order, reparenting, multi-selection with group move, alignment, distribution
and same-size, multi-delete, theme-token add/recolor/rename/delete, and action assignment and
creation. Every operation calls the same `DesignerDocumentSession` method as the standalone tool;
[`VSCODE_DESIGNER_PLAN.md`](VSCODE_DESIGNER_PLAN.md) maps each one. Project creation, resource
import, and binding assignment remain standalone-only for now.

## Extension model

### Package contents

A LitheView extension is an explicitly installed, versioned package containing:

- `extension.litheview.json` with identity, version, compatibility, hashes, and capabilities;
- declarative widget/property/action/state schema fragments;
- embedded C++ source/headers or documented library integration;
- optional native preview-worker module;
- optional trusted C# property/component/resource editors;
- canonical examples, fixtures, tests, and developer documentation; and
- licensing and provenance information.

Type IDs are globally namespaced, for example `com.example.controls.dropdown-picker`.

### Schema-only and trusted modes

The designer can inspect, validate, serialize, and generate a schema-only extension without executing
extension code. Specialized managed editors and native preview modules remain disabled until the user
trusts the exact package hash. Trust is local state and cannot be granted by opening a project.

Native modules load only in the worker. Managed editors execute in the designer only after
authorization and receive narrow document services. The first release has no online gallery or
automatic download/update. A lock file records exact identity, version, content hash, schema version,
preview ABI, and embedded API compatibility. Missing or mismatched required extensions fail closed.

### Extension parity

Built-in and custom widgets use the same schema, validators, document model, preview protocol,
exporters, docs, and tests. The reference extension is a dropdown picker. It proves collection editing,
popup/overlay children, selection actions, keyboard/pointer/encoder input, a custom property editor,
native preview, and embedded integration without a designer-core edit.

## Safety, determinism, and security

Desktop tooling may allocate, use exceptions internally, run background conversions, and access
project files. It must use cancellation, immutable snapshots, explicit diagnostics, atomic save, and
bounded untrusted-input parsing.

Embedded/runtime rules do not change:

- portable C++23 without exceptions or RTTI;
- no heap during parse, build, input, animation, layout, or render;
- caller-owned storage/resources with explicit lifetimes;
- fixed capacities and fail-closed exhaustion;
- no filesystem, network, clock, thread, RTOS, or driver ownership; and
- explicit errors rather than partially constructed layouts.

Import rejects external XML entities, paths outside the project root, oversized messages, duplicate
identities, missing resources, invalid capabilities, and capacity overflow. Strict JSON parsing rejects
duplicate keys. Saves write a new temporary file and atomically replace the old file; failure leaves the
prior file intact.

For identical inputs and tool versions, output is byte-for-byte identical. Validation layers are:

1. project, surface, form, schema, extension, and resource syntax;
2. typed property and parent/child rules;
3. geometry, shape/safe-area, color/input capability, and rotation compatibility;
4. identity, action, binding, theme, and resource resolution;
5. optional downstream deployment capacities and budgets;
6. strict XML self-parse and typed-model equality;
7. generated C++ compile/layout equivalence;
8. native preview goldens; and
9. host/cross-build size, stack, static-analysis, format, and test gates.

Diagnostics have a stable code, severity, component/property/profile location, plain description, and
remediation. A warning cannot downgrade a hard contract error.

## Test strategy

The suite includes surface/profile validation and migration, schema/C#/C++ parity, command/undo,
copy/paste/inheritance, historical XML round-trip, parser-free C++ equivalence, resource determinism,
preview protocol mutation/crash containment, per-format render goldens, extension trust/mismatch,
target budget diagnostics, UI automation, accessibility, and the existing whole-project C++ gates.

Win32 preview is host evidence. M33/M7 cross-builds are compile/link/size evidence. Physical display
behavior is a separate hardware test, and use of the shared H750 board requires permission first.

## Completion criteria

Item 5 is complete only when:

- a developer starts from a display surface and builds a representative project without touching
  markup or selecting hardware;
- shape, physical/pixel size, color depth, input capability, and generated resource sizes affect
  validation and output as documented;
- every supported property round-trips through canonical XML;
- parser-free C++ renders equivalently to XML;
- the real native renderer drives every canvas preview;
- fonts, icons, images, and animations produce deterministic display-compatible variants and sizes;
- every item 5 widget is usable through palette, inspectors, XML, C++, and input simulation;
- the dropdown-picker extension works without a designer-core edit;
- malformed input, missing resources, overflow, and incompatible extensions fail closed;
- the common demo exercises tabs, picker/list, button states, validation, and keyboard overlays on
  Win32 and H750 and cross-builds for U545/M33; and
- the final project-wide conformance/style sweep reduces remaining advisory debt with hard gates at
  zero.

The phased execution plan is [ITEM_5_IMPLEMENTATION_PLAN.md](ITEM_5_IMPLEMENTATION_PLAN.md).

## Deferred decisions

Installer/update policy, package signing/gallery, cross-platform hosts, localization ownership,
live-device preview, and constraint/responsive layout beyond explicit surface geometry are deferred
until the walking skeleton provides evidence. None may weaken deterministic export, extension trust,
or the embedded ownership boundary.

## Design references

- [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Windows Forms changes in .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100)
- [Windows Forms design-time overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls-design/designer-overview)
