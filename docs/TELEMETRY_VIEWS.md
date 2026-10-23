# Plot, axis, overlay, meter and waterfall views

`PlotView`, `AxisView`, `MarkerRangeView`, `MeterView` and `WaterfallView` are portable drawing elements for measurements,
instrumentation, audio and spectrum displays. They do not acquire samples, run a
radio DSP pipeline, own a timer or select a device. An application publishes a
completed snapshot on its UI thread and chooses when to render and present it.
Both views draw through `Painter`, so the destination can be RGB565, a different
enabled color writer, or a constrained native display. A display that cannot
represent the requested palette will quantize through its normal destination
writer; the views do not create an RGB staging framebuffer.

## Trace

`PlotView` borrows up to 4096 `uint16_t` samples. `setRange(minimum, maximum)`
defines the vertical interval, with the minimum at the bottom. Values outside
it are clipped. `setMode` chooses points, connected lines or both.
`setSamples(span)` switches to a complete caller-owned snapshot
and invalidates the view; the span must stay alive and unmodified until it is
replaced. Empty input clears the trace. A producer can maintain two fixed arrays
and publish the one it has finished writing. The view draws connected lines
across its width and does no allocation. Optional bounded grid divisions and a
sample-index cursor are overlays; application code chooses calibration, units
and marker meaning.

`setSecondarySamples(span, color)` adds one optional trace over the same
vertical range and normalized horizontal axis. The secondary trace has its own
caller-owned snapshot and can have a different sample count. It draws beneath
the primary trace so crossings stay visible. Passing an empty span removes it.
The view neither computes nor retains peaks: an SDR can publish a peak-hold
array, while a temperature instrument can publish a reference sensor or limit
curve. Both inputs are capped at 4096 samples and invalid input leaves the
previous snapshot unchanged.

## Axis and scale

`AxisView` draws a separate horizontal or vertical scale. Its application-owned
`AxisTick` array contains normalized positions from 0 to 65535 and borrowed
labels. On a horizontal axis, zero is the left endpoint; on a vertical axis,
zero is the bottom endpoint. `setTicks` accepts at most 16 strictly ascending
positions with labels no longer than 16 bytes. Invalid input leaves the
previous scale in place. The view does not compute calibration, choose units,
or format numbers; those are application policy. The caller keeps the array
and label storage alive until a replacement is published on the UI thread.
Place an axis directly beside a plot, waterfall, gauge or map, with matching
width or height. `setOrientation` selects horizontal or vertical layout.

The [H750 SDR example](../examples/stm32h750b-dk-sdr/README.md) uses an axis
under its plot. Tuning changes its center label, and changing span changes the
endpoint labels. The same control can show seconds, degrees, pressure, or any
other application-supplied values. Designer preview shows sample labels to
make the geometry visible; generated code expects the application to publish
the actual ticks.

## Markers and ranges

`MarkerRangeView` is a transparent layer that can share a plot or waterfall's
frame. The application supplies up to eight ordered, nonoverlapping ranges and
16 ordered markers. Their positions use the same normalized 0..65535
left-to-right coordinate space as the horizontal axis. Ranges carry a color
that may be translucent; markers carry a line color and an optional label of
at most 16 bytes. The arrays and label text are borrowed. Invalid or oversized
input is rejected without replacing the last published data. Put the overlay
after the underlying view in the form so it draws on top. The control knows
nothing about passbands, alarm limits, frequency or sample acquisition.

The SDR example draws a fixed highlighted analysis region, a center marker,
and a second marker that follows its synthetic signal. The application
publishes the moving marker from one of two fixed arrays so the previously
published array is not changed while the view may read it.

## Calibrated meter

`MeterView` accepts a signed integer minimum, maximum and reading in the
application's units. It draws a bar whose length uses the configured range,
plus up to eight application-owned tick labels and eight ordered color zones.
No floating-point conversion or display-specific buffer is needed. Invalid
scale or zone settings leave the previous settings in place. An out-of-range
reading remains available through `reading()` while its bar saturates at the
nearest endpoint; the application decides whether to flag that condition.

The SDR screen maps its **synthetic** amplitude to an illustrative -120..0
meter scale. Those numbers are a UI demonstration, not calibrated RF power.
Other applications can supply engineering units and zone thresholds from
their own measurement model.

## Waterfall

`WaterfallView` borrows storage for a fixed number of columns and history rows.
Each intensity is an 8-bit application value; `pushRow` uses its high nibble as
an index into a borrowed 16-color palette. History stores two four-bit indices
per byte, padding each row to a byte boundary. Call `requiredBytes(columns,
rows)` to size the caller-owned array. For example, 400 columns by 100 rows
requires 20,000 bytes. This is history storage, not a full-color framebuffer.
The newest row appears at the top, older rows move visually downward and the
oldest is overwritten at capacity. `pushRow` rejects a short or long row without
changing history. `clear` discards the logical history. Palette changes use
`setPalette` with exactly 16 colors.

For a 400 by 100 waterfall, the owning UI object can declare its storage and
views without a heap:

```cpp
static constexpr std::int32_t columns = 400;
static constexpr std::int32_t rows = 100;
std::array<std::uint8_t, WaterfallView::requiredBytes(columns, rows)> history{};
std::array<Color, WaterfallView::kPaletteSize> palette{};
palette.fill(Color::rgb(0x000000U));
palette[15] = Color::rgb(0x00ffffU);
Style style{};
style.background = Color::rgb(0x000000U);
style.foreground = Color::rgb(0x00ff00U);
WaterfallView waterfall{{0, 80, columns, rows}, history, columns, rows, palette, style};
PlotView trace{{0, 0, columns, 80}, style};
```

The presenter receives completed values, calls `trace.setSamples(samples)` and
`waterfall.pushRow(intensities)`, then lets the ordinary view tree render. The
arrays and palette belong to that UI object and must outlive the views.

Both buffers and the palette must outlive their views. Publish from the UI
thread; an ISR or acquisition task should signal or queue a completed snapshot
for the application presenter, not mutate a buffer while rendering reads it.
The application owns thresholds, frequency calibration, units, cursor behavior,
menu navigation and any sensor or DSP state machine.

The current implementation invalidates and redraws the whole waterfall when a
row arrives. That is correct for a bounded first slice and suitable for proving
the contract on a host. Before using a large, fast waterfall on a target, measure
its render time and display transfer, then add a destination-aware row-scroll or
tile update path if required. Fixed storage prevents RAM growth but does not by
itself guarantee a frame-rate budget.

## Design direction from instrument references

The [H750 SDR display example](../examples/stm32h750b-dk-sdr/README.md)
composes the first two primitives with ordinary labels, a signal meter and
buttons. Its synthetic source proves the UI contract without claiming an RF
receiver. Additional SDR and telemetry references suggest distinct follow-up
pieces: interactive cursor selection and a color legend. These should compose
with plots and histories rather than turn `PlotView` into a radio-specific
widget. Trend charts can reuse the axis and trace pieces. Maps, geographic
overlays and 3D spectra need separate data models, memory budgets and rendering
contracts; they are not properties of a 2D waterfall.

The designer now exposes Plot, Axis, Marker and Range, Calibrated Meter, and Waterfall as palette widgets. The XML owns
their bounds and common colors; generated named accessors let the application
publish trace snapshots and bind caller-owned packed history and a palette.
The native designer preview supplies bounded synthetic samples solely to show
the layout. It does not generate acquisition code or application buffers.
Plot range, grid and cursor, and the waterfall palette remain presenter-set
properties; a later slice can expose those in the property editor and account
for the history buffer in target RAM estimates.
