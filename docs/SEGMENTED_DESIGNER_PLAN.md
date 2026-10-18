# Segmented displays in the designer

Status: project integration and VS Code authoring implemented, 2026-10-17.
Generated-runtime adoption and automated target validation are implemented.
The owner accepted the generated build on the PCB2100 A03 board, confirming
play/pause, slider navigation, volume adjustment, cancel/save and the ring.
The owner also confirmed "The Windows simulator works too" for the generated
presenter. Both platforms now have automated checks and owner acceptance.
The exact scope and firmware identity are recorded in the example's
[validation report](../examples/efm32tg-stk3300-menu/VALIDATION.md).

### First implementation increment

The designer Core and CLI now validate version-1 glass JSON and segmented form
XML and export typed, bounded C++ encoders. Tiny Gecko source definitions live
under the example's `UI/design`; a second synthetic glass checks generic behavior.
See [the compiler consumer](../tests/segmented_designer_consumer/README.md) for
commands, schema rules, generated interfaces and tests.

Version-4 projects now lock a segmented glass and logical input without a raster
surface. Core/CLI resolve and generate every screen; the native worker previews
segment bits with capability negotiation. VS Code 0.2.15 supports field bindings,
preview values, Configure/import glass, screen navigation and Add screen.
See the [Tiny Gecko designer lesson](../examples/efm32tg-stk3300-menu/docs/segmented-designer.md).

S0-S3 now have implementation and service/extension-host evidence. Initial project
creation remains CLI-based, polygon editing is JSON-based, and native preview is
bounded to 1024 by 1024. The standalone desktop designer rejects v4 with an
explicit diagnostic. S4 now connects the generated header to both platform mains
through the handwritten presenter. The FSM is unchanged. Windows tests and target
program/readback/startup checks pass; see the validation report for owner acceptance.

## What we are building

I want the developer to describe the glass, bind application values to its fields,
and generate the presentation code. The developer's application still owns the
menu, navigation, state, device effects and timing. The same presentation code
must produce the same segment bits in Windows and on the embedded target.

The basic object is an individually addressable segment. Some segments look like
icons, some are strokes in a character, and some are pieces of a ring. They are
fixed physical shapes that can be on or off. They are not bitmap assets.

| Object | Example | Presentation value |
| --- | --- | --- |
| Individual segment | Padlock or antenna symbol | Boolean |
| Ordered segment group | Eight pieces of the volume ring | Explicit piece states, or a bounded value with a declared scale |
| Character cells | Seven fourteen-segment positions | Bounded text using the declared glyph set |
| Numeric field | Four seven-segment positions | Number with explicit formatting and range |

An icon may contain several electrodes. Its definition can name that fixed group,
and a Boolean turns the group on or off together. A ring exposes its pieces as
individually addressable segments and can also offer a convenient ordered-group
binding. A form must choose one writer for each electrode; it cannot drive the
same ring through both a percentage binding and individual piece bindings.

Animation is a later behavior that changes these values over time. It is not the
storage model and is not needed to display volume. Blinking or chasing segments
would use an application-owned timer and submit another presentation value.
No timer, FSM, task or ISR is generated for an icon.

## Three separate descriptions

1. **Glass definition:** stable segment IDs, fixed geometry, character cells,
   glyph maps, named icons and ordered groups. Geometry is for the host's drawing
   and the designer's canvas. The segment IDs describe logical outputs.
2. **Form:** which fields are used, their typed bindings, formatting, defaults
   and preview values. Several logical screens can reuse the same glass.
3. **Hardware adapter:** maps the logical outputs to controller registers or
   transport commands. MCU pins, COM reversal, LCD bias, clocking and SDK code
   belong here, in the consuming application.

The existing Tiny Gecko logical IDs use glass COM/S coordinates. Preserve their
meaning for the first comparison, but do not require that numbering scheme in
the generic format. `Platform/EFM32/lcd_rows.hpp` remains responsible for the MCU
mapping. The FSM must not acquire segment IDs, coordinates or register knowledge.

A host viewport has dimensions, but those dimensions are not a framebuffer
extent or a target RAM budget. Windows expands the segment state into visible
shapes only at the final presentation boundary. The embedded application stores
the bounded segment frame, not an RGB image of the glass.

## Fit into the existing designer

`DesignerDisplay` currently requires a width, height and `SurfacePixelFormat`.
That contract works for raster and native packed displays. Segmented glass needs
an explicit display kind with its own typed definition; it must not be another
pixel-format enum value or a fake RGB surface.

Introduce a discriminated display contract with common identity and logical-input
information, and separate raster and segmented payloads. Keep existing version
1-3 manifest interpretation and generated RGB/packed output unchanged. Use a new
manifest version for segmented projects, with locked glass and input snapshots.
Freeze its schema and canonical hash rules in the first implementation step.

Use the same application-owned project paths, Configure command, screen navigator,
Add screen workflow, save/undo behavior and Generate UI command. Segmented forms
can keep the `.litheview.xml` extension with a distinct, versioned segmented root;
the loader must dispatch by the declared document kind and reject mismatches.
Do not reinterpret existing pixel XML or silently migrate existing projects.

All forms live together, for example:

```text
MyApplication/
  Middlewares/Third_Party/LiTheView/
  Application/                    # Handwritten FSM and device behavior
  UI/
    project.litheview.json
    profiles/glass.segment-display.json
    forms/home.litheview.xml
    forms/setup.litheview.xml
    forms/volume.litheview.xml
    forms/info.litheview.xml
    Generated/                    # Generated descriptions and encoders
    presenter.hpp                 # Handwritten state-to-presentation mapping
  Platform/Win32/
  Platform/EFM32/
```

These folders are configurable. The Tiny Gecko example stays bare metal. A form
is not a task, and multiple screens do not require multiple RTOS tasks or projects.

## What the developer does

1. Use Configure to define a segmented display and its logical input. Start from
   a custom glass definition, with the tutorial explaining the Tiny Gecko values.
   Importing a versioned JSON description is sufficient for the first glass
   authoring step; a drawing tool is not a prerequisite. Include a readable
   template and actionable validation, not a required Tiny Gecko preset.
2. Inspect the complete glass in the designer. Select named fields or individual
   segments to identify them. An identification preview changes host preview
   values only; it does not flash or operate hardware.
3. Add a screen and choose its field bindings. For Home, bind text, numeric value
   and volume ring. For Volume, use the same physical fields with draft values
   supplied by the presenter. Physical shapes stay fixed in normal form editing.
4. Set the ring's order, range 0-100 and nearest-eighth rounding. At 50, preview
   four pieces clockwise from the top. Defaults and sample values are preview
   inputs, not a saved copy of the application's FSM.
5. Generate UI. Connect the generated typed presentation interface to the
   handwritten presenter, then build and debug through the existing Windows
   CMake preset. Switch to the embedded preset to run the same application.

The palette shows the glass's available fields, not pixel buttons or arbitrary
images that the glass cannot display. The fixed segments are selectable in the
editor without becoming interactive controls on the physical display.

PB0, PB1 and the slider remain separate input devices. Windows mouse/keyboard and
hardware adapters produce the same logical events. The FSM consumes previous,
next, select and back; the presenter selects a form and supplies values. Form
generation does not generate GPIO handlers, queues or navigation transitions.

## Generated interface and preview

The first generated interface replaces the roles of `UI/glass.hpp` and
`encodePresentation()` while leaving `Application/menu.hpp` and the state-reading
part of `UI/presenter.hpp` handwritten. Its exact C++ names are decided with the
schema, but these properties are required:

- Named, typed inputs for text, number, Boolean symbols and ordered-group values.
  No expression strings evaluated as code, runtime string dispatch or `void*`
  callback contexts. Do not generate unused event stubs for passive symbols.
- Bounded immutable descriptions and `lithe::SegmentFrame<N>`, usable through
  `LitheView::Segments` without Core's raster renderer, an SDK or an RTOS.
- Synchronous encoding with explicit failure. Borrowed text is consumed during
  the call and never retained. Unsupported characters, excessive text length,
  invalid numbers or out-of-range values reject the presentation before publish.
  The caller retains the last valid frame and can report the error.
- Every successful encode defines the entire frame, including off bits, so a
  screen change cannot leave stale symbols. No hidden heap, exceptions, mutable
  global UI state or dependency on a system clock.
- Handwritten presenters, handlers and the FSM are never overwritten. Generated
  files have a clearly bounded output tree and ownership manifest.

The volume ring binds `volumePercent`, not the general numeric field: Info shows
3300 while the ring still shows saved volume. The presenter decides committed
versus draft volume, including cancel and save. Encoding never makes that decision.

The native preview must exercise the same C++ encoding rules as the application.
Extend the worker protocol with an explicit segmented capability and bounded
segment-frame/geometry messages. Reject an old worker with a rebuild diagnostic.
Do not squeeze the new data into the pixel ABI's format or stride fields, or
duplicate the encoder in JavaScript. Host geometry rendering is separate from
the encoder and consumes its bits, just as the current Windows simulator does.

The first interactive deliverable is the VS Code plug-in plus Core, CLI and service
support. The desktop designer must either support the new document kind or reject
it clearly before editing; it must not crash or pretend it is a raster form.

## Validation boundaries

Glass and form compatibility is checked without selecting an MCU. Validate IDs,
segment references, glyph capacity, supported formats, group order, exclusive
writers and bounded geometry. Reusing an electrode on different screens is normal;
two writers on one screen is an error. Shared/aliased field overlays are deferred.

Check logical input separately from hardware feasibility. Flash, RAM, stack,
controller mapping and transfer constraints are evaluated by the target build.
An unevaluated target must not prevent a valid host preview or generation.

Treat imported definitions and forms as untrusted data: bounded counts and vertex
lists, finite coordinates, no scripts or arbitrary SVG execution, no XML external
entities, and paths constrained to the existing approved project/output rules.
Re-check snapshot hashes and preflight every requested screen before writing any
generated output. Validation failure writes nothing. Define and test recovery
from output I/O failure; do not claim a multi-file filesystem operation is atomic.
Preserve workspace trust, dirty-document checks and editor undo when adding screens.

## Implementation stages and acceptance

| Stage | Work | Required evidence |
| --- | --- | --- |
| S0: contracts | Freeze glass, segmented-form and manifest schemas; typed display-kind boundary; Tiny Gecko fixture plus a small unrelated synthetic glass | Invalid definitions rejected; existing raster and packed project resolution unchanged |
| S1: encoding and generation | Core/CLI resolve and export bounded descriptions and typed encoders; preserve handwritten code | Generated consumer compiles against Segments alone; independent bit oracle; explicit errors and no partial publish |
| S2: preview service | Negotiate segmented worker capability; preview real encoded bits using fixed geometry | Text, numbers, individual icons and all ring percentages agree with oracle; malformed messages and old workers rejected |
| S3: plug-in | Configure/import glass, field palette and properties, preview values, screen navigation, Add screen and Generate UI | Packaged extension walkthrough in an application outside the library, including paths with spaces and unsaved edits |
| S4: Tiny Gecko tutorial | Replace handwritten presentation encoding with generated output; document both designer steps and resulting files | Same FSM source; Windows behavior and embedded frame output equivalent; fresh hardware acceptance |

S1 tests must cover independent on/off pieces as well as scaled groups. For Tiny
Gecko, retain the existing independent threshold/electrode oracle for all 101
percentages, non-ring preservation, clearing, draft/save/cancel and controller
bit masks. Test boundaries on text and numeric fields, rejected overlapping
bindings, stale snapshots and generation into the wrong path. Synthetic glass
must use different geometry and segment numbering to expose board assumptions.

S3 must start from a blank consuming project and a source-prepared installed
extension. Walk Configure, author fields, Add screen, save, reopen, Generate UI,
Windows build and debug. Existing raster/packed preview and generation tests are
regression gates. No Simulate or Build firmware buttons are added to the designer.

At S4, compare with the accepted handwritten baseline: 7,040 bytes flash, 64 bytes
static RAM and 1,024 bytes reserved stack. The observed startup/idle stack use was
400 bytes, not a worst-case bound. Measure the generated build instead of promising
zero overhead. Existing example link budgets and no-allocation constraints apply.

Only after the generated Tiny Gecko path is accepted should we add more glass
symbols, interactive polygon authoring or optional animation helpers. Preserve the
original STM32 starters, unrelated local changes and private reference documents.

## Source entry points

- [DesignerDisplay](../tools/designer/src/LitheView.Designer.Core/Projects/DesignerDisplay.cs)
  and [project manifests](../tools/designer/src/LitheView.Designer.Core/Projects/DesignerProjectManifest.cs).
- [Raster/native definitions](../tools/designer/src/LitheView.Designer.Core/Profiles/DisplayDefinitionProfile.cs)
  and [preview surfaces](../tools/designer/src/LitheView.Designer.Core/Preview/PreviewSurfaceFactory.cs).
- [VS Code extension](../tools/vscode/extension.js), its Configure and Add screen
  flows, and the existing designer service protocol.
- [Tiny Gecko presenter](../examples/efm32tg-stk3300-menu/UI/presenter.hpp),
  [glass mapping](../examples/efm32tg-stk3300-menu/UI/glass.hpp) and
  [volume-ring lesson](../examples/efm32tg-stk3300-menu/docs/volume-ring.md).
- [House style](style_guide.md), [embedded C++ philosophy](02-embedded-cpp-philosophy.md)
  and [overall display plan](DISPLAY_MODEL_PLAN.md).
