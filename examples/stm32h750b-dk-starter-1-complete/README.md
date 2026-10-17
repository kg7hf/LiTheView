# H750 starter 1: completed first display

This is the expected end of the [guided starter](../stm32h750b-dk-starter-1/README.md).
Open **this folder** as your VS Code workspace to build the finished lesson.
To work through the exercise yourself, open the blank starter instead.

The completed application contains one full-size panel at `(0,0,480,272)`, gray fill
`0x00303030`, white border `0x00ffffff`, and border width 2. A `button1` labelled TOGGLE changes `label1` between ON and OFF.
The handwritten `Tasks/UI/form_handlers.hpp` contains the named click handler.
Windows mouse and H750 touch events enter the same application task.
The bonus lesson adds `buttonCount`: click the blue USER button in the
Windows board photo, or press physical B1 on the H750, to increment PRESSES.
The adapters, generated counter and front-view configuration are included here.

**Configure â†’ PC front view** saves
`Tasks/UI/project.litheview.front-panel.json`, mapping the photo's LCD area and
`userButton` rectangle. The bonus adapter maps that device to B1/PC13. See
[bonus step 8](../stm32h750b-dk-starter-1/README.md#8-bonus-a-physical-push-button-and-a-board-front-view)
for the complete sequence, persistent label buffer and shared debounce logic.

## Build and run

Download [STM32CubeMX from STMicroelectronics](https://www.st.com/en/development-tools/stm32cubemx.html) and install it separately under ST's terms.
LiTheView does not bundle or redistribute CubeMX. It is needed to edit the `.ioc`
hardware configuration or regenerate ST initialization code; it is not needed to
build the checked-in starter sources or to use the LiTheView designer.

Initialize the pinned platform and LiTheView dependencies without recursion:

```powershell
./tools/init-platform.ps1
./tools/init-litheview.ps1
```

The generated C++ is checked in, so building this result does not require the
Designer or .NET SDK. Follow the **Starting-project prerequisites** in the guided
starter, including `tools/configure-tools.ps1 -Platform Win32 -RequireDebugger`
and its STM32 counterpart before F5. Both launchers use locally discovered tools.

1. Select **win32-debug** with **CMake: Select Configure Preset**.
2. Run **CMake: Configure**, then **CMake: Build**.
3. Select **H750 starter: Windows** in Run and Debug and press **F5**.
4. After checking the Windows application, select/configure/build **stm32-debug**.
5. Select **H750 starter: STM32H750 (ST-LINK)** and press **F5**. Select
   your H750 probe serial when prompted, then continue from `main`.

Terminal checks:

```powershell
./tools/build.ps1 -Preset win32-debug -Action Test
./tools/build.ps1 -Preset stm32-debug
```

Both builds run the same application task and UI on the same pinned FreeRTOS
kernel. Platform code supplies the Windows or STM32 display implementation. The
Windows application check verifies task progress, successful initialization, and
a presented frame, without restricting subsequent form colors or border widths.
For the checked completed layout, `starter.exe --interaction-self-test` additionally
injects two mouse clicks and checks that the native frame changes and returns to
its initial pixels. This coordinate-based check is specific to this lesson's layout.
The bonus implementation also has `starter.exe --button-self-test` for
the photo hotspot, hold/release, cancellation, counter repaint and transformed
LCD clicks. Run it against this completed layout; it is not a generic test for
arbitrary developer-edited forms.

## Compare with your work

The display was entered using **Custom display**, not selected from the catalog.
Its application-owned ID is `litheview.project.first-display.surface`.
Read `Tasks/UI/profiles/display-surface.json`: 480 Ã— 272 rectangle, active area
95.04 Ã— 53.86 mm, RGB565 framebuffer, RGB888 parallel panel interface, full safe
area, and Rocktech RK043FN48H-CT672B provenance. `logical-input.json` declares
pointer input, with no encoder or keypad. The manifest locks the canonical profile hashes.
This is the result of **Configure â†’ Input â†’ Touch â†’ Apply** in
[step 6 of the guided lesson](../stm32h750b-dk-starter-1/README.md#6-add-input-windows-mouse-and-h750-touch).
That step explains the shared device interface, Windows mouse queue and H750 FT5336 adapter.

`Tasks/UI/forms/main.litheview.xml` is the form after setting the blank root's
Background Color, Border Color and Border Width, then adding the button and label. `Tasks/UI/Generated` is the
result of saving and pressing **Generate UI**. Open the form in Designer 0.2.8+
to inspect or edit it. Save and generate again before rebuilding.

The platform and lesson integration sources match the starting example. The
completed example adds UI source/profile/generated files. Both examples use the
same two presets and launchers throughout: Windows and STM32H750. Its STM32 `.ioc` has the completed project
name and the same hardware configuration. Do not regenerate the application-owned
CMake or replace the pinned vendor submodules with copied trees.

The source form and generated component can be compared without copying platform
code between examples. Display geometry, storage and input capabilities should
match the lesson. Profile IDs remain stable when Configure changes capabilities,
so your initial input choice, UI name or provenance can produce different canonical
hashes without changing the resulting behavior.

See [VALIDATION.md](VALIDATION.md) for the actual verification scope. Passing the
Windows test does not by itself establish target memory or timing feasibility.

Generate UI emits a portable component and its ownership marker, not an application or standalone
simulator. The existing CMake presets and launchers remain the only build/debug workflow. Earlier
`simulation.json` exports are accepted during migration; generation verifies their owned files before
retiring unused scaffold. Application-owned handlers are preserved and remain outside generated code.
