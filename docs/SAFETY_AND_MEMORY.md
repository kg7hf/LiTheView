# Safety, memory, and power model

LitheView is a modernization of an embedded design, not a desktop toolkit reduced after the fact.
The core is C++23, uses no heap, throws no exceptions, requires no RTTI, performs no I/O, starts no
threads, and owns no clocks.

This describes the portable Core's operations, not a requirement that every consuming
application store its objects at static addresses. An application can own a bounded
layout, framebuffer or transfer workspace in dynamically allocated storage and lend
it to Core while preserving those lifetimes and capacities. The planned shared-system
examples will offer static-only, C-heap and RTOS-backed ownership with explicit
allocation phases; see [the memory-policy plan](SHARED_APPLICATION_PLAN.md#build-time-memory-policy).
Those configuration options and adapters are not implemented yet. Core's existing
allocation-free parsing/layout/input/animation/rendering contract remains intact.

## Bounded storage and work

Generated `Form::Instance` owns its bounded layout and controller together. Optional
`WidgetOverrides` owns actual derived controls in typed `optional` slots with their
real size/alignment and no heap fallback. Partial builds destroy constructed objects;
a live form cannot rebuild its storage. Detach its controller before destruction.
Button hooks commit control state before one form notification; recursive controller
input dispatch is rejected. Callbacks must not destroy/reparent the active scene.
All dispatch and UI mutation belong to one owner task. See [control objects](CONTROL_OBJECTS.md).

- `StaticLayout<N>` places views in a private, explicitly tagged union with caller-selected capacity.
  An optional `WidgetSet` limits the kinds it can build; the code for other widgets is then never
  referenced, so `--gc-sections` drops it. For the checked Alarm Time form on Cortex-M33, the form's
  own `Layout` links 22,568 bytes of text instead of 30,628 for an all-kinds layout.
- The retained tree uses intrusive child/sibling links. A leaf does not carry an array of unused child
  pointers.
- XML nodes and animation frame names are written through spans over caller-provided fixed-capacity
  arrays.
- Controller depth, focus collection, children per view, XML nesting, and dirty rectangles have compile-
  time ceilings.
- Scenes and overlays share the existing fixed `config::kMaxControllerDepth` entry array. Transition
  state retains indexes, elapsed time, and offsets rather than framebuffer copies.
- The single-contact gesture recognizer stores one origin, current position, timestamps, thresholds,
  contact ID, and explicit state flags. It allocates nothing and requests clock advancement only while
  a stationary contact or transition requires it.
- Picker items remain one delimiter-backed ROM string rather than a per-widget pointer table.
- Editable text remains in a caller-owned NUL-terminated buffer; XML carries only its binding name and
  maximum byte length.
- XML serialization and tiled rendering use bounded spans over caller-provided buffers.
- Parser, builder, writer, surface-validation, and tile-sink failures are explicit values; capacity
  exhaustion fails closed. `Display::present()` returns `void`, so a full-frame hardware adapter must
  expose and check transport faults separately; a successful render is not proof of successful I/O.

The build defaults to `-fno-exceptions -fno-rtti` (or the MSVC equivalents). A 32-bit compilation unit
contains regression assertions for the core view, picker, button, tab bar, tab view, wheel picker,
parse spec, state-style entry, and `StaticLayout<16>`. With the current Arm ABI those objects are 80,
104, 108, 96, 100, 108, 120, 32, and 1,816 bytes respectively; the button, tab, and wheel ceilings are
the 112-byte largest layout member, so these widgets cannot grow the layout union. Button icons, tab
titles, wheel items, and state-style tables are borrowed, never copied. The
NUCLEO-U545RE-Q probe separately limits the complete four-screen demo plus four-row strip buffer to
16 KiB of object storage.

## Framebuffer choices

A conventional `Display` supplies a complete writable surface with an explicit RGB565, RGB888,
XRGB8888, or ARGB8888 format. Win32 and STM32H750B-DK each offer the original 261,120-byte RGB565
framebuffer and a 522,240-byte high-color variant. The Win32 RGB565 adapter uses a separate host-only
XRGB8888 DIB buffer for presentation; that buffer is not part of the embedded memory model. The ST
RGB888 BSP mode uses ARGB8888 LTDC storage, exposed as opaque XRGB8888 by LitheView. Both embedded
variants fit in external SDRAM. Logical color remains 8-bit RGBA until the painter writes that surface.

`TiledRenderer` is the constrained alternative. It renders the same view tree into a reusable caller-
owned tile and synchronously passes each format-tagged tile to a `TileSink`. The tile carries its
absolute screen origin, so clipping, text alignment, icons, scroll views, and animation frames match
full-frame output. The test suite compares arbitrary RGB565 and XRGB8888 tiles byte-for-byte with their
full framebuffers.

The U545 probe uses 480 by 4 pixels: 3,840 bytes instead of 261,120 bytes for a 480 by 272 RGB565
framebuffer. A later panel adapter may select another tile width/height based on its bus transaction and
DMA constraints.

The optional `DisplayTransferAdapter` makes DMA, DMA2D, LTDC layers, asynchronous ownership, and
completion explicit. A pending transfer retains its caller-owned source until its token reaches a
terminal state; no portable rendering API silently extends a span lifetime or blocks for hardware.

## Power behavior

The core is event-driven. Input or model changes invalidate views; `render()` returns without presenting
when nothing is dirty. Animation has no internal timer. The application supplies elapsed milliseconds
only while the active screen has a running animation, and can query `animationsRunning()` to decide
whether a wakeup is required.

The same caller-owned clock advances stationary long-press recognition and scene transitions. Pointer
events carry timestamps for event-to-event timing. Navigation cancels captured input and gesture state;
input is rejected during a transition, and explicit transition cancellation restores a defined stable
stack. No background timer, callback, or hidden scheduler is created.

Transparent-child invalidation carries a bounded 32-bit root-space rectangle to the nearest repainting
ancestor. The ancestor and any overlapping children are redrawn through that clip, so restoring the
background remains correct without turning a small animation into a full-display update. The display
adapter receives only the resulting dirty regions; on H750 it cleans only those framebuffer cache lines.

The full-frame U545 checksum pass is a manufacturing/debug/CI probe, not a required periodic task. A
battery product should sleep on input, model, and scheduled-animation events and should not poll the
graphics framework.

## Integration obligations

The product remains responsible for:

- validating display geometry, buffer address/stride, DMA completion, and cache coherency;
- mapping touch coordinates through panel orientation/calibration before dispatch;
- ensuring XML, strings, fonts, bitmaps, and animation-frame arrays outlive their views;
- supplying and synchronizing mutable text buffers for any editable fields;
- placing large immutable resources in appropriate ROM/external memory sections;
- serializing access when model updates and rendering can occur from different tasks;
- choosing watchdog, diagnostic, and safe-state behavior for display or resource failures;
- testing worst-case parse, navigation, animation, and transfer timing on the selected MCU.

These are explicit platform/application responsibilities rather than hidden global services.
