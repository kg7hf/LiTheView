// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Controls/transport_button.hpp"
#include "Generated/form.hpp"
namespace menu
{
using HomePlaybackBase = lithe::generated::HomeMenu::Instance<
    lithe::WidgetOverrides<lithe::generated::HomeMenu::PlayButtonType<controls::PlayPauseButton>,
                           lithe::generated::HomeMenu::StopButtonType<controls::StopButton>>>;
}
