# Completed starter 1 validation, 2026-10-03

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

The completed form now has a PRESSES counter. The Windows photo hotspot and
STM32 B1/PC13 implement the same typed device input; shared UI-task code debounces
the press and calls the handwritten form handler. The PNG and hit-test geometry
are host-only. Application builds and debug launchers retain the same names.

Verified: 72 extension unit tests and installed-VSIX configuration/save/reopen/
cancel/no-op/generation tests; Windows runtime and STM32 cross-build; same-preset
blank-to-complete progression in an isolated application; two counter updates,
hold/cancel handling, and LCD coordinate transformation preserving ON/OFF/ON.
Compile-time geometry/debounce checks and CMake rejection of overlap, unmapped
button names and stale PNG hashes pass. Root cast audit unchanged.

Evidence in the outer checkout:
`build/starter-progression/e8be9b1f57544a519f45cbdfd9b3592c/result.json`,
`build/vscode-designer/integration-20261004T212546765/application with spaces/vscode-validation.json`,
and `build/front-panel/`. STM32 Debug: 58,632 bytes FLASH, 14,560 bytes DTCM,
522,240 bytes framebuffer SDRAM. No physical hardware accessed for this update;
B1 target execution remains an acceptance step. The developer's starter UI and
all dependency pins were preserved. The installed extension is 0.2.8; the older
pinned middleware package still supplies the validated C++ library.

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

The completed lesson's form and handler were copied into an isolated starter and
regenerated through Designer 0.2.6's input configuration operation. Output only
ignored pointer samples; Touch enabled ON/OFF/ON behavior in the same Windows
application, and the STM32 build passed. Source form/handler hashes stayed unchanged.
See the [guided-starter validation](../stm32h750b-dk-starter-1/VALIDATION.md#input-progression-checkpoint-2026-10-04)
for retained evidence. This checkpoint did not access physical hardware.

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

## Component-only generation cleanup

Both starter submodules now pin `c3377a585e742ab473f7af94f7cde4bbd86b5c8a`.
The completed example passed its Windows CTest and STM32 Debug cross-build against that pin.
Current reference usage: flash 41,160 bytes, DTCM 14,144 bytes, SDRAM 522,240 bytes.
Logs are `build/cleanup-work/complete-pinned-{host,target}.log` at the outer repository root.

Designer 0.2.3 emits `component.json` and portable UI/resource inputs. The completed output contains
no unused application hook, runtime wrapper, executable source, or generated debugger/task files.
The Windows/H750 preset names and launchers remain unchanged. The isolated progression regression
against the cleaned outer library passed both cross-builds, Windows runtime before/after adding UI,
and partial-export rejection without an explicit reconfigure after generation. Evidence:
`build/starter-progression/2e592afc11ad470f85a02bf9e76a4ae8/result.json` at the repository root.

No hardware was reflashed for the resource/export packaging cleanup. The hardware observations below
refer to the earlier workflow validation. See the root agent handoff for the latest submodule pins.

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

This example is the checked-in end state of the custom-display lesson. The blank
sibling has no `Tasks/UI` files. Both pin LiTheView `8013289` (Designer 0.2.1)
and the same five vendor submodule revisions. No vendor tree is copied into the
application or recursively initialized.

## Verified stages

| Stage | Result |
| --- | --- |
| Custom display creation | Same wizard request builder and service as VS Code; 480 Ã— 272 RGB565 storage, RGB888 parallel panel transfer, output-only input, application-owned profile ID |
| Blank-to-bordered form | Creation had no border and generated no code; three property edits set gray fill, white border and width 2; save then explicit export succeeded |
| Regeneration | All 19 output, ownership and handwritten files remained byte-identical on a second Generate |
| Native form rendering | White corner RGBA 255/255/255/255; gray center within RGB565 quantization; no display errors |
| Designer service | 145 checks passed, including blank creation, property edits, explicit export, independent circular/input definition and protected file ownership |
| Extension | 59 unit tests and packaged VS Code integration passed; custom display, blank root, edit/save/Generate and application task dispatch |
| Completed Windows application | i686 GCC build and real FreeRTOS application test passed; expected corner/fill, one presentation and task progress |
| Actual Windows F5 | Existing launcher and prelaunch build passed; live GDB pause/continue observed 53 heartbeats, 1 frame, 0 assertion |
| Completed H750 cross-build | STM32 GCC Debug build passed; flash 41,164 bytes, DTCM 14,144 bytes, framebuffer SDRAM 522,240 bytes |
| Actual H750 F5 | Existing ST launcher and prelaunch build passed; programming/verification, breakpoint at main, continue, runtime pause/readback, continue/disconnect |

The H750 was explicitly selected by ST-LINK serial **003000483137510C33333639**.
Enumeration also found L4R9 serial `0676FF504955857567024748`; it was not used.
No competing debug client was present. After ten seconds on H750:

- `starter::status.heartbeats = 200`
- `starter::status.frames = 1`
- `starter::status.failureLine = 0`
- `uxCurrentNumberOfTasks = 3` (application, idle, timer)
- SDRAM corner `0xffff`; center `0x3186`

The completed lesson image was left running. Its ELF SHA-256 is
`5b28fa3dbe97b6e5a24a7098fa617cfd632294617391936430b1a1d1decdfd1e`.
These are build/debug/runtime observations, not human visual acceptance or
worst-case stack/timing measurements. Input controls are outside this lesson.

An initial automated Windows `--self-test` debugger experiment did not reach its
expected process termination. The standard F5 configuration was then tested
without that override, using live task counters and debugger pause/continue;
it passed. The separate application self-test passed through CTest.

## Retained local evidence

Paths are relative to the outer LiTheView checkout:

- `build/starter-1-acceptance/create-completed.cjs` and `completed-creation.json`
- `build/starter-1-complete-validation/regenerate.cjs` and `regenerate.log`
- `build/designer-service-tests/22b831c28ca5400bb57cec8bac80bbb7/`
- `build/vscode-designer/integration-20261003T165834637/application with spaces/vscode-validation.json`
- `examples/stm32h750b-dk-starter-1-complete/build/win32-validation.log`
- `examples/stm32h750b-dk-starter-1-complete/build/stm32-validation.log`
- `build/starter-1-complete-validation/win32-live/result.json` and `dap.jsonl`
- `build/starter-1-complete-validation/hardware/result.json`, `dap.jsonl`, `gdb-client.log`
- `build/starter-1-complete-validation/*/harness/` (private VS Code test runners)

The cast audit counts 9 reviewed starter platform boundary lines and 10 in the
completed example, including one additional generated Win32 template boundary.
The portable-core ceiling remains unchanged. The completed application compiles
the generated form/resources through its own build; legacy generated simulator
scaffolding is not used by its application targets.
