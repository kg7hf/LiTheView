# Define your segmented glass

This is the reading guide for [glass.segment-display.json](glass.segment-display.json).
Start with the glass drawing and its electrode table. We describe what can light
up, give those pieces useful field names, then bind application values to them.
The application owns the menu and FSM; the glass definition has no menu states.

The import file is strict JSON. It does **not** accept `//` comments, trailing
commas or extra properties such as `description`. The comments in the teaching
example below belong in this guide; remove them before importing that example.
Keep your own naming and wiring notes beside your JSON in the same way.

## Start small

This complete teaching example describes one indicator. Its triangle is only the
preview drawing; one logical bit turns the physical indicator on or off.

```jsonc
{
  "schemaVersion": 1,             // Glass document format, not a board revision.
  "id": "myDeviceGlass",          // Stable identity for this glass definition.
  "segmentCount": 1,              // Logical bit capacity; ID 0 is the only bit.
  "viewWidth": 100,               // Preview coordinates, not a pixel framebuffer.
  "viewHeight": 60,
  "shapes": [
    {"id": 0, "polygon": [{"x": 10, "y": 10}, {"x": 50, "y": 10}, {"x": 30, "y": 50}]}
  ],
  "icons": [{"id": "warningIndicator", "segments": [0]}], // Boolean field.
  "groups": [],                  // Required arrays may be empty.
  "texts": [],
  "numbers": []
}
```

Import your comment-free file in **LiTheView: Add LiTheView UI**, choose a
segmented display, inspect its preview, and select the application's logical
input capabilities. Bind `warningIndicator` to an application input such as
`warningActive`, save and **Generate UI**. Your presenter supplies that Boolean;
your platform adapter sends the resulting segment bits to the LCD controller.

## Read the Tiny Gecko definition

| JSON member | Meaning in this example |
| --- | --- |
| `id` | `tinyGeckoA03` identifies this glass/mapping contract. |
| `segmentCount` | 160 logical bit positions, numbered 0 through 159. It does not mean 160 visible polygons. |
| `viewWidth`, `viewHeight` | 870 by 330 preview units. Origin is top left; x grows right, y grows down. |
| `shapes` | 134 polygons used by this lesson: seven 14-stroke characters, four 7-stroke digits and eight ring pieces. Other glass symbols are not described yet. |
| `icons` | `ringPiece0` through `ringPiece7`: individually addressable Boolean fields. |
| `groups` | `volumeRing`: the same eight pieces as an ordered level indicator, range 0 through 100. |
| `texts` | `label`: seven character cells, left to right, with the supported glyph table. |
| `numbers` | `value`: four decimal cells, left to right; values 0 through 9999. |

Field names start with a lowercase letter, contain only letters and digits, and
have at most 64 characters. Names must be unique across all four field arrays.
For your glass, use names such as `batteryLevel`, `alarmIndicator`,
`temperatureDigits` and `modeText` to describe its physical fields. Form input
names describe the values your application supplies and may differ from field
names: this example binds `volumeRing` to `volumePercent`.

## Keep geometry, bits and wiring separate

A shape ID is a logical bit number. Changing its polygon changes the preview,
not the electrode driven on the hardware. Every segment used by a field needs
one shape with that ID. Polygons use 3 to 32 points inside the viewport and must
have nonzero signed area; draw a simple, non-crossing outline.

This example chooses `id = glassCOM * 20 + glassSEG`. For example, ID 121 is
**glass COM6 / SEG1**, the top stroke of the first text cell. This numbering is
an application convention, not a LiTheView requirement for other displays.

The PCB2100 A03 board reverses the eight COM connections. The application adapter
in [lcd_rows.hpp](../../Platform/EFM32/lcd_rows.hpp) maps glass COM6 to controller
COM1; SEG1 stays SEG1. The JSON contains no MCU register addresses. For custom
glass, choose stable logical IDs and implement the physical mapping in your
platform adapter. Check the actual board revision's schematic before connecting
that adapter to hardware.

## Character cells and glyph strokes

`cells` is an array of cells in display order. Each cell is an array of segment
IDs in a consistent stroke order. A glyph's `strokes` contains **indices into a
cell**, not logical segment IDs. All cells in one field have the same number of
strokes. The same glyph table therefore works at every position.

The Tiny Gecko text stroke order is:

| Index | Drawing name | Position |
| --- | --- | --- |
| 0 | A | Top |
| 1 | B | Upper right |
| 2 | C | Lower right |
| 3 | D | Bottom |
| 4 | E | Lower left |
| 5 | F | Upper left |
| 6 | G | Middle left |
| 7 | H | Upper-left diagonal |
| 8 | J | Upper center vertical |
| 9 | K | Upper-right diagonal |
| 10 | M | Middle right |
| 11 | N | Lower-right diagonal |
| 12 | P | Lower center vertical |
| 13 | Q | Lower-left diagonal |

Its first cell is `[121,122,42,2,1,81,61,101,82,102,62,22,41,21]`.
A glyph using stroke index 0 lights ID 121 in that cell, but the top segment of
the next cell when drawn there. The seven-stroke numeric fields use indices 0
through 6 in order: top, upper right, lower right, bottom, lower left, upper left,
middle. Numeric fields must define exactly glyphs `0` through `9`. Text glyphs
are explicit printable ASCII characters; an empty stroke list can represent a
space. Unsupported characters and overlong strings are rejected, not silently
substituted. Numbers are right aligned without leading zeroes; zero lights the
rightmost zero glyph.

## The ring is an ordered group

```json
{"id": "volumeRing", "segments": [140,147,146,145,144,143,142,141], "maximum": 100}
```

The order starts at twelve o'clock and moves clockwise. The generated encoder
rounds `value * 8 / 100` to the nearest piece, with ties rounded up: 0 lights
none, 50 lights four, and 100 lights all eight. Values above 100 are rejected.
An icon can contain several segment IDs that switch together. A group instead
lights the first N entries in its declared order.

The individual `ringPiece` icons overlap `volumeRing` intentionally as alternative
ways to use the glass. Bind the group **or** the individual icons in a form.
Two bindings cannot write the same segment. Reordering a group's segments changes
its fill direction; reordering `shapes` does not.

## Change custom glass through the project workflow

1. Keep the source definition in `UI/design`. Adjust its geometry, IDs, fields and
   glyphs against your glass drawing. Begin with one icon, then add the rest.
2. Open **Configure**, import the revised glass and inspect the preview. Select
   the logical input capabilities and Apply. The project owns a locked copy under
   `UI/designer/profiles`; editing the source alone does not update that copy.
3. Update form bindings if you renamed or removed fields. Save and Generate UI.
   Let the designer update snapshot hashes; do not hand-edit hashes to bypass a
   mismatch or edit generated headers.
4. Build the same Windows preset and check the presenter values. Then check the
   hardware adapter with one electrode at a time. A correct preview does not prove
   the COM/SEG wiring. Keep these tests in the application, outside the FSM.

The current editor imports expanded polygon definitions. Reusable visual pattern
placement and a graphical wiring editor are future work; this file is the current
explicit contract.

## Segment naming reference

These descriptive names are reading aids, not extra JSON properties. Cell numbers
are zero based and run left to right. This table covers every polygon in the
source definition. The COM/SEG columns refer to the glass, before A03 reversal.

| Descriptive name | Logical ID | Glass COM | Glass SEG |
| --- | --- | --- | --- |
| `label.cell0.top` | 121 | 6 | 1 |
| `label.cell0.upperRight` | 122 | 6 | 2 |
| `label.cell0.lowerRight` | 42 | 2 | 2 |
| `label.cell0.bottom` | 2 | 0 | 2 |
| `label.cell0.lowerLeft` | 1 | 0 | 1 |
| `label.cell0.upperLeft` | 81 | 4 | 1 |
| `label.cell0.middleLeft` | 61 | 3 | 1 |
| `label.cell0.upperLeftDiagonal` | 101 | 5 | 1 |
| `label.cell0.upperCenter` | 82 | 4 | 2 |
| `label.cell0.upperRightDiagonal` | 102 | 5 | 2 |
| `label.cell0.middleRight` | 62 | 3 | 2 |
| `label.cell0.lowerRightDiagonal` | 22 | 1 | 2 |
| `label.cell0.lowerCenter` | 41 | 2 | 1 |
| `label.cell0.lowerLeftDiagonal` | 21 | 1 | 1 |
| `label.cell1.top` | 123 | 6 | 3 |
| `label.cell1.upperRight` | 124 | 6 | 4 |
| `label.cell1.lowerRight` | 44 | 2 | 4 |
| `label.cell1.bottom` | 4 | 0 | 4 |
| `label.cell1.lowerLeft` | 3 | 0 | 3 |
| `label.cell1.upperLeft` | 83 | 4 | 3 |
| `label.cell1.middleLeft` | 63 | 3 | 3 |
| `label.cell1.upperLeftDiagonal` | 103 | 5 | 3 |
| `label.cell1.upperCenter` | 84 | 4 | 4 |
| `label.cell1.upperRightDiagonal` | 104 | 5 | 4 |
| `label.cell1.middleRight` | 64 | 3 | 4 |
| `label.cell1.lowerRightDiagonal` | 24 | 1 | 4 |
| `label.cell1.lowerCenter` | 43 | 2 | 3 |
| `label.cell1.lowerLeftDiagonal` | 23 | 1 | 3 |
| `label.cell2.top` | 125 | 6 | 5 |
| `label.cell2.upperRight` | 126 | 6 | 6 |
| `label.cell2.lowerRight` | 46 | 2 | 6 |
| `label.cell2.bottom` | 6 | 0 | 6 |
| `label.cell2.lowerLeft` | 5 | 0 | 5 |
| `label.cell2.upperLeft` | 85 | 4 | 5 |
| `label.cell2.middleLeft` | 65 | 3 | 5 |
| `label.cell2.upperLeftDiagonal` | 105 | 5 | 5 |
| `label.cell2.upperCenter` | 86 | 4 | 6 |
| `label.cell2.upperRightDiagonal` | 106 | 5 | 6 |
| `label.cell2.middleRight` | 66 | 3 | 6 |
| `label.cell2.lowerRightDiagonal` | 26 | 1 | 6 |
| `label.cell2.lowerCenter` | 45 | 2 | 5 |
| `label.cell2.lowerLeftDiagonal` | 25 | 1 | 5 |
| `label.cell3.top` | 127 | 6 | 7 |
| `label.cell3.upperRight` | 128 | 6 | 8 |
| `label.cell3.lowerRight` | 48 | 2 | 8 |
| `label.cell3.bottom` | 8 | 0 | 8 |
| `label.cell3.lowerLeft` | 7 | 0 | 7 |
| `label.cell3.upperLeft` | 87 | 4 | 7 |
| `label.cell3.middleLeft` | 67 | 3 | 7 |
| `label.cell3.upperLeftDiagonal` | 107 | 5 | 7 |
| `label.cell3.upperCenter` | 88 | 4 | 8 |
| `label.cell3.upperRightDiagonal` | 108 | 5 | 8 |
| `label.cell3.middleRight` | 68 | 3 | 8 |
| `label.cell3.lowerRightDiagonal` | 28 | 1 | 8 |
| `label.cell3.lowerCenter` | 47 | 2 | 7 |
| `label.cell3.lowerLeftDiagonal` | 27 | 1 | 7 |
| `label.cell4.top` | 149 | 7 | 9 |
| `label.cell4.upperRight` | 130 | 6 | 10 |
| `label.cell4.lowerRight` | 50 | 2 | 10 |
| `label.cell4.bottom` | 10 | 0 | 10 |
| `label.cell4.lowerLeft` | 29 | 1 | 9 |
| `label.cell4.upperLeft` | 109 | 5 | 9 |
| `label.cell4.middleLeft` | 89 | 4 | 9 |
| `label.cell4.upperLeftDiagonal` | 129 | 6 | 9 |
| `label.cell4.upperCenter` | 90 | 4 | 10 |
| `label.cell4.upperRightDiagonal` | 110 | 5 | 10 |
| `label.cell4.middleRight` | 70 | 3 | 10 |
| `label.cell4.lowerRightDiagonal` | 30 | 1 | 10 |
| `label.cell4.lowerCenter` | 69 | 3 | 9 |
| `label.cell4.lowerLeftDiagonal` | 49 | 2 | 9 |
| `label.cell5.top` | 151 | 7 | 11 |
| `label.cell5.upperRight` | 132 | 6 | 12 |
| `label.cell5.lowerRight` | 52 | 2 | 12 |
| `label.cell5.bottom` | 12 | 0 | 12 |
| `label.cell5.lowerLeft` | 31 | 1 | 11 |
| `label.cell5.upperLeft` | 111 | 5 | 11 |
| `label.cell5.middleLeft` | 91 | 4 | 11 |
| `label.cell5.upperLeftDiagonal` | 131 | 6 | 11 |
| `label.cell5.upperCenter` | 92 | 4 | 12 |
| `label.cell5.upperRightDiagonal` | 112 | 5 | 12 |
| `label.cell5.middleRight` | 72 | 3 | 12 |
| `label.cell5.lowerRightDiagonal` | 32 | 1 | 12 |
| `label.cell5.lowerCenter` | 71 | 3 | 11 |
| `label.cell5.lowerLeftDiagonal` | 51 | 2 | 11 |
| `label.cell6.top` | 153 | 7 | 13 |
| `label.cell6.upperRight` | 134 | 6 | 14 |
| `label.cell6.lowerRight` | 54 | 2 | 14 |
| `label.cell6.bottom` | 14 | 0 | 14 |
| `label.cell6.lowerLeft` | 33 | 1 | 13 |
| `label.cell6.upperLeft` | 113 | 5 | 13 |
| `label.cell6.middleLeft` | 93 | 4 | 13 |
| `label.cell6.upperLeftDiagonal` | 133 | 6 | 13 |
| `label.cell6.upperCenter` | 94 | 4 | 14 |
| `label.cell6.upperRightDiagonal` | 114 | 5 | 14 |
| `label.cell6.middleRight` | 74 | 3 | 14 |
| `label.cell6.lowerRightDiagonal` | 34 | 1 | 14 |
| `label.cell6.lowerCenter` | 73 | 3 | 13 |
| `label.cell6.lowerLeftDiagonal` | 53 | 2 | 13 |
| `value.cell0.top` | 18 | 0 | 18 |
| `value.cell0.upperRight` | 58 | 2 | 18 |
| `value.cell0.lowerRight` | 118 | 5 | 18 |
| `value.cell0.bottom` | 138 | 6 | 18 |
| `value.cell0.lowerLeft` | 98 | 4 | 18 |
| `value.cell0.upperLeft` | 38 | 1 | 18 |
| `value.cell0.middle` | 78 | 3 | 18 |
| `value.cell1.top` | 17 | 0 | 17 |
| `value.cell1.upperRight` | 57 | 2 | 17 |
| `value.cell1.lowerRight` | 117 | 5 | 17 |
| `value.cell1.bottom` | 137 | 6 | 17 |
| `value.cell1.lowerLeft` | 97 | 4 | 17 |
| `value.cell1.upperLeft` | 37 | 1 | 17 |
| `value.cell1.middle` | 77 | 3 | 17 |
| `value.cell2.top` | 16 | 0 | 16 |
| `value.cell2.upperRight` | 56 | 2 | 16 |
| `value.cell2.lowerRight` | 116 | 5 | 16 |
| `value.cell2.bottom` | 136 | 6 | 16 |
| `value.cell2.lowerLeft` | 96 | 4 | 16 |
| `value.cell2.upperLeft` | 36 | 1 | 16 |
| `value.cell2.middle` | 76 | 3 | 16 |
| `value.cell3.top` | 15 | 0 | 15 |
| `value.cell3.upperRight` | 55 | 2 | 15 |
| `value.cell3.lowerRight` | 115 | 5 | 15 |
| `value.cell3.bottom` | 135 | 6 | 15 |
| `value.cell3.lowerLeft` | 95 | 4 | 15 |
| `value.cell3.upperLeft` | 35 | 1 | 15 |
| `value.cell3.middle` | 75 | 3 | 15 |
| `ringPiece0` | 140 | 7 | 0 |
| `ringPiece1` | 147 | 7 | 7 |
| `ringPiece2` | 146 | 7 | 6 |
| `ringPiece3` | 145 | 7 | 5 |
| `ringPiece4` | 144 | 7 | 4 |
| `ringPiece5` | 143 | 7 | 3 |
| `ringPiece6` | 142 | 7 | 2 |
| `ringPiece7` | 141 | 7 | 1 |
