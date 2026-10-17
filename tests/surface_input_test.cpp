// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/application.hpp"

#include <cstddef>
#include <cstdio>

namespace
{

using namespace lithe;

class RecordingView final : public View
{
public:
    explicit RecordingView(Rect frame) noexcept : View(frame)
    {
        setFocusable(true);
    }

    void handlePointerEvent(PointerEvent event) noexcept override
    {
        View::handlePointerEvent(event);
        lastPosition = event.position;
        switch (event.phase)
        {
        case PointerPhase::pressed:
            ++presses;
            break;
        case PointerPhase::moved:
            ++moves;
            break;
        case PointerPhase::released:
            ++releases;
            break;
        case PointerPhase::cancelled:
            ++cancellations;
            break;
        }
    }

    bool handleKey([[maybe_unused]] Key key) noexcept override
    {
        ++keys;
        return false;
    }

    bool handleTextInput([[maybe_unused]] char32_t codepoint) noexcept override
    {
        ++textInputs;
        return true;
    }

    void activate() noexcept override
    {
        ++activations;
    }

    Point lastPosition{};
    std::size_t presses{0U};
    std::size_t moves{0U};
    std::size_t releases{0U};
    std::size_t cancellations{0U};
    std::size_t keys{0U};
    std::size_t textInputs{0U};
    std::size_t activations{0U};
};

class RecordingDelegate final : public ControllerDelegate
{
public:
    void onAction([[maybe_unused]] ViewController& controller,
                  [[maybe_unused]] ViewId action) noexcept override
    {
        ++actions;
    }

    void onBack([[maybe_unused]] ViewController& controller) noexcept override
    {
    }

    bool onGesture([[maybe_unused]] ViewController& controller,
                   GestureEvent event) noexcept override
    {
        lastGesture = event;
        ++gestures;
        return true;
    }

    GestureEvent lastGesture{};
    std::size_t actions{0U};
    std::size_t gestures{0U};
};

bool roundCapture() noexcept
{
    constexpr Viewport kRound = Viewport::circle({0, 0, 100, 100});
    View root{{0, 0, 100, 100}};
    RecordingView control{{0, 0, 100, 100}};
    RecordingDelegate delegate{};
    ViewController controller{root, delegate};
    Application application{kRound};
    if (!root.addSubview(control) || !application.push(controller) ||
        application.handlePointer({{0, 0}, PointerPhase::pressed, 1U, 0U}) ||
        control.presses != 0U || application.animationsRunning())
    {
        return false;
    }

    if (!application.handlePointer({{50, 50}, PointerPhase::pressed, 1U, 10U}) ||
        !control.pressed() ||
        !application.handlePointer({{60, 50}, PointerPhase::moved, 1U, 20U}) ||
        delegate.lastGesture.type != GestureType::dragStarted ||
        !application.handlePointer({{0, 0}, PointerPhase::moved, 1U, 30U}) || control.moves != 2U ||
        control.lastPosition.x != 0 || control.lastPosition.y != 0 ||
        delegate.lastGesture.type != GestureType::dragged)
    {
        return false;
    }
    // A different contact ending outside must leave the tracked contact alone.
    application.handlePointer({{0, 0}, PointerPhase::released, 2U, 35U});
    if (control.cancellations != 0U || !control.pressed() ||
        !application.handlePointer({{0, 0}, PointerPhase::released, 1U, 40U}) ||
        control.cancellations != 1U || control.releases != 0U || control.pressed() ||
        control.activations != 0U || delegate.actions != 0U ||
        delegate.lastGesture.type != GestureType::swipeLeft ||
        delegate.lastGesture.position.x != 0 || delegate.lastGesture.position.y != 0 ||
        delegate.lastGesture.contactId != 1U || controller.cancelPointerCapture() ||
        application.animationsRunning())
    {
        return false;
    }
    // The cancelled control capture and completed gesture both permit a new tap.
    if (!application.handlePointer({{50, 50}, PointerPhase::pressed, 3U, 50U}) ||
        !application.handlePointer({{50, 50}, PointerPhase::released, 3U, 60U}) ||
        control.activations != 1U || delegate.actions != 1U || control.releases != 1U ||
        delegate.lastGesture.type != GestureType::tap || controller.cancelPointerCapture())
    {
        return false;
    }
    // Leaving the circle during capture does not cancel a release back inside.
    return application.handlePointer({{50, 50}, PointerPhase::pressed, 4U, 70U}) &&
           application.handlePointer({{0, 0}, PointerPhase::moved, 4U, 80U}) &&
           application.handlePointer({{50, 50}, PointerPhase::released, 4U, 90U}) &&
           control.activations == 2U && delegate.actions == 2U &&
           delegate.lastGesture.type == GestureType::dragEnded && control.cancellations == 1U &&
           !control.pressed();
}

bool ancestorFocus() noexcept
{
    View root{{0, 0, 100, 100}};
    View hidden{{0, 0, 20, 20}};
    View nested{{0, 0, 20, 20}};
    RecordingView hiddenChild{{0, 0, 20, 20}};
    View disabled{{20, 0, 20, 20}};
    RecordingView disabledChild{{0, 0, 20, 20}};
    View eligible{{0, 30, 100, 60}};
    RecordingView first{{0, 0, 40, 20}};
    RecordingView second{{0, 30, 40, 20}};
    RecordingDelegate delegate{};
    ViewController controller{root, delegate};
    if (!root.addSubview(hidden) || !hidden.addSubview(nested) || !nested.addSubview(hiddenChild) ||
        !root.addSubview(disabled) || !disabled.addSubview(disabledChild) ||
        !root.addSubview(eligible) || !eligible.addSubview(first) || !eligible.addSubview(second))
    {
        return false;
    }
    hidden.setVisible(false);
    disabled.setEnabled(false);
    controller.activate();
    if (controller.focusedView() != &first || !first.focused() || hiddenChild.focused() ||
        disabledChild.focused() || !controller.handleKey(Key::up) ||
        controller.focusedView() != &second || !controller.handleKey(Key::down) ||
        controller.focusedView() != &first || !controller.handleKey(Key::select) ||
        first.activations != 1U || delegate.actions != 1U)
    {
        return false;
    }

    const std::size_t firstKeys = first.keys;
    eligible.setVisible(false);
    if (controller.handleKey(Key::select) || first.keys != firstKeys || first.focused() ||
        controller.focusedView() != nullptr || delegate.actions != 1U ||
        controller.handleKey(Key::down))
    {
        return false;
    }

    eligible.setVisible(true);
    controller.activate();
    if (controller.focusedView() != &first || !controller.handleTextInput(U'a') ||
        first.textInputs != 1U)
    {
        return false;
    }
    eligible.setEnabled(false);
    if (controller.handleTextInput(U'b') || first.textInputs != 1U || first.focused() ||
        controller.focusedView() != nullptr)
    {
        return false;
    }

    eligible.setEnabled(true);
    controller.activate();
    eligible.setVisible(false);
    if (controller.focusedView() != nullptr || first.focused())
    {
        return false;
    }
    // Reactivation must discard a retained descendant of an ineligible branch.
    eligible.setVisible(true);
    controller.activate();
    eligible.setEnabled(false);
    hidden.setVisible(true);
    controller.activate();
    if (controller.focusedView() != &hiddenChild || !hiddenChild.focused() || first.focused())
    {
        return false;
    }
    // The controller root is part of the same eligibility chain.
    root.setVisible(false);
    controller.activate();
    if (controller.focusedView() != nullptr || hiddenChild.focused())
    {
        return false;
    }
    root.setVisible(true);
    controller.activate();
    root.setEnabled(false);
    if (controller.handleKey(Key::select) || hiddenChild.keys != 0U ||
        controller.focusedView() != nullptr || hiddenChild.focused())
    {
        return false;
    }
    root.setEnabled(true);
    return controller.handleKey(Key::down) && controller.focusedView() == &hiddenChild &&
           hiddenChild.focused() && controller.handleKey(Key::select) &&
           hiddenChild.activations == 1U && delegate.actions == 2U && disabledChild.keys == 0U &&
           disabledChild.activations == 0U;
}

bool deeplyNestedFocus() noexcept
{
    // Direct default initialization invokes View's explicit constructor for
    // every element; that constructor initializes the complete view state.
    std::array<View, 256U> ancestors;
    View root{};
    View last{};
    RecordingView leaf{{0, 0, 10, 10}};
    last.setFocusable(true);
    if (!root.addSubview(ancestors.front()) || !root.addSubview(last))
    {
        return false;
    }
    for (std::size_t index = 1U; index < ancestors.size(); ++index)
    {
        if (!ancestors[index - 1U].addSubview(ancestors[index]))
        {
            return false;
        }
    }
    if (!ancestors.back().addSubview(leaf))
    {
        return false;
    }
    ViewController controller{root};
    controller.activate();
    if (controller.focusedView() != &leaf || !controller.handleKey(Key::down) ||
        controller.focusedView() != &last || !controller.handleKey(Key::down) ||
        controller.focusedView() != &leaf)
    {
        return false;
    }
    ancestors[128U].setEnabled(false);
    controller.activate();
    return controller.focusedView() == &last;
}

} // namespace

int main()
{
    if (!roundCapture())
    {
        std::puts("FAIL: circular viewport capture and gesture lifecycle");
        return 1;
    }
    if (!ancestorFocus() || !deeplyNestedFocus())
    {
        std::puts("FAIL: ancestor-aware focus collection and input dispatch");
        return 1;
    }
    std::puts("PASS: surface input capture and ancestor focus");
    return 0;
}
