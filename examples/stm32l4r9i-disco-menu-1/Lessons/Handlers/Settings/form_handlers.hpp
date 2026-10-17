// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// LiTheView handwritten form: form.main
#pragma once
#include "Application/navigation.hpp"
#include "menu_features.hpp"
#include "menu_form_types.hpp"
namespace menu
{
class SettingsMenuForm final : public menu::forms::Settings::Instance<>
{
public:
    explicit SettingsMenuForm(Requests& requests) noexcept : mRequests{requests}
    {
    }

protected:
    // <litheview-handlers>
    // litheview-handler component.main.volume onVolumeButtonClicked
    void onVolumeButtonClicked() noexcept override
    {
        if constexpr (features::hasVolume)
        {
            mRequests.offer(Event::openVolume);
        }
    }
    // litheview-handler component.main.storage onStorageButtonClicked
    void onStorageButtonClicked() noexcept override
    {
        mRequests.offer(Event::openStorage);
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
