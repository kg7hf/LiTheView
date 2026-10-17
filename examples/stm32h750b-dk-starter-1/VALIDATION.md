# Starter 1 validation, 2026-10-03

## Publication validation — October 17, 2026

Both starter source trees include the shared physical-button adapter, Windows photo
adapter and bounded input tests. The guided tree remains blank in Git; its local
exercise UI is preserved and ignored. The completed tree includes the finished UI.

The reviewed library pin is `5e83237afb75ed4bbb8c532ed650e694efbba4d8`; vendor pins
are unchanged. GCC 16.1.0/i686 built Windows; Arm GNU GCC 14.3.1 built STM32 Debug.
The fresh progression uses an application path and compiler path containing spaces.
It passes blank-to-generated builds on the same two presets, CTest 2/2 before and
after generation, ON/OFF mouse interaction, front-panel counter/hold/cancel checks,
and rejection of partial output, overlapping/unmapped hit regions and a changed
photo hash. Missing compiler, x64 compiler and invalid debugger overrides fail closed.

The completed sibling also passes its own fresh configure/build for both platforms,
CTest 2/2 and both interactive self-test modes. Its STM32 image uses 61,192 bytes
of flash, 14,576 bytes of DTCM and 522,240 bytes of SDRAM. The normal Windows GDB
path stops at WinMain and runs the RTOS self-test to normal exit. Arm GDB reads main
from the ELF. No board was probed or flashed, and the F5 UI was not exercised anew.

Run from the library root:

```powershell
./tools/test-h750-starter-progression.ps1
```

Retained evidence: `build/starter-progression/ed227388d36e40e2a8ad95e9655f42ba/`
and `build/starter-publication-20261017/`. The isolated source copies reuse pinned
vendor checkouts through test-only junctions. Public clone initialization remains a
cutover check, after the future history bootstrap and self-submodule repinning.
See [the provenance inventory](../../licenses/stm32-examples.md) for the outstanding
CubeMX template grant; successful compilation does not resolve that license issue.


## Device front-view bonus lesson (2026-10-04, Designer 0.2.8)

The next section uses a developer-supplied PNG, marks LCD and physical-button
areas, and explicitly maps the named PC button and target GPIO to a shared device
read, debounce and counter handler. The complete sibling includes this end state.
The learner's existing local form was not modified.

Verified: 72 extension unit tests; installed-VSIX configuration/create/edit/
generate coverage, including front-view save/reopen/cancel/no-op; isolated
blank-to-complete Windows and STM32 builds with the same presets; native RGB565
counter repaint after two button presses, hold and cancellation behavior, and
transformed LCD clicks retaining ON/OFF behavior. Compile-time C++ checks cover
bounce, held state, repeated presses, timer wrap and scaled hit boundaries.
CMake rejects overlap, unconnected device names and changed image hashes.
The root cast audit passes without additional casts.

Evidence in the outer checkout:
`build/starter-progression/e8be9b1f57544a519f45cbdfd9b3592c/result.json`,
`build/vscode-designer/integration-20261004T212546765/application with spaces/vscode-validation.json`,
and `build/front-panel/`. Completed STM32 Debug uses 58,632 bytes FLASH,
14,560 bytes DTCM and 522,240 bytes framebuffer SDRAM. Only the Windows build
reads the photo/sidecar. Physical B1 execution was not tested in this increment;
the tutorial includes the target acceptance step. No MCU pins or submodule pins
changed. Designer 0.2.8 was packaged and installed in the default VS Code profile.

## Configure workflow update (2026-10-04, Designer 0.2.7)

The guide now returns to the original display/input setup fields through
**Configure -> Input: Touch -> Apply**, followed by Generate UI and the normal
application build. No application source, preset or launcher changes in this update.
The managed solution builds with zero warnings/errors; 463 service checks and 67
extension tests pass. Packaged VS Code integration verifies saved settings,
input-only changes, display format changes, cancellation/no-op, generation and
source preservation for RGB and packed projects. Evidence: `build/project-setup`
at the parent checkout and its current `docs/AGENT_HANDOFF.md` entry.

This update did not rebuild or run the H750 application. The application checks
below belong to their recorded earlier checkpoints; no new hardware result is claimed.


## Input progression checkpoint, 2026-10-04

Designer 0.2.6 and the revised lesson add an explicit input section after the
button/label handler. Configure input updates the independent snapshot and its
manifest lock, then Generate UI enables pointer input in the same application.
The actual packaged VS Code workflow passes. An isolated copy was also regenerated
through this new service operation: Output only ignored injected pointer samples;
Touch produced ON/OFF/ON frames with the same Windows preset; STM32 Debug rebuilt
successfully. The source form and handwritten handler hashes did not change.

Evidence in the outer checkout: `build/input-work/application-input-result.json`,
`build/input-work/{outputOnly,touch}-interaction.log`, and
`build/vscode-designer/integration-20261004T200338180/application with spaces/vscode-validation.json`.
The existing blank-to-generated test also passed both presets at
`build/starter-progression/212e8ade614d47afaf8b33f343c8b13a/result.json`.
The developer's local UI was preserved. No physical hardware was accessed; these
are host execution and cross-build results, not physical touch acceptance.

## Typed-control library pin

This application pins LiTheView `1b1e0859ffd1754dabf184a63c5565f360c1e4e9`.
The blank-to-generated progression builds both platforms against this actual pin,
using the same presets and build directories, rejects partial exports, and passes
the ON/OFF/ON injected-pointer frame check. The completed sibling passes its Windows
test and STM32 Debug cross-build with this pin (FLASH 56,920 bytes, DTCM 14,408 bytes,
external SDRAM 522,240 bytes). Evidence at the outer root:
`build/starter-progression/d82b15677377475d80071ee6ab7c54ac/result.json` and
`build/interaction-work/complete-pinned-*.log`.

The developer's untracked guided-starter UI remains unchanged. It still declares
Output only and uses an older generated header: migrate input deliberately and
Generate UI with Designer 0.2.4 before using it with the updated lesson. The isolated
progression check excludes that UI and validates the checked-in starting point.

Designer 0.2.4 is packaged, installed in the default VS Code profile, and passes the
real packaged custom-editor/handler integration. Reload the VS Code window after upgrading.
No physical hardware was accessed for this checkpoint; target execution and physical
touch verification remain separate from these cross-build and host results.

## Showcase retirement checkpoint

The application now pins LiTheView `2506b75ddcc394f2ac87e00d7202350cdb67709f`. Its existing `win32-debug`
preset and host tests pass, and `stm32-debug` configures and builds against that pin. Platform
submodules and application code are unchanged. The library's old root FreeRTOS/Cube dependencies
and showcase applications are removed; reusable rendering checks moved to `tests/rendering`.
The former example build options are now `LITHEVIEW_BUILD_RENDERING_FIXTURES` and
`LITHEVIEW_BUILD_RENDERING_TESTS`. Logs are in the outer repository's `build/retirement-work`.
No hardware was accessed for this retirement. The records below describe earlier checkpoints.

## Library/resource cleanup checkpoint

The library submodule now pins `c3377a585e742ab473f7af94f7cde4bbd86b5c8a`.
Windows CTest and the STM32 Debug cross-build pass against this pin, with the developer's
existing untracked UI untouched. Logs are `build/cleanup-work/starter-pinned-{host,target}.log`
at the outer repository root. The isolated blank-to-generated progression also passes with
the same two presets; see the root agent handoff. Designer 0.2.3 is installed in the default
profile. No board was flashed during this cleanup; the hardware evidence below is historical.

## Earlier workflow validation: one application, two platforms

At this checkpoint, both examples pinned LiTheView `36b9109`, the published workflow correction.
Designer 0.2.2 removes build/run/task controls from both designer shells. Each
starter now has only `win32-debug` and `stm32-debug`, with the corresponding
Windows and STM32H750 F5 launchers. The next build detects the first completed UI
export automatically. A stale `STARTER_WITH_UI=OFF` cache entry is removed.

Checks completed on 2026-10-03:

- 55 extension unit tests, 16 standalone designer tests, and the real packaged
  0.2.2 VS Code editing/creation/generation integration test passed. The integration
  test verifies that generation executes no application tasks and preserves them.
- The isolated progression regression configured both presets before any UI,
  built the blank application, introduced the completed Generate UI outputs, and
  built both presets again without an explicit configure step or new build tree.
  Windows runtime checks changed from zero frames to one. Partial export was
  rejected with a clear missing-header diagnostic.
- The completed reference built with both new presets; Windows CTest passed.
  H750 reference usage: flash 41,164 bytes, DTCM 14,144 bytes, SDRAM 522,240 bytes.
- In the developer's actual starter, real CMake Tools configure/build returned 0
  for both platforms. Windows F5 and its prelaunch task passed; pause/readback
  showed 48 heartbeats, one frame and no assertion.
- Real H750 F5 programmed/verified, stopped at main, continued and passed runtime
  readback on probe `003000483137510C33333639`: 203 heartbeats, one frame, no
  assertion and three tasks. Native framebuffer border `0xb924` and background
  `(10,10)` `0x262a` matched the developer's current form. An initial assertion
  sampled the center after a button was added there; correcting the sample
  location passed. This was a test expectation issue, not a render failure.
- The owner confirmed both builds/launchers and the corrected workflow work.
  They added button1 and label1 and changed the colors. Those local `Tasks/UI`
  files remain untouched and untracked in the guided starter.

The H750 image was left running after debugger disconnect. ELF SHA-256:
`267092969791846ca980bba186d45fc09deb721960f59416e712fe34ef485c8b`.
The L4R9 probe was enumerated but not used. No input/event-handler behavior or
worst-case timing/stack claim follows from these display checks. Named handlers
and input are the next step in the same starter, not yet implemented.

Local evidence, relative to the outer repository:

- `build/starter-progression/8efd4a07798f42039cfe764de5924f48/result.json`
- `build/vscode-designer/integration-20261003T184415397/application with spaces/vscode-validation.json`
- `build/starter-workflow-validation/win32/result.json` and `dap.jsonl`
- `build/starter-workflow-validation/stm32/result.json`, `dap.jsonl`, `gdb-client.log`
- `build/starter-workflow-validation/designer-app-tests.log`
- `examples/stm32h750b-dk-starter-1-complete/build/current-win32-validation.log`
- `examples/stm32h750b-dk-starter-1-complete/build/current-stm32-validation.log`

Re-run `tools/test-h750-starter-progression.ps1` from the library root to exercise
the same-preset transition without modifying local tutorial work or using hardware.

## Earlier paired-lesson validation (historical)

The evidence below predates removal of feature-specific presets and task dispatch.
Its references to 0.2.1 and separate UI builds describe that historical checkpoint.

## Current paired lesson

The owner requested a clean reset. The local `Tasks/UI` project was removed; the
starting example contains no form/profile/generated UI. Its blank Windows RTOS
application build and CTest check passed again after the reset. The blank STM32
Debug build also passed: 27,396 bytes flash, 13,256 bytes DTCM, zero framebuffer
SDRAM reservation. These current figures supersede the earlier build below.
LiTheView is now
pinned at `8013289` (Designer 0.2.1).

The README now teaches **Custom display**, a blank root canvas, manual fill/border
properties, save and Generate UI, then the normal Win32 build/F5 followed by STM32.
The [completed sibling validation](../stm32h750b-dk-starter-1-complete/VALIDATION.md)
records the new creation, builds and both actual debug launchers.

## Earlier 0.2.0 acceptance (historical)

The following evidence predates the paired custom-display lesson. Its automatic
border and separate preview steps have been superseded; the initial blank-platform
build evidence still applies to the unchanged platform sources.

The tracked example contains no `Tasks/UI` project or generated form. Tests used a
separate application at `build/starter-1-acceptance/application with spaces` in
the outer LiTheView checkout. The five platform dependencies were referenced there
through test-only junctions. The LiTheView dependency was deliberately absent for
the blank builds, then checked out normally at `4bd402d` for the UI lesson. The
designer rejected a junction as a library path, as its path protection requires.
The distributed example uses actual pinned submodules for all six dependencies.

## Completed checks

| Stage | Result |
| --- | --- |
| Blank Windows, LiTheView absent | GCC i686 build and real FreeRTOS application-task test passed; zero presented frames |
| Blank H750, LiTheView absent | STM32 Debug cross-build passed; 27,516 bytes flash, 13,256 bytes DTCM, zero framebuffer SDRAM reservation |
| Blank H750 Release | Cross-build passed; 22,888 bytes flash, 13,256 bytes DTCM, zero framebuffer SDRAM reservation |
| Project creation | Same request builder and service path used by Add UI created a 480 Ã— 272 RGB565 output-only form with a two-pixel white border and gray fill |
| UI-only simulation | Exported Windows preview built and its native generated smoke check passed |
| Full application Windows UI | Same application task plus generated form and Windows display adapter built; test checked four task ticks, one presentation, white corners and RGB565 gray center |
| H750 UI cross-build | Debug: 41,232 bytes flash, 14,144 bytes DTCM, 522,240 bytes framebuffer SDRAM |
| Actual VS Code ST launcher | Prelaunch build, program/verify, breakpoint at main, continue, runtime pause/readback, continue and disconnect passed |

The board was explicitly selected by ST-LINK serial **003000483137510C33333639**.
Enumeration identified STM32H750B-DK. No competing debug client was present. The
also-connected L4R9 probe was not accessed. After approximately ten seconds,
debugger readback recorded:

- `starter::status.heartbeats = 200`
- `starter::status.frames = 1`
- `starter::status.failureLine = 0`
- `uxCurrentNumberOfTasks = 3` (application, idle, timer)
- SDRAM corner pixel `0xffff`, center pixel `0x3186`

The validated image was left running on the H750. Its ELF SHA-256 is
`0abb89e1dd8d8bc24e26233ed42966b1b11ea1402bbd9d1eeaf771352aaf9cd0`.
Physical visual acceptance is separate from these runtime and memory observations.
No touch/button behavior is part of this output-only lesson. Target memory figures
are linker allocations, not proof of worst-case task stack or transfer timing.

## Retained local evidence

Paths below are relative to the outer checkout:

- `build/starter-1-acceptance/blank-win32-debug.log`
- `build/starter-1-acceptance/blank-stm32-debug.log`
- `build/starter-1-acceptance/creation.json` and `create-ui.cjs`
- `build/starter-1-acceptance/ui-preview.log`
- `build/starter-1-acceptance/win32-ui-debug.log`
- `build/starter-1-acceptance/stm32-ui-debug.log`
- `build/starter-1-acceptance/hardware/result.json`, `dap.jsonl`, `gdb-client.log`
- `build/starter-1-acceptance/hardware/harness/run.js`

The VS Code 0.2.0 creation/editor changes separately passed 140 service checks,
87 shared-core checks, 17 standalone-app checks and 59 Node checks. A real isolated
VS Code packaged-extension run exercised cancel, creation, native preview, edit,
save/undo, generation and existing application tasks; its result is under
`build/vscode-designer/integration-20261003T153523005/application with spaces/`.

This proves the blank-to-first-display route. It does **not** claim the proposed
named control/event-handler generator is implemented. That next milestone and
configurable asset-scaling policy are recorded in `docs/VSCODE_DESIGNER_PLAN.md`.
