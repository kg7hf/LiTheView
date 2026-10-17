# Tabs: tab bar and TabView

This guide describes the third Phase 5.7 vertical slice for a developer who has not used LitheView
tabs before. It covers the runtime C++ API, XML, the VS Code designer, generated C++, input,
rendering, memory, and the checked example. Theme tokens and the common state resolver are
described in [THEMES_AND_WIDGET_STATES.md](THEMES_AND_WIDGET_STATES.md).

## Two widgets

| Widget | `ViewKind` | C++ type | Children | Use it when |
| --- | --- | --- | --- | --- |
| Tab Bar | `tabBar` | `lithe::TabBarView` | none | The application switches content itself, or the strip selects a mode. |
| Tab View | `tabView` | `lithe::TabView` | one page per tab | The pages are part of the same form. |

Both draw an equal-width strip of up to 16 tabs. Titles are one pipe-delimited string in
caller-owned immutable storage, the same convention as picker items (`"DISPLAY|NETWORK|ABOUT"`).
`TabView` derives from `TabBarView`, adds a strip height, and treats its direct children as pages:
page *i* is visible only while tab *i* is selected. Hidden pages are skipped by drawing, hit testing,
and focus traversal, exactly as any other hidden view.

## Input

| Input | Result |
| --- | --- |
| `Key::left` / `Key::right` while the strip is focused | Selects the previous/next tab and reports the widget's action once. At either end the key is consumed; it wraps only when `wrap` is true (the default). |
| `Key::select`, encoder push, or push button | Advances to the next tab and always wraps, so encoder-only products can reach every tab. Reports the action. |
| `Key::up` / `Key::down`, encoder rotation | Move focus as usual: the strip, then focusable controls on the visible page, then the rest of the form. |
| Pointer press on a tab | Focuses the strip and highlights that tab with the pressed colors. The selection does not change yet. |
| Pointer release on a tab | Selects the tab under the release and reports the action. Dragging along the strip selects where the contact lifts. |
| Release off the strip, outside the view, or cancellation | No selection change. |

The strip of a `TabView` always receives its own pointer presses, even under a page that
incorrectly overlaps it. A press on an empty page background focuses the tab view without changing
the tab; as for any focusable container, releasing there still reports its action, so compare
`selectedIndex()` if that matters.

Key-driven changes are reported through a small, general controller addition: a widget that
consumes a key may set `View::setActionRequested(true)`, and `ViewController::handleKey()` then
calls `ControllerDelegate::onAction()` with the widget's `actionId()`. The flag is assigned on every
key the tab bar handles, so it never outlives that key. Other widgets are unchanged.

Inside `onAction()` the selection already holds the new value:

```cpp
void onAction(lithe::ViewController& controller, lithe::ViewId action) noexcept override
{
    if (action == kModeAction)
    {
        if (const auto* mode = mLayout.find<lithe::TabBarView>(kModeAction))
        {
            mModel.setMode(mode->selectedIndex());
        }
    }
}
```

## Rendering and state colors

Each tab resolves its own state with the common precedence disabled, pressed, focused, selected,
normal: the selected tab is "selected", it is also "focused" while the strip has focus, and the tab
under a held contact is "pressed". Unselected tabs use the normal (or disabled) pair. The selected
tab also draws a 3-pixel underline in its resolved foreground, so the value never depends on color
alone and stays visible when disabled. Titles are centered and use the widget's font and text scale.
A nonzero border is stroked around the strip and, for a tab view, around the whole frame.

A tab view fills its content area with the normal background before its pages draw. Pages are
ordinary containers and normally fill the content area with their own background.

## Runtime C++

```cpp
lithe::TabView settings{{8, 8, 464, 212}, font, "DISPLAY|NETWORK|ABOUT", 0U, 32U};
lithe::View display{settings.contentFrame()};
lithe::View network{settings.contentFrame()};
lithe::View about{settings.contentFrame()};
settings.addSubview(display);
settings.addSubview(network);
settings.addSubview(about);
settings.syncPages(); // StaticLayout does this after attaching pages

lithe::TabBarView mode{{8, 228, 464, 36}, font, "AUTO|MANUAL|SERVICE", 0U, false};
mode.setSelectedIndex(2U); // clamps to the last tab; returns whether the selection changed
```

`contentFrame()` is in the tab view's own coordinates: page frames use the same coordinate rules
as every other container, so a full page is `{0, tabHeight, width, height - tabHeight}`. Titles
beyond the sixteenth are ignored at run time; strict XML and the designer reject them. With fewer
pages than tabs, a tab without a page shows only the content background; extra pages are never
shown. `setSelectedIndex()`, `selectPrevious()`, and `selectNext()` change pages immediately.

## XML

```xml
<tabview frame="8,8,464,212" runtimeID="20" items="DISPLAY|NETWORK|ABOUT" tabHeight="32"
         selectedBackgroundColor="0x00FAE4D6" selectedForegroundColor="0x00803D1A">
  <panel frame="0,32,464,180"> ... </panel>
  <panel frame="0,32,464,180"> ... </panel>
  <panel frame="0,32,464,180"> ... </panel>
</tabview>
<tabbar frame="8,228,464,36" runtimeID="30" items="AUTO|MANUAL|SERVICE" wrap="0"/>
```

Elements are `tabbar` and `tabview` (aliases `tab-bar` and `tab-view`); historical object markup
uses `LCDTABBARVIEW` and `LCDTABVIEW`. `items`, `selectedIndex`, and `wrap` are the existing picker
attributes. `tabHeight` accepts 8 through 255 pixels (default 32). Strict XML requires 1 through 16
titles and a `selectedIndex` that names one of them, and rejects `tabHeight` on any other widget;
compatibility XML clamps an out-of-range selection. `XmlLayoutWriter` writes all four attributes.

## Designer workflow (VS Code)

1. Add **Tab View** (Containers) or **Tab Bar** (Input) from the palette. A new tab widget starts
   with the titles `TAB 1|TAB 2`; edit **Items** in the Properties panel.
2. Select the tab view and click **Panel** (or Scroll Panel or Table) in the palette. The palette now
   adds into the selected container when the widget may live there, and a new tab-view child fills
   the content area below the strip. Add one page per tab.
3. Set **Selected Index** to choose the page shown when the form starts. It is also the
   design-time page selector: the native preview shows that page, and the canvas does not hit-test
   the other pages or their contents. They stay in the hierarchy, marked "hidden page"; selecting
   one there shows a dashed selection outline.
4. Set **Tab Height**, **Wrap**, and the focused, pressed, and selected colors as for other widgets.

Validation:

| Code | Severity | Meaning |
| --- | --- | --- |
| `LVFD1133` | error | A tab widget needs 1 through 16 titles, and Selected Index must name one of them. |
| `LVFD1134` | warning | A tab view's page count differs from its tab count. |
| `LVFD1135` | warning | A page does not lie within the content area and would cover the strip. |

The page rules are warnings so a tab view can be built up one page at a time; the runtime behavior
for a mismatch is deterministic. Only containers (Panel, Scroll Panel, Table) may be pages.
The standalone designer validates and exports the same forms but receives no new editors.

## Generated C++

```cpp
{
    ViewSpec& spec = result[1U];
    spec.kind = ViewKind::tabView;
    spec.style.states = &kStateStyles[1U];
    spec.parent = 0U;
    spec.style.background = Color::fromLegacyValue(0x00402818U);
    spec.style.foreground = Color::fromLegacyValue(0x00ffffffU);
    spec.frame = {8, 8, 464, 212};
    spec.text = "DISPLAY|NETWORK|ABOUT";
    spec.id = 20U;
    spec.tabHeight = 32U;
}
```

`ViewSpec::tabHeight` uses padding that already existed after `selectedIndex`, so `ViewSpec` stays
120 bytes. `StaticLayout::build()` calls `TabView::syncPages()` after every child is attached.

## Memory and ownership

| Object (32-bit Cortex-M) | Size | Ceiling |
| --- | ---: | ---: |
| `View` | 80 (unchanged; the action-request flag uses a spare bit) | 80 |
| `TabBarView` | 96 | 112 |
| `TabView` | 100 | 112 |
| `ViewSpec` | 120 (unchanged) | 124 |
| `StaticLayout<16>` | 1,816 (unchanged; `AnimatedImageView` is still the largest member) | 1,900 |

Titles are borrowed; tab widgets own no strings, pages, timers, or heap storage. Tab count,
selection, and the pressed tab are bytes. Parsing, layout, input, and rendering allocate nothing.
An all-kinds `StaticLayout` links both widgets (about 2 KB of Cortex-M33 text in a linked image); a
layout whose `WidgetSet` omits them, such as the `Layout` of a generated form without tabs, links
none of it. The core archive grows by about 3 KB; measured sizes are recorded in
[AGENT_HANDOFF.md](AGENT_HANDOFF.md).

## Example and tests

[`tests/designer-fixtures/tab-view-smoke`](../tests/designer-fixtures/tab-view-smoke/README.md) is a checked
designer project with a three-page tab view (picker, checkbox, button, and labels on the pages) and
a non-wrapping three-tab bar. Host tests cover strict and compatibility XML, the writer round trip,
page visibility, focus traversal across visible pages, key/select/pointer/drag/cancel input, action
reporting, bounded damage, focus release when a page hides, the strip under an overlapping page,
RGB565 and XRGB8888 goldens with tile equality, and XML-versus-generated equivalence. Native preview
tests drive keys, push buttons, and taps through the real renderer.

## Limits and follow-ups

- Tabs are equal width; there is no scrolling strip, icon tab, or per-tab disabled state.
- The picker still reports actions only on activation; adopting the key action request is a
  candidate for the picker slice.
- Clicking a tab in the VS Code canvas does not switch the design-time page; use Selected Index.
- The WinForms canvas draws every page outline; it is kept compiling but not extended.
