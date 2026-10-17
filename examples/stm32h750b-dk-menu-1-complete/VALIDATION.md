# Validation: completed H750 menu

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

Loaded ELF SHA-256: `c753ff8093dab20fbef6cfadd0df779c12746f9116837a8aca57e63b9b7cdc99`. ST-LINK: `003000483137510C33333639`.
Evidence: library-checkout `build/menu-validation/load-20261017/`, especially
`scroll-hardware-run/` for this final firmware. Earlier `hardware-run/` retains
the owner-accepted three-entry build. Earlier radio prototypes are superseded.
Accepted firmware FLASH: **79,844 bytes**. A fresh rebuild after renaming the example
to `stm32h750b-dk-menu-1-complete` passes all 7 Windows checks and the STM32 build
(FLASH **79,912 bytes**, including longer source-path diagnostic strings). The board
continues running the accepted ELF recorded above; the rename rebuild was not flashed. The existing H750 starter photo is reused unchanged.
Completed simulator: `build/menu-validation/load-20261017/scroll-completed-h750/starter.exe`
(at the library checkout root; keep its adjacent board-front.png).

## Load list owner validation (2026-10-17)

The owner confirmed both boards work correctly with the three-entry Load list:
touch selects a row, LOAD updates the loaded name, Back/reopen retains selection,
and L4R9 joystick Up/Down and center select/activate correctly. This acceptance
applies to the retained `load-20261017/hardware-run` firmware, before the eight-entry
scrolling extension. The owner also confirmed the completed L4R9 Windows simulator
works, including its board-photo presentation.

## Owner Windows walkthrough (2026-10-17)

The owner confirmed that everything works correctly in the H750 Win32 simulator after
the mouse/keyboard checklist: Volume adjustment and focus/activation, value retention,
nested menus and keyboard Back, Play/Pause/Stop symbols and status, dragged-click
cancellation, and playback state retained across Settings/Back. This is owner-performed
desktop acceptance, separate from automated Windows CTest (5/5) and board acceptance.

Walkthrough executable SHA-256:
`665d01afade1281fec6b72423f2ccdba40d92a85fc8bbcf52e2117da24c26f01`.
Build/test log: `build/menu-validation/h750-hands-on-build.log` at the library checkout.


## Custom playback control validation (2026-10-17)

- Windows CTest **5/5** passes: navigation, Volume, playback, the Windows input/RTOS/render
  workflow and task health. Playback checks cover named custom types, generated handlers,
  play/pause/resume/stop, native symbol pixels, disabled/cancelled/dragged input, keyboard
  focus/activation and state retained across navigation.
- Designer service inspect/render/Generate UI succeeds; re-export preserves the handwritten
  Home handler. Application subclasses run in the simulator, not in designer preview.
- STM32 build passes; FLASH **73,120 / 131,072 bytes; DTCM 17,192 bytes**. Framebuffer/workspace sizes are unchanged.
  No library or vendor dependency pin changed.
- Backed up, programmed and verified the playback firmware. Startup status: ready=1, assertion=0, heartbeat=1783, frames=1.
- On 2026-10-17 the owner confirmed both boards work correctly: Play/Pause changes the
  symbol, caption and status; separate Stop returns to STOPPED; Settings/Back retains state;
  and L4R9 joystick-center activates the focused control. This confirms physical playback
  interaction for the loaded firmware below.
- Existing L4 idle I2C arbitration and transparent-sibling redraw issues remain open.
  The changing playback status label uses an opaque background.
- All **331** inventoried original starter files in private/public checkouts are unchanged.

Loaded ELF SHA-256: `cabae51b3cdc2deb0cffd293202413c13a0961f732375b9d026036362ef7001b`.
ST-LINK: `003000483137510C33333639`. Retained evidence:
library-checkout `build/menu-validation/playback-20261017/`, including host/target logs,
native runtime PPM captures, hardware backups, exact ELFs and startup reads.
The three playback PNGs in docs are actual native runtime renders, not hardware photographs.


## Volume lesson validation (2026-10-17)

- Designer inspect/render/Generate UI: Volume passes; re-export preserves the handwritten handler.
- Windows CTest **4/4** passes: navigation, Volume contract, mouse/key to RTOS/render workflow,
  and shared task health. The Volume checks cover +/- generated callbacks, joystick keys,
  focus/center activation, cancellation, 0-100% saturation, retained values, label strings,
  native progress pixels and unchanged repaint state at saturation.
- STM32 build passes; FLASH **69,288 / 131,072 bytes**, DTCM **16,952 bytes**. Framebuffer
  allocation remains **522,240 bytes** in SDRAM. No library or vendor submodule pin changed.
- Backed up, programmed and verified this firmware on the H750 probe recorded below.
  Startup status: ready=1, assertion line=0, heartbeat=1835, frames=1.
- On 2026-10-17 the owner confirmed the Volume build works correctly on both boards:
  +/- updates the percentage and bar together, and Back/reopen retains the value.
  This is physical interaction confirmation for the loaded Volume firmware below.

Loaded ELF SHA-256: `0f75e4c729735dabc4cfda4a0042f63413eaf85c1240ca83d5f42dc13d25574c`.
ST-LINK: `003000483137510C33333639`. Evidence: library-checkout `build/menu-validation/volume-20261017/`,
including `hardware-run/`, host/target logs and backups (local, ignored).

The dynamic percentage label has an explicit opaque panel-colored background. A separate
core issue was discovered with transparent sibling dirty regions: updating a transparent
label can clip away an independently dirty adjacent progress bar. Reproduction details
are retained in `transparent-sibling-repro.md` in that evidence directory; the core issue
is not fixed by this example. Current Volume pixel checks pass with independent redraw regions.

Existing starter-1 work: all 331 inventoried private/public files unchanged.

## Previous three-page validation

2026-10-17; public development checkout. No existing button/counter starter was changed.

- Designer service: inspect, native preview and Generate UI succeed for Home, Settings and Storage; handwritten handlers are unchanged by export.
- Windows i686 build: CTest **3/3** passes, including 100 nested navigation cycles and the mouse/key -> FreeRTOS task -> rendered-frame path.
- STM32H750 build: succeeds with warnings treated as errors for first-party C++; FLASH **64,876 / 131,072 bytes**, RGB565 buffers **522,240 bytes** in SDRAM.
- Programming: backed up the previous flash, programmed and verified the three-level firmware using ST-LINK `003000483137510C33333639` (device `0x450`).
- Runtime read: ready=1, assertion line=0, heartbeat=6412, frames=1 after startup.
- On 2026-10-17 the owner confirmed that both boards navigate correctly in response to the expanded SETTINGS -> STORAGE -> BACK -> BACK interaction check. This confirms physical three-level navigation on the H750, separately from automated programming and health reads.

Programmed ELF SHA-256: `780195ddcc777ddf76c1c869b55aa887745ee328590e576fccbf6cf84c2a7e3a`.
Retained local logs, previous-flash backups and ELF: `build/menu-validation/20261017-113756-nested` at the library development checkout root (ignored, not distributed).
Host/target logs: `build/menu-validation/stm32h750b-dk-menu-1-complete-final-*.log`.

Run from this application's directory:

```powershell
./tools/build.ps1 -Preset win32-debug -Action Test
./tools/build.ps1 -Preset stm32-debug
```

The ST-LINK/GDB backend used by the F5 launcher was exercised through its portable tool
paths: download, verify, reset, temporary breakpoint at `main`, then detach. The programmer
junction now preserves its installation root so `bin/..` still finds the device database.
The VS Code GUI's F5 action itself has not been manually exercised in this validation run.
The existing starter files match the retained pre-task hash inventory: 174 private-checkout
files and 155 public-checkout files unchanged.
