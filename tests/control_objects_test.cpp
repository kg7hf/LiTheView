// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "designer-fixtures/control-handlers/form.hpp"
#include "lithe/controller.hpp"
#include "lithe/layout.hpp"

#include <array>
#include <cstdio>
#include <type_traits>

namespace
{
int failures{0};
void check(bool condition, const char* message)
{
    if (!condition)
    {
        std::printf("FAIL: %s\n", message);
        ++failures;
    }
}

class alignas(64) CountingButton : public lithe::ButtonView
{
public:
    using ButtonView::ButtonView;
    CountingButton(const CountingButton&) = delete;
    CountingButton& operator=(const CountingButton&) = delete;
    CountingButton(CountingButton&&) = delete;
    CountingButton& operator=(CountingButton&&) = delete;
    ~CountingButton() override
    {
        ++destructions;
    }
    static inline int destructions{0};
    int clicks{0};
    bool selectedAtClick{false};
    lithe::ViewController* disableThroughController{nullptr};
    std::array<unsigned char, 129U> applicationState{};

protected:
    void onClicked() noexcept override
    {
        ++clicks;
        selectedAtClick = selected();
        if (disableThroughController != nullptr)
        {
            setEnabled(false);
            (void)disableThroughController->focusedView();
        }
    }
};

class Observer : public lithe::ControllerDelegate
{
public:
    void onAction(lithe::ViewController&, lithe::ViewId) noexcept override
    {
        ++numericCalls;
    }
    void onBack(lithe::ViewController&) noexcept override
    {
    }
    void onControlAction(lithe::ViewController&, lithe::View& source) noexcept override
    {
        ++typedCalls;
        lastSource = &source;
    }
    int typedCalls{0};
    int numericCalls{0};
    lithe::View* lastSource{nullptr};
};

constexpr auto specs = []
{
    std::array<lithe::ViewSpec, 4U> result{};
    result[0].frame = {0, 0, 100, 100};
    result[1].kind = lithe::ViewKind::button;
    result[1].id = 1U;
    result[1].parent = 0U;
    result[1].frame = {0, 0, 40, 30};
    result[1].buttonBehavior = lithe::ButtonBehavior::toggle;
    result[2].kind = lithe::ViewKind::label;
    result[2].parent = 0U;
    result[2].text = "OFF";
    result[3].kind = lithe::ViewKind::button;
    result[3].parent = 0U;
    result[3].frame = {50, 0, 40, 30};
    return result;
}();
using Overrides =
    lithe::WidgetOverrides<lithe::WidgetOverride<3U, lithe::ViewKind::button, CountingButton>,
                           lithe::WidgetOverride<1U, lithe::ViewKind::button, CountingButton>>;
using Layout = lithe::StaticLayout<4U, lithe::AllWidgets, Overrides>;
static_assert(alignof(Layout) >= alignof(CountingButton));
static_assert(std::is_same_v<Layout::ComponentType<1U, lithe::ButtonView>, CountingButton>);
static_assert(!lithe::WidgetOverrides<
              lithe::WidgetOverride<1U, lithe::ViewKind::button, CountingButton>,
              lithe::WidgetOverride<1U, lithe::ViewKind::button, CountingButton>>::fits<4U>());

using Generated = lithe::generated::FirstDisplayMainForm;
class HandwrittenForm
    : public Generated::Instance<lithe::WidgetOverrides<Generated::Button1Type<CountingButton>>>
{
public:
    int calls{0};

protected:
    void onButton1Clicked() noexcept override
    {
        check(!controller()->handleKey(lithe::Key::select), "recursive input fails closed");
        ++calls;
        getLabel1()->setText(calls % 2 == 0 ? "ON" : "OFF");
    }
};
} // namespace

int main()
{
    Layout layout;
    check(layout.component<1U, lithe::ButtonView>() == nullptr, "unbuilt access is checked");
    check(layout.build(specs).ok(), "actual derived controls build");
    auto* first = layout.component<1U, lithe::ButtonView>();
    auto* second = layout.component<3U, lithe::ButtonView>();
    auto* label = layout.component<2U, lithe::LabelView>();
    if (first == nullptr || second == nullptr || label == nullptr || layout.root() == nullptr)
    {
        return 1;
    }
    first->applicationState.back() = 97U;
    check(layout.find<CountingButton>(1U) == first &&
              std::as_const(layout).find<CountingButton>(1U) == first &&
              layout.find<CountingButton>(0U) == nullptr,
          "typed lookup keeps the selected derived type and rejects unrelated slots");
    label->setText("ON");
    check(label->text() == "ON", "named storage ignores duplicate runtime IDs");
    check(first->applicationState.back() == 97U && second->applicationState.back() == 0U,
          "larger derived objects have independent storage");
    {
        Observer observer;
        lithe::ViewController controller{*layout.root(), observer};
        controller.activate();
        check(controller.handleKey(lithe::Key::select), "keyboard activation");
        check(first->clicks == 1 && first->selectedAtClick, "hook follows toggle state once");
        check(observer.typedCalls == 1 && observer.numericCalls == 0 &&
                  observer.lastSource == first,
              "one typed notification after component hook");
        check(controller.handlePointer(lithe::Point{55, 5}), "pointer activation");
        check(second->clicks == 1 && observer.typedCalls == 2,
              "second instance routes by identity");
        check(controller.handlePointer({{5, 5}, lithe::PointerPhase::pressed, 0U}), "press");
        check(controller.cancelPointerCapture(), "cancel capture");
        check(first->clicks == 1 && observer.typedCalls == 2, "cancel never clicks");
        check(controller.handlePointer({{5, 5}, lithe::PointerPhase::pressed, 0U}), "press again");
        (void)controller.handlePointer({{99, 99}, lithe::PointerPhase::released, 0U});
        check(first->clicks == 1, "release outside never clicks");
        first->setEnabled(false);
        check(!controller.handlePointer(lithe::Point{5, 5}),
              "disabled button does not receive input");
        first->setEnabled(true);
        controller.activate();
        first->disableThroughController = &controller;
        check(controller.handleKey(lithe::Key::select) && controller.focusedView() == nullptr &&
                  observer.lastSource == first,
              "a hook may disable itself without losing the original notification source");
        layout.root()->setEnabled(false);
        check(!controller.handleKey(lithe::Key::select),
              "disabled ancestor cancels focus eligibility");
    }
    layout.destroy();
    check(CountingButton::destructions == 2, "actual derived destructors run exactly once");
    check(layout.component<1U, lithe::ButtonView>() == nullptr, "destroyed access is checked");
    auto invalid = specs;
    invalid[3].kind = lithe::ViewKind::label;
    check(layout.build(invalid).error == lithe::LayoutError::invalidKind,
          "mismatched override fails closed");
    check(layout.root() == nullptr && CountingButton::destructions == 3,
          "partial failure releases only constructed objects");
    check(layout.build(specs).ok(), "rebuild after failure");
    layout.destroy();
    check(CountingButton::destructions == 5, "rebuild lifetime is balanced");
    {
        HandwrittenForm form;
        check(form.controller() == nullptr && form.getButton1() == nullptr,
              "unbuilt generated form");
        check(form.build().ok(), "generated form builds its selected derived type");
        static_assert(std::is_same_v<decltype(form.getButton1()), CountingButton*>);
        if (form.controller() == nullptr)
        {
            return 1;
        }
        form.controller()->activate();
        check(form.build().error == lithe::LayoutError::alreadyBuilt,
              "form cannot rebuild live views");
        check(form.controller()->handleKey(lithe::Key::select), "generated form dispatches");
        check(form.calls == 1 && form.getButton1()->clicks == 1 &&
                  form.getLabel1()->text() == "OFF",
              "component hook and named form handler each run once");
        check(form.controller()->handlePointer(lithe::Point{160, 170}),
              "generated pointer dispatch");
        check(form.calls == 2 && form.getLabel1()->text() == "ON", "same handler toggles back");
    }
    check(CountingButton::destructions == 6, "generated form owns actual derived lifetime");
    std::printf("Control object checks: %d failure(s)\n", failures);
    return failures == 0 ? 0 : 1;
}
