# Touch integration

Touch is a platform input source, not a display-framework dependency. A board adapter reads its
controller, applies calibration and orientation, and sends a `PointerEvent` in logical display
coordinates to `Application::handlePointer()`.

```cpp
lithe::PointerEvent event{
    {logicalX, logicalY},
    lithe::PointerPhase::pressed,
    contactId,
    monotonicMilliseconds
};
application.handlePointer(event);
```

The phases are `pressed`, `moved`, `released`, and `cancelled`. The controller captures one contact by
ID on press, continues delivering moves outside the original control, and activates only when that same
contact releases over the captured control. Releasing outside or cancelling performs no action. This
prevents an accidental command merely because a finger first touched a control.

The one-contact capture policy is intentional for small instrument UIs. Contact IDs make unrelated or
spurious secondary reports detectable without making every view carry multi-touch state. The
allocation-free `GestureRecognizer` now sits between `Application` and MVC dispatch. It recognizes
tap, long press, drag, and four-direction swipes using caller-selected logical-pixel and millisecond
thresholds. Views do not carry gesture state; a controller delegate decides what a recognized gesture
means to the product.

The Win32 demo maps mouse down/move/up/capture-cancel through this exact lifecycle. The STM32H750B-DK
adapter converts FT5336 state to the same events.

`ScrollView` reserves a 20-pixel touch target at its trailing edge around the visible eight-pixel
scrollbar. Pressing the thumb preserves the grab offset; moving the captured contact maps the thumb
travel to the bounded content offset; releasing or cancelling ends the drag without dispatching a view
action. Pressing elsewhere on the scrollbar track moves the thumb toward that position and begins the
same captured drag. Child controls remain the pointer targets outside this trailing-edge region.

A U545 panel adapter should own:

- I2C/SPI transactions and interrupt handling;
- raw-range validation and controller fault recovery;
- affine calibration, axis swap/inversion, rotation, and logical-size scaling;
- pressure/contact filtering and debounce policy;
- wake-from-sleep behavior;
- the monotonic wrapping millisecond timestamp attached to each accepted event; and
- the caller-owned timer used for stationary long-press recognition.

None of those policies belongs in XML or in a view. Unit tests cover capture, contact-ID rejection,
release-inside activation, release-outside cancellation, explicit cancellation, softkey selection,
captured scrollbar dragging, gesture thresholds, all four swipe directions, timestamp wrap-safe
elapsed time, and navigation conflicts. See
[the complete scene, gesture, transition, and viewport contract](SCENES_GESTURES_AND_VIEWPORTS.md).

## Rotary encoder and push button

A touch controller is not required. An application-owned quadrature decoder can map each accepted
counterclockwise/clockwise detent to `Key::up`/`Key::down`, and a debounced push button can map to
`Key::select`. LitheView remains unaware of GPIO, encoder phase, debounce, acceleration, ISR/task
handoff, or press-duration policy.