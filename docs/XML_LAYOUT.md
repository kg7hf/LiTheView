# XML layout contract

XML parsing is part of the package. Storage and transport are not.

`XmlLayoutParser::parse()` takes a `std::string_view` and a caller-owned `std::span<ViewSpec>`. It
performs no heap allocation and no I/O. `StaticLayout<N>::build()` constructs the requested views in
fixed aligned storage and resolves optional fonts/bitmaps through a caller-supplied
`ResourceProvider`.

```cpp
constexpr char menuXml[] = R"xml(
<object className="LCDVIEW" frame="0,0,320,240" runtimeID="1">
  <object className="LCDLABELVIEW" frame="8,8,304,32"
          runtimeID="10" text="MENU" textAlignmentFlags="14"/>
  <object className="LCDBUTTONVIEW" frame="8,52,304,36"
          runtimeID="101" text="OPEN" detail="XML"/>
</object>
)xml";

std::array<lithe::ViewSpec, 8> specs{};
auto parsed = lithe::XmlLayoutParser::parse(menuXml, specs);
lithe::StaticLayout<8> layout{};
if (!parsed || !layout.build(std::span<const lithe::ViewSpec>{specs}.first(parsed.nodeCount))) {
    // Inspect parsed.error/offset or LayoutResult.error/node.
}
```

Both the XML bytes and resource data must outlive the built layout because strings, fonts, and bitmaps
are non-owning. `constexpr` arrays and generated ROM headers naturally satisfy that rule.

`StaticLayout<N>` can build every widget kind, which is what XML parsed at run time needs. A product
that knows which kinds its layouts use can name them, and then links only that widget code:

```cpp
using MenuWidgets = lithe::WidgetSet<lithe::ViewKind::panel, lithe::ViewKind::label,
                                     lithe::ViewKind::button>;
lithe::StaticLayout<8, MenuWidgets> layout{};
```

`build()` then reports `LayoutError::invalidKind` at the first node of any other kind, before
resolving that node's resources, and leaves the layout empty. Designer-generated forms declare their
exact set as `Form::Widgets` and the matching layout type as `Form::Layout`.

## Elements and classes

The legacy form is `<object className="...">`:

| Legacy class | View kind |
| --- | --- |
| `LCDVIEW` | panel |
| `LCDSCROLLVIEW` | scroll container |
| `LCDTABLEVIEW` | scrollable table/container |
| `LCDLABELVIEW`, `LCDTEXTVIEW` | label |
| `LCDBUTTONVIEW` | button |
| `LCDPROGRESSVIEW` | progress |
| `LCDIMAGEVIEW` | image or frame animation when `animationImageN` is present |
| `LCDICONVIEW` | centered icon |
| `LCDLINEVIEW` | horizontal or vertical rule |
| `LCDPICKERVIEW` | delimiter-backed choice picker |
| `LCDCHECKBOXVIEW` | explicit Boolean checkbox |
| `LCDRADIOBUTTONVIEW` | radio-style Boolean choice |
| `LCDTEXTFIELDVIEW` | bounded editor over an application-owned buffer |
| `LCDSOFTKEYVIEW` | left/right softkey bar |
| `LCDTABBARVIEW` | equal-width tab strip |
| `LCDTABVIEW` | tab strip whose direct children are the pages |
| `LCDWHEELPICKERVIEW` | vertical scroll wheel with the selection centered in a band |

The concise elements `<view>`, `<panel>`, `<label>`, `<button>`, `<progress>`, `<image>`, `<icon>`,
`<line>`, `<scroll>`, `<table>`, `<picker>`, `<checkbox>`, `<radio>`, `<textfield>`/`<text-field>`,
`<softkey>`, `<tabbar>`/`<tab-bar>`, `<tabview>`/`<tab-view>`, and `<wheelpicker>`/`<wheel-picker>` are
also accepted. Names are case-sensitive.
Elements may be self-closing or contain child elements. XML comments and processing instructions are
skipped.

## Attributes

| Attribute | Meaning |
| --- | --- |
| `frame="x,y,width,height"` | Frame relative to the parent; width and height must be positive. |
| `runtimeID`, `id` | Unsigned 16-bit view/action identifier. |
| `backgroundColor` | Legacy encoded background color. |
| `textColor`, `foregroundColor` | Legacy encoded foreground color. |
| `accentColor`, `borderColor` | Legacy encoded widget colors. |
| `borderWidth` | 0 through 16 pixels. |
| `textAlignmentFlags` | Original 4-bit horizontal/vertical alignment encoding. |
| `textScale` | Integer built-in-font scale from 1 through 8. |
| `text`, `detail` | Non-owning text shown by labels/buttons. |
| `value` | Progress value from 0 through 100. |
| `imageName`, `resource` | Bitmap name resolved by `ResourceProvider`. |
| `font` | Font name resolved by `ResourceProvider`. |
| `contentHeight`, `scrollOffset`, `scrollStep` | Scroll/table extent and initial position. |
| `showsIndicator` | Enables the compact scroll indicator. |
| `items` | Picker choices, tab titles, or wheel items separated by `|`; the original string remains in ROM. |
| `selectedIndex`, `wrap` | Initial picker item, tab, or wheel item, and whether stepping wraps at the ends. |
| `tabHeight` | Tab-view strip height, 8 through 255 pixels (default 32). |
| `rowHeight` | Wheel-picker row height, 12 through 255 pixels (default 28). |
| `checked`, `selected` | Initial checkbox, toggle-button, or radio state. |
| `behavior` | Button behavior: `momentary` (default, non-latching) or `toggle` (latching). |
| `icon` | Optional button bitmap name resolved by `ResourceProvider`. |
| `focusedBackgroundColor`, `focusedForegroundColor` | Optional focused-state colors; need state-style storage. |
| `pressedBackgroundColor`, `pressedForegroundColor` | Optional pointer-pressed colors; need state-style storage. |
| `disabledBackgroundColor`, `disabledForegroundColor` | Optional disabled colors; need state-style storage. |
| `selectedBackgroundColor`, `selectedForegroundColor` | Optional selected-state colors for a latched toggle, checked checkbox, or selected radio button. |
| `binding` | Text-field buffer name resolved by `ResourceProvider::textBuffer()`. |
| `placeholder`, `maxLength` | Empty-field hint and maximum UTF-8 byte count. |
| `left_text`, `right_text` | Softkey labels. |
| `leftAction`, `rightAction` | Softkey action IDs. |
| `animationInterval` | Frame interval in milliseconds, 1 through 65,535. |
| `animationImage0` ... `animationImageN` | Consecutive ordered bitmap names in the historical dialect. |
| `repeatIndex` | Frame to return to after the final frame; supports intro-then-loop sequences. |
| `autoplay` | Starts/stops the application-clocked animation. |
| `hide` | Boolean; inverted to visibility. |
| `enabled`, `canFocus` | Boolean interaction flags. |

Boolean values are `0`, `1`, `true`, `false`, `TRUE`, or `FALSE`. Integers may be decimal or `0x`
hexadecimal. The legacy color reader preserves the original byte ordering: low byte red, then green,
blue, and an inverted high-byte transparency value. `0x00BBGGRR` is therefore opaque.

Unknown attributes are ignored for compatibility with historical metadata such as wireframe IDs and
product string identifiers. Unknown elements/classes, invalid attribute values, over-capacity output,
nesting deeper than 32, and mismatched/unclosed elements return explicit errors.

Pass `XmlParseOptions{true}` for build validation. Production-generated XML
should be validated in this strict mode; compatibility mode exists for incremental import of historical
screens.

Strict mode also rejects button state that the node cannot represent: `checked="1"` on a momentary
button, and `behavior` on any non-button node. Compatibility mode keeps importing such historical XML;
the built momentary button simply remains unselected. A state-color attribute requires a caller-owned
`WidgetStateStyle` entry for that node through `XmlParseOptions`; see
[THEMES_AND_WIDGET_STATES.md](THEMES_AND_WIDGET_STATES.md). Button variants are described in
[BUTTONS.md](BUTTONS.md).

Strict tab bars and tab views need 1 through 16 titles and a `selectedIndex` that names one of them,
and `tabHeight` is rejected on every other node; compatibility mode clamps the selection. Tab-view
pages use the tab view's coordinates and normally start at `tabHeight`. See [TABS.md](TABS.md).

Strict wheel pickers need 1 through 255 items and a `selectedIndex` that names one of them, and a
non-default `rowHeight` is rejected on every other node; compatibility mode clamps the selection. A
`rowHeight` below 12 is rejected in both modes. See [WHEEL_PICKER.md](WHEEL_PICKER.md).

## Animation frame storage

Historical animation frame attributes are separate strings in the XML. To avoid putting a 100-pointer
array into every `ViewSpec`, the caller supplies one shared frame-name arena:

```cpp
std::array<lithe::ViewSpec, 8> specs{};
std::array<std::string_view, 16> frames{};
lithe::XmlParseOptions options{};
options.rejectUnknownAttributes = true;
options.animationFrames = frames;

const auto parsed = lithe::XmlLayoutParser::parse(animationXml, specs, options);
lithe::StaticLayout<8> layout{};
const auto built = layout.build(
    std::span<const lithe::ViewSpec>{specs}.first(parsed.nodeCount),
    lithe::font5x7(),
    &resources,
    std::span<const std::string_view>{frames}.first(parsed.animationFrameCount));
```

Frame numbers must be consecutive from zero. A missing arena, gap, invalid repeat index, or exhausted
arena fails explicitly. The arena and XML bytes must outlive the built animation view. The application
calls `Application::advanceTime(elapsedMs)` only when appropriate; the package owns no timer or thread.

## Editable text storage

A text field does not allocate or own settings. XML names a binding and the application resolves that
name to a mutable, NUL-terminated buffer:

```cpp
class Resources final : public lithe::ResourceProvider {
public:
    const lithe::Bitmap* bitmap(std::string_view) const noexcept override { return nullptr; }
    lithe::MutableTextBuffer textBuffer(std::string_view name) const noexcept override {
        return name == "device-name"
            ? lithe::MutableTextBuffer{mDeviceName}
            : lithe::MutableTextBuffer{};
    }
private:
    mutable std::array<char, 24> mDeviceName{{'U', 'N', 'I', 'T', '-', '1', '\0'}};
};
```

`maxLength` is a UTF-8 byte bound and is also clamped to `capacity - 1`. The control handles Unicode
scalar input and removes a complete UTF-8 scalar on backspace. The application's input adapter remains
responsible for keyboards, keypads, touch keyboards, or other text-entry policy. An unresolved binding
causes layout construction to fail with `LayoutError::missingResource`.

These checkbox, radio, and text-field class names are modern portable extensions. They expose behaviors
that the LiTheView product historically assembled from toggle/focused buttons and blinking pickers; they are
not presented as source-compatible historical LiTheView widget classes.

## XML writer and designer boundary

`XmlLayoutWriter::write()` serializes a `std::span<const ViewSpec>` into a caller-owned
`std::span<char>`. For an animated layout, pass the same bounded frame-name arena in
`XmlWriteOptions`. Output is always NUL-terminated when the destination is nonempty, and an
insufficient buffer returns `XmlWriteError::bufferTooSmall`. See [DESIGNER.md](DESIGNER.md) for the
WYSIWYG/code-generation contract.

## Static build-time alternative

XML and static layouts converge on the same structure:

```cpp
constexpr auto views = [] {
    std::array<lithe::ViewSpec, 2> result{};
    result[0].kind = lithe::ViewKind::panel;
    result[0].parent = -1;
    result[0].id = 1;
    result[0].frame = {0, 0, 320, 240};
    result[1].kind = lithe::ViewKind::button;
    result[1].parent = 0;
    result[1].id = 101;
    result[1].frame = {8, 8, 304, 36};
    result[1].text = "OPEN";
    return result;
}();
```

Use this form when a build tool has already compiled XML or when a tiny target should omit the parser
object code. The framework does not impose the code-generation tool.

## Deliberate parser limits

This is a layout dialect parser, not a general XML implementation. It accepts the five predefined XML
entities and numeric character references in visible text; decoding happens lazily while measuring and
drawing, without copying strings. It does not accept text nodes, expand DTDs, process namespaces
semantically, or normalize attribute text. Put visible text in attributes. There is no external entity,
network, or filesystem access.
