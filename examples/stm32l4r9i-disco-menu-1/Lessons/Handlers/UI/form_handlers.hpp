// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// LiTheView handwritten form: form.main
#pragma once
#include "Application/navigation.hpp"
#include "menu_form_types.hpp"
namespace menu
{
class HomeMenuForm final : public menu::forms::Home::Instance<>
{
public:
    explicit HomeMenuForm(Requests& requests) noexcept : mRequests{requests}
    {
    }

protected:
    // <litheview-handlers>
    // litheview-handler component.main.play onPlayButtonClicked
    void onPlayButtonClicked() noexcept override
    { /* Demonstration placeholder: no device operation or state
         change. */
    }
    // litheview-handler component.main.record onRecordButtonClicked
    void onRecordButtonClicked() noexcept override
    { /* Demonstration placeholder: no device operation or state
         change. */
    }
    // litheview-handler component.main.settings onSettingsButtonClicked
    void onSettingsButtonClicked() noexcept override
    {
        mRequests.offer(Event::openSettings);
    }
    // </litheview-handlers>
    void onBack(lithe::ViewController&) noexcept override
    {
        mRequests.offer(Event::back);
    }

private:
    Requests& mRequests;
};
} // namespace menu
