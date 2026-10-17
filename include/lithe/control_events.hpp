// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstddef>
#include <string_view>

namespace lithe
{
inline constexpr unsigned kControlEventContractVersion{3U};

class View;
class RadioButtonView;

struct CheckedChange
{
    bool previousValue;
    bool value;
};

struct SelectionChange
{
    std::size_t previousIndex;
    std::size_t index;
};

// Choices are borrowed for this synchronous call; they are never owned by an event.
struct RadioSelectionChange
{
    const RadioButtonView* previous;
    const RadioButtonView* selected;
};

struct SelectionEdit
{
    std::size_t initialIndex;
    std::size_t index;
};

// Text views borrow the caller's buffers for this synchronous notification only.
// No previous-text copy is hidden inside a change event.
struct TextChange
{
    std::size_t previousLength;
    std::string_view text;
};

struct TextEdit
{
    std::string_view initialText;
    std::string_view text;
};

// Internal typed dispatch boundary. No queue, allocation, or erased context.
class ControlEventSink
{
public:
    ControlEventSink() = default;
    virtual ~ControlEventSink() = default;
    ControlEventSink(const ControlEventSink&) = delete;
    ControlEventSink& operator=(const ControlEventSink&) = delete;
    ControlEventSink(ControlEventSink&&) = delete;
    ControlEventSink& operator=(ControlEventSink&&) = delete;
    virtual void checkedChanged(View& source, CheckedChange event) noexcept = 0;
    virtual void selectionChanged(View& source, SelectionChange event) noexcept = 0;
    virtual void radioSelectionChanged(View& source, RadioSelectionChange event) noexcept = 0;
    virtual void editCommitted(View& source, SelectionEdit event) noexcept = 0;
    virtual void editCancelled(View& source, SelectionEdit event) noexcept = 0;
    virtual void textChanged(View& source, TextChange event) noexcept = 0;
    virtual void textEditCommitted(View& source, TextEdit event) noexcept = 0;
    virtual void textEditCancelled(View& source, TextEdit event) noexcept = 0;
};
} // namespace lithe
