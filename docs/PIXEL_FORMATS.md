# Pixel formats and display transfers

LitheView keeps logical color as four 8-bit RGBA channels until a painter writes a destination pixel.
Each product chooses its destination format at build time. Styles, views, XML, and application models
continue to use logical colors. `LITHEVIEW_DESTINATION_FORMAT` selects one writer; `ALL` retains every
writer for compatibility and conformance builds. The generated `lithe/render_config.hpp` travels with
the installed Core archive. Source bitmap readers remain available independently of the destination.

## Surface formats

`PixelSurface<Format>` binds caller-owned storage to a compile-time format. The public aliases are
`Rgb565Surface`, `Rgb888Surface`, `Xrgb8888Surface`, and `Argb8888Surface`. A `Display` erases that type
to the bounded `Surface` view required by the retained view API. The painter selects a specialized
rasterizer once per drawing command; there is no virtual call or runtime format branch per pixel.

Multibyte values use little-endian byte order:

| Format | Bytes in memory | Alpha behavior |
| --- | --- | --- |
| RGB565 | low, high for `RRRRRGGGGGGBBBBB` | destination is opaque |
| RGB888 | red, green, blue | destination is opaque |
| XRGB8888 | blue, green, red, `0xff` | destination is opaque |
| ARGB8888 | blue, green, red, alpha | destination alpha is preserved |
| R2 packed 2x2 | one byte per four logical red pixels | destination is opaque |
| RRRR2222 | one byte per logical red cell with four emitters | destination is opaque |
| RYRY2222 | one byte per logical red/yellow cell with four emitters | destination is opaque |

`PixelFormatTraits<Format>` defines block dimensions, bytes per block, alpha capability, and deterministic
encode/decode operations. RGB565 remains available through the historical `Pixel` alias as well as the
explicit `Rgb565Pixel` name.

`PixelSurface` is restricted to 1 by 1 storage blocks. Use a bounded byte-span `Surface` for packed R2.
`bytesPerPixel()` is an integer-byte compatibility helper and returns zero for packed R2; it is not a
storage-sizing API. `storageBlock()` describes all supported blocks, and `storageLayout()` performs the
authoritative checked sizing for surfaces, read-only views, and tiles.

## Checked storage layout

```cpp
constexpr auto layout = lithe::storageLayout(
    lithe::PixelFormat::r2Packed2x2, {512, 384}, 256U);
static_assert(layout && layout->rowBytes == 256U && layout->rowCount == 192U);
static_assert(layout->usedBytes == 49152U);
```

Stride is bytes per **storage-block row**; dimensions and origins remain logical pixels. The result
is `std::expected<StorageLayout, StorageError>` and distinguishes unsupported format, invalid extent,
block misalignment, invalid stride, coordinate/size overflow, and insufficient storage at binding.
Check the result before dereferencing it. No target profile or allocation is needed.

`Surface::layout()` and `PixelBufferView::layout()` additionally check their borrowed spans. Existing
`valid()` and `usedBytes()` calls wrap the checked result. The required span is
`(rowCount - 1) * strideBytes + rowBytes`: intermediate padding is included, but no final-row trailing
padding is required. Surface strides are explicit and nonzero; the source-bitmap zero-stride convention
below is unchanged. Logical exclusive edges must also fit the coordinate type.

Structural validity does not imply a writer is compiled in. `Painter::valid()` checks both. Unsupported
application rendering fails before presentation or consuming scene damage, and `TiledRenderer` returns
`unsupportedFormat` before calling its sink. Changing the clip cannot reactivate an invalid painter.

## Native R2 packed pixels

`PixelFormat::r2Packed2x2` stores a complete 2 by 2 logical block in one byte. Top-left uses bits 3:2,
top-right 7:6, bottom-left 1:0, and bottom-right 5:4. Levels 0, 1, 2, 3 in that order encode as `0x72`.
Each write preserves the other three fields. Surface and tile extents/origins must align to complete
blocks; drawing clips, lines, glyphs, and bitmap coverage can address individual pixels.

Red is rounded to the nearest of four levels using `(red * 3 + 127) / 255`; green and blue do not
contribute. Source-over first decodes the stored level to red 0, 85, 170, or 255, blends using the existing
integer coverage arithmetic, then quantizes again. Alpha is not stored. Existing primitives, compact
text, antialiased glyphs, and source bitmaps render directly into this storage without an RGB workspace.

## Native cell formats

`rrrr2222` and `ryry2222` use 1 by 1 logical storage blocks of one byte. A logical cell contains
four emitter fields in the same TL/TR/BL/BR bit positions as R2. RRRR writes repeat the quantized red
level in all four fields: 0, 1, 2, 3 produce `00`, `55`, `aa`, `ff`. RYRY assigns yellow to TL/BR and
red to TR/BL. It derives yellow from `min(red, green)` and red from `red - yellow`, then quantizes each.
Full red is `c3`; full yellow is `3c`; red level 1 and yellow level 2 produce `69`.

`PixelFormatTraits<Format>::readEmitter()` returns one opaque emitter color. Coverage blends each
stored emitter independently before quantizing; a whole-cell clamped RGB round trip would lose levels.
Alpha zero preserves every bit. For an opaque background emitter level `L`, the exact operation is
`blended = (foreground * alpha + 85 * L * (255 - alpha)) / 255`, followed by
`level = (blended * 3 + 127) / 255`. Alpha 255 takes the opaque write path. These formats store no alpha.

## Source-over and bitmap formats

Logical colors use straight, not premultiplied, alpha. Partial coverage uses deterministic integer
source-over composition. Opaque destinations read back with alpha 255; ARGB8888 destinations preserve
the composed alpha channel.

Bitmap resources support:

- opaque RGB565, RGB888, and XRGB8888 pixels;
- straight-alpha ARGB8888 pixels;
- opaque native `r2Packed2x2`, with even logical dimensions and byte strides per 2-row storage block;
- `indexed4`, with the high nibble representing the left pixel, the low nibble representing the right
  pixel, and a borrowed palette of 1 through 16 `Color` entries;
- `alpha1`, with the most significant bit representing the leftmost pixel and `Bitmap::tint` supplying
  its logical color; and
- `alpha8`, where each byte scales the tint alpha from zero to full coverage.

Bitmap rows may have an explicit byte stride. A zero stride selects the tightly packed minimum. Invalid
dimensions, strides, multiplication overflow, or insufficient spans fail closed without drawing.
An indexed value outside its palette span also fails closed as transparent.

## Full-frame and tiled rendering

`Application{viewport}` constructs scene/input state without a display. A legacy bound display or
`application.render(display)` supplies full-frame output when needed. `TiledRenderer` accepts a bounded
`Surface` workspace and sends a `PixelBufferView` to `TileSink`. `beginFrame()`
receives the selected format, and every tile retains that format and a byte stride. The sink must consume
or copy each tile before `writeTile()` returns.

The workspace origin is ignored. Frame/workspace extents must contain complete storage blocks, and
the renderer validates them and the logical transfer count before opening the sink. Each tile is
reconstructed from the current scene, with intermediate-row padding initialized to zero. Painter
operations on an ordinary caller surface preserve its padding. Sink failures report an incomplete
frame; callers must inspect `TileRenderResult` before reporting success.

The NUCLEO-U545RE-Q profile deliberately retains a 480 by 4 RGB565 strip: 3,840 bytes. Supporting
higher-color targets does not add a full framebuffer or a 32-bit workspace to that profile.

The Win32 simulator has separate RGB565 and RGB888-output/XRGB8888-framebuffer presets. RGB565 is the
actual LitheView rendering surface in that variant; the adapter expands completed dirty regions into a
host-only 32-bit DIB buffer strictly for Windows presentation. The high-color variant presents its
XRGB8888 rendering surface directly.

STM32H750B-DK has separate RGB565 and RGB888-output presets. The pinned ST BSP implements its RGB888
mode with a 4-byte ARGB8888 LTDC framebuffer. LitheView uses `Xrgb8888Surface` for that variant, forcing
the unused byte to `0xff`, while the original variant retains `Rgb565Surface`.

## Optional accelerated transfer boundary

`DisplayTransferAdapter` is an optional platform hook; the portable renderer does not call it. An
adapter reports DMA, DMA2D, LTDC-layer, asynchronous, and layer-count capabilities, then accepts a
bounded `DisplayTransferRequest`.

A completed submission consumed or copied its source before returning. A pending submission returns a
nonzero token, and its source span plus any observer must remain valid and unchanged until polling or
the completion callback reports `completed` or `failed`. Polling is non-blocking. The adapter owns the
callback execution context, so an interrupt-backed adapter must hand work to application-owned bounded
state rather than calling the view system from an interrupt.
