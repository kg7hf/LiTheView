# Font and text pipeline

LitheView deliberately provides two font paths. The compact 5 by 7 font remains the default for
bring-up, diagnostics, very small targets, and applications that value transparent data over
typographic quality. Generated ROM fonts add proportional metrics, true lowercase and accented
glyphs, regular and designed-bold faces, and anti-aliased coverage. Neither backend performs file I/O
or allocation while measuring or drawing text.

## Compile-time backend selection

The font choice is a C++ template argument, so a closed application configuration does not add a
runtime backend branch:

```cpp
constexpr auto kFontBackend = lithe::BuiltInFontBackend::compact5x7;

const lithe::Font& regular = lithe::builtInFont<kFontBackend>();
const lithe::Font& bold = lithe::builtInBoldFont<kFontBackend>();
```

Change `kFontBackend` to `BuiltInFontBackend::generatedSans` to select the generated regular and bold
faces. `font5x7()` and `font5x7Bold()` are in the optional `LitheView::CompactFont` target. The generated tables are isolated
in `LitheView::GeneratedFonts`; applications that select them must link that target:

```cmake
target_link_libraries(my_application PRIVATE LitheView::GeneratedFonts)
```

Because `GeneratedFonts` is a separate static archive, a compact application that links
`LitheView::CompactFont` does not acquire the generated glyph payload. The shared demo uses the same template
selection through the `LITHEVIEW_DEMO_FONT_BACKEND` CMake cache value. Its default, `AUTO`, selects
the compact 5 by 7 backend for RGB565 examples and Generated Sans for the higher-color
RGB888-output/XRGB8888-framebuffer examples. `COMPACT5X7` and `GENERATED_SANS` explicitly override
that pairing:

```powershell
cmake -S . -B build/windows-generated -G "MinGW Makefiles" `
  -DCMAKE_BUILD_TYPE=Release `
  -DLITHEVIEW_BUILD_WIN32=ON `
  -DLITHEVIEW_DEMO_FONT_BACKEND=GENERATED_SANS
cmake --build build/windows-generated --parallel 2
```

The automatic pairing is an example policy, not a technical dependency. Either font backend works
with RGB565, RGB888, XRGB8888, and ARGB8888 surfaces and with full-frame or tiled rendering.

## Generated resource contract

`GeneratedFont` borrows a `GeneratedFontData` object containing sorted glyph records, immutable
coverage bytes, line metrics, a fallback code point, and a manifest. All referenced tables must
outlive the font and every view that borrows it. The renderer validates record offsets and dimensions
before exposing a glyph.

`src/generated_font.cpp` contains the complete source-level integration walkthrough: why the pipeline
is offline, how the flat byte and record tables fit together, how to reproduce or replace a face, how
lookup/fallback/coverage work, which files a unique target must absorb, and which ownership and
validation decisions must remain intact. Generated face files point back to that walkthrough so a
developer opening a table first is not left to reverse-engineer its contract.

Each `GeneratedGlyphRecord` stores:

- a Unicode code point and byte offset;
- width, height, row stride, and horizontal advance;
- signed X/Y offsets relative to the line origin; and
- coverage format inherited from the font manifest.

`GlyphCoverage::alpha1` stores eight left-to-right coverage bits per byte and is intended for the
smallest profile. `GlyphCoverage::alpha8` stores one coverage byte per source pixel and retains the
anti-alias values generated from the outline. The 5 by 7 backend uses `GlyphCoverage::rowMask`, its
existing one-byte-per-row representation.

Coverage is not color. A coverage value controls how much of the draw call's logical RGBA color is
applied at that pixel. `Painter` then blends that color and lets `PixelFormatTraits` encode it for the
destination surface. The same font data can therefore render any per-run color on every supported
framebuffer format. Multiple colors use multiple text runs.

Regular and bold generated faces contain independent metrics and coverage. The compact bold face
continues to use the inexpensive one-pixel synthetic overstrike documented in `resources/fonts/font5x7.cpp`.

## Offline converter

`tools/convert-font.ps1` converts a caller-selected TTF or OTF file through the installed Windows
GDI+ outline renderer. It writes a C++ translation unit and a JSON manifest; it does not install the
source font or make it a runtime dependency. Inputs are explicit so a product can select and review
its own licensed font and bounded repertoire.

To regenerate the checked regular face, supply the licensed DejaVu Sans source font
at your chosen input path. `assets/source/DejaVuSans.ttf` below is an example location,
not a bundled input or a Windows-installed font requirement:

```powershell
.\tools\convert-font.ps1 `
  -InputFont assets\source\DejaVuSans.ttf `
  -OutputSource resources\fonts\generated_sans_regular.cpp `
  -OutputManifest resources\fonts\generated_sans_regular.json `
  -Symbol GeneratedSansRegular `
  -EntryPoint generatedSans `
  -PixelSize 14 `
  -FontStyle Regular `
  -StyleLabel Regular `
  -Weight 400 `
  -Coverage Alpha8
```

The bold command uses `DejaVuSans-Bold.ttf`, `GeneratedSansBold`, `generatedSansBold`, and
`-FontStyle Bold -StyleLabel Bold -Weight 700`. `FontStyle` selects the actual regular or bold outline
exposed by the file. `StyleLabel` and `Weight` preserve the developer's reviewed visual identity, so
a semibold file rendered through its regular outline can still be recorded as `Semibold`/`600`.
`-Coverage Alpha1` produces the packed one-bit profile. `-Codepoints` accepts a
comma-separated list of individual values and inclusive ranges such as
`U+0020-U+007E,U+00B0,U+00E9,U+2026,U+FFFD`. U+003F must be present because it is the deterministic
fallback. A semibold file that exposes ordinary GDI `Regular` or `Bold` outlines can use the matching
`-FontStyle` for rasterization and `-StyleLabel Semibold` for its manifest identity.

The v2 metrics file is a strict machine contract, not an informal report. It locks converter ID and
version, family, visual and raster style, weight, size, canonical repertoire, generated symbol and
entry point, source SHA-256, FNV-1a coverage checksum, glyph/coverage/record byte counts, and line
metrics. Unknown or duplicate properties fail closed. The C++ and metrics files are written with
normalized UTF-8/LF output; rerunning the same converter on the same Windows GDI+ rasterizer with the
same source and options must reproduce the checked output exactly.

The headless designer CLI exposes the same conversion service used by the visual editor:

```powershell
$Dotnet = 'dotnet' # Select the SDK required by tools/designer/global.json on PATH.
& $Dotnet run --project tools\designer\src\LitheView.Designer.Cli -c Release --no-build -- `
  font convert assets\source\DejaVuSans.ttf build\font.cpp build\font.metrics.json `
  UiFont uiFont 18 Regular Regular 400 Alpha8 U+0020-U+007E tools\convert-font.ps1
```

### Visual designer font workflow

Open a UI project and choose **Resources > Import Font** (`Ctrl+Alt+F`). The three-page editor lets a
developer select a local TTF/OTF file, preview its actual family and raster style, choose 4-64 pixel
size, visual weight, alpha1/alpha8 fidelity, and the exact Unicode repertoire. It also requires source
origin, explicit redistribution approval, and either an SPDX ID or copied license text before import.

The editor converts into a hidden staging directory, verifies the measured output, writes a canonical
resource manifest, copies the source and license evidence into the project, and adds a logical font
reference to the current form. Reopening the project verifies every hash and selects the variant by
the display surface's painter format. Because alpha coverage is not a destination color encoding, a
font import is declared compatible with all four supported painter formats. It does not ask about a board, MCU, RTOS, internal/external
flash, QSPI, linker section, or memory budget. The consuming application makes those deployment
choices after export.

The converter fails on malformed ranges, surrogate code points, missing fallback coverage, an
unavailable requested face, or metrics that exceed the compact record fields. The manifest records
the exact source SHA-256, repertoire, glyph count, coverage format, coverage-byte count, FNV-1a data
checksum, and estimated table payload. Reproduction requires an input font with the recorded SHA-256;
matching only the family name is insufficient. Font metadata is escaped before it is emitted into a
C++ string literal, unsupported control characters fail explicitly, and the selected repertoire may
not exceed the manifest's 65,535-glyph capacity.

The current checked manifests report:

| Face | Glyphs | Coverage bytes | Record bytes | Estimated table payload |
| --- | ---: | ---: | ---: | ---: |
| DejaVu Sans Regular, 14 px | 99 | 7,617 | 1,584 | 9,201 bytes |
| DejaVu Sans Bold, 14 px | 99 | 8,734 | 1,584 | 10,318 bytes |

Those values describe immutable table payload, not final linked firmware size. Linker alignment,
manifest strings, renderer code, dead stripping, and application usage affect the final image and must
be measured on the selected target.

The task-3 H750 MinSizeRel links provide that target measurement:

| Framebuffer | Backend | Text | Data | BSS |
| --- | --- | ---: | ---: | ---: |
| RGB565 | Compact 5 by 7 | 72,972 | 188 | 35,532 |
| RGB565 | Generated Sans | 92,520 | 348 | 35,532 |
| XRGB8888 / RGB888 output | Compact 5 by 7 | 73,004 | 188 | 35,532 |
| XRGB8888 / RGB888 output | Generated Sans | 92,552 | 348 | 35,532 |

The generated selection adds 19,548 text bytes and 160 initialized-data bytes to either image and no
BSS. The compact link maps contain no generated glyph tables. The separate M33/M7 archive gates also
account for the font archive only when generated selection is active; both retain the existing
872-byte maximum bounded stack frame.

The host golden tests retain separate exact signatures for both configured backends:

| Backend | RGB565 initial/focused | XRGB8888 initial/focused |
| --- | --- | --- |
| Compact 5 by 7 | `0xf88f6224` / `0x807cef56` | `0x94b033a1` / `0x25b32d3b` |
| Generated Sans | `0x8baac9e5` / `0xb4159684` | `0x7955ec5c` / `0x56343042` |

Compact row-mask glyphs remain pixel-equivalent after XRGB8888 output is quantized to RGB565.
Generated alpha coverage blends at the destination surface's native precision, so its RGB565 and
XRGB8888 outputs intentionally use format-specific goldens rather than requiring quantized pixel
identity.

## Measurement, wrapping, and drawing

The original single-line-compatible API remains available:

```cpp
const lithe::TextMetrics metrics = lithe::Painter::measureText(text, font, 1U);
painter.drawText(frame, text, font, color, alignment, 1U);
```

The bounded layout API adds word/character wrapping, clipping or ellipsis, maximum line count, line
spacing, underline, and an explicit invalid UTF-8 policy:

```cpp
lithe::TextLayoutOptions options{};
options.align = {lithe::HorizontalAlign::left, lithe::VerticalAlign::top};
options.wrap = lithe::TextWrap::word;
options.overflow = lithe::TextOverflow::ellipsis;
options.invalidUtf8 = lithe::InvalidUtf8Policy::replacementCharacter;
options.maxLines = 3U;
options.lineSpacing = 2U;
options.underline = true;

const lithe::TextMetrics metrics =
    lithe::Painter::measureTextLayout(text, font, frameSize, options);
painter.drawTextLayout(frame, text, font, color, options);
```

`TextMetrics` reports the rendered width/height, visible line count, truncation, and whether the full
input was valid UTF-8. Replacement policy consumes an invalid byte and renders U+FFFD; a font without
that code point uses its declared fallback. Stop policy truncates before the first invalid sequence.
Overlong encodings, isolated continuation bytes, surrogate values, incomplete sequences, and values
above U+10FFFF are invalid.

Layout rescans borrowed text instead of building a heap-resident line array. Work is bounded by the
input length and `maxLines`; line count saturates at the public 16-bit result capacity and reports
truncation. Glyph coverage is clipped to the supplied text rectangle as well as the active painter
clip. No runtime path opens the source font, reads the manifest, allocates, or depends on a filesystem.

The current implementation intentionally does not perform shaping, bidirectional layout, language
specific line breaking, kerning, or font fallback across multiple generated families. Products that
need those features should define a bounded offline shaping/resource contract rather than introduce a
general-purpose runtime font engine into the embedded core.

The data files and configuration options are documented in [resources/fonts](../resources/fonts/README.md). Core-only consumers can supply their own font and exclude both bundled packages.
