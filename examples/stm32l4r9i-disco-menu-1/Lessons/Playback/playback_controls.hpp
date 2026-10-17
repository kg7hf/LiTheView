// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Controls/transport_button.hpp"
#include "menu_form_types.hpp"
namespace menu
{
using HomePlaybackBase = menu::forms::Home::Instance<
    lithe::WidgetOverrides<menu::forms::Home::PlayButtonType<controls::PlayPauseButton>,
                           menu::forms::Home::StopButtonType<controls::StopButton>>>;
}
