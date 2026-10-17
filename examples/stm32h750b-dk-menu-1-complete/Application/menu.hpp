// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Application/navigation.hpp"
#include "Tasks/Settings/form_handlers.hpp"
#include "Tasks/Storage/form_handlers.hpp"
#include "Tasks/UI/form_handlers.hpp"
#if MENU_HAS_VOLUME
#include "Tasks/Volume/form_handlers.hpp"
#endif
#if MENU_HAS_LOAD
#include "Tasks/Load/form_handlers.hpp"
#endif
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
    Menu(const Menu&) = delete;
    Menu& operator=(const Menu&) = delete;
    Menu(Menu&&) = delete;
    Menu& operator=(Menu&&) = delete;
    [[nodiscard]] bool initialize(const lithe::ResourceProvider* resources = nullptr) noexcept
    {
        if (mInitialized || !mHome.build(lithe::font5x7(), resources) ||
            !mSettings.build(lithe::font5x7(), resources) ||
            !mStorage.build(lithe::font5x7(), resources) || mStorage.controller() == nullptr ||
            mHome.controller() == nullptr || mSettings.controller() == nullptr)
        {
            return false;
        }
#if MENU_HAS_VOLUME
        if (!mVolume.build(lithe::font5x7(), resources) || mVolume.controller() == nullptr)
        {
            return false;
        }
        mVolume.refresh();
#endif
#if MENU_HAS_LOAD
        if (!mLoad.build(lithe::font5x7(), resources) || mLoad.controller() == nullptr ||
            !mLoad.connect())
        {
            return false;
        }
#endif
#if MENU_HAS_PLAYBACK
        mHome.refreshPlayback();
#endif
        mInitialized = mApplication.pushScene(*mHome.controller());
        return mInitialized;
    }
    [[nodiscard]] bool key(lithe::Key key) noexcept
    {
        if (!mInitialized)
        {
            return false;
        }
#if MENU_HAS_VOLUME
        // On Volume only, horizontal directions adjust; vertical directions keep
        // the normal focus traversal and center activates the focused control.
        if (page() == Page::volume && (key == lithe::Key::left || key == lithe::Key::right))
        {
            mVolume.adjust(key == lithe::Key::right ? VolumeChange::increase
                                                    : VolumeChange::decrease);
            return true;
        }
#endif
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
#if MENU_HAS_VOLUME
        else if (*next == Page::volume)
        {
            changed = mApplication.pushScene(*mVolume.controller());
        }
#endif
#if MENU_HAS_LOAD
        else if (*next == Page::load)
        {
            changed = mApplication.pushScene(*mLoad.controller());
        }
#endif
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
#if MENU_HAS_VOLUME
    [[nodiscard]] std::int32_t volume() const noexcept
    {
        return mVolumeState.value();
    }
#endif
#if MENU_HAS_PLAYBACK
    [[nodiscard]] PlaybackState playbackState() const noexcept
    {
        return mHome.playbackState();
    }
#endif
#if MENU_HAS_LOAD
    [[nodiscard]] EntryId selectedEntry() const noexcept
    {
        return mLoad.selectedEntry();
    }
    [[nodiscard]] std::optional<EntryId> loadedEntry() const noexcept
    {
        return mLoad.loadedEntry();
    }
#endif
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
    Requests mRequests;
    Navigation mNavigation;
    HomeMenuForm mHome{mRequests};
    SettingsMenuForm mSettings{mRequests};
    StorageMenuForm mStorage{mRequests};
#if MENU_HAS_VOLUME
    Volume mVolumeState;
    VolumeMenuForm mVolume{mRequests, mVolumeState};
#endif
#if MENU_HAS_LOAD
    LoadMenuForm mLoad{mRequests};
#endif
    // Destroy Application first: it borrows the persistent controllers above.
    lithe::Application mApplication;
    bool mInitialized{};
};
} // namespace menu
