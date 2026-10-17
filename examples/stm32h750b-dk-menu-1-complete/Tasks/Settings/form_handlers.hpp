// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// LiTheView handwritten form: form.main
#pragma once
#include "Application/navigation.hpp"
#include "Generated/form.hpp"
namespace menu
{
class SettingsMenuForm final : public lithe::generated::SettingsMenu::Instance<>
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
#if MENU_HAS_VOLUME
        mRequests.offer(Event::openVolume);
#endif
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
