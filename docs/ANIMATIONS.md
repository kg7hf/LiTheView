# Animation resources

LitheView animation resources are ordered, immutable `Bitmap` frames plus timing defaults. The
embedded runtime does not decode a GIF, APNG, video, or sprite sheet. Sprite extraction, scaling,
quantization, alpha handling, palette selection, and byte packing happen offline. The consuming
application compiles the generated C++, gives every frame a stable `ResourceProvider` name, and
advances `AnimatedImageView` from its existing monotonic clock.

That boundary is intentional:

- there is no runtime decoder, filesystem, heap allocation, animation thread, or hidden timer;
- every frame has a known format, stride, byte count, and lifetime before the application starts;
- a single indexed palette can be shared by every frame;
- source, license, conversion options, checksums, and generated output are reviewable and hash-locked;
  and
- target, bus, RTOS, linker section, internal/external flash, QSPI, and scheduling policy remain
  decisions for the consuming application rather than properties of the visual design.

## Designer workflow

Open a designer project and choose **Resources > Import Animation** (`Ctrl+Alt+A`). The dialog has
three pages.

1. **Source and identity** selects the raster sprite sheet, stable resource ID, display name,
   generated C++ table symbol, and generated accessor name.
2. **Frames, timing, and preview** defines the row-major source grid, output frame geometry, native
   `Bitmap` representation, alpha/palette/dither choices, frame interval, repeat index, and autoplay
   default. The preview plays the selected frames; click it to pause or resume while inspecting an
   individual pose. The measured representation size updates without assigning those bytes to a
   hardware memory region.
3. **Provenance and license** records origin and requires explicit redistribution approval plus an
   SPDX identifier or copied license text.

Import is transactional. The designer stages the source sheet, optional license, generated C++, and
strict metrics in a unique directory. It checks that the converter returned the exact requested grid,
representation, timing, symbols, source hash, checksums, and byte counts. It then canonicalizes and
validates the resource manifest, verifies every staged artifact, moves the new resource directory into
place, and atomically updates the project manifest. An existing resource directory is never merged or
overwritten.

The form receives one logical bitmap resource per frame. For an entry point `busyAnimation`, the
default runtime names are `busyAnimation-f0`, `busyAnimation-f1`, and so on. Assign those frame
resources, in order, to an Animated Image widget's **Animation Frames** property. The generated
animation payload itself remains one compact sequence; the logical names do not duplicate its bytes.

## Sprite-sheet grid

The checked importer reads one rectangular row-major grid:

```text
sheet origin (x,y)
       |
       v
    +--------+--------+--------+--------+
    | frame0 | frame1 | frame2 | frame3 |
    +--------+--------+--------+--------+
    | frame4 | frame5 | frame6 | frame7 |
    +--------+--------+--------+--------+
```

The grid inputs are:

- **Sheet X/Y**: top-left source pixel of frame zero;
- **Source frame width/height**: exact source rectangle for every frame;
- **Columns**: frames per source row;
- **Frame count**: number of rectangles consumed in row-major order; and
- **Output frame width/height**: exact dimensions of every generated `Bitmap`.

The complete grid, including the partly used final row, must fit the decoded source image. Frames may
not overlap implicitly, have different dimensions, or rely on whitespace detection. Those features
are deliberately explicit rather than heuristic so the same source and options produce the same
payload on every machine.

If source art has margins between cells, crop those margins out before import or produce a reviewed
tightly packed sprite sheet. If frame sizes differ, normalize them onto equal transparent canvases.
Keeping the grid regular lets the converter remain simple, deterministic, and fail-closed.

## Representation choices

Each frame uses one of the normal LitheView `Bitmap` formats:

| Format | Bytes per frame row | Animation use |
|---|---:|---|
| `alpha1` | `(width + 7) / 8` | Small hard-edged sequence tinted at draw time |
| `alpha8` | `width` | Antialiased coverage sequence tinted at draw time |
| `indexed4` | `(width + 1) / 2` | Up to 16 reviewed ARGB colors shared by all frames |
| `rgb565` | `width * 2` | Compact opaque color frames |
| `rgb888` | `width * 3` | Opaque 24-bit color frames |
| `xrgb8888` | `width * 4` | Aligned opaque high-color frames |
| `argb8888` | `width * 4` | High-color frames with per-pixel alpha |

Tintable animations must use `alpha1` or `alpha8`. Their pixels are coverage, not color; the
`Bitmap::tint` supplies the desired color without creating another frame set. Direct and indexed
frames already contain authored color and cannot be marked tintable.

An `indexed4` sequence stores two palette indexes per byte and one shared palette. Palette entry zero
is transparent only when its explicit ARGB alpha is zero. Sharing the palette is important: eight
frames do not require eight copies of the same colors. The converter verifies that every independently
packed frame produced the same palette before it concatenates the payload.

Nearest-neighbor sampling preserves pixel-art edges. Bilinear sampling is appropriate for smoothly
shaded source art. Ordered 4-by-4 dither is deterministic and available for `indexed4` and `rgb565`;
it can improve perceived gradients but should be reviewed because the pattern moves with the frames.
Alpha preserve, threshold, and opaque policies have the same meaning described in `docs/IMAGES.md`.

## Timing and playback

The generated resource records three playback defaults:

- **Frame interval** is the number of milliseconds between frame changes and fits an unsigned
  16-bit value.
- **Repeat index** is the frame selected after the last frame. Zero produces a conventional loop. A
  nonzero index supports a one-time introduction followed by a shorter repeating tail.
- **Autoplay** tells the application whether the authored form expects the sequence to start
  advancing when shown.

These values do not create a timer. `AnimatedImageView` remains caller-clocked. The application feeds
elapsed monotonic time through its normal update path, which preserves product control over pause,
resume, manual stepping, scene visibility, low-power policy, and scheduling. The designer preview uses
a Windows timer only to help inspect the authored sequence; it is not generated product behavior.

## Generated C++ and lifetime

For a symbol `BusyAnimation` and entry point `busyAnimation`, the generated translation unit has this
conceptual shape:

```cpp
namespace lithe
{

namespace
{

inline constexpr std::array<std::byte, kTotalPixelBytes> kBusyAnimationPixels{
    /* deterministic packed frame bytes */
};

inline constexpr std::array<Color, kPaletteCount> kBusyAnimationPalette{
    /* optional shared indexed4 colors */
};

inline constexpr std::array<Bitmap, kFrameCount> kBusyAnimationFrames{{
    Bitmap{/* frame-zero subspan and descriptor */},
    Bitmap{/* frame-one subspan and descriptor */},
}};

} // namespace

std::span<const Bitmap> busyAnimation() noexcept;
std::uint16_t busyAnimationIntervalMilliseconds() noexcept;
std::size_t busyAnimationRepeatIndex() noexcept;
bool busyAnimationAutoplay() noexcept;

} // namespace lithe
```

Every `Bitmap` borrows a subspan of the one immutable pixel array. Indexed frames also borrow the one
immutable palette. The array, descriptors, and palette all have static lifetime, so the returned span
and every pointer returned by an application provider stay valid for the program lifetime. Do not
return a frame that borrows a temporary vector, stack array, decoded image, or short-lived palette.

Generated packed tables use paired `clang-format off/on` guards so deterministic byte packing remains
stable while all surrounding handwritten C++ stays under the project's Allman formatting gate.

## ResourceProvider integration

The generated accessor returns frames; the application owns the names used by layout XML and widgets.
A small adapter can map them without copying descriptors or pixels:

```cpp
namespace lithe
{

std::span<const Bitmap> busyAnimation() noexcept;

class ApplicationResources final : public ResourceProvider
{
public:
    ApplicationResources() noexcept : mFrames(busyAnimation())
    {
    }

    const Bitmap* bitmap(std::string_view name) const noexcept override
    {
        for (std::size_t index = 0U; index < mNames.size(); ++index)
        {
            if (name == mNames[index] && index < mFrames.size())
            {
                return &mFrames[index];
            }
        }
        return nullptr;
    }

private:
    static constexpr std::array<std::string_view, 4U> mNames{
        "busyAnimation-f0",
        "busyAnimation-f1",
        "busyAnimation-f2",
        "busyAnimation-f3",
    };

    std::span<const Bitmap> mFrames;
};

} // namespace lithe
```

The form writer emits those names as consecutive `animationImage0`, `animationImage1`, and later
attributes. `AnimatedImageView` resolves them through the ordinary `ResourceProvider` boundary. This
keeps the graphics package independent of generated-file names, linker layout, and product storage.

## Headless conversion

The CLI exposes the same checked converter used by the visual dialog:

```powershell
$Dotnet = 'dotnet' # Select the SDK required by tools/designer/global.json on PATH.

& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -c Release --no-build -- `
  animation convert `
    tests/rendering/assets/animations/gallop_sprite_sheet.png `
    build/gallop_animation.cpp `
    build/gallop_animation.metrics.json `
    GallopAnimation gallopAnimation `
    false Indexed4 `
    0 0 128 96 4 8 `
    96 72 Bilinear Preserve 64 Ordered4x4 `
    ffffffff ff000000 `
    "00000000,ff141519,ff2c2d35,ff543422,ff7d321f,ffa94a22,ffc96936,ffe6a548,fff5c96a,ff2bb7ad" `
    100 0 true `
    tools/convert-animation.ps1
```

After the symbol and entry point, arguments are tintable, format, sheet X/Y, source frame width/height,
columns, frame count, output width/height, sampling, alpha policy, threshold, dither, tint, matte,
palette, interval, repeat index, autoplay, and converter path.

The checked gallop golden is an eight-frame, 96-by-72 indexed4 sequence. It stores 27,648 pixel bytes
and one 40-byte palette for a 27,688-byte representation. Its source SHA-256 and pixel, palette, and
combined FNV-1a checksums are locked by a real two-pass test that also requires byte-identical C++ and
metrics.

## Scaling the resource to a product

The checked golden deliberately has enough detail to demonstrate a capable color display. It is not a
minimum requirement. If application storage is tight, reduce output dimensions, frame count, palette
size, or color depth; choose `alpha1` when one tint is sufficient; or keep a higher-fidelity variant in
application-owned external storage. If update time matters more than representation size, pre-size the
frames rather than asking the widget to scale them while drawing.

Those are deployment and performance tradeoffs. The designer records the reviewed visual variant and
its exact byte count but does not say whether it belongs in U545 internal flash, H750 internal or
external flash, a host executable, or a custom asset provider.

## Review checklist

Before accepting an imported animation, verify:

- source origin, attribution, license, and redistribution approval are complete;
- sheet origin, cell size, columns, and frame count select the intended poses in the intended order;
- every frame has the same transparent canvas and visual registration;
- output dimensions and sampling preserve the intended fidelity;
- alpha, matte, palette, dither, and tintability match the chosen format;
- interval, repeat index, and autoplay express the authored behavior without assuming a hidden timer;
- measured frame, pixel, palette, and total byte counts are plausible;
- the consuming provider exposes every generated logical frame name in order; and
- source, generated output, metrics, and manifest changes are reviewed together.
