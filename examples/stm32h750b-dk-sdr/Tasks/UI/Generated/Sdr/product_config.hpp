// Portable UI aliases; the consuming application owns behavior and devices.
#pragma once
#include "display.hpp"
#include "form.hpp"
#include "resources.hpp"
#include "lithe/surface_layout.hpp"
#include <array>

namespace lithe::simulator::SDRSpectrum
{
using Configuration = lithe::generated::assets::SDRSpectrum::DisplayConfiguration;
using Form = lithe::generated::SDRSpectrum;
using SelectedResources = lithe::generated::assets::SDRSpectrum::SelectedResources;
inline constexpr lithe::SurfaceInputRequirements kInputRequirements{
    false, false, false};
inline constexpr std::array<lithe::SurfacePlacementPolicy, 0U> kPlacement{};
} // namespace lithe::simulator::SDRSpectrum
