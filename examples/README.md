# Application starters

These examples teach one developer workflow: the application owns its hardware and RTOS,
LiTheView supplies portable controls and rendering, and the designer generates UI components.
Windows and embedded builds use the same application code and platform interfaces.

| Application | Purpose |
| --- | --- |
| [stm32h750b-dk-starter-1](stm32h750b-dk-starter-1/README.md) | Start with a blank application, define a custom display, and generate the first panel |
| [stm32h750b-dk-starter-1-complete](stm32h750b-dk-starter-1-complete/README.md) | Expected completed result using the same two platform presets and launchers |
| [stm32h750b-dk-menu-1-complete](stm32h750b-dk-menu-1-complete/README.md) | Completed menu with volume, playback and a scrolling Load list |
| [stm32l4r9i-disco-menu-1](stm32l4r9i-disco-menu-1/README.md) | Guided round-display menu with Windows and STM32 builds |
| [efm32tg-stk3300-menu-1](efm32tg-stk3300-menu-1/README.md) | Start with blank glass, import its definition and bind presentation values in the plug-in |
| [efm32tg-stk3300-menu-1-complete](efm32tg-stk3300-menu-1-complete/README.md) | Finished segmented menu and volume ring, Windows and bare-metal EFM32 |

Continue the starter through controls, named handlers and backend interaction. Future starters
will cover other displays, input devices, fonts, native assets and resource constraints. Each
lesson should have a guided starting point and a completed result. Adding a feature does not
create a new build configuration in the developer's application.

Vendor SDKs and, where used, FreeRTOS are application-owned submodules inside the examples.
LiTheView Core has no OS or MCU dependency. Initialize the chosen application's dependencies
with its setup scripts; do not recursively initialize every submodule in the library checkout.

Each application README lists its own tested hardware and prerequisites. Internal rendering fixtures live
under [tests/rendering](../tests/rendering/README.md) and are not application templates.
