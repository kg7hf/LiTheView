# SDR example validation

## STM32 cross-build correction (2026-10-23)

- The ARM GCC build exposed a mixed `int`/`std::int32_t` call to `std::max`
  in the marker/range overlay. The overlay now computes its width with an
  explicit 32-bit saturating subtraction.
- `stm32-debug` target `starter` cross-build and link passed with STM32 GNU
  Tools 14.3.1. FLASH: 72,320/131,072 bytes; DTCMRAM: 25,304/131,072 bytes.
- The Win32 SDR tests passed 2/2 and LiTheView host CTest passed 18/18 after
  the fix. The board was not connected, programmed or run in this check.

## Marker/range overlay and calibrated meter (2026-10-23)

- The designer form now stacks `MarkerRangeView` over the plot and uses
  `MeterView` in place of the old percentage progress bar. The screenshot in
  `docs/sdr-preview.png` was regenerated from the Windows simulator test and
  visually inspected: the translucent region, moving orange marker, center
  VFO marker, three meter zones and scale labels are visible.
- The application publishes the moving marker from two fixed arrays and maps
  synthetic amplitude to an illustrative -120..0 scale. This is UI calibration
  for the demo, not a measurement or RF power claim.
- Windows SDR build and tests: 2/2 passed. LiTheView host CTest: 18/18 passed,
  including bounded overlay/meter tests and native designer preview coverage.
  The i686 Windows build also satisfies the per-view and fixed-layout RAM
  ceilings. Designer Service tests: 964/964 passed; VS Code extension tests:
  97/97 passed. Designer Core tests:
  101/102 passed; the one remaining bitmap conversion fixture could not launch
  Windows PowerShell here (`AuthorizationManager check failed`). A local VSIX
  was packaged from the current catalog, service and Windows preview worker at
  `build/marker-meter-designer/litheview-designer-0.2.18.vsix`. The ordinary
  preparation output was in use by another process, so fresh build directories
  were used without interrupting it. No hardware was probed or flashed.

## Reusable axis and scale (2026-10-23)

- The designer form now places a separate `AxisView` between the plot and
  waterfall. Its normalized endpoint ticks align with the plot's endpoints.
  The application owns three frequency labels and updates them for tuning and
  wide/narrow span changes; the same library control accepts arbitrary units.
- Windows SDR CMake/MinGW build and both tests passed. The control test checks
  center and endpoint label changes through button input. The updated
  `docs/sdr-preview.png` was generated from the simulator test and visually
  inspected with the axis labels visible below the trace.
- LiTheView host CTest passed 18/18. Axis tests cover bounded publication,
  invalid input preserving the old scale, and both horizontal and vertical
  endpoint placement. The native preview test renders an Axis with synthetic
  ticks between Plot and Waterfall. The 32-bit Win32 build satisfies the
  existing fixed-layout RAM ceiling and the new per-axis ceiling.
- Designer Core tests passed 101/102 with the current rebuilt preview worker.
  The remaining native bitmap conversion fixture could not launch Windows
  PowerShell here (`AuthorizationManager check failed`); it does not exercise
  the axis. No hardware was probed or flashed for this change.

2026-10-23, Windows checkout `D:\source\repos\LitheView`, local uncommitted
telemetry-view implementation and selectable-list work.

- Windows i686 MinGW-w64 CMake configure and `starter` build: passed.
- Windows `sdr_controls` and one-application-task tests: 2/2 passed. The
  control test checks tuning, hold/resume, bounded 64-row history, a rendered
  waterfall pixel and an exported 480 × 272 RGB image.
- STM32H750 ARM GCC cross-build: passed. Link report: 67,836 bytes of flash,
  24,480 bytes of DTCM and 510 KiB of SDRAM, including board framebuffers.
- LiTheView host tests: 18/18 passed, including telemetry on RGB565 and native
  packed destinations. An additional regression covers sibling invalidation in
  the view tree.
- The SDR XML validated and generated a typed C++ form. The native preview
  worker rendered its synthetic plot and waterfall; a preview regression
  checks that both regions contain data. The Win32 and STM32 applications now
  build from that generated form, and the Win32 controls tests still pass 2/2.
- Designer Core checks: 102/102; designer Service checks: 961; VS Code
  extension unit tests: 97/97. The native worker and local designer service
  were rebuilt against the updated widget catalog, and a local VSIX was
  packaged under `build/vscode-designer`.
- Cast audit: passed with an independently counted eight-line H750 SDR
  platform boundary. The portable plot has one reviewed narrowing conversion
  for a cursor already bounded below 4096 samples.

The new SDR firmware has **not** been programmed or observed on the board.
These checks establish host behavior and cross-build feasibility, not physical
touch, display timing or frame-rate acceptance. `WaterfallView` currently
redraws its full history when a row arrives; measure target render time before
selecting a production sample rate. The example uses generated synthetic data
and does not include RF acquisition or DSP.

## Secondary trace follow-up (2026-10-23)

- `PlotView` now draws an independent borrowed secondary trace. Host tests
  cover its rendering, clearing, sample-count independence and rejection of
  oversized input, plus cursor clearing after compacting its stored index.
- The 32-bit MinGW size probe reports `PlotView` at 120 bytes and
  `StaticLayout<16>` at 1,948 bytes, matching the existing layout ceiling.
- LiTheView host CTest: 18/18 passed. H750 SDR Win32 tests: 2/2 passed.
  The SDR test also checks that the application's decaying peak snapshot
  never falls below the current sample on its first frame.
- `docs/sdr-preview.png` was regenerated from the Win32 control test and
  visually inspected; amber peak and green live traces are both visible.
- The H750 cross-build was attempted but could not run: the installed
  `arm-none-eabi-gcc.exe` fails to start on this Windows host. The STM32Cube,
  MCUXpresso and CubeIDE compiler copies were checked, with the latter two
  reporting `Illegal System DLL Relocation`. The earlier cross-build above
  predates this secondary-trace change. No board was flashed.

## ST-LINK launch checks (2026-10-23)

The first F5 attempt completed its pre-launch build, then the debugger
reported `ENOENT` opening `.settings/ide.store.json`. The SDR example now
includes the same `STM32H750XBH6` / `Cortex-M7` metadata as the H750 starters.
An offline check followed the debugger's upward search from the ELF directory,
found this file, parsed it, and matched both fields to `launch.json`.

The next F5 attempt started ST-LINK GDB server 7.14.0, which reported it was
looking for `STM32_Programmer_CLI.exe` directly inside the `-cp` directory.
This establishes that `serverCubeProgPath` must name the `bin` folder for this
server. The SDR launcher now supplies `build/tool-links/programmer/bin`, and
an offline check confirms the CLI exists at that exact path. The referenced
ELF, GDB and ST-LINK server also exist. That check did not access the board.

The user then reported one successful F5 launch. A later launch stopped in
ST-LINK GDB server 7.14.0 with `Multiple debuggers detected` (exit code 16).
The installed VS Code extension omits the server's `-i` selector when
`serverSerialNumber` is empty; the example already prompts for that value but
used an empty default. The prompt and README now state that the developer must
enter the H750 probe's serial when more than one ST-LINK is present. No unique
probe serial is committed to the public example.

The user retried F5 with the H750 serial `003000483137510C33333639` and
confirmed that download and run-to-main worked. This is user-observed debugger
behavior, not an independently captured flash readback or runtime display test.

## DejaVu Sans typography (2026-10-23)

The SDR form now builds with the optional 14 px `generatedSans()` face. Its
frequency uses 2× scale; the remaining text uses the native face size. The
designer component was regenerated from the edited XML and passed its surface
check. The Win32 `starter` and `sdr_controls_test` rebuilt, and both example
CTest cases passed. `docs/sdr-preview.png` was regenerated from the shared
form's RGB565 test frame and visually checked at 480 × 272.
The SDR CMake target now disables and omits the unused compact-font resource;
the Windows build and both tests passed again after that cleanup.

The H750 firmware was not cross-built for this change. The installed STM32
and MCUXpresso `arm-none-eabi-gcc.exe` copies both fail to start on this host
with `Illegal System DLL Relocation`. No board was flashed or timed. The
designer's native preview still uses its compact font, so the Windows
application and saved screenshot are the visual reference for DejaVu glyphs.
