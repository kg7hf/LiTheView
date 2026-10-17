# Display model and rendering implementation plan

Host device presentation now has a separate application-owned front-view sidecar
(Designer 0.2.8). It describes a PC board/enclosure PNG, LCD placement and named
physical-button hit regions. It does not alter display compatibility, native
framebuffer storage, resource packing or target budgeting. The starter's next
lesson maps a named mouse hotspot and the STM32 B1 GPIO to the same debounced
application handler and label counter. The MCU build contains neither the photo
nor the host hit-test geometry. See starter 1 step 8 and the current handoff for
implementation and validation scope; hardware button execution is still a target
acceptance step.

Publication scope: the designer front-view editor/schema and example README/front
images are published. The application adapters and generated bonus counter remain
local example work at the owner's request; the published examples retain their
earlier implementation. The local validation above is not a claim that the bonus
workflow is fully implemented in a fresh checkout of the published examples.

Status: M0 and M1 complete. Framebuffer-free construction, configured destination writers, checked
storage blocks, native packed R2, separate RGB products, installed consumers, and M1 integration gates
are verified. M2 native writers, examples, and row migration are implemented; integration checks
pass against synthetic geometry and the defined packed-storage contracts. Physical emitter order,
controller integration and optical acceptance remain application-specific validation work. M3 profile/definition/resource export
and six independent generated consumers pass. M4 layout/input validation, deterministic round replay,
and the complete host/package/static/cross-build checkpoint pass. M5 separate target feasibility and
its software integration gate are complete. Actual placement and missing hardware evidence remain
explicitly unevaluated in target reports.
[The implementation record](DISPLAY_MODEL_IMPLEMENTATION.md) distinguishes completed
changes and validation from the remaining contracts and acceptance gates in this plan.

The developer adoption path is now documented in the [new-hardware tutorial](NEW_HARDWARE_TUTORIAL.md):
collect display and touch/knob/button specifics, define a fixed portable product, establish PC simulation,
then integrate a hardware target. The [worksheet](DISPLAY_BRINGUP_WORKSHEET.md) keeps surface/input facts
separate from MCU selection and measured budgets. The [blank H750 starter](../examples/stm32h750b-dk-starter-1/README.md)
is the developer walkthrough. The [handwritten integration fixture](../tests/rendering/display-bringup/README.md)
provides lower-level native rendering and input checks for library maintenance.

The designer exports UI components with locked display definitions and independent logical-input
snapshots. As of Designer 0.2.2 it edits, previews and generates only; the application's CMake and
IDE build/debug configurations own execution. The paired H750 starter demonstrates the same preset
before and after generation. Legacy standalone RGB scaffold/CLI helpers remain available for tooling
checks. Generated files are inspectable and the tutorial explains their handwritten equivalents.
Native packed designer projects are version-3 manifests that lock a packed definition and logical
input with no editing surface. They open, edit, preview natively and generate through the shared
designer workspace with no target or RGB staging framebuffer; legacy RGB staging profiles are not
silently promoted to packed definitions.
Designer 0.2.5 creates these projects directly from both New Project wizards, using a native
catalog definition or custom logical geometry and a supported native format. Input stays separate;
the VS Code workflow validates and publishes application path settings with the blank form.
Designer 0.2.7 adds **Configure** to reopen the same display/input setup fields
with current values. It updates the locked configuration without recreating the
form, changing handlers or adding build presets. Logical input stays independent;
display extent changes preserve authored coordinates and require root/layout
correction before Generate UI. The application still supplies hardware adapters
and evaluates MCU budgets separately.

Application layout integration is implemented through an optional path sidecar and the designer's
Project Paths dialog. The UI project can be created in an existing folder and the generated component
can use a developer-selected directory. Application CMake owns build output paths. The exported
component borrows typed application handlers and embeds in a parent CMake build with a source or
installed Core dependency.
See [APPLICATION_INTEGRATION.md](APPLICATION_INTEGRATION.md) for ownership, device boundaries, and
current limits (whole-component output paths, project-contained resource imports, one generated
component per parent build).

## Current implementation priority (2026-10-17)

The second increment now implements typed toggle/checkbox, bounded radio-group,
picker and wheel-picker events, generated form overrides and shared Events
discovery in Designer 0.2.9. See [control objects](CONTROL_OBJECTS.md) for the
implemented semantics and handwritten equivalents. Host, cross-build and physical
acceptance remain separate; the current validation record is in the handoff.
This increment preserves the local starter work and does not publish its adapters.

Complete and demonstrate the typed control-event workflow before extending image/font authoring
workflows. The implemented controls have automated host and packaged Designer 0.2.9 coverage;
bounded text and drop-down events have runtime and generated-handler regression coverage.
Dedicated control starters, selectable table interaction and physical H750 acceptance remain open.
Progression and status:

1. Exercise the existing button-to-label handler from discovery in VS Code through regeneration,
   the Windows application, and separately recorded H750 execution in starter 1.
2. Implemented: typed control hooks, form handlers and designer discovery for checkbox, radio selection,
   and picker/wheel-picker events, with value changes separate from edit confirmation.
3. Implemented: bounded text changes and explicit commit/cancel with caller-owned rollback storage,
   checked validation, generated handlers and a setpoint regression consumer. Defer the new application
   starter until requested; keep individual control lessons separate instead of expanding starter 1.
   Drop-down selection is also implemented in Designer 0.2.11: a bounded native popup, tentative
   changes, explicit commit/cancel, immutable index identity and generated handler discovery.
   Next define the table selection/data contract. The current table is a scroll container,
   not a selectable data table. Keep individual control examples deferred until requested.
4. Follow with source validation, native bitmap encoding and resource preview, then the font workflow.

The [control-event milestone](VSCODE_DESIGNER_PLAN.md#control-event-milestone) specifies semantics,
increment boundaries and acceptance. The [native asset contract](#source-validation-and-native-asset-encoding)
defines the narrowed image-tool responsibility. The completed M0-M5 rendering gates above are
separate from acceptance of these developer workflows.

## Goal

Move LitheView toward a generic embedded graphics package in the same broad category as LVGL,
STemWin, and TouchGFX: a developer defines the display first, then layouts, resources, widgets,
rendering, and target adapters resolve against that display definition.

A display definition must be sufficient to develop, render, and exercise an application on the host
before an MCU, board, or transport is selected. Selecting hardware adds deployment constraints without
redefining the application's display semantics.

### Display portability through separate build configurations

Display format, address space, rendering strategy, storage capacities, and resource variants are product
configuration decisions resolved before the application is built. Each example executable has a fixed
configuration. Changing from RGB565 to XRGB8888 means selecting another configuration and rebuilding;
the running application does not switch its destination format or discover which display to use.

Provide distinct compact RGB565 and high-color XRGB8888 example projects with their own entry points,
configuration, selected resources, storage, build instructions, and output artifacts. Adapt the existing
examples and retain their useful scenes/assets. Each has a host build and, where supported, a board
build of its configured portable application. The shared H750 board
is a validation convenience: these examples also represent different users with different hardware,
or a product choosing RGB565 to reduce memory and bandwidth costs. They are not runtime modes of one
universal demonstration program.

The library supplies reusable source and public contracts across those configurations. Compile-time
traits, generated definitions, or configuration-specific library builds bind the required format and
operations. A compact consumer must not have to link unused destination writers, format-selection
machinery, or unselected resource payloads. Verify that property in the built artifacts; an immutable
runtime descriptor or a linker assumption alone does not establish it. Supported source masks/indexed
assets may still need their selected readers even when there is only one destination format.

Host tools can inspect multiple profiles and generate separate configurations. Common test scenes can
exercise the build matrix, and internal tests may cover multiple writers. Neither requires production
examples to carry runtime surface selection. A host simulator build uses the same fixed display and
resource configuration as its corresponding embedded build. Dynamic application data, input, time,
and checked buffer lifetimes remain runtime concerns within that configuration.

## Product contract

Owner clarification, 2026-10-03: LiTheView provides familiar C++ objects, forms,
controls, properties, specialization, composition and events, with the display's
capabilities determining their generated representation and resource costs.
These are requirements for the library, designer, generated code and examples;
they are not a claim that every planned capability is already implemented.

- **One application across platforms.** Host and MCU compile the same application,
  forms, widget behavior and selected renderer. Platform adapters provide display,
  input and other device implementations.
- **Explicit memory policy.** Static-only operation is a complete path. Optional
  C-heap or RTOS-backed owners must declare allocation phases, capacities, failure
  behavior and lifetimes. They must not introduce hidden allocation in portable
  parsing, layout, input, animation or rendering operations.
- **Display-specific assets and native rendering.** Keep high-fidelity authoring
  sources off the target. Prepare image dimensions and colour/intensity choices
  with established external tools; LiTheView validates the prepared input and
  encodes selected resources into the display's native storage representation
  before compilation. Render directly into the configured native framebuffer or tile.
  Runtime scaling, especially fonts, is a deliberate product option with bounded
  costs; it does not authorize an RGB staging framebuffer for a packed display.
- **Typed, extensible components and events.** A handler may belong to a form,
  control, reusable composite or application controller. Support intentional
  specialization of existing controls, alongside composition and typed event
  connections. RTOS messages cross application task boundaries explicitly;
  individual UI events do not require their own queues or tasks.
- **Readable generated C++.** Expose named controls and checked typed connections,
  preserve application-owned handlers, and construct the actual selected custom
  types. Developers must be able to supply equivalent handwritten objects.
- **Application-owned builds and debugging.** Designer edits/preview/generation
  feed the application's existing CMake presets and IDE launchers. Adding UI or
  another control does not require a new feature-specific build configuration.
- **Mission-critical engineering discipline.** Follow the C++ Core Guidelines
  with the documented embedded constraints and the owner's modern C++ philosophy:
  defined behavior, explicit ownership, bounded resources, precise types, checked
  errors, deliberate extension contracts and repeatable evidence. Apply the same
  standard to handwritten code, generated code and platform adapters. Formatting
  and static checks support review; they do not replace behavioral validation.

### Assets match the display before deployment

For a 240 x 480 true-color source intended for an 8-bpp grayscale display, choose
its displayed dimensions and prepare the grayscale values with external image
tools. LiTheView then validates and encodes that prepared source into a supported
native or compact representation before linking it into the application. Retain
the original as authoring input; do not ship it merely to convert it on every draw.
An unsupported native encoding needs a validated reader/writer and export contract
before it can be selected; this example is a requirement, not a claim that every
possible grayscale encoding is already implemented.

For a 512 x 384 2-bpp configuration, the runtime framebuffer or tile uses that
configuration's native packed layout, including its block/row rules. Rendering
through a full RGB888 framebuffer and converting afterward is not an accepted
production implementation of that configuration. A host window may expand the
completed native bytes for presentation, after exercising the same native renderer
as the target. Logical colors and supported alpha/indexed resources may still be
resolved directly by the native writer; they do not require an RGB staging surface.

## Engineering principles and scope

[The embedded C++ philosophy](02-embedded-cpp-philosophy.md) describes another project. Its generic
engineering discipline is a requirement for LiTheView, applied through the existing contracts in
[the modernization plan](MODERNIZATION_PLAN.md), [the memory contract](SAFETY_AND_MEMORY.md), and
[the style guide](style_guide.md):

- design for bounded memory, explicit ownership, and predictable execution from the beginning;
- run the same portable rendering implementation on host and target;
- distinguish logical coordinates, native cells, and byte counts where types prevent real mistakes;
- make fallible operations return useful, checked failure information;
- resolve static facts and check invariants at configuration or compile time where practical; and
- support behavior and resource claims with repeatable evidence.

For changed code, preserve types through the call chain, initialize state, use
`const` and RAII deliberately, and keep buffer bounds and ownership visible.
Internal C++ callbacks must not split an object into an untyped `void*` plus a
function pointer. Use typed borrowed interfaces or compile-time bindings with
explicit lifetimes. C/RTOS/OS adapters contain the unavoidable conversions.
No general callback wrapper may introduce an unbudgeted allocation.

Capacity exhaustion, invalid input, arithmetic overflow and transport failures
need explicit outcomes. New fallible APIs use a checked, `[[nodiscard]]` result
contract; construction and cleanup must remain safe with exceptions disabled.
Spans carry bounds but do not prove index validity or lifetime. Compile-time
invariants, warnings-as-errors, applicable static analysis, failure-path tests,
byte-level rendering fixtures and measured target resource costs are the evidence
for these properties. Track exceptions and unresolved checks explicitly; do not
infer worst-case timing or whole-program correctness from a successful build.

The modem's numeric type, particular `Result<T>` wrapper, and project-specific allocation policies are
not automatically LitheView requirements. This work does not import modem dependencies or mandate a
library-wide error/type rewrite. Evaluate a consistent LitheView result convention for newly added or
changed interfaces, preserve compatibility deliberately, and keep failure policy with the caller.
`[[nodiscard]]` and warnings-as-errors help enforce handling; bounds-carrying spans still require
validated sizes, lifetimes, indices, and arithmetic.

LitheView's existing portable-path constraints remain in force: caller-owned bounded storage, no heap
allocation in parsing/layout/input/animation/rendering, no exceptions or RTTI in embedded builds, and
measured Cortex-M33/M7 budgets. Host authoring and window-presentation facilities stay outside that
portable path. Broader widget expansion and visual designer work are outside the original M0-M5
rendering scope; subsequent component/event work is tracked in [the designer plan](VSCODE_DESIGNER_PLAN.md).

## Independent validation stages

Surface compatibility and target feasibility are separate results. Selecting a small or low-color
display does not imply an MCU class, a RAM limit, or a bus bandwidth.

| Stage | Required inputs | What it establishes |
| --- | --- | --- |
| Surface compatibility | Display definition, C++ layout or form, resource set, declared logical input configuration, supported renderer capabilities | Geometry, shape, address space, rendering operations, resource compatibility, and interaction requirements are valid |
| Target feasibility | Surface-compatible application plus a target configuration and applicable build/measurement evidence | Selected capacities, flash, RAM, workspace, resource placement, transport, and timing constraints are satisfied |

With no target selected, target feasibility is **not evaluated**. This must not block surface
validation, resource export, generated C++ export, or host simulation, and must not appear as a passed
embedded-budget check. A target failure must not invalidate the independent surface result.

Surface validation can report exact asset bytes and storage requirements without enforcing a target
ceiling. Target validation adds the chosen capacities and limits. Exact resource/workspace calculations
can run before compilation; linked code size and compiler stack reports require a target build.
Transfer estimates must identify their assumptions. CPU deadlines and physical transfer timing need
the applicable measurements before they can be reported as verified. Missing evidence remains
unevaluated for that constraint.

For example, one form can pass surface validation and run on the host, fail a small target's RAM
budget, and pass a larger target's measured budget. The surface identity, layout, and resources remain
the same unless an explicit configuration change is selected, revalidated, and rebuilt. Target-specific
limits and result records do not enter form XML or the surface/resource identity.

### Host simulation before hardware

The host simulator must use the real layout, widgets, resource readers, native format writers,
clipping, and input dispatch. Its adapter supplies normalized input events, caller-controlled elapsed
time, and window presentation. For packed displays, render native bytes first and decode those bytes
for the PC window. Window magnification must not alter logical coordinates, layout, or hit testing.

The simulator is another platform port of the configured application. The display driver presents the
renderer output through either a host window or target hardware. The input driver translates host
mouse/touch events or touch-controller samples into the same logical press/move/release events and
coordinates. Device scaling/calibration and hardware access stay at that boundary; hit testing,
gestures, and widget behavior remain portable. Surface encoding stays in the configured renderer.

Logical input requirements can be declared before physical controls exist. For example, keyboard
events may simulate an encoder and push button. Selecting a real input driver later adds wiring and
device transforms, and must satisfy the declared interaction contract. An absent panel-integrated
touch device does not prohibit an application-owned external input device.

Provide both framebuffer and bounded tiled host runs. Recorded events and explicit time advances must
reproduce native output for a fixed configuration. The host may simulate transfer failures and delayed
completion, but those results do not establish physical panel behavior, DMA correctness, or MCU timing.

## Current state

The checked display-surface profile schema already records useful display facts:

- shape;
- physical size;
- pixel size;
- coordinate origin and rotation;
- painter surface format and storage depth;
- native display color model and transfer format;
- safe area;
- input capabilities; and
- provenance.

The H750 examples are close to the desired model:

- RGB565 renders into an RGB565 framebuffer.
- RGB888-output renders into XRGB8888 storage because the pinned ST BSP configures LTDC as ARGB8888.
- The panel transfer remains RGB888 even when the LitheView storage is XRGB8888.

The round-display example adds an equally important non-format dimension:

- `profiles/display-surfaces/gc9a01-1.28-round-240-rgb565.json` records a circular 240 by 240
  RGB565 surface with a 168 by 168 rectangular safe area.
- `RoundDemo` constructs `Application` with `Viewport::circle({0, 0, 240, 240})`.
- The painter clips all primitives, glyphs, bitmaps, and damage to the circular viewport.
- The tile renderer still visits square controller memory deterministically; whether invisible
  corner writes are skipped is an adapter optimization, not a form/layout assumption.

- `Surface`, `PixelBufferView`, and `PixelFormatTraits` assume a byte-addressed pixel model through
  `bytesPerPixel(format)`.

Two additional structural seams need attention:

- `Application` requires `Display&`, while the tiled probes provide `NoFramebufferDisplay` objects
  returning empty surfaces. Geometry and scene behavior need a usable contract independent of a full
  framebuffer.
- Profile loading and resource selection currently live in the managed tooling; resource selection
  matches `CompatiblePainterFormats`. Preserve that work while exposing resolved runtime definitions
  and headless validation without requiring the designer UI or adding managed dependencies to C++.

The existing `tests/package_consumer` already consumes `LitheView::Core` through `find_package`.
Extend that adoption path with standalone examples rather than requiring consumers to copy a board
demo or reach into private source directories.

## Design principle

A display is an addressable grid of cells. Each cell has one or more emitters or channels, physical
layout, brightness precision, storage packing, and transfer packing. Common formats such as RGB565,
RGB888, XRGB8888, and ARGB8888 are conventional presets over that more general model.

For ordinary displays, the subpixel structure is usually hidden:

```text
RGB888 logical pixel
  red emitter:   8 bits
  green emitter: 8 bits
  blue emitter:  8 bits
  storage:       24 bits per addressed cell
```

For a synthetic packed-cell fixture:

```text
  addressed grid: 256 x 192 cells
  storage:        8 bits per cell
  emitters:       four 2-bit emitters in a 2 x 2 physical layout
```

## Layering

Keep the following three configuration concepts separate. Their implementation contracts are listed
afterward; they do not require a new library or class hierarchy for every responsibility.

### Display surface

The display surface is the logical graphics target. It should describe:

- visible shape;
- physical dimensions;
- logical address space;
- cell or pixel dimensions;
- safe area;
- coordinate system;
- native format family;
- channel or emitter model;
- destination alpha storage and supported coverage/compositing capabilities;
- default resource compatibility; and
- optional alternate address spaces.

This layer affects layout, scaling, clipping, typography, native bitmap encoding, font generation, resource
selection, render format, and host preview.

Shape must be treated as a first-class part of the surface. A circular display changes valid placement,
clipping, hit testing, transition damage, dropdown placement, popup collision, and migration. The
current rectangular `safeArea` is useful, but
it is not a complete replacement for the actual visible shape.

### Display interface

The display interface describes how completed pixels move to the panel:

- full framebuffer;
- partial framebuffer;
- tiled or strip rendering;
- direct cell stream;
- SPI;
- parallel RGB;
- LTDC;
- MIPI DSI;
- DMA/DMA2D;
- asynchronous transfer;
- dirty-region flushing;
- cache coherency;
- TE/vsync;
- single/double buffering; and
- native command packing.

These facts belong to target adapters and target profiles, not to form XML.

### Device capabilities

Device capabilities describe related hardware that the graphics runtime can consume through normalized
boundaries:

- touch availability and coordinate transform;
- key, encoder, and push-button input;
- display reset/enable;
- backlight/brightness/PWM;
- sleep/wake;
- orientation controls; and
- hardware fault reporting.

The product/application owns the drivers and fault policy. LitheView should describe and consume
capabilities without becoming a display-driver generator.

The logical interaction configuration belongs to surface/application validation and can be exercised
on the host. Physical availability, GPIO/bus bindings, backlight control, and driver initialization
belong to the selected target integration. Do not infer application input capabilities solely from
which devices are integrated into the display panel.

### Runtime contracts and ownership

| Contract | Responsibility |
| --- | --- |
| Display definition | Logical dimensions, controller extent, visible shape, safe area, coordinate transforms, address spaces |
| Format and storage layout | Color meaning, bit packing, storage blocks, row stride, alignment, checked byte sizing |
| Surface view | Borrowed storage bound to a format, extent, and logical origin |
| Rendering | Primitives, text, images, coverage/composition, clipping, and logical damage |
| Presentation | Framebuffer presentation or tile submission, borrowing lifetime, completion, and failure |
| Resolved capabilities | Effective logical input, supported operations, resource compatibility, and placement policy |

Resolve the display definition at configuration/build time, using handwritten `constexpr` C++ or
generated definitions. Any descriptor retained for runtime geometry or diagnostics is an immutable view
of that compiled configuration, not a runtime format selector. JSON parsing, authoring metadata,
provenance strings, and host filesystem work stay in host tooling. Handwritten and generated definitions
must describe equivalent behavior. Target feasibility consumes the configuration and a separate target
configuration/report.

Allow scene and input behavior to use the display definition without an empty framebuffer object.
Preserve existing full-frame entry points through compatibility adapters or overloads as needed.
Decide the concrete API in milestone M0; do not combine this change with a scene-system rewrite.

Keep caller ownership explicit. A synchronous tile sink consumes or copies bytes before returning.
An asynchronous submission retains its source only under the existing explicit completion/lifetime
contract. Do not silently turn `TileSink` into a retaining interface. Keep driver callbacks, timers,
DMA, cache policy, and concurrency at the platform boundary.

Use one shared storage-layout calculation for surfaces, read-only buffer views, raster addressing,
tiling, and transfer validation. Bind the destination writer through the build configuration and retain
specialized raster loops. Existing type-erased APIs may need compatibility wrappers, but those wrappers
must not force every destination writer into a configured product. A profile selects a supported writer
at build time; arbitrary packing metadata does not create a working renderer. Unsupported static
format/operation combinations fail during configuration or compilation; invalid dynamic data is checked
before drawing or submission.

## XML and profile boundaries

Form XML should remain logical. It should describe components, geometry, style references, theme
tokens, actions, bindings, fonts, bitmaps, and animations. It should not contain SPI, LTDC, MIPI,
backlight, or touch-controller bus details.

Display profiles should be separate versioned documents. A form or project can reference a selected
surface ID or compatible surface family. The same form should be renderable on host and target
profiles when the surface geometry and resource capabilities are compatible.

The designer can later use these profiles as its first workflow decision, but the runtime and host
examples should prove the model first.

## Shape, safe area, and usable regions

Round and irregular displays enhance the display model because they separate three ideas that are often
collapsed on rectangular panels:

- **Controller memory**: the addressable RAM or transfer rectangle. For a GC9A01 round panel this is
  typically 240 by 240 square controller memory.
- **Visible shape**: the pixels that are physically visible through the glass or mask. For the current
  round profile this is a 240-pixel circle.
- **Safe area**: the region where ordinary controls, text, and popups can be placed without awkward
  clipping. For the current round profile this is a conservative 168 by 168 rectangle.

The profile model should represent all three.

Suggested concepts:

- `controllerExtent`: addressable width and height, when this differs from the visible extent.
- `visibleRegion`: rectangle, circle, rounded rectangle, polygon, or bounded mask in the future.
- `safeArea`: conservative rectangle for normal controls.
- `contentInsets`: optional per-edge logical inset for panels that have bezels, curved edges, or
  optical dead zones.
- `placementRegions`: optional named regions for popups, menus, keyboard overlays, status bands, or
  complications.
- `clipPolicy`: whether the renderer clips to the visible region, the safe area, or a caller-selected
  viewport.

The runtime already has a `Viewport` abstraction for rectangle and circle. The next model should
connect display profiles to viewport creation instead of requiring each demo to hardcode the viewport.
The target adapter should still decide whether to transmit cleared invisible corners, skip them, or
use panel-specific command windows.

Widget layout should use safe area and placement regions by default. Rendering should still clip to
the actual visible region. This distinction matters: a dropdown may need to avoid the curved edge even
though the painter can technically clip it.

## Device-aware widgets and resources

Widgets should become device-aware through resolved capabilities, not by embedding hardware knowledge
in each widget.

The flow should be:

```text
build-time display definition + C++ layout or form XML + resources + logical input configuration
        |
        v
compiled render configuration and surface compatibility result
        |
        +-- widget capability validation
        +-- layout and popup placement
        +-- resource-variant selection
        +-- native format writer selection
        +-- input-mode validation
        |
        +-- separate configured host executable / surface-compatible export
        |
        +-- optional target configuration + build/measurement evidence
                |
                v
            separate target feasibility result
```

The form XML remains logical. Surface compatibility must be checked before surface-compatible export
and application construction, whether construction uses XML or C++. It must not require a target.
Target-specific export/build gates additionally enforce the selected target's applicable constraints.

Report incompatible resource representations at the surface stage, with stable diagnostics naming the
resource, selected format, and supported alternatives. At equal cell dimensions an uncompressed RGB565
image takes twice the payload of an 8-bit native-cell image; masks or palettes may be smaller still.
Report these byte costs independently of whether any target budget has been selected. The target stage
can then reject a compatible but over-budget selection without changing its surface-valid status.

### Render context

Add or formalize a small render/layout context that is available when a form or static layout is built:

- surface dimensions and default address space;
- visible shape and safe area;
- native format descriptor;
- destination alpha storage, source coverage, and supported composition operations;
- available resource formats;
- text/glyph coverage options;
- physical DPI or pixels-per-millimeter;
- declared logical input capabilities;
- popup/overlay placement regions;
- minimum recommended touch target size, when the interaction policy makes it applicable.

The render context should be a derived object, not a second source of truth. It is computed from the
selected display definition, supported renderer operations, resources, and logical interaction
configuration. Resource/workspace requirements can be reported alongside it; target ceilings and their
validation results remain separate. A target does not silently change the context's layout or format.

### Widget validation

The widget schema already has input-capability and resource-dependency concepts. Extend that idea so a
widget can declare:

- required input classes, such as touch, encoder, key, or pointer;
- optional input affordances, such as hover, drag, swipe, long press, or focus-only operation;
- required coverage/composition operations and any actual need for stored destination alpha;
- supported native format classes;
- minimum readable physical size;
- minimum touch or focus target size;
- popup/overlay needs;
- resource formats it can consume; and
- fallback behavior when a capability is absent.

Examples:

- A touch scroll wheel requires touch or must expose an encoder/key fallback.
- A dropdown or menu must have a placement region large enough for its popup, or it must fall back to a
  full-screen overlay/picker.
- A translucent overlay requires supported composition or a declared opaque fallback. It does not
  automatically require an alpha channel in the destination.
- An image widget's resource binding must resolve to a compatible variant at export/build time.
- A keyboard overlay requires touch and enough safe area, otherwise it should be rejected or replaced
  by product-owned input.

Surface validation applies before export and before application construction for both handwritten C++
and XML. Runtime checks remain for dynamic operations. Unsupported combinations need explicit
diagnostics; intentional clipping or an application-selected fallback must be distinguishable from an
unusable control. Initial validation policy must not silently relocate or rescale an existing form.

### Layout and popup placement

- clamp ordinary controls to safe area when requested;
- compute visible clipping from shape;
- place dropdowns inside named placement regions;
- flip, slide, or convert a popup to an overlay when it would exceed usable area;
- use physical dimensions to enforce readable text and touch sizes; and
- use input capabilities to select focus, encoder, or touch navigation affordances.

For round displays, menus and dropdowns usually should prefer centered overlays or radial-safe
placement instead of rectangular desktop-style popups at arbitrary edges.

For very small or low-color displays, a dropdown may need to render as a separate full-screen list,
not as a floating menu.

### Source validation and native asset encoding

Owner clarification, 2026-10-04: LiTheView owns display-specific encoding and compatibility
validation. It does not take on general image editing, colour-depth reduction, dithering, palette
design, resampling or artistic colour-to-monochrome decisions. Established tools prepare those
choices; a future explicit external-tool integration may invoke them without making their algorithms
part of the portable library or a requirement for an ordinary C++ build.

The boundary is between changing the intended image and encoding its already selected samples:

| Input and destination | Required generation behavior |
| --- | --- |
| RGB565 samples for a BGR565 storage contract | Reorder the channel fields before compilation when that encoding is supported; otherwise diagnose the mismatch and the required prepared format. This is a planned example, not a claim of current BGR565 support. |
| Correct samples, different byte order, row stride or block alignment | Encode the declared ordering and padding deterministically, checking dimensions and byte counts. |
| Prepared four-level red image for RRRR2222 | Pack the selected two-bit emitter levels in native cell order: four emitter values per byte. Preserve the declared cell/emitter geometry. |
| High-colour artwork with no defined four-level interpretation | Report the required source preparation or use an explicitly selected external preparation step; do not silently choose a quantizer, dither or channel mapping. |

Container encoding is not sample meaning. For example, an RGB-encoded PNG may hold only the four
intended intensity levels. A supported host decoder may read it, validate the levels against an
explicit interpretation, and pack them without a lossy colour-reduction operation. Do not reject it
solely because the file decoder exposes RGB samples, or silently infer whether red, luminance or a
palette index is intended. Source dimensions use an explicit logical/cell/emitter address space;
import must not resize or reinterpret 512 x 384 emitters as 512 x 384 cells.

Automatically perform supported lossless channel/byte reordering and native bit packing. Unsupported
representations fail with a diagnostic identifying the asset, actual properties, required properties
and remediation. Missing mappings, incompatible alpha, invalid geometry and overflow block export;
they do not trigger a runtime conversion fallback. Invalid or stale inputs publish no partial output.

Generated bitmap payloads follow the configured native storage contract. In particular, an RRRR2222
asset must not be shipped as RGB and reduced or repacked on each draw. Source/master files can remain
in the application repository, but only selected native or explicitly supported compact payloads
enter the firmware. Native storage and panel transfer encoding remain distinct: display drivers own
commands, addressing and bus transfer; an RGB panel bus does not require RGB bitmap storage.

This narrows the default designer workflow; it does not claim that existing conversion tools have
already been removed or replaced. Preserve implemented tooling and regression fixtures until an
explicit migration is implemented. Existing font generation and deliberately configured runtime
scaling are separate capabilities; this decision neither expands them nor imposes a blanket ban.

### Resource selection and publication

Resources should be generated and selected based on display compatibility:

- An RGB565 surface selects RGB565 or alpha coverage resources.
- An XRGB8888 surface can select high-color resources.
- `rrrr2222` can select 2-bit red-luminance or alpha mask resources.
- `ryry2222` can select a red/yellow palette or indexed resource.
- round displays may need alternative icon sizes or masks because clipped corners change composition.

The resource manifest should record compatible native format classes, not only current RGB painter
formats. The host/build resolver selects exactly one compatible variant or reports a diagnostic.
The configured application receives the selected resource bindings and payloads; it does not search
all surface variants at runtime. Runtime image/animation changes use the resources included in that
build.

Resource selection computes the selected variant's byte cost before export without requiring a target.
A separate target feasibility check compares payload and required workspace against selected limits.
That check may reject deployment of a surface-compatible resource set while leaving host simulation
and surface-compatible export available.

The preferred resource model is:

```text
retain master artwork and provenance
prepare intended dimensions and colour/intensity choices with external tools
validate prepared source against the selected display definition
encode and publish native or supported compact payloads with Generate UI
build the same application's host or embedded target with its existing presets
```

A source asset should usually be retained at the best available authoring fidelity: RGBA artwork,
reviewed vector source plus a deterministic raster export, high-color sprite sheets, TTF/OTF fonts,
or other source material with provenance and license records. Retain prepared bitmap inputs and any
external preparation recipe separately from the master. The LiTheView encoder produces one or more
supported native or compact variants from those prepared inputs; it does not silently regenerate
their colour or size choices from the master artwork.

Examples:

```text
prepared image/icon
  externally selected dimensions and samples, with an explicit interpretation

RGB565 surface export (also used by H750)
  RGB565 bytes or alpha coverage plus draw color

XRGB8888 surface export (also used by H750)
  XRGB8888 bytes or high-color variant

  red-luminance/native-cell representation

  red/yellow palette or native-cell representation

  packed R2 subpixel asset, if the advanced address space is selected
```

Native color images, coverage masks, and supported indexed resources are all legitimate output
representations. For example, a tintable alpha1 icon may be smaller than a fully expanded native image.
Resolve exactly one supported variant with known storage and drawing costs; do not force every glyph
or mask into destination-color storage. Any newly proposed alpha2/red2 format needs an implemented
reader/writer and validation before a profile can select it.

Historical high-color/RGB565 staging remains diagnostic/test evidence only; it is not an export
fallback or a production asset path for a native packed configuration. Host window presentation may
expand completed native pixels, but must not replace native rendering with an RGB-first simulation.

Separate dynamic and static work:

- vector-like primitives such as rectangles, lines, fills, borders, progress bars, and runtime text
  render through the selected native format writer;
- static bitmap resources such as images, icons and animation frames are prepared externally,
  then validated and encoded into selected native, mask or indexed variants; font tooling separately
  prepares glyph tables;
- theme colors are resolved or quantized at export/build time where possible, with small runtime
  writers only for truly dynamic state; and
- dynamic product data may still render at runtime, but it renders directly into the target-native
  surface.

The host encoder owns the supported prepared-sample-to-storage mappings. It records source and
payload hashes, source interpretation, dimensions/address space, storage format, stride/alignment,
byte cost and encoder version/settings. It emits deterministic payloads for the target, without
adding general image-conversion algorithms to LiTheView.

Acceptance for the resource workflow:

- A developer selects a prepared image, receives actionable compatibility diagnostics, assigns it
  to a control, previews the actual native payload, and generates it without selecting an MCU.
- Independently specified expected bytes prove channel/byte ordering, emitter packing, padded rows
  and block edges for each supported mapping. Invalid levels, geometry, alpha and truncated data
  fail before publication. Never create a golden by invoking the encoder under test.
- The designer preview, compiled host application and target build use the same selected payload
  and interpretation. Only the final host presentation expands pixels to RGBA.
- Artifact/link checks establish that original RGB sources and unselected variants do not enter a
  packed target, and that drawing does not require an RGB staging buffer or repeated colour reduction.
- Surface/resource compatibility and byte costs are available first; MCU flash, workspace and
  transfer-budget validation is a separate stage. A host run is not physical display acceptance.

### Configuration changes and runtime fallback policy

Changes to surface format, address space, font set, or resource representation require an explicit
configuration change, revalidation, and rebuild. Examples include:

- use compact font instead of generated font;
- use alpha1 instead of alpha8;
- select RGB565 instead of XRGB8888 to meet a product footprint constraint; and

Runtime behavior fallbacks operate within the capabilities compiled into that configuration. Declared
examples include converting a floating dropdown into a full-screen overlay when placement requires it,
using an included focus/encoder interaction, or using an opaque panel. They do not change the compiled
surface format or load another product's resource set.

Bad fallbacks include:

- silently scaling a form;
- silently rendering RGB565 then quantizing if the profile says native packed format;
- clipping controls outside the visible shape without diagnostics;
- dropping unsupported alpha; or
- accepting a touch-only widget on an encoder-only display.

## Format model

Retain familiar format names, but make them instances of a richer descriptor model.

Separate color semantics, storage packing, and transfer encoding. A preset can bind them together,
but a storage bit layout does not define quantization, blending, or panel commands by itself. Keep
storage depth separate from transfer-unit depth, as the existing H750 example already requires.

Useful descriptor fields, with final C++ names settled in M0:

- `storageUnitBits`: size of a storage unit, with logical block width/height and byte row alignment.
- `emitterCount`: number of emitters or channels in one addressed cell.
- `bitsPerEmitter`: brightness precision for each emitter.
- `channelModel`: RGB, RGBW, red-only, red/yellow, luminance, indexed, alpha-mask, etc.
- `emitterLayout`: physical position of each emitter within the addressed cell.
- `packing`: byte/word bit layout.
- `destinationAlpha`: whether alpha is stored and its representation.
- supported source coverage and composition operations: separate from stored alpha.
- `addressSpaces`: normal cell address space and optional subpixel address spaces.

Define deterministic logical-color quantization and rounding for each implemented writer. Normal
RRRR2222 cell drawing quantizes one red level and repeats it across the four emitters; explicit native
cell packing can still express four independent levels. RYRY2222 drawing resolves red and yellow levels
and repeats each across its declared emitter pair. Record and test the color-mapping policy separately
from exact bit placement. RGBW mapping remains deferred until its semantics are specified.

RGBA `Color` remains the logical drawing color during this work. Opaque destinations may still support
source-over and glyph coverage by composing against
the destination and quantizing the result; each supported operation needs a defined result. A packed
write must preserve untouched fields in the same storage unit.

### Packed storage and tile rules

- Use checked storage-block sizing and addressing shared by `Surface`, `PixelBufferView`, painter,
  tiled renderer, and transfer validation. A byte count of zero must not hide an unsupported format.
- The initial R2 packed surface uses complete 2 by 2 logical blocks. Surface extents and tile origins
  must align to those blocks; reject incomplete storage or misaligned tile workspaces before output.
  Arbitrary padded/sub-byte-origin surface views are deferred.
- Drawing coordinates and clips may address individual logical pixels inside a valid block-aligned
  surface. Preserve neighboring fields; a clipped glyph does not require a block-aligned glyph shape.
- A changed logical pixel can require a whole storage block to be transferred. Distinguish logical
  damage from the aligned transfer extent. Expanding a region is safe only if all transmitted fields
  are retained or reconstructed correctly; never send uninitialized neighboring fields.
- Reconstruct complete tiles from the same scene state for framebuffer-free rendering. Define clear
  values for invisible controller cells and deterministic padding before hashing or transmitting.
- Preserve full-frame/tiled equality for a fixed format. If ordered dithering is supported, anchor it
  to global logical coordinates so tile boundaries do not change the result.

Both RRRR2222 and RYRY2222 cell surfaces are byte-addressed. Their support alone does not demonstrate
sub-byte generality; milestone M1 must also render to `r2-packed2x2` without an RGB workspace.

Common presets:

```text
rgb565
  addressed cell: one logical pixel
  channels:       R5 G6 B5
  storage:        16 bits

rgb888
  addressed cell: one logical pixel
  channels:       R8 G8 B8
  storage:        24 bits

xrgb8888
  addressed cell: one logical pixel
  channels:       unused8 R8 G8 B8
  storage:        32 bits
  alpha:          opaque

argb8888
  addressed cell: one logical pixel
  channels:       A8 R8 G8 B8
  storage:        32 bits
  alpha:          stored

rgbw2222
  addressed cell: one native cell
  emitters:       R2 G2 B2 W2
  storage:        8 bits

rrrr2222
  addressed cell: one native cell
  emitters:       R2 R2 R2 R2
  storage:        8 bits
  layout:         four spatial red emitters

ryry2222
  addressed cell: one native cell
  emitters:       red/yellow/red/yellow, exact positions declared by profile
  storage:        8 bits
  layout:         diagonal red/yellow physical pattern

r2-packed2x2
  logical surface: 512 x 384 red emitters
  logical pixel:   R2
  native storage:  one byte packs a 2 x 2 block into one 256 x 192 native cell
```

## Existing examples as the portability matrix

The repository already contains the required demonstration material. Preserve and migrate it onto the
generic contracts. Do not replace the existing applications with a new showcase or make new artwork,
scenes, or a large example framework a prerequisite for the library work.

| Existing example material | Geometry and configuration | What its migration must demonstrate |
| --- | --- | --- |
| H750 RGB565, common demo, and Win32 port | 480 by 272 rectangular RGB565 | Independent compact build, configured resources, framebuffer output, host display/touch boundary |
| H750 XRGB8888, common demo, and Win32 port | 480 by 272 rectangular XRGB8888 | Independent high-color build on the same public contracts, with its own storage and resource costs |
| U545 `GraphicsProbe` using the common demo | 480 by 272, 480 by 4 RGB565 strips | Same portable scene behavior with bounded tiled storage and no framebuffer-shaped dummy dependency |
| `RoundDemo`, Win32 round port, and U545 `RoundGraphicsProbe` | 240 by 240 controller extent, circular visibility, 168 by 168 safe area | Geometry independent of the 480 by 272 examples, circle clipping, input mapping, scenes, gestures, and overlays |

Make the portable example applications consumable through the installed package and exported targets,
beginning with `find_package(LitheView CONFIG REQUIRED)`. Their host builds must not depend on private
library paths, the designer UI, an MCU SDK, or board initialization. Board builds add their platform
drivers separately. Checked generated assets should allow C++ builds without rerunning authoring tools.

Each example needs a README with actual configure/build/run commands, selected configuration, expected
output, storage ownership/lifetimes, and evidence limits. Add a headless mode that emits native bytes
and metrics; window presentation can be a separate host adapter. Keep independently selectable build
targets, configuration/generated headers, build directories, and outputs. Common portable components
and conformance scenes can be shared without reducing the examples to a runtime format switch. Record
explicit time/input sequences for interactive behavior. Examples should demonstrate public error
handling as carefully as the successful path.

Board examples build on these contracts and explain the remaining integration work: input drivers,
time delivery, transport, completion, and fault handling. Keep the distinction between a cross-built
probe and a flashable application visible.

### Required independent RGB565 and XRGB8888 examples using H750

Use the two existing RK043FN48H profiles as the definitions for two separate RGB example configurations
in M1:

- `profiles/display-surfaces/stm32h750b-dk-rk043fn48h-rgb565.json`;
- `profiles/display-surfaces/stm32h750b-dk-rk043fn48h-xrgb8888.json`.

The checked profiles share the 480 by 272 rectangular geometry, safe area, physical size, and declared
touch input because this repository validates both configurations on the same panel. Each example
selects its rendering/storage surface at build time. A consumer may instead have hardware supporting
only its chosen configuration, or choose the compact surface for product resource constraints:

| Property | RGB565 surface | XRGB8888 surface |
| --- | --- | --- |
| Color precision | R5 G6 B5, 16 bits | R8 G8 B8, 24 bits |
| Storage per pixel | 2 bytes | 4 bytes, including the unused X byte |
| Tightly packed full framebuffer | 261,120 bytes (255 KiB) | 522,240 bytes (510 KiB) |
| Stored destination alpha | None | None; current writer sets X to `0xff` |
| Panel input in the checked profiles | RGB888 parallel | RGB888 parallel |

The byte counts are per framebuffer, before alignment, additional buffers, or other application costs.
The pinned H750 BSP uses LTDC ARGB8888 storage for its high-color mode; the LitheView adapter's opaque
XRGB8888 convention accommodates that configuration. This driver detail stays in the adapter.

Build and run each example's own host executable. Retain the existing compact/high-color scenes and
resource choices, with independent storage and integration entry points; neither example must link the
other configuration to run. The examples can share portable source and assets explicitly selected by
both builds. Preserve their current behavior while changing integration contracts. Use shared small scenes
only where they help compare renderer semantics in tests. Verify each example's color quantization,
byte/stride costs, input behavior, and full-frame/tiled equivalence in separate strategy builds. M1 may
use handwritten definitions; M3 generates equivalent build definitions from versioned profiles. The
historical H750 names do not require MCU selection or board access for the host build.

Whether a particular framebuffer or tile configuration fits H750 memory and meets its timing limits is
a separate target-feasibility decision. A failed budget can lead the developer to choose another surface
configuration and rebuild; it must not cause an automatic runtime format fallback. Preserve both
offline H750 build variants as the relevant APIs change; physical-board execution remains separate
evidence under the existing coordination rules.

## Implementation milestones

Complete these milestones in dependency order. Each is a bounded reviewable change with its own
acceptance evidence; the first implementation session is not expected to complete all of them.
Preserve unrelated working-tree changes and keep existing RGB behavior under regression checks.

### M0: baseline and public contracts

Resolve and document these choices before changing the affected APIs:

- the display-definition entry point for framebuffer and framebuffer-free applications;
- the relationship between existing `PixelFormat` aliases and a native format descriptor, with one
  authoritative layout calculation rather than two parallel format systems;
- build-time binding of destination writers and selected resource readers, including how installed
  consumers obtain configuration-specific code without mismatched library/header configuration;
- independent RGB565 and XRGB8888 example projects, artifacts, and per-example host/board build paths;
- checked surface construction, stride units, logical origins, block alignment, and size overflow;
- channel mapping, quantization, source coverage, and the initially supported operation set;
- failure/result conventions, including separate meanings for no damage, failure, and incomplete
  presentation; assess an `expected`-based result without mandating a project-wide rewrite;
- ownership and lifetime at synchronous and asynchronous boundaries; and
- compatibility wrappers and the migration of existing public calls.

Acceptance: small API/build sketches show ordinary C++ consumers fixing their display configuration,
supplying storage, compiling a chosen rendering strategy, and handling errors. The examples must be
possible without XML, runtime profile parsing, designer tools, or a dummy `Display`. Use compile-time
checks for fixed configuration; host-loaded authoring profiles receive equivalent validation before
code generation. Runtime buffers, events, and XML layouts still require validation against the fixed
capabilities of the compiled application.

### M1: storage foundation and first example migrations

Implement the minimum display-definition/surface contracts, shared checked storage math, and the
framebuffer-free application path. Retain existing RGB fast paths and add `r2-packed2x2` early so this
milestone actually exercises sub-byte writes. Start with clear/fill/line/clipping, then the compact text
and bitmap coverage required by small conformance fixtures; unsupported operations must fail explicitly.
Adapt the existing RGB565 and XRGB8888 application builds to the new contracts. Include the existing
240 by 240 round example early so dimensions and shape are not accidentally tied to the H750 geometry.

Acceptance:

- the existing compact RGB565 and high-color XRGB8888 examples use the new contracts in independent
  fixed-configuration builds, preserving their scenes and selected resources;
- the existing round example runs with its own dimensions and circular viewport, without inheriting
  480 by 272 constants; broader placement/profile changes remain in M3/M4;
- the RGB examples have separate entry points, configuration, documentation, and host executables;
  neither is a runtime mode of the other;
- separate full-frame and tiled test builds render shared small conformance scenes for each format;
- each format has independently specified expected bytes, and full-frame/tiled output matches within
  that format; different color formats are not expected to have identical bytes;
- invalid sizes, overflow, misalignment, and insufficient spans fail before output; individual logical
  writes preserve neighboring packed fields;
- the example reports exact storage/workspace costs and handles sink failure without claiming frame
  completion;
- artifact and size checks show that each product build omits unused destination implementations and
  unselected resource variants; one universal multi-format test binary is insufficient evidence;
- no target profile, MCU SDK, or RGB intermediate is required; and
- affected host/package tests and offline embedded budget checks pass. Record inherited blockers
  separately; any required check that remains blocked leaves the corresponding gate incomplete.

### M2: native packed rendering

Keep controller-independent packed writers, exact-byte tests, checked block strides and full/tiled
parity. Use synthetic geometries and independent expected-byte oracles. A host decodes native bytes
only for window presentation; an embedded adapter transfers native bytes with explicit ownership.
Physical controller protocols belong in consuming applications.

### M3: profile resolution and resource export without a target

Version or explicitly migrate the surface schema for address spaces, native formats, emitter/channel
layout, packing, resource compatibility, and distinct alpha/composition capabilities. Preserve existing
H750 RGB and round profile meanings. Keep target budgets, memory placement, drivers, and control-bus
details out of surface and form contracts. Define logical input configuration independently of physical
driver selection.

Connect host/build profile resolution to compiled display definitions and viewport creation. Emit
deterministic C++ definitions and selected resource tables. Reuse existing host loaders/converters
behind headless interfaces;
any relocation from designer-named tooling should be limited to the boundary this work needs. Do not
add managed or filesystem dependencies to the embedded library.

Acceptance: surface-only validation, resource generation, C++ export, and host simulation work with no
MCU target selected; target status is unevaluated. A display configuration is still required and compiled
into its host executable. Handwritten and generated equivalent definitions render the same bytes.
One retained asset produces native and compact variants with exact hashes/costs, and
selection rejects missing, ambiguous, or unsupported variants. Profile migration is explicit and does
not silently rescale an existing 512 by 384 form.

### M4: surface-aware layout and interaction validation

Status: complete. Shared C++/managed diagnostic fixtures, checked layout construction, ancestor-aware
focus, captured-release handling, and the explicit round-popup fallback pass the M4 acceptance gate.
The round replay compares 24 frames in RGB565 and XRGB8888 across independent full-frame and tiled
scenes. Debug/Release host tests pass 15/15 each, installed-package tests 3/3, managed Core tests 67/67,
and the recorded static-analysis and offline embedded gates pass. This does not close M2's physical
source-verification gate. Exact measurements and scope are in the implementation record.

Use the resolved context for safe-area policy, visible-shape clipping, logical input checks, resource
compatibility, and one popup/dropdown placement or declared fallback. Share semantics and diagnostic
identities across C++ construction and host/XML validation, with common fixtures where implementations
are in different languages. The portable layer uses bounded status data; host tools can expand it into
messages. General polygon masks, radial widgets, and broad widget redesign are deferred.

Acceptance: the existing round example derives its viewport from the definition, keeps rendering and hit testing
consistent, diagnoses unusable placement, and demonstrates a deliberate popup fallback. Recorded input
and time sequences replay deterministically. Surface failures are independent of target availability.

### M5: separate target feasibility and full integration evidence

Status: implemented and validated. The optional headless target check preserves surface results,
binds compiled identities/capacity facts, and separates calculated, estimated, cross-build, and hardware
evidence. Constrained/sufficient fixtures, stale/malformed evidence, independent exports/consumers,
and the existing offline build gates pass. Missing measurements and actual resource placement/alignment
remain unevaluated; the CLI consumes caller-reported evidence rather than independently measuring it.
M2's remaining variant-color and physical-integration questions stay open. See the source evidence and
implementation records for exact scope.

Add the optional target check over the already surface-compatible application. Enforce selected
capacities, resources, workspace, placement, and build budgets at their applicable stage. Report the
source of each result: calculated, estimated, cross-build measured, or hardware measured. Do not infer
MCU limits from display color depth. Target failure must leave surface validation and host simulation
usable.

Acceptance: the same surface/application passes host validation with no target; a constrained fixture
target rejects a known cost; a sufficient fixture accepts that cost; other unevaluated constraints stay
unevaluated. Real offline M33/M7 builds check their actual gates. Documentation names the example commands,
measured footprint, passed checks, and remaining hardware evidence. Complete the relevant existing
conformance gates at the integration checkpoint; no physical-board access is implied by this plan.

## Validation plan

Create focused tests before moving broad examples.

### Unit-level format tests

Add tests for exact packing:

- RRRR2222 logical-color drawing repeats one quantized red level; the explicit native packing helper
  packs four independently supplied red levels into their declared fields.
- `ryry2222` maps red and yellow levels to the correct diagonal emitters.
- `r2-packed2x2` maps four logical R2 pixels to the expected byte.
- row stride, size overflow, insufficient storage, incomplete blocks, and unsupported formats fail
  closed before writing or starting a transfer.
- single-emitter updates preserve the other fields in a nonzero byte; clipped writes preserve guards
  and row padding.
- native level rounding, zero/full/partial coverage, and supported composition have exact fixtures.

Expected bytes must come from small independently specified cases, not from invoking the production
packer to generate its own oracle. Keep compact checked fixtures alongside scene-level CRCs.

### Renderer tests

Build the same tiny conformance scene separately for:

- RGB565;
- XRGB8888;
- RRRR2222;
- RYRY2222; and
- R2 packed subpixel mode.

Assert exact storage bytes or hashes and exact payload sizes.

Run these comparisons across the configured executables. Internal unit tests may exercise multiple
writers, but the public examples must independently demonstrate the fixed-configuration deployment
path. Full-frame/tiled equivalence is checked using separate strategy builds for each fixed format.

Compare framebuffer and tiled output within each format across several valid tile sizes. Exercise
nonzero aligned origins, drawing across block boundaries, partial clips, and repeated updates. Verify
that invalid tile alignment fails before submission and that expanded transfer regions contain valid
neighboring fields. Include antialiased glyphs on an opaque destination to distinguish composition
support from destination alpha storage.

Add shaped-surface cases:

- render a scene to the 240 by 240 circular viewport and verify corner pixels do not produce visible
  damage;
- verify full-frame and tiled output remain byte-equivalent for the round profile;
- verify ordinary controls placed outside the round safe area produce the declared diagnostic or
  explicit placement policy before export or layout finalization; and
- verify a popup/dropdown near a circular edge chooses a declared fallback or reports a stable error.

### Validation-stage and host-simulator tests

- A surface-compatible application validates, exports, and runs on the host without a target profile;
  target feasibility is reported as unevaluated.
- Adding a deliberately insufficient target budget fails only target feasibility. A sufficient budget
  passes the corresponding calculated constraint without claiming unevaluated timing or hardware checks.
- Invalid resources, unsupported operations, or missing required logical input fail surface validation
  even if a target offers abundant memory.
- Equivalent handwritten C++ and generated definitions produce the same rendering and capability
  decisions. Target selection does not mutate surface identity, geometry, or resource selection.
- RGB565 and XRGB8888 are independent example builds with their own resource bindings and storage;
  no runtime display-format option is required or exposed. Their host builds use the same compiled
  display configuration as their corresponding board builds.
- Changing a surface format or address space requires regeneration where applicable and a rebuild.
  Runtime XML/resource requests incompatible with the compiled capabilities fail explicitly.
- PC keyboard/mouse events can supply the declared logical input configuration without hardware drivers.
- Fixed resources, input events, and elapsed-time steps reproduce native output. Window scaling changes
  only presentation and its inverse input-coordinate mapping.
- The packed host path presents decoded native output and exercises the same bounded tiled path used by
  the embedded example.
- Failure injection checks begin/write/end handling and complete-versus-incomplete reporting. Reused
  tile storage is not retained by synchronous sinks; any asynchronous example checks its explicit
  ownership contract.

### Example tests

Synthetic packed tests check complete storage rows, expected native byte counts, arbitrary legal
extents, padded strides, deterministic changes, and rejection of incomplete buffers and blocks.
Generic input tests drive pointer, navigation and activation without depending on a sensor driver.

Keep round-display evidence:

- circle clipping uses exact integer pixel-center classification;
- safe-area guidance is preserved in the profile;
- transitions and overlays repaint correctly under the circular viewport;
- full-frame/tiled equivalence remains true; and
- host output distinguishes controller extent from visible region.

### Build and budget tests

Maintain the existing M33/M7 budget discipline:

- no heap in portable paths;
- no exceptions/RTTI in embedded configurations;
- warnings as errors for first-party C++;
- host tests for every format path.

Track changes in code size and workspace at each affected milestone. Do not silently increase limits to
make new formats fit. Keep formatter/static-analysis checks applicable to touched first-party code and
run the relevant conformance suite at the integration gate. A maximum single stack-frame report is not
a proof of whole-call-chain or RTOS task stack usage; label the measurement actually collected.

Check link maps/symbols and selected asset inventories for each example configuration. The compact
build must not retain the high-color destination path or its unselected assets merely because another
example supports them. Record each configuration's linked code, static data, workspace, and resource
payload independently. Keep generated configuration and build directories isolated so one example's
definitions cannot accidentally alter another example's library or consumer.

## Documentation updates

Update the following alongside the milestone that changes their contract. This planning update alone
does not change implementation status in the handoff or conformance records:

- `docs/PIXEL_FORMATS.md`: replace byte-addressed-only assumptions with cell/emitter/storage model.
- `docs/SAFETY_AND_MEMORY.md`: describe packed native surfaces and subpixel address spaces.
- `docs/SCENES_GESTURES_AND_VIEWPORTS.md`: connect shaped viewports to display profiles, safe areas,
  and popup/menu placement.
- `docs/CONFORMANCE.md`: record new exact native-format evidence.
- `schema/display-surface.schema.json`: version or migrate the profile contract.
- target-profile schema and profiles: keep separate feasibility inputs, updated native workspace
  requirements, and evidence sources; do not embed those limits in form/resource/surface contracts.
- resource docs/manifests: distinguish high-fidelity source assets from target-native generated
  and compact variants and record selected variant byte costs independently of target limits.
- example READMEs and the main README: document the standalone adoption sequence, host operation
  without MCU selection, independent build configurations and executables, actual commands, ownership,
  and expected outputs.
- `docs/AGENT_HANDOFF.md`: record final commands, evidence, and remaining gaps.

## Non-goals for the next session

- Do not start visual designer work.
- Do not make form XML hardware-specific.
- Do not add SPI/MIPI/LTDC/backlight driver generation.
- Do not make every widget individually responsible for display quirks; centralize capability
  resolution and placement.
- Do not require MCU/board/transport selection for surface validation, resource export, or simulation.
- Do not implement runtime switching between product surface formats, address spaces, or resource
  configurations. Host profile authoring and internal multi-format tests do not change this boundary.
- Do not make the other project's philosophy a mandate to import its types, dependencies, or policies.
- Do not replace the library's error/type conventions wholesale as part of native-format support.
- Do not use an RGB intermediate for the native acceptance path; supported compact masks and indexed
  resources remain legitimate formats.
- Do not implement arbitrary profile-defined writers, every blend mode, RGBW mapping, irregular masks,
  optical dithering, or a new asynchronous rendering scheduler in the first foundation milestone.
- Preserve existing RGB565/RGB888/XRGB8888/ARGB8888 library support while allowing product builds to
  include only their selected destination formats and source-resource readers.
- Do not weaken the current H750 smoke evidence or shared-board permission rules.

## Next implementation sequence

1. Keep the starter progression coherent: blank application, custom display, panel, controls,
   logical input, typed handlers and host/target builds using the same presets.
2. Extend typed events to text entry, dropdown selection, tables and named tab changes, with
   application-owned lifetimes and synchronous bounded dispatch.
3. Complete prepared-resource encoding and preview. Validate dimensions, channels and precision;
   publish native payloads atomically and reject unsupported mappings before deployment.
4. Add font selection to the teaching path, using the existing generated-font reader and resources.
   Keep the basic font available; make any runtime scaling an explicit application choice.
5. Add other starter pairs for display shapes, formats and constrained storage. Keep physical drivers,
   RTOS configuration, allocation policies and hardware acceptance in each consuming application.

## Success criteria

The foundation is useful at the M1 gate. The full workstream is successful when:

- a developer can build an installed-package C++ example, define a display, supply bounded storage,
  and exercise its fixed configuration on the host without designer tools, an MCU SDK, or MCU selection;
- RGB565 and XRGB8888 have independent example projects and artifacts, each with its own selected
  resources and measured footprint; sharing H750 validation hardware does not couple their builds;
- format/address-space selection is resolved at build time, and product binaries omit unused
  destination implementations and unselected resource payloads;
- framebuffer and tiled paths use the same display semantics and produce identical bytes within each
  tested format, including genuinely sub-byte R2 storage, without a dummy framebuffer requirement;
- surface compatibility and target feasibility have independent results; target absence is unevaluated
  and target failure leaves valid host simulation available;
- at least one static resource path demonstrates high-fidelity source to native export, and compact
  coverage/indexed resources remain available with explicit costs;
- synthetic 256 x 192 packed-cell fixtures use exactly 49,152 native bytes, with no staging frame;
- exact byte tests prove the declared emitter and bit layout;
- the same display model still describes ordinary RGB565 and XRGB8888 H750 profiles;
- the round display is represented as controller extent, visible shape, and safe area rather than as
  just a square RGB565 surface;
- at least one widget/menu/dropdown validation path consumes resolved display capabilities;
- recorded input/time sequences reproduce host native output and presentation decodes that output;
- supported operation checks distinguish coverage/composition from stored destination alpha;
- form/layout XML remains logical and display-independent;
- target/interface details remain in target profiles and adapters;
- existing RGB behavior and the relevant offline M33/M7 budgets remain within their gates; and
- acceptance records distinguish implemented behavior, passed checks, estimates, unevaluated target
  constraints, and physical hardware evidence still required.

