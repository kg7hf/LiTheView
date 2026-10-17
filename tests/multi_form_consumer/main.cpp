// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "product_config.hpp"
#include "Settings/product_config.hpp"
#include "Foo/product_config.hpp"
#include "FooAssets/product_config.hpp"
int main()
{
    lithe::generated::SelectedResources homeResources;
    lithe::generated::assets::Settings::SelectedResources settingsResources;
    lithe::generated::assets::Foo::SelectedResources emptyResources;
    lithe::generated::HomeMainForm::Instance<> home;
    lithe::generated::Settings::Instance<> settings;
    lithe::generated::Settings::Instance<> wrongProvider;
    lithe::generated::Foo::Instance<> foo;
    lithe::generated::FooAssets::Instance<> fooAssets;
    return home.build(lithe::font5x7(), &homeResources).ok() &&
                   settings.build(lithe::font5x7(), &settingsResources).ok() &&
                   wrongProvider.build(lithe::font5x7(), &emptyResources).error ==
                       lithe::LayoutError::missingResource &&
                   foo.build().ok() && fooAssets.build().ok()
               ? 0 : 1;
}
