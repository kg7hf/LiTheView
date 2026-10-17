// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Application/load_selection.hpp"
#include "Application/navigation.hpp"
#include "Application/playback.hpp"
#include "lithe/application.hpp"
#include "lithe/tiled_renderer.hpp"
#include "menu_composition.hpp"
namespace menu
{
template <typename Pages> class MenuRuntime final
{
public:
    explicit MenuRuntime(lithe::Display& display, lithe::Viewport viewport = {}) noexcept
        : mApplication{display, viewport}
    {
    }
    explicit MenuRuntime(lithe::Viewport viewport) noexcept : mApplication{viewport}
    {
    }
    [[nodiscard]] bool
    renderTiled(lithe::Size size, lithe::Surface workspace, lithe::TileSink& sink) noexcept
    {
        return mInitialized &&
               lithe::TiledRenderer::render(mApplication, size, workspace, sink).ok();
    }
    ~MenuRuntime() = default;
    MenuRuntime(const MenuRuntime&) = delete;
    MenuRuntime& operator=(const MenuRuntime&) = delete;
    MenuRuntime(MenuRuntime&&) = delete;
    MenuRuntime& operator=(MenuRuntime&&) = delete;
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
        if constexpr (Pages::hasVolume)
        {
            if (!mVolume.build(lithe::font5x7(), &mVolumeResources) ||
                mVolume.controller() == nullptr)
            {
                return false;
            }
            mVolume.refresh();
        }
        if constexpr (Pages::hasLoad)
        {
            if (!mLoad.build(lithe::font5x7(), &mLoadResources) || mLoad.controller() == nullptr ||
                !mLoad.connect())
            {
                return false;
            }
        }
        if constexpr (Pages::hasPlayback)
        {
            mHome.refreshPlayback();
        }
        mInitialized = mApplication.pushScene(*mHome.controller());
        return mInitialized;
    }
    [[nodiscard]] bool key(lithe::Key key) noexcept
    {
        if (!mInitialized)
        {
            return false;
        }
        if constexpr (Pages::hasVolume)
        {
            // On Volume only, horizontal directions adjust; vertical directions keep
            // the normal focus traversal and center activates the focused control.
            if (page() == Page::volume && (key == lithe::Key::left || key == lithe::Key::right))
            {
                mVolume.adjust(key == lithe::Key::right ? VolumeChange::increase
                                                        : VolumeChange::decrease);
                return true;
            }
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
        else
        {
            if constexpr (Pages::hasVolume)
            {
                if (*next == Page::volume)
                {
                    changed = mApplication.pushScene(*mVolume.controller());
                }
            }
            if constexpr (Pages::hasLoad)
            {
                if (*next == Page::load)
                {
                    changed = mApplication.pushScene(*mLoad.controller());
                }
            }
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
        requires(Pages::hasVolume)
    {
        return mVolumeState.value();
    }

    [[nodiscard]] PlaybackState playbackState() const noexcept
        requires(Pages::hasPlayback)
    {
        return mHome.playbackState();
    }

    [[nodiscard]] EntryId selectedEntry() const noexcept
        requires(Pages::hasLoad)
    {
        return mLoad.selectedEntry();
    }
    [[nodiscard]] std::optional<EntryId> loadedEntry() const noexcept
        requires(Pages::hasLoad)
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
    typename Pages::HomeResources mHomeResources;
    Requests mRequests;
    Navigation mNavigation;
    typename Pages::Home mHome{mRequests};
    typename Pages::SettingsResources mSettingsResources;
    typename Pages::Settings mSettings{mRequests};
    typename Pages::StorageResources mStorageResources;
    typename Pages::Storage mStorage{mRequests};
    typename Pages::VolumeResources mVolumeResources;
    Volume mVolumeState;
    typename Pages::Volume mVolume{mRequests, mVolumeState};

    typename Pages::LoadResources mLoadResources;
    typename Pages::Load mLoad{mRequests};

    // Destroy Application first: it borrows the persistent controllers above.
    lithe::Application mApplication;
    bool mInitialized{};
};
using Menu = MenuRuntime<PageComposition>;
} // namespace menu
