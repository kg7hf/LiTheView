# Cast audit

LitheView treats every explicit cast as a boundary that requires evidence. Internal type mismatches
must be corrected at their source rather than hidden by successive conversions between layers.

The initial audit found 255 explicit casts. The current enforced footprint is 18 reviewed cast-bearing
lines and has no application-layer downcasts, C-style casts, cast-to-void result suppression, or
repeated pointer/length conversion chains. Fixed-capacity heterogeneous views use a private
discriminated union rather than raw byte storage, XML fields parse directly into their destination
types, and signed geometry crosses into unsigned storage indexing in one checked helper.

## Approved boundaries

The remaining cast-bearing lines are restricted to these reviewed categories:

| Boundary | Reason |
|---|---|
| Packed color and UTF-8 representations | A masked 32-bit channel narrows to one byte, and `char` storage preserves UTF-8 byte representations. |
| Signed geometry to `std::size_t` | Geometry permits negative off-screen coordinates; memory indexing does not. Negative values fail closed before the audited conversion. |
| Wide arithmetic to signed geometry | Products and ratios are evaluated in 64 bits, range checked, and saturated before returning to the 32-bit GUI coordinate domain. |
| Parsed external numbers | XML values and bounded indexes are range checked before conversion to their declared field type. |
| Legacy signed XML values | Old LCDView color syntax requires preserving a defined 32-bit two's-complement representation. |
| Win32 bitmap descriptor | `StretchDIBits` requires a `BITMAPINFO*` for a verified standard-layout header-plus-three-mask object. |
| Win32 callback context | `LPARAM`, `LONG_PTR`, `void*`, and `GWLP_USERDATA` are OS-defined storage forms for caller-owned pointers. |
| Win32 minimum window size | `WM_GETMINMAXINFO` supplies a borrowed `MINMAXINFO*` through `LPARAM`; the simulator reads it only in that message and retains no pointer. |
| STM32 LTDC framebuffer | The BSP exposes a fixed physical address rather than a typed pixel span. |
| CMSIS cache maintenance | CMSIS requires a `uint32_t*` address and signed byte count after explicit range validation. |
| STM32 touch coordinates | The BSP publishes unsigned coordinates; both axes are range-checked before entering signed GUI geometry. |

Every approved line is tagged `LITHEVIEW_CAST_BOUNDARY` with its local justification. The
`litheview_cast_audit` build target fails when an untagged explicit cast appears or the reviewed cast
footprint grows. GCC and Clang builds also enable `-Wold-style-cast`; the cast-focused `clang-tidy`
profile checks C-style, downcast, reinterpret, and const-cast rules.

## Review rule

The H750 starter and completed starter have separate ceilings of 9 and 10 cast-bearing lines.
Their vendor/library submodules and ignored build products are excluded; first-party application
sources remain audited. Core, preview and generated simulator boundaries have their own ceilings.

Do not add a tag merely to silence the audit. First inspect the producer, every intermediate layer,
and the final consumer. Change the shared type when the value has one logical domain. A cast is only
eligible when an external ABI or a deliberate serialized/memory representation forces two domains
to meet, and its range, alignment, lifetime, and ownership assumptions are locally proven.
