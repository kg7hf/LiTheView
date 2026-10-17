# Gallop source artwork

The PNG files in this directory are original source assets created for LitheView with OpenAI's
built-in image-generation tool on 2026-08-30. The horse sheets supplied during design exploration were
not copied into the repository because no redistribution license accompanied them.

Paul R. Decker offers the project's rights in these assets and subsequent edits under
[GPL-3.0-only](../../../../LICENSE). This records the project's licensing decision and preserves
the generation history; it does not assert that modification alone removes third-party rights.

- `gallop_source.png` is the retained high-resolution generated silhouette sheet.
- `gallop_sprite_sheet.png` is the normalized transparent silhouette source for the alpha1 payload.
- `gallop_color_sprite_sheet.png` is the normalized flat-color source for the indexed4 payload.

Both files are 512 by 192 pixels: four 128 by 96 cells across and two rows down. The checked-in
showcase arrays reduce those cells to 96 by 72 frames. Runtime code uses the ROM-ready arrays
documented in [Animation assets](../../../../docs/ANIMATION_ASSETS.md), not these PNGs.

## Generation prompts

The silhouette source was generated with this prompt:

```text
Use case: stylized-concept
Asset type: source sprite sheet for a tiny embedded C++ graphics-library animation test
Primary request: Create an original eight-frame looping gallop animation of one riderless horse, shown in strict side profile moving left to right.
Scene/backdrop: genuinely transparent background.
Subject: one anatomically readable horse silhouette; eight distinct consecutive gallop poses that form a smooth cyclic run.
Style/medium: crisp flat monochrome black silhouette, simple vector-like contours, designed to remain recognizable after conversion to a 32 by 24 pixel 1-bit alpha mask.
Composition/framing: exact 4 columns by 2 rows sprite sheet; every cell identical in size; one full horse per cell; identical scale, horizontal center, baseline, and overall bounding box; generous clear gutters between cells; no pose may touch a cell edge.
Color palette: solid black only with transparent background; no gray and no antialiasing.
Constraints: eight frames exactly; consistent horse identity and proportions; complete legs, tail, ears, and body visible in every frame; frame order reads left-to-right across the first row then left-to-right across the second row; seamless loop; no cropping.
Avoid: rider, saddle, reins, hurdles, ground line, shadows, scenery, borders, grid lines, labels, numbers, text, logos, signature, watermark, photographic texture, gradients, duplicate poses, extra limbs, disconnected anatomy.
```

The flat-color source was derived from the generated silhouettes with this edit prompt:

```text
Use case: precise-object-edit
Asset type: color source sprite sheet for a tiny embedded C++ graphics-library animation test
Input images: Image 1 is the edit target and contains the authoritative eight horse silhouettes.
Primary request: Add a restrained flat color treatment inside each existing horse silhouette.
Style/medium: crisp flat-color embedded sprite art with clean region boundaries and no texture.
Color palette: chestnut brown body, dark charcoal mane and tail, near-black lower legs and hooves, warm tan muzzle and body highlight, one small muted teal accent per horse.
Constraints: change only the colors inside the eight existing horse silhouettes; preserve the exact 4-column by 2-row layout, all eight poses, silhouette contours, scale, centers, baselines, gutters, frame order, and genuinely transparent background; use no more than 12 opaque colors total; hard-edged color regions suitable for conversion to a 4-bit indexed palette; no antialiasing outside silhouette.
Avoid: changing anatomy or pose, adding or removing limbs, rider, saddle, reins, harness, hurdle, ground line, scenery, shadow, border, grid, text, number, logo, signature, watermark, gradient, photographic texture, semi-transparent interior pixels.
```

The generated sheets were normalized and packed deterministically; no user-supplied horse pixels remain
in either asset.
