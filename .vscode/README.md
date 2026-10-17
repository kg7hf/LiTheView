# Library maintenance workspace

For application development, open the selected starter folder in VS Code.
This repository-root workspace provides library, designer and regression maintenance tools. The host launchers use CMake, MinGW GCC,
`mingw32-make`, and `gdb.exe` from `PATH`. Select a launcher in **Run and Debug** and
press F5. Managed tasks use `dotnet` from PATH, and managed launchers name their built DLL; the SDK must
satisfy `tools/designer/global.json`. See [build prerequisites and local overrides](../docs/BUILD_ENVIRONMENT.md). Its `preLaunchTask` configures the matching product and builds its actual
executable before GDB starts.

For extension development, **LitheView: VS Code Designer Extension** prepares the
shared service/native worker and opens an extension development host on `examples`.
It works on Windows (MinGW worker) and Linux/macOS (`host-debug`, Ninja).
**LitheView: package VS Code Designer** creates a Windows VSIX; **LitheView: test
VS Code Designer** exercises editing, native preview and generation in an isolated
application workspace. See [the extension guide](../tools/vscode/README.md).
The designer does not invoke builds or tasks. Open a consuming application at its
root and use its CMake presets and F5 launchers to build and run the whole system.

For a new product, open the [blank H750 starter](../examples/stm32h750b-dk-starter-1/README.md)
and use its application presets and launchers. The [hardware integration reference](../docs/NEW_HARDWARE_TUTORIAL.md)
explains the portable contracts. The following root launchers maintain handwritten integration tests:
**LitheView: Display Bring-up Simulator (MinGW Debug)** opens its interactive 128x96 RGB565 product;
mouse input emulates touch and wheel/up/down plus Enter emulate a knob with a push switch.
**LitheView: Display Bring-up Checks (MinGW Debug)** runs deterministic input/rendering/failure checks
and writes `initial.ppm` and `activated.ppm` under `build/display-bringup-debug/host/`.
Both launchers use **LitheView: prepare Display Bring-up Debug**, which builds and installs a separate
fixed Core, then builds the standalone CMake consumer and runs CTest. It requires PowerShell 7 as well
as the host tools above. Neither launcher accesses hardware. Their CMake project lives in
`tests/rendering/display-bringup/`; they do not use or change the showcase presets below.

Each product uses a separate Debug build directory and a fixed destination writer.
The standard and round scenes share a format's core archive; they have separate
executable targets. No launcher changes the renderer format at runtime.

| Launcher | Configure preset | Build preset | Executable under `tests/rendering/win32/` |
| --- | --- | --- | --- |
| Regression: Win32 RGB565 | `windows-mingw-rgb565-product-debug` | same | `litheview_win32_rgb565.exe` |
| Regression: Win32 XRGB8888 | `windows-mingw-xrgb8888-product-debug` | same | `litheview_win32.exe` |
| Round RGB565 | `windows-mingw-rgb565-product-debug` | `windows-mingw-round-rgb565-debug` | `litheview_win32_round_rgb565.exe` |
| Round XRGB8888 | `windows-mingw-xrgb8888-product-debug` | `windows-mingw-round-xrgb8888-debug` | `litheview_win32_round.exe` |

The build directory is `build/<configure-preset>/`. These Debug presets retain
source symbols and disable the universal tests, preview worker, and U545 probes.
Their Release counterparts remain available for product size and performance work.
For example, the same RGB565 debug build can be driven from a terminal:

```powershell
cmake --preset windows-mingw-rgb565-product-debug
cmake --build --preset windows-mingw-rgb565-product-debug
cmake --build --preset windows-mingw-round-rgb565-debug
```

Use **Tasks: Run Task → LitheView: test Display Model Debug** for the native CTest
suite, including storage, native rows, surface layout/input, rendering, and the
cast audit. It configures and builds the separate `windows-mingw-debug` regression
preset first. That preset enables all destination writers. The designer's native
preview tasks also use this universal build, because the worker must handle more
than one destination format. Product launchers do not reconfigure the preview's
core archive.

Use **LitheView: test Target Feasibility Debug** for the separate headless target
report checks. It builds the managed Debug CLI, exercises constrained/sufficient
budgets and malformed evidence, and verifies that target checking leaves the
surface export unchanged. It does not connect to hardware.

Each starter has its own Windows and STM32 launchers. Open that application folder in VS Code.
Root Regression launchers inspect library test scenes; Designer tasks prepare and verify the editor.
