# Third-party notices

This inventory describes optional resources, development-tool binaries and example dependencies.
It does not make them dependencies of `LitheView::Core`. LiTheView-owned material uses GPL-3.0-only,
as recorded in [NOTICE.md](NOTICE.md) and [LICENSE](LICENSE); the separate terms below still apply.

## Optional generated fonts

`resources/fonts/generated_sans_regular.cpp`, `generated_sans_bold.cpp`, and the
`generated_sans_20_*` and `generated_sans_28_*` files contain coverage and metrics
generated from DejaVu Sans and DejaVu Sans Bold. Copyright (c) 2003 Bitstream, Inc.; DejaVu changes
are in the public domain; imported Arev glyphs carry Copyright (c) 2006 Tavmjong Bah. The complete
upstream notice, including its additional font notices, is in [DejaVu-LICENSE.txt](licenses/DejaVu-LICENSE.txt).
These terms travel with the generated font resource package. Converting fonts into C++ tables does
not assign them LiTheView's own license. The original TTF files are not bundled.

## Windows designer native runtime

The Windows bundled VSIX includes a native preview worker and these separately licensed
runtime libraries. The optional `-SourceOnly` VSIX contains readable extension source and
notices, with no managed/native binaries or compiler runtimes. It uses the developer's locally
prepared tools through the extension's existing tool-resolution path.

| Binary/component | Terms and notices |
| --- | --- |
| `libgcc_s_seh-1.dll`, `libstdc++-6.dll` | [GNU GPL version 3](licenses/GCC-COPYING3.txt), with the [GCC Runtime Library Exception 3.1](licenses/GCC-RUNTIME-EXCEPTION.txt); Copyright Free Software Foundation, Inc. See the exact upstream sources listed in the runtime manifest for individual notices. |
| `libwinpthread-1.dll` | [Winpthreads notices](licenses/Winpthreads-COPYING.txt): mingw-w64 project and Lockless Inc.; MIT and BSD-style terms. |
| MinGW-w64 runtime/startup support linked into native binaries | [MinGW-w64 notice](licenses/MinGW-w64-COPYING.txt) and [authors](licenses/MinGW-w64-AUTHORS.txt); file-specific exceptions remain applicable. |

[The runtime manifest](licenses/windows-runtime.json) identifies the checked DLL hashes, upstream
versions, binary release and source locations. The packager checks the actual DLLs against that
manifest and includes it in the VSIX. A different compiler distribution needs its own reviewed
manifest; matching a version string alone is insufficient.

These DLLs come from the [WinLibs GCC 14.2.0 / MinGW-w64 12.0.0 release](https://github.com/brechtsanders/winlibs_mingw/releases/tag/14.2.0posix-19.1.1-12.0.0-ucrt-r2).
Upstream source is maintained by [GCC](https://github.com/gcc-mirror/gcc/tree/releases/gcc-14.2.0)
and [MinGW-w64](https://github.com/mingw-w64/mingw-w64/tree/v12.0.0); WinLibs publishes its
[build recipes](https://github.com/brechtsanders/winlibs_recipes). LiTheView does not mirror or
rebuild those source trees. The manifest retains versioned source links and the provenance limits
of the recorded recipe snapshot.

The GCC runtime exception permits qualifying independent applications under their own terms;
the runtime libraries retain their own licensing obligations. [GPL section 6(d)](licenses/GCC-COPYING3.txt)
allows corresponding source on a third-party server with clear directions and equivalent access.
The upstream links are attribution and provenance, not a certification of every binary release.

The managed service is framework-dependent: this package does not redistribute the .NET runtime.
Node.js, VS Code, CMake, compilers, Windows SDKs and STM32 tools installed by the developer retain
their own terms and are not redistributed as part of Core.

## Application example submodules

Both H750 starters use the following application-owned submodules. Their upstream licenses and
per-file notices remain in those repositories. These entries do not relicense vendor code.

| Component | Pinned revision | License |
| --- | --- | --- |
| Arm CMSIS 6 | `45dab712ad84f8cbbf2b7bfc089c19088507df6f` | Apache-2.0; preserve per-file notices |
| ST CMSIS device H7 | `8f922cdc7cc6de2344e75ddd657889f4ff761790` | Apache-2.0; preserve per-file notices |
| ST H7 HAL driver | `a1996eed9172b59887bafaaa0ea1816ea14d48b5` | BSD-3-Clause |
| ST FreeRTOS middleware | `d8c632a9478f66d41365680f8e3fd7c8e640747b` | MIT at repository root; the CMSIS-RTOS2 adapter has Apache-2.0 notices |
| FreeRTOS Kernel | `0adc196d4bd52a2d91102b525b0aafc1e14a2386` | MIT; retain Amazon and other per-file notices |

Their source URLs are recorded in `.gitmodules`. Source distributions must retain the submodule
license files; firmware distributions must carry the notices required by the components actually
linked. Merely listing the dependency here does not replace its full upstream terms.

The [file-level STM32 inventory](licenses/stm32-examples.md) distinguishes verified CMSIS/HAL,
FreeRTOS and linker-script terms from unresolved CubeMX template grants. Complete Cube package
and component notices are retained with their source hashes. No vendor file is relabelled GPL,
and application-owned starter code has its own first-party notices. Resolving or replacing the remaining application boilerplate
is required before distributing linked starter firmware; it does not block publishing Core and
the source-only designer extension.
