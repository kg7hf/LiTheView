# From a display in the designer to a running C++ product

Start by collecting the display and input specifications. Define that display in the designer, select
logical input separately, and run your application on the PC with the generated UI. Then edit its application
code and verify the behavior. Only after that passes, select or integrate the target hardware and
connect its display and input drivers to the same portable application.

```text
Display + touch / knob / buttons
               |
               v
Fixed display definition + logical input + portable application
               |
               v
PC simulation: native rendering, interaction, layout, resource checks
               |
               v
Hardware target: drivers, cross-build, bring-up, measured budgets
```

Steps 1-4 take the designer route to a working simulator. Steps 5-11 explain the equivalent handwritten
C++ route and the deeper format, storage, and resource checks. Neither route needs an MCU selection,
target budget, board SDK, or physical device. Steps 12-16 are the hardware stage. A product may already
have a chosen MCU, but that choice does not determine whether its form is compatible with the display.

This guide is for a C++ developer adding a display product. The designer generates ordinary files
against the installed `LitheView::Core` package; it is not a runtime dependency. A developer can also
write the same definitions, views, typed action hooks, resource provider, and host adapter by hand.
The handwritten route requires no designer or .NET runtime. For an existing firmware tree, follow
[application integration](APPLICATION_INTEGRATION.md): the library may live anywhere as a submodule,
the designer can add its project to an existing UI folder, and component/build outputs are configurable.

The [companion example](../tests/rendering/display-bringup/) is a working 128x96 RGB565 diagnostic application.
It supplies a starting point and a repeatable host check, not a driver for an unnamed panel. The
hardware-specific steps below identify exactly what your board integration must implement.

## Before step 1: use your application as the root

LiTheView is a library within your product, not the superproject around it. Start
from your application's existing source tree and open that root in VS Code.
Keep its firmware and Windows simulator presets, startup code, drivers and tasks.
Choose where to add the library and where your UI files should live:

| Library location | Application-owned UI location |
| --- | --- |
| `Middlewares/Third_Party/LiTheView/` | `Tasks/UI/` |
| `externals/LiTheView/` | `src/ui/` |
| `UI/LiTheView/` | `UI/`, with forms and generated files alongside the dependency |

You choose one layout; none is mandatory. Keep your forms, generated components
and event handlers outside the library submodule. The **Add LiTheView UI** wizard
records those directories, and your application build includes the library and
generated component. Follow [application integration](APPLICATION_INTEGRATION.md#choose-the-layout-that-fits-your-application)
for complete trees, relative path examples and CMake integration.

The H750 starters happen to be distributed under this repository's `examples/`.
For that exercise, the selected starter folder is the application root; its
`Middlewares/Third_Party/LiTheView/` folder is the library dependency. Your own
application does not need to live under a LiTheView checkout.

## 1. Collect the display and input specifications

Before choosing a framebuffer, record the following from the panel/controller datasheet. Include
document revision and page/figure references. Separate confirmed facts from unresolved assumptions.
Copy the [bring-up worksheet](DISPLAY_BRINGUP_WORKSHEET.md) into your product's documentation and
complete the display and input sections first. Leave unknown hardware-target fields open.

| Question | What to record | Why it matters |
| --- | --- | --- |
| What does an address select? | One pixel, an emitter, a multi-emitter cell, or a memory page | Determines application coordinates and storage blocks |
| How large is controller memory? | Addressed width/height, offsets, row/page order | Defines legal transfers and storage size |
| What is visible? | Visible rectangle/circle, cutouts, safe area, orientation | Controls clipping, hit testing, and control placement |
| What does a native byte mean? | Channel/gray fields, bit positions, byte order, level polarity | Determines the writer and PC decoder |
| How are rows stored? | Active bytes, row stride, alignment, page height, partial-block rules | Determines allocation and valid tile dimensions |
| How are bytes transferred? | Bus, commands, address widths, inclusive/exclusive endpoints, CS/DC behavior | Belongs in the display adapter |
| When can storage be reused? | Synchronous transfer or explicit DMA completion | Determines ownership and buffering |
| What initializes the panel? | Rails, reset, delays, registers, clocks, backlight/enable | Belongs in the board driver |
| What input is available? | Touch, encoder, buttons, keyboard, text entry | Defines logical input independently of the display |
| What are the physical constraints? | Write/read clock limits, refresh/busy behavior, temperature or power requirements | Must be verified on the actual hardware |

For input, collect the device-specific facts now and express what the application receives separately:

| Device | Facts to collect | Logical interface used in simulation | Typical PC equivalent |
| --- | --- | --- | --- |
| Touch controller | Raw range, axes/orientation, contact ID, release/lost-contact behavior, calibration | Pointer position, phase, ID, timestamp in display coordinates | Mouse press, drag, release, cancellation |
| Rotary encoder | Detents, direction, quadrature interpretation, optional push switch | Navigation keys; push supplies activation | Mouse wheel or up/down; Enter for push |
| Discrete buttons | Roles, polarity, debounce/repeat requirements | Navigation, select, back as needed | Keyboard bindings |
| Keyboard/text source | Supported keys, character encoding, text-entry requirement | Logical keys and text events | Host keyboard/text adapter |

The I2C/SPI peripheral, GPIO pins, interrupt routing, and debounce implementation belong to the later
board adapter. A touch-only and a knob-only product have different logical input configurations even
if their displays are identical. The portable application must validate with the chosen input.

Draw one storage unit with its bit numbers. Specify at least two literal bytes by hand before writing
an encoder. A diagram that only says "4 BPP" is insufficient: it may mean four bits per pixel,
four bits per cell, or a compressed transfer command into a different SRAM representation.

**Checkpoint:** you can explain what logical coordinate `(x,y)` changes, which stored bits represent
it, and which command sends those bits. Open questions are written down rather than embedded in code.

## 2. Decide what kind of support is missing

| Your new hardware | Work needed |
| --- | --- |
| New board/controller; existing native format | Define its geometry and input; implement display/input adapters. Usually no painter change. |
| Existing storage format; different wire byte order or command protocol | Keep the surface format; serialize/translate at the driver boundary using bounded storage. |
| New native sample layout or channel model | Extend the core format implementation, capabilities, and fixed build selection; follow step 7. |
| New shape outside the existing rectangle/circle model | Add a tested visibility contract before claiming correct clipping or hit testing. A safe rectangle is placement guidance, not a visibility mask. |
| Refresh technology with busy periods, waveform phases, or limited update cycles | Add product/driver scheduling appropriate to that panel; `TileSink` alone is not a refresh controller. |

Current destination formats are RGB565, RGB888, XRGB8888, ARGB8888, R2 packed 2x2, RRRR2222, and
RYRY2222. They are selected when the library/product is built. The enum and renderer dispatch are
closed today: adding a JSON profile or specializing a trait outside the library does not register
an arbitrary new destination writer.

For an RGB565 controller that expects the high byte first, the numeric color red is still `0xf800`.
LitheView's RGB565 bytes are `00 f8`; a high-byte-first wire protocol needs `f8 00`. That is a transfer
serialization issue, not a reason to relabel the application as XRGB8888 or introduce a second RGB
framebuffer. Check both byte order and RGB/BGR channel configuration.

**Checkpoint:** choose the adapter-only route or the new-format route. Keep that decision separate
from selecting the MCU and deciding whether it can afford a full framebuffer.

## 3. Define the display and first form inside your application

Open your **application root** in VS Code. Initialize the LiTheView submodule wherever your project
keeps third-party libraries. Use the [guided H750 starter](../examples/stm32h750b-dk-starter-1/README.md)
for a complete reproducible exercise, or follow [application integration](APPLICATION_INTEGRATION.md)
for your own source layout. The starter assumes that the Windows and embedded applications already
build, with their platform code and one RTOS task established.

Install the Designer version matching your library checkout. The
[extension guide](../tools/vscode/README.md) covers installation;
preparing the tools from source requires the .NET SDK selected by `tools/designer/global.json`.
Compiling an application with checked generated C++ requires no designer or .NET SDK.

1. Run **LiTheView: Add LiTheView UI**. Choose your UI source and generated output folders.
2. Choose **Custom display**. Enter the addressed dimensions, native framebuffer format, shape,
   safe area, physical dimensions and source/provenance facts collected in step 1. The H750 lesson
   enters 480 by 272, RGB565 storage and an RGB888 parallel panel interface explicitly.
3. Specify logical input independently. Output-only is sufficient for the first filled panel.
4. Open the new `.litheview.xml` form in the designer. It starts with a blank root panel. Set its
   fill, border color and border width, save, then press **Generate UI**.
5. Choose your existing Windows CMake preset, configure/build, select the existing Windows launcher
   and press **F5**. Your application now displays the panel. Keep using that same preset as the UI grows.
6. After the PC result is correct and the display adapter exists, build with the existing embedded
   preset and use its target launcher. Target memory and transfer validation remain separate stages.

The designer canvas is the layout preview. Generate UI writes portable component files. Neither
operation creates or runs a separate simulator application or replaces your CMake/debug setup.
The [completed H750 sibling](../examples/stm32h750b-dk-starter-1-complete/README.md) shows the expected
first-panel result without requiring another build configuration for each feature.

**Checkpoint:** the same application that built before adding UI now displays the panel on Windows,
using its original preset and launcher. The canvas alone is not this runtime check.

## 4. Understand the generated component and application ownership

The following paths are examples within your application's UI folder. You choose the source and
output directories; no file is located relative to the designer's working directory.

| File | Owner and purpose |
| --- | --- |
| `project.litheview.json` | Source manifest and locked display/input identities |
| `project.litheview.paths.json` | Application/library/component locations |
| `forms/main.litheview.xml` | Editable form and control properties |
| `profiles/display-surface.json` | Editing-surface snapshot |
| `profiles/display-definition.json`, `profiles/logical-input.json` | Independent display and input contracts |
| `Generated/form.hpp` | Portable static form description and bounded layout type |
| `Generated/display.hpp`, `resources.hpp`, `product_config.hpp` | Display constants, selected resources and form aliases |
| `Generated/assets/*.cpp`, `resources.cmake` | Selected native resource data and source list |
| `Generated/CMakeLists.txt` | Interface component target for the parent application |
| `Generated/component.json` | Generated-file hashes; surface passed, target unevaluated |
| `ui.cpp`, `ui_actions.hpp`, device/platform implementations | Application-owned UI lifetime, behavior and devices; choose your own filenames |

Regeneration verifies ownership and replaces only unmodified generated files. It rejects collisions
or edits in generated files. It creates no `main`, RTOS task, simulator runtime, unused application
hook, or `.vscode` build/debug configuration. When migrating an older `simulation.json` export,
verified obsolete scaffold is retired and existing handwritten files are preserved.

Named control accessors and typed form handlers are available; see [CONTROL_OBJECTS.md](CONTROL_OBJECTS.md).
Create or open a named handler from the Events section of the designer. Handwritten handler bodies
belong to the application and survive regeneration.

Without the visual designer, use the same component exporter after authoring the source files:

```powershell
dotnet $cli component export-project $project $library
cmake --preset win32-debug
cmake --build --preset win32-debug
```

`$cli` is the built designer CLI DLL, `$project` is your project manifest and `$library` is the tooling
checkout. Use your application's preset names. Or write the ordinary C++ display definition, layout,
resource provider and UI owner yourself; the following steps show that route.

**Checkpoint:** UI generation changes the application's component, while the application retains
its startup, devices, task, behavior, build and debugger configuration.

## 5. Optional handwritten route: run the first-light example on the PC

Use a C++23 toolchain whose standard library supports `std::expected` and `std::span`, CMake 3.25 or
newer, and its matching build tool. The checked Windows route uses MinGW GCC, `mingw32-make`, CMake,
and PowerShell 7 on `PATH`. From the repository root:

```powershell
./tools/run-display-bringup.ps1 -Configuration Debug
```

The script creates separate Core, install, and application directories under
`build/display-bringup-debug/`. It configures a fixed RGB565 Core with demos, preview, U545 probes,
and library tests disabled; builds and installs the package; builds the independent consumer; and
runs its CTest check. It also builds the separately packaged generated-font archive to satisfy the
package's install rules; the application links only `LitheView::Core`.

Inspect these outputs:

| Output under `build/display-bringup-debug/` | Purpose |
| --- | --- |
| `host/initial.ppm` | Diagnostic pattern before input |
| `host/activated.ppm` | Pattern after the test button action |
| `host/litheview_display_bringup.exe` on Windows | Debuggable host executable |
| `host/litheview_display_bringup_simulator.exe` on Windows | Interactive simulation of the same product |
| `test-application.log` | Rendering, input, and injected-transfer-failure results |
| `validation.json` | Completion result and image hashes |

Open the PPM files with a PPM-capable image viewer. The automatic check replays logical input and
writes images. Then launch the interactive Windows simulation:

```powershell
./build/display-bringup-debug/host/litheview_display_bringup_simulator.exe
```

Click the ACTIVATE/RESET button to exercise touch press/release; drag off it before releasing to
exercise cancellation. Mouse wheel or up/down keys stand in for knob navigation, and Enter/Space
for its push switch. The starter has one actionable control, so navigation has no second focus stop;
add another control when testing a multi-control knob workflow. Resizing the window changes host
presentation, not the product's 128x96 logical surface. No board or panel is accessed.

Repeat the optimized build with `-Configuration Release`. Use `-Generator Ninja` with a matching
compiler/build environment on hosts where MinGW Makefiles is not appropriate.

To run or debug the executable directly, set its working directory to the `host` directory; the PPM
files are written in the current directory. VS Code provides **LitheView: Display Bring-up Simulator
(MinGW Debug)** and **LitheView: Display Bring-up Checks (MinGW Debug)**, with the same preparation task.
Both use this script. Other operating systems can run the headless check and supply a window/input
adapter at the boundary described in step 10; this starter currently supplies a Win32 window adapter.

**Checkpoint:** the unmodified example passes, produces two meaningful images, and responds in the
PC simulator. Fix toolchain or package problems here before changing panel semantics or attaching a board.

## 6. Optional handwritten route: create the portable application

Read the companion files in this order:

1. [`product.hpp`](../tests/rendering/display-bringup/product.hpp): the fixed display definition, logical
   input, diagnostic view, button, typed action delegate, controller, and framebuffer-free application.
2. [`main.cpp`](../tests/rendering/display-bringup/main.cpp): host-owned frame/tile storage, full-frame display,
   synchronous tile sink, input replay, and PPM presentation.
3. [`win32_main.cpp`](../tests/rendering/display-bringup/win32_main.cpp): the interactive host window, native
   byte decoder, coordinate conversion, and mouse/knob-equivalent input adapter.
4. [`CMakeLists.txt`](../tests/rendering/display-bringup/CMakeLists.txt): an ordinary installed-package consumer.

Copy the companion directory into your application's source tree, then separate the portable product,
host adapter, and checks as the project grows. Keep these responsibilities separate:

```text
my-product/
  portable/     display definition, views/controllers, resources, application state
  host/         native-byte capture/decoder, window or file output, PC input mapping
  board/        startup, clocks, pins, linker map, panel driver, physical input driver
  tests/        literal pixel bytes, event replay, transport recorder, failure cases
```

The portable application borrows controllers, views, fonts, and resources. Their owners must outlive
every user of them. The board and PC own their buffers. There is no global board singleton or internal
function-pointer-plus-`void*` callback in the example; actions use a typed `ControllerDelegate`.

A minimal declaration for a different, supported RGB565 surface looks like this:

```cpp
inline constexpr lithe::DisplayDefinition kDisplay{
    "acme.definition.panel-rgb565",
    {160, 128},
    lithe::ViewportShape::rectangle,
    {0, 0, 160, 128},
    lithe::PixelFormat::rgb565,
    lithe::DisplayAddressSpace::pixels};

static_assert(lithe::validateDisplayDefinition(kDisplay));
static_assert(lithe::supportsDestination(kDisplay.format));
```

Change the layout coordinates and diagnostic drawing to match those dimensions as well. Changing
only the definition does not rescale an existing form. Use a new build/install directory when changing
the destination format, so the generated `lithe/render_config.hpp` always matches its Core archive.

Make these small changes in your copied starter, rebuilding the PC version after each:

1. Set `kDisplay` and `kInput` from the worksheet; check constexpr surface/input validation.
2. Adjust the diagnostic view, button bounds, stride and tile size to that geometry. Replace the
   starter's fixed byte-count assertions with independently calculated expectations for your display.
3. Update the host frame/decoder and the literal byte/input fixtures. The supplied `main.cpp` is an
   acceptance test for the original 128x96 product, not a geometry-independent test generator. For
   touch-only or knob-only input, replay available events and verify unavailable events are rejected.
4. Keep the selected format fixed in both Core and the application. If changing away from RGB565,
   replace the starter's RGB565-only assertions/decoder and install a separate matching Core.
5. Build your copied project using the [standalone CMake commands](../tests/rendering/display-bringup/README.md)
   with its own source/build paths and package prefix. The repository runner remains the reproducible
   baseline for the original example; its byte counts are not measurements of your edited product.

This is ordinary C++ configuration today. The worksheet does not automatically generate a simulator
or register a new pixel format. The supplied host adapter and buildable example are the starting code.

`DisplayDefinition` contains no SPI instance, GPIO, frame pointer, MCU clock, or flash ceiling.
Construct `Application` from the validated `displayViewport(kDisplay)` and supply output storage
only at rendering time, as the companion does. Physical input pins do not belong in the definition.

Choose logical input from your worksheet. For example, a knob with a push switch can declare:

```cpp
inline constexpr lithe::LogicalInputConfiguration kInput{
    .encoder = true, .select = true};
static_assert(lithe::validateSurfaceInput(kInput, {.activation = true, .navigation = true}));
```

A touch-only product instead uses `{.pointer = true}`. The supplied starter enables pointer and keys
so both host interaction paths can be explored; its `Product` filters events using that fixed
configuration. Select the intended input when adapting the example, and keep PC bindings that emulate
that input. A profile's declared input is a compatibility contract; the physical driver still needs
to deliver those logical events correctly. Adding an input flag does not generate a controller driver.

For layouts expressed as handwritten, generated, or parsed `ViewSpec` arrays, call
`validateLayoutSurface()` or `buildSurfaceLayout()` with the definition and explicit logical input
requirements. A widget having pointer and keyboard handlers does not mean the product requires both.
On a shaped display, keep controls inside the safe area or choose an explicit placement policy;
use `placeSurfacePopup()` for a declared anchored/fallback placement.

**Checkpoint:** your own geometry, controls, and selected touch/knob/button behavior validate on the
host without an MCU target profile. The remaining simulation steps establish bytes, storage, and assets.

## 7. Add a new native format when the existing writers cannot represent it

Skip this step for an adapter-only port. This is a library contribution, not a board-driver callback.
Do it in small commits, starting with literal byte tests and the storage contract.

As a concrete design exercise, consider a **hypothetical 160x128 four-bit grayscale display**:

- One logical coordinate addresses one grayscale sample.
- Two horizontal samples share a byte: left in bits 7:4, right in bits 3:0.
- Values 0 and 15 mean black and white; intermediate values are defined by the product's quantization.
- The storage block is 2x1 logical samples in one byte. A tight row is 80 bytes, a frame 10,240 bytes,
  and a 160x4 tile 320 bytes.
- Gray levels `[0,15,8,1]` must encode exactly as `[0x0f,0x81]`. Updating one nibble must preserve its neighbor.

**This G4 format is not currently implemented.** The example above specifies a proposed port; it is
not a CMake option or a ready-to-use profile. Implement each of the following before selecting it:

| Order | Location | Required change |
| --- | --- | --- |
| 1 | `include/lithe/pixel_format.hpp` | Add an enum value without renumbering existing values; define storage traits, block geometry, checked read/write, and format-query cases. |
| 2 | `include/lithe/storage_layout.hpp` and `include/lithe/display.hpp` | Ensure the common checked-layout path validates the new block, stride, capacity, and origins. Packed sub-byte formats use byte storage in `Surface`, not `PixelSurface`. |
| 3 | `src/painter.cpp` | Add guarded fixed destination dispatch. Destination offsets and row-major field indexes already use storage-block geometry; reuse that path and verify the new trait's read/write and composition behavior. |
| 4 | `CMakeLists.txt`, `cmake/render_config.hpp.in` | Add the fixed configuration and enable checks; a G4-only build must not retain unrelated destination writers. Register it in `cmake/display_products.cmake` only if adding a product to the repository's existing example machinery. |
| 5 | `include/lithe/display_definition.hpp` | Add native descriptor/channel metadata and the valid address-space rule. The current channel enum and bounded field descriptor may need extension for a new grayscale/page organization. |
| 6 | Host presentation adapter | Decode actual native bytes into displayable RGB. Do not show a separate high-color render that bypasses the new writer. |
| 7 | Tests and artifact checker | Add independent packing/composition/tile fixtures and extend `tools/check-destination-artifacts.ps1` for the new named format. |

Use `tests/storage_layout_test.cpp`, `tests/r2_render_test.cpp`, and
`tests/native_cell_render_test.cpp` as coverage examples. Avoid generating expected bytes with the
same writer being tested. For the proposed G4 format, test all 16 gray levels, nibble preservation,
nonzero aligned origins, padded stride, guard bytes, clipped updates, alpha 0/255/intermediate values,
and exact full-frame/tiled equivalence. Initially reject odd widths or misaligned origins; partial
storage blocks need an explicit contract and their own tests.

Decide how a logical RGB color becomes gray and how source alpha/coverage blend with stored levels.
Reading a gray destination should yield the corresponding logical gray for composition; it must not
silently become a red-only destination. Destination alpha storage and source-alpha blending are
separate capabilities. Gamma, optical luminance calibration, and hardware inversion also need explicit
decisions; do not infer them from the number of bits.

For sub-byte storage, `bytesPerPixel()` returns zero; do not round a fractional pixel size up to one.
Use storage-block geometry and checked layouts for allocation and transfer accounting.

If your panel is page-addressed with eight vertical one-bit samples per byte, specify a 1x8 block
and its bit order instead. The destination offset/index path already uses block geometry, but the
new format still needs registration, a tested trait, and matching metadata. In particular, the current
native descriptor holds only four fields; an eight-field page needs an explicit metadata design.
Treat this as a different format port, with page-aligned tiles and a matching host decoder.

Handwritten C++ can be your first acceptance path. If you also need profile loading/export, update the
managed profile enums, v2 schema/validator/descriptor/compiler mappings and fixtures. If native bitmap
assets are needed, also extend `BitmapFormat`, source readers, resource formats/metrics, and offline
conversion. Source bitmap handling still has R2-specific packed indexing and validation; it is separate
from the generic destination block path. A G4 destination can initially draw the existing indexed4
source format with a 16-entry gray palette. Destination support does not automatically add a native,
palette-free source-bitmap decoder. Keep the visual
designer and its preview protocol as a subsequent consumer; do not claim that a new C++ writer makes
every host tool understand the format.

**Checkpoint:** a fixed-format Core package renders independently specified native bytes and an
installed consumer can replay the same scene full-frame and tiled. No hardware is needed for this gate.

## 8. Select full-frame or tiled storage explicitly

For the companion RGB565 product:

| Choice | Render storage | Suitable first integration |
| --- | ---: | --- |
| Full frame, 128x96 | 24,576 bytes | Memory-mapped/LTDC-style output or a simple host reference |
| Full-width strip, 128x4 | 1,024 bytes | A controller accepting bounded row/rectangle transfers |

The native bytes are identical. The host's full-frame reference and PPM conversion are not part of
the embedded tile workspace. The MCU can instantiate the portable product, one strip, and its sink
without retaining a full host reference image.

Calculate allocation with `storageLayout(format, extent, strideBytes)` and check its result. For a
storage block `(blockWidth, blockHeight, blockBytes)`, a tight valid row contains
`width / blockWidth * blockBytes`, and the frame has `height / blockHeight` storage rows. Padding and
hardware alignment can make the actual allocation larger. Validate divisibility and arithmetic
before using those calculations; the public helper already supplies checked failure results.

`TileSink` borrows each tile until `writeTile()` returns. Start with a synchronous transfer that
finishes or copies the data before returning success. If DMA is still reading a reusable tile when
the renderer overwrites it, the ownership contract is broken even if the screen sometimes looks right.
For asynchronous work, provide additional owned buffers and explicit completion; the optional
`DisplayTransferAdapter` defines tokens and lifetimes but is not automatically a tiled DMA scheduler.

**Checkpoint:** allocation, stride, alignment, tile dimensions, buffer owner, and reuse point are all
explicit. Record their costs for later target selection; no MCU budget is required to run the simulation.

## 9. Keep the first application diagnostic

Use an asymmetric corner pattern, distinct red/green/blue areas, black/white and gray levels, a
one-pixel edge, and one actionable control. The companion provides this kind of small diagnostic
scene. Inspect it at native scale before adding complex UI assets.

Keep every fallible construction checked: adding a child, creating a validated layout, and pushing
a controller can fail because storage or geometry is invalid. Retain the root/controller/resources
for the full application lifetime. A temporary `ViewController` pushed from an initialization helper
is a dangling borrowed object once that helper returns.

For a general form, begin with `SurfaceInputRequirements` describing what the application needs:
activation, navigation, and/or text entry. Declare the logical input that supplies those operations.
An encoder alone supplies navigation; a button or another activation mechanism is needed for select.

**Checkpoint:** one logical action produces one visible change, and drawing can be reproduced from
the same initial state, events, and elapsed-time sequence.

## 10. Make the host simulator show the actual destination bytes

The companion constructs independent applications, renders one full-frame and the other through a
bounded tile, and compares output after logical input. Its sink also injects a transfer failure and
checks recovery. This separates drawing defects from a future bus/driver problem.

Decode the completed native surface/tiles for the window. Window scaling changes presentation only;
mouse coordinates must pass through its inverse transform into the same logical coordinate space.
Keep the host conversion/window buffer outside the portable renderer's storage accounting. If you
cannot reproduce a packing defect in this path, verify that the simulator is actually using your new
format instead of an RGB substitute.

**Checkpoint:** host output demonstrates the intended quantization, address space, shape, and selected
touch/knob/button behavior before the panel or MCU exists. This is the first usable version of the product.

## 11. Add resources after primitive rendering works

Start with the built-in `font5x7()` and a small mask or literal bitmap. Then add a `ResourceProvider`
that borrows immutable static resources. Alpha1/Alpha8 masks and supported indexed resources can be
useful on a compact destination; the resource's encoding need not equal the destination's encoding.
It must be supported by the selected source reader and have an explicit size/lifetime.

Convert larger assets offline. Record source identity, converter options, generated payload bytes,
and selected variants. Do not add a runtime image decoder just to bring up a panel. Keep resource
placement in flash/QSPI and the linker script in the product integration, separate from the form.

For designer imports, component export compiles the selected static resource files and generates the
provider bindings. Older animation manifests that omit converter `entryPoint`/`symbol` metadata need
to be reimported before this export; the tooling does not guess function identities from C++ source.

For an optional generated workflow, follow
[display definitions without a target](../tests/rendering/display-model-generated/README.md). Surface
validation/export can run before selecting an MCU. That host tool path is optional; the companion
and a handwritten embedded product have no managed-runtime dependency.

**Checkpoint:** text and one real resource render correctly in both full-frame and tiled captures,
and the build includes only the intended resource representations.

## 12. Create the board project and cross-build the same application

First get your vendor/BSP project booting with clocks, linker regions, stack, a diagnostic channel,
and an explicit fault indication. LitheView does not provide generic startup code, a linker script,
or panel initialization for an arbitrary board.

Add the portable product header/sources to that firmware. Build Core using the board compiler, CPU,
floating-point ABI, and destination selection; never link the PC archive into firmware. One common
integration is a source subdirectory in an already configured board project:

```cmake
# In your board CMake project, after selecting its toolchain and languages.
# Replace the path and the board_firmware target with your actual BSP project.
set(LITHEVIEW_BUILD_RENDERING_FIXTURES OFF CACHE BOOL "" FORCE)
set(LITHEVIEW_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(LITHEVIEW_BUILD_WIN32 OFF CACHE BOOL "" FORCE)
set(LITHEVIEW_BUILD_STM32U545_PROBE OFF CACHE BOOL "" FORCE)
set(LITHEVIEW_BUILD_DESIGNER_PREVIEW OFF CACHE BOOL "" FORCE)
set(LITHEVIEW_DESTINATION_FORMAT RGB565 CACHE STRING "" FORCE)
add_subdirectory(path/to/LitheView litheview)
target_link_libraries(board_firmware PRIVATE LitheView::Core)
```

Alternatively install a separate cross-compiled package and use `find_package` with that prefix.
The repository's `cmake/arm-none-eabi-gcc.cmake` supports `LITHEVIEW_ARM_TOOLCHAIN_ROOT`,
`LITHEVIEW_ARM_CPU`, `LITHEVIEW_ARM_FLOAT_ABI`, and `LITHEVIEW_ARM_FPU` for Arm archive builds.
It does not turn a library archive into flashable firmware. For another architecture, use the board's
own toolchain and ensure its C++23 library supports the public types.

Begin without DMA acceleration or concurrent UI dispatch. Place the product and framebuffer/tile in
deliberately owned static/task-lifetime storage; do not place a large framebuffer on a small task stack.
On a cached MCU, specify DMA-visible memory regions, alignment, cache clean/invalidate ownership, and
barriers before introducing DMA or scanout. CPU access working does not prove that the display engine
can access the same RAM.

**Checkpoint:** the actual firmware links, its map identifies the product's code/resources/storage,
and the board can report initialization failures. This is cross-build evidence, not panel execution.

## 13. Bring the panel up with a raw driver pattern

Before connecting the view tree, use the panel's documented reset/power/initialization sequence and
send a small known raw pattern through the board driver. Implement the actual pins, bus mode, clocks,
commands, addressing offsets, and required delays for this board and panel revision.

Use this sequence:

1. Establish electrical startup and a bounded diagnostic/readback path where supported.
2. Clear the addressed image using the documented operation or a known full write.
3. Write a single known storage unit, then one row/page, then the full pattern.
4. Identify all four corners and check X/Y direction, offsets, rotation, bit order, channel order,
   gray polarity, and the last visible row/column.
5. Inspect a logic-analyzer capture against the datasheet: command bytes, address bytes, payload count,
   CS/DC transitions, and clock timing. Keep the capture with the board/firmware identity.
6. Exercise a timeout or disconnected-panel error and prove that CS, buffers, and bus ownership recover.

Apply the selected panel's actual electrical/optical operating constraints. Another panel's clock,
startup sequence, or backlight policy is not a portable library default. Use the authoritative source
for that exact part, and resolve contradictory/cropped diagrams before relying on them.

**Checkpoint:** raw bytes produce the expected physical pattern with measured transport behavior.
If this fails while the PC capture is correct, debug the hardware driver before changing the painter.

## 14. Connect rendering to the working panel driver

Choose the boundary that matches your storage decision:

| Path | Your implementation | Contract to preserve |
| --- | --- | --- |
| Full-frame | Derive from `Display`: return its writable `Surface`; implement `present(regions)` | Consume/retain the frame according to your documented scanout/buffering ownership; honor damage and cache rules. |
| Tiled | Derive from `TileSink`: implement `beginFrame`, `writeTile`, `endFrame` | Consume or copy each borrowed tile before returning; propagate failure and never call an incomplete frame complete. |

For a strip driver, start with full-width, storage-block-aligned strips. In `beginFrame`, validate the
fixed extent/format and reset frame accounting. In `writeTile`, validate the destination rectangle,
pixel format, extent, stride, valid data span, and expected row order. Then select the panel window,
serialize the address and native data, finish the transaction, and advance accounting only on success.
Ensure an opened transaction is ended on write failure as well. In `endFrame(true)`, require expected
transfer coverage before accepting a completed frame. In `endFrame(false)`, clean up the aborted frame
and return whether that cleanup succeeded; do not mark the frame complete. Returning false for a
successfully handled abort would mask the original write error as `sinkEndFailed`.

`Display::present()` currently returns `void`. A successful `Application::render(Display&)` therefore
does not by itself prove that a hardware transfer succeeded. A full-frame adapter must retain an
inspectable fault/status or use an explicit transfer interface, and the board loop must check it.
After a failed presentation, arrange a full invalidation/retry because rendered damage may already
have been consumed. The tiled path returns `TileRenderResult` with specific sink failure stages.

Keep any HAL/RTOS-required `void*` at that C boundary and recover the typed object once. Inside the
adapter use borrowed references, `std::span`, and typed `noexcept` interfaces. Never retain a pointer
to a tile or a stack-allocated command header after a synchronous write returns.

**Checkpoint:** the portable diagnostic scene appears on the panel, failure injection remains honest,
and captured bytes match the host's native output after documented wire serialization.

## 15. Connect touch, keys, and time through the same logical events

Calibrate and orient physical touch coordinates in the input driver, then send `PointerEvent` values
in logical display coordinates. Rotation must be applied once consistently with display orientation.
For packed emitter/cell products, do not confuse an addressed cell with the application's logical unit.

```cpp
const lithe::PointerEvent pressed{
    {logicalX, logicalY}, lithe::PointerPhase::pressed, contactId, monotonicMilliseconds};
const bool handled = application.handlePointer(pressed);
```

The variable names above are outputs of your board input adapter. Send the matching moved, released,
or cancelled phases using a stable contact ID and monotonic timestamps. Do not discard an outside
release: it cancels activation and completes capture/gesture handling. Lost contact/reset must produce
cancellation rather than leaving a button permanently pressed.

Map an accepted encoder detent to `Key::up`/`Key::down` and a debounced activation to `Key::select`.
The board owns debounce, acceleration, calibration, and ISR-to-task delivery. Feed logical events to
the UI from one owned execution context, not directly from an interrupt while rendering is active.

For animation call `Application::advanceTime(elapsedMs)` with elapsed monotonic time, and render when
state/invalidation or an active animation requires it. The portable application owns no OS timer.
Check return values and keep transfer timeouts bounded. The small companion has no animation; its
replayed input is a starting fixture for extending your product's event/time sequence.

Follow [the touch contract](TOUCH_INTEGRATION.md) for capture and gesture behavior. Replay the same
logical events on host and board. Physical sampling/debounce can differ while the portable event
sequence and resulting UI state remain comparable.

**Checkpoint:** one real user action produces one logical action, cancellation works, and input
coordinates agree with the rendered controls at all corners and orientations.

## 16. Measure the target and make the port reproducible

Keep two acceptance records. Surface compatibility asks whether this application/layout/resources
can render and interact correctly on this display. Target feasibility asks whether a particular build
fits the selected MCU and meets transfer/update constraints. A failed target budget must not invalidate
the already working host simulation.

Measure linked flash and RAM by region, actual frame/tile/DMA allocations, resource placement and
alignment, interrupt/task stack requirements, update latency, and bus traffic. Archive totals and the
largest single function stack frame are useful regression gates, not whole-firmware or task-stack
proofs. Include cache maintenance, protocol overhead, CS gaps, busy waits, and scheduling in timing.

The optional `display check-target` workflow is described in
[the generated example guide](../tests/rendering/display-model-generated/README.md#add-an-optional-target-check).
It consumes caller-supplied evidence and leaves missing measurements unevaluated; it does not inspect
an ELF or certify the panel for you. Target-profile capacities are validation inputs. They do not
automatically rewrite the C++ capacities in `include/lithe/config.hpp`; keep the compiled configuration,
application storage, and report aligned. Never increase a budget merely to hide a regression.

Finish the port with:

- A fixed build configuration, isolated build directory, and matching VS Code/debug executable.
- A short board README naming the exact board/panel revision, pins, toolchain, startup sequence,
  flashing procedure, and expected diagnostic image/input behavior.
- Datasheet references, literal byte fixtures, deterministic host captures, full-frame/tiled comparison,
  and transport failure tests appropriate to the format.
- Source/firmware identity, linker/resource measurements, hardware captures, and explicit unresolved items.
- Formatter, critical static checks, relevant host/package regressions, and actual cross-build gates.
  `tools/run-conformance.ps1` checks the repository's M33/M7 configurations; add your target's own gate.

**Done for this port** means the named application works on the named board/display, its resource and
timing constraints are evidenced, and another developer can repeat the build and bring-up. A correct
PC image, a successful flash command, and a visibly correct panel are distinct checkpoints.

## Troubleshooting from the first symptom

| Symptom | Check first |
| --- | --- |
| Native PC image is wrong | Destination selection, byte/field mapping, stride, origin, quantization |
| PC is correct; panel colors are wrong | Wire endianness, RGB/BGR mode, channel assignment, inversion |
| Image wraps, shifts, or loses the last row | Controller offsets, inclusive endpoints, page boundaries, row stride |
| Full frame is correct; tiles have seams | Absolute tile origin, block alignment, preserved neighbors, padding/last-row handling |
| Image tears or intermittently corrupts | Buffer reuse before DMA completion, cache coherency, scanout ownership |
| Touch is mirrored or offset | Calibration, rotation applied twice, cell-versus-emitter coordinates, inverse host scaling |
| Buttons remain pressed after leaving the screen | Missing released/cancelled event or inconsistent contact ID |
| A render reports success after SPI failure | Full-frame `present()` status handling or a sink that ignores completion/failure |
| Firmware grew unexpectedly | ALL destination build, unselected assets, mismatched installed prefix, linked demo archives |

For the detailed contracts, continue with [pixel formats](PIXEL_FORMATS.md),
[bounded storage and ownership](SAFETY_AND_MEMORY.md), [surface/input placement](SCENES_GESTURES_AND_VIEWPORTS.md),
and the [coding guide](style_guide.md).
