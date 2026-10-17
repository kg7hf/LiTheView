// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Application/navigation.hpp"
#include "Tasks/UI/Generated/Home/resources.hpp"
#include "Tasks/UI/Generated/Load/resources.hpp"
#include "Tasks/UI/Generated/Settings/resources.hpp"
#include "Tasks/UI/Generated/Storage/resources.hpp"
#include "Tasks/UI/Generated/Volume/resources.hpp"
#include "Tasks/UI/handlers/Home.hpp"
#include "Tasks/UI/handlers/Load.hpp"
#include "Tasks/UI/handlers/Settings.hpp"
#include "Tasks/UI/handlers/Storage.hpp"
#include "Tasks/UI/handlers/Volume.hpp"
#include "lithe/application.hpp"
#include "lithe/tiled_renderer.hpp"
namespace menu
{
class Menu final
{
public:
    explicit Menu(lithe::Display& display, lithe::Viewport viewport = {}) noexcept
        : mApplication{display, viewport}
    {
    }
    explicit Menu(lithe::Viewport viewport) noexcept : mApplication{viewport}
    {
    }
    [[nodiscard]] bool
    renderTiled(lithe::Size size, lithe::Surface workspace, lithe::TileSink& sink) noexcept
    {
        return mInitialized &&
               lithe::TiledRenderer::render(mApplication, size, workspace, sink).ok();
    }
    ~Menu() = default;
    Menu(const Menu&) = delete;
    Menu& operator=(const Menu&) = delete;
    Menu(Menu&&) = delete;
    Menu& operator=(Menu&&) = delete;
    [[nodiscard]] bool initialize() noexcept
    {
        if (mInitialized || !mHome.build(lithe::font5x7(), &mHomeResources) ||
            !mSettings.build(lithe::font5x7(), &mSettingsResources) ||
            !mStorage.build(lithe::font5x7(), &mStorageResources) ||
            mStorage.controller() == nullptr || mHome.controller() == nullptr ||
            mSettings.controller() == nullptr)
        {
            return false;
        }
        if (!mVolume.build(lithe::font5x7(), &mVolumeResources) || mVolume.controller() == nullptr)
        {
            return false;
        }
        mVolume.refresh();
        if (!mLoad.build(lithe::font5x7(), &mLoadResources) || mLoad.controller() == nullptr ||
            !mLoad.connect())
        {
            return false;
        }
        mHome.refreshPlayback();
        mInitialized = mApplication.pushScene(*mHome.controller());
        return mInitialized;
    }
    [[nodiscard]] bool key(lithe::Key key) noexcept
    {
        if (!mInitialized)
        {
            return false;
        }
        // On Volume only, horizontal directions adjust; vertical directions keep
        // the normal focus traversal and center activates the focused control.
        if (page() == Page::volume && (key == lithe::Key::left || key == lithe::Key::right))
        {
            mVolume.adjust(key == lithe::Key::right ? VolumeChange::increase
                                                    : VolumeChange::decrease);
            return true;
        }
        (void)mApplication.handleKey(key);
        return applyRequests();
    }
    [[nodiscard]] bool pointer(const lithe::PointerEvent& event) noexcept
    {
        if (!mInitialized)
        {
            return false;
        }
        (void)mApplication.handlePointer(event);
        return applyRequests();
    }
    void cancelPointer() noexcept
    {
        if (auto* active = mApplication.activeController(); active != nullptr)
        {
            (void)active->cancelPointerCapture();
        }
    }
    [[nodiscard]] bool navigate(Event event) noexcept
    {
        if (!mInitialized)
        {
            return false;
        }
        const auto next = mNavigation.destination(event);
        if (!next)
        {
            return true;
        } // Back at Home and repeated opens are harmless.
        bool changed{};
        if (event == Event::back)
        {
            changed = mApplication.popScene();
        }
        else if (*next == Page::settings)
        {
            changed = mApplication.pushScene(*mSettings.controller());
        }
        else if (*next == Page::storage)
        {
            changed = mApplication.pushScene(*mStorage.controller());
        }
        else if (*next == Page::volume)
        {
            changed = mApplication.pushScene(*mVolume.controller());
        }
        else if (*next == Page::load)
        {
            changed = mApplication.pushScene(*mLoad.controller());
        }
        if (!changed)
        {
            return false;
        } // Commit only after the scene change succeeds.
        mNavigation.commit(*next);
        return true;
    }
    [[nodiscard]] bool render() noexcept
    {
        return mInitialized && mApplication.render();
    }
    [[nodiscard]] Page page() const noexcept
    {
        return mNavigation.page();
    }
    [[nodiscard]] std::int32_t volume() const noexcept
    {
        return mVolumeState.value();
    }
    [[nodiscard]] PlaybackState playbackState() const noexcept
    {
        return mHome.playbackState();
    }
    [[nodiscard]] EntryId selectedEntry() const noexcept
    {
        return mLoad.selectedEntry();
    }
    [[nodiscard]] std::optional<EntryId> loadedEntry() const noexcept
    {
        return mLoad.loadedEntry();
    }
    [[nodiscard]] std::size_t depth() const noexcept
    {
        return mApplication.controllerDepth();
    }

private:
    [[nodiscard]] bool applyRequests() noexcept
    {
        if (mRequests.overflowed())
        {
            return false;
        }
        const auto event = mRequests.take();
        return !event || navigate(*event);
    }
    lithe::generated::assets::HomeMenu::SelectedResources mHomeResources;
    Requests mRequests;
    Navigation mNavigation;
    HomeMenuForm mHome{mRequests};
    lithe::generated::assets::SettingsMenu::SelectedResources mSettingsResources;
    SettingsMenuForm mSettings{mRequests};
    lithe::generated::assets::StorageMenu::SelectedResources mStorageResources;
    StorageMenuForm mStorage{mRequests};
    lithe::generated::assets::VolumeMenu::SelectedResources mVolumeResources;
    Volume mVolumeState;
    VolumeMenuForm mVolume{mRequests, mVolumeState};
    lithe::generated::assets::LoadMenu::SelectedResources mLoadResources;
    LoadMenuForm mLoad{mRequests};
    // Destroy Application first: it borrows the persistent controllers above.
    lithe::Application mApplication;
    bool mInitialized{};
};
} // namespace menu
