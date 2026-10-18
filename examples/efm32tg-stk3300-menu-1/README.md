# Tiny Gecko starter 1: describe the glass, then add the menu

Start here with a working application and a blank glass display. We will use the
VS Code designer to define its presentation, then build the same application
again. The completed result is in
[efm32tg-stk3300-menu-1-complete](../efm32tg-stk3300-menu-1-complete/README.md).

This is the original **EFM32TG-STK3300, PCB2100 A03, EFM32TG840F32**. Its display
has fixed electrodes, not pixels. Windows and the board use the same menu FSM,
presenter and 20-byte segment frame. PB0, PB1 and the touch slider feed the
application. There is no RTOS and no UI work in an interrupt handler.

## 1. Open your application and build it

Open this folder as your VS Code workspace. Your application is the project root;
LiTheView is a dependency. In your own project, initialize it in the location you
choose, for example:

```powershell
git submodule add https://github.com/kg7hf/LiTheView.git Middlewares/Third_Party/LiTheView
git submodule update --init Middlewares/Third_Party/LiTheView
```

Do that in your own application, not again in this contributor checkout. Here,
CMake finds the enclosing LiTheView checkout. Another dependency location works
by setting `LITHEVIEW_ROOT` in a local CMake user preset. The designer's library
path must resolve to that same checkout.

Install CMake 3.25 or newer, Ninja, a C++23 MinGW-w64 compiler and its matching GDB.
GCC 14.2 is tested. Put the tools on PATH before starting VS Code. Install CMake
Tools and Microsoft C/C++. Follow the [designer setup](../../tools/vscode/README.md)
for the matching .NET SDK, service, native worker and plug-in (0.2.18 or later).
The designer needs those tools; building generated C++ does not need .NET.

Select `win32-debug`, configure and build. Select **Tiny Gecko: Windows** and press
F5. The debugger stops at `main`; press Continue to open the simulator window.
Configuration finds GDB beside the selected MinGW compiler and creates an ignored
`build/tool-links/mingw` junction to the complete toolchain. The launcher uses it
automatically, including GDB's adjacent Python files. No debugger-path prompt or
global PATH change is needed.

The pre-launch tasks use CMake Tools and explicit presets, so F5 uses the same
CMake executable as the IDE build. If CMake is not on PATH, set **CMake: Cmake
Path** in your VS Code user settings. Terminal commands still require CMake
and CTest on the terminal PATH.

```powershell
cmake --preset win32-debug
cmake --build --preset win32-debug
ctest --preset win32-debug
```

The window shows the glass outline and input controls, with every electrode off.
That is the starting point. The FSM and input adapters already run, but there is
no designer project or generated presentation yet. Close the simulator before
rebuilding it. Keep these presets and launchers throughout the lesson.

## 2. Describe the display and input

Run **LiTheView: Add LiTheView UI** from the Command Palette. Select your LiTheView
checkout if asked. Name the UI **Tiny segmented menu** and set:

| Setting | Value |
| --- | --- |
| UI source folder | `UI/designer` |
| Generated component folder | `UI/Generated/segmented` |
| Display type | Segmented display |
| Glass definition | Import `UI/design/glass.segment-display.json` from this application |
| Input | Custom capabilities: Navigation keys, Rotary encoder, Select button |

Leave pointer and text input off. The application translates slider motion into
relative navigation; the logical capabilities do not configure GPIOs. PB0 selects
and PB1 goes back or cancels. The board adapters already supply those events.

Review the all-segments diagram. The glass provides a seven-character `label`, a
four-digit `value` and an eight-piece `volumeRing`. The geometry describes the
physical glass; it does not decide which segments are on at startup. The JSON
also declares individual pieces and their logical bit IDs. The EFM32 adapter maps
those IDs to the controller's COM/SEG outputs. Windows draws the same bits.

Click **Create UI**. The source folder must be new. A blank `home.litheview.xml`
opens. Unlike a pixel display, we cannot move or resize these electrodes. We bind
fields to values from our application. Visual pattern drawing and electrode
assignment are planned; this lesson imports an existing glass description.

## 3. Bind the three presentation values

Select each field in **Glass fields**, enter its application input name and
preview value, then click **Apply field**:

| Glass field | Application input name | Preview value |
| --- | --- | --- |
| `label` | `label` | `PLAY` |
| `value` | `value` | `0` |
| `volumeRing` | `volumePercent` | `50` |

Add all three bindings before generating. The handwritten presenter expects those
names, and the compiler checks the contract. The preview now shows PLAY, zero and
four ring pieces. Save and click **Generate UI**. This creates
`UI/Generated/segmented/home.hpp`.

The ring is a group of eight independently addressable pieces. Binding the group
provides a bounded percentage indicator; binding individual pieces instead gives
Boolean control. A form cannot bind both owners of the same electrode.

## 4. Build the same application again

Build `win32-debug` again. There is no UI-specific preset to select. CMake detects
the new generated header, reconfigures, and compiles the presentation into the
same `tiny_gecko` executable. F5 uses the same **Tiny Gecko: Windows** launcher.

Try PB0 to toggle PLAY/PAUSE. Move along the slider to VOLUME and press PB0. Adjust
the draft value, then press PB1 to cancel or PB0 to save. The number and ring move
together. Keyboard arrows move, Enter acts as PB0, and Escape acts as PB1.
SETUP contains LIGHT and INFO. Playback demonstrates state; it does not play audio.

Run `ctest --preset win32-debug` again. The blank starter has three suites; after
generation, a fourth checks native segment encoding against an independent oracle.
The existing tests still exercise the FSM, device contracts and real Win32 input
message handler. These are automated checks, not physical board acceptance.

## 5. Follow the application boundary

`Application/menu.hpp` owns navigation, saved values and drafts. It has no display
includes. `UI/presenter.hpp` translates that state into label, number and volume,
then passes them to the generated encoder:

```cpp
return lithe_generated::screen_home::encode({
    .field_label = view.label,
    .field_value = view.value,
    .field_volumePercent = view.volumePercent});
```

This adapter is already connected in the starter. Before a generated header
exists, it returns an all-off frame. Generation enables the code above at build
time. It never changes the FSM. Preview samples are editor values, not runtime
initialization. The application supplies runtime values.

Windows draws the returned segment bits. EFM32 maps them to LCD registers. A
failed encode retains the last valid frame. No renderer owns a menu transition.
One form is enough here: the menu pages reuse the same physical fields with
different values. Use **Add screen** when another page needs different bindings,
then choose its encoder in your presenter. Forms do not create tasks.

## 6. Build and run the hardware version

Initialize this example's SDK from the LiTheView repository root:

```powershell
git submodule update --init examples/efm32tg-stk3300-menu-1/Drivers/GeckoSDK
```

A standalone application uses `git submodule update --init Drivers/GeckoSDK`.
The dependency is pinned to Gecko SDK v4.4.6. Windows does not require it.
Install Arm GNU Toolchain and SEGGER J-Link tools and driver. Put the Arm compiler
on PATH or set `ARM_GCC_BIN` to its bin directory in your environment or local
CMake user preset. Select `efm32-debug`, configure and build:

```powershell
cmake --preset efm32-debug
cmake --build --preset efm32-debug
```

Connect the A03 kit's debug USB. Identify its USB serial with J-Link Commander's
`ShowEmuList`; do not select another attached board. Choose
**Tiny Gecko: EFM32 (J-Link)**. Supply Arm GDB, J-Link GDB server and the probe
serial. F5 loads the firmware and stops at `main`; Continue runs it. Leave the
slider untouched during startup calibration, then repeat the Windows checks.

The same hardware preset also builds before the lesson, when the glass stays
blank. Generated inputs are checked for staleness on each build; if you change
an authored form or configuration, save and Generate UI before compiling again.

## 7. Explore the symbols

Follow [the volume-ring mapping lesson](docs/volume-ring.md) to trace its eight
pieces from glass coordinates to LCD registers. Use **Configure** to revisit the
glass and logical input definition. The [designer reference](docs/segmented-designer.md)
explains bindings, multiple forms and the generated contract. Compare your result
with the [completed example](../efm32tg-stk3300-menu-1-complete/README.md).

See [validation](VALIDATION.md) for this split's build evidence and
[third-party notices](THIRD_PARTY_NOTICES.md) for the example dependencies.

## Define your own glass

Read the [annotated glass definition and segment naming reference](UI/design/glass.segment-display.md)
beside the JSON before adapting it to your device. It explains preview geometry,
logical segment IDs, character stroke order, named fields and platform wiring.

## Windows board picture

The simulator uses `board-front.png`, copied beside the executable by CMake.
It loads that copy relative to the executable, so launching from another working
directory works too. The live segment display covers the photographed LCD.
Click the photographed **PB0** and **PB1**, or drag across the printed slider.
**SETUP -> LIGHT** switches the simulated orange user LED below and to the left of PB0.
The arrow keys, Enter and Esc still work as before.

The 465 by 288 image is shown at twice its size. `Platform/Win32/main.cpp` keeps
all placement rectangles in window coordinates: `kDisplay`, `kPb0`, `kPb1`,
`kSlider` and `kLed`. Update those bounds when using a different board picture.
The image decoder and display scaling belong only to the Windows example;
the generated segment bits, application FSM and embedded LCD driver are unchanged.

## Tool discovery

CMake checks explicit tool settings, then a valid cached selection or PATH. On
Windows it also checks installed WinLibs (WinGet), MSYS2, MCUXpresso and STM32Cube
tool locations. The fallback candidates within each location are sorted by
version-like path order, newest first. Configuration prints the selected paths.
No tools are downloaded, and no machine-specific paths are committed.

For a custom installation or to select a particular version, put these cache
variables in an ignored `CMakeUserPresets.json`, or set the same environment
variables before starting VS Code:

| Setting | Value |
| --- | --- |
| `TINY_MINGW_BIN` | Directory containing the Windows `g++.exe` and matching GDB |
| `ARM_GCC_BIN` | Directory containing `arm-none-eabi-gcc` and its companion tools |
| `TINY_NINJA` | Full path to Ninja |

A missing explicit override is an error; it does not silently select another
installation. Standard `CMAKE_CXX_COMPILER`, `CMAKE_C_COMPILER` and
`CMAKE_MAKE_PROGRAM` cache settings are also supported. Configure with `--fresh`
when deliberately switching toolchains. Git must be available for the hardware
build's SDK pin check. Command-line use still needs a way to locate CMake itself;
VS Code uses the executable selected by CMake Tools.

If configuration reports that `g++`, `arm-none-eabi-gcc` or Ninja cannot be found,
check the selected paths and overrides above. A previous successful build may
have used cached paths; a fresh configure must be able to discover the tools too.
