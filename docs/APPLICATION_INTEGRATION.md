# Add LitheView to an existing application

Your application owns its directory layout, devices, tasks, toolchain, and firmware build. LitheView
can be a submodule anywhere in that tree. The designer saves a portable UI project and exports a C++
component to a location you choose. The same component can run in the PC simulator before the board
or its drivers are available.

The broader workflow builds the same application model, services, UI presenter and
forms for host and embedded targets, selecting device/platform implementations in
the application build. The [shared application plan](SHARED_APPLICATION_PLAN.md)
records the shared host/MCU architecture. The [VS Code designer](../tools/vscode/README.md) integrates form editing
inside the application root; CMake and the IDE own building and debugging.

For example, this layout is supported; none of the application folder names is required:

```text
Control Panel/
  CMakeLists.txt                        application-owned
  Core/Src/
  Devices/
    display_driver/
    touch_controller/
    button_controller/
  Middlewares/Third_Party/LiTheView/     pinned submodule
  Tasks/UI/
    ui_task.cpp                        application-owned
    ui_actions.hpp                     application-owned
    project.litheview.json             designer source project
    project.litheview.paths.json        integration paths
    forms/                             editable form sources
    profiles/                          locked display/input snapshots
    Generated/                         exported component
  out/ui-simulator/                     host build output
```

On another product, the library could be `external/graphics/lithe`, the UI could be `src/operator`,
and build output could be on another drive. The developer does not need to rename their device
classes or move them into the library.

## Set the paths in the designer

1. Launch the designer from the LitheView checkout using its normal preparation task. Its tooling
   root supplies the widget catalog, profile catalog, and native preview worker.
2. Choose **File > New Project**, select the display and logical input, and select the option to use
   an existing application folder. Choose the exact UI source folder, such as `Tasks/UI`.
   Existing files remain untouched. A collision with a new project filename fails before publication.
   Opening an existing UI instead uses its `project.litheview.json`.
3. Use **Project > Project Paths...** to choose the application root, LitheView
   checkout and generated component directory. The standalone wizard opens these
   settings after creation in an existing folder. The VS Code **Add LiTheView UI**
   wizard records these paths directly.
4. Save and choose **Generate UI**, then build with your application's existing
   Windows CMake preset and debug with its normal F5 launcher. Repeat for the MCU
   after connecting the platform drivers. Generation does not build or launch.
   The [H750 starter](../examples/stm32h750b-dk-starter-1/README.md) demonstrates this
   progression without changing presets after the first export.

The optional `buildDirectory` field below remains for legacy CLI scaffold tooling;
it does not select or redirect the application CMake build, and is not exposed by
the designer paths dialog.

Path settings live beside the project manifest: `project.litheview.json` uses
`project.litheview.paths.json`. They are separate from the hash-locked display/form contracts. For
the example above, save:

```json
{
  "schemaVersion": 1,
  "applicationRoot": "../..",
  "libraryRoot": "Middlewares/Third_Party/LiTheView",
  "componentDirectory": "Tasks/UI/Generated",
  "buildDirectory": "out/ui-simulator"
}
```

`applicationRoot` is relative to the directory containing the project manifest. The other three
paths are relative to that resolved application root. Fully qualified absolute paths are also
accepted for external library/output locations. Relative settings are preferable in source control:
moving the application and its submodule together preserves them. No saved path is relative to the
designer's launch directory. Spaces in paths are supported.

Without a sidecar, the defaults are the manifest directory as application root, the running
designer's checkout as library root, `simulation` as component directory, and `simulation/build`
as build root. This preserves the standalone tutorial. Use matching designer/Core revisions;
choosing a separate checkout does not guarantee API compatibility with every library version.

These settings choose directories for the component as a unit. The component's internal filenames
and includes stay stable. This version does not distribute individual generated headers and sources
among separate `Inc`/`Src` trees. Form and resource references retain their existing project-relative
contracts; resource imports copy into the UI project rather than referencing arbitrary external
resource roots. Hardware driver include directories are configured by the parent build.

## File ownership and regeneration

The designer may share an output directory with unrelated application files. `component.json`
records the exact generated paths and hashes. Export replaces only previously owned, unmodified
generated files; a conflicting filename or an edited generated file stops export. It does not delete
unrelated files. In particular, an existing application's `CMakeLists.txt` is never adopted or replaced:
choose a component subdirectory when that filename is already used.

Generate UI emits form/display/resource definitions, selected native assets, an interface CMake
target and the component manifest. It does not create an application, runtime wrapper, device,
standalone executable, build directory or VS Code task/launcher. Keep behavior in application-owned
files such as `ui_actions.hpp` and `ui_task.cpp`; generation does not overwrite them.

When upgrading an old `simulation.json` export, all owned file hashes are checked before migration.
Verified obsolete scaffold files are removed. The old developer-owned `application.hpp` is preserved
and a warning explains that it is no longer an automatically wired hook. Reconcile any needed
behavior into your real UI owner before removing that file. New exports do not create it.

Keep CMake caches in application build directories. Do not copy caches when relocating a project;
reconfigure with the same application presets. The application root remains your VS Code workspace.

Output paths cannot overlap form/profile/resource inputs or library source directories. For the
library's own standalone examples, output beneath its `build` directory is allowed. Symbolic links
and Windows reparse points in these paths are rejected to keep file ownership unambiguous.

## Add the component to the firmware build

An application using the source submodule provides `LitheView::Core` from its parent CMake build:

```cmake
cmake_minimum_required(VERSION 3.25)
project(ControlPanel LANGUAGES C CXX)

# Select the toolchain in the application's preset/configure command. These
# choices are fixed for this build; use another build tree for another format.
set(LITHEVIEW_DESTINATION_FORMAT RGB565 CACHE STRING "UI destination format")
set(LITHEVIEW_BUILD_RENDERING_FIXTURES OFF CACHE BOOL "")
set(LITHEVIEW_BUILD_TESTS OFF CACHE BOOL "")
set(LITHEVIEW_BUILD_DESIGNER_PREVIEW OFF CACHE BOOL "")
set(LITHEVIEW_BUILD_WIN32 OFF CACHE BOOL "")
add_subdirectory("Middlewares/Third_Party/LiTheView" "dependencies/litheview")
add_subdirectory("Tasks/UI/Generated" "components/ui")

# This target and the device implementation sources belong to the application.
add_executable(control_panel Core/Src/main.cpp Tasks/UI/ui_task.cpp)
target_link_libraries(control_panel PRIVATE LitheView::GeneratedComponent LitheView::CompactFont)
target_include_directories(control_panel PRIVATE
    Tasks/UI Devices/display_driver Devices/touch_controller Devices/button_controller)
```

Supply your existing startup files, linker script, HAL/CMSIS, RTOS, device sources, and target flags
as usual. The generated component does not select or fetch them. An installed package works too:
replace the library `add_subdirectory` with `find_package(LitheView CONFIG REQUIRED)` for a package
built for the same toolchain and destination format. A format mismatch fails the generated static
checks. Host and firmware need separate build directories and toolchains.

`LitheView::GeneratedComponent` supplies Core, the generated include directory, C++23, and selected
resource sources. Link CompactFont explicitly when using `font5x7()` or the default layout font;
applications supplying their own font need no bundled font target.
The current generated target/namespace supports one UI component per parent build.

## Connect application behavior and devices

Current controls report action IDs through the borrowed `ControllerDelegate` interface. A delegate
can borrow devices, application state or a bounded command queue through typed references. The
planned named-control/event-handler interface is documented in [VSCODE_DESIGNER_PLAN.md](VSCODE_DESIGNER_PLAN.md);
this cleanup does not pretend that Create/Open Handler or custom derived-control construction is
already implemented. There is no `void*` callback/context pair in the C++ application contract.

| Application boundary | Connection to LiTheView |
| --- | --- |
| Framebuffer display driver | Implement `lithe::Display`; bind it to `lithe::Application` or pass it to `render(display)`. Present only damaged regions. |
| Bounded tile transfer | Supply a `lithe::TileSink` and application-owned workspace to `lithe::TiledRenderer`. The sink consumes or copies borrowed bytes before returning. |
| Touch controller | Map device coordinates into display coordinates and deliver pointer events to `Application::handlePointer`, including release/cancellation. |
| Encoder or buttons | Decode/debounce in the device layer and deliver `Application::handleKey` events. |
| Text input | Deliver Unicode code points to `Application::handleTextInput` when the input contract enables it. |
| UI task | Serialize input, model/view changes, time advancement and rendering. Check failed operations. |

LiTheView owns no task, interrupt, timer or allocator. The application owns queues, DMA completion,
error handling and synchronization. A DMA transfer must retain independently owned storage until
completion; it cannot keep borrowing a synchronous tile workspace after returning.

Use host/fake implementations of the same application interfaces in the PC build. Bring up the
physical drivers later and run the separate target feasibility/budget validation. A successful PC
simulation establishes the UI behavior and display compatibility, not MCU memory or transfer limits.

## The same workflow without the designer UI

The CLI uses the same path resolver and component exporter as Generate UI. Set `$cli` to the built
`LitheView.Designer.Cli.dll`, `$library` to the tooling checkout, and `$project` to your manifest:

```powershell
dotnet $cli component export-project $project $library
cmake --preset win32-debug
cmake --build --preset win32-debug
```

Use your existing application's preset names and F5 launcher. The explicit legacy `simulator`
commands remain available for generator testing; they are separate from component generation.

For a handwritten integration, create the display/input definitions, static view descriptions,
resource provider and typed UI owner yourself. The generated headers are ordinary C++ examples of
those responsibilities. No designer or JSON manifest is needed by Core at runtime. The
[new-hardware tutorial](NEW_HARDWARE_TUTORIAL.md) covers display definitions and driver bring-up.

Named control accessors and generated handler declarations are documented in
[CONTROL_OBJECTS.md](CONTROL_OBJECTS.md). Handwritten handlers belong to the application and are
kept across regeneration. The starter pair demonstrates this path.
