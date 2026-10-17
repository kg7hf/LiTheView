// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// LiTheView handwritten form: form.storage
#pragma once
#include "Application/navigation.hpp"
#include "../Generated/Storage/form.hpp"
namespace menu
{
class StorageMenuForm final : public lithe::generated::StorageMenu::Instance<>
{
public:
    explicit StorageMenuForm(Requests& requests) noexcept : mRequests{requests}
    {
    }

protected:
    // <litheview-handlers>
    // litheview-handler component.main.save onSaveButtonClicked
    void onSaveButtonClicked() noexcept override
    { /* Demonstration placeholder: no device operation or state change. */
    }
    // litheview-handler component.main.load onLoadButtonClicked
    void onLoadButtonClicked() noexcept override
    {
        mRequests.offer(Event::openLoad);
    }
    // litheview-handler component.main.back onBackButtonClicked
    void onBackButtonClicked() noexcept override
    {
        mRequests.offer(Event::back);
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
