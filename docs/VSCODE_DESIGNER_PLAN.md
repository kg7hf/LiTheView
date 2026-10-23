# LiTheView designer inside the application IDE

The developer opens their application root in VS Code and opens a form as a visual
editor beside C++ source. LiTheView supplements that workspace. The application
continues to own its build, debugger, board drivers and source layout.

The intended simulator is a host build of that same application. Its product logic,
UI presenter and generated forms compile for both host and MCU, with platform and
device implementations selected by the build. See [the shared application plan](SHARED_APPLICATION_PLAN.md)
for the LTE reference analysis, modern boundaries and the next system tutorial increment.

The initial implementation is a Windows desktop extension. The VS Code extension is
the designer's primary editing surface from Phase 5.7 onward: new widget, property,
and workflow editing lands here first. The standalone Windows Forms designer remains
buildable and shares the same document, validation, generation, and preview engine,
but receives no new editor features unless a later decision reverses this. No firmware, display-driver or portable-renderer behavior is changed by this
work. Additional display shapes remain display-contract work shared by both editors.

## Ownership

The implemented Configure front-view section (0.2.8) writes a separate,
application-owned `project.litheview.front-panel.json`: a PNG path relative to
the application root, image dimensions/hash, PC window width, LCD rectangle and
named physical-button rectangles in source-image pixels. The application build
consumes this metadata. Its Windows device layer performs hit testing; its
embedded device layer reads GPIO or a controller through the same typed interface.
Shared task code owns debounce and application callbacks. The H750 starter bonus
lesson demonstrates this with a second label counter. The photo never becomes a
display-native asset or MCU dependency. Configuration does not infer pin mappings,
create a new feature build preset, or route physical buttons through UI focus.
The learner can use their own device image; the supplied board photo is an example.
The designer implementation is published. The H750 bonus application adapters
and generated counter are retained locally; only the example README and photo
are published for this increment, per the owner's publication scope.

### Component specialization and event ownership

Owner clarification, 2026-10-03: form-owned handlers are one supported choice.
A control may implement specialized behavior, a reusable composite may handle its
children, or a button may connect to a typed application handler. UI event routing
and cross-task messaging are separate contracts. Run widget changes on their UI
owner; route commands to other tasks through explicit application messaging.
Define activation, notification ordering, cancellation and handler lifetime so a
single click is not accidentally dispatched twice or delivered to a destroyed object.

Built-in controls intended for reuse must permit public derivation with documented
behavior/drawing hooks. Keep invariants and state private; protected hooks do not
require protected data. Use `final` only with a specific reason, not as the default
for every control. See Core Guidelines [C.139](https://isocpp.github.io/CppCoreGuidelines/CppCoreGuidelines#Rh-final)
and [C.129](https://isocpp.github.io/CppCoreGuidelines/CppCoreGuidelines#Rh-kind).
The first implementation opens `ButtonView` and `LabelView` for derivation, provides
the button's protected `onClicked()` hook, and lets `StaticLayout` own selected
application types through `WidgetOverrides`. Generated names return the actual
selected type. Its storage, alignment, construction and destruction follow that type.
Other control extension/event contracts remain to be designed individually.
No object slicing, reinterpretation of a built-in storage slot as a larger derived
object, RTTI requirement or hidden heap fallback is acceptable.

The handler/custom-type design must follow the [product engineering contract](DISPLAY_MODEL_PLAN.md#product-contract)
and [style guide](style_guide.md). Preserve types through the C++ path; do not add
`void*` contexts or potentially allocating callback wrappers as convenience glue.
Specify safe attachment/detachment or a checked owner-outlives-control lifetime.
Keep predictable execution, allocation behavior and failure policy explicit.

Acceptance extends the same starter: exercise a form-owned button handler, then
an application-defined derived control or reusable composite without modifying
LiTheView source. Verify identical handler behavior on Windows/H750, cancel and
disabled-state behavior, regeneration preserving handwritten code, and actual
custom-object storage. Button/label behavior, cancellation, derived storage and
regeneration preservation now have host coverage; H750 target execution of this
new interaction is still a separate validation step. See [the implemented contract](CONTROL_OBJECTS.md).

### Developer experience target: forms, controls and named handlers

Owner clarification (2026-10-03): the intended workflow resembles a visual form
designer: place and name controls, edit their properties, create an event handler,
and connect that handler to application devices or services. The current generic
`onAction(ViewId)` hook and manually assembled H750 presenter are lower-level
building blocks; they do **not** complete that workflow.

Designer 0.2.4 generates named control accessors and typed button event dispatch,
provides **Create/Open click handler** in VS Code, and preserves developer handler
bodies across regeneration. The application supplies typed references to
its LED, controller, model or command queue. Internal action identifiers and view
construction should not be routine application work. Renames/deletions must report
stale bindings and preserve user code; generating or updating a handler must be
explicit and reviewable. Avoid opaque `void*` context plumbing outside genuine OS/C
API boundaries.

Acceptance: from the blank H750 starter, add a button, create its named click
handler, call the application's LED interface, and run the **same handler and UI**
on Windows and STM32. Then add a selector and receive its selected value through
a typed handler. The ON/OFF label exercise below is implemented first; LED backend
integration remains a subsequent increment; typed selector events are implemented
in the control-event milestone below.

The owner's first real interaction is a button toggling a label between ON and
OFF. They created `button1` and `label1` without code. That is now the first discovery
and acceptance exercise: select the button, create/open a named click handler,
access the label by its generated name, save/generate/build with the same presets,
and exercise the same handler on Windows and H750. Do not make the developer find
a numeric action ID or a handler in unused generated scaffold code.

The starter remains blank until the developer creates UI. Its lesson starts with
Output only, adds the border, then button1/label1 and their handwritten handler.
Designer 0.2.7 adds **Configure**, reopening the familiar initial display/input
screen with the saved values filled in. The next lesson selects **Input: Touch**,
then Apply, Generate UI and the same application build. Input-only changes leave
display profiles intact. Display edits preserve form coordinates and handwritten
handlers; changed dimensions require an explicit root/layout edit before generation.
All forms share these project settings; hardware adapters and target budgets remain
application responsibilities. Configure replaces the narrow 0.2.6 input command.
`Lessons/FirstDisplay/ui.cpp` owns the form, constructs it and drains bounded input
on the UI task. Windows supplies a mouse queue; H750 polls its FT5336 touch controller.
Generate UI now emits only a component; the old `application.hpp` stub is no longer emitted.
Any preserved legacy copy is not compiled by this starter. See [boundary cleanup](LIBRARY_BOUNDARY_CLEANUP.md).
Named events use application-owned `form_handlers.hpp`. Input remains separate from the
display definition; no raw cross-thread widget access or erased context callbacks
should be added to the portable application. Evolve `stm32h750b-dk-starter-1`
through these steps in place; do not create another example or preset for this
interaction. `stm32h750b-dk-starter-1-complete` contains the expected ON/OFF end state.

Resource sizing is a configurable cost decision. Prefer bitmap inputs prepared
externally at their final display size; permit multiple prepared sizes or explicit runtime scaling
for selected resources, especially fonts. Do not impose a blanket runtime-scaling
ban. Design/export should expose storage and rendering tradeoffs, preserve this
choice in the project, and use the same resource configuration in simulation and
firmware. Existing font/bitmap scaling capabilities remain available; a unified
designer policy and cost reporting for these choices are still to be implemented.
LiTheView validates prepared bitmap samples and encodes supported channel ordering,
byte ordering and native packing before compilation. General resizing, colour-depth
reduction, dithering and palette design remain external-tool responsibilities.
See [source validation and native encoding](DISPLAY_MODEL_PLAN.md#source-validation-and-native-asset-encoding).

The 0.2.2 extension's **LiTheView: Add LiTheView UI** defaults to **Custom display**.
It validates the display and independent logical input, creates a blank form and
locked profiles, and records application-relative paths. The developer designs
the form, saves, and selects **Generate UI**. The form editor is the layout preview;
the next execution step is the application's Win32 build and F5, followed by the
embedded build and F5. Build/run commands have been removed from both designer shells. CMake and the
application IDE own compilation and execution, using the same platform presets
and debug configurations before and after adding UI.
MCU budgeting remains a later stage. Existing source and unowned generated files
are protected. See the [extension guide](../tools/vscode/README.md).

Validation covers blank creation, custom RGB565 storage with RGB888 panel transfer,
manual fill/border edits, explicit generation, native rendering and existing
application build ownership (no task dispatch from the designer).

| Layer | Responsibility |
| --- | --- |
| VS Code | Authoritative text document, save, undo/redo, file changes, source control, tasks, debugging |
| Extension | Form association, application/project discovery, editor lifecycle, diagnostics, explicit generation |
| Webview | Native-pixel presentation, selection, drag/resize, palette, hierarchy and property controls |
| Designer service | Checked document transformations, surface validation, existing code generation and preview worker lifecycle |
| Native worker | Pixels produced by the real C++ renderer, using the selected surface format |
| Application | Directory paths, generated component integration, typed behavior hooks and hardware adapters |

The text buffer is the source of truth. Each engine request carries the current
document text and version. Edits return proposed text; the extension checks the
version and applies a VS Code WorkspaceEdit. The service does not save forms or
maintain a competing undo history. Invalid source text stays intact and produces
diagnostics. Stale preview results cannot replace the view for a newer version.

Preview bytes are converted to RGBA for the editor's canvas only. The web frontend
does not redraw LiTheView widgets in CSS or alter the generated product's native
pixel format. Protocol and widget-catalog compatibility are checked before preview.

## Increment 1: a complete existing-form workflow

Open an existing project form from the application root; inspect its native preview;
add, select, move and resize controls; edit text; save/undo/reopen; generate the C++
component; run an existing workspace task. The extension reads the existing
`project.litheview.paths.json` with its documented manifest/application-relative bases.
It preserves generated-file ownership rules and handwritten application hooks.

The first pass does not require a project wizard, resource editor, new hardware
configuration UI, replacement build system, or debugger implementation. Users can
continue to create/configure projects and manage resources in the standalone editor.

Acceptance gates:

1. Opening a form from an application root with spaces and an independently located
   library resolves the same manifest and output paths as the standalone tool.
2. An edit followed by save, undo, source edit and reopen preserves one document
   history and does not overwrite concurrent or malformed source changes.
3. Native RGB888 preview and generated code reflect the same edited form.
4. Regeneration preserves developer-owned code and refuses conflicting generated edits.
5. Workspace build/debug configurations remain application-owned. User-selected
   tasks execute in their existing scope; no automatic hardware access occurs.
6. Service failure, incompatible protocol/catalog, missing worker and editor closure
   produce useful diagnostics and clean up owned child processes.
7. Standalone designer and existing Core tests continue to pass.

## Validated first increment

Increment 1 implementation and Windows validation completed on 2026-09-27:

- Strict managed solution build: zero warnings/errors; existing Core 72/72 and
  standalone designer 17/17 checks pass.
- Service 52/52 and Node host/frontend 29/29 checks pass, including three real native
  RGB format previews, stale edits, failed preview status and task ownership.
- Actual VS Code extension and installed VSIX workflows pass. The installed package
  passes ten checks using a copied application with spaces in its path, an independently
  located library without prepared tools, no service/worker overrides, and MinGW
  removed from the test process PATH. Palette operations, native pixels, save/undo,
  source recovery, generation and existing task dispatch are exercised.
- The resulting edited RGB888 consumer compiles and passes CTest 1/1. Its execution
  is UI-example validation, not the shared complete-system milestone.

Local evidence is under `build/vscode-designer`: `latest-integration.json` points
to the installed-package result; `regression-build.log`, `regression-core-tests.log`,
`regression-app-tests.log` and `generated-simulator-build.log` retain the build/test
outputs. Service evidence is under
`build/designer-service-tests/5bfbd4af621848cca09ba9b6e9e381b5/validation.json`.
All fixture edits are isolated copies; no hardware actions were performed.

## Increment 2: schema-driven properties

The webview inspector now edits every editable schema property of the selected
component, so each widget slice is usable in VS Code as soon as its catalog entry
exists. The designer service's `inspect` result carries the authored property values,
catalog property descriptors (kind, category, default, range, units, enum values,
documentation), and the form's declared theme colors, resources, actions, and
bindings. Two additive protocol-version-1 edit operations, `property` and `reset`,
pass through the same document session and strict resolver as the standalone tool.

Controls are typed: checkboxes, bounded number fields, enum and reference dropdowns,
text fields, and colors that accept a `theme.*` token, a legacy eight-digit value,
or an opaque native color picker. Unset properties stay absent from the XML and show
their default. Every change is one VS Code document edit and therefore one undo step;
rejected values (for example `LVFD1132`, a checked momentary button) appear in the
validation panel without changing the document. Frame and text keep their dedicated
forms; animation-frame lists are shown read-only.

Linux evidence (2026-10-01): service 58 checks, Node 35 checks, and a headless
Chromium run of the real webview against the real service and the real C++ preview
library. That run changed a button to a latched toggle with a theme-token selected
color, observed the native latch bar, cleared Checked by returning to momentary,
and saw `LVFD1132` reported for an invalid edit. The VS Code host itself, the VSIX,
and the Windows preview worker still need the Windows integration runner.

## Increment 3: containers and tab pages

The palette adds into the selected component when it is a container that the new widget may be
placed in (the widget view now carries `allowedParentIds` and `container`); otherwise it adds to the
form root as before. A nested addition sends no frame, so the shared editing session chooses it:
default geometry centered in the parent, or, for a tab view, the whole content area below the
strip. New tab widgets start with two titles so they are valid immediately.

Component views carry `previewHidden` for tab-view pages other than the selected one and for
everything inside them. The native preview already hides those components because it runs the real
runtime; the canvas now skips them in hit testing, the hierarchy marks them "hidden page", and a
hidden selection is outlined with a dashed border. Selected Index is therefore the design-time page
selector, edited like any other property and undone like any other edit. Page-structure problems
are warnings (`LVFD1134`, `LVFD1135`), so building a tab view one page at a time never blocks an
edit. These are additive protocol-version-1 fields.

Linux evidence (2026-10-01): service 69 checks (adding a tab view, adding pages without a frame,
`previewHidden` following Selected Index, `LVFD1133` rejection) and Node 37 checks (container-aware
palette operation and hidden-page hit testing). The VS Code host and VSIX still need the Windows
integration runner.

## Increment 4: editing parity

Everyday form editing no longer needs the standalone designer. Each operation below is an additive
protocol-version-1 edit that calls the existing shared `DesignerDocumentSession` method, so
validation, undo granularity, and generated output match the standalone tool exactly:

| User action in VS Code | Edit operation | Shared session method |
| --- | --- | --- |
| Rename in the Component section | `rename` | `Rename` |
| Duplicate (one or a multi-selection) | `duplicate` | `Duplicate` |
| To back / Back / Forward / To front | `order` | `ChangeZOrder` |
| Parent dropdown ("Move to") | `reparent` | `Reparent` |
| Delete or move a multi-selection, align/distribute/same size | `delete` with `componentIds`, `frames` | `Delete`, `SetFrames` |
| Theme colors panel: add, recolor, rename, delete | `themeColor`, `renameThemeColor`, `deleteThemeColor` | `SetThemeColor`, `RenameThemeColor`, `RemoveThemeColor` |
| Actions section: assign, clear, or create and assign | `action` | `AssignAction`, `CreateAndAssignAction` |

Ctrl- or Shift-click adds to the selection in the canvas or hierarchy; dragging any selected
component or pressing an arrow key moves the whole selection; Delete removes it. Alignment uses the
last selected component as the anchor and applies only to siblings. Renaming a theme token rewrites
every reference in the same edit, and a token still in use cannot be deleted (`LVED1802`). A new
action's C++ symbol follows the designer's identifier rule, its ID is derived from the symbol, and
its runtime ID defaults to one above every declared action and component runtime ID.

Two shared-session defects surfaced and were fixed: reparenting into a container that appears
later in the document failed with `LVFD1111` because the moved subtree kept its old position, and a
z-order change left document order out of step with sibling order. Both now restore parent-first
document order. The standalone designer benefits from the same fix.

Linux evidence (2026-10-01): Core 79/82 (the three Windows-only failures are unchanged), service 94
checks, Node 42 checks, and a headless Chromium run of the real webview against the real service and
C++ preview library covering every row of the table, with the form file on disk untouched. The
Windows integration runner now also applies rename, duplicate, order, theme-token, and action edits
in the real VS Code host.

## Increment 5: wheel picker and schema-sized additions

The Wheel Picker reaches VS Code with no webview code of its own: the palette, the
schema-driven Properties panel (including the new **Row Height**), validation, and the native
preview all come from the catalog and the shared session. A new wheel starts with five starter items
so it is valid immediately, and `LVFD1136` rejects an empty or oversized item list or a selection
beyond it.

Root-level palette additions previously sent a fixed 100-by-36 frame, which made a new wheel too
short to use. The palette now sends no frame for any addition, so the shared session places every
new widget at its schema default size centered in the parent, exactly as the standalone designer
does.

Linux evidence (2026-10-02): Core 83/86 (the three Windows-only failures are unchanged), service 100
checks, Node 49 checks, and a headless Chromium run of the real webview against the real service and
C++ preview library: selecting a wheel, changing Row Height, adding a wheel from the palette at
120 by 140 with starter items, and rejecting a selection beyond them. The VS Code host and VSIX
still need the Windows integration runner.

## Increment 6: application task buttons (superseded in 0.2.2)

The earlier Simulate/Build firmware task dispatch experiment was removed after
owner review. Designer buttons, commands, task-ranking/settings code and the
active-project task variable are gone. The standalone designer likewise exposes
Generate UI, with no build/run/cancel actions or owned simulator process.

The form is the visual preview. The developer builds and debugs their application
through its existing CMake/IDE configurations. Starter 1 demonstrates the
progression: `win32-debug` and `stm32-debug` build the initial application, then
include generated UI on the next build without selecting feature-specific presets.
The completed sibling is a separate example of the expected result, not a separate
configuration of the starting application.

## Increment 7: Linux and macOS preview

The native preview worker now builds and runs on Linux and macOS. Windows keeps its transport
unchanged: the designer creates a named mapping and the worker renders into it. Other hosts have
no named shared memory, so the worker advertises an inline-pixels capability in a previously reserved
hello field, the client sends an empty mapping name, and the worker renders into a bounded buffer
(zeroed per request, like a fresh mapping) and returns the frame after its render response. Request
and response bytes on Windows are unchanged, so either side can be updated first. The `host-debug`
CMake preset builds the worker on Linux and macOS, the prepare script and the F5 task use it, and
the extension accepts local Linux and macOS workspaces.

Linux evidence (2026-10-02): the Core tests' native worker checks now run on Linux and pass,
including the frame hashes previously recorded from the Windows worker for all four pixel formats,
the tiled renderer, and worker restart (Core 86/87; the remaining failure needs Windows PowerShell);
service 112 checks with the worker (12 more than without it); the prepare script under PowerShell 7
publishes the service and builds the `host-debug` worker; and the real webview, service, and worker
render the wheel example in headless Chromium. VS Code itself could not be downloaded in the
container, so the extension host on Linux/macOS still needs a manual F5 check.

## Control-event milestone

Implementation update, 2026-10-09: increment 2 is implemented for buttons/toggles,
checkboxes, application-owned bounded radio groups, pickers and wheel pickers.
Shared event metadata drives generation, service discovery and the VS Code Events
inspector. Component/event bindings preserve handwritten bodies and legacy click
comments. [Control objects](CONTROL_OBJECTS.md) defines the implemented payloads,
edit boundaries and ownership. The October 17 increment adds bounded text editing;
selectable list support is now implemented; selectable table contracts and named
tab/softkey events remain open. The table below records the current boundaries.
Validation and the separate hardware acceptance boundary are recorded in the handoff.
Packaged Designer 0.2.9 acceptance passed on October 16 with 24 workflow checks,
including multiple event handlers and preservation through save/reopen/regeneration.

Owner decision, 2026-10-04: complete and exercise interactive control handlers before the image/font
workflow increment. Implemented events and remaining contracts are distinguished below.
The existing button/label host tests establish a foundation; they do not establish all-control support
or a completed physical H750 walkthrough.

### Event coverage and current gaps

| Control | Required semantic events and payload | Current boundary |
| --- | --- | --- |
| Button | Clicked; checked-state change for a toggle, carrying the accepted Boolean state | Implemented named handlers and protected hooks. Toggle change precedes click. |
| Checkbox | Checked changed, carrying the accepted Boolean value | Implemented named handler and specialization hook. Programmatic value setters remain silent. |
| Radio choice/group | Selection changed, identifying previous and new choice | Implemented bounded application-owned group and typed event after exclusive state settles. Group assignment is handwritten; no designer grouping editor yet. |
| Picker / wheel picker | Selection changed; edit committed/cancelled where the interaction supports editing | Implemented typed selection events and wheel commit/cancel lifecycle. Picker selection is immediate and has no separate edit session. |
| Text field / edit box | Text changed, edit committed, edit cancelled where supported; bounded accepted text and edit context | Implemented typed change/commit/cancel, explicit caller-owned rollback storage, validation before commit and cancellation on focus loss. A separate starter is deferred. |
| Drop-down list | Selection changed and committed/cancelled according to its interaction contract; item identity | Implemented in 0.2.11: borrowed immutable items, index identity, bounded native popup, tentative changes, explicit commit, cancellation on Back/outside press/focus loss, generated typed handlers. |
| Selectable list | Selection changed and item activated, with a row index mapped to an application ID | Implemented: `ListView` has 0–64 borrowed entries, bounded scrolling and separate typed handlers. The designer previews authored entries and generates the real list. Application `setItems()` and `setSelectedIndex()` are silent; the H750 Load menu is the consumer. |
| Table | Selection changed, row/item activated, and cell edit committed if editing is supported; item identity and column where relevant | `TableView` is currently a scrolling child container. Data, selection and optional editing are new contracts, not handler-only additions. |

The shared catalogue records supported events or an explicit no-application-event policy for every
widget. Tabs and softkeys retain numeric actions; named callbacks remain open. Scroll containers
handle scrolling internally. Do not advertise a callback whose control behavior or input path is unimplemented. Read-only
controls do not need synthetic interaction events merely to fill out the catalog.

### Dispatch and lifetime contract

- Separate input mechanics from semantic events. Pointer, key and encoder paths expressing the
  same accepted operation produce the same application event. Consuming a key does not itself mean
  a value changed; a release or scroll must not be relabelled as a click on every control.
- Distinguish changed, committed and cancelled. Changed reports an actual accepted value transition;
  committed reports explicit edit acceptance, even if unchanged where the control contract allows it.
  Cancel never emits commit. Each editable control must define its edit baseline, restoration and
  notification order, including focus loss; do not infer these from a generic action bit.
- Commit control invariants before notifications. A changed event does not fire for an unchanged
  value. A valid button click may fire without a value change. If one operation emits several distinct
  events (such as toggle change and click), specify and test their order and deliver each once.
- Preserve the control-hook and form-handler choices. A control hook handles reusable behavior;
  a named form handler coordinates application behavior. For each event the control hook precedes
  the form notification. Responsibilities must be deliberate so one application side effect is not
  repeated in both. Retain existing button semantics, including direct `activate()` calling only its
  control hook, unless an explicit compatibility change is approved and documented.
- Ordinary programmatic setters for the new event contracts update state without synthesizing user
  events. A notification-producing operation, if needed, must be explicit. Define its origin and
  dispatch behavior; do not silently change legacy setters or introduce feedback loops.
- Use typed source identity and typed bounded payloads, with no internal `void*` context, RTTI,
  hidden heap allocation or unbounded callback chains. Borrowed text/item data is valid only for
  the documented call lifetime; an application that retains or queues it copies into its own bounded
  message/storage. Do not allocate previous-text snapshots implicitly to implement change events.
- All dispatch and widget mutation run on the UI owner task. Keep nested dispatch rejected and
  prevent recursive activation. Handlers may change normal properties; they must not destroy,
  rebuild or reparent the active scene. Document attachment/detachment and owner lifetimes.
  Other tasks receive explicit application messages, not a framework-owned event queue per control.
- Fallible edits and application validation use checked results with documented rejection behavior.
  A post-change notification is not an implicit veto mechanism. Rejected input must not masquerade
  as a successful change or commit.

### Designer and generated code

Describe supported events with typed signatures in shared control/event metadata used by the service,
generator and VS Code inspector. Replace button-specific discovery with an Events section offering
Create/Open for the selected event. Illustrative names include `onEnableCheckedChanged`,
`onModeSelectionChanged` and `onSetpointEditCommitted`; exact C++ payload types are an implementation
design decision governed by the contracts above.

Bind by component identity plus event identity, not method spelling or numeric runtime ID alone.
Generate checked named overrides/accessors and validate event availability. Preserve handwritten
`form_handlers.hpp` bodies during generation, including multiple events on one control. Diagnose
renamed/deleted controls, removed events and signature changes with an actionable migration; do not
discard existing bodies. Keep current click bindings compatible. Derived controls use actual typed
storage with their own size, alignment and lifetime. Equivalent handwritten use remains supported.

### Implementation order and acceptance

1. Complete the real developer button-to-label walkthrough in `stm32h750b-dk-starter-1`, starting
   with handler discovery, then editing, regeneration and the existing Windows/H750 builds. Preserve
   the user's current form; update the completed sibling only as the lesson's expected result.
2. Establish the shared event descriptor/dispatch contract through checkbox, radio selection and
   picker/wheel-picker use cases. Include both a form-owned handler and a specialized control hook.
3. Bounded text editing now supplies explicit commit/cancel and checked failure behavior, with a
   generated setpoint consumer in the test suite. Owner decision (October 17): implement controls
   first and create separate control-specific starters later; do not add another application yet.
   Drop-down interaction is implemented using the same event system. Next define the table data and
   selection contract. Keep new application examples deferred until requested.
4. Once these interactions and their acceptance evidence are in place, proceed to native bitmap
   import/preview/generation and the separate font workflow.

For each increment, test the semantic event and actual application effect, not only that a stub
exists: correct source/value, exact counts/order, repeated unchanged values, cancellation, disabled
controls/ancestors, focus loss, invalid or capacity-exceeding input, silent programmatic refresh,
safe lifetimes and rejected reentrancy. Cover pointer and key/encoder paths supported by the control.
Radio-group invariants must hold when the handler runs. Text tests include UTF-8 and buffer bounds.
Table/drop-down tests include empty data, invalid selection and bounded model lifetime.

Exercise Create/Open, save/reopen, regenerate and deliberate rename/deletion with a retained handler
body; compile the generated C++ against the library and execute a handwritten consumer. Run the
same lesson application on Windows and separately on H750. Input not physically available on a board
must be recorded as host-only coverage, not target-tested. Report host tests, cross-builds and physical
execution separately, and run applicable conformance/static-analysis/budget gates for core changes.
Each lesson retains its existing platform presets and debug launchers. No designer build/run buttons
or separate feature-specific application configurations are introduced.

## Subsequent increments

- Complete the [control-event milestone](#control-event-milestone) first. RGB/native project creation
  and display/input configuration already exist; extend their shared engine rather than duplicating it.
- Bring prepared bitmap import, resource assignment, native encoding and actual resource preview
  into VS Code under the [asset contract](DISPLAY_MODEL_PLAN.md#source-validation-and-native-asset-encoding),
  followed by the separately scoped font workflow. General image preparation remains external.
- Improve distribution so compatible tools follow the pinned library revision without
  asking the developer to understand internal tool paths.
- Test remote (SSH, WSL, container) workspaces explicitly; the extension still refuses them.
- Exercise the full hardware tutorial from VS Code while preserving the equivalent
  command-line/manual file workflow.
- Extend the existing shared-application example with the accepted control/resource workflows.
  Build and debug continue through the application's own targets and IDE commands.

Surface validity remains independent of MCU budgeting. Simulator work must not
require a board selection. The extension does not select an MCU, allocate a physical
framebuffer, change linker scripts or select a connected programmer.
