// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/controller.hpp"
#include "lithe/layout.hpp"

#include <optional>

namespace lithe
{

// Application-owned form storage and event receiver. Resources/text must outlive
// this object. Register controller() only after build succeeds, and remove the
// scene from Application before destroying the form. UI dispatch and mutations
// belong to one owner; callbacks must not rebuild/destroy the active scene.
template <typename Definition, typename Overrides = WidgetOverrides<>>
class FormInstance : public ControllerDelegate
{
public:
    using Layout = StaticLayout<Definition::kViewCount, typename Definition::Widgets, Overrides>;

    [[nodiscard]] LayoutResult build(const Font& font = font5x7(),
                                     const ResourceProvider* resources = nullptr) noexcept
    {
        if (mController)
        {
            return {LayoutError::alreadyBuilt, 0U};
        }
        const auto result =
            mLayout.build(Definition::views(), font, resources, Definition::animationFrames());
        if (!result)
        {
            return result;
        }
        mController.emplace(*mLayout.root(), *this);
        return {};
    }

    ViewController* controller() noexcept
    {
        return mController ? &*mController : nullptr;
    }

protected:
    template <std::size_t Index, typename Default> auto* component() noexcept
    {
        return mLayout.template component<Index, Default>();
    }

private:
    void onAction(ViewController&, ViewId) noexcept override
    {
    }
    void onBack(ViewController&) noexcept override
    {
    }

    Layout mLayout{};
    // Destroy the controller before the views it borrows.
    std::optional<ViewController> mController{};
};

} // namespace lithe
