# MB1314 display adapter

The public C++ interface is application-owned. It borrows RGB888 tile bytes
in R,G,B order and never retains a source span. The private C translation unit
adapts ST's board initialization and uses the HAL's bus-address interface.
No STemWin library, font implementation, or BSP drawing implementation is linked.

Reference package: STM32CubeL4 1.18.2, 14 November 2025, available from
https://github.com/STMicroelectronics/STM32CubeL4/tree/v1.18.2.
The package license identifies the basic examples and Discovery BSP as
BSD-3-Clause. This directory retains that license and source attribution.

| Source within package | SHA-256 |
| --- | --- |
| `Projects/32L4R9IDISCOVERY/Examples/DSI/DSI_CmdMode_SingleBuffer/Src/main.c` | `eb5d556679af8a48614130480475b73df1911ba89b235def4e7677b3f43084fd` |
| `Projects/32L4R9IDISCOVERY/Examples/DSI/DSI_CmdMode_SingleBuffer/Src/stm32l4xx_hal_msp.c` | `2edc35b8684be4e0fb03ef8240ffab8ec6e56a8fc20fee88785c2855ceeaa5a3` |
| `Projects/32L4R9IDISCOVERY/Examples/DSI/DSI_CmdMode_SingleBuffer/Inc/gfxmmu_lut_390x390_24bpp.h` | `b60c296df27c91e910ab4e1404e620e7cda57e20b8e3987ea062bd257dd42a2f` |
| `Drivers/BSP/STM32L4R9I-Discovery/stm32l4r9i_discovery_lcd.c` (revision-specific power sequence) | `666c628c38954b8907a2cb8d4777564ed278916e11edc3cdee9f0d8d3166b868` |

The LUT is copied without modification. `configure_display` is extracted from
the example's `LCD_Config`: register values, panel commands, command ordering,
and delays are preserved; all configuration objects are initialized, every
command return is checked, and the stale 768-pixel RGB888 stride comment is
corrected. Clock setup returns errors instead of entering infinite loops.
Display initialization verifies SYSCLK120/MSI4 and owns the display peripheral
clocks, DSI/LTDC interrupt handlers, and refresh/error callbacks.

The 365040-byte physical framebuffer is aligned to 16 bytes in
`.display_framebuffer`. The application linker must place this whole section
in SRAM3, beginning at 0x20040000, within its 384 KiB. Storage is NOLOAD and is
explicitly cleared before use. The GFXMMU virtual address is 0x30000000 and its
RGB888 stride is 3072 bytes; physical storage is not a flat 390x390 image.

DMA2D preserves the example's input red/blue swap and regular output ordering.
Copy destination is virtual base + y*3072 + x*3. Input and output line offsets
are in pixels. Tile copy waits at most 100 HAL milliseconds and resets DMA2D
on failure before returning ownership to the caller. Refresh follows the
standalone example: panel tear-off, DSI-link TE source configured, explicit
HAL_DSI_Refresh, completion by DSI IRQ. Foreground polling enforces a 250-ms
refresh deadline. HAL timeouts require the application's live 1-ms SysTick.

One foreground execution context owns the C++ state. ISR-to-foreground status
uses aligned 32-bit volatile flags at this platform boundary. Errors are sticky;
retry after a fault requires an MCU reset. Invalid tile/state requests do not
mutate the framebuffer. Aborting a partially copied frame does not roll back
its pixels, so the next frame must redraw that region before presentation.

The explicit board-revision argument selects ST's A/B versus C power sequence.
Unlike the reference's fresh-MFX-state assumption, reset is explicitly driven
low and checked before changing either display supply. It remains low through
the supply sequence and 15-ms delay, making MCU-only restart deterministic.
Do not select a revision until the physical main-board marking is known.
MFX initialization checks the device ID and output changes use pin read-back
through `MFX_IO_ReadMultiple`, checking its HAL status before trusting a value.
The upstream BSP's void write/configuration callbacks do not expose individual
I2C write transaction errors; read-back is an additional check, not electrical proof.
This adapter assumes PSRAM is unused, since its supply is shared with the LCD.

No physical operation or hardware validation is implied by building this code.
