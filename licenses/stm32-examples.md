# STM32 example provenance and license boundaries

This inventory covers the existing `stm32h750b-dk-starter-1` and
`stm32h750b-dk-starter-1-complete` applications. Their first-party application code and tooling now carry Paul R.
Decker / GPL-3.0-only SPDX notices; their vendor dependency revisions remain unchanged. Vendor boilerplate is excluded from LiTheView's
default GPL license; the original copyright notices remain in each file.

## External CubeMX installation

Download [STM32CubeMX from STMicroelectronics](https://www.st.com/en/development-tools/stm32cubemx.html) directly from ST and install it under its supplied terms.
LiTheView does not bundle the CubeMX application, installer or template package.
The owner has chosen this external-tool approach; no CubeMX replacement is planned
as part of the starter publication pass. This is separate from the provenance of
the already-generated ST source inventoried below, whose notices remain intact.

Both `.ioc` files identify STM32CubeMX 6.18.1, database DB.6.0.181 and
STM32Cube FW_H7 V1.13.0. The installed Cube package's original terms are retained
verbatim in [STM32CubeH7-Package-license.txt](STM32CubeH7-Package-license.txt).
The archive identity, internal source paths and notice hashes are recorded in
`upstream-files.json`. The package has multiple licenses: its SLA is not a
replacement for the Apache, BSD or MIT licenses of individual components.

| Files relative to either starter | Origin and applicable evidence |
| --- | --- |
| `Core/Src/system_stm32h7xx.c`, `startup_stm32h750xx.s` | ST CMSIS device templates; [Apache-2.0](ST-CMSIS-H7-LICENSE.txt). Preserve ST's per-file copyright and modification notices. |
| `Core/Inc/stm32h7xx_hal_conf.h`, `Core/Src/stm32h7xx_hal_timebase_tim.c` | ST HAL configuration/timebase templates; [BSD-3-Clause](ST-H7-HAL-LICENSE.txt). These terms apply to the HAL-derived portions. |
| `Core/Inc/FreeRTOSConfig.h` | Full MIT grant is already in the file, with Amazon and ST attribution. Retain that complete header; it is separate from the CMSIS adapter's Apache terms. |
| `STM32H750xx_FLASH.ld` | Full ST BSD-3-Clause grant and 2025 copyright already in the file. |
| `Core/Inc/main.h`, `Core/Inc/stm32h7xx_it.h`, `Core/Src/main.c`, `Core/Src/stm32h7xx_it.c`, `Core/Src/stm32h7xx_hal_msp.c`, `Core/Src/freertos.c` | CubeMX-generated application scaffolding. Its header refers to a component-root LICENSE, absent in these examples. CubeMX's installed `db/templates/BSD_License.ftl` emits that header, but contains no complete BSD grant. This observation alone is not a replacement license. |
| `Core/Src/syscalls.c`, `Core/Src/sysmem.c` | CubeMX/CubeIDE bare-project templates, including newer Picolibc additions. The installed template plugin's `about.html` carries SLA0048 Rev5. Older CubeH7 basic examples contain related BSD-covered files, but they differ from these copies; do not automatically apply their license to the newer additions. |

The CubeH7 basic-examples [component notice](STM32CubeH7-Examples-LICENSE.txt)
and package bill of materials identify BSD-3-Clause for basic examples. They do
not establish that every file emitted by a different CubeMX version came from
that component. Neither an `AS-IS` sentence nor a missing LICENSE file grants
permission to relicense ST code as GPL.

The submodule license inventory and exact revisions are in
[THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md). Full ST HAL, ST CMSIS, ST
FreeRTOS and FreeRTOS Kernel notices are included alongside this document.
Arm's CMSIS-RTOS2 adapter retains its Apache-2.0 headers and the full
[Apache license](Apache-2.0.txt).

Before separately distributing a linked starter firmware, establish the applicable
grant for the unresolved CubeMX scaffolding and newer system-call templates, or
replace those pieces with independently authored or verified permissive equivalents.
Do not include SLA-only code in a GPL-covered combined firmware by assumption.
The starter publication pass retains those vendor headers unchanged and does not
assert a grant that has not been established. The source-only designer VSIX
contains none of these STM32 files, and Core neither builds nor installs them.

Each starter includes a `THIRD_PARTY_NOTICES.md` pointing to these terms and the
licenses in its initialized submodules. First-party notices cover the application,
platform adapters, lesson code and build scripts; they do not relicense `Core/`,
the ST startup assembly or the linker script. Generated UI files inherit the
application's first-party license unless they identify a separate asset license.
The board-front photograph is Paul R. Decker's own work, per his provenance statement.

The remaining provenance limitation concerns the CubeMX scaffolding listed above:
obtain a component-specific permissive grant or replace it with code of established
provenance before distributing GPL-linked starter firmware. The tool's general
[SLA0048](https://www.st.com/resource/en/license/SLA0048_STM32CubeMX-Lin.pdf) preserves
separately licensed open-source portions; it does not itself establish which
unlabelled templates are BSD-covered. This records a limitation of the license evidence for linked-firmware distribution;
adding the tool download link does not establish a new grant for those source files.
