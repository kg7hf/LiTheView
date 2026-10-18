# Segmented designer compiler consumer

This test compiles real designer-generated C++ against `LitheView::Segments`.
It does not link the raster renderer, FreeRTOS or a vendor SDK. It compares the
generated encoder with the retained test-only handwritten reference; the Tiny
Gecko runtime now consumes generated code and its FSM remains unchanged.

Segmented projects now support the VS Code editor, native preview, Configure and
multi-screen generation. The loose-file commands below remain useful for testing
the encoder independently. See the [Tiny Gecko designer walkthrough](../../examples/efm32tg-stk3300-menu-1-complete/docs/segmented-designer.md)
for the project workflow.

## Generate and build

Run from the LiTheView checkout root. Use the .NET SDK specified by
`tools/designer/global.json`. `$Dotnet` may be a full path to that SDK's host.
The Windows commands below use MinGW GCC and Ninja available on PATH. Select
their actual installations on your machine; no personal tool path is required.

```powershell
$Dotnet = 'dotnet'
& $Dotnet build tools/designer/src/LitheView.Designer.Cli --nologo -m:1
$Cli = 'tools/designer/src/LitheView.Designer.Cli/bin/Debug/net10.0/LitheView.Designer.Cli.dll'
$Tiny = 'examples/efm32tg-stk3300-menu-1-complete/UI/design'
$Small = 'tests/designer-fixtures/segmented-icons'
$Generated = "$PWD/build/segmented-designer/generated"

& $Dotnet $Cli segment check "$Tiny/glass.segment-display.json" "$Tiny/presentation.litheview.xml"
& $Dotnet $Cli segment export-cpp "$Tiny/glass.segment-display.json" "$Tiny/presentation.litheview.xml" "$Generated/tiny.hpp"
& $Dotnet $Cli segment export-cpp "$Small/glass.segment-display.json" "$Small/status.litheview.xml" "$Generated/status.hpp"
& $Dotnet $Cli segment export-cpp "$Small/glass.segment-display.json" "$Small/piece.litheview.xml" "$Generated/piece.hpp"

cmake -S tests/segmented_designer_consumer -B build/segmented-designer/consumer -G Ninja `
  "-DCMAKE_CXX_COMPILER=$((Get-Command g++).Source)" "-DGENERATED_DIRECTORY=$Generated"
cmake --build build/segmented-designer/consumer
ctest --test-dir build/segmented-designer/consumer --output-on-failure
```

Check each command's exit code before continuing. The CMake consumer can also use
another C++23 compiler. The generated C++ has no Windows dependency.

## The source contracts

[Glass schema](../../schema/segment-glass.schema.json) version 1 describes:

- `segmentCount`: the bounded frame capacity, not the number of pixels.
- `viewWidth`, `viewHeight`, `shapes`: host drawing geometry. Each shape maps a
  polygon to a logical segment ID. No images, scripts or MCU registers are stored.
- `icons`: one or more segment IDs controlled together by a Boolean.
- `groups`: ordered segment IDs and a positive maximum. Version 1 scales 0 to
  maximum to the segment count using nearest rounding, with half rounded up.
- `texts`: ordered cells, each listing electrode IDs in stroke order, and an
  explicit printable ASCII glyph map. Text is left aligned; unused cells are off.
- `numbers`: the same cell/glyph description, requiring digits 0-9. Version 1
  uses unsigned decimal, right alignment, no leading zeros and a visible zero.

All referenced electrodes need geometry. IDs may be sparse. Two field definitions
may describe the same electrodes, such as a ring and its individual pieces.
A form cannot bind overlapping definitions at the same time.

The Tiny Gecko definition has 160 logical bits, 134 drawn electrodes, seven
character cells, four numeric cells and eight ring pieces. It describes the
currently implemented subset of the glass. Other physical symbols are not yet
mapped. Its authored schematic geometry comes from the example's own Windows
renderer, not a copied vendor image.

[Form schema](../../schema/segmented-form.xsd) version 1 describes the glass
identity, its canonical SHA-256, and named bindings. It describes no menu states,
transitions, hardware input or tasks. For example:

```xml
<bind field="volumeRing" input="volumePercent" />
```

The binding becomes `Values::field_volumePercent`. Text becomes `std::string_view`,
numbers/groups become `std::uint32_t`, and icons become `bool`. Prefixing generated
names avoids C++ keyword collisions without changing application input names.
The form ID chooses the namespace, such as `lithe_generated::screen_presentation`.
Headers for separate forms can be included together.

```cpp
const auto model = tiny::present(menu); // Handwritten, application-owned.
const auto frame = lithe_generated::screen_presentation::encode(
    {model.label, model.value, model.volumePercent});
if (frame)
{
    // Submit *frame to the consuming application's display adapter.
}
else
{
    // Report the bad presentation and retain the last valid frame.
}
```

Encoding consumes borrowed text synchronously and retains no references. Every
successful result defines a complete frame. Invalid text, numbers or group values
return an empty optional; no partially written frame escapes. Geometry is not
included in the generated embedded header.

To intentionally change the glass, validate it and obtain its new hash:

```powershell
& $Dotnet $Cli segment glass-hash "$Tiny/glass.segment-display.json"
```

Review the changed definition before updating `glassSha256` in the form. A stale
hash rejects generation. Canonical JSON uses typed property order, sorted shapes
and field/glyph names, compact UTF-8 and no trailing newline. Semantic ordered
arrays are retained. The generated header also records the exact XML source hash.

Export validates all inputs before writing one `.hpp` file with a same-directory
temporary-file replacement. An existing destination must have the matching
generator ownership line; handwritten files and other forms' outputs are refused.
Reparse paths are refused. This loose-file CLI does not claim the application-root
path policy or multi-form transaction handling provided by `segment export-project`.

The second executable exercises the native preview protocol parser with sparse
icons, groups and text, compares electrode bytes and host pixels, and rejects
truncated payloads, invalid fields and trailing bytes. The service suite additionally
checks all 101 Tiny Gecko ring values and the other electrodes against an independent
A03 mapping oracle. Preview expansion occurs after native electrode encoding.

## What the tests establish

- Tiny Gecko output matches the accepted handwritten encoder for all 101 volume
  values, all 10,000 numeric values, the supported glyphs and 65,536 event histories.
- A separate literal threshold/electrode oracle checks the ring, independently of
  either encoder. Invalid input is rejected.
- A synthetic 17-bit glass proves sparse numbering, Boolean multi-electrode icons,
  individually addressed pieces and an odd group maximum. Unused bits remain off.
- Designer Core tests reject duplicate/unknown JSON properties, null entries,
  bad mappings, malformed XML, external entities, overlapping writers, stale hashes
  and unsafe overwrite attempts. Output is culture-independent and contains no
  generated copyright claim over application content.

These are host compiler and behavior checks. The generated encoder now drives
both example platforms; fresh hardware observations are recorded separately in
[the example validation report](../../examples/efm32tg-stk3300-menu-1-complete/VALIDATION.md). See
[the implementation plan](../../docs/SEGMENTED_DESIGNER_PLAN.md) for the remaining
runtime integration and hardware acceptance stages.
