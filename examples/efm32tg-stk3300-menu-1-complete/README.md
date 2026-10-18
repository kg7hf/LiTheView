# Tiny Gecko menu 1: completed reference

This is the finished result of the [guided starter](../efm32tg-stk3300-menu-1/README.md).
It includes the authored segmented project and generated encoder. Build it directly
to compare PLAY/PAUSE, volume save/cancel and the ring with your own result.
Follow the starter to create the UI through the plug-in from a blank glass display.

This application demonstrates a menu with two physical buttons, PB0 and PB1,
and a touch slider. The Windows window simulates those inputs and the Tiny Gecko
A03 glass. The application owns its menu state. LiTheView supplies bounded segment
storage; there is no raster widget tree, RTOS or MCU dependency in this build.

The Windows simulator and Cortex-M3 firmware use the same FSM, presenter and
A03 character/digit matrix. The hardware adapter adds GPIO debounce, capacitive
sensing and LCD register writes. See VALIDATION.md for the measured build and
board evidence. The volume ring is the first advanced symbol lesson. The next
lesson adds segmented authoring, native preview and Configure in the VS Code
designer. Both targets consume that generated encoder through the presenter.

## Build and open the simulator

Install CMake 3.25 or newer, Ninja, a C++23 MinGW-w64 toolchain (GCC 14.2 tested),
and matching GDB. Put their executables on PATH. In VS Code use CMake Tools and
Microsoft C/C++. No .NET, STM32 SDK or FreeRTOS installation is needed.

Open this application folder in VS Code. Select the `win32-debug` CMake preset,
configure and build. Choose **Tiny Gecko: Windows** in Run and Debug and press F5.
F5 stops at `main`; Continue opens the simulator window. Configuration finds GDB
beside the selected MinGW compiler and creates an ignored `build/tool-links/mingw`
junction to the complete toolchain, preserving its Python support files. The
launcher uses that path automatically; no personal paths are committed.

The same workflow from a terminal in this folder:

The pre-launch tasks use CMake Tools and explicit presets, so F5 uses the same
CMake executable as the IDE build. If CMake is not on PATH, set **CMake: Cmake
Path** in your VS Code user settings. Terminal commands still require CMake
and CTest on the terminal PATH.

```powershell
cmake --preset win32-debug
cmake --build --preset win32-debug
ctest --preset win32-debug
./build/win32-debug/tiny_gecko.exe
```

Compiler selection can be overridden in a local CMake user preset. Use the same
MinGW installation for the compiler and debugger. The MinGW executable links its
runtime statically for local execution, so it does not need copied runtime DLLs.
If distributing binaries, retain the applicable compiler runtime notices and
review the toolchain's license requirements; this example ships source only.

In this contributor checkout, CMake finds the parent LiTheView source. In your own
application, put LiTheView under `Middlewares/Third_Party/LiTheView`, or set
`LITHEVIEW_ROOT` to its actual location. Only `cmake/Segments.cmake` is included;
the raster core is not built. Your application remains the CMake/VS Code root.

## Try the menu

1. The initial label is PLAY. Click **PB0** to play; it changes to PAUSE. Click PB0
   again to pause. The numeric field shows 1 or 0.
2. Drag the slider to the right to reach VOLUME. The slider needs actual movement;
   initial contact anchors it, and holding still does not repeat.
3. Click PB0 to enter the volume editor. Drag left/right to change the draft value.
   PB0 saves and returns. PB1 cancels and returns. Reopen VOLUME to verify the value.
4. Move to SETUP and click PB0. LIGHT toggles the simulated user LED. Move to INFO
   and select it to show the kit identifier. PB1 returns to Setup, then Home.
5. Verify that returning to a menu retains its selection and playback state.

Keyboard shortcuts: arrows move one step; Enter is PB0; Escape is PB1. Repeated
keydown messages are suppressed. A mouse button activates on release inside the
same control. Dragging away or losing focus cancels it. There are no extra physical
buttons hidden behind the keyboard shortcuts.

The drawn slider has four pads. Windows supplies normalized contact position;
the hardware adapter derives it from four capacitance measurements. A movement
of 100/1000 of its length produces one step per sample. Holding a position does
not repeat. This keeps the interaction the same on both platforms.

## Follow the files

| File | Responsibility |
| --- | --- |
| `Application/menu.hpp` | FSM, committed/draft state and typed effects; no display includes |
| `Devices/slider.hpp` | Bounded contact-to-navigation translation |
| `UI/presenter.hpp` | Read-only state to supported label and numeric value |
| `UI/glass.hpp` | A03 character/digit wiring and authored stroke glyphs |
| `Platform/Win32/main.cpp` | Mouse/keyboard adapter, native window and segment drawing |
| `Tests/fsm_checks.cpp` | Display-free navigation, values, effects and slider tests |
| `Tests/segment_checks.cpp` | Independent electrode expectations and presentation checks |

The encoder returns a complete 20-byte frame or failure. IDs use glass COM/S
coordinates. The Win32 renderer reads those bits to light strokes; it never renders
the menu text with a Windows font. Labels around the simulated device use Windows
text, but the LCD text uses the segment matrix. The LED is an application effect,
not an LCD segment. A failed encode retains the last valid frame and shows an error.

The schematic is the original Energy Micro manual, `t0011_1.00`, 2011-05-13,
[page 26](https://docs.rs-online.com/30b1/0900766b814a5093.pdf#page=26).
The wiring was transcribed from that sheet; the limited glyph shapes and simulator
drawing are authored here. Vendor EMLIB lives in the example's SDK submodule;
no vendor font table is used. Glass COM0 connects to MCU COM7, so the register
adapter reverses the common index while preserving segment numbers.
The volume ring uses eight additional bits; other icon segments remain off.

## Validation and next step

`ctest` runs four suites: display-free FSM/input checks, native electrode checks,
hardware adapter contracts, and a hidden-window Win32 walkthrough using the mouse/keyboard
message handler. The sequence tests cover 65,536 eight-event histories. They do not
replace a person checking usability or physical hardware testing.

For an image of the application's own drawing, without capturing the desktop:

```powershell
./build/win32-debug/tiny_gecko.exe --snapshot build/tiny-gecko.bmp
```

## Build the Tiny Gecko firmware

This target is the original **EFM32TG-STK3300, PCB2100 A03, EFM32TG840F32**.
It is not the Giant Gecko STK3700 or a later Tiny Gecko revision. Check the board
marking before programming it. The MCU has 32 KiB flash and 4 KiB RAM.

1. Initialize this application's `Drivers/GeckoSDK` submodule. In the LiTheView
   contributor checkout, run from the repository root:

   ```powershell
   git submodule update --init examples/efm32tg-stk3300-menu-1-complete/Drivers/GeckoSDK
   ```

   A standalone application uses `git submodule update --init Drivers/GeckoSDK`.
   This example pins Silicon Labs Gecko SDK v4.4.6. It does not download SDK files
   during CMake configuration, and the Windows build does not require that SDK.

2. Install Arm GNU Toolchain, CMake and Ninja. Put `arm-none-eabi-gcc` on PATH,
   or set `ARM_GCC_BIN` to the compiler's `bin` directory before opening VS Code.
   A local CMake user preset can also set the `ARM_GCC_BIN` cache variable.

3. In this application folder, select **efm32-debug**, configure and build:

   ```powershell
   cmake --preset efm32-debug
   cmake --build --preset efm32-debug
   ```

   The outputs are `build/efm32-debug/tiny_gecko.elf`, `.bin` and `.map`.
   The link fails above 24 KiB flash or 2 KiB static RAM, reserves a 1 KiB
   stack and rejects heap dependencies. Stack reservation is not a proof of
   worst-case stack use. The `.su` files provide per-function stack measurements.

4. Connect the Tiny Gecko's debug USB. Install SEGGER J-Link tools and their USB
   driver, or use an existing installation. Run J-Link Commander's `ShowEmuList`
   and identify this board explicitly. Do not select another connected board.
   The old onboard debugger can report a different USB selection serial from
   the firmware's printed serial; use the USB list's value for selection.

5. Choose **Tiny Gecko: EFM32 (J-Link)** in VS Code. The launcher asks for the
   Arm GDB path, J-Link GDB server path and probe USB serial. F5 builds, programs
   and stops at `main`; Continue runs it. This uses Microsoft C/C++, not an STM32
   debug profile. No machine-specific tool paths or probe serials are committed.

6. Leave the slider untouched for the first second after reset. PB0 selects;
   PB1 goes back or cancels. Slide to move between PLAY, VOLUME and SETUP. In
   VOLUME, slide to change the draft, PB0 saves, PB1 cancels. SETUP -> LIGHT
   toggles the board's user LED. Playback remains a state demonstration, not audio.

### What changed for hardware?

`Platform/EFM32/main.cpp` owns the same `Menu` used by Windows. The 1 ms SysTick
interrupt only advances a counter. The foreground loop samples buttons, consumes
completed slider scans, dispatches events and sends a changed segment frame.
It sleeps between ticks; there is no RTOS and no UI work in an ISR.

`Devices/button.hpp` debounces for 20 ms and emits on release. A button held
at startup must first be released. `Devices/capacitive_position.hpp` calibrates
32 scans, rejects zero/saturated readings and computes a normalized position.
`Devices/slider.hpp` then supplies the same navigation events as on Windows.
Capacitive thresholds are example settings; moisture, enclosures and supply
changes require calibration work before treating this as a product design.

`Platform/EFM32/board.c` uses EMLIB and the SDK's STK3300 capacitive configuration.
PB0 is PD8, PB1 is PB11, the LED is PD7, and slider pads are PC5/7/12/13.
LESENSE scans asynchronously. A scan timeout disables slider input and increments
a diagnostic counter; the button/display loop continues. EMLIB initialization
still contains vendor synchronization waits; this is not a qualified safety or
low-power implementation. LCD uses octaplex, one-quarter bias, LFRCO and VDD
drive. `lcd_rows.hpp` translates the shared frame into the A03 register mapping.

Watch `tiny_gecko_status` in the debugger for startup, events, frame submissions,
calibration, scan faults and four raw sensor counts. Those counters help diagnose
the board; they do not replace looking at the glass and trying the controls.

## Advanced lesson: fixed symbols

Follow [Show volume on the glass ring](docs/volume-ring.md). It walks through
the electrode mapping, quantization, presenter binding, Windows drawing and
physical checks. The ring shows saved volume on ordinary pages and the draft
while editing; cancel restores it and save retains it. No FSM changes are needed.

The ring lesson explains the physical mapping. The next lesson,
[Describe the glass, then bind the menu values](docs/segmented-designer.md), uses
the plug-in to open, edit, preview, configure and generate a segmented project.
Battery, padlock, antenna, concentric rings,
punctuation and unit indicators remain future exercises.

See the [segmented designer plan](../../docs/SEGMENTED_DESIGNER_PLAN.md) for the
glass definition, individually addressable symbols, typed form bindings
and generation workflow. The menu FSM stays in the application.

The first compiler increment now has source definitions in `UI/design` and a
[generated C++ comparison test](../../tests/segmented_designer_consumer/README.md).
It checks designer-generated segment bits against this working example. The
plug-in project is under `UI/designer`; both platform builds consume
`UI/Generated/segmented/home.hpp`. The original encoder now lives only in
`Tests/reference_encoder.hpp` as an independent regression reference. CMake rejects
stale generated inputs and asks you to Generate UI before rebuilding.

The supplied board photograph is retained at the repository root for a later
Windows front-panel pass. This firmware lesson does not alter the simulator's
existing drawing or any H750/L4R9 starter.


To create this UI in your own application, follow
[Start with a blank application](docs/segmented-designer.md#start-with-a-blank-application).
The VS Code New Project workflow imports the glass, creates the blank form and
path settings, then lets you bind the text, number and ring fields. Your application
continues to own the FSM, presenter, input adapters and build configurations.

## Define your own glass

Read the [annotated glass definition and segment naming reference](UI/design/glass.segment-display.md)
beside the JSON before adapting it to your device. It explains preview geometry,
logical segment IDs, character stroke order, named fields and platform wiring.
