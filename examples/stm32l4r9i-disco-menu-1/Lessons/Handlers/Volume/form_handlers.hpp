// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

// LiTheView handwritten form: form.main
#pragma once
#include "Application/navigation.hpp"
#include "Application/volume.hpp"
#include "menu_form_types.hpp"
namespace menu
{
class VolumeMenuForm final : public menu::forms::Volume::Instance<>
{
public:
    explicit VolumeMenuForm(Requests& requests, Volume& volume) noexcept
        : mRequests{requests}, mVolume{volume}
    {
    }
    // Call after build, and after each application-owned volume change.
    void refresh() noexcept
    {
        if (auto* progress = getVolumeProgress(); progress != nullptr)
        {
            progress->setValue(mVolume.value());
        }
        if (auto* label = getLevelLabel(); label != nullptr)
        {
            label->setText(mVolume.text());
        }
    }
    void adjust(VolumeChange change) noexcept
    {
        mVolume.adjust(change);
        refresh();
    }

protected:
    // <litheview-handlers>
    // litheview-handler component.main.minus onMinusButtonClicked
    void onMinusButtonClicked() noexcept override
    {
        adjust(VolumeChange::decrease);
    }
    // litheview-handler component.main.plus onPlusButtonClicked
    void onPlusButtonClicked() noexcept override
    {
        adjust(VolumeChange::increase);
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
    Volume& mVolume;
};
} // namespace menu
