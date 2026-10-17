# Target planning and regression fixtures

These profiles exercise target validation separately from display compatibility. They do not
select a board for the designer or qualify a new application. Application owners supply their own
linker regions, storage placement, stack/workspace measurements and transfer evidence.

The `synthetic-m7-*-full-frame` pair is an invented full-frame planning fixture. Its declared
ceilings are 128 KiB internal flash, 512 KiB internal RAM, 16 MiB external/framebuffer storage and
a 4 KiB stack frame. Every ceiling is an advisory estimate, not a linker or hardware measurement.
The RGB565 and XRGB8888 versions deliberately share limits so tests can check inheritance and
format-dependent costs. These files replace retired application-specific planning profiles.

The two `stm32u545-*-tiled` profiles describe the live displayless regression probes under
`tests/rendering/stm32u545re-q`. The standard compact/alpha1 archive ceiling is 85,200 bytes and the
round ceiling is 68,600 bytes. Both include the explicit 3,000-byte control-event and 5,600-byte
drop-down allowances; standard also includes the 1,600-byte text-event allowance in the root
CMake gate. Their strip, static-object, data/BSS and stack-frame limits refer to current
source assertions and gates. Neither profile establishes physical panel behavior.

Profile hashes include provenance and ceilings. Intentional changes update the canonical hashes
and any base-profile lock together; managed tests check the identities and source-path existence.
Old application pins and historical measurements are not substitutes for fresh target evidence.
