// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "designer-fixtures/control-events/form.hpp"

#include <array>
#include <cstdio>
#include <optional>

namespace
{
int failures{0};
void check(bool value, const char* message)
{
    if (!value)
    {
        std::printf("FAIL: %s\n", message);
        ++failures;
    }
}

class SpecializedCheckbox : public lithe::CheckboxView
{
public:
    using CheckboxView::CheckboxView;
    int hooks{0};

protected:
    void onCheckedChanged(lithe::CheckedChange event) noexcept override
    {
        ++hooks;
        check(checked() == event.value && event.previousValue != event.value,
              "checkbox settled before hook");
        activate(); // A direct recursive activation must do nothing.
        check(checked() == event.value, "recursive activation rejected before mutation");
    }
};

using Generated = lithe::generated::ControlEvents;
class Form
    : public Generated::Instance<lithe::WidgetOverrides<Generated::EnableType<SpecializedCheckbox>>>
{
public:
    lithe::RadioGroup group;
    int checks{0};
    int radios{0};
    int picks{0};
    int changes{0};
    int commits{0};
    int cancels{0};
    int clicks{0};
    bool disableOnChange{false};
    std::array<int, 64U> order{};
    std::size_t count{0U};
    lithe::SelectionChange lastChange{};
    lithe::SelectionEdit lastEdit{};
    void record(int value) noexcept
    {
        if (count < order.size())
            order[count++] = value;
    }

protected:
    void onEnableCheckedChanged(lithe::CheckedChange event) noexcept override
    {
        ++checks;
        check(getEnable()->hooks >= checks, "control hook precedes form handler");
        getStatus()->setText(event.value ? "ON" : "OFF");
        check(!controller()->handleKey(lithe::Key::select), "nested key dispatch rejected");
        check(!controller()->handlePointer(lithe::Point{10, 10}),
              "nested pointer dispatch rejected");
        getEnable()->activate();
        check(getEnable()->checked() == event.value, "form cannot recursively activate source");
    }
    void onRadio2RadioSelectionChanged(lithe::RadioSelectionChange event) noexcept override
    {
        ++radios;
        check(event.previous == getRadio1() && event.selected == getRadio2(),
              "radio payload identifies choices");
        check(!getRadio1()->selected() && getRadio2()->selected() &&
                  group.selected() == getRadio2(),
              "radio group invariants precede form notification");
    }
    void onModeSelectionChanged(lithe::SelectionChange event) noexcept override
    {
        ++picks;
        lastChange = event;
        check(getMode()->selectedIndex() == event.index, "picker accepted state visible");
    }
    void onSetpointSelectionChanged(lithe::SelectionChange event) noexcept override
    {
        ++changes;
        lastChange = event;
        record(1);
        if (disableOnChange)
        {
            getSetpoint()->setEnabled(false);
            check(controller()->focusedView() == nullptr,
                  "query hides disabled focus during callback");
        }
    }
    void onSetpointEditCommitted(lithe::SelectionEdit event) noexcept override
    {
        ++commits;
        lastEdit = event;
        record(2);
        check(!getSetpoint()->editing(), "edit state settled before commit");
    }
    void onSetpointEditCancelled(lithe::SelectionEdit event) noexcept override
    {
        ++cancels;
        lastEdit = event;
        record(3);
        check(!getSetpoint()->editing(), "edit state settled before cancel");
    }
    void onToggleCheckedChanged(lithe::CheckedChange event) noexcept override
    {
        check(getToggle()->selected() == event.value, "toggle accepted value");
        record(4);
    }
    void onToggleClicked() noexcept override
    {
        ++clicks;
        record(5);
    }
};

void exerciseForm()
{
    Form form;
    check(form.build().ok(), "generated form builds with specialized checkbox");
    check(form.group.add(*form.getRadio1()).has_value() &&
              form.group.add(*form.getRadio2()).has_value(),
          "attach bounded group");
    auto& controller = *form.controller();
    controller.activate();
    check(controller.handleKey(lithe::Key::select), "checkbox key activation");
    check(form.checks == 1 && form.getStatus()->text() == "ON",
          "checkbox drives actual label behavior");
    check(controller.handlePointer(lithe::Point{10, 10}), "checkbox pointer activation");
    check(form.checks == 2 && form.getStatus()->text() == "OFF",
          "pointer and key have identical semantics");
    form.getEnable()->setChecked(true);
    check(form.checks == 2 && form.getEnable()->hooks == 2, "programmatic refresh is silent");
    form.getEnable()->activate();
    check(form.checks == 2 && form.getEnable()->hooks == 3, "direct activation reaches hook only");
    (void)controller.handlePointer({{10, 10}, lithe::PointerPhase::pressed, 0U});
    check(controller.cancelPointerCapture() && form.checks == 2,
          "checkbox cancellation produces no change");
    form.getRootPanel()->setEnabled(false);
    check(!controller.handlePointer(lithe::Point{10, 10}) &&
              !controller.handleKey(lithe::Key::select),
          "disabled ancestor blocks input");
    form.getRootPanel()->setEnabled(true);
    check(controller.handlePointer(lithe::Point{110, 40}) && form.radios == 1,
          "radio pointer changes group once");
    (void)controller.handleKey(lithe::Key::select);
    check(form.radios == 1, "selected radio emits no redundant event");
    form.getRadio1()->setSelected(true);
    check(!form.getRadio2()->selected() && form.radios == 1,
          "silent radio setter preserves group invariant");
    (void)controller.handleKey(lithe::Key::select);
    check(form.radios == 2, "radio keyboard uses same group event");

    (void)controller.handlePointer(lithe::Point{190, 70});
    check(form.picks == 1 && form.lastChange.previousIndex == 0U && form.lastChange.index == 1U,
          "picker changes on press exactly once");
    (void)controller.handleKey(lithe::Key::right);
    (void)controller.handleKey(lithe::Key::right);
    check(form.picks == 2 && form.getMode()->selectedIndex() == 2U,
          "clamped unchanged picker emits no event");
    form.getMode()->setSelectedIndex(0U);
    check(form.picks == 2, "picker setter is silent");

    // A centered tap explicitly accepts the existing wheel value.
    (void)controller.handlePointer(lithe::Point{60, 160});
    check(form.commits == 1 && form.changes == 0, "unchanged wheel commit is distinct from change");
    (void)controller.handleKey(lithe::Key::select);
    (void)controller.handleKey(lithe::Key::down);
    (void)controller.handleKey(lithe::Key::back);
    check(form.changes == 2 && form.cancels == 1 && form.commits == 1 &&
              form.getSetpoint()->selectedIndex() == 0U,
          "back restores baseline, changes then cancels without commit");
    check(form.count >= 3U && form.order[form.count - 2U] == 1 && form.order[form.count - 1U] == 3,
          "restoration notification precedes cancellation");
    (void)controller.handleKey(lithe::Key::select);
    (void)controller.handleKey(lithe::Key::down);
    (void)controller.handleKey(lithe::Key::select);
    check(form.commits == 2 && form.lastEdit.initialIndex == 0U && form.lastEdit.index == 1U,
          "key commit carries edit baseline");
    (void)controller.handleKey(lithe::Key::select);
    (void)controller.handleKey(lithe::Key::down);
    (void)controller.handlePointer(lithe::Point{10, 10});
    check(form.commits == 3 && form.lastEdit.index == 2U,
          "ordinary focus loss accepts current edit");

    (void)controller.handlePointer({{60, 160}, lithe::PointerPhase::pressed, 0U});
    (void)controller.cancelPointerCapture();
    check(form.cancels == 2 && form.commits == 3, "lost capture cancels once without commit");
    (void)controller.handlePointer({{60, 160}, lithe::PointerPhase::pressed, 0U});
    form.getRootPanel()->setEnabled(false);
    check(!controller.handlePointer({{60, 184}, lithe::PointerPhase::released, 0U}),
          "disabled capture cannot accept release");
    check(form.cancels == 3 && form.commits == 3, "disabled capture only cancels");
    form.getRootPanel()->setEnabled(true);
    const int previousChanges = form.changes;
    form.getSetpoint()->setSelectedIndex(0U);
    check(form.changes == previousChanges, "wheel refresh is silent");
    (void)controller.handlePointer({{60, 160}, lithe::PointerPhase::pressed, 0U});
    (void)controller.handlePointer({{300, 160}, lithe::PointerPhase::released, 0U});
    check(form.cancels == 4 && form.commits == 3, "tap outside cancels");
    (void)controller.handlePointer({{60, 160}, lithe::PointerPhase::pressed, 0U});
    (void)controller.handlePointer({{300, 136}, lithe::PointerPhase::released, 0U});
    check(form.commits == 4 && form.getSetpoint()->selectedIndex() == 1U,
          "drag outside snaps and commits");
    (void)controller.handlePointer(lithe::Point{230, 70});
    check(form.clicks == 1 && form.order[form.count - 2U] == 4 && form.order[form.count - 1U] == 5,
          "toggle change precedes click and each fires once");
}

void exerciseInvalidEdit()
{
    Form form;
    check(form.build().ok(), "invalid-edit fixture builds");
    auto& controller = *form.controller();
    (void)controller.handlePointer(lithe::Point{60, 160});
    (void)controller.handleKey(lithe::Key::select);
    form.disableOnChange = true;
    (void)controller.handleKey(lithe::Key::down);
    check(form.changes == 2 && form.cancels == 1 && form.commits == 1 &&
              !form.getSetpoint()->editing() && form.getSetpoint()->selectedIndex() == 0U,
          "self-disable during change finishes callback then restores and cancels edit");
    check(controller.focusedView() == nullptr, "disabled focus cleared after dispatch");
    lithe::WheelPickerView empty{{0, 0, 100, 100}, lithe::font5x7(), ""};
    check(!empty.handleKey(lithe::Key::select) && !empty.editing(), "empty wheel rejects editing");
    lithe::PickerView emptyPicker{{0, 0, 100, 30}, lithe::font5x7(), ""};
    emptyPicker.selectNext();
    emptyPicker.setSelectedIndex(99U);
    check(emptyPicker.itemCount() == 0U && emptyPicker.selectedIndex() == 0U,
          "empty picker remains bounded");
}

void exerciseGroupLifetime()
{
    lithe::RadioButtonView first{{}, lithe::font5x7(), "one", true};
    lithe::RadioButtonView second{{}, lithe::font5x7(), "two", true};
    {
        lithe::RadioGroup group;
        check(group.add(first).has_value(), "first choice attached");
        check(group.add(first).error() == lithe::RadioGroupError::alreadyGrouped,
              "duplicate rejected");
        const auto conflicting = group.add(second);
        check(!conflicting && conflicting.error() == lithe::RadioGroupError::conflictingSelection &&
                  second.selected(),
              "conflicting initial selection rejected intact");
    }
    lithe::RadioGroup group;
    check(group.add(first).has_value(), "destroyed group detaches surviving choice");
    {
        lithe::RadioButtonView transient{{}, lithe::font5x7(), "temporary"};
        check(group.add(transient).has_value(), "temporary choice attached");
        transient.setSelected(true);
    }
    check(group.selected() == nullptr, "destroyed choice detaches from surviving group");
    std::array<std::optional<lithe::RadioButtonView>, lithe::RadioGroup::kCapacity> choices;
    for (std::size_t index = 0U; index < choices.size(); ++index)
    {
        auto& choice = choices[index].emplace(lithe::Rect{}, lithe::font5x7(), "bounded");
        const auto added = group.add(choice);
        check(index + 1U < choices.size() ? added.has_value() : !added.has_value(),
              "bounded group capacity");
    }
}
} // namespace

int main()
{
    exerciseForm();
    exerciseGroupLifetime();
    exerciseInvalidEdit();
    return failures == 0 ? 0 : 1;
}
