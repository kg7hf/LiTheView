# Validation: round-display menu tutorial

## Eight-entry Load scrolling and board-photo validation (2026-10-17)

- Completed Windows application: **7/7 CTest checks pass**, covering navigation,
  Volume, playback, Load, photo input mapping and the shared RTOS application.
- Load now contains eight immutable demo entries in a three-row viewport. Checks cover
  scrolling to hidden entries, endpoint clamping, scrolled row-to-identity mapping,
  touch up/down targets, cancellation, selection without loading, and explicit activation.
  Native renderer captures in `docs/load-*.png` include the final scrolled viewport.
- The application owns this SelectionList custom control and typed ListEvents observer;
  no new library table widget or filesystem implementation is claimed. The designer
  edits its named panel slot; the application supplies the runtime custom control.
- The Windows photo is copied alongside the executable and checked against its sidecar
  hash. LCD mouse coordinates map to native pixels. The physical-button hotspot follows
  the application's debounced input path. No photo bytes enter the embedded build.
- Programmed and verified the expanded build on the board below, retaining the previous
  flash image. Status reads report ready=1 and assertion=0 with running task heartbeats.
  On 2026-10-17 the owner confirmed: 'The scrolling works correctly on both boards.'
  This accepts the eight-entry scrolling behavior on the exact firmware recorded below.
- All **331** inventoried original starter files in private/public checkouts are unchanged.
  No library/vendor dependency pins changed during validation. Publication is recorded in Git history.

Loaded ELF SHA-256: `75011c0b1bc0d00829ab84cd049bb54d5279f6c3feb08e7961f90fd2675b5dd0`. ST-LINK: `0676FF504955857567024748`.
Evidence: library-checkout `build/menu-validation/load-20261017/`, especially
`scroll-hardware-run/` for this final firmware. Earlier `hardware-run/` retains
the owner-accepted three-entry build. Earlier radio prototypes are superseded.
ARM FLASH: **85,988 bytes**; framebuffer **365,040 bytes**, render workspace
**4,680 bytes**, unchanged. The final Load handler helper was checked for creation
and overwrite refusal. The guided source is blank again (Tasks/kernel_memory.cpp
only); blank Windows CTest **1/1** and the blank STM32 build pass. Completed exercise
sources are retained in this application's ignored `build/load-tutorial-validation/Tasks/`.
The board still runs the completed firmware, not the blank build. Completed simulator:
`build/menu-validation/load-20261017/scroll-completed-l4r9/starter.exe` at the library
checkout root, with its adjacent board-front.png. Existing idle I2C arbitration and
transparent-sibling redraw issues remain open.

## Load list owner validation (2026-10-17)

The owner confirmed both boards work correctly with the three-entry Load list:
touch selects a row, LOAD updates the loaded name, Back/reopen retains selection,
and L4R9 joystick Up/Down and center select/activate correctly. This acceptance
applies to the retained `load-20261017/hardware-run` firmware, before the eight-entry
scrolling extension. The owner also confirmed the completed L4R9 Windows simulator
works, including its board-photo presentation.

## Custom playback control validation (2026-10-17)

- Windows CTest **5/5** passes: navigation, Volume, playback, the Windows input/RTOS/render
  workflow and task health. Playback checks cover named custom types, generated handlers,
  play/pause/resume/stop, native symbol pixels, disabled/cancelled/dragged input, keyboard
  focus/activation and state retained across navigation.
- Designer service inspect/render/Generate UI succeeds; re-export preserves the handwritten
  Home handler. Application subclasses run in the simulator, not in designer preview.
- STM32 build passes; FLASH **79,276 bytes**. Framebuffer/workspace sizes are unchanged.
  No library or vendor dependency pin changed.
- Preceding Volume-only lesson still passes **4/4**. Restored blank starter passes
  **1/1** and its STM32 build. Completed playback sources are retained locally under
  `build/playback-tutorial-validation/Tasks/`; published Tasks contains only kernel_memory.cpp.
- The board runs the retained playback ELF, while the current build output is the blank
  starter. Use the retained ELF for debugging the board.
- Backed up, programmed and verified the playback firmware. Startup status: ready=1, assertion=0, heartbeat=9158, frames=1.
- On 2026-10-17 the owner confirmed both boards work correctly: Play/Pause changes the
  symbol, caption and status; separate Stop returns to STOPPED; Settings/Back retains state;
  and L4R9 joystick-center activates the focused control. This confirms physical playback
  interaction for the loaded firmware below.
- Existing L4 idle I2C arbitration and transparent-sibling redraw issues remain open.
  The changing playback status label uses an opaque background.
- All **331** inventoried original starter files in private/public checkouts are unchanged.

Loaded ELF SHA-256: `88e55b8b4bc4a6bd25d9688c887cc77c1ab18e57299521f7a9d66fd1a30cbede`.
ST-LINK: `0676FF504955857567024748`. Retained evidence:
library-checkout `build/menu-validation/playback-20261017/`, including host/target logs,
native runtime PPM captures, hardware backups, exact ELFs and startup reads.
The three playback PNGs in docs are actual native runtime renders, not hardware photographs.


## Volume lesson validation (2026-10-17)

- Existing three-page lesson: Windows CTest **3/3** still passes without Volume connected.
- Added Volume with `add-menu-page.ps1 -Name Volume`, authored the README layout, generated
  through the designer service and connected with `connect-menu-handlers.ps1 -Volume`.
  Re-export preserved the handwritten handler. Re-running either helper refused existing
  output; the handler hash remained unchanged.
- Completed Volume stage: Windows CTest **4/4** passes, including native RGB888 progress
  pixels, generated +/- handlers, joystick direction/center behavior, cancellation, bounded
  values and persistence across Back/reopen. The real Windows RTOS input/render path passes.
- STM32 build passes; FLASH **75,468 bytes**. Physical RGB888 buffer remains **365,040 bytes**,
  native render workspace **4,680 bytes**. RAM1's 100% linker report includes the reserved
  stack top as explained below. No vendor or library submodule pin changed.
- Both preset builds pass after restoring the guided source to blank; Windows CTest **1/1**.
  Completed exercise source is retained locally under `build/volume-tutorial-validation/Tasks/`.
- Backed up, programmed and verified this completed Volume firmware on the L4R9 probe below.
  Later startup sample: ready=1, assertion line=0, heartbeat=5608, frames=1. The initial sample
  was before the first loop tick.
- On 2026-10-17 the owner confirmed the Volume build works correctly on both boards:
  touch +/- updates the percentage and bar together, Back/reopen retains the value,
  and L4R9 joystick left/right adjusts while up/down and center control focus/activation.
- The earlier idle I2C arbitration issue remains open. This lesson changes no bus timing,
  recovery policy, power sequencing or physical input mapping.
- The board runs the completed Volume ELF retained below; the current application build
  output is the blank starter. Do not substitute that blank ELF for hardware diagnosis.

Loaded ELF SHA-256: `d3db643a9823d7c9af145be02bc3ece5a4458c63a3ac42df829a9dfd781b77d4`.
ST-LINK: `0676FF504955857567024748`. Evidence: library-checkout `build/menu-validation/volume-20261017/`,
including `hardware-run/`, host/target logs and backups (local, ignored).

The dynamic percentage label has an explicit opaque panel-colored background. A separate
core issue was discovered with transparent sibling dirty regions: updating a transparent
label can clip away an independently dirty adjacent progress bar. Reproduction details
are retained in `transparent-sibling-repro.md` in that evidence directory; the core issue
is not fixed by this example. Current Volume pixel checks pass with independent redraw regions.

Existing starter-1 work: all 331 inventoried private/public files unchanged.

## Previous three-page validation

2026-10-17; MB1311 C-02 / MB1314 C-01. The published application starts blank.
Completed exercise forms used for verification are local ignored artifacts, not another example.

## Developer progression

1. **Blank:** Windows build and RTOS smoke test pass (1/1); STM32 build succeeds.
2. **Custom display:** created through the same `creationRequest` and `project.create` service used by the VS Code wizard: 390 x 390, circle, RGB888, Touch + keys.
3. **First panel:** Generate UI, then ordinary `cmake --build --preset win32-debug` and `stm32-debug` pick up the new form without a separate configure command or preset. Windows test passes (1/1).
4. **Pages:** `add-menu-page.ps1` creates Settings and Storage with shared display/input locks. Designer inspect/render/export passes for all three.
5. **Handlers:** `connect-menu-handlers.ps1` connects the handwritten template bodies to actual component IDs. The same build presets detect navigation. Windows CTest passes **3/3** and STM32 compiles.

Navigation checks execute all nine generated button callbacks, 100 nested Home/Settings/Storage
cycles, duplicate opens, logical Back, focus restoration and bounded requests. The round test
renders with the generated circular viewport and checks a black outer corner plus a filled
left-center edge, proving that the root is not clipped to an inscribed square.
Windows input-to-task/frame checks are separate from physical touch tests.

Final exercise build: FLASH **71,060 bytes**, GFXMMU framebuffer **365,040 bytes** in SRAM3,
render workspace **4,680 bytes**. The linker reports SRAM1 as 100% because the reserved
8 KiB main stack is deliberately placed at its top; the explicit data/stack overlap assertion
is the meaningful capacity guard. That display is not a heap-usage measurement.

## Physical evidence and open check

The previous two-page firmware was confirmed by the owner: display fills the circle, touch
and joystick navigate. The expanded three-page firmware was backed up/programmed/verified
through ST-LINK `0676FF504955857567024748`, device `0x470`. Its runtime is ready with no
application assertion, heartbeat=4555 and frames=1 in the first retained status sample.

**Open hardware issue:** after idle, the BSP error counter grew. A bounded debugger attach
caught `HAL_I2C_ERROR_ARLO` (`0x2`, arbitration lost) in the FT3X67 touch-status read. The
BSP performs bus recovery; the application cancels invalid touch samples instead of treating
them as presses and continues sampling the joystick independently. This is not yet explained
or accepted as normal hardware behavior. On 2026-10-17 the owner confirmed that both boards
navigate correctly in response to the SETTINGS -> STORAGE -> BACK -> BACK check, including
the requested L4R9 joystick directions and center check. This functional confirmation does
not resolve the idle touch-bus arbitration errors.
No speculative timing or power changes were made to hide the error.

Programmed ELF SHA-256: `bdd4293e7aa2ba73c96c6e25513c75e6b9b802adfedee01c76ca65adbbf7f334`.
Local retained evidence: `build/menu-validation/20261017-113756-nested` at the library checkout
root, including flash backups, logs, status reads and `input-diagnostic.log`. Host/target and
blank/panel/progression logs are adjacent. These local artifacts are ignored.

The F5 launcher's ST-LINK/GDB backend was exercised using its exact portable tool paths:
download/verify, reset, breakpoint at `main`, then detach. CubeProgrammer is linked by its
installation root with a `/bin` suffix, preserving relative device-database access. The VS Code
GUI's F5 action itself was not manually exercised.

The tutorial helpers reject existing pages/handlers without modifying them; a deliberately
mismatched Storage display contract is rejected by CMake. After the completed exercise passed,
its forms were moved to local ignored `build/completed-tutorial-validation/Tasks/`. The source
starter is blank again; an ordinary same-preset rebuild and Windows CTest (1/1) passed, as did
the final blank STM32 build. The board still runs the retained completed exercise firmware;
its ELF is in the evidence directory, not the now-blank build output.
Original button/counter starter work is unchanged against the retained preservation inventory.
