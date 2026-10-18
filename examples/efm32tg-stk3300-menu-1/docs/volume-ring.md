# Advanced lesson: show volume on the glass ring

The basic menu already runs on Windows and the Tiny Gecko. Now we will connect
one of the fixed symbols to application state. We will use the eight-piece ring
to show volume. The menu already owns a saved value and an edit value, so we do
not need another state machine or a new menu event.

## 1. Identify the field on the glass

This is a segmented display. We cannot move, resize or draw a new ring on the
hardware. We can turn its eight existing pieces on or off. The Windows drawing
shows those same pieces so we can test their behavior before using the board.

The original A03 schematic names the pieces A0 through A7. A7 is at the top;
A0 is at the upper right. For a clockwise indicator starting at twelve o'clock,
use the order A7, A0, A1, A2, A3, A4, A5, A6.

| Clockwise position | Glass symbol | Glass COM | Glass S | Controller COM / SEG |
| --- | --- | --- | --- | --- |
| 1, top | A7 | 7 | 0 | 0 / 0 |
| 2 | A0 | 7 | 7 | 0 / 7 |
| 3 | A1 | 7 | 6 | 0 / 6 |
| 4 | A2 | 7 | 5 | 0 / 5 |
| 5 | A3 | 7 | 4 | 0 / 4 |
| 6 | A4 | 7 | 3 | 0 / 3 |
| 7 | A5 | 7 | 2 | 0 / 2 |
| 8 | A6 | 7 | 1 | 0 / 1 |

The mapping comes from [the A03 schematic, page 26](https://docs.rs-online.com/30b1/0900766b814a5093.pdf#page=26).
The SDK's STK3300 ring mapping corroborates the controller coordinates. Glass
coordinates and controller coordinates are different here because the board
reverses the common lines. Keep that translation in the hardware adapter.

## 2. Name the field and define its scale

Open `UI/glass.hpp`. `kVolumeRing` lists the eight logical electrodes in display
order. A compile-time check rejects duplicate electrodes or an overlap with a
character or digit. The designer glass definition gives `volumeRing` the same
ordered IDs and a maximum of 100. Its generated encoder now writes these bits.
The original `setVolumeRing` helper remains in `Tests/reference_encoder.hpp` only
for regression comparisons.

We chose the nearest eighth, rounded half up:

```cpp
const auto count = (percent * 8U + 50U) / 100U;
```

The generated encoder validates 0 through 100 before returning a complete frame.
Off bits are cleared, so lowering volume clears old pieces. Invalid input returns
an empty optional and the application retains its previous valid frame.

| Volume | Pieces lit |
| --- | ---: |
| 0â€“6 | 0 |
| 7â€“18 | 1 |
| 19â€“31 | 2 |
| 32â€“43 | 3 |
| 44â€“56 | 4 |
| 57â€“68 | 5 |
| 69â€“81 | 6 |
| 82â€“93 | 7 |
| 94â€“100 | 8 |

The numeric value remains exact on the Volume page. The ring is a coarse
indicator; it will not change on every one-point adjustment. A nonzero value
below 7 still has an empty ring under this policy. Choose another quantization
policy if your product needs every nonzero value to light a piece.

## 3. Bind the ring to state in the presenter

Open `UI/presenter.hpp`. `Presentation` now carries `volumePercent` alongside
the label and numeric field. Most pages pass `menu.volume()`. The Volume editor
passes `menu.draft()` instead:

```cpp
case Page::volume:
    return {"VOLUME", menu.draft(), menu.draft()};
```

The ring remains visible on Home, Setup and Info. It always means volume, even
when the numeric field means playback state or the kit identifier. Do not bind
the ring to that numeric field: INFO's value is 3300, not a volume percentage.

The existing FSM handles commit and cancel. PB0 saves the draft; PB1 discards it.
When the presenter reads the next state, the ring follows automatically. There
is no ring callback, and the FSM does not include a display header.

## 4. Encode one complete presentation

`encodePresentation(view)` passes the label, value and volumePercent to
`lithe_generated::screen_home::encode`. It returns a complete frame or failure. The old visible frame
is not partially updated if a label, number or ring value is invalid.

Both `Platform/Win32/main.cpp` and `Platform/EFM32/main.cpp` call:

```cpp
const auto view = tiny::present(menu);
const auto frame = tiny::encodePresentation(view);
```

The display frame is still 20 bytes. We are using previously unused electrodes,
not allocating another framebuffer or storing a bitmap.

## 5. Show the same bits on both platforms

Windows draws eight authored polygons in the ring's order. Each polygon reads
one bit from the frame; the renderer does not calculate the volume or its scale.
The hardware driver uses the existing `lcdRows()` conversion and LCD write path.
No change to the FSM, capacitive driver or LCD driver is required.

Use the same `win32-debug` and `efm32-debug` presets and launchers as the basic
lesson. Adding a field does not create another build configuration.

1. Build Windows and run the four tests. Start the simulator.
2. At startup, volume is 50 and four pieces are lit, starting at the top.
3. Enter VOLUME. Move above 56 and below 44 to see the next pieces appear/disappear.
4. Change the draft, then PB1. Reopen VOLUME: the saved value and ring are restored.
5. Change it again, then PB0. Visit Setup and return: the ring retains the saved value.
6. Check 0 and 100. The ring must become completely empty and completely full.
7. Build and load the Tiny Gecko version. Leave the slider untouched for the first
   second, then repeat the same checks. Check the physical order as well as the count.

`Tests/segment_checks.cpp` checks all 101 input values against an independent
electrode/threshold table, invalid input, stale-bit clearing, preservation of
unrelated bits, draft, cancel and save. `hardware_contract_checks.cpp` verifies
the expected controller bits. The Windows integration test exercises the actual
key-message path and checks the resulting ring frame. Physical acceptance is
recorded separately in `VALIDATION.md`.

## Continue in the visual designer

Follow [Describe the glass, then bind the menu values](segmented-designer.md).
The plug-in now opens `UI/designer/forms/home.litheview.xml`, previews native
segment bits, edits bindings and Configure parameters, and generates the encoder
used by both targets. No raster display profile or bitmap of the glass is needed.

The menu still belongs to the application. The field supplies a value-to-segment
mapping; it does not own volume, navigation, a timer or a hardware input callback.
