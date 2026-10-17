# LitheView modernization plan

Status: approved development direction as of 2026-08-29.

This plan defines the next five modernization tasks after the initial LitheView baseline. The order is
intentional: first preserve behavior, then improve the rendering foundation, then build text and
interaction features on that foundation, and finally expose the expanded system through widgets and a
new designer.

## Definition of done for every task

Every task must preserve these project-wide constraints:

- Follow the C++ Core Guidelines and the LitheView style guide.
- Keep the portable library independent of an OS, RTOS, filesystem, device driver, and settings store.
- Use C++23 without exceptions or RTTI in the portable and embedded configurations.
- Perform no heap allocation during parsing, layout, input dispatch, animation, or rendering.
- Express ownership, lifetimes, bounds, and failure explicitly; keep casts at audited external API
  boundaries.
- Retain caller-selected fixed capacities and a viable Cortex-M33 tiled-rendering configuration.
- Measure worst-case flash, static RAM, stack, rendering time, and transfer behavior on representative
  targets.
- Keep Win32, Cortex-M33, and Cortex-M7 builds and their relevant regression tests green.
- Run the complete first-party Core Guidelines and style inventory at every item gate. Treat the
  advisory findings as a ratchet: each item must retire a reviewed subset and record the smaller
  remaining bundle, but an item is not required to complete unrelated large-scale modernization.

## 1. Automated conformance, regression, and performance gates

### Goal

Turn the baseline tests and audits into a permanent compatibility harness before expanding public APIs.

### Scope

- Add continuous integration for Windows and Linux host builds.
- Cross-compile representative Cortex-M33 and Cortex-M7 configurations.
- Add pixel-for-pixel golden tests for full-frame and tiled rendering.
- Exercise supported pixel clipping, transparency, scrolling, animation, focus, controller-stack, and
  pointer-capture behavior.
- Expand XML parser/writer round-trip, malformed-input, fixed-capacity, and fuzz/property testing.
- Record flash, static RAM, stack, render-time, dirty-region, and animation-wakeup budgets.
- Test representative build options, including exceptions and RTTI disabled.
- Keep vendor/generated sources outside first-party static-analysis policy while compiling the real
  integration configurations.

### Completion gate

The host matrix, M33 and M7 cross-builds, safety analysis, XML tests, and rendering golden tests run from
one documented command or CI workflow. Budget regressions fail visibly rather than silently increasing
resource use.

### Implementation checkpoint — 2026-08-29

Implemented on `codex/item-1-conformance-gates`:

- one-command Debug/Release host, package-consumer, M33, and M7 gates;
- Windows/Linux CI plus an offline Arm cross-build matrix and logged budget reports;
- exact full-frame/tiled RGB565 golden signatures and arbitrary-tile pixel equality;
- deterministic XML mutation, round-trip, malformed-input, and capacity coverage;
- warnings as errors, critical Cppcheck/Clang-Tidy profiles, cast auditing, and
  `-Wdouble-promotion`;
- host footprint/timing ceilings plus Arm archive text/data/BSS and stack-frame gates; and
- offline Cortex-M33 and Cortex-M7 cross-builds that do not require physical-board use.

All available local gates passed. The checked-in GitHub Actions workflow is manual-only and selectable
by scope because hosted minutes and local-runner capacity are constrained. A hosted run is optional
evidence rather than a merge prerequisite; do not call an unrun remote scope green.

## 2. Generalize rendering and pixel formats

### Goal

Remove RGB565 as a universal surface assumption while retaining it as the efficient small-target
default.

### Scope

- Define explicit RGB565, RGB888, XRGB8888, and ARGB8888 format contracts.
- Keep logical colors lossless until conversion at a rendering or display boundary.
- Use compile-time traits or specialized rasterizers so pixel-format selection does not add a virtual
  dispatch per pixel.
- Preserve caller-owned full-frame and tiled surfaces.
- Support opaque, one-bit-alpha, and anti-aliased-alpha assets consistently.
- Add optional adapter hooks for DMA, DMA2D, LTDC layers, asynchronous transfers, and completion
  signaling without making them portable-core dependencies.
- Validate every format against the same logical reference scenes and clipping rules.

### Completion gate

The same demo and golden scenes render correctly through RGB565 and at least one 24/32-bit format. The
M33 configuration continues to operate with a small caller-owned tile and does not acquire a full-frame
or 32-bit-buffer requirement.

### Implementation checkpoint — 2026-08-29

Implemented on `codex/item-2-pixel-formats`:

- compile-time `PixelSurface<Format>` and `PixelFormatTraits<Format>` contracts for RGB565, RGB888,
  XRGB8888, and ARGB8888;
- one command-level format dispatch into specialized raster loops, with no format branch or virtual
  call per pixel;
- opaque, one-bit-alpha, straight-ARGB, and anti-aliased-alpha bitmap paths;
- separate Win32 RGB565 and RGB888-output/XRGB8888-framebuffer simulator variants plus
  RGB565/XRGB8888 full-frame and arbitrary-tile goldens;
- separate STM32H750B-DK RGB565 and RGB888-output/XRGB8888-framebuffer build variants;
- a format-preserving tile-sink contract and optional DMA/DMA2D/LTDC/asynchronous transfer hooks; and
- an unchanged 3,840-byte RGB565 M33 strip, with measured Arm archive text of 54,645 bytes and a
  maximum reported bounded function frame of 872 bytes.

The standalone H750 image cross-links at 69,144 bytes text, 188 bytes data, and 35,532 bytes BSS. With
active-session authorization, the committed `8ad8abf` image was programmed and verified on the shared
STM32H750B-DK. A non-halting hot-plug read found the core running, task status `0x4C560004`, frame count
one, touch count zero, and the expected repeated RGB565 `0x19AA` menu-header value in SDRAM. This is an
operational board smoke test; no visual LCD or touch-interaction observation was made during that test.

## 3. Modern font and text asset pipeline

### Goal

Add deterministic, ROM-resident typography suitable for modern instrument displays while retaining
the compact 5 by 7 font as a permanent compile-time option for bring-up and constrained targets.

### Scope

- Create an offline converter for selected TTF/OTF inputs and any required historical SIF assets.
- Generate immutable glyph tables, metrics, coverage metadata, checksums, and resource manifests.
- Support regular, semibold/bold, underline, per-run color, alignment, and line spacing.
- Add anti-aliased glyph coverage when the selected target format permits it.
- Add accurate measurement, clipping, wrapping, ellipsis, and multiline layout.
- Begin with product-selected UTF-8 repertoires instead of embedding an unbounded Unicode font.
- Allow one-bit glyph coverage for the smallest profile and higher-quality coverage for larger targets.
- Keep runtime text measurement and rendering allocation-free and filesystem-free.

### Completion gate

A generated font renders consistently in Win32, RGB565 embedded, higher-color, full-frame, and tiled
paths, with exact backend/format-specific goldens. Tests cover missing glyphs, invalid UTF-8 policy,
measurement, wrapping, clipping, and capacity failures. The generated manifest reports the exact
flash cost and supported character repertoire.

### Implementation checkpoint — 2026-08-29

Implemented on `codex/item-3-font-pipeline` while retaining the compact 5 by 7 backend as an option:

- an offline, deterministic TTF/OTF converter emits sorted ROM glyph records, alpha1 or alpha8
  coverage, source hashes, checksums, bounded repertoires, and JSON manifests;
- separate regular and designed-bold DejaVu Sans 14-pixel assets cover 99 selected glyphs, with a
  combined 19,519-byte table payload isolated in `LitheView::GeneratedFonts`;
- template selection and `LITHEVIEW_DEMO_FONT_BACKEND` choose compact or generated fonts at compile
  time; `AUTO` pairs RGB565 examples with compact 5 by 7 and the RGB888-output/XRGB8888-framebuffer
  examples with Generated Sans, while explicit overrides remain available;
- compact H750 link maps contain no generated glyph tables;
- allocation-free UTF-8 measurement and drawing support replacement-or-stop policy, word/character
  wrapping, clipping, ellipsis, maximum lines, multiline alignment, line spacing, underline, and
  per-run color;
- host tests compare full-frame and arbitrary-tile generated text for RGB565, RGB888, XRGB8888, and
  ARGB8888 and cover fallback glyphs, invalid UTF-8, CRLF, wrapping, clipping, and line capacity; and
- both compact and generated font selections pass Win32, installed-package, Cortex-M33, Cortex-M7,
  and standalone H750 RGB565/RGB888 offline builds.

The M33 archive totals are 58,835 bytes text for compact and 78,998 bytes for generated selection;
the M7 totals are 58,161 and 78,332 bytes. All report an 872-byte maximum bounded stack frame. Linked
H750 RGB565 text/data/BSS are 72,976/188/35,532 bytes compact and 92,496/348/35,532 bytes generated;
RGB888 values are 72,988/188/35,532 and 92,508/348/35,532 bytes. These are offline build and size
results; task 3 did not connect to or execute on the shared board.

## 4. Bounded scenes, overlays, transitions, and gestures

### Goal

Provide modern touch interaction and motion without putting gesture policy in individual views or
requiring two full framebuffers.

### Scope

- Add timestamps to portable input events.
- Place tap, long-press, drag, and four-direction swipe recognition between the platform adapter and MVC
  dispatch.
- Preserve the existing contact-ID, capture, cancellation, and release-inside activation guarantees.
- Add a fixed-capacity overlay/scene stack for dialogs, menus, and keyboards.
- Add slide-in, slide-out, drop-away, and left/right/top/bottom screen transitions.
- Render outgoing and incoming scenes through bounded translated/clipped regions or tiles.
- Retain application-owned clocks, explicit animation activity, and damage-limited redraws.
- Define deterministic interruption and cancellation when navigation changes during a gesture or
  transition.

### Completion gate

The round Win32 acceptance example shows visible tap/drag feedback, swipe navigation, and a
slide-up/drop-away overlay without runtime allocation. M33 builds the same state machines and renders
them through its bounded tile configuration. Automated tests cover gesture thresholds, capture
conflicts, cancellation, interrupted transitions, edge coordinates, full-frame/tile equality, and
damage-limited updates after an overlay settles. The shared H750 example remains a consumer of the same
portable `Application` implementation; no physical-board access is required for this task.

### Implementation checkpoint

Item 4 is implemented on `codex/item-4-scenes-overlays` from merged item-3 tip `0ad49d6`:

- rectangular and circular `Viewport` contracts use integer pixel-center clipping with no mask or
  floating-point work;
- timestamped tap, long-press, drag, and four-way swipe recognition is centralized between the
  platform adapter and MVC dispatch;
- scenes and overlays share the fixed `config::kMaxControllerDepth` array, with explicit layer checks,
  pointer/gesture cancellation, input rejection during motion, and deterministic push/pop rollback;
- left/right/up/down/drop-away transitions render outgoing and incoming views through translated clips
  or the existing tile workspace, not duplicate framebuffers;
- the 240 by 240 round Win32/U545 example proves shaped clipping, visible tap/drag recognition,
  swipe-driven scene navigation, overlay composition, exact tile equality, and build-image
  identification; and

## 5. Widget expansion and schema-driven WYSIWYG designer

### Goal

Add the remaining modern controls and recreate the historical visual workflow without coupling desktop
tooling to the embedded runtime.

### Runtime scope

- Add a tab bar and `TabView`.
- Add standardized momentary, toggle, icon, and text-button variants.
- Add a touch-oriented scroll wheel/picker.
- Add overlay panels and a slide-up keyboard using the task 4 scene primitives.
- Add virtualized list behavior for data sets larger than retained view capacity.
- Define focused, pressed, selected, disabled, error, and validation visual states.
- Introduce bounded theme tokens for colors, typography, spacing, radii, and motion.

### Schema and designer scope

Make one typed widget/property schema drive:

- XML parsing and strict validation;
- deterministic XML writing;
- generated `constexpr ViewSpec` arrays for targets that omit the runtime parser;
- property metadata and resource validation used by the desktop designer; and
- generated reference documentation and compatibility tests.

Build the desktop designer around that schema with a palette, canvas, hierarchy tree, property
inspector, undo/redo, resource validation, and deterministic export. Preview through the real LitheView
host renderer so editor output matches embedded behavior.

### Completion gate

The designer round-trips every supported property through canonical XML, validates resource and
capacity errors before export, and generates both XML and compile-time layouts that render equivalently.
The common demo exercises tabs, scroll-wheel input, button states, and the slide-up keyboard on Win32
and H750, with the same layout cross-building for M33.

### Architecture checkpoint — 2026-08-30

Item 5 begins with the developer's actual first decision: the display surface. Shape, physical size,
pixel dimensions, orientation, color depth, native transfer format, full-frame/tiled rendering, safe
area, input capabilities, and resource budget are versioned project inputs because they change layout,
clipping, interaction, generated resources, memory, and transfer work. The designer never silently
rescales a form after one of those inputs changes.

`docs/DESIGNER.md` selects a Windows-first C#/.NET 10 Windows Forms shell with a custom classic-RAD
canvas, a real LitheView renderer in an isolated native worker, one JSON widget schema that generates
C# and C++ metadata, canonical XML plus deterministic C++23 export, offline resource conversion, and a
versioned/trusted extension model. It explicitly excludes QML/XAML-style application languages,
scripting, product-logic generation, drivers, storage, and a second embedded runtime.

Generated integration code contains stable, fail-closed user-code save zones. Regeneration preserves
their exact contents; malformed markers or removed/orphaned regions prevent overwrite and produce a
separate reconciliation candidate rather than discarding developer work.

`docs/ITEM_5_IMPLEMENTATION_PLAN.md` divides the effort into gated phases. Display-surface and target
profiles precede the widget schema; headless validation/export and the native preview worker precede
the visual shell; resource, widget, extension, and classic-RAD productivity work then arrive as
vertical slices. No item 5 runtime or designer implementation is claimed by this architecture
checkpoint.

## Sequencing summary

```text
continuous gates
      |
pixel formats and rendering backends
      |
modern fonts and text
      |
gestures, scenes, overlays, and transitions
      |
widgets, shared schema, and WYSIWYG designer
```

Work may be prototyped ahead of this order, but a later task must not establish a public contract that
depends on an unfinished prerequisite. Filesystem persistence, product settings, localization storage,
and hardware bus drivers remain application or platform responsibilities rather than LitheView core
services.
