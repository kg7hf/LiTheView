# Library and example ownership cleanup

The reference workflow is one application with Windows and STM32H750 builds. Adding a UI changes
the application's contents, not its preset or launcher. These boundaries support the named-control and
handler work in [VSCODE_DESIGNER_PLAN.md](VSCODE_DESIGNER_PLAN.md).

## Boundaries

The subsequent retirement pass removes the initial showcase applications as developer examples.
Only the H750 starter pair remains under `examples`. Rendering scenes,
native-format configurations, display probes, handwritten integration checks and their artwork now
live under `tests/rendering`. The obsolete H750 executable/product overlay, vendor submodules, root board
presets, launchers and flash helper are removed. Other displays and features will be taught through
new starter lessons. Core has no FreeRTOS, STM32 HAL or CMSIS dependency.

- **Core:** portable view/control behavior, rendering, native surfaces, input dispatch, font decoding
  and bounded layout/resource contracts. No bundled font tables, example application, RTOS or driver.
- **Resources:** optional immutable fonts with their own CMake targets and retained provenance.
  Application-specific graphics remain with their owning application/example.
- **Designer:** editing, native preview, surface validation, prepared bitmap validation/native encoding
  and portable component generation. General image preparation remains in external tools; resource
  preview/import workflow completion is still planned.
- **Application:** startup, tasks, device adapters, state/handlers, CMake build and debug launchers.
- **Tests:** smoke projects and example/probe conformance fixtures, with separate portable tests.

## Implemented cleanup

1. Moved bundled font data and metrics to `resources/fonts`. Core contains only font machinery.
   `CompactFont` and `GeneratedFonts` are explicit link dependencies. The compact default remains
   source-compatible; an application using it now links `LitheView::CompactFont`.
2. Removed the installed Demo target and example/animation headers. RGB examples and the showcase
   capture consumer compile their own scene code against Core and selected fonts.
3. Separated `LITHEVIEW_BUILD_TESTS` from `LITHEVIEW_BUILD_RENDERING_TESTS`. Portable tests work with
   examples and board probes disabled. The full conformance suite still requires its complete matrix.
4. Moved designer smoke projects to `tests/designer-fixtures`, animation source art to
   `tests/rendering/assets/animations`, and retired the unreferenced DesignerTest experiments.
5. Generate UI now emits a component: `form.hpp`, `display.hpp`, `resources.hpp`, selected native
   sources, `resources.cmake`, portable aliases, interface CMake integration and `component.json`.
   It creates no main, task, display driver, simulator runtime, unused `application.hpp`, or VS Code
   build/debug configuration. CLI parity is `component export-project <project.json> <designer-checkout>`.
6. A legacy export can migrate only after all previously generated hashes are verified. Matching
   obsolete generated files are removed; handwritten files are preserved. An incomplete publication
   marker prevents application builds from consuming partially written outputs. Conflicts fail before
   publication. The starter watches both the new marker and the older `simulation.json` during migration.

## Follow-up cleanup

The unused fixed-geometry Win32 native adapter and its dormant native/encoder build branches are
removed. Generic input handling elsewhere is unchanged. Packed-row decode checks now use varied
synthetic sizes, including one storage block, exact emitter bytes, boundary guards and rejected spans.
The Win32 inspection option names its actual XRGB8888 storage; an old RGB888 cache receives an
explicit reconfigure diagnostic.

The unreferenced `simulate-designer-project.ps1` wrapper is removed. The maintained CLI simulator
export/build regression facility remains separately documented and tested. Shared contained-path,
reparse-point and hash checks now live in `IO/GeneratedFileAccess`; current component generation
references legacy simulation types only for safe migration of old generated output.

The target catalog contains two live U545 probe profiles and two explicitly synthetic Cortex-M7
planning fixtures. Provenance paths and canonical hashes are checked; invented limits do not claim
starter linker enforcement. The conformance runner now invokes all ten installed rendering-strategy
consumers. General image preparation/import scope remains the separately planned asset-workflow
revision; this cleanup does not remove a converter still used by resource tooling.

## Tool and path portability

Shared maintenance commands use PATH or caller overrides; tool versions remain explicit requirements,
not installation paths. The [build environment guide](BUILD_ENVIRONMENT.md) documents prerequisites,
local presets and the starter-only hard-coded paths deliberately left for the later application cleanup.
Core source and installed-package consumers pass from relocated paths with spaces, without any vendor
submodules. The animation converter also discovers its child shell through PATH and omits empty
optional arguments that Windows PowerShell would drop. See the handoff for the validation scope.

## Deliberate limits and next increment

The explicit legacy CLI simulator remains a test/tooling facility. The designer no longer calls its
exporter. Existing `lithe::simulator` aliases in `product_config.hpp` remain source-compatible and own
no simulator runtime. They can be migrated separately with the named form-object interface.

The starter's button/label increment now has named accessors, discoverable Create/Open click handler,
preserved user code, form/button hooks and application-owned input. `WidgetOverrides` supplies actual
typed construction/storage/alignment/destruction for a selected derived control; it does not just
remove `final`. See [control objects](CONTROL_OBJECTS.md). The next priority is the
[control-event milestone](VSCODE_DESIGNER_PLAN.md#control-event-milestone), including a developer
walkthrough and separately recorded host/target evidence before the image/font workflow increment.
The UI owner handles controls; cross-task messages remain an application boundary. No erased C++
callback context or hidden allocation is introduced for convenience.

The subsequent bitmap workflow validates prepared image samples and emits the selected native storage
format before compilation. Lossless channel/byte reordering and display-specific packing belong to
LiTheView; resizing, colour reduction, dithering and palette choices belong to external preparation.
See [the asset contract](DISPLAY_MODEL_PLAN.md#source-validation-and-native-asset-encoding).

## Validation

Evidence for this cleanup is recorded in `build/cleanup-work` and the corresponding
`build/cleanup-*` directories. Core-only source/package consumers include a caller-owned font and
framebuffer-free rendering. Full native scene tests, unchanged RGB showcase frame checks, designer
ownership/migration checks, and the H750 same-preset progression cover the affected boundaries.
Host tests, cross-builds and physical board execution are separate evidence stages; see the latest
agent handoff for the completed runs and any remaining limits.
