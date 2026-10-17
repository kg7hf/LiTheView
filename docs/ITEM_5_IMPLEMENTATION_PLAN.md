# Item 5 implementation plan

This plan turns [DESIGNER.md](DESIGNER.md) into reviewable, independently gated work. Item 5 is too
large for one branch or pull request. Each phase lands on `main` before a later phase depends on it,
and every phase leaves existing examples and supported profiles usable.

## Program rules

- Use one `codex/item-5-*` branch per reviewable phase or widget slice.
- Keep designer dependencies outside `LitheView::Core` and normal package consumers.
- Add no automatic network, extension download, push, or hosted-CI requirement.
- Use targeted local commands by default; aggregate local/hosted runners remain optional.
- Treat surface, XML, generated C++, schema, resource, and extension formats as versioned designer
  interfaces. Keep optional target/deployment profiles outside the designer project contract.
- Deliver widgets through runtime, schema, preview, serialization, generation, docs, and tests.
- At every gate, inspect the whole first-party C/C++ tree and retire a bounded set of existing
  Core-Guidelines/style advisory debt. Hard diagnostics remain zero.
- Do not use the shared H750 board until the user authorizes board use for that phase.

## Phase map

| Phase | Outcome | Depends on |
| --- | --- | --- |
| 5.0 | Architecture and risk-spike contract | Item 4 merged |
| 5.1 | Display-surface and project contracts | 5.0 |
| 5.2 | Shared widget schema compiler and generated metadata | 5.1 |
| 5.3 | Headless document compiler and deterministic exports | 5.1, 5.2 |
| 5.4 | Isolated real-renderer preview worker | 5.1-5.3 |
| 5.5 | Classic RAD walking-skeleton designer | 5.1-5.4 |
| 5.6 | Resource studio and surface-compatible variants | 5.5 |
| 5.7 | Runtime widget/state/theme vertical slices | 5.2-5.6 |
| 5.8 | Extension SDK and dropdown-picker proof | 5.7 |
| 5.9 | Classic RAD productivity and reusable forms | 5.5-5.8 |
| 5.10 | Integration, hardware validation, and hardening | All prior phases |

Phases 5.6, 5.7, and 5.9 should be split into smaller pull requests. Their gates do not change.

Implementation status on 2026-09-01: phases 5.0 through 5.5 are merged on `main` at `bff2d6b`.
Phase 5.6A on `codex/item-5-resource-manifests` implements the resource manifest, artifact locking,
policy, surface-compatible variant selection, checked fixture, CLI, and deterministic project
identity. Deployment storage, linker placement, board/MCU/RTOS, and target budgets remain separate
application-integration concerns. Phase 5.6B on the same branch implements font
face/size/weight/repertoire selection, alpha1/alpha8 conversion, preview, and contained import. Phase
5.6C implements deterministic raster image/icon crop, scale, alpha, palette, mask/tint, dither,
preview, CLI conversion, and contained import. Phase 5.6D implements deterministic sprite-grid
animation conversion, playback preview, timing/repeat metadata, shared palettes, and contained frame
import. The first Phase 5.7 slice implements bounded color tokens and common widget-state resolution.
The second slice implements text, icon, momentary, and toggle buttons, the third implements
the tab bar and `TabView`, and the fourth implements the touch/encoder scroll-wheel picker; all are on
`codex/item-5-resource-manifests`. The overlay panel and slide-up keyboard are next. Designer features for each slice land in the VS Code
extension; the standalone designer is kept building but not extended.

## 5.0 Architecture and risk spikes

### Deliverables

- Approve `docs/DESIGNER.md` and this plan as the item 5 baseline.
- Record surface, project, schema, preview, extension, deployment-boundary, and embedded-boundary
  decisions.
- After design approval, create a minimal `tools/designer` C#/.NET 10 solution skeleton.
- Prove three disposable spikes before committing a large framework:
  - a custom WinForms canvas overlays selection/resize adorners on a native pixel image;
  - C# starts, version-negotiates with, stops, and restarts a native preview worker; and
  - one JSON descriptor generates equivalent C# and C++ metadata.
- Convert useful spike behavior into tests or decision notes and discard unused spike code.

### Gate

- The owner accepts the display-first classic RAD workflow and explicit non-goals.
- No choice requires a second embedded runtime.
- The three walking-skeleton risks have measured evidence and a selected path.
- Existing C++ gates remain unchanged and green.

## 5.1 Display surfaces and project contracts

This phase comes before widgets because shape, geometry, color depth, native pixel representation,
safe area, and input capabilities define what a widget document means. Target board, rendering and
allocation strategy, bus, RTOS, linker, and storage choices do not.

### Deliverables

- Define strict versioned JSON schemas for display surfaces and the project manifest.
- Model rectangle, square, and circle; physical and pixel dimensions; orientation; surface/native
  formats; safe area; and input capabilities.
- Define surface compatibility-family and explicit-override rules.
- Hash the resolved surface and carry the identity into validation/export reports.
- Add a headless create/list/resolve/validate command and C# immutable model.
- Define explicit surface migration diagnostics without implementing the complete GUI wizard.
- Retain target profiles only as an optional downstream conformance/deployment input; they are not
  selected by, copied into, or hashed into a designer project.

### Gate

- Invalid geometry, shape, color/native-format combinations, rotations, and paths fail with stable
  diagnostics.
- Known surfaces reproduce existing example dimensions, formats, and native quantization behavior.
- Equivalent input resolves and hashes byte-for-byte identically.
- Changing pixel dimensions or shape is detected as a migration, never a transparent edit.
- Profile code adds no dependency to `LitheView::Core`.

## 5.2 Shared widget schema compiler

### Deliverables

- Add strict `widgets.schema.json` and the built-in `widgets.json` catalog.
- Model every current `ViewKind`, XML element/class, property, default, range, action, binding, state,
  parent/child rule, input capability, and resource dependency.
- Implement a deterministic headless schema compiler.
- Generate immutable C# descriptors, C++ metadata, and property-reference documentation.
- Add compatibility/version rules and deterministic migration hooks.
- Add a generated-artifact freshness check.

### Gate

- The schema describes all existing first-party widgets and supported XML properties.
- C# and C++ descriptor snapshots are semantically identical.
- Duplicate identities, invalid defaults/ranges, unknown types, and incompatible changes fail closed.
- Regeneration from a clean tree is byte-for-byte stable.
- Current XML/layout tests remain green.

## 5.3 Headless document compiler and exporters

### Deliverables

- Add typed form/resource/action/binding models to `LitheView.Designer.Core`.
- Import compatibility and strict-mode LitheView XML without executing extensions.
- Write canonical XML with stable order and strict self-parse.
- Generate Allman C++23 `constexpr ViewSpec` and resource-name arrays.
- Add stable component identity, runtime ID, symbol, action, binding, theme, and resource resolution.
- Add stable `LITHEVIEW USER CODE BEGIN/END` regions to generated integration files and preserve their
  exact contents across regeneration.
- Fail closed on missing, duplicate, nested, mismatched, or orphaned user regions; keep the original
  file and write a separately named candidate for manual reconciliation.
- Add CLI commands for import, normalize, validate, XML/C++ export, and budget reporting.
- Produce machine-readable and human-readable diagnostics with stable codes.

### Gate

- Every supported property survives XML -> model -> canonical XML -> C++ without loss.
- Canonical XML reaches a fixed point after one normalization.
- XML-built and C++-built layouts have identical typed specs and render signatures.
- Repeated generation preserves user regions byte-for-byte; malformed and orphaned-region fixtures
  prove that no existing developer code is overwritten or silently dropped.
- Generated C++ passes warnings, format, Core Guidelines, host, M33, and M7 compile gates.
- A headless build reproduces output without opening the GUI.

## 5.4 Native preview worker

### Deliverables

- Define the versioned C ABI and length-bounded worker protocol.
- Link a native worker to the real LitheView core and generated schema metadata.
- Render immutable snapshots into caller-owned RGB565, RGB888, XRGB8888, and ARGB8888 buffers.
- Return dirty regions, animation wakeup state, timing, and structured diagnostics.
- Support round/rectangular masks and surface-native quantization.
- Simulate pointer, key, encoder, push-button, focus, and elapsed-time input.
- Add timeout, cancellation, stale-response, crash-restart, and protocol-mutation tests.

### Gate

- Preview frames match existing core goldens for each checked surface/profile.
- Tiled previews match full-frame output where applicable.
- Invalid sizes, versions, schema, resources, and messages fail without partial success.
- Terminating the worker leaves the CLI/designer and project files intact.
- The worker opens no project paths and performs no network access.

### Result

The checked worker now negotiates protocol/ABI/schema/catalog identity; drives the real parser,
fixed-capacity layout, application/controller, full-frame renderer, and `TiledRenderer`; and supports
all four public host formats, shaped viewports, target quantization, and all planned input classes.
Managed containment tests cover deadline, cancellation, stale/hostile/unknown records, missing
resources, schema mismatch, forced process death, restart, and project immutability. Debug/Release
managed suites pass 45 of 45, each of the four Win32 profiles passes 3 of 3 native/core/cast tests, and
the whole-tree advisory inventory is reduced from 908 to 896 normalized sites. No board was accessed.

## 5.5 Classic RAD walking skeleton

### Completed result

- Checked or custom surface definition precedes project creation.
- Project creation is staged, fail-closed, non-overwriting, and retains an exact local surface
  snapshot.
- The classic work areas, initial six-widget palette, synchronized multi-selection, group drag/nudge,
  drag move/resize, duplicate, hierarchy reparent, z-order, in-place text, configurable grid/snap,
  schema-driven property editing, stable action assignment, delete, one-transaction undo/redo, dirty
  state, atomic save, native preview, and protected C++ export are implemented.
- Debug and Release builds have zero warnings; the cross-platform core suite passes 50 of 50 and the
  Windows UI shell/accessibility/editor suite passes 5 of 5 in both configurations. The full Roslyn
  information-level gate passes, and a hidden Release startup smoke opens a checked project and stays
  live until deliberately terminated. No embedded board was accessed. Phase 5.5 is complete; Phase
  5.6 resource manifests and deterministic conversion are next.

### Deliverables

- Add a new-project wizard whose first page defines/selects the display surface.
- Create/open/save projects/forms with atomic save.
- Add project explorer, palette, canvas, hierarchy, Properties/Actions inspector, output panel, and
  component tray.
- Start with panel, label/static text, button, image, line, and progress components.
- Add selection, multi-select, placement, move, resize, reparent, delete, duplicate, z-order, exact
  geometry, keyboard nudging, in-place text, grid/snap lines, safe-area display, and zoom.
- Implement command-based undo/redo and clean/dirty tracking.
- Drive canvas pixels exclusively through the native worker with C# adorners above them.
- Export and validate through the headless services.

### Gate

- A cold developer selects a display, creates a form, places static text/controls, and exports it
  without editing markup.
- Canvas/hierarchy selection remains synchronized through undo, redo, and reparenting.
- A drag or property edit is one atomic undo transaction.
- Saving/reopening preserves semantic and canonical output.
- Keyboard-only editing and baseline accessibility automation pass.
- Native preview/export equivalence remains checked.

## 5.6 Resource studio and surface-compatible variants

Split font, image/icon, and animation work into separate pull requests if necessary.

### Phase 5.6A completed foundation

- `schema/resource-manifest.schema.json` and immutable managed models define the source, provenance,
  license/policy, converter, generated output, logical integration identity, and surface-compatible
  variant contract.
- Canonical JSON and SHA-256 identity make source or option changes review-visible.
- The CLI validates/canonicalizes the contract and verifies source, output, and license artifacts.
- Project resolution selects exactly one variant compatible with the display surface's painter format
  and incorporates resource identity into the resolved project hash.
- Board, processor, bus, RTOS, linker section, alignment, internal/external flash, QSPI, and target
  capacity are deliberately absent from the resource and project manifests. A consuming application
  decides those deployment details after UI export.
- The alpha1 status icon is a deterministic manifest/integration fixture, not the final image
  converter or a substitute for Phase 5.6 converter goldens.

### Phase 5.6B implemented font slice

- The checked Windows GDI+ converter emits strict v2 metrics with converter identity, visual/raster
  style, weight, generated symbols, canonical repertoire, source/checksum identity, and measured
  record/coverage payload.
- The managed conversion runner uses argument-list process creation, bounded request validation,
  cancellation that terminates the converter process tree, and strict metrics loading.
- The visual resource editor selects a caller-owned TTF/OTF, pixel size, actual regular/bold outline,
  visual style/weight, alpha1/alpha8 coverage, exact repertoire, and provenance/license evidence. Its
  local preview loads the chosen face without installing it.
- Import stages and verifies all artifacts before moving a new contained resource into place. It
  never overwrites or merges a resource directory, atomically updates the project manifest, and adds
  the generated C++ entry point as a logical form font resource.
- `font convert` exposes the same service to headless builds. The checked regular/bold metrics and
  fake-runner transaction tests keep deterministic identities and failure behavior reviewable.
- No font editor or manifest field names a target, board, processor, bus, RTOS, linker section,
  internal/external flash, QSPI placement, alignment, or target budget.

### Phase 5.6C implemented image and icon slice

- The checked GDI+ converter accepts PNG/BMP/JPEG/GIF/TIFF raster sources and emits allocation-free
  native `Bitmap` C++ for alpha1, alpha8, indexed4, RGB565, RGB888, XRGB8888, and ARGB8888.
- Crop, output geometry, nearest/bilinear sampling, preserve/threshold/opaque alpha, matte, ordered
  4-by-4 dither, tint, and explicit indexed4 palette are strict, hash-visible conversion inputs.
- **Resources > Import Image** and **Import Icon** provide a live crop/tint preview and measured
  payload estimate. Tintable masks are limited to alpha1/alpha8 icons; fixed-color images keep their
  color semantics.
- Import uses the same no-overwrite staging, artifact verification, atomic project update, and logical
  form-resource integration policy as font import. `image convert` exposes the same runner to
  headless builds.
- A real two-pass indexed4 golden locks source SHA-256, 384 pixel bytes, 40 palette bytes, all FNV-1a
  checksums, byte-identical metrics/C++, generated formatter guards, and native compile validity.
- The converter does not silently select an SVG engine. Reviewed vector art may be preserved as
  source evidence, but this checkpoint requires an explicit lossless raster export for conversion.
- The editor and manifest continue to exclude target, board, processor, bus, RTOS, linker, flash/QSPI
  placement, alignment, and target-budget policy. They report representation bytes only.

### Deliverables

- Add manifests, hashes, converter versions/options, provenance, and license notes.
- Add font face/size/weight/repertoire selection and alpha1/alpha8 generation.
- Add image/icon crop, scale, format, alpha, palette, mask/tint, and dither controls. (Phase 5.6C
  complete for deterministic raster sources.)
- Add animation frame/sprite import, ordering, timing, repeat, palette, and playback preview. (Phase
  5.6D complete for deterministic row-major raster sprite sheets.)
- Generate compact/high-fidelity variants without runtime decoders.
- Report deterministic artifact byte counts without assigning them to a hardware storage region.
- Keep optional deployment sizing and target-profile analysis in a separate command whose results do
  not alter designer project identity or form serialization.

### Phase 5.6D implemented animation slice

- The checked animation converter extracts 2-256 equal-sized frames from an explicit row-major
  source grid and passes every frame through the same deterministic native `Bitmap` packer as image
  conversion. It concatenates one immutable pixel payload and shares one indexed4 palette.
- **Resources > Import Animation** (`Ctrl+Alt+A`) exposes source grid, output size, sampling, native
  format, alpha, tint, matte, palette, dither, interval, repeat index, and autoplay. Its live preview
  plays the declared sequence and pauses/resumes on click.
- Import is transactional and adds stable logical bitmap names for every frame as one undoable edit.
  The generated C++ returns a borrowed `std::span<const Bitmap>` plus timing-default accessors; the
  consuming application maps those frames through its existing `ResourceProvider`.
- `animation convert` exposes the same checked service to headless builds. A real two-pass indexed4
  golden locks eight 96-by-72 frames, a 27,648-byte pixel payload, one 40-byte palette, source and
  FNV-1a identities, and byte-identical C++/metrics.
- Frame timing remains caller-clocked. The generated resource introduces no decoder, heap, timer,
  thread, filesystem, or deployment policy. The complete integration and scaling walkthrough is in
  `docs/ANIMATIONS.md`.

### Gate

- Identical source/options produce identical payload and hashes.
- Font, alpha/color, and animation results match converter/renderer goldens.
- Each resource has a complete ownership/lifetime/integration description.
- Missing license/provenance can be configured as a product-build error.
- One UI project resolves identically regardless of whether a consuming application later places its
  artifacts in U545 internal flash, H750 internal/external flash, host files, or custom storage.

## 5.7 Widget, state, and theme vertical slices

Implement reviewable slices in dependency order:

1. bounded theme tokens and common widget-state resolution;
2. text, icon, momentary, and toggle buttons;
3. tab bar and `TabView`;
4. touch/encoder scroll-wheel picker;
5. overlay panel and slide-up keyboard;
6. virtualized list/data-provider contract; and
7. validation/error presentation and focus/action integration.

Every slice includes bounded runtime C++, schema/metadata, XML/C++ export, palette/editors, native
preview, input modes, validation, example, goldens, M33/M7 budgets, docs, and a project-wide advisory
ratchet. A widget is not complete while it exists in only one path.

### Slice 2 result: buttons

- Runtime: `ButtonView` gains `ButtonBehavior::momentary|toggle`, an optional borrowed icon tinted
  by the resolved foreground for alpha formats, and a latch bar. The retained `selected` state is a
  spare `View` flag shared with checkboxes and radio buttons, with precedence disabled, pressed,
  focused, selected, normal and optional selected colors. `View` stays 80 bytes, `ViewSpec` 120,
  `ButtonView` 108 of a 112-byte ceiling, and `StaticLayout<16>` 1,816 on a 32-bit target.
- Schema and metadata: `button-behavior`, `icon`, `selected-background-color`, and
  `selected-foreground-color` are additive, compatible catalog properties; catalog identity
  `d297c183...` with 47 properties and 15 widgets. Generated C#/C++/reference artifacts are fresh.
- XML and C++: strict `behavior`, `icon`, and selected-color attributes; strict rejection of checked
  momentary buttons and of `behavior` on other widgets; writer round trip; generated
  `ButtonBehavior` enumerators and selected state colors. The generator now builds each sparse
  state-style entry locally because GCC 13 constant evaluation zero-filled omitted members of
  entries reached after skipped predecessors.
- Designer: palette Button, enum/token/resource property editors, `LVFD1132`, atomic
  momentary-clears-checked edits, starter `theme.selected`/`theme.on-selected` tokens, and CLI
  `preview render --input` steps.
- Evidence: host goldens and tile equality in RGB565/XRGB8888, input/damage/state tests, generated
  equivalence for `tests/designer-fixtures/button-variants-smoke`, real-worker preview input tests, and
  M33/M7 archive budgets. Icon payloads are not yet transported to the editing preview worker.

### Slice 3 result: tabs

- Runtime: `TabBarView` (an equal-width strip of up to 16 borrowed pipe-delimited titles) and
  `TabView` (a strip plus one container page per tab, page *i* visible only while tab *i* is
  selected). Left/right step and report the action through a new one-bit `View` action request that
  `ViewController::handleKey()` honors; select and encoder push advance and always wrap; a pointer
  selects the tab under the release, with drag-off cancellation. Each tab resolves the common state
  precedence, and the selected tab carries a foreground underline. `View` stays 80 bytes,
  `ViewSpec` 120 (`tabHeight` uses existing padding), `TabBarView` 96, `TabView` 100 of a 112-byte
  ceiling, and `StaticLayout<16>` 1,816 on a 32-bit target.
- Schema and metadata: `lithe.widget.tab-bar` and `lithe.widget.tab-view`, a `tab-height` property,
  and Panel/Scroll Panel/Table pages under a tab view; catalog identity `9cffe8a3...` with 48
  properties and 17 widgets, checked compatible with the previous catalog.
- XML and C++: `tabbar`/`tabview` elements and aliases, `LCDTABBARVIEW`/`LCDTABVIEW`, strict title
  count, selection, and `tabHeight` rules, writer round trip, and generated `ViewKind::tabBar`,
  `ViewKind::tabView`, and `spec.tabHeight`.
- Designer (VS Code): the palette adds into the selected container, so a Panel added to a tab view
  becomes a page filling the content area; new tab widgets start with two titles; Selected Index is
  the design-time page selector; the service marks unselected pages `previewHidden` and the canvas
  skips them; `LVFD1133` (error) and `LVFD1134`/`LVFD1135` (page-structure warnings).
- Evidence: host goldens and tile equality in RGB565/XRGB8888, input/action/damage/focus tests,
  generated equivalence for `tests/designer-fixtures/tab-view-smoke`, native preview tab input tests, C#
  and service tab tests, Node palette/hit-test tests, and M33/M7 archive budgets. Every
  `StaticLayout` user grows by about 3 KB of Cortex-M33 text.

### Slice 4 result: wheel picker

- Runtime: `WheelPickerView`, a vertical column of 1 through 255 borrowed pipe-delimited items with
  the selection centered in a band of `rowHeight` pixels (default 28). Focus and editing are
  separate: up/down and encoder rotation move focus until select or an encoder push starts editing;
  while editing they step the value and report the action through the existing key action request,
  back restores the value from before editing, and losing focus ends editing and keeps the value. A
  drag scrolls and snaps on release (reported even when it lifts outside); a tap selects the row
  under it. `View` gains a protected `focusChanged()` hook (no storage), and the controller reports
  an action requested by a release that lifts outside the widget. `View` stays 80 bytes, `ViewSpec`
  120 (`rowHeight` uses existing padding), `WheelPickerView` 108 of a 112-byte ceiling, and
  `StaticLayout<16>` 1,816 on a 32-bit target.
- Schema and metadata: `lithe.widget.wheel-picker` and a `row-height` property (12-255); catalog
  identity `9a4ba555...` with 49 properties and 18 widgets, checked compatible with the previous
  catalog.
- XML and C++: `wheelpicker`/`wheel-picker` elements, `LCDWHEELPICKERVIEW`, strict item count,
  selection, and `rowHeight` rules, writer round trip, and generated `ViewKind::wheelPicker` and
  `spec.rowHeight`.
- Designer (VS Code): Wheel Picker in the Input palette group with five starter items; `LVFD1136`
  (error) for the item count and selection. The palette now leaves every new widget's size to the
  schema default instead of a fixed 100 by 36.
- Evidence: host goldens and tile equality in RGB565/XRGB8888, focus/editing/step/back/wrap/
  focus-loss/tap/drag/cancel/disabled tests, generated equivalence for
  `tests/designer-fixtures/wheel-picker-smoke`, native preview wheel input tests, C# and service wheel
  tests, Node tests, a headless run of the real webview, and M33/M7 archive budgets. Every
  `StaticLayout` user grows by about 2.6 KB of Cortex-M33 text.

## 5.8 Extension SDK and dropdown-picker proof

### Deliverables

- Define extension manifest, lock file, namespaced IDs, compatibility, and hashes.
- Load schema fragments without executing extension code.
- Add explicit exact-hash trust for optional managed editors/native preview modules.
- Add narrow property/component/resource editor services.
- Load native modules only in the crash-contained worker.
- Publish an extension template, pack/validate CLI, SDK guide, and tests.
- Implement a dropdown picker outside designer core as the reference extension.

### Gate

- Installing the reference adds palette, properties, actions, preview, XML, C++, docs, and tests
  without editing designer-core source.
- Missing, mismatched, duplicate, and untrusted packages fail deterministically.
- Schema-only inspection/export executes no extension code.
- A crashing native module cannot corrupt the project or designer process.

## 5.9 Classic RAD productivity and reusable forms

### Deliverables

- alignment/distribution/same-size/order tools and keyboard commands;
- rulers, persistent guides, configurable grids, snap policy, and lock state;
- focus-order editor and action/code navigation;
- collection, color, theme, font, image, animation, and binding editors;
- cross-form/project copy/paste with resource conflict handling;
- nonvisual component tray behavior;
- reusable composite components and templates;
- single-base form inheritance with inherited/local visualization; and
- workspace persistence and large-document performance work.

### Gate

- Common work requires no markup or modal-dialog maze.
- Multi-selection, guides, paste, templates, and inheritance are deterministic and undoable.
- Inheritance cycles, missing bases, and sealed overrides fail before export.
- A cold developer completes the repository tutorial and extension walkthrough.
- UI automation covers mouse/keyboard workflows at supported DPI settings.

## 5.10 Integration, hardware validation, and hardening

### Deliverables

- Build a common XML and generated-C++ demonstration.
- Exercise tabs, buttons/states, picker/list, validation, resources, keyboard overlay, and an extension.
- Run Win32 RGB565/high-color previews for the exact selected display surfaces.
- Cross-build the same layout/resources for U545/M33 and H750/M7.
- With explicit board permission, flash and inspect both H750 display profiles.
- Record measured flash/RAM/stack/render/transfer separately from estimates and host evidence.
- Complete user, framework-author, extension-author, resource, migration, and troubleshooting guides.
- Package CLI, worker, designer, schemas, and SDK with exact version reporting.
- Perform final project-wide conformance, safety, style, and Core Guidelines ratchet.

### Gate

- All completion criteria in `DESIGNER.md` and `MODERNIZATION_PLAN.md` pass.
- XML and generated C++ render equivalently on checked profiles.
- U545 hard deployment budgets pass; H750 high-fidelity/QSPI placement, when used, is explicit in the
  consuming application rather than the designer project.
- Hardware results identify branch, commit, build UTC, surface/target profile, and resource hashes.
- No critical warning, analysis, formatting, test, or budget diagnostic remains.
- Remaining advisory debt is smaller and documented rather than hidden.

## Recommended first implementation branch

After design approval, start `codex/item-5-display-profiles` with phase 5.1. The first implementation
commit should contain only:

- JSON schemas and immutable C# records for display surfaces and designer projects;
- one rectangular RGB565 profile and one 240 by 240 round profile;
- deterministic resolve/hash/validate CLI behavior; and
- tests proving shape, pixel dimensions, color depth, and invalid combinations affect output.

Expand to every existing display surface only after that thin path is reviewed. Add target deployment
analysis independently. This anchors all later widget, resource, preview, and code-generation work in
the developer's real first decision: the display.
