# Application starters

These examples teach one developer workflow: the application owns its hardware and RTOS,
LiTheView supplies portable controls and rendering, and the designer generates UI components.
Windows and embedded builds use the same application code and platform interfaces.

| Application | Purpose |
| --- | --- |
| [stm32h750b-dk-starter-1](stm32h750b-dk-starter-1/README.md) | Start with a blank application, define a custom display, and generate the first panel |
| [stm32h750b-dk-starter-1-complete](stm32h750b-dk-starter-1-complete/README.md) | Expected completed result using the same two platform presets and launchers |

Continue the starter through controls, named handlers and backend interaction. Future starters
will cover other displays, input devices, fonts, native assets and resource constraints. Each
lesson should have a guided starting point and a completed result. Adding a feature does not
create a new build configuration in the developer's application.

CMSIS, STM32 drivers and FreeRTOS are application-owned submodules inside each application.
LiTheView Core has no OS or MCU dependency. Initialize the chosen application's dependencies
with its setup scripts; do not recursively initialize every submodule in the library checkout.

The starter pair is the supported hardware application path. Internal rendering fixtures live
under [tests/rendering](../tests/rendering/README.md) and are not application templates.
