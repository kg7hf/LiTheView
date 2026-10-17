# Scenes, gestures, transitions, and shaped viewports

This layer is graphics infrastructure, not an application framework. It supplies bounded mechanisms
for composing retained views and interpreting pointer motion. Product navigation policy, business
state, display-controller commands, RTOS integration, storage, and communications remain outside
LitheView.

The 240 by 240 round demo is an acceptance example. Its dashboard content is intentionally ordinary;
the reusable behavior being exercised is circular clipping, timestamped gestures, fixed-capacity
scene and overlay composition, translated transitions, full-frame/tiled equivalence, and deterministic
cancellation.

## Ownership and fixed bounds

`Application` borrows every object supplied to it. The caller owns the `Display`, each `ViewController`,
each retained `View`, fonts, bitmaps, strings, and any mutable model storage. They must outlive every
application entry that refers to them.

Scenes and overlays share `config::kMaxControllerDepth` entries. There is no heap fallback. A push or
pop returns `false` without changing the stable stack when:

- the fixed capacity is exhausted;
- the same controller is already present;
- the requested pop does not match the top layer type;
- there is no base scene; or
- another transition is already active.

The application, recognizer, and transition state contain no locks, threads, timers, I/O handles, or
transport state. An integration that can update the model from multiple tasks must serialize input,
model mutation, clock advancement, and rendering at its own boundary.

## Shaped viewport

A `Viewport` is defined in logical display coordinates. A rectangle is the default; a circle requires
square bounds. Invalid or empty bounds passed to `Application` select the complete target surface.

```cpp
lithe::Application application{
    display,
    lithe::Viewport::circle({0, 0, 240, 240})
};
```

The circular test classifies pixel centers using doubled 64-bit integer coordinates. There is no
floating-point calculation, square root, mask allocation, or per-frame geometry construction. The
painter first intersects ordinary rectangular clips, then limits each affected scan line to the
circular span. Lines, rectangles, bitmaps, glyph coverage, and individual pixels all observe the same
viewport.

Damage remains a bounded list of rectangles. A tile in a display corner can be entirely outside the
circle and therefore produce no damage. `TiledRenderer` still sends that cleared tile to the sink;
"nothing inside the shaped viewport changed" is not a rendering failure.

The viewport is a clip, not a panel driver. It does not know whether the physical glass is round,
whether a square controller RAM surrounds it, or how pixels are transferred.

## Timestamped pointer input and gestures

The platform adapter validates, calibrates, rotates, and converts raw touch reports before dispatch.
Distances are logical pixels, timestamps are monotonic wrapping milliseconds, and contact IDs are
caller-assigned.

```cpp
lithe::PointerEvent event{
    {logicalX, logicalY},
    lithe::PointerPhase::pressed,
    contactId,
    monotonicMilliseconds
};

const bool handled = application.handlePointer(event);
```

`GestureRecognizer` is a one-contact, allocation-free state machine. `GestureThresholds` configures
tap slop, drag distance, swipe distance, maximum tap time, long-press time, and maximum swipe time.
Distances use exact integer comparisons. Invalid thresholds fail closed and do not retain partial
tracking state.

The recognizer emits:

- tap and long press;
- drag start, update, and end;
- left, right, up, and down swipe; and
- explicit cancellation.

The controller receives gestures through `ControllerDelegate::onGesture()`. Product code decides what
a gesture means. LitheView does not reserve a swipe for page navigation or a long press for a menu.

Pointer capture still belongs to the controller. One control receives the press/move/release sequence,
including motion outside its frame. Navigation cancels capture and gesture tracking before changing
layers. Input during an active transition is rejected so an event cannot target two translated scenes.

LitheView owns no timer. Pointer timestamps cover event-to-event timing. To recognize a stationary
long press, the caller advances time only while `Application::animationsRunning()` is true:

```cpp
if (application.animationsRunning())
{
    const bool changed = application.advanceTime(elapsedMilliseconds);
    if (changed)
    {
        application.render();
    }
}
```

The same clock call advances image sequences and scene transitions. The caller chooses its timer,
wakeup policy, tick source, and synchronization rules.

## Scenes, overlays, and transitions

A scene is a full navigation destination. An overlay composes above the nearest scene and any lower
overlays. The caller supplies the controllers and selects the transition at each navigation decision.

```cpp
constexpr lithe::SceneTransitionSpec kSlideLeft{
    lithe::SceneTransition::slideLeft,
    220U
};
constexpr lithe::SceneTransitionSpec kSlideUp{
    lithe::SceneTransition::slideUp,
    180U
};

if (!application.pushScene(activityController, kSlideLeft))
{
    // Apply the product's diagnostic or safe-state policy.
}

if (!application.pushOverlay(menuController, kSlideUp))
{
    // The stable stack has not been modified.
}
```

Available motions are left, right, up, down, and drop-away. A zero duration or `none` commits
immediately. Animated layers are drawn at translated offsets through the existing clip; LitheView does
not allocate outgoing and incoming framebuffers. A full-frame display can reuse its one existing
buffer, and a tiled display can render the same transition into its normal workspace.

`cancelTransition()` has deterministic rollback semantics:

- cancelling a push removes the incoming scene or overlay; and
- cancelling a pop restores the outgoing scene or overlay.

Either result invalidates every visible layer for one stable redraw. Completion does the same after
committing exactly once. This matters for transparent overlays: the opaque scene must repaint the
overlay's previous translated position before the overlay is composed at its final position. Starting
another navigation operation while a transition is active fails closed rather than silently
retargeting the animation.

Motion can require full-viewport redraws because old and new positions both change. Once the stack is
stable, ordinary child invalidation is damage-limited again; an overlay does not force every later
update to repaint the complete screen.

## Adapting a 240 by 240 GC9A01 panel

GC9A01, SPI, DMA, GPIO, reset, backlight, and touch-controller logic belong in the product adapter. A
small-memory integration normally supplies one RGB565 strip and a synchronous `TileSink`:

```cpp
class Gc9a01Sink final : public lithe::TileSink
{
public:
    bool beginFrame(lithe::Size frameSize, lithe::PixelFormat format) noexcept override
    {
        return frameSize.width == 240 && frameSize.height == 240 &&
               format == lithe::PixelFormat::rgb565;
    }

    bool writeTile(lithe::Rect destination,
                   lithe::PixelBufferView pixels) noexcept override
    {
        // Validate the rectangle and view, program the GC9A01 address window,
        // then synchronously transmit or copy every row before returning.
        return writePanelWindow(destination, pixels);
    }

    bool endFrame(bool complete) noexcept override
    {
        return complete;
    }
};

std::array<lithe::Pixel, 240U * 4U> strip{};
lithe::Surface workspace{strip, 240, 4, 240};
const lithe::TileRenderResult result =
    lithe::TiledRenderer::render(application, {240, 240}, workspace, sink);
```

The four-row RGB565 workspace is 1,920 bytes. `writeTile()` must consume or copy its borrowed bytes
before returning because the renderer immediately reuses them. An adapter that begins asynchronous DMA
must retain its own transfer storage and expose completion through the separate
`DisplayTransferAdapter` lifetime contract.

A typical GC9A01 has square 240 by 240 controller memory behind round glass. LitheView deliberately
writes cleared corner tiles as well as visible circular spans, producing deterministic panel RAM and
identical full-frame/tiled output. If a particular controller or transport can safely omit invisible
corner writes, that is an adapter optimization and must preserve the synchronous sink contract.

## Reference examples and evidence

The Win32 build produces a second 240 by 240 round executable for each framebuffer configuration. It
uses the generated font in the XRGB8888 build and the compact 5 by 7 font in the RGB565 build. Swipe
left opens a gesture-lab scene, where tap and drag events move a bounded marker and identify the
recognized event. Swipe right returns to the dashboard; swipe up or long press presents an overlay;
swipe down dismisses it. The overlay includes version, target, branch, commit, and UTC build time so a
tester can identify the image. The runnable feature matrix is in
[the Win32 Item 4 capability tour](../tests/rendering/win32/README.md#item-4-capability-tour).

`RoundGraphicsProbe` cross-compiles the same state machines for the NUCLEO-U545RE-Q and renders through
the 1,920-byte strip. The board has no panel, so the probe is compilation, object-budget, pixel-count,
tile-count, and deterministic-CRC evidence only. It is not a GC9A01 electrical or timing claim.

Host regression tests cover circular edge and corner classification, full-frame/arbitrary-tile byte
equivalence, exact RGB565 and XRGB8888 image signatures, all gesture directions, threshold boundaries,
unrelated contacts, cancellation, captured-control navigation conflicts, halfway transitions,
transition rollback, overlay composition, and damage-limited updates after settling.
