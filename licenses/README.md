# License sources and provenance

Files in this directory reproduce upstream terms; their presence does not apply every license to
LiTheView. See [the component inventory](../THIRD_PARTY_NOTICES.md) for their scope.

`upstream-files.json` records the original URL and SHA-256 of each notice. The locally installed
CubeH7 package notices additionally record their downloaded archive hash and internal paths;
their URL identifies ST's package download page. License files
are copied verbatim. The versions below identify the notice sources; the historical font manifests
record TTF hashes but not the precise upstream font release, so no font-version claim is inferred.

| Files | Upstream reference |
| --- | --- |
| `DejaVu-LICENSE.txt` | DejaVu `version_2_37`, `LICENSE` |
| `GCC-COPYING3.txt`, `GCC-RUNTIME-EXCEPTION.txt` | GCC `releases/gcc-14.2.0`, `COPYING3` and `COPYING.RUNTIME` |
| `MinGW-w64-COPYING.txt`, `MinGW-w64-AUTHORS.txt`, `Winpthreads-COPYING.txt` | MinGW-w64 `v12.0.0` |
| `Apache-2.0.txt` | Apache Software Foundation's complete Apache License 2.0 text; included for Apache-licensed example dependencies, not as an implicit license selection for LiTheView |
| `ST-H7-HAL-LICENSE.txt`, `ST-CMSIS-H7-LICENSE.txt`, `ST-FreeRTOS-LICENSE.txt`, `FreeRTOS-Kernel-LICENSE.txt` | Exact starter dependency revisions in `THIRD_PARTY_NOTICES.md` |
| `STM32CubeH7-Package-license.txt`, `STM32CubeH7-Examples-LICENSE.txt` | Original notices from `stm32cube_fw_h7_v1130.zip`; see the file-level review in `stm32-examples.md` |

## Project-owned material

Copyright (c) 2012-2026 Paul R. Decker. Project-owned material is offered under GPL-3.0-only;
the complete text is in the root [LICENSE](../LICENSE). This includes the original implementation
and the 2012 5 by 7 glyph table in `resources/fonts/font5x7.cpp`. Those glyphs are separate from DejaVu.

Paul R. Decker identifies the starters' `board-front.png` as his photograph taken with his phone.
It is included under the project license. This is the owner's provenance statement;
no vendor license or endorsement is inferred from the
board markings. The image files and local starter work are unchanged.

The retained horse artwork has an image-generation history and prompts in
`tests/rendering/assets/animations/README.md`, with subsequent project edits. We preserve that record;
the quantity of modification is not itself proof that any third-party rights have disappeared.
No new origin story or exclusive copyright claim is asserted. The project's rights in that artwork
are offered under GPL-3.0-only, and the fixture records that license without changing the pixels.
The small status-icon fixture retains its previously declared CC0-1.0 dedication.

## Packaging and release maintenance

The CMake install includes LICENSE, NOTICE, THIRD_PARTY_NOTICES and this directory under `share/LitheView`.
The VSIX includes the same materials under `extension/`; the source manifest and notice hashes
travel with the runtime DLLs. The package sidecar records the included entry hashes.

For another compiler, supply `-RuntimeManifest path/to/windows-runtime.json`. Record each DLL's
SHA-256, upstream version, complete notice files and source locations. The manifest describes the
packaged dependencies; it is not a legal certification.

Compiler source remains upstream. LiTheView records the [WinLibs release](https://github.com/brechtsanders/winlibs_mingw/releases/tag/14.2.0posix-19.1.1-12.0.0-ucrt-r2),
[GCC sources](https://github.com/gcc-mirror/gcc/tree/releases/gcc-14.2.0),
[MinGW-w64 sources](https://github.com/mingw-w64/mingw-w64/tree/v12.0.0) and
[WinLibs recipes](https://github.com/brechtsanders/winlibs_recipes), without mirroring or rebuilding
them. The packager retains their notices and checks DLL identity; it does not require a locally
hosted compiler source archive. `-ForRedistribution` records the intended use in the package sidecar
and checks the first-party license selection. It does not upload anything or certify compliance.

Using an installed compiler does not itself redistribute that compiler. Including runtime DLLs in
a VSIX does redistribute those DLLs, subject to their terms. GPL section 6(d) permits corresponding
source on a third-party server with clear directions and equivalent access; it does not require
LiTheView to host a copy. The upstream links document provenance, not a guarantee of complete
corresponding source for every binary release.

`-SourceOnly` remains an optional platform-neutral VSIX containing editable JavaScript, media and
license texts without runtime binaries. It needs no prepared binaries or installed compiler to
package. Developers prepare the service and preview worker in their application-owned LiTheView
checkout before using it, as described in `tools/vscode/README.md`. Application builds and debugging
remain in CMake and VS Code.
