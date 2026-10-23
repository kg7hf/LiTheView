# Controls, form instances and handwritten handlers

A form description is not the live UI. The generated `Form::Instance<>` builds real
`ButtonView`, `LabelView` and other control objects in bounded application-owned
storage, then owns the `ViewController` which routes input to those objects.
Properties remain normal control methods. No runtime XML parser, reflection,
untyped callback context or heap allocation is needed for this path.

## The first handler

Designer 0.2.4 adds **Create/Open click handler** to a selected button's inspector.
For a button named `button1`, the generated form base offers `onButton1Clicked()`.
The extension creates the handwritten override in `form_handlers.hpp` only when
requested. Generate UI owns the base and never rewrites this handwritten file.

```cpp
#include "Generated/product_config.hpp"

namespace application::ui
{
class Form : public lithe::simulator::Form::Instance<>
{
protected:
    void onButton1Clicked() noexcept override
    {
        mOn = !mOn;
        getLabel1()->setText(mOn ? "ON" : "OFF");
    }

private:
    bool mOn{true};
};
}
```

The starter's CMake selects this class when the handler header exists. In another
application, instantiate your form, check `build(font, resources)`, then attach its
`controller()` to your application scene. Failed construction leaves no live
controller. Accessors return null before successful construction; after that they
return the actual named objects. Keep form, resources and borrowed text alive while
attached. Detach the scene before destroying the form. A built form cannot be
rebuilt in place; construct another instance after detaching if replacement is needed.

The generated names use component identity, not numeric runtime IDs. If a control
is renamed or deleted, the extension diagnoses its existing binding before export.
Update the handwritten override and its `litheview-handler` comment deliberately;
do not throw away the body. C++ `override` also rejects a stale method name at build.
Removing or editing the insertion markers requires manual source maintenance.

## A reusable specialized button

`ButtonView` and `LabelView` permit public derivation. Button activation maintains
its state privately and calls a protected `onClicked()` hook. Select the derived
type at compile time, using the generated component alias:

```cpp
class CountingButton : public lithe::ButtonView
{
public:
    using ButtonView::ButtonView;
    unsigned int clicks() const noexcept
    {
        return mClicks;
    }

protected:
    void onClicked() noexcept override
    {
        if (mClicks != 100U)
        {
            ++mClicks;
        }
    }

private:
    unsigned int mClicks{0U};
};

using Definition = lithe::simulator::Form;
using Controls = lithe::WidgetOverrides<Definition::Button1Type<CountingButton>>;
class MyForm : public Definition::Instance<Controls>
{
    // getButton1() returns CountingButton*, with its actual storage and lifetime.
};
```

Public derivation is required for the framework to use the object as a button.
The actual derived object has typed storage with its own size and alignment;
it is never placed into a slot sized for `ButtonView`. Constructors must satisfy
the built-in constructor contract and be `noexcept`; destructors must not throw.
Duplicate or out-of-range replacement indices fail at compile time, incompatible
layout kinds fail during construction, and partial construction is cleaned up.
Checkbox, radio, picker, wheel-picker, drop-down and text-field controls also permit public derivation.
Their input entry points remain final so specialization uses the protected typed
hooks without replacing state maintenance. The same generated override aliases
select these derived types and provide storage with their actual size/alignment.

## Event and lifetime rules

On controller-driven activation, a button commits its toggle state, invokes its
own `onClicked()` hook once, then notifies the form's named handler once. A reusable
button may implement its behavior in the first hook; a form may coordinate its
controls in the second. Choose their responsibilities so an application side
effect is not performed twice. Direct `ButtonView::activate()` invokes the control
hook only; it does not synthesize controller input or notify a form.

Pointer cancellation, release outside, disabled controls and disabled ancestors
do not activate the button. Nested input dispatch on the same controller is
rejected. Callbacks may mutate normal control properties, but must not destroy,
rebuild or reparent the active scene or recursively invoke activation. Execute
dispatch and UI mutations on one owner task. For other tasks, send an explicit
application message; the UI library does not become a cross-task message broker.

The existing `ControllerDelegate::onAction(ViewId)` remains supported. Its new
`onControlAction(ViewController&, View&)` bridge forwards to that method by default.
Generated form instances override the typed bridge for named button handlers.
Numeric actions retain their previous behavior independently of the new semantic
events. An application should use one notification path for each side effect.

## Typed value and edit events

Designer 0.2.11 exposes **Events** in the selected control's inspector. Choose
**Create/Open** for an event, edit and save `form_handlers.hpp`, then Generate UI
and build/debug with the application's existing configurations. Each binding is
identified by component plus event, so a wheel can own several handlers. Existing
click bindings remain compatible. Renames, deletion and incompatible signatures
are diagnosed without overwriting handwritten bodies.

| Control | Events | Payload |
| --- | --- | --- |
| Button | Clicked; Checked changed for toggle behavior | No argument; `lithe::CheckedChange` |
| Checkbox | Checked changed | `lithe::CheckedChange { previousValue, value }` |
| Radio choice | Radio selection changed | `lithe::RadioSelectionChange { previous, selected }` |
| Picker | Selection changed | `lithe::SelectionChange { previousIndex, index }` |
| Wheel picker | Selection changed; Edit committed; Edit cancelled | `lithe::SelectionChange`; `lithe::SelectionEdit { initialIndex, index }` |
| Drop-down | Selection changed; Edit committed; Edit cancelled | `lithe::SelectionChange`; `lithe::SelectionEdit { initialIndex, index }` |
| Text field | Text changed; Edit committed; Edit cancelled | `lithe::TextChange { previousLength, text }`; `lithe::TextEdit { initialText, text }` |

For example, a checkbox named `enable` can update a label named `status`:

```cpp
void onEnableCheckedChanged(lithe::CheckedChange event) noexcept override
{
    getStatus()->setText(event.value ? "ON" : "OFF");
}
```

Its reusable control hook is `onCheckedChanged(lithe::CheckedChange)`. The other
protected hooks are `onSelectionChanged`, `onRadioSelectionChanged`,
`onEditCommitted` and `onEditCancelled`, with the types above. A hook runs before
the corresponding form handler. Payloads describe the accepted transition even
if a hook subsequently changes another property; they are notifications, not
vetoes. Radio pointers are borrowed for the synchronous call. Do not retain them
beyond their controls' lifetime or queue them as cross-task messages.

Ordinary value setters and `selectPrevious`/`selectNext` remain silent. Controller input
delivers semantic events; direct input/activation calls reach specialization
hooks only. Changed events require a value transition. Toggle change precedes its
click hook and form click handler. A picker changes on pointer press or left/right
input; release does not produce a second change. Recursive activation and nested
controller input are rejected before mutation.

Wheel editing follows these rules:

- Select starts an edit and records its baseline. Arrow/encoder movement produces
  changes. Select again commits, including an unchanged selection.
- Back restores the baseline, sends a change if restoration changed the value,
  then sends cancellation. It never sends commit.
- Ordinary focus loss accepts the current edit, preserving the wheel's existing
  behavior. Hiding/disabling the control or an ancestor cancels instead. If a
  handler disables its source, invalid-focus cleanup occurs after the handler.
- A pointer press accepts an existing keyboard edit and begins a pointer gesture.
  A tap inside or a completed drag sends any change, then commit. Drags still snap
  when released outside. A tap outside or capture loss cancels. Pointer movement
  only changes the visual offset until release, so capture cancellation needs no
  selection rollback. An empty wheel cannot start or commit an edit.

The controller borrows a typed sink only during the synchronous control call.
Read-only views carry no sink storage. There is no event queue, erased `void*`
context, RTTI or implicit allocation. Controls and handlers stay on one UI owner
task and the active tree must remain alive and attached throughout dispatch.

## Radio groups

Individual radio buttons remain usable without a group. An application-owned
`lithe::RadioGroup` adds exclusive selection; the designer does not infer groups
from adjacency or parent panels. After a successful form build, attach named
choices with `group.add(*form.getRadio1())` and check its `std::expected` result.
Repeat for each choice. Keep the group as a member of the handwritten form or its
application owner.

A group contains at most `RadioGroup::kCapacity` choices (the configured maximum
focusable count, currently 64). Duplicate membership, capacity overflow and two
initially selected choices are rejected without changing the attempted attachment.
Selecting a new choice clears the previous one before the new choice's hook and
form handler run. The event identifies both choices; it is emitted once by the
newly selected control. Activating the current choice is silent. Programmatic
`setSelected(true)` also maintains exclusivity, without notifications;
`setSelected(false)` may leave the group empty.

Both controls and groups detach their membership on destruction, so either may
outlive the other. Destruction still belongs outside active dispatch. Neither
groups nor controls can be copied or moved.

The compiled [event consumer](../tests/control_events_test.cpp) demonstrates real
generated handlers, label updates, specialization, radio grouping and input
semantics. Generated forms with these events require the matching Core containing
`lithe/control_events.hpp`; update the application's pinned dependency when
adopting them. Generation emits a compile-time diagnostic for older packages.

Selectable table data and editing remain later steps in the
[control-event milestone](VSCODE_DESIGNER_PLAN.md#control-event-milestone). Tabs and
softkeys retain their numeric actions; scroll containers handle scrolling
internally. The event catalog documents these boundaries rather than advertising
unimplemented callbacks.

## Drop-down selection

Add **Drop-down** from the palette, name it (for example `mode`), and edit **Items**,
**Selected Index**, **Row Height**, **Wrap**, colors and the closed control's frame.
Items use the existing pipe-delimited format, such as `AUTO|MANUAL|OFF`. The
designer requires 1–255 items and an index that names one of them. An index is the
item identity; equal labels are allowed, and application-specific IDs remain the
application's mapping. The native constructor borrows immutable item storage for
the control's lifetime; an empty, oversized or initially invalid model is inert.

The closed selector displays its current item. Select or a click opens a list
above other controls, below the field when space permits or above it otherwise.
It shows at most five complete rows within the root surface. It may extend beyond
a containing panel's clip. Painter viewport masks still apply. Leave enough room
for readable choices on a shaped display; opening fails if no complete row fits.
Row height defaults to 28 pixels (designer range 12–255).

- While open, arrow keys or encoder steps change the tentative selection. At an
  unwrapped edge nothing changes and no change event is sent.
- Select commits, even if the selection is unchanged. Tapping a list row changes
  and commits it once. Drag the list to reveal additional items; scrolling alone
  does not change or commit the selected item.
- Back, another click on the closed field, an outside press, lost focus, or
  cancelled capture restores the initial selection. Restoration sends a change
  only if necessary, then cancellation. The outside press does not click through
  to the covered application control.
- Hiding/disabling an ancestor makes the popup ineligible immediately; the
  controller clears retained focus and cancels the edit at its next focus query
  or dispatch. A source disabled during its own callback is cleaned up after that
  event finishes. Direct control calls reach control hooks only.
- `setSelectedIndex(index)` accepts a valid application refresh and silently
  closes an edit. It returns false for an invalid index or reentrant mutation,
  and true for an accepted refresh, including the same index. `beginEdit`,
  `commitEdit`, and `cancelEdit` report success; opening requires focus and space.

For the selected `mode` control, **Events → Edit committed → Create/Open** creates
this application-owned method in `form_handlers.hpp`:

```cpp
void onModeEditCommitted(lithe::SelectionEdit event) noexcept override
{
    // Apply event.index to the application's backend here.
    getStatus()->setText(getMode()->item(event.index));
}
```

The other hooks are `onModeSelectionChanged(lithe::SelectionChange)` and
`onModeEditCancelled(lithe::SelectionEdit)`. A derived `DropDownView` can implement
`onSelectionChanged`, `onEditCommitted`, and `onEditCancelled` instead or alongside
the form hooks. Control hooks run first. Regeneration preserves handwritten code.
Generated drop-down forms require control-event contract version 3.

The compiled [drop-down regression consumer](../tests/drop_down_test.cpp) checks
real generated handlers, backend/label updates, popup hit testing and repainting,
scrolling, cancellation, disabled ancestors and synchronous callback ordering.
Application starter examples are deferred; neither existing H750 starter is changed.

## Bounded text editing

`TextFieldView` supports specialization. Its protected hooks are `onTextChanged`,
`onTextEditCommitted` and `onTextEditCancelled`. The generated form equivalents
for a field named `setpoint` are `onSetpointTextChanged(lithe::TextChange)`,
`onSetpointEditCommitted(lithe::TextEdit)` and `onSetpointEditCancelled(lithe::TextEdit)`.
They are offered in the same Events inspector as the other controls. Adding a text
field from the palette declares a distinct 32-byte mutable-text binding and a
31-byte text limit; it does not allocate application buffers. Duplication copies
the binding declaration with a new identity and the same capacity. Undo rolls
back the field and its new binding together.

The application supplies two distinct buffers when it wants transactional editing:

- `ResourceProvider::textBuffer(name)` is the mutable UTF-8 draft, including space
  for its NUL terminator. Existing binding `capacityBytes` describes this buffer.
- `ResourceProvider::textEditBuffer(name)` is optional rollback storage. It must
  have at least `maxLength() + 1` bytes and must not overlap the draft buffer.
  Include these extra bytes in the application's memory budget. Core never
  allocates the snapshot. Both buffers and the provider outlive the built form;
  neither buffer is shared with another active field or mutated behind its back.

Handwritten construction takes the optional edit span after `Style`:

```cpp
std::array<char, 4> draft{'2', '5'};
std::array<char, 4> baseline{};
lithe::TextFieldView field{frame, font, draft, 3U, "0 TO 100", style, baseline};
```

An empty edit span preserves the existing immediate editor: accepted typing sends
`TextChange`, but there is no commit/cancel transaction. A provided but undersized
or overlapping snapshot rejects editing before mutation. `beginEdit()`,
`commitEdit()` and `cancelEdit()` return `std::expected<void, TextEditError>`;
check the result. Direct calls reach control hooks only. For form notifications,
route logical input through the form's controller on the UI owner task.

With rollback storage supplied:

1. Pointer activation, Select, or the first accepted text input starts an edit.
   The baseline is copied once. Typing appends Unicode scalar values within the
   byte limit; Backspace removes a complete UTF-8 code point. There is no caret
   movement, selection, paste or IME composition contract in this increment.
2. A real text change sends `TextChange`. `previousLength` is a byte count; no
   previous-text string is implicitly copied. Invalid scalar values, empty
   Backspace and capacity failures produce no change event.
3. Select while editing calls the protected, side-effect-free
   `acceptsText(std::string_view)` validation hook. It defaults to accepting the
   draft. A rejected commit returns `invalidText`, keeps the draft editable and
   sends no commit. Select remains consumed, so rejection is not a numeric action.
   An accepted commit settles the edit before notification, even if text is unchanged.
4. Back or focus loss cancels. Restoration occurs first, followed by a changed
   event only if needed, then cancellation. `TextEdit.text` is the restored text
   on cancellation. Cancel never commits. Focus invalidation after a handler
   disables its source is deferred until that change notification finishes.
5. `setText()` is a silent authoritative refresh. It abandons a pending edit,
   and the next edit uses the new value as its baseline. It rejects oversize text
   and embedded NUL bytes. The caller supplies valid UTF-8 for programmatic text.
   Mutating text or starting another edit from that control's callback is rejected
   so borrowed event strings remain stable throughout both notifications.

`TextEdit.initialText` and `text`, and `TextChange.text`, are borrowed only during
that synchronous notification. Copy any value needed later into application-owned
bounded storage. In particular, `LabelView::setText` also borrows its argument:
passing a draft/event view straight to an applied-value label would let later typing
change that label. Keep separate persistent storage for the accepted value.

The compiled [setpoint workflow test](../tests/text_setpoint_test.cpp) validates a
specialized numeric field, generated form callbacks, a backend value and a label:
valid integers 0–100 apply on Select, invalid drafts remain editable, and Back
restores the baseline. [Lifecycle tests](../tests/text_events_test.cpp) cover UTF-8,
capacity, cancellation order, focus, silent refresh and re-entry. These are test
fixtures, not a new application starter. The control-specific starter and physical
hardware walkthrough are deliberately deferred; existing H750 starter files stay local.

Generated text handlers require control event contract version 2. Update the
application's LiTheView dependency and prepare/package the designer service when
adopting them. The designer does not build or launch the application.

## Selectable list

`ListView` is a reusable single-column control for a bounded, application-owned
catalog. A form can author preview rows in its `items` property; the application
can replace them with `setItems(std::span<const std::string_view>)`. The span and
each string's storage are borrowed for the lifetime of that snapshot. Up to 64
entries are accepted without per-row allocation. The caller maps a row index to
a stable domain ID before changing the catalog.

Up/Down moves selection and scrolls the selected row into view. A row tap selects
it but does not activate it; Select or an equivalent hardware center input sends
`ItemActivation{index}`. The right-edge touch targets reach rows outside the
viewport. A change sends `SelectionChange{previousIndex, index}` first, only if
the index actually changes. `setItems()` and `setSelectedIndex()` are silent;
both reject reentrant calls from the list's own event callback. The list can be
empty while the application is obtaining data, in which case it has no valid
selection or activation. It does not implement drag scrolling, multi-selection,
sorting, filesystem discovery or columns.

The designer exposes **Selectable List** and generates named
`on<Name>SelectionChanged(SelectionChange)` and
`on<Name>ItemActivated(ItemActivation)` hooks. These use control-event contract 4.
The [H750 Load form](../examples/stm32h750b-dk-menu-1-complete/Tasks/UI/forms/Load.litheview.xml)
and its [handwritten handlers](../examples/stm32h750b-dk-menu-1-complete/Tasks/UI/handlers/Load.hpp)
show the form workflow. The [compiled control test](../tests/list_view_test.cpp)
checks the same events through a generated form, keyboard and pointer input.
`TableView` remains a scrolling container; it does not inherit row semantics
from this list.

## Without the designer

Construct the same `ButtonView` and `LabelView` objects directly with their public
constructors, attach them to a parent `View`, and supply a `ViewController` with
a typed `ControllerDelegate`. Its `onControlAction` can compare the source's
address to your known button instance and call your application method. Or supply
a constexpr `ViewSpec` array to `StaticLayout`; its optional `WidgetOverrides`
argument constructs selected application types. Resources, input drivers, display
drivers, tasks and the choice of static or application-managed allocation remain
the application's responsibility.

The [guided starter](../examples/stm32h750b-dk-starter-1/README.md) takes this from
a custom display definition through a border, button and label to the same Windows
and H750 builds. The completed sibling contains the expected handwritten handler.
