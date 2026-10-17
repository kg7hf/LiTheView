# Image and icon resources

LitheView treats an image as immutable pixel data plus a small `Bitmap` descriptor. The embedded
runtime does not open PNG, JPEG, SVG, GIF, or TIFF files. Image decoding, cropping, scaling,
quantization, alpha handling, and byte packing happen offline; the application compiles the resulting
C++ table and exposes it through its `ResourceProvider`.

This separation keeps the runtime bounded and allocation-free while letting the designer preserve the
reviewed source, license evidence, exact conversion options, hashes, and measured representation size.
The visual resource contract deliberately does not decide where a consuming application links the
table. Internal flash, external flash, host files, and a custom provider are application deployment
choices, not image properties.

## Start with the intended use

Choose **Image** for fixed artwork and **Icon** for a reusable symbol. The distinction is semantic
until tinting is enabled:

- a fixed-color image or icon carries its final color in direct pixels or an indexed palette;
- a tintable icon uses `alpha1` or `alpha8`, where the payload describes coverage and `Bitmap::tint`
  supplies the draw color; and
- tintable images are rejected so a resource cannot silently lose its authored color meaning.

A tintable coverage mask is usually the best representation for controls whose normal, focused,
pressed, disabled, or themed states share one shape. One mask can be reused with different tints
without storing another copy of the pixels. Use a fixed-color representation when color is part of the
artwork itself.

## Designer workflow

With a project open, choose **Resources > Import Image** (`Ctrl+Alt+I`) or **Resources > Import
Icon** (`Ctrl+Alt+K`). The editor walks through three groups of decisions:

1. **Identity** selects the source raster, stable resource ID, display name, generated C++ symbol,
   entry point, and optional provider identity.
2. **Conversion** selects a crop rectangle, output dimensions, sampling, native `Bitmap` format,
   alpha policy, threshold, optional ordered dither, tint, matte, and indexed palette. The preview
   shows the crop and tint on a checkerboard while the payload estimate updates.
3. **Provenance and license** records where the artwork came from and requires explicit
   redistribution approval plus an SPDX identifier or copied license text.

Import is transactional. The designer converts into a staging directory, strictly loads the metrics,
checks that every measured field matches the request, writes a canonical resource manifest, verifies
the source/license/generated artifacts, and only then moves the new resource into the project and
updates the project manifest. An existing resource directory is never merged or overwritten.

The checked converter accepts PNG, BMP, JPEG, GIF, and TIFF raster input. A project may preserve SVG
as reviewed source material, but this GDI+ conversion checkpoint does not contain an SVG rasterizer.
Export reviewed SVG art to a lossless PNG at the intended source resolution before importing it. The
restriction is explicit so different developer machines cannot silently choose different SVG engines,
fonts, filters, or color-management behavior.

## Format choices and byte layout

All rows are tightly packed unless a future converter records a larger explicit stride. Multibyte
pixels follow the native `Bitmap` contract in `include/lithe/painter.hpp`.

| Format | Row bytes | Stored meaning | Typical use |
|---|---:|---|---|
| `alpha1` | `(width + 7) / 8` | One MSB-first coverage bit per pixel | Small, hard-edged tintable icon |
| `alpha8` | `width` | One 0-255 coverage byte per pixel | Antialiased tintable icon |
| `indexed4` | `(width + 1) / 2` | Two palette indexes per byte, left pixel in the high nibble | Small fixed-color art with up to 16 ARGB colors |
| `rgb565` | `width * 2` | Little-endian 5:6:5 direct color | Compact opaque color art |
| `rgb888` | `width * 3` | R, G, B bytes | Opaque 24-bit color art |
| `xrgb8888` | `width * 4` | B, G, R, `0xff` bytes | Aligned opaque high-color art |
| `argb8888` | `width * 4` | B, G, R, A bytes | High-color art with per-pixel alpha |

For `indexed4`, palette entry zero is not automatically transparent. Every entry is an explicit ARGB
color, and its alpha determines transparency. The converter records both payload and palette byte
counts because both consume representation storage.

`alpha1` and `alpha8` contain no color channels. Their `tint` is therefore meaningful and may be
changed without regenerating the shape. Direct and indexed formats already contain color; treating
them as tintable would be ambiguous and is rejected by the request and manifest validators.

## Crop, scale, alpha, and dither

The crop rectangle is expressed in source pixels and must remain completely inside the decoded image.
Output dimensions describe the exact generated bitmap; there is no implicit aspect-ratio correction.
That makes stretching or accidental one-pixel changes visible in the manifest diff.

Sampling options are:

- **Nearest neighbor** for pixel art, masks, and intentionally hard edges; and
- **Bilinear** for photographic or smoothly shaded source art.

Alpha handling is explicit:

- **Preserve** retains sampled alpha in alpha/indexed/ARGB destinations;
- **Threshold** converts alpha at the selected 0-255 boundary; and
- **Opaque** forces every sampled pixel to full coverage.

Formats without an alpha channel composite partially transparent input over the chosen matte before
packing. This prevents a desktop decoder's implicit background color from leaking into generated
pixels.

The optional ordered 4-by-4 dither is deterministic and available only for `indexed4` and `rgb565`.
It can preserve the visual impression of a gradient at low color depth, but it also introduces a
visible pattern. Leave it off for flat icons, one-pixel lines, text-like art, or artwork whose palette
was already authored by hand.

## Generated C++ contract

The converter emits immutable byte storage, an optional immutable palette, a `const Bitmap`, a
compile-time validity check, and one accessor. A generated file has this conceptual shape:

```cpp
namespace lithe
{

namespace
{

inline constexpr std::array<std::byte, kPayloadBytes> kStatusPixels{/* generated bytes */};
inline constexpr Bitmap kStatusBitmap{
    kStatusPixels,
    24,
    24,
    3U,
    BitmapFormat::alpha1,
    Color::argb(0xff2d6cdfU),
    {}};

static_assert(kStatusBitmap.valid());

} // namespace

const Bitmap& statusIcon() noexcept
{
    return kStatusBitmap;
}

} // namespace lithe
```

Generated packed tables are surrounded by paired `clang-format off/on` markers. This is the same
narrow exception used by generated font tables: it preserves deterministic, reviewable packing while
the surrounding first-party C++ remains subject to the project Allman formatter.

The application declares the accessor and maps its logical resource name at its existing provider
boundary:

```cpp
namespace lithe
{

const Bitmap& statusIcon() noexcept;

class ApplicationResources final : public ResourceProvider
{
public:
    const Bitmap* bitmap(std::string_view name) const noexcept override
    {
        if (name == "status-icon")
        {
            return &statusIcon();
        }
        return nullptr;
    }
};

} // namespace lithe
```

The generated arrays and any palette must outlive every `Bitmap` that borrows them. The generated
accessor satisfies that rule with static immutable storage. If an application builds its own
descriptor, the application owns the same lifetime obligation.

## Headless conversion

The designer CLI exposes the same conversion service for scripted builds. From
`tools/designer`:

```powershell
$Dotnet = 'dotnet' # Select the SDK required by tools/designer/global.json on PATH.

& $Dotnet run --project src/LitheView.Designer.Cli -c Release --no-build -- `
  image convert `
    assets\source\status.png `
    ..\..\build\status_icon.cpp `
    ..\..\build\status_icon.metrics.json `
    StatusIcon statusIcon Icon true Alpha8 `
    0 0 128 128 24 24 `
    Bilinear Preserve 96 None `
    ff2d6cdf ff000000 "" `
    ..\..\tools\convert-image.ps1
```

The arguments after the format are crop X/Y/width/height, output width/height, scale mode, alpha mode,
alpha threshold, dither mode, tint ARGB, matte ARGB, and a comma-separated indexed4 ARGB palette. Use
an empty palette for every format except `indexed4`.

The metrics file records converter identity/version, source and crop dimensions, output geometry,
stride, every option, payload/palette sizes, FNV-1a checksums, and the source SHA-256. Identical input
bytes and options must generate byte-identical C++ and metrics. Changing an option is supposed to
change the reviewable resource identity.

## Choosing compact versus high-fidelity art

Representation size is reported so the developer can compare variants, but the designer does not
declare that a measured payload belongs in a particular memory device. Practical choices include:

- prefer `alpha1` for a crisp monochrome symbol whose edges survive thresholding;
- prefer `alpha8` when antialias quality matters and tint reuse offsets the larger mask;
- prefer `indexed4` for artwork that can be reviewed against an explicit small palette;
- prefer `rgb565` for compact opaque color art; and
- prefer `xrgb8888` or `argb8888` when fidelity or alpha is more important than payload size.

If application storage is tight, reduce source crop area, output dimensions, frame count, palette
size, or color depth and compare the preview again. If a higher-fidelity application has more storage,
keep the larger representation. Those are consuming-application tradeoffs; the same logical image ID
and `Bitmap`/`ResourceProvider` API remain valid.

## Review checklist

Before accepting an imported image or icon, verify:

- the source origin and redistribution rights are recorded;
- the crop and output dimensions match the intended display composition;
- tintable content is an icon using `alpha1` or `alpha8`;
- transparent indexed colors have explicit alpha rather than relying on index zero;
- matte, threshold, palette, and dither choices are intentional;
- the preview is acceptable on every declared compatible painter format;
- a second conversion produces identical C++ and metrics; and
- the consuming application maps the logical name and preserves the generated storage lifetime.

