// Portable UI aliases; the consuming application owns behavior and devices.
#pragma once
#include "display.hpp"
#include "form.hpp"
#include "resources.hpp"
#include "lithe/surface_layout.hpp"
#include <array>

namespace lithe::simulator::SettingsMenu
{
using Configuration = lithe::generated::assets::SettingsMenu::DisplayConfiguration;
using Form = lithe::generated::SettingsMenu;
using SelectedResources = lithe::generated::assets::SettingsMenu::SelectedResources;
inline constexpr lithe::SurfaceInputRequirements kInputRequirements{
    false, false, false};
inline constexpr std::array<lithe::SurfacePlacementPolicy, 0U> kPlacement{};
} // namespace lithe::simulator::SettingsMenu
