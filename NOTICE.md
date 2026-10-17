# LiTheView ownership and attribution

LitheView is a cleanly bounded extraction and modernization based on the architecture and behavior
of the LiTheView `Frameworks/LCDViewGUI`, `Feature/Resources`, and `HAL/LCDController` implementation.

Copyright (c) 2012-2026 Paul R. Decker.

LiTheView-owned code, documentation and artwork are licensed under the GNU General Public License,
version 3 only (SPDX: GPL-3.0-only), except where an explicit separate license is recorded.
The complete terms are in LICENSE. LiTheView is provided without warranty, including without any
implied warranty of merchantability or fitness for a particular purpose; see LICENSE for details.

Paul R. Decker is the author and owner of the original implementation and created the compact
5 by 7 glyphs in 2012. Historical notices used his identifier `prd`; current attribution uses his name.

STM32 platform dependencies and FreeRTOS are separately pinned Git submodules owned by the consuming
applications under `examples/`, and remain subject to their respective licenses. The portable library
has no vendor dependency. The obsolete showcase's root FreeRTOS and STM32CubeH7 submodules were removed.
Vendor code in the current application examples remains outside first-party formatting policy.
Copied STM32Cube/CubeMX boilerplate also remains vendor-owned and is excluded from the default
LiTheView license. See `licenses/stm32-examples.md` for the file-level provenance and unresolved
template grants; a missing vendor LICENSE is not replaced by this repository's GPL license.

The optional generated Sans coverage tables are derived from DejaVu Sans and DejaVu Sans Bold;
they are not the owner's original compact font. Their upstream notices are reproduced in
`licenses/DejaVu-LICENSE.txt`. Source-font hashes and selected repertoires remain in `resources/fonts/`.

Third-party materials retain their own terms. See `THIRD_PARTY_NOTICES.md` and `licenses/README.md`
for the component inventory, asset provenance, license sources and remaining release requirements.
The LiTheView license does not replace those terms. The GCC Runtime Library Exception applies only
to the separately licensed GCC runtime components identified in the inventory.
