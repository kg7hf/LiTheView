# Starter split validation

2026-10-18 UTC, private checkout. These are build and automated test results;
no hardware was flashed for the split.

| Check | Result |
| --- | --- |
| Blank starter, Windows GCC 14.2 / C++23 | Build passed; CTest 3/3 |
| Completed reference, Windows GCC 14.2 / C++23 | Build passed; CTest 4/4 |
| Blank starter, EFM32 GNU Arm 14.2 | 5,192 bytes flash; 64 bytes static RAM |
| Completed reference, EFM32 GNU Arm 14.2 | 7,740 bytes flash; 64 bytes static RAM |
| Stack reservation | 1,024 bytes in each hardware link; not a worst-case measurement |
| Designer solution | Build passed, zero warnings/errors |
| Core / Service tests after fixture relocation | 101/101 / 953 checks passed |
| Independent generated-component consumer | 2/2 tests passed |
| Cast audit | Passed; separate 10 Win32 / 1 sensor ceilings for each Tiny example |

The blank Windows tests run the actual application: FSM sequences, button/slider
and LCD contracts, and a hidden-window walkthrough of mouse and keyboard events.
They assert that the blank presenter keeps every segment off. The completed
example additionally tests generated segment encoding against independent data.

An isolated installed source-only VS Code extension, version 0.2.16, used a copy
of the real blank starter in an external application directory containing spaces.
The test built the blank application, imported the glass through New Project,
checked cancellation, created the project, bound the label/number/ring, previewed,
saved and generated, added a screen, and applied Configure to both screens.
A normal build then discovered the generated header and passed all four suites,
without an explicit second configure command. FSM source and task configuration
were unchanged by authoring. The normal user extension profile was not modified.

Evidence is retained locally under `build/tiny-gecko-split` in the library checkout,
including the installed-package identities and external-application report in
`integration-evidence.json`. Logs are not shipped as source dependencies.
Both SDK submodules resolve to Gecko SDK v4.4.6 commit
`59d2160fe448b13730a91c0f019a8b4c53c43eb2`.

The completed reference's `tiny_gecko.bin` SHA-256 is
`2088888b94743788ffe41e790beb45abc9b854c1a7afcee33d77dc1a2fd32e62`.
It matches the retained pre-split firmware exactly. Previous human Windows and
hardware acceptance is recorded in the completed reference below; it is not a
new physical test of the blank starter. Existing STM32 starters were not edited.

## Retained pre-split validation

The records below originated under `examples/efm32tg-stk3300-menu`; retained
logs can still contain that original source path.

# Windows simulator validation

2026-10-17, private checkout `D:\source\repos\LitheView`.

- GCC 14.2 MinGW-w64 x64, CMake 3.30, Ninja, C++23; warnings treated as errors,
  exceptions and RTTI disabled. Configure and build passed.
- User acceptance: the owner confirmed "Everything works correctly in the
  simulator" on 2026-10-17, before the hardware adapter was added.
- CTest 4/4 passed after the hardware increment: FSM/input, segment encoding,
  hardware adapter contracts and Win32 input workflow.
- Both portable suites explore 65,536 eight-event sequences. Separate tests cover
  volume saturation at 0 and 100, commit/cancel, parent selection, typed effects,
  slider anchoring/jitter/release and invalid input.
- Independent native oracles cover first-position L plus numeric 1, and the
  irregular fifth-position A plus numeric 0. Compile-time mapping checks cover
  uniqueness and range for all seven character and four digit positions.
- Hidden-window integration sends mouse clicks, slider drags and keyboard messages
  through the real window procedure. It exercises playback, volume cancel/commit,
  Setup/Light/Info/Back, key-repeat suppression, outside release and capture/focus
  loss. This is automated integration, not user acceptance of physical interaction.
- The executable's own `--snapshot` rendering was visually inspected. The initial
  PLAY label, numeric zero, PB0/PB1 and four-pad slider are visible without clipping.
  Decorative icon geometry is intentionally absent in this increment.
- Repository cast audit passed with unchanged Core/preview allowances and a new
  separately reviewed ten-line Win32 boundary. Portable segment/FSM code adds no
  explicit casts and no internal `void*` callback contexts.
- Root package build/install passed. A separate installed consumer linked only
  `LitheView::Segments`, built and ran; its link command contains no Core archive.
- PE import inspection shows Windows system DLLs only, with no libstdc++, libgcc
  or winpthread DLL requirement. Runtime static linking is for local builds; no
  binary or toolchain package is being published by this change.

Reproduce the normal checks from this example folder:

```powershell
cmake --preset win32-debug
cmake --build --preset win32-debug
ctest --preset win32-debug
```

## Tiny Gecko firmware and first board run

Arm GNU 14.2.1 Cortex-M3 soft-float, C++23, `-Os -g3`, no exceptions/RTTI.
Clean hardware build passed. EMLIB/CMSIS are from Gecko SDK v4.4.6,
`59d2160fe448b13730a91c0f019a8b4c53c43eb2`. The existing FSM, presenter,
segment encoder and Windows adapter were not changed for this port.

- Firmware flash image: **6,936 bytes / 32,768**.
- Static RAM: **64 bytes / 4,096**, plus a separately reserved **1,024-byte stack**.
  Application objects are stack-resident; the static RAM figure is not total RAM use.
- Link assertions enforce 24 KiB flash and 2 KiB static RAM budgets, stack separation,
  no dynamic global constructors and no `malloc`/`_malloc_r`/`_sbrk` dependency.
- Startup/idle stack watermark: **384 bytes touched** in the 1,024-byte reservation
  after three seconds, with 640 untouched. This is observed startup/idle usage,
  not a worst-case proof for every event sequence. Compiler reports main 216 bytes,
  encoder 88 bytes and board initialization 120 bytes per function, excluding callees.
- Host adapter tests cover startup-held buttons, debounce/bounce, one release event,
  counter wrap, capacitive calibration and invalid readings, weighted position,
  reversed commons and an independent L/1 register oracle.
- Cast audit passed; sensor normalization has one reviewed bounded narrowing.

The connected board identifies as Tiny Gecko family 0x49, part 840, 32 KiB flash,
4 KiB RAM (`DEVINFO`: `00040020 0B490348`). J-Link USB selection is 123456;
its old onboard firmware prints serial 440001204. SEGGER V9.42 connected over
SWD at 1 MHz, backed up the original 32 KiB flash, then programmed and verified
the application. No STM32 probe was selected.

Basic-menu binary SHA-256 (before the ring lesson):
`8C938083B52AD28FEA00FB43678B88136B1678177F67E2E0D4A5D2E44D265315`.

At 3.032 seconds: ready=1, frames=1, calibrated=1, sliderFaults=0; raw pad
counts 741/841/770/789. The predecessor image also ran for 228.743 seconds with
zero scan faults. The final run resumed before disconnecting the probe.
Arm GDB connected through the J-Link server and read the typed diagnostics.
VS Code launch configuration is prepared; an interactive F5 walkthrough is
not yet recorded. The server and GDB connection were exercised directly.

Private raw evidence and original flash backup:
`build/tiny-gecko-hardware-20261017/` at the repository root. Do not distribute
the original flash dump. Source changes and this report do not publish a binary.

Physical acceptance, 2026-10-17: the owner confirmed **"Display and controls work"**
in response to the PLAY/0, PB0 playback toggle, slider navigation, Volume editing
and PB1 cancel/back checklist. The basic menu is accepted on both Windows and
the Tiny Gecko board. This is user-observed evidence, separate from the counters.

No standalone Silicon Labs or SEGGER installation was needed on this machine:
the SEGGER tools were found in the installed STM32CubeIDE bundle and used with
the eval kit's onboard J-Link debugger. New environments still need compatible
J-Link tools and drivers as described in the README.

Still pending: walking-segment optical validation, symbols, environmental
capacitive calibration, low-power measurement and segmented designer support.

## Advanced volume ring (2026-10-17)

The presenter now binds saved volume on ordinary pages and draft volume in the
editor. The shared encoder sets the eight ring electrodes in clockwise order
from A7 at the top, rounding to the nearest eighth. The FSM, input adapters,
LCD driver and original STM32 starters are unchanged. This is handwritten
segmented UI, not new designer/plug-in support; see `docs/volume-ring.md`.

- Windows build and all four CTest suites passed against the newly linked binary.
  The first link attempt was blocked by the old running simulator; the old test
  executable's pass was not counted as ring validation. After restarting that
  application, the new build and new integration test passed.
- Tests cover every percentage against an independent electrode/threshold oracle,
  preservation of non-ring bits, clearing, invalid input, draft/save/cancel and
  known controller bit masks. The Win32 message test verifies initial half-ring,
  full draft and restored half-ring after cancel.
- Own-render snapshot inspected: `build/tiny-gecko-ring-20261017/half-ring.bmp`.
- Cortex-M3 build: **7,040 bytes flash** (104 bytes more), **64 bytes static RAM**,
  unchanged **1,024-byte stack reservation**. Startup/idle watermark: **400 bytes
  touched**, 624 untouched. This remains an observed bound, not worst-case proof.
- Cast audit passed with no new cast allowances. Four host suites passed.
- Same identified Tiny Gecko programmed/verified via J-Link. At 3.034 seconds:
  ready=1, frames=1, calibrated=1, sliderFaults=0. Probe released with MCU running.
- Owner confirmed **"ring worked on hardware"** against the half-ring,
  adjustment and cancel/save checklist. After reopening the simulator, the owner
  confirmed **"The ring works in Windows too"** on 2026-10-17. The ring is now
  accepted on both platforms. These are user observations, separate from the
  automated integration tests and debugger measurements above.

Ring firmware SHA-256:
`957E90AFC957A24BE971B7ABBA22CAE0A9D33C51C63DEA691ACD16DFADAE0295`.
ELF, binary, map, programming log, stack dump and snapshot are retained privately
under `build/tiny-gecko-ring-20261017/` at the repository root.

Private reference documents are kept outside this example in the private
checkout's `docs/private-hardware/efm32tg-stk3300-a03`. Do not publish that folder.

## Designer compiler comparison (2026-10-17)

The new `UI/design` glass/form sources compile through the designer CLI into an
encoder tested alongside the handwritten encoder. The runtime and application FSM
have not been switched to generated code. See the
[consumer test](../../tests/segmented_designer_consumer/README.md).

- Designer solution: zero warnings/errors. Core: 101/101 tests passed.
- Generated host consumer: ring oracle, all 101 volume values, all 10,000 numeric
  values, supported glyphs, invalid input and 65,536 eight-event FSM histories pass.
- A separate 17-bit glass checks individually controlled pieces, multi-electrode
  Boolean icons, scaled groups and unused bits. Four frame capacities also have
  compile-time boundary checks.
- Generated consumer compiles for Cortex-M3 with -Os and warnings as errors.
  This is an object compilation, not a linked/generated firmware memory result.
- A bounded byte-view change in SegmentFrame avoids GCC's mixed-frame-size
  identical-code-folding diagnostic. Original Tiny Gecko Windows suites: 4/4.
- Rebuilt handwritten firmware remains 7,040 bytes flash, 64 bytes static RAM,
  with the same SHA-256 as the accepted ring image above. No hardware accessed.

Logs, generated headers, host consumer and ARM object are in the private checkout's
`build/segmented-designer/`. At that checkpoint, project loading, native preview
and segmented plug-in editing were pending. The next section records their
implementation. No new physical acceptance is claimed for generated code.


## Segmented project and plug-in integration (2026-10-17)

The project in `UI/designer` now supports field bindings, editor sample values,
Configure/import glass and logical input, Add screen and multi-form C++ generation.
The worker encodes native electrode bits before expanding the fixed geometry for
host display. Tiny Gecko uses 20 segment bytes; the host viewport is not a target
framebuffer. The accepted runtime, application FSM and physical image are unchanged.

Validation in the private checkout:

- Designer solution builds with zero warnings/errors; Core regression: 101/101.
  Existing standalone desktop regression: 18/18.
- Service: 939 checks, including all ring thresholds and non-ring electrodes
  against independent A03 mappings, invalid requests, stale Configure snapshots,
  all-screen generation, overwrite protection and failed-write rollback.
- Plug-in unit tests: 91/91. The source VSIX is version 0.2.15.
- Generated encoder and native parser consumer tests: 2/2. Native preview checks
  sparse mappings, text, pixels, malformed/truncated payloads and unused bits.
- Targeted preview-worker Clang-Tidy reports no unsuppressed project diagnostics;
  Cppcheck warning/portability profile passes. The repository cast audit passes.
- Installed-package extension-host exercise starts with a blank segmented form,
  adds fields, receives native previews, adds a screen, applies Configure to both
  screens, and generates both headers. Application build tasks remain unchanged.
  Evidence: `build/vscode-designer/integration-20261017T235412594/application with spaces/vscode-validation.json`.
  This is an automated extension-host exercise, not a manual mouse walkthrough.

Logs are under `build/segmented-designer/final-*`; native analysis logs use
`preview-clang-tidy.log` and `preview-cppcheck.log`. Service evidence is under
`build/designer-service-tests/e7e7a859a0984fdea687ac5a90dee91e`.

Initial segmented project creation uses the CLI; Configure then edits that project.
The New Project wizard and standalone desktop do not create segmented projects.
The example still builds its handwritten encoder. Switching the runtime to generated
headers and repeating Windows/hardware acceptance is the next integration stage.
No hardware access, flash, commit or push occurred in this increment.


## Generated encoder on Windows and Tiny Gecko (2026-10-18 UTC)

`UI/presenter.hpp` now calls `lithe_generated::screen_home::encode` using named
fields. Both platform mains use that adapter. One form supplies the same physical
fields across menu states; the presenter supplies their changing values. The FSM,
input drivers and LCD transfer behavior are unchanged. The original encoder moved
to `Tests/reference_encoder.hpp`; production does not include it.

- Windows: clean warning-as-error build, 4/4 CTest suites. This includes actual
  Win32 mouse, slider and keyboard message paths, draft/save/cancel, and ring checks.
- Generated presentation equals the handwritten reference over 65,536 eight-event
  FSM histories and all 10,000 numeric values. The standalone consumer retains
  its independent ring/glyph checks and passes 2/2 suites.
- Designer solution: zero warnings/errors; Core 101/101; Service 939 checks.
  Contributor path tests allow outputs only within the declared example and reject
  another example, library sources, protected inputs and an oversized application root.
- CMake checks normalized source hashes before either platform build. Four deliberate
  source changes were rejected; CRLF checkout conversion was accepted. Builds need
  no .NET or running designer when the checked-in generated output is current.
- Cortex-M3: **7,740 bytes flash**, **64 bytes static RAM**, **1,024-byte stack
  reservation**. Flash is 700 bytes above the 7,040-byte handwritten baseline.
  The no-heap, no-dynamic-initializer and link-budget checks passed.
- J-Link USB selection 123456, onboard firmware serial 440001204, target identity
  words `00040020 0B490348`; VTref 3.288 V. No other debug client was active.
- Firmware programmed and verified, then reset and observed after 3 seconds:
  ready=1, frames=1, calibrated=1, sliderFaults=0. All eight LCD register rows match
  the independent handwritten encoder for PLAY, numeric zero and half-ring.
- Flash readback's complete 7,740-byte image matches the built binary. The capture
  contains 30,528 bytes because J-Link's savebin length is hexadecimal; only the
  image-length prefix is used for that comparison.
- Startup/idle stack watermark: **400 bytes touched**, **624 bytes untouched**.
  This is an observed startup bound, not worst-case stack proof.
- The final header self-containment fix rebuilds to the exact programmed binary.
  The simulator's own render was inspected. Hardware was resumed and the probe closed.

Firmware SHA-256:
`2088888B94743788FFE41E790BEB45ABC9B854C1A7AFCEE33D77DC1A2FD32E62`.

Evidence: repository-root `build/tiny-gecko-generated-20261018/` contains the
ELF/bin/map, programming and readback logs, stack dump, hardware report, guard
report, test logs and Windows snapshot. The earlier failed host contract test
is retained: it expected an empty numeric area from the old ring-only helper;
the generated complete frame correctly also displays zero. The corrected test
checks the full independently specified row pattern. The sandbox-blocked first
Arm build log is retained alongside the successful external build. Identification
also logged an unnecessary resume on an already-running CPU; programming and
verification then completed normally using the bounded flash script.

Owner confirmed **"Everything works on the hardware"** for this generated image
against the PLAY/zero/half-ring, PB0 play/pause, slider navigation, VOLUME adjustment,
PB1 cancel and PB0 save checklist. This is fresh physical acceptance of the
generated encoder, separate from the automated and debugger evidence above.

The owner subsequently confirmed **"The Windows simulator works too"** for the
generated presenter. This records owner acceptance of both platforms, separately
from the automated checks. Existing STM32 starter work remains untouched.


## Segmented New Project acceptance (2026-10-18 UTC)

Version 0.2.16 adds segmented creation to the installed VS Code plug-in. The
exercise starts in a new temporary application outside the LiTheView checkout,
with spaces in its path and no designer manifest, snapshots or generated UI.
The fixture retains application-owned input adapters, presenter and FSM, and
builds a blank Windows executable before the UI exists.

The installed source-only VSIX, using the newly published service and existing
validated native worker, passed import, cancel without writes, New Project,
blank-screen preview, typed bindings, save, Generate UI, Add screen and Configure.
The same win32-debug preset then built the complete simulator. Four suites passed,
including its real Win32 mouse/slider/keyboard message workflow. The FSM source and
application task file remained byte-for-byte unchanged. No CLI creation was used.

Additional checks: designer solution zero warnings/errors, Core 101/101, Service
953 checks, desktop App 18/18 and extension Node tests 93/93. Creation tests reject
invalid glass, conflicting display selections, invalid identity, missing custom
input and overlapping output paths before publishing files. Existing projects
are never replaced. An unrelated synthetic glass uses the same creation path.
The webview regression checks pixel/segment switching, field summary, geometry
preview and custom input submission.

Evidence is retained under repository-root build/segmented-new-project and the
run identified by build/vscode-designer/latest-integration.json. The integration
report records the external application location and exact package/service/worker
hashes. Initial sandbox-denied temporary renames are retained alongside the passing
normal-permission run. Service publication succeeded; the preparation wrapper
subsequently encountered an in-use native runtime DLL. The existing worker was
unchanged and the new service/package passed the isolated integration run.

No new hardware flash or physical acceptance is claimed for this authoring-only
increment. The previously accepted generated hardware firmware is unchanged.
Existing STM32 starters and the normal VS Code user profile were not modified.

## VS Code Windows debugger acceptance

After the CMake Tools pre-launch task fix, the user confirmed that both F5 and
the green Run and Debug triangle stop at `main`; Continue runs the Tiny Gecko
Windows simulator. This is human acceptance in the existing completed-example
workspace, in addition to the isolated launch test recorded in
`build/tiny-gecko-split/f5-integration/result.json` in the library checkout.
No hardware was flashed for this debugger check.
