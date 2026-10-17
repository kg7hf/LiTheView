// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "designer-fixtures/text-events/form.hpp"
#include "lithe/detail/representation.hpp"

#include <algorithm>
#include <array>
#include <charconv>
#include <cstdio>
#include <optional>

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

std::optional<unsigned> parseSetpoint(std::string_view text) noexcept
{
    if (text.empty())
    {
        return std::nullopt;
    }
    unsigned value{0U};
    const auto result = std::from_chars(text.data(), text.end(), value);
    if (result.ec != std::errc{} || result.ptr != text.end() || value > 100U)
    {
        return std::nullopt;
    }
    return value;
}

// This specialization supplies policy, while TextFieldView owns the edit lifecycle.
class SetpointField final : public lithe::TextFieldView
{
public:
    using TextFieldView::TextFieldView;
    unsigned commits{0U};

protected:
    bool acceptsText(std::string_view text) const noexcept override
    {
        return parseSetpoint(text).has_value();
    }
    void onTextEditCommitted([[maybe_unused]] lithe::TextEdit event) noexcept override
    {
        ++commits;
    }
};

class Resources final : public lithe::ResourceProvider
{
public:
    const lithe::Bitmap* bitmap([[maybe_unused]] std::string_view name) const noexcept override
    {
        return nullptr;
    }
    lithe::MutableTextBuffer textBuffer(std::string_view name) const noexcept override
    {
        return name == "setpoint" ? std::span{mDraft} : std::span<char>{};
    }
    lithe::MutableTextBuffer textEditBuffer(std::string_view name) const noexcept override
    {
        return name == "setpoint" ? std::span{mBaseline} : std::span<char>{};
    }

private:
    mutable std::array<char, 4U> mDraft{'2', '5'};
    mutable std::array<char, 4U> mBaseline{};
};

using Generated = lithe::generated::TextSetpoint;
class Form final
    : public Generated::Instance<lithe::WidgetOverrides<Generated::SetpointType<SetpointField>>>
{
public:
    unsigned applied{25U};
    unsigned commits{0U};
    unsigned cancels{0U};

protected:
    void onSetpointTextChanged(lithe::TextChange event) noexcept override
    {
        getStatus()->setText(parseSetpoint(event.text) ? "DRAFT - ENTER TO APPLY"
                                                       : "INVALID 0 TO 100");
    }
    void onSetpointEditCommitted(lithe::TextEdit event) noexcept override
    {
        const auto value = parseSetpoint(event.text);
        check(value.has_value(), "only validated edits reach application commit");
        if (!value)
        {
            return;
        }
        applied = *value;
        ++commits;
        check(getSetpoint()->commits == commits, "specialization precedes generated form callback");
        // LabelView borrows its text. Copy the event into persistent application
        // storage so the next draft cannot change the applied-value label.
        std::copy(event.text.begin(), event.text.end(), mAppliedText.begin());
        mAppliedText[event.text.size()] = '\0';
        getApplied()->setText({mAppliedText.data(), event.text.size()});
        getStatus()->setText("APPLIED");
    }
    void onSetpointEditCancelled([[maybe_unused]] lithe::TextEdit event) noexcept override
    {
        ++cancels;
        getStatus()->setText("CANCELLED");
    }

private:
    std::array<char, 4U> mAppliedText{};
};
} // namespace

int main()
{
    Resources resources;
    Form form;
    check(form.build(lithe::font5x7(), &resources).ok(), "generated text form builds");
    auto& controller = *form.controller();
    controller.activate();
    const auto replaceDraft = [&](std::string_view text)
    {
        while (!form.getSetpoint()->text().empty())
        {
            check(controller.handleTextInput(U'\b'), "delete draft character");
        }
        for (const char character : text)
        {
            check(controller.handleTextInput(lithe::detail::codeUnit(character)), "type draft");
        }
    };
    replaceDraft("75");
    check(form.applied == 25U && form.getApplied()->text() == "25" &&
              form.getStatus()->text() == "DRAFT - ENTER TO APPLY",
          "draft does not change backend");
    check(controller.handleKey(lithe::Key::select) && form.applied == 75U &&
              form.getApplied()->text() == "75" && form.commits == 1U,
          "apply updates backend and label");
    replaceDraft("999");
    check(form.getStatus()->text() == "INVALID 0 TO 100", "invalid draft has feedback");
    check(controller.handleKey(lithe::Key::select) && form.applied == 75U &&
              form.getSetpoint()->editing() && form.commits == 1U,
          "invalid commit stays editable");
    check(controller.handleKey(lithe::Key::back) && form.getSetpoint()->text() == "75" &&
              form.getStatus()->text() == "CANCELLED" && form.cancels == 1U,
          "cancel restores applied draft");
    replaceDraft("0");
    check(form.getApplied()->text() == "75", "applied label does not alias draft storage");
    check(controller.handleKey(lithe::Key::select) && form.applied == 0U, "zero is valid");
    replaceDraft("100");
    check(controller.handleKey(lithe::Key::select) && form.applied == 100U, "upper bound is valid");
    replaceDraft("");
    check(controller.handleKey(lithe::Key::select) && form.applied == 100U &&
              form.getSetpoint()->editing(),
          "empty draft is not zero");
    check(controller.handleKey(lithe::Key::back) && form.getSetpoint()->text() == "100",
          "cancel empty draft");
    replaceDraft("5x");
    check(controller.handleKey(lithe::Key::select) && form.applied == 100U,
          "partial numeric parse must not commit");
    return failures == 0 ? 0 : 1;
}
