// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/tiled_renderer.hpp"
#include "runtime_types.hpp"

#include <array>
#include <concepts>
#include <cstdint>
#include <optional>
#include <span>

namespace lithe::simulator
{

// Own only the mutable text declared by the form. Immutable fonts and images
// remain borrowed from the selected generated resource provider.
class Resources final : public ResourceProvider
{
public:
    const Bitmap* bitmap(std::string_view name) const noexcept override
    {
        return mSelected.bitmap(name);
    }
    const Font* font(std::string_view name) const noexcept override
    {
        return mSelected.font(name);
    }
    MutableTextBuffer textBuffer(std::string_view name) const noexcept override
    {
        std::size_t offset = 0U;
        for (const auto& binding : Form::kBindings)
        {
            if (binding.name == name)
            {
                return std::span{mText}.subspan(offset, binding.capacityBytes);
            }
            offset += binding.capacityBytes;
        }
        return {};
    }

private:
    SelectedResources mSelected{};
    mutable std::array<char, kTextBytes> mText{};
};

template <typename Hooks>
concept ProductHooks = requires(Hooks& hooks, Views& views, ViewId action) {
    { hooks.onReady(views) } noexcept -> std::same_as<void>;
    { hooks.onAction(views, action) } noexcept -> std::same_as<void>;
    { hooks.onBack(views) } noexcept -> std::same_as<void>;
};

// This owner has no framebuffer, window, target SDK, timer, or dynamic storage.
// The application-owned hook is borrowed and must outlive this component.
template <ProductHooks Hooks> class Product final : public ControllerDelegate
{
public:
    explicit Product(Hooks& hooks) noexcept : mHooks(hooks)
    {
    }

    bool initialize() noexcept
    {
        if (mReady)
        {
            return true;
        }
        if (!buildSurfaceLayout(mViews,
                                kDisplay,
                                Form::views(),
                                kInput,
                                kInputRequirements,
                                kPlacement,
                                font5x7(),
                                &mResources,
                                Form::animationFrames()) ||
            mViews.root() == nullptr)
        {
            return false;
        }
        mHooks.onReady(mViews);
        mController.emplace(*mViews.root(), *this);
        mReady = mApplication.pushScene(*mController);
        return mReady;
    }

    bool handleKey(Key key) noexcept
    {
        const bool available = kInput.keys ||
                               (kInput.encoder && (key == Key::up || key == Key::down)) ||
                               (kInput.select && key == Key::select);
        return mReady && available && mApplication.handleKey(key);
    }
    bool handlePointer(PointerEvent event) noexcept
    {
        return mReady && kInput.pointer && mApplication.handlePointer(event);
    }
    bool handleTextInput(char32_t codepoint) noexcept
    {
        return mReady && kInput.text && mApplication.handleTextInput(codepoint);
    }
    bool advanceTime(std::uint32_t elapsedMs) noexcept
    {
        return mReady && mApplication.advanceTime(elapsedMs);
    }
    [[nodiscard]] bool animationsRunning() const noexcept
    {
        return mReady && mApplication.animationsRunning();
    }
    bool render(Display& display) noexcept
    {
        const auto target = display.surface();
        if (!mReady || target.format != kDisplay.format || target.width != kWidth ||
            target.height != kHeight || target.strideBytes != kStrideBytes ||
            target.origin.x != 0 || target.origin.y != 0 || !target.valid())
        {
            return false;
        }
        // The reference simulator redraws against opaque black each time,
        // including transparent roots and pixels outside a circular viewport.
        for (std::size_t offset = 0U; offset < kFrameBytes; offset += kPixelBytes)
        {
            PixelFormatTraits<kDisplay.format>::write(target.data, offset, Color::rgb(0U));
        }
        mApplication.invalidate();
        return mApplication.render(display);
    }
    [[nodiscard]] TileRenderResult renderTiled(Surface workspace, TileSink& sink) noexcept
    {
        return TiledRenderer::render(mApplication, kDisplay.logicalSize, workspace, sink);
    }
    [[nodiscard]] std::optional<Point> focusedPoint() noexcept
    {
        if (!mReady)
        {
            return std::nullopt;
        }
        if (const auto* view = mController->focusedView())
        {
            const auto frame = view->frameInRoot();
            return Point{frame.x + frame.width / 2, frame.y + frame.height / 2};
        }
        return std::nullopt;
    }
    [[nodiscard]] ViewId lastAction() const noexcept
    {
        return mLastAction;
    }
    [[nodiscard]] std::size_t actionCount() const noexcept
    {
        return mActionCount;
    }
    Views& views() noexcept
    {
        return mViews;
    }

private:
    void onAction([[maybe_unused]] ViewController& controller, ViewId action) noexcept override
    {
        mLastAction = action;
        ++mActionCount;
        mHooks.onAction(mViews, action);
    }
    void onBack([[maybe_unused]] ViewController& controller) noexcept override
    {
        mHooks.onBack(mViews);
    }

    Resources mResources{};
    Views mViews{};
    Hooks& mHooks;
    std::optional<ViewController> mController{};
    Application mApplication{Configuration::kViewport};
    ViewId mLastAction{0U};
    std::size_t mActionCount{0U};
    bool mReady{false};
};

} // namespace lithe::simulator
