# RGB565 rendering regression fixture

This is the fixed RGB565 product configuration of the existing 480 by 272 widget showcase.
[configuration.hpp](configuration.hpp) is the handwritten C++ display definition used by
[win32_main.cpp](win32_main.cpp). The complete menu, settings,
widget gallery, editable text, icons, and animations come from the shared portable `Demo`.

The configuration selects compact 5×7 fonts and alpha1 animation resources. Compile-time assertions
reject a package whose destination writer or resource choices do not match. To define another
product, change its C++ configuration and build a matching library package in a separate directory.
There is no runtime format selector.

| Property | Selected value |
| --- | --- |
| Logical/controller extent | 480 × 272, rectangular |
| Native row stride | 960 bytes |
| Native full framebuffer | 261,120 bytes |
| Stored destination alpha | None |
| Surface profile | `litheview.surface.stm32h750b-dk-rk043fn48h-rgb565` |
| Logical input | Pointer/touch, keys, and text input |

The display definition and host executable require no MCU profile, SDK, or connected board. Target
flash, workspace, memory placement, and transfer limits are a separate validation stage.

## Build the host product

Run from the repository root:

```powershell
cmake --preset windows-mingw-rgb565-product-release
cmake --build --preset windows-mingw-rgb565-product-release --target litheview_win32 --parallel 2
.\build\windows-mingw-rgb565-product-release\tests\rendering\win32\litheview_win32_rgb565.exe
```

The product preset builds only the RGB565 destination writer. The older host regression presets
intentionally build all writers for conformance tests; those test binaries are separate evidence.

The same entry point is also a standalone installed-package project:

```powershell
$packagePrefix = Join-Path (Get-Location) "build/install-rgb565"
cmake --install build/windows-mingw-rgb565-product-release --prefix $packagePrefix
cmake -S tests/rendering/rgb565 -B build/rgb565-example -G "MinGW Makefiles" -DCMAKE_BUILD_TYPE=Release "-DCMAKE_PREFIX_PATH=$packagePrefix"
cmake --build build/rgb565-example --parallel 2
.\build\rgb565-example\litheview_rgb565.exe
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
cmake -S tests/demo_package_consumer -B build/rgb565-showcase-capture -G "MinGW Makefiles" "-DCMAKE_PREFIX_PATH=$packagePrefix" -DEXPECTED_DESTINATION_FORMAT=RGB565
cmake --build build/rgb565-showcase-capture --parallel 2
ctest --test-dir build/rgb565-showcase-capture --output-on-failure
```

Its output is `build/rgb565-showcase-capture/initial-frame.bin`; it reports 261120 native bytes and
one presentation. This is a host test fixture. It does not measure display transfer timing.


The old H750 runner has been retired. This fixture preserves the native frame checks; it is not
an application template. New display and feature lessons belong in the application starters.
