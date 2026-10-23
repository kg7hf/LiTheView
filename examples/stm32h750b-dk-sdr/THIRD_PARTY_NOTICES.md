# Starter ownership and third-party notices

Copyright (c) 2026 Paul R. Decker. Application-owned code use GPL-3.0-only; see [the repository license](../../LICENSE).
Source files carrying vendor notices retain their original ownership and terms.

The [repository component inventory](../../THIRD_PARTY_NOTICES.md) and
[file-level STM32 provenance](../../licenses/stm32-examples.md) apply to this example.
The latter records the unresolved CubeMX template grant; including these notices
does not resolve it or authorize distribution of GPL-linked firmware by assumption.

The screen links pre-rasterized DejaVu Sans font data from the repository's
`LitheView::GeneratedFonts` resource. Keep the repository's DejaVu attribution and
the full [DejaVu license](../../licenses/DejaVu-LICENSE.txt) with redistributed
font tables; the original TTF files are not included.

Download [STM32CubeMX from STMicroelectronics](https://www.st.com/en/development-tools/stm32cubemx.html) directly from ST.
The tool is an external developer prerequisite and is not redistributed by LiTheView.
Existing ST-generated source retains its vendor notices and the provenance recorded above.

Initialize the sibling H750 starter's pinned submodules to obtain their complete
source and per-file notices until this new example receives its own dependency
pins: Arm CMSIS 6 and ST CMSIS H7 (Apache-2.0), ST HAL (BSD-3-Clause), ST FreeRTOS
and FreeRTOS Kernel (MIT), with the CMSIS-RTOS2 adapter's separate Apache-2.0 terms.
Full upstream notices are also retained under the repository's `licenses/` folder.
The Windows build uses the compiler's MinGW-w64/GCC runtime components; their
attribution and source links are in the repository inventory. Installed build and
debug tools are not copied into this example or its source distribution.

When extracting this example into another repository, retain this notice, the
repository LICENSE, relevant full upstream license texts and component provenance;
relative links into the parent repository alone are insufficient for redistribution.

The board-front photograph is reused unchanged from the H750 completed starter.
Its existing notice attributes it to Paul R. Decker under GPL-3.0-only. ST board
markings identify the photographed hardware; no ST endorsement is implied.
