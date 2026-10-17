// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/controller.hpp"
#include "lithe/widgets.hpp"

#include <array>
#include <cstdio>
#include <string>

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

class Field : public lithe::TextFieldView
{
public:
    using TextFieldView::TextFieldView;
    int changes{0};
    int commits{0};
    int cancels{0};
    bool reject{false};
    bool disableOnChange{false};
    bool cancelInsideChange{false};
    std::string order;

protected:
    bool acceptsText([[maybe_unused]] std::string_view value) const noexcept override
    {
        return !reject;
    }
    void onTextChanged(lithe::TextChange event) noexcept override
    {
        ++changes;
        order += 'c';
        check(event.text == text(), "changed text already settled");
        check(!setText("reentrant"), "silent setter cannot invalidate borrowed event text");
        check(!handleTextInput(U'X'), "direct recursive input rejected");
        if (cancelInsideChange)
        {
            check(cancelEdit().error() == lithe::TextEditError::reentrant,
                  "nested cancellation rejected");
        }
        if (disableOnChange)
        {
            setEnabled(false);
        }
    }
    void onTextEditCommitted(lithe::TextEdit event) noexcept override
    {
        ++commits;
        order += 'a';
        check(!editing() && text() == event.text, "commit state settled before hook");
        check(!beginEdit(), "cannot overwrite borrowed initial text during commit");
    }
    void onTextEditCancelled(lithe::TextEdit event) noexcept override
    {
        ++cancels;
        order += 'x';
        check(!editing() && event.initialText == text() && event.text == text(),
              "cancel restores text before hook");
    }
};

class Delegate final : public lithe::ControllerDelegate
{
public:
    explicit Delegate(Field& field) : mField(field)
    {
    }
    int changes{0};
    int commits{0};
    int cancels{0};
    int actions{0};
    int backs{0};
    std::string initial;
    std::string accepted;
    void onAction(lithe::ViewController&, lithe::ViewId) noexcept override
    {
        ++actions;
    }
    void onBack(lithe::ViewController&) noexcept override
    {
        ++backs;
    }
    void onControlTextChanged(lithe::ViewController& controller,
                              lithe::View& source,
                              lithe::TextChange event) noexcept override
    {
        ++changes;
        check(&source == &mField && mField.changes == changes, "control hook before form");
        check(event.text == mField.text(), "form borrows settled draft");
        check(!controller.handleKey(lithe::Key::back) && !controller.handleTextInput(U'9'),
              "controller rejects nested dispatch");
    }
    void onControlTextEditCommitted(lithe::ViewController&,
                                    lithe::View&,
                                    lithe::TextEdit event) noexcept override
    {
        ++commits;
        initial = event.initialText;
        accepted = event.text;
        check(mField.commits == commits, "control commit hook before form");
    }
    void onControlTextEditCancelled(lithe::ViewController&,
                                    lithe::View&,
                                    lithe::TextEdit event) noexcept override
    {
        ++cancels;
        check(event.text == event.initialText && !mField.editing(), "form sees rollback");
    }

private:
    Field& mField;
};

void lifecycle()
{
    std::array<char, 9> draft{'2', '5'};
    std::array<char, 9> baseline{};
    Field field{{0, 0, 100, 30}, lithe::font5x7(), draft, 8U, {}, {}, baseline};
    lithe::ButtonView other{{0, 40, 100, 30}, lithe::font5x7(), "OTHER"};
    lithe::View root{{0, 0, 120, 90}};
    check(root.addSubview(field) && root.addSubview(other), "attach controls");
    Delegate delegate{field};
    lithe::ViewController controller{root, delegate};
    controller.activate();
    field.cancelInsideChange = true;
    check(controller.handleTextInput(U'0') && field.editing(), "typing starts transaction");
    check(controller.handleKey(lithe::Key::back), "Back cancels");
    check(field.text() == "25" && field.order == "ccx" && delegate.cancels == 1 &&
              delegate.commits == 0 && delegate.backs == 0,
          "rollback changed then cancelled; no commit or back leakage");
    check(controller.handlePointer(lithe::Point{10, 10}) && field.editing(),
          "pointer activation starts same edit");
    check(controller.handleKey(lithe::Key::select), "Select accepts unchanged edit");
    check(delegate.commits == 1 && delegate.initial == "25" && delegate.accepted == "25",
          "explicit unchanged commit remains an event");
    check(controller.handleTextInput(U'1'), "append accepted input");
    field.reject = true;
    check(controller.handleKey(lithe::Key::select) && field.editing() && delegate.commits == 1,
          "invalid commit is consumed and keeps draft");
    field.reject = false;
    check(controller.handleKey(lithe::Key::select) && delegate.accepted == "251" &&
              delegate.initial == "25",
          "validated commit captures original and new value");
    const int before = field.changes;
    check(field.setText("42") && !field.editing() && field.changes == before,
          "programmatic refresh is silent");
    check(controller.handleTextInput(U'0') && field.setText("7") && !field.editing(),
          "authoritative refresh ends pending edit silently");
    check(controller.handleTextInput(U'2') && controller.handleKey(lithe::Key::back) &&
              field.text() == "7",
          "next edit rolls back to refreshed value");
    check(controller.handleTextInput(U'3') && controller.handleKey(lithe::Key::down) &&
              field.text() == "7" && !field.editing(),
          "focus loss cancels draft");
    check(controller.handleKey(lithe::Key::up), "return focus");
    field.disableOnChange = true;
    check(controller.handleTextInput(U'4') && !field.editing() && field.text() == "7" &&
              controller.focusedView() == nullptr,
          "self-disable cancels after change notification");
    field.disableOnChange = false;
    field.setEnabled(true);
    controller.activate();
    check(controller.handleTextInput(U'5'), "edit before hidden ancestor");
    root.setVisible(false);
    check(controller.focusedView() == nullptr && field.text() == "7" && !field.editing(),
          "hidden ancestor cleanup restores edit");
    check(!field.handleTextInput(U'6'), "direct input respects hidden ancestors");
}

void boundedStorage()
{
    std::array<char, 5> draft{};
    std::array<char, 5> baseline{};
    Field field{{}, lithe::font5x7(), draft, 4U, {}, {}, baseline};
    check(!field.handleTextInput(U'\b') && !field.editing(), "empty backspace does not start edit");
    check(!field.handleTextInput(U'\r') && !field.handleTextInput(0xd800U) &&
              !field.handleTextInput(0x110000U) && field.changes == 0,
          "invalid Unicode and control codes have no events");
    check(field.handleTextInput(0x1f600U) && field.text().size() == 4U,
          "four-byte scalar fits exactly");
    check(!field.handleTextInput(U'1') && field.changes == 1 && draft.back() == '\0',
          "capacity rejects without mutation or notification");
    check(field.handleTextInput(U'\b') && field.text().empty(),
          "backspace removes complete scalar");
    check(field.cancelEdit().has_value() && field.changes == 2 && field.cancels == 1,
          "unchanged rollback emits only cancellation");
    check(field.setText("1234") && field.setText(field.text().substr(1)) && field.text() == "234",
          "silent setter supports aliasing");
    check(!field.setText(std::string_view{"a\0b", 3U}), "embedded NUL rejected");
    std::array<char, 2> shortBaseline{'s', 'x'};
    Field shortField{{}, lithe::font5x7(), draft, 4U, {}, {}, shortBaseline};
    check(shortField.beginEdit().error() == lithe::TextEditError::storageUnavailable &&
              !shortField.handleTextInput(U'5') && shortBaseline[0] == 's',
          "insufficient snapshot rejects before writing");
    Field alias{{}, lithe::font5x7(), draft, 4U, {}, {}, draft};
    check(!alias.beginEdit() && !alias.handleTextInput(U'1'), "overlapping snapshot rejected");
    Field immediate{{}, lithe::font5x7(), draft};
    check(immediate.setText("") && immediate.handleTextInput(U'1') && immediate.changes == 1 &&
              !immediate.editing() && !immediate.beginEdit(),
          "legacy provider supports immediate changes without hidden rollback allocation");
    Field empty{{}, lithe::font5x7(), {}, 0U, {}, {}, baseline};
    check(!empty.beginEdit() && !empty.setText("") && !empty.handleTextInput(U'1'),
          "empty storage fails closed");
}
} // namespace

int main()
{
    lifecycle();
    boundedStorage();
    return failures == 0 ? 0 : 1;
}
