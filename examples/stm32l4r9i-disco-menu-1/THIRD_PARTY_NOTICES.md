# Ownership and third-party notices

Application code: Copyright 2026 Paul R. Decker, GPL-3.0-only; see [LICENSE](LICENSE).
Vendor files retain their original notices and license terms.

Pinned upstream submodules contain Arm CMSIS 6 and ST CMSIS L4 (Apache-2.0), ST HAL,
Discovery BSP and components (BSD-3-Clause), ST FreeRTOS and FreeRTOS Kernel (MIT),
and the CMSIS-RTOS2 wrapper (Apache-2.0). Each retains its upstream license.
The ST board-driver revisions match STM32CubeL4 v1.18.2.

`Devices/display_driver/display_hal.c`, its interface and GFXMMU lookup table derive
from ST's DSI example under BSD-3-Clause. Full license and source provenance are in
[the driver directory](Devices/display_driver/PROVENANCE.md).
`Core/Src/system_stm32l4xx.c` and `startup_stm32l4r9xx.s` retain ST CMSIS device notices;
see the license in `Drivers/CMSIS/Device/ST/STM32L4xx`.

`Utilities/Fonts/fonts.h` is ST's unmodified declaration header, required transitively
by the Discovery BSP, from STM32CubeL4 v1.18.2. Its BSD-3-Clause license is beside it.
No ST font tables, BSP drawing implementation or STemWin code is compiled.
The text uses LiTheView's original compact 5x7 font.

The externally installed MinGW-w64/GCC runtime's provenance and upstream links are in
[the library inventory](Middlewares/Third_Party/LiTheView/THIRD_PARTY_NOTICES.md).
Compiler installations and application binaries are not included in this source example.
Download [STM32CubeMX](https://www.st.com/en/development-tools/stm32cubemx.html) from ST
if you need that separate configuration tool.

The example owner supplied `board-front.png` for the simulator front view. It is copied
verbatim from the supplied `STM32L4R9AI-DISCO.png`; no image processing was performed.
The visible MB1314 B-01 label is part of the photo and does not change the revision-C
board configuration. ST logos and product markings belong to their respective owners.
