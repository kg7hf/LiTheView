# Install tools, build and run LiTheView

Start here on a new development machine. Build the portable library first, then add the designer
and the application platform tools you need. **Core requires no FreeRTOS, STM32 HAL, CMSIS or .NET.**

- [Install Windows tools](#1-install-windows-tools)
- [Check the environment and get the source](#2-check-the-environment-and-get-the-source)
- [Build Core and run a consumer](#3-build-core-and-run-a-consumer)
- [Open a Windows rendering window](#4-open-a-windows-rendering-window)
- [Prepare the VS Code designer](#5-prepare-the-vs-code-designer)
- [Continue with an application or H750 starter](#6-continue-with-an-application-or-h750-starter)
- [Linux and macOS](#linux-and-macos)
- [Troubleshooting](#troubleshooting)

## 1. Install Windows tools

This path targets a Windows x64 development PC. Install tools in your chosen directories; no
particular drive or username is required. You can reuse suitable tools already installed.

| Install | Required for | What to select |
| --- | --- | --- |
| [Git](https://git-scm.com/install/windows) | Cloning the repository and application submodules | Git available in the terminal |
| [CMake](https://cmake.org/download/) | All CMake builds | 3.25 or newer; make `cmake` and its companion `ctest` available on PATH |
| [WinLibs GCC/MinGW-w64](https://winlibs.com/) | The Windows build path below | x86_64, POSIX threads, SEH, UCRT; add its `bin` directory to PATH. It supplies GCC, GDB and GNU Make. Local validation used GCC 14.2.0; other versions must support the C++23 features used here |
| [Ninja](https://ninja-build.org/) | The minimal Core commands below and the starter presets | `ninja` available on PATH; an existing compatible installation is sufficient |
| [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows) | Designer preparation and repository scripts | PowerShell 7.2+ (`pwsh`), alongside the existing Windows PowerShell |
| [Visual Studio Code](https://code.visualstudio.com/download) | Form editing and the IDE workflow | 1.95 or newer for the current extension; install Microsoft **C/C++** and **CMake Tools**. Microsoft **C#** is needed only for managed tooling debug launchers |
| [.NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) | Building the designer service or extension package | SDK **10.0.400**, or a patch allowed by `tools/designer/global.json`; a runtime alone cannot build the tools |

The root Windows presets use `mingw32-make`, while the minimal Core example below uses Ninja.
Keep GCC, its DLLs and GDB from the same selected distribution. An alternative host installation is
[MSYS2 UCRT64](https://code.visualstudio.com/docs/cpp/config-mingw); its toolchain group provides the
compiler, debugger and native make tool. Select one host compiler environment per build directory.

If you already have a matching LiTheView VSIX, running it requires the .NET 10 runtime, not the SDK
or a compiler for its bundled worker. The separate WinForms designer requires the .NET 10 **Desktop**
runtime. The SDK installation supplies development tooling; check the installed runtimes if a desktop
application reports one missing. Neither runtime is a dependency of your generated C++ UI.

## 2. Check the environment and get the source

Open **PowerShell 7** after setting PATH. Restart VS Code too if it was already running.

```powershell
git --version
cmake --version
ctest --version
ninja --version
g++ --version
g++ -dumpmachine
mingw32-make --version
gdb --version
Get-Command cmake, ninja, g++, mingw32-make, gdb
```

For this root Windows workflow, `g++ -dumpmachine` should report `x86_64-w64-mingw32`.
The `Get-Command` output shows which installation wins when several are present.

Clone into a directory you own, without recursively initializing the examples:

```powershell
git clone https://github.com/kg7hf/LiTheView.git
Set-Location LiTheView
```

All subsequent Windows commands run from this checkout root unless stated otherwise. An existing
checkout can start here. The first clone/SDK restore needs network access; Core builds use the local
source and installed compiler and do not fetch STM32 or FreeRTOS repositories.

### Windows paths for recursive dependencies

Prefer a short checkout directory. The starters' nested FreeRTOS dependencies can
exceed Windows' traditional path-length limit. If Git reports `Filename too long`,
enable long paths for the dependency initialization command:

```powershell
git -c core.longpaths=true submodule update --init --recursive
```

This applies to that command and its recursive Git operations without changing
machine-wide configuration. It does not remove path limits in other build tools.

## 3. Build Core and run a consumer

Core is a library, so it does not open a window by itself. These commands build it with all application,
preview and bundled-font dependencies disabled, install it, then run small applications that use it:

```powershell
cmake -S . -B build/getting-started-core -G Ninja -DCMAKE_BUILD_TYPE=Release `
  -DLITHEVIEW_BUILD_RENDERING_FIXTURES=OFF -DLITHEVIEW_BUILD_TESTS=OFF `
  -DLITHEVIEW_BUILD_WIN32=OFF -DLITHEVIEW_BUILD_STM32U545_PROBE=OFF `
  -DLITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF `
  -DLITHEVIEW_BUILD_COMPACT_FONT=OFF -DLITHEVIEW_BUILD_GENERATED_FONTS=OFF
cmake --build build/getting-started-core --parallel 2
cmake --install build/getting-started-core --prefix build/getting-started-installed

$prefix = (Resolve-Path build/getting-started-installed).Path
cmake -S tests/package_consumer -B build/getting-started-consumer -G Ninja `
  -DCMAKE_BUILD_TYPE=Release "-DCMAKE_PREFIX_PATH=$prefix"
cmake --build build/getting-started-consumer --parallel 2
ctest --test-dir build/getting-started-consumer --output-on-failure
```

Expected result: **3/3 tests pass**, covering basic rendering, framebuffer-free rendering and a
caller-owned font. These executables have no GUI or hardware access. You can now link Core into
your application using [the integration guide](APPLICATION_INTEGRATION.md).

## 4. Open a Windows rendering window

For a visible check before creating your own UI, build a maintained internal RGB565 rendering fixture:

```powershell
cmake --preset windows-mingw-rgb565-product-debug
cmake --build --preset windows-mingw-rgb565-product-debug --parallel 2
./build/windows-mingw-rgb565-product-debug/tests/rendering/win32/litheview_win32_rgb565.exe
```

A graphics window opens. Mouse clicks or Up/Down and Enter exercise the controls; close the window
when finished. This is a library rendering fixture. The developer lesson is the H750 starter below.
No board, Arm compiler, RTOS checkout, .NET SDK or designer is needed for this fixture.

## 5. Prepare the VS Code designer

Skip preparation if a matching LiTheView Designer VSIX is already installed. Otherwise, use this
current tooling checkout, not an older LiTheView dependency pinned inside an example.

First verify SDK resolution in the directory containing its pin:

```powershell
Push-Location tools/designer
try {
    Get-Content global.json
    dotnet --version
} finally { Pop-Location }
```

With the current pin, the result must be a compatible **10.0.4xx** SDK; an arbitrary .NET 10 SDK
does not necessarily satisfy it. Install the requested SDK instead of lowering the pin locally.

From the checkout root, using the x64 host compiler on PATH:

```powershell
./tools/prepare-vscode-designer.ps1
./tools/package-vscode-designer.ps1
Get-ChildItem build/vscode-designer/*.vsix
```

In VS Code run **Extensions: Install from VSIX...**, select the package just produced, and reload
the window. The Windows package includes its service and native worker. No npm install or Marketplace
publication is needed. See [the extension guide](../tools/vscode/README.md) for overrides and details.

Open your **application root** in VS Code. Initialize its LiTheView submodule, then run
**LiTheView: Add LiTheView UI**. Define the display and input, edit the form and click **Generate UI**.
Use your application's normal CMake preset, Build command and F5 launcher for the Windows simulator
or embedded target. The designer does not build or launch the application.

## 6. Continue with an application or H750 starter

An existing application supplies its own hardware, RTOS, compiler and debug integration. It can put
LiTheView in any dependency folder; follow [application integration](APPLICATION_INTEGRATION.md).

For the supplied [H750 guided starter](../examples/stm32h750b-dk-starter-1/README.md), additionally install
the STM32 tools through the vendor's [STM32 development tooling](https://www.st.com/en/development-tools/stm32cubeide.html)
and VS Code extension. The application needs Arm GNU C++23 tools, ST-LINK GDB server/programmer and
the probe driver. Its own `tools/init-platform.ps1` initializes the pinned drivers and RTOS. Follow
**Starting-project prerequisites** in that application's README before the UI lesson.

Download [STM32CubeMX from STMicroelectronics](https://www.st.com/en/development-tools/stm32cubemx.html) and install it separately under ST's terms.
LiTheView does not bundle or redistribute CubeMX. It is needed to edit the `.ioc`
hardware configuration or regenerate ST initialization code; it is not needed to
build the checked-in starter sources or to use the LiTheView designer.

**The current starter's Windows application is 32-bit.** Its CMSIS wrapper stores handles in 32-bit
values, so it requires an i686 MinGW GCC/GDB toolchain even on an x64 PC. Its `STARTER_MINGW_BIN`
override selects that compiler. The root designer/Windows VSIX remains x64; keep these compiler
selections separate. This limitation belongs to the application wrapper, not Core.

The starter launchers use local tool junctions created by their configure scripts.
Follow their README to check discovery or select explicit installation overrides. No physical board is needed to build Core, create forms or exercise host rendering.

## Linux and macOS

Install Git, CMake 3.25+, Ninja and a compiler/standard library supporting the C++23 features used here.
Use the appropriate platform downloads for PowerShell and the pinned .NET SDK only if building the
designer. From the checkout root, the portable host checks are:

```sh
cmake --preset host-debug
cmake --build --preset host-debug --parallel 2
ctest --preset host-debug
```

These hosts run the portable checks and designer preview worker. The Win32 application window,
WinForms designer, GDI+ converters and current Windows VSIX packaging require Windows. The VS Code
extension can be run from source on Linux/macOS; see its guide. The commands above were not rerun on
Linux/macOS during the Windows documentation pass.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| A command is not found | Install that tool, add its selected `bin` directory to PATH, then restart the terminal/editor |
| .NET says the requested SDK cannot be found | Run `dotnet --list-sdks` and `Get-Command dotnet`; verify `dotnet --version` inside `tools/designer`. The SDK and runtime are different requirements |
| Compiler identification fails or a runtime DLL is missing | Keep the selected compiler's matching DLLs on PATH; do not mix x86 and x64 MinGW installations |
| CMake reports a different generator/compiler/source directory | Use a fresh build directory for that toolchain or checkout; do not reuse another machine's CMake cache |
| The starter reports missing CMSIS/HAL/FreeRTOS | Run its platform initialization script. These are application dependencies, not missing Core dependencies |
| No LiTheView commands or XML opens as plain text | Install/reload the VSIX; use **Reopen Editor With... Ã¢â€ â€™ LiTheView Designer** for an existing form |
| You need only generated UI in firmware | Build/link the generated C++ and selected Core/resources. .NET and the designer are development-time tools |

## Dependency and path reference

LiTheView can live anywhere in an application's source tree. There is no required drive,
username, sibling checkout, SDK installation directory or shared network folder.
An installed Core package is also relocatable; consumers use `find_package(LitheView CONFIG REQUIRED)`.
See [application integration](APPLICATION_INTEGRATION.md) for the source-submodule alternative.

## Dependencies by layer

| Layer or operation | Required tools and dependencies |
| --- | --- |
| `LitheView::Core` | CMake 3.25+ and a C++23 compiler/standard library; no FreeRTOS, STM32 HAL, CMSIS, designer, .NET or board files |
| Host library tests | CMake, CTest and a matching build tool; checked Windows presets use MinGW GCC, `mingw32-make` and `gdb.exe` from PATH; Linux/macOS presets use Ninja |
| Offline Arm library/probe checks | Arm GNU C++ toolchain on PATH, or a toolchain-root override; the displayless probe does not require STM32 HAL or an RTOS |
| Designer build | .NET SDK selected by `tools/designer/global.json`; PowerShell 7.2+ for preparation/package scripts; CMake and a host compiler for native preview |
| Installed VS Code designer | VS Code and .NET 10 runtime; the local Windows VSIX includes its service, worker and matching MinGW runtime DLLs |
| Extension unit tests | Node.js with its built-in test runner; no npm packages are required by the extension |
| Existing offline font/image/animation converters | Windows GDI+ / System.Drawing and Windows PowerShell (`powershell.exe` on PATH); source artwork/fonts are caller-supplied |
| Static analysis | Cppcheck, Clang-Tidy, CMake, build tool and compiler on PATH |
| H750 application examples | Their own CMSIS, STM32 device/HAL and FreeRTOS submodules, platform compiler and debug tools; see the selected application's README |

The library build does not fetch vendor repositories. Root `.gitmodules` entries belong to the
starter applications; their dependency initialization is explicit and separate from building Core.
Do not recursively initialize examples just to consume the graphics library. Designer restore can
require standard .NET targeting packs; these are tooling dependencies, not embedded dependencies.

## Selecting tools

The root VS Code tasks use `dotnet` from PATH. Managed debug launchers point to the built DLL
under `${workspaceFolder}` using the C# debugger, rather than a fixed dotnet host location.
Install the SDK specified in `tools/designer/global.json` and make its host the selected
`dotnet` before starting VS Code. Check from `tools/designer`:

```powershell
Get-Command dotnet
dotnet --version
```

`--version` must succeed in that directory; a different global SDK elsewhere is not evidence that
this checkout can build. The SDK pin and roll-forward policy are unchanged. If multiple installations
exist, put the intended installation first on your local PATH and restart VS Code so tasks/debuggers
inherit it. A terminal-only PATH change does not change an already running editor's environment.

Preparation and validation scripts that accept `-Dotnet` can use a selected executable directly:

```powershell
./tools/prepare-vscode-designer.ps1 -Dotnet (Join-Path $env:DOTNET_ROOT 'dotnet.exe')
```

This example assumes you have set `DOTNET_ROOT` locally; use `dotnet` instead of `dotnet.exe` on
non-Windows hosts. It is an explicit override, not a required folder. The installed extension retains
its own compatible-runtime discovery and `litheview.dotnetPath` override; that runtime selection
does not configure the separate root maintenance tasks or CMake application builds.

The Arm toolchain resolves `LITHEVIEW_ARM_TOOLCHAIN_ROOT`, then `STM32_TOOLCHAIN_ROOT`, then
`arm-none-eabi-g++` on PATH. A supplied root must contain `bin/arm-none-eabi-*`. Use an ignored
`CMakeUserPresets.json` for machine-specific overrides; `CMakeUserPresets.json.example` shows the shape.
Native builds can likewise set compiler/build-tool cache variables in a local inherited preset.
Windows VSIX packaging discovers runtime DLLs beside the configured MinGW compiler or accepts
`-CompilerBin`. It intentionally packages the same compiler's runtime rather than an unrelated DLL.

## Source, output and asset paths

- Checked-in configuration uses `${workspaceFolder}`, `${sourceDir}`, `${CMAKE_CURRENT_LIST_DIR}`,
  `$PSScriptRoot` or explicit caller parameters. Derived absolute paths in build caches/logs and local
  preparation records are expected; those generated files are ignored.
- Supply paths with spaces as single quoted arguments. Source and binary directories need not sit
  beside one another, and an installed prefix can be moved before configuring a consumer.
- Designer project sidecars store the application's chosen library and output layout. The service
  receives resolved absolute paths over its protocol; it does not prescribe those paths to the user.
- `assets/source/DejaVuSans.ttf` and similar documentation inputs are examples of caller-owned assets.
  The font is not assumed to be installed in the Windows font directory or bundled in the repository.
- Absolute paths in synthetic path-handling tests are test data. They do not name required local files.
  The historical LTE archive is architectural background and is not a dependency.

## Starter tool selection

Both starters resolve application tools through `tools/configure-tools.ps1`. CMake
Tools, terminal builds and F5 share that selection. The launcher paths are relative
to the application; machine-specific paths and directory junctions stay in ignored
`build/`. Install an i686 MinGW-w64 GCC/GDB toolchain for the Windows RTOS port.
A compatible host toolchain does not require a vendor IDE.

The script searches PATH, the conventional MSYS2 installation on the system drive,
and applicable STM32Cube bundles. Explicit `STARTER_*` overrides take precedence
and fail if invalid. See the starter's **Starting-project prerequisites** for the
complete override table and `-RequireDebugger` checks. Change compiler selections
with a fresh CMake configure; do not reuse an old compiler cache.

Only the application examples require STM32 HAL, CMSIS or FreeRTOS. These tool
settings do not introduce vendor dependencies into Core or the designer.
