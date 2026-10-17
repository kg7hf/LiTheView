// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include "Tasks/UI/Generated/Home/product_config.hpp"
#include "lithe/application.hpp"
#include "lithe/tiled_renderer.hpp"
namespace menu
{
// First-display lesson: show whichever form the designer generated. Navigation
// becomes part of this same application when its handwritten handlers are
// added.
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
    ~Menu() = default;
    Menu(const Menu&) = delete;
    Menu& operator=(const Menu&) = delete;
    Menu(Menu&&) = delete;
    Menu& operator=(Menu&&) = delete;
    [[nodiscard]] bool initialize() noexcept
    {
        if (mInitialized || !mForm.build(lithe::font5x7(), &mResources) ||
            mForm.controller() == nullptr)
        {
            return false;
        }
        mInitialized = mApplication.pushScene(*mForm.controller());
        return mInitialized;
    }
    [[nodiscard]] bool key(lithe::Key key) noexcept
    {
        if (!mInitialized)
        {
            return false;
        }
        (void)mApplication.handleKey(key);
        return true;
    }
    [[nodiscard]] bool pointer(const lithe::PointerEvent& event) noexcept
    {
        if (!mInitialized)
        {
            return false;
        }
        (void)mApplication.handlePointer(event);
        return true;
    }
    void cancelPointer() noexcept
    {
        if (auto* controller = mApplication.activeController(); controller != nullptr)
        {
            (void)controller->cancelPointerCapture();
        }
    }
    [[nodiscard]] bool render() noexcept
    {
        return mInitialized && mApplication.render();
    }
    [[nodiscard]] bool
    renderTiled(lithe::Size size, lithe::Surface workspace, lithe::TileSink& sink) noexcept
    {
        return mInitialized &&
               lithe::TiledRenderer::render(mApplication, size, workspace, sink).ok();
    }

private:
    lithe::generated::assets::HomeMenuMainForm::SelectedResources mResources;
    lithe::simulator::HomeMenuMainForm::Form::Instance<> mForm;
    lithe::Application mApplication; // Borrows mForm; destroyed before it.
    bool mInitialized{};
};
} // namespace menu
