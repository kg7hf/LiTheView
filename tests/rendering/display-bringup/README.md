# Handwritten display integration regression fixture

This internal fixture is a handwritten consumer of an installed `LitheView::Core` package. It fixes
the application at RGB565, 128 by 96 pixels, before choosing an MCU or display controller. Its host
runner renders a labeled orientation pattern, primary colors, eight gray steps, status, and one button.
The interactive Windows simulator exercises the same product with mouse/touch and knob/push equivalents.
The automatic check writes `initial.ppm` and `activated.ppm`. Neither contains a hardware driver.

## Build and run on the host

The repository's [bring-up runner](../../../tools/run-display-bringup.ps1) builds and installs a matching
fixed RGB565 Core, then builds and tests this independent consumer. From the repository root:

```powershell
.\tools\run-display-bringup.ps1 -Configuration Debug
```

Its output is under `build/display-bringup-debug/host/`; use `-Configuration Release` for the separate
Release tree. Use the [application starter](../../../examples/stm32h750b-dk-starter-1/README.md) for
the developer workflow. This fixture preserves the lower-level display/input checks used by the
[hardware integration reference](../../../docs/NEW_HARDWARE_TUTORIAL.md).

Start the interactive simulator after the build:

```powershell
.\build\display-bringup-debug\host\litheview_display_bringup_simulator.exe
```

Click ACTIVATE/RESET, or press Enter/Space. Press on the button and release outside it to cancel.
Wheel/up/down map to knob navigation; the initial scene has one focusable control, so add a second
when exploring focus movement. The window scales native RGB565 pixels and maps mouse coordinates back
to the product's logical surface. In VS Code select **LitheView: Display Bring-up Simulator (MinGW Debug)**.
Other hosts can run the automatic check and provide their own window adapter.

Use a C++23 compiler and a separately installed **RGB565-only** Core package built with the same
toolchain. The example rejects an incompatible or universal package at compile time. From the
repository root, the command below uses the package prepared by `tools/run-display-bringup.ps1`;
replace that prefix with your own installation directory if building it separately:

```powershell
cmake -S tests/rendering/display-bringup -B build/display-bringup-manual-debug -G "MinGW Makefiles" `
  -DCMAKE_BUILD_TYPE=Debug -DCMAKE_EXPORT_COMPILE_COMMANDS=ON `
  "-DCMAKE_PREFIX_PATH=$((Resolve-Path build/display-bringup-debug/installed).Path)"
cmake --build build/display-bringup-manual-debug --parallel 2
ctest --test-dir build/display-bringup-manual-debug --output-on-failure
```

Use a separate directory with `-DCMAKE_BUILD_TYPE=Release` to check an optimized consumer. With a
multi-configuration generator, pass `--config Debug` to the build and `-C Debug` to CTest. CTest runs
the executable in its build directory, where the two PPM files are written. A successful run reports:

```text
PASS: RGB565 128x96; full-frame=24576 bytes; tile-workspace=1024 bytes; 24 tiles/frame
PASS: initial/select/pointer/cancellation bytes match; rejected sink and retry checked
Wrote initial.ppm and activated.ppm in the working directory
```

The host runner compares independent full-frame and tiled `Product` instances initially, after a
logical select, and after each pointer press/release, outside release, and lost-contact cancellation.
Literal red/green/blue/white byte checks establish the RGB565 packing independently.
An injected sink rejection must report `sinkWriteFailed` and successfully abort; a new complete
submission must then match. These checks establish portable rendering/input behavior, not panel timing,
electrical compatibility, DMA correctness, or physical color/orientation.

## Read the source files

1. `product.hpp` declares `kDisplay` and the separate `kInput`. Public constexpr validation checks the
   definition, button placement, required activation input, and storage sizes. `Product` owns a bounded
   view/controller/application graph and delivers button actions through typed `ControllerDelegate`
   methods. It owns no framebuffer, device, timer, host file, or dynamic allocation.
2. `main.cpp` supplies static caller-owned storage and two host adapters. `FrameDisplay` borrows a
   24,576-byte framebuffer. `ComparingSink` checks exactly 24 ordered 128 by 4 strips, each borrowing
   1,024 bytes with a 256-byte stride. The sink consumes each span before returning and retains no tile
   pointer. Its borrowed reference frame exists only to verify the host comparison.
3. `win32_main.cpp` owns the interactive window, host RGB565 storage, and decoded presentation buffer.
   It translates mouse/keyboard/wheel events into the same logical input; no window type enters `Product`.
4. `CMakeLists.txt` uses `find_package(LitheView CONFIG REQUIRED)` and `LitheView::Core`. It includes no
   repository-private headers, demo libraries, or MCU SDK. First-party warnings are errors; GNU/Clang
   compilation disables exceptions and RTTI.

## Carry the product to a board

Include `product.hpp` in the board application and keep one `Product` and its selected storage alive.
Check `initialize()` before accepting input. Choose the rendering strategy when defining that board
application: full-frame storage for a controller that scans a framebuffer, or the 1,024-byte strip for
a synchronous command/row transport. The host verification executable contains both strategies to
compare them; a board product needs only its chosen adapter and storage.

Implement the actual `Display` or `TileSink` in a board-owned source file. A tiled driver must validate
the same format/geometry/stride contract, send the selected panel's documented address commands, and
finish consuming the borrowed strip before returning. Starting DMA and returning while it still reads
the reusable strip violates this synchronous interface; add explicit completion ownership before
attempting an asynchronous adapter. The driver must translate the core's little-endian RGB565 bytes to
the transport's documented byte order when necessary.

Map physical touch into the product's logical coordinates, phases, contact ID, and monotonic timestamp
before calling `handlePointer()`. Map buttons or an encoder/select input into logical `Key` events.
Keep bus operations, calibration, orientation mapping, reset/backlight, interrupts, and MCU types out
of `product.hpp`. Retain the visible `TL`, `TR`, `BL`, and `BR` markers during the first real display run.

The two storage numbers cover pixel storage only. The `Product`, font, driver state, stacks, command
buffers, transfer rate, flash, and any DMA/cache requirements require their own target measurements.
This fixed example is a baseline: a display with an unsupported native format needs a checked format
implementation and independent packing tests before it can replace RGB565 here.
