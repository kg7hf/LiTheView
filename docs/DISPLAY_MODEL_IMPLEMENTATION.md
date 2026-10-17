# Display model implementation

The portable runtime separates the display's logical contract from its transport and target budgets.
The current direction and remaining work are in [DISPLAY_MODEL_PLAN.md](DISPLAY_MODEL_PLAN.md).

## Implemented contracts

- Display definitions describe logical extent, shape, safe area, address space and native storage.
- Logical input is configured separately from the display. Physical drivers translate into pointer,
  key, encoder, activation and time events.
- RGB and packed destinations are selected at build time. Checked layouts describe storage blocks,
  row stride, alignment and complete-block requirements; they reject invalid or undersized buffers.
- Painters render native bytes into caller-owned full-frame or tiled storage. Host presentation decodes
  those bytes only at the operating-system window boundary.
- Surface compatibility can pass before an MCU is chosen. Target feasibility separately evaluates
  flash, workspace, transfer and other budgets using explicitly identified evidence.
- Version-3 designer projects lock native display definitions and logical input. Editing, native preview
  and generated components share the display contract. Revalidation rejects stale snapshots and resources.
- Typed controls, named accessors and handler declarations connect generated forms to application code.
  Application-owned handler bodies survive regeneration.
- Configure reopens display and input settings. Application path settings support arbitrary project layouts.
  Front-view metadata describes host hit regions for simulated physical controls.

## Maintained examples and tests

The [H750 starter pair](../examples/README.md) teaches one application with Win32 and STM32 builds.
The application owns its RTOS, MCU drivers, transport and input adapters; LiTheView remains a middleware
dependency. The designer supplies layout and generation while CMake and VS Code own build and debug.

The [test guide](../tests/README.md) distinguishes unit tests, rendering regressions, designer integration
and installed-package checks. Synthetic packed displays exercise format correctness without representing
a physical controller. Exact-byte tests and full/tiled comparisons remain required.

## Remaining work and evidence limits

Text entry, dropdown lists, table selection and named tab events need additional typed control handlers.
Bitmap resource preview and prepared-image-to-native asset publication need further integration.
Generated fonts exist as optional resources; the starter currently chooses the compact 5 by 7 font.

Host rendering, compiled target feasibility and physical display/input acceptance are separate evidence
stages. No physical panel or sensor is qualified by a synthetic fixture or archive-size measurement.
