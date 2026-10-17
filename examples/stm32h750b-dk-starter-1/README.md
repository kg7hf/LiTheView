# H750 starter 1: from a blank application to the first display

**This folder is the application root for the exercise.** Its placement inside
LiTheView's `examples/` is for distribution. In your own product, your application
is the superproject and LiTheView is a dependency inside it. This example uses
`Middlewares/Third_Party/LiTheView/` and keeps its UI in `Tasks/UI/`; you can instead
use `externals/LiTheView/` with `src/ui/`, or `UI/LiTheView/` with sibling forms and
generated files. See [project layout options](../../docs/APPLICATION_INTEGRATION.md#choose-the-layout-that-fits-your-application).


Open **this folder** as your VS Code workspace. This is an application that already
boots and runs one application task on both Windows and STM32H750B-DK. The Windows
build runs the same FreeRTOS kernel and CMSIS wrapper with a Windows platform port.
The board build uses Cube-generated startup, STM32 HAL and the Cortex-M port.

This is the guided starting point. The matching
[stm32h750b-dk-starter-1-complete](../stm32h750b-dk-starter-1-complete/README.md)
contains the expected result if you want to compare your work.

The starting project contains **no form, display profile or generated UI**. Its
builds do not need LiTheView, .NET or the designer. The Windows window is
black and its title counts task heartbeats; the board blinks LD1 with the LCD
backlight off. FreeRTOS's idle and timer tasks are kernel infrastructure in addition
to the one application task. All application/kernel objects use static storage.

This lesson assumes those two starting builds already work. If you are setting up
this supplied checkout for the first time, see **Starting-project prerequisites**
below. The LiTheView lesson begins here.

## 1. Initialize the LiTheView package under Middlewares/Third_Party

Run in the VS Code terminal at this application root:

```powershell
./tools/init-litheview.ps1
```

This initializes the pinned `Middlewares/Third_Party/LiTheView` submodule, without
recursively fetching the library's examples and their dependencies. The script
handles this example being a folder inside a larger Git repository.

In your own independent application repository, the equivalent initial addition is:

```powershell
git submodule add https://github.com/kg7hf/LiTheView.git Middlewares/Third_Party/LiTheView
git -C Middlewares/Third_Party/LiTheView checkout <reviewed-library-commit>
git add .gitmodules Middlewares/Third_Party/LiTheView
```

For an already registered submodule use `git submodule update --init --
Middlewares/Third_Party/LiTheView`. This supplied example already registers and
pins its package; do not add it a second time. Other application layouts can use
another path and set `litheview.libraryRoot` accordingly.

Install the LiTheView Designer version matching the pinned library using
**Extensions: Install from VSIX...**, then reload VS Code. The package is a
VSIX, not a published Marketplace download. For a **source-only** extension, run
`Middlewares/Third_Party/LiTheView/tools/prepare-vscode-designer.ps1` from this
application root before opening the designer; an installed extension alone does
not prepare a newly initialized submodule. A matching bundled-runtime extension
already contains its service and worker. See [designer setup troubleshooting](#designer-setup-troubleshooting)
for SDK selection and updates.

## 2. Define your display, then open a blank canvas

Press **Ctrl+Shift+P**, run **LiTheView: Add LiTheView UI**, and enter:

| Application paths | Value |
| --- | --- |
| UI name | `First Display` |
| UI source folder | `Tasks/UI` |
| Generated component folder | `Tasks/UI/Generated` |

Leave **Display configuration** on **Custom display**. For this exercise, describe
the H750 kit's panel as if it were a new display. You are creating an
application-owned definition; you do not need a catalog profile or connected board.
Enter the following values from the panel specification:

| Custom display field | Value | Meaning |
| --- | --- | --- |
| Shape | Rectangle | Visible surface geometry |
| Width / Height (pixels) | `480` / `272` | Coordinates used by the form |
| Native storage format | RGB565 (16 bits) | Storage and rendering format selected for this application |
| Active width / height (mm) | `95.04` / `53.86` | Active image area, excluding the bezel |
| Panel transfer format | RGB888 parallel | Physical RGB interface driven by LTDC |
| Safe area | `0,0,480,272` | Rectangle where controls may be placed |
| Manufacturer | `Rocktech Displays Limited` | Display supplier |
| Display model | `RK043FN48H-CT672B` | Panel identity |
| Datasheet HTTPS URL | `https://www.rocktech.com.hk/lcd-product/rk043fn48h-ct672b/` | Source of the display specifications |
| Datasheet title / revision | `Rocktech RK043FN48H-CT672B product specification page` | Source description |
| Application input | Output only | Start with display output; add pointer input in step 6 |

Coordinates start at the top left with zero rotation. These settings describe
compatibility independently of MCU flash, memory or timing limits. The wizard
checks the definition and writes a locked snapshot into your UI project.

**RGB565 and RGB888 parallel describe different things.** Each framebuffer pixel
occupies two bytes in RAM. LTDC reads that RGB565 buffer and drives the panel's
24-bit RGB interface. Choosing a 32-bit XRGB8888 framebuffer instead would require
a different build configuration and matching driver storage. This lesson's driver
is deliberately fixed at 480 Ã— 272 RGB565 and checks that contract at compile time.

On your own product, collect these values from the display documentation and
choose a framebuffer format supported by the library and your display adapter.
The definition does not initialize pins, clock signals or the display controller.
Those belong to the platform driver, which you can implement after the host build
works. A new pixel encoding may require renderer support as well as a driver.

Click **Create UI**. A blank 480 Ã— 272 canvas opens. The full-size `rootPanel`
already exists because the form needs a root container; it has no border or child
controls. Creation saves source files only. It does not generate or run code.

## 3. Draw the filled border and generate the UI

1. Select **rootPanel** in the Components list (or click the empty canvas).
2. Keep its frame at **X 0, Y 0, Width 480, Height 272**.
3. In **Properties â†’ Appearance**, set **Background Color** to gray. Use the
   swatch value `#303030`, or enter the legacy color value `0x00303030`.
4. Set **Border Color** to white (`#ffffff` in the swatch, or `0x00ffffff`).
5. Set **Border Width** to **2** pixels. The border now surrounds the canvas.
6. Press **Ctrl+S**, then click **Generate UI**.

The form itself is the visual preview. There is no separate preview application
to launch. Designer zoom changes the editor's presentation only; it does not
change the product's pixel dimensions.

The saved form should describe one panel:

```xml
<panel frame="0,0,480,272" backgroundColor="0x00303030"
       borderColor="0x00ffffff" borderWidth="2" />
```

The designer also retains metadata comments identifying the form and root panel.
The files you have created are:

| Files | When created and purpose |
| --- | --- |
| `Tasks/UI/project.litheview.json` | Create UI: form references and locked profile references |
| `Tasks/UI/project.litheview.paths.json` | Create UI: relative application/library/output paths |
| `Tasks/UI/forms/main.litheview.xml` | Create UI, then your edits: layout source of truth |
| `Tasks/UI/profiles/*` | Create UI: your custom display, derived definition and logical-input snapshots |
| `Tasks/UI/Generated/form.hpp` | Generate UI: static view descriptions and bounded layout storage |
| `Tasks/UI/Generated/display.hpp` | Generate UI: display/input constants and compatibility checks |
| `Tasks/UI/Generated/resources.*` | Generate UI: selected resources and CMake inputs |
| `Tasks/UI/Generated/product_config.hpp` | Generate UI: stable aliases for the selected form/resources |
| Other generated files | Export bookkeeping; this application builds the form and resources through its own CMake |

Generated files carry an ownership manifest. The exporter protects handwritten
files and edited generated files from replacement. Keep application behavior in
application-owned sources. For subsequent layout changes, save and **Generate UI**
again before rebuilding either target.

## 4. Build the Win32 application and press F5

1. Run **CMake: Select Configure Preset** and select **win32-debug**.
2. Run **CMake: Build**. Configure once if this preset has never been configured;
   select the matching build preset if prompted.
3. In **Run and Debug**, select **H750 starter: Windows**.
4. Press **F5**.

The Windows window should contain your gray panel with a white two-pixel border.
The same FreeRTOS application task now initializes and renders the generated form.
The Windows display driver presents its native RGB565 surface through GDI. This
exercises your application task and platform adapter as well as the form.

The equivalent terminal build and automated application check are:

```powershell
./tools/build.ps1 -Preset win32-debug -Action Test
```

That check verifies task progress, successful initialization and a presented frame.
Changing the form's colors or border width does not require editing the check.

Use **the same preset and launcher before and after the lesson**. There is no
blank/UI build switch. The application's CMake watches for the completed export
marker, `Tasks/UI/Generated/component.json`. The next ordinary build automatically
reconfigures when that file first appears, adds the generated resources, LiTheView
Core and `Lessons/FirstDisplay/ui.cpp`, and selects the platform display adapter.
Incomplete exports stop with a Generate UI diagnostic. Later generation rebuilds
the same application in the same build directory. The designer owns neither the
application's build commands nor its RTOS task or debug launcher.

If upgrading an older checkout, select `win32-debug` or `stm32-debug` once to replace
any previously selected `*-ui-debug` preset. The old `STARTER_WITH_UI` cache entry
is cleared automatically; no build-directory deletion is required.

## 5. Add a button, a label and a click handler

Return to the form in the designer. The running application remains your normal
Win32 build; stop its debug session before rebuilding.

1. Add a **Label**. Set **Name** to `label1`, **Text** to `ON`, **Text Scale** to
   `3`, text color to white, and frame to `140,64,200,60`.
2. Add a **Button**. Set **Name** to `button1`, **Text** to `TOGGLE`, text color to
   white, background to `0x00805020`, and frame to `140,148,200,60`.
3. Select `button1` and click **Create/Open click handler** in the inspector.
   VS Code opens `Tasks/UI/form_handlers.hpp` at `onButton1Clicked()`.
4. Put this code in the new method:

   ```cpp
   mOn = !mOn;
   getLabel1()->setText(mOn ? "ON" : "OFF");
   ```

5. Add the following private member inside `class Form`, after the closing
   `// </litheview-handlers>` marker:

   ```cpp
   private:
       bool mOn{true};
   ```

6. Save the C++ file and the form. Continue to **step 6** to enable input before
   generating and running the interactive application. Adding a button does not
   automatically enable a mouse or touch controller.

There is no runtime action number to assign for this path. Generated code connects
the actual button object to the form's named virtual method. `getLabel1()` returns
the real `LabelView` object; `setText()` invalidates it so the next application tick
redraws it. These literal strings have static lifetime. For changing text buffers,
the application must keep the buffer alive while the label borrows it.

`form_handlers.hpp` is application-owned. Generate never rewrites its method bodies.
Create/Open returns to an existing handler or inserts another stub at the marker.
Keep the `litheview-handler` binding comments: a renamed or deleted control is
diagnosed before generation instead of silently discarding your behavior. Update
the corresponding override and comment together when deliberately renaming it.

The supplied CMake watches for this handler header as well as the generated export.
The first build after saving it selects `application::ui::Form` automatically.
The UI task builds the form before dispatching input, so its named controls exist
when the handler runs. Keep UI changes in that task; send explicit application
messages when a click needs work performed by another task.

## 6. Add input: Windows mouse and H750 touch

The display definition describes the pixels. The **logical input definition**
describes how the application interacts with controls. The initial **Output only**
configuration deliberately ignores pointer samples, even when a button and its
handler exist. The designer canvas edits controls; test clicks in the running
application window.

1. Stop the application debug session and return to your form in the designer.
2. Click **Configure** in the form toolbar, or run **LiTheView: Configure**
   from **Ctrl+Shift+P** with the form open in the designer. The dialog reopens
   the saved **Display** and **Input** settings; leave the display unchanged here.
3. In **Input**, select **Touch**, just as in the initial setup screen, and click
   **Apply**. This enables logical pointer
   input, supplied by the Windows mouse in the host build and the FT5336 controller
   in the H750 build. It does not select an MCU or install a hardware driver.
   The command saves
   `Tasks/UI/profiles/logical-input.json` and updates its matching lock in
   `Tasks/UI/project.litheview.json`; your form and handler remain intact.
4. Save the form and handler, then click **Generate UI**. This is required even
   if the form's layout did not change: the generated input configuration changed.
5. Build **win32-debug**, select **H750 starter: Windows**, and press **F5**.
   Click and release TOGGLE: ON becomes OFF; click again: ON returns.

If you selected Touch when originally creating the project, applying it again
leaves the input files unchanged. Still generate before rebuilding if the generated
component predates that choice. Do not recreate the UI or manually edit a profile
hash to add input. If Configure is missing, install Designer 0.2.7+ and run
**Developer: Reload Window**; an explicitly overridden older service must also be updated.

Use the same **Configure** screen whenever display or input requirements change.
It shows the current dimensions, shape, safe area, storage format and display
specification, alongside independent input capabilities. Cancel leaves files
unchanged. Applying an input-only change leaves the display profiles untouched.
Every form in the UI project shares these settings.

Apply preserves form coordinates and handwritten handlers. If you
change width or height, set the root panel's **Frame** to `0,0,newWidth,newHeight`
in the designer and reposition controls as needed; Generate UI rejects a root
that does not match the display. Review assets and application display adapters
for the new configuration before generating and building. The H750 adapter in
this lesson remains fixed at 480 Ã— 272 RGB565. Configure does not rewrite it,
configure pins/buses, or establish MCU memory and transfer budgets.

This example supplies the application-owned input adapters already. Read them in
this order to understand what you would provide for a new product:

| File | Responsibility |
| --- | --- |
| [Devices/input.hpp](Devices/input.hpp) | Shared `PointerSample`, checked `initializeInput()` and `readPointer()` interface |
| [Platform/Win32/main.cpp](Platform/Win32/main.cpp) | Receives mouse press, move, release and capture-loss messages from Windows |
| [Lessons/FirstDisplay/Win32/input.cpp](Lessons/FirstDisplay/Win32/input.cpp) | Passes those samples through a bounded queue from the window thread to the application task |
| [Lessons/FirstDisplay/STM32/input.cpp](Lessons/FirstDisplay/STM32/input.cpp) | Initializes and polls the FT5336 through the supplied I2C4 setup, maps coordinates to the landscape display, and reports contacts/releases |
| [Lessons/FirstDisplay/ui.cpp](Lessons/FirstDisplay/ui.cpp) | When generated pointer input is enabled, initializes the adapter and dispatches bounded input on the UI owner task |

Both platforms follow the same path:

```text
Windows mouse -> bounded queue --+
                                +-> PointerSample -> UI task -> form controller
H750 FT5336 -> touch adapter ----+     -> button1 -> onButton1Clicked() -> label1
```

The generated definition enables this input path at compile time. CMake selects
the platform implementation of the same device interface. For different hardware,
implement that interface and its pin/bus setup in your application; LiTheView's
form and handler do not depend on a specific touch-controller chip. Input faults
and queue exhaustion return a checked error, and the UI cancels pointer capture.

If the button draws but does not respond, first check that
`Tasks/UI/profiles/logical-input.json` has `"pointer":true`, regenerate, rebuild
and restart the application. Do not edit `Generated/display.hpp`; generation
produces its `DisplayConfiguration::kInput` from the locked input profile.
If the command reports an unsaved configuration, save the named JSON documents
before generating. A mismatched saved lock is rejected rather than silently repaired.

## 7. Select the STM32 configuration and run on the H750

Select **stm32-debug** with **CMake: Select Configure Preset**, then configure
and build. The terminal equivalent is:

```powershell
./tools/build.ps1 -Preset stm32-debug
```

Select **H750 starter: STM32H750 (ST-LINK)** and press **F5**. Confirm the
serial number of your H750's ST-LINK when prompted, especially with other boards
connected. The launcher builds, programs/verifies the image and stops at `main`.
Continue to start the kernel and display. Touch TOGGLE to exercise the same
`onButton1Clicked()` method used by the Windows mouse. The metadata in `.settings/ide.store.json`
is required by the installed STM32 debug adapter; keep it with this project.

The STM32 display implementation initializes SDRAM, supplies native RGB565 storage,
cleans the data cache, and swaps the two LTDC framebuffers at vertical blank. It
waits for the reload to complete before reusing a scanout buffer. Presentation
then turns on the backlight. The default RTOS task has an 8 KiB static stack.

Target feasibility is checked separately from display compatibility: this fixed
board's linker limits flash to 128 KiB and reserves 522,240 bytes of external SDRAM
for two 480 Ã— 272 Ã— 2-byte buffers once the generated UI is present. Review build memory output,
task stack high-water and presentation time for your application. Passing the PC
test alone does not prove target flash, workspace or transfer feasibility.

## 8. Bonus: a physical push-button and a board front view

The shared button adapter, Windows front-panel adapter and input tests are included
in this starter. The completed sibling includes the finished counter and photo
configuration so you can compare your work.

Start with a front photograph or drawing of **your device**, including its display
and physical buttons. Save it as a PNG inside your application, for example
`Simulation/my-device-front.png`; this is PC artwork, not a display resource.
Choose a straight-on view so rectangular areas line up with the display and buttons.
For this worked exercise, use the supplied `board-front.png`. The next component
belongs to the product, outside the LCD: its blue USER button at the lower left.
On Windows, click that part of the photo.
On the H750, press B1. Both enter the same application handler and increment a
second label. The existing TOGGLE button and touch input continue to work.

### Mark the PC front view

1. Stop debugging, open the form, and click **Configure**. This section requires
   Designer **0.2.8+**; reload the VS Code window after installing the updated VSIX.
2. Leave **Display** and **Input: Touch** unchanged. In **PC front view**, choose
   **Choose front-view PNGâ€¦** and select your device picture. For this exercise,
   select `board-front.png` at this application's root.
3. Set **PC window width** to **905**. Select **LCD display area** and drag a
   rectangle around the LCD's active image area. The numerical fields allow an
   exact adjustment: **X 232, Y 166, Width 1386, Height 812**.
4. Enter **userButton** as the physical button name, then **Add push-button**.
   Drag around the blue USER button, or enter **X 344, Y 1140, Width 84, Height 80**.
   The LCD and button rectangles must not overlap. Coordinates refer to the
   original 1810 Ã— 1326 example image, regardless of the editor's display size.
   These numerical values are specific to our photo. For your own image, draw
   around its LCD and physical buttons and use the resulting coordinates.
5. Keep **Show front view in the PC application** checked and click **Apply**.

Configure saves `Tasks/UI/project.litheview.front-panel.json`. Its image path is
relative to the application root in `project.litheview.paths.json`. A different
application can keep its PNG anywhere inside its own tree. CMake reads this
sidecar for the Windows build and copies the PNG beside `starter.exe`. The STM32
build does not include the photo, GDI+ decoder, hit rectangles or PC presentation
code. Its display still renders directly to the same native RGB565 buffers.

The button name identifies an application device, not a UI control. This starter
already maps **userButton** to B1/PC13. A new product supplies its own matching
Win32 and GPIO adapters; Configure does not generate pin assignments or drivers.
Uncheck **Show front view** to return the PC application to its LCD-only window.

### Assign the physical button on both platforms

There are two assignments to make in your application. The name from Configure
is the bridge to the Windows adapter; the embedded adapter supplies the physical
electrical input. The starter includes both implementations so you can inspect
and run the whole path before adapting it for your product:

1. **Windows assignment:** open `Platform/Win32/front_panel.cpp`. In `mouse()`,
   the named region **userButton** calls `setUserButton(true)` when pressed and
   `setUserButton(false)` on release or cancellation. This stores an atomic raw
   device state. `Lessons/FirstDisplay/Win32/input.cpp` implements the shared read:

   ```cpp
   bool readUserButton() noexcept
   {
       return userButton.load(std::memory_order_relaxed);
   }
   ```

   The selected image coordinates are transformed into window coordinates for
   hit testing. LCD clicks go through a separate transform and the touch path.
   When adding another named physical input, add its typed state/read function
   and map its region in this adapter. Also declare its accepted name in
   `cmake/front_panel.cmake`; an unconnected name causes a build diagnostic.

2. **Embedded assignment:** open `Lessons/FirstDisplay/STM32/input.cpp`. It
   implements the **same** interface from `Devices/user_button.hpp`:

   ```cpp
   bool readUserButton() noexcept
   {
       return HAL_GPIO_ReadPin(B1_GPIO_Port, B1_Pin) == GPIO_PIN_SET;
   }
   ```

   For this H750 board, Cube's `Core/Inc/main.h` assigns B1 to **GPIOC, pin 13**;
   pressed is high. Its `.ioc` and GPIO initialization already contain that
   assignment. For your own hardware, configure the pin, pull resistors and
   electrical active level from its schematic, then implement this read with
   your HAL or device driver. An active-low button compares with `GPIO_PIN_RESET`.
   Polling here does not depend on an EXTI callback. CMake selects one platform's
   implementation; the shared task and form never include STM32 GPIO types.

3. **Shared application assignment:** `Lessons/FirstDisplay/ui.cpp` samples
   `readUserButton()` and debounces it, then calls `Form::onUserButtonPressed()`
   on the UI task. Add that method in the next subsection. Neither the mouse
   thread nor a hardware ISR directly edits the label. Do not enable logical
   Select just to route this independent device button to the counter.

### Add the counter and its application handler

1. Return to the form. Add a **Label**, name it **buttonCount**, and set its text
   to **PRESSES: 0**, Text Scale to **2**, and text color to white. A convenient
   frame below TOGGLE is **X 20, Y 224, Width 440, Height 28**.
2. Save and **Generate UI**. The generated form now provides `getButtonCount()`.
3. Open your handwritten `Tasks/UI/form_handlers.hpp`. Keep the existing toggle
   handler. Add these includes after `Generated/product_config.hpp`:

   ```cpp
   #include <array>
   #include <charconv>
   #include <cstdint>
   #include <limits>
   #include <string_view>
   ```

4. Add a **public** method to `application::ui::Form`, outside the generated
   `litheview-handlers` markers:

   ```cpp
   public:
       void onUserButtonPressed() noexcept
       {
           if (mButtonCount == std::numeric_limits<std::uint32_t>::max())
           {
               return;
           }
           ++mButtonCount;
           const auto result = std::to_chars(mCountText.data() + 9U,
                                            mCountText.data() + mCountText.size(), mButtonCount);
           if (result.ec != std::errc{})
           {
               getButtonCount()->setText("COUNT ERROR");
               return;
           }
           getButtonCount()->setText(std::string_view{mCountText.data(), result.ptr});
           getButtonCount()->invalidate();
       }
   ```

5. Add these members to the form's existing **private** section:

   ```cpp
   std::uint32_t mButtonCount{};
   std::array<char, 24U> mCountText{'P', 'R', 'E', 'S', 'S', 'E', 'S', ':', ' '};
   ```

   The label borrows its text: the buffer must outlive the displayed text, so it
   is a form member. `invalidate()` requests repaint after changing that same
   buffer in place. `to_chars` uses bounded storage without allocating a string.
   The count saturates at the largest `uint32_t` value.

6. Save, build the same **win32-debug** preset, and press **F5** with **H750 starter:
   Windows**. The display appears over the board photo. Click the blue button:
   PRESSES increases once. Holding it does not repeat. Click TOGGLE on the LCD:
   ON/OFF still changes independently. Counts reset when the application restarts.
7. Build **stm32-debug** and use the existing STM32 launcher. Continue from `main`
   and press the physical blue B1 button. It increments the same counter through
   the same C++ method. Touch TOGGLE to check the independent touch path.

The device path is application-owned:

```text
PC mouse -> named photo rectangle -> atomic button state --+
                                                         +-> UI task debounce
STM32 B1/PC13 -> GPIO input -------------------------------+   -> onUserButtonPressed()
                                                              -> buttonCount
```

`Devices/user_button.hpp` defines the typed device interface and a 20 ms stable
state debounce. `Lessons/FirstDisplay/ui.cpp` calls the handler on the UI owner
task, once per debounced press. A compile-time check allows earlier lesson stages
to build before that method exists. There is no event allocation, ISR control
mutation, generic `void*` callback or use of the focused UI button's Select event.
Mouse capture loss and dragging outside the simulated physical button release it.
Very short presses below the debounce interval are ignored on both platforms.

The completed sibling includes this bonus lesson. Its
`starter.exe --button-self-test` verifies the front-view hit mapping, repeated
presses, hold behavior, cancellation, counter repaint and LCD touch mapping.
For another product, the named regions are useful host metadata; connect them
to that application's device interface and hardware implementation.

## What would I write without a designer?

You would supply the same display/input definitions, static view descriptions,
resource provider and selected native resource data. The one-box form is a panel
at `(0,0,480,272)`, gray fill, white border, border width 2. The integration in
`Lessons/FirstDisplay/ui.cpp` would build that layout and connect it to your display.
The completed lesson also supplies `form_handlers.hpp`, whose body is ordinary
C++ you would write yourself. `FormInstance` owns the controls and controller;
`ButtonView::onClicked()` is available for reusable control behavior, while the
form override handles this application's action. See
[typed controls and handlers](../../docs/CONTROL_OBJECTS.md) for manual construction
and a derived-button example. No XML parser, designer service or .NET runtime is
required on the MCU.

Your code owns the task, device interfaces and application behavior. Generated
code owns layout construction, properties and named button event wiring.
Asset sizing is a product choice: final-size assets, several generated sizes, or
explicit runtime scaling where worthwhile, especially fonts. The label and button use the optional
CompactFont package. Integer text scale is explicit; there are no firmware bitmap
assets in this lesson. The bonus board picture belongs only to the PC application.

## Starting-project prerequisites

Download [STM32CubeMX from STMicroelectronics](https://www.st.com/en/development-tools/stm32cubemx.html) and install it separately under ST's terms.
LiTheView does not bundle or redistribute CubeMX. It is needed to edit the `.ioc`
hardware configuration or regenerate ST initialization code; it is not needed to
build the checked-in starter sources or to use the LiTheView designer.

These steps establish the assumed working firmware/RTOS application before step 1:

```powershell
./tools/init-platform.ps1
./tools/build.ps1 -Preset win32-debug -Action Test
./tools/build.ps1 -Preset stm32-debug
```

Use PowerShell 7.2+, CMake 3.25+, Ninja, STM32 GCC with C++23 support, and **32-bit**
MinGW-w64 GCC/GDB (i686 target). The CMSIS wrapper encodes handles
as 32-bit values. The host compiler's private DLL path is also set for direct
CMake Tools builds. No global PATH modification is needed. Override tool locations
with `STARTER_CMAKE`, `STARTER_NINJA`, `STARTER_MINGW_BIN`, `STARTER_ARM_BIN` as needed.

Drivers and middleware are pinned submodules. The same ST FreeRTOS 11.2.0 kernel
and CMSIS wrapper compile in both builds; only the port changes. The separate
upstream FreeRTOS-Kernel checkout supplies its Windows port. The `Core/` files
and `.ioc` retain Cube's normal layout. After Cube regeneration, run
`tools/prepare-cube.ps1`, review the diff and recheck both builds; do not let Cube
replace the pinned vendor dependencies or application-owned CMake files.

Build configuration discovers tools on PATH and in the STM32Cube tool bundles.
For Windows it also checks the conventional MSYS2 `mingw32/bin` installation on
the system drive, skipping unrelated x64 compilers. Neither vendor IDE is needed
for a host-only build. Tool paths are stored locally under ignored `build/`.

Before your first F5 session, verify the selected debugger tools:

```powershell
./tools/configure-tools.ps1 -Platform Win32 -RequireDebugger
./tools/configure-tools.ps1 -Platform STM32 -RequireDebugger
```

The script creates directory junctions under `build/tool-links`; it does not copy
or redistribute the installed tools. The two launchers use these same local links.
Keep the project on a Windows filesystem that supports directory junctions (NTFS).
When moving the project to another computer, exclude `build/` and configure again.

| Optional environment override | Value |
| --- | --- |
| `STARTER_CMAKE`, `STARTER_NINJA` | Full path to the executable |
| `STARTER_MINGW_BIN` | i686 GCC, G++ and matching GDB directory |
| `STARTER_ARM_BIN` | Arm GCC, G++, objcopy and size directory |
| `STARTER_ARM_GDB_BIN` | Arm GDB directory |
| `STARTER_STLINK_BIN` | ST-LINK GDB server directory |
| `STARTER_PROGRAMMER_BIN` | STM32CubeProgrammer CLI directory |

An invalid explicit override is an error. Set overrides in the environment before
opening VS Code; terminal-only variables are not visible to the CMake extension.
After changing compilers, run `cmake --fresh --preset win32-debug` (or
`stm32-debug`) to replace the old compiler cache. Tool discovery never changes the
global PATH. The compiler's DLL directory is set for each compile/link invocation.
In VS Code, select a CMake executable with **CMake: Select CMake Executable** if
needed; `STARTER_CMAKE` selects the terminal build helper's executable.

The ST launcher prompts for your probe serial without a board-specific default.
Enter the connected H750 probe identity, especially when several boards are attached.
CMake Tools and the explicit build tasks target the same `starter.exe` / `starter.elf`.

The checked-in example remains blank so you can perform the lesson yourself.
The completed sibling contains the generated end state; see [VALIDATION.md](VALIDATION.md)
for the checks performed on both examples.

### Optional: build the extension package from source

Use these commands only when producing your own VSIX, from a LiTheView tooling
checkout containing Designer **0.2.8+**. Both examples pin the reviewed library
and designer tooling together. The current outer checkout or the initialized
middleware checkout can build the extension package. These commands run from
that LiTheView package root, not from the application root:

```powershell
./tools/prepare-vscode-designer.ps1
./tools/package-vscode-designer.ps1
```

Install the resulting VSIX from the package's `build/vscode-designer` folder.
Preparing tools requires the .NET SDK selected by its `tools/designer/global.json`,
CMake, Ninja and a native C++23 compiler. The installed VSIX carries its service
and preview worker; it needs .NET 10 runtime to run. A custom .NET install can be
selected with `litheview.dotnetPath` in VS Code settings.

If preparation reports SDK 9 while requesting SDK 10, check which `dotnet` the
terminal found. Different installations have separate SDK inventories. If you installed the required SDK under your user account but PATH selects an
older machine-wide SDK, a deliberate tooling rebuild can select that installation
without changing PATH or `global.json`:

```powershell
./tools/prepare-vscode-designer.ps1 `
    -Dotnet "$env:USERPROFILE/.dotnet/dotnet.exe"
```

The installed extension automatically searches the per-user .NET installation.
It does not require this preparation command to edit or create forms.

Generate UI emits a portable component and its ownership marker, not an application or standalone
simulator. The existing CMake presets and launchers remain the only build/debug workflow. Earlier
`simulation.json` exports are accepted during migration; generation verifies their owned files before
retiring unused scaffold. Application-owned handlers are preserved and remain outside generated code.

## Designer setup troubleshooting

Opening this starter in a new checkout does not update an older LiTheView
extension already installed in VS Code. Install the matching extension, prepare
the tools in this application's `Middlewares/Third_Party/LiTheView` submodule,
and run **Developer: Reload Window**. An outer checkout's prepared tools do not
prepare this submodule automatically.

A widget-catalog error indicates mismatched service/library versions. Missing
`libstdc++-6.dll` or `libgcc_s_seh-1.dll` indicates that the native preview worker
cannot find its compiler runtime; the current preparation script supplies the
matching DLLs locally. See the [setup guide](../../tools/vscode/README.md#updating-an-existing-designer-installation)
for SDK selection, tool overrides and recovery steps. These tooling repairs do not
require regenerating the UI or changing the application's CMake presets.
