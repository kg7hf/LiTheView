# Animation assets

LitheView animation is intentionally split into three independent concerns:

1. an application owns immutable frame bytes and their lifetime;
2. a `ResourceProvider` maps stable names to `Bitmap` descriptors; and
3. `AnimatedImageView` selects a frame when the caller advances application time.

The core does not load files, decode PNG data, allocate a frame buffer, create a timer, or choose an
asset format. A product can therefore keep a one-bit icon sequence in internal flash, place a larger
indexed sequence in external ROM, or provide an entirely different resource provider without changing
the animation widget.

## Supplied gallop example

The acceptance examples include eight original 96 by 72 horse frames in two compile-time forms. The
checked-in size deliberately favors visible fidelity so the examples demonstrate the animation path,
not merely its minimum storage footprint:

| Representation | Bytes per frame | Eight-frame payload | Color contract |
| --- | ---: | ---: | --- |
| `BitmapFormat::alpha1` | 864 | 6,912 | one caller-selected `Bitmap::tint` |
| `BitmapFormat::indexed4` | 3,456 | 27,648 | one borrowed palette with at most 16 `Color` entries |

The packed arrays are in `tests/rendering/common/gallop_frames.hpp` and
`tests/rendering/common/gallop_indexed4_frames.hpp`. `GallopResources<Format>` exposes both through the same
names, `gallop-f0` through `gallop-f7`. The source PNGs under `tests/rendering/assets/animations` are developer-facing
artwork and provenance; the runtime never reads them.

The default selection is compile-time configurable:

```powershell
cmake -S . -B build/custom `
  -DLITHEVIEW_DEMO_ANIMATION_FORMAT=ALPHA1

cmake -S . -B build/custom-color `
  -DLITHEVIEW_DEMO_ANIMATION_FORMAT=INDEXED4
```

`AUTO` selects `ALPHA1` for RGB565 demos and `INDEXED4` for the RGB888/XRGB8888 Win32 and H750
variants. This is an example policy, not a restriction in the graphics core. Either bitmap format can
render into any supported destination surface.

## One-bit packing

An alpha1 frame contains 12 bytes per 96-pixel row. The most-significant bit is the leftmost pixel:

```text
source pixels x=0..7:  1 0 1 1 0 0 0 1
stored byte:           1 0 1 1 0 0 0 1 = 0xb1
```

A set bit draws the bitmap tint. A clear bit is transparent and leaves the destination unchanged.
Changing the tint does not change or duplicate the shape data:

```cpp
lithe::Bitmap warning = frame;
warning.tint = lithe::Color::rgb(0xffaa00U);
```

This is the smallest supplied representation and is appropriate for monochrome panels, icons, masks,
or applications that recolor the same shape at runtime.

## Four-bit indexed packing

An indexed4 frame stores two palette indexes per byte. The high nibble is the left pixel and the low
nibble is the right pixel:

```text
source palette indexes x=0,1:  3, 12
stored byte:                    0x3c
```

The palette is borrowed immutable storage and must outlive every bitmap that refers to it. Index zero
has no implicit meaning; the gallop palette deliberately assigns it `Color::transparent()`. A palette
index outside the supplied palette span fails closed as transparent. The example uses ten entries, so
six index values remain unused.

Indexed4 retains flat color at half a byte per pixel. It is a useful middle ground between alpha1 and
full RGB/ARGB assets when ROM matters more than smooth gradients.

## Scaling without larger frame arrays

`Painter::drawBitmap(origin, bitmap, scale)` performs integer nearest-neighbor scaling. Each source
pixel becomes a `scale` by `scale` block. `AnimatedImageView::setBitmapScale()` applies the same rule
while keeping the result centered in the view frame:

```cpp
lithe::demo::GallopResources<
    lithe::demo::GallopResourceFormat::indexed4> resources;
lithe::AnimatedImageView animation({4, 46, 576, 432},
                                   resources,
                                   lithe::demo::kGallopResourceNames,
                                   lithe::demo::kGallopIntervalMs);
animation.setBitmapScale(6U);
```

Scaling performs no allocation and stores no resized copy. Destination clipping happens before the
pixel loop, and source coordinates are derived with bounded integer division. Scale zero is rejected by
the painter; the widget normalizes it to one. Nearest-neighbor is deliberate: it preserves exact
coverage and palette indexes, has deterministic execution, and avoids filter work in an embedded hot
path.

## Choosing fidelity versus flash

The checked-in 96 by 72 sequence is a showcase setting, not a framework requirement. If application
flash or external-resource bandwidth is tight, developers can make independent, compile-time choices:

- regenerate the same sequence at 64 by 48 or 32 by 24;
- select `alpha1` when one runtime tint is sufficient;
- use fewer frames or a longer interval when motion smoothness is less important;
- keep high-fidelity assets in application-owned external flash; or
- pre-size artwork for the target when saved scaling work is worth additional ROM.

For eight frames, 64 by 48 costs 3,072 bytes in alpha1 or 12,288 bytes in indexed4. The 32 by 24 form
costs 768 bytes or 3,072 bytes. These alternatives use the same `ResourceProvider`, `Bitmap`, and
`AnimatedImageView` contracts. No image decoder, heap allocation, or expanded framebuffer copy is
introduced.

## Advancing the animation

The view owns frame index and elapsed remainder only. The application owns the clock:

```cpp
if (application.animationsRunning())
{
    const bool changed = application.advanceTime(elapsedMilliseconds);
    if (changed)
    {
        application.render();
    }
}
```

An idle application has no animation wakeup. A frame change invalidates the transparent image region;
the retained view tree redraws the required background and overlapping children through the same bounded
clip.

## Creating or replacing a sequence

For a replacement that preserves the supplied resource contract:

1. Draw exactly eight poses in a 4 by 2 transparent sheet. Keep cell dimensions, baseline, subject
   scale, and gutters consistent. Frame order is left to right across the first row, then the second.
2. Normalize every pose into a 128 by 96 cell. The supplied sheets reserve an approximately 120 by 88
   content box and share one baseline so the subject does not jump.
3. Reduce each cell to the selected master geometry. The supplied showcase uses 96 by 72; 64 by 48
   and 32 by 24 are useful constrained alternatives. For alpha1, threshold alpha and pack eight pixels
   per byte, MSB first.
   For indexed4, quantize only covered pixels to a palette of at most 16 colors and pack two indexes per
   byte, high nibble first.
4. Concatenate frames in display order. Keep each frame contiguous and use the recorded stride when
   constructing its `Bitmap` span.
5. Update the FNV-1a checksums beside the arrays and the deterministic resource tests. Run both RGB565
   and XRGB8888 host suites because source and destination formats are independent contracts.
6. Inspect every frame at its actual target scale. A readable source image can still lose an ear, hoof,
   or single-pixel gap during thresholding.

The current payload checksums are `0xb24d577d` for alpha1 and `0x4760d9a4` for indexed4. These guard
accidental byte changes; they are not cryptographic signatures.

For a different frame count or geometry, create a new resource provider rather than silently changing
these names and constants. The application must ensure that source bytes, palettes, frame-name spans,
and the resource provider all outlive the view.
