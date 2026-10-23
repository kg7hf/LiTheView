# Optional bundled fonts

These are immutable resource payloads, separate from the portable library in `src/`.
`src/generated_font.cpp` implements the reusable `GeneratedFont` reader and stays in Core.

| Target | Configure option | Contents |
| --- | --- | --- |
| `LitheView::CompactFont` | `LITHEVIEW_BUILD_COMPACT_FONT` | Compact 5 by 7 regular and synthetic bold font |
| `LitheView::GeneratedFonts` | `LITHEVIEW_BUILD_GENERATED_FONTS` | Checked DejaVu Sans regular and bold coverage and metrics |
| `LitheView::GeneratedSans20` | `LITHEVIEW_BUILD_GENERATED_SANS20` | Optional 20 px DejaVu Sans regular and bold faces |
| `LitheView::GeneratedSans28` | `LITHEVIEW_BUILD_GENERATED_SANS28` | Optional 28 px DejaVu Sans regular and bold faces |

Both resource targets depend on `LitheView::Core`. Core has no dependency on either resource.
CompactFont is available by default. GeneratedFonts is enabled by default only when selected by a
repository showcase or the full example conformance suite; a consuming application enables it explicitly.
The 20 and 28 px targets are opt-in, independent of the existing 14 px target. Each face is
pre-rasterized at its actual pixel size; selecting a larger face does not scale a smaller bitmap at runtime.

```cmake
set(LITHEVIEW_BUILD_GENERATED_FONTS ON CACHE BOOL "Application uses the bundled Sans fonts")
add_subdirectory(Middlewares/Third_Party/LiTheView)
target_link_libraries(application PRIVATE LitheView::GeneratedFonts)
```

Code using `font5x7()` or `StaticLayout::build()`'s default font links `LitheView::CompactFont`.
This is a migration from the earlier package, which put that payload inside Core.
Applications supplying their own font can disable both resource options and link only Core:

```cmake
set(LITHEVIEW_BUILD_COMPACT_FONT OFF CACHE BOOL "Application owns its fonts")
set(LITHEVIEW_BUILD_GENERATED_FONTS OFF CACHE BOOL "Application owns its fonts")
add_subdirectory(Middlewares/Third_Party/LiTheView)
target_link_libraries(application PRIVATE LitheView::Core)
```

Pass the application's `Font` to the layout and controls. Its glyph storage and font object must
outlive the controls that borrow them. There is no global font registry, allocation, or runtime
format conversion introduced by choosing a resource target.

See [font conversion and representation](../../docs/FONTS.md) and [attribution](../../NOTICE.md).
The JSON files retain converter metrics and source-font hashes; the original TTF files are not bundled.
The compact glyphs were created by the project owner in 2012. The generated Sans tables instead
retain the [DejaVu terms](../../licenses/DejaVu-LICENSE.txt), copied with every installed package.
