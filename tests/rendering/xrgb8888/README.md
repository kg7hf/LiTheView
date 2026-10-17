# XRGB8888 rendering regression fixture

This is the fixed XRGB8888 product configuration of the existing 480 by 272 widget showcase.
[configuration.hpp](configuration.hpp) is the handwritten C++ display definition used by
[win32_main.cpp](win32_main.cpp). The complete menu, settings,
widget gallery, editable text, icons, and animations come from the shared portable `Demo`.

The configuration selects generated Sans fonts and indexed4 animation resources. Compile-time assertions
reject a package whose destination writer or resource choices do not match. To define another
product, change its C++ configuration and build a matching library package in a separate directory.
There is no runtime format selector.

| Property | Selected value |
| --- | --- |
| Logical/controller extent | 480 × 272, rectangular |
| Native row stride | 1920 bytes |
| Native full framebuffer | 522,240 bytes |
| Stored destination alpha | None |
| Surface profile | `litheview.surface.stm32h750b-dk-rk043fn48h-xrgb8888` |
| Logical input | Pointer/touch, keys, and text input |

The display definition and host executable require no MCU profile, SDK, or connected board. Target
flash, workspace, memory placement, and transfer limits are a separate validation stage.

## Build the host product

Run from the repository root:

```powershell
cmake --preset windows-mingw-xrgb8888-product-release
cmake --build --preset windows-mingw-xrgb8888-product-release --target litheview_win32 --parallel 2
.\build\windows-mingw-xrgb8888-product-release\tests\rendering\win32\litheview_win32.exe
```

The product preset builds only the XRGB8888 destination writer. The older host regression presets
intentionally build all writers for conformance tests; those test binaries are separate evidence.

The same entry point is also a standalone installed-package project:

```powershell
$packagePrefix = Join-Path (Get-Location) "build/install-xrgb8888"
cmake --install build/windows-mingw-xrgb8888-product-release --prefix $packagePrefix
cmake -S tests/rendering/xrgb8888 -B build/xrgb8888-example -G "MinGW Makefiles" -DCMAKE_BUILD_TYPE=Release "-DCMAKE_PREFIX_PATH=$packagePrefix"
cmake --build build/xrgb8888-example --parallel 2
.\build\xrgb8888-example\litheview_xrgb8888.exe
```

The example compiles its own showcase source and assets. That project uses `find_package(LitheView CONFIG REQUIRED)`, Core and the selected font resource target. It compiles the
shared Win32 display/input adapter against installed public headers and archives. It does not compile
the portable library or use private library source paths.

## Inspect the regression scene

On startup, `WIDGET GALLERY` is focused. Press Enter to open its animation and controls. Up/Down
changes focus; Left/Right changes a picker or softkey; Enter activates; Escape returns. Type into the
focused text field and use Backspace to edit. Mouse press/move/release follows the same normalized
pointer lifecycle as the embedded touch adapter. Window resizing changes presentation scale and the
inverse pointer mapping; logical dimensions remain 480 by 272.

For deterministic unattended output, the installed showcase consumer emits the unchanged initial
native frame and checks its retained golden:

```powershell
cmake -S tests/demo_package_consumer -B build/xrgb8888-showcase-capture -G "MinGW Makefiles" "-DCMAKE_PREFIX_PATH=$packagePrefix" -DEXPECTED_DESTINATION_FORMAT=XRGB8888
cmake --build build/xrgb8888-showcase-capture --parallel 2
ctest --test-dir build/xrgb8888-showcase-capture --output-on-failure
```

Its output is `build/xrgb8888-showcase-capture/initial-frame.bin`; it reports 522240 native bytes and
one presentation. This is a host test fixture. It does not measure display transfer timing.


The old H750 runner has been retired. This fixture preserves the native frame checks; it is not
an application template. New display and feature lessons belong in the application starters.
