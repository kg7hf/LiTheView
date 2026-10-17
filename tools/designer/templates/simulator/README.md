# Exported LitheView UI component

This directory is a portable C++23 UI component with optional host executables.
It links `LitheView::Core` and has no managed designer dependency at runtime.
Display compatibility has been checked; MCU feasibility remains **unevaluated**.
Your application owns its devices, tasks, business state, and action hooks.

## Files and ownership

| File | Purpose | Ownership when regenerating |
| --- | --- | --- |
| `display-definition.json`, `logical-input.json` | Exact independent display and input contracts | Generated snapshots |
| `form.litheview.xml` | Canonical form | Generated snapshot |
| `display.hpp` | Fixed display geometry, storage and input configuration | Generated |
| `form.hpp` | Static view descriptions, actions, bindings and resource references | Generated |
| `resources.hpp`, `resources.cmake`, `assets/*.cpp` | Selected static resource provider and verified payloads | Generated |
| `product_config.hpp` | Compile-time aliases and operation requirements | Generated |
| `runtime_types.hpp`, `runtime.hpp` | Bounded view/text storage and a portable `Product<Hooks>` borrowing your typed hook | Generated |
| `application.hpp` | Standalone host behavior (`onReady`, `onAction`, `onBack`); an embedding application may provide its own hook type elsewhere | **Developer-owned; preserved byte for byte** |
| `win32.cpp`, `headless.cpp` | Host presentation/input adapter and rendering checks | Generated |
| `CMakeLists.txt`, `.vscode/*` | Installed-package build and debugger configuration | Generated |
| `simulator-build.json` | Simulator build root, recorded relative to this component directory | Generated |
| `simulation.json` | Generated file hashes and validation-stage status | Generated |

Edit the form in the designer and behavior in `application.hpp`. Export refuses
to overwrite modified generated files. Reconcile those changes against the
designer source, or export into a new empty directory. After an interrupted
publication, an incomplete manifest cannot be built; exporting to a fresh
directory is a safe recovery route. The project's configured component directory
contains these files as one unit; it need not be named `simulation`.

The optional first-light starter uses action 1 for ACTIVATE, action 2 for RESET
and label view 100 for status. Its behavior is visible C++ in `application.hpp`.
The generic runtime gives those names no special meaning.

## Embed in an existing application

The parent project supplies `LitheView::Core` using its existing package or source
integration, then adds this component. Use your actual component path; paths with
spaces are supported:

```cmake
cmake_minimum_required(VERSION 3.25)
project(MyApplication LANGUAGES CXX)
find_package(LitheView CONFIG REQUIRED)

set(LITHEVIEW_BUILD_GENERATED_SIMULATOR OFF)
add_subdirectory("ui components/operator panel" "operator-panel-build")

add_executable(my_application main.cpp)
target_link_libraries(my_application PRIVATE LitheView::GeneratedComponent)
```

If the parent uses a source checkout or Git submodule, replace `find_package`
with its existing source integration. For a new integration, the parent can
choose a source path and matching fixed destination before adding Core:

```cmake
set(LITHEVIEW_SOURCE_DIRECTORY "" CACHE PATH "LitheView checkout or submodule")
set(LITHEVIEW_DESTINATION_FORMAT RGB565 CACHE STRING "Match the component display")
set(LITHEVIEW_BUILD_RENDERING_FIXTURES OFF CACHE BOOL "")
set(LITHEVIEW_BUILD_TESTS OFF CACHE BOOL "")
set(LITHEVIEW_BUILD_WIN32 OFF CACHE BOOL "")
set(LITHEVIEW_BUILD_STM32U545_PROBE OFF CACHE BOOL "")
set(LITHEVIEW_BUILD_DESIGNER_PREVIEW OFF CACHE BOOL "")
add_subdirectory("${LITHEVIEW_SOURCE_DIRECTORY}" "litheview-build")
```

The source path is an application choice, passed through CMake; the generated UI
does not assume a repository, SDK, board, or device location.

`LitheView::GeneratedComponent` is an interface target. It supplies the component
include directory, C++23 requirement, Core dependency, and selected resource
sources. Your application compiles those resource sources with its own toolchain
and flags. The component reuses an existing `LitheView::Core` target; it does not
configure another copy or replace the parent's project or toolchain. The current
target and generated namespace represent one UI component per parent build.

`LITHEVIEW_BUILD_GENERATED_SIMULATOR` defaults to **OFF** under `add_subdirectory`
and **ON** when this directory is configured directly. Enabling it adds the
existing `litheview_generated_smoke` and, on Windows,
`litheview_generated_simulator` executables. Their strict warning, no-exception,
and no-RTTI flags are private to those executables. An embedding application owns
its build flags and scheduler.

Include `runtime.hpp` from your application. It does not include the standalone
`application.hpp` or create an action handler. Supply a concrete type with these
three `noexcept` methods; the `ProductHooks` concept checks their signatures.
`lithe::simulator::Views` is the form's `Form::Layout`, which builds only the widget kinds the
form uses, so `views.find<T>()` for another widget type returns `nullptr`:

```cpp
#include "runtime.hpp"

#include <cstdint>

namespace product
{
struct ApplicationState
{
    bool active{false};
};

class UiActions
{
public:
    explicit UiActions(ApplicationState& state) noexcept : mState(state)
    {
    }

    void onReady(lithe::simulator::Views& views) noexcept
    {
        updateStatus(views);
    }

    void onAction(lithe::simulator::Views& views, lithe::ViewId action) noexcept
    {
        if (action == 1U)
        {
            mState.active = true;
        }
        else if (action == 2U)
        {
            mState.active = false;
        }
        updateStatus(views);
    }

    void onBack([[maybe_unused]] lithe::simulator::Views& views) noexcept
    {
    }

private:
    void updateStatus(lithe::simulator::Views& views) const noexcept
    {
        if (auto* label = views.find<lithe::LabelView>(100U))
        {
            label->setText(mState.active ? "ACTIVE" : "READY");
        }
    }

    ApplicationState& mState;
};

// The application supplies its host or board Display adapter. This wrapper
// creates no device, task, interrupt, filesystem path, or background thread.
class UiAdapter
{
public:
    UiAdapter(UiActions& actions, lithe::Display& display) noexcept
        : mUi(actions), mDisplay(display)
    {
    }

    bool initialize() noexcept
    {
        return mUi.initialize();
    }

    bool pointer(lithe::PointerEvent event) noexcept
    {
        return mUi.handlePointer(event);
    }

    bool key(lithe::Key key) noexcept
    {
        return mUi.handleKey(key);
    }

    bool update(std::uint32_t elapsedMs) noexcept
    {
        mUi.advanceTime(elapsedMs);
        return mUi.render(mDisplay);
    }

private:
    lithe::simulator::Product<UiActions> mUi;
    lithe::Display& mDisplay;
};
} // namespace product
```

This example uses the tutorial's actions 1/2 and status label 100; use the IDs in
your own form. Construct your `ApplicationState`, then `UiActions`, then
`UiAdapter`, passing your application's display driver. The state and actions
must outlive the UI; the display must outlive its adapter. `Product` owns bounded
views and text storage and borrows the action object by reference. It never copies
the hook, allocates it, or erases its type behind an opaque context pointer.

Your existing task reads its touch controller or encoder, translates the samples
into logical `PointerEvent` or `Key` values, and calls the wrapper. Supply wrapping
monotonic millisecond timestamps for pointer contacts, including cancellation when
contact is lost. Call `update(elapsedMs)` with caller-measured elapsed time and
handle a false rendering result in the application's error policy. Serialize
calls in that task; the UI does not add synchronization. Input capabilities in
`logical-input.json` still gate which logical events the component accepts.

A task using tiled output can instead call `Product::renderTiled(workspace, sink)`
with its own bounded workspace and synchronous `TileSink`; it need not create a
full framebuffer or `Display`. The host simulator's window, timers and conversion
buffer are separate consumers and are absent when standalone hosts are disabled.

## Build and run the standalone host

In the designer choose **Build > Build and Run Simulator** (Ctrl+F5). CMake,
CTest, MinGW GCC and `mingw32-make` must be on the designer process's PATH.
The generated `simulator-build.json` records the configured build root relative
to this component directory. The command installs a matching fixed-format Core
under that root's `debug/installed`, builds both native executables and runs CTest
before launching `debug/host/litheview_generated_simulator.exe` under the same
root. The build root may be outside the component directory but remains within
the application project.

After that first build, open this directory in VS Code and choose **Generated
simulator (Debug)**. The launcher's pre-launch task rebuilds the application
against that installed Core. Put `gdb.exe` on PATH and install VS Code's C++
debugger extension. Changing the library itself requires rebuilding/installing
Core with the designer command or the commands below.

To build without the designer, set `LITHEVIEW_SOURCE`, `CORE_BUILD`, and
`CORE_PREFIX` to your checkout and chosen build/install directories. Use the
`destinationFormat` from `simulation.json` (RGB565 below is an example):

```powershell
cmake -S "$env:LITHEVIEW_SOURCE" -B "$env:CORE_BUILD" -G "MinGW Makefiles" `
  -DCMAKE_BUILD_TYPE=Debug -DLITHEVIEW_DESTINATION_FORMAT=RGB565 `
  -DLITHEVIEW_BUILD_RENDERING_FIXTURES=OFF -DLITHEVIEW_BUILD_TESTS=OFF `
  -DLITHEVIEW_BUILD_WIN32=OFF -DLITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF
cmake --build "$env:CORE_BUILD" --parallel 2
cmake --install "$env:CORE_BUILD" --prefix "$env:CORE_PREFIX"
cmake -S . -B build/manual -G "MinGW Makefiles" -DCMAKE_BUILD_TYPE=Debug `
  "-DCMAKE_PREFIX_PATH=$env:CORE_PREFIX"
cmake --build build/manual --parallel 2
ctest --test-dir build/manual --output-on-failure
./build/manual/litheview_generated_simulator.exe
```

On non-Windows hosts choose a supported generator/toolchain instead; CMake builds
the headless check and omits the Win32 window. A mismatched or multi-format Core
fails the compile-time destination check. RGB565 and XRGB8888 are separate
builds, never a runtime format switch.

Headless CTest compares full-frame and tiled rendering, exercises available
logical inputs and checks transfer failure/abort/retry. It writes `initial.ppm`
and `replayed.ppm`. Passing it does not establish that your application handlers
meet product requirements, or that a physical display driver works.

Mouse input represents a pointer only when enabled in `logical-input.json`.
Arrow keys and Enter exercise supported navigation/selection; the
mouse wheel represents an encoder. Text entry requires explicit text capability.
Unsupported input paths remain disabled.

## Writing these files yourself

The generated code is an inspectable example of the same C++ interfaces you use
by hand. Define your fixed display configuration and input capabilities, static
form and resources, bounded storage and typed application callbacks. Then add
a host adapter and a CMake consumer linking `LitheView::Core`. You own every file
in a handwritten project; no generator or `simulation.json` is required by Core.
The repository's `tests/rendering/display-bringup` provides a compact handwritten
version, explained alongside this export in `docs/NEW_HARDWARE_TUTORIAL.md`.

After host simulation works, replace the display/input adapters with your board
drivers and evaluate the hardware target's flash, workspace and transfer limits.
Neither the designer nor this host build invents a driver for an unsupported
controller or qualifies an MCU's memory/timing budget.
