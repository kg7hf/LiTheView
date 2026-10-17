// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Devices/geometry.hpp"
#include "Tasks/UI/Generated/Home/display.hpp"
namespace starter
{
static_assert(lithe::generated::assets::HomeMenuMainForm::DisplayConfiguration::kDefinition.shape ==
              lithe::ViewportShape::circle);
static_assert(lithe::generated::assets::HomeMenuMainForm::DisplayConfiguration::kDefinition.logicalSize.width == width);
static_assert(lithe::generated::assets::HomeMenuMainForm::DisplayConfiguration::kDefinition.logicalSize.height == height);
static_assert(lithe::generated::assets::HomeMenuMainForm::DisplayConfiguration::kFormat == lithe::PixelFormat::rgb888);
} // namespace starter
