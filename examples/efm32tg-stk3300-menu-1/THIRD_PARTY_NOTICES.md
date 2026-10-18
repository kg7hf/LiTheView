# Notices

First-party application, glyph patterns and drawing code:
Copyright 2026 Paul R. Decker, GPL-3.0-only. See the LiTheView repository LICENSE.

The electrical mapping is transcribed from Energy Micro's original
EFM32TG-STK3300 user manual, t0011_1.00, page 26. The original document and its
notices remain the vendor's work. No vendor font table, logo, photograph
or manual PDF is copied into this example. The simulated board is an authored
schematic presentation, not a photograph or pixel-exact model of the glass.

Hardware builds use the unmodified [Silicon Labs Gecko SDK](https://github.com/SiliconLabs/gecko_sdk)
submodule at v4.4.6, commit `59d2160fe448b13730a91c0f019a8b4c53c43eb2`, under
`Drivers/GeckoSDK`. EMLIB, device support and STK3300 configuration retain their
Silicon Labs notices (including Zlib-licensed files). ARM CMSIS headers retain
their Apache-2.0 notices. Consult each upstream file and the SDK's license files;
the repository's GPL notice does not relabel this dependency. The example uses
its own minimal startup and linker script, not the SDK's GCC startup assembly.

Build tools and Windows system components retain their own licenses. For MinGW
runtime information see the repository's `licenses/GCC-RUNTIME-EXCEPTION.txt`,
`licenses/GCC-COPYING3.txt` and `licenses/MinGW-w64-COPYING.txt`. Binary distribution
must be assessed against the actual toolchain used, separately from this source
example. The static-link option does not remove third-party notice obligations.
