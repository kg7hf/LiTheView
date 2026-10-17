// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "display.hpp"
#include "expected.hpp"
#include "form.hpp"
#include "lithe/lithe.hpp"
#include "resources.hpp"

#include <algorithm>
#include <array>
#include <cstdio>
#include <span>

namespace
{

using Configuration = lithe::generated::DisplayConfiguration;
using Form = lithe::generated::DisplayConformance;
constexpr auto kDefinition = Configuration::kDefinition;
constexpr auto kExpected = fixture::kExpected;
static_assert(lithe::validateDisplayDefinition(kExpected));
static_assert(kDefinition.id == kExpected.id && kDefinition.format == kExpected.format &&
              kDefinition.addressSpace == kExpected.addressSpace &&
              kDefinition.shape == kExpected.shape &&
              kDefinition.logicalSize.width == kExpected.logicalSize.width &&
              kDefinition.logicalSize.height == kExpected.logicalSize.height &&
              kDefinition.safeArea.x == kExpected.safeArea.x &&
              kDefinition.safeArea.y == kExpected.safeArea.y &&
              kDefinition.safeArea.width == kExpected.safeArea.width &&
              kDefinition.safeArea.height == kExpected.safeArea.height);
constexpr auto kBlock = lithe::storageBlock(kExpected.format);
constexpr std::size_t kStride =
    lithe::detail::nonnegativeSize(kExpected.logicalSize.width / kBlock.width) * kBlock.bytes;
constexpr auto kLayout = lithe::storageLayout(kExpected.format, kExpected.logicalSize, kStride);
static_assert(kLayout);

constexpr std::array<lithe::ViewSpec, 4U> handwrittenViews() noexcept
{
    std::array<lithe::ViewSpec, 4U> result{};
    result[0].frame = {0, 0, kExpected.logicalSize.width, kExpected.logicalSize.height};
    result[0].style.background = lithe::Color::rgb(0x000000U);
    result[1].kind = lithe::ViewKind::image;
    result[1].parent = 0U;
    result[1].frame = {48, 48, 96, 72};
    result[1].resource = "gallop-icon";
    result[2].kind = lithe::ViewKind::button;
    result[2].parent = 0U;
    result[2].id = 10U;
    result[2].frame = {48, 136, 56, 28};
    result[2].text = "PREV";
    result[2].style.background = lithe::Color::rgb(0xff0000U);
    result[2].style.foreground = lithe::Color::rgb(0xffffffU);
    result[2].style.accent = lithe::Color::rgb(0xffff00U);
    result[3] = result[2];
    result[3].id = 11U;
    result[3].frame.x = 112;
    result[3].text = "NEXT";
    return result;
}

class HandwrittenResources final : public lithe::ResourceProvider
{
public:
    const lithe::Bitmap* bitmap(std::string_view name) const noexcept override
    {
        return name == "gallop-icon" ? &fixture::expectedBitmap() : nullptr;
    }
};

class FrameDisplay final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return {pixels,
                kExpected.format,
                kExpected.logicalSize.width,
                kExpected.logicalSize.height,
                kStride};
    }
    void present([[maybe_unused]] std::span<const lithe::Rect> regions) noexcept override
    {
    }
    std::array<std::byte, kLayout->usedBytes> pixels{};
};

int fail(const char* message) noexcept
{
    std::puts(message);
    return 1;
}

} // namespace

int main(int argc, char* argv[])
{
    if (argc != 2)
    {
        return fail("Usage: litheview_display_definition_consumer output.bin");
    }
    const std::span arguments{argv, lithe::detail::nonnegativeSize(argc)};
    lithe::generated::SelectedResources generatedResources;
    HandwrittenResources handwrittenResources;
    if (generatedResources.bitmap("gallop-icon") != &fixture::expectedBitmap() ||
        generatedResources.bitmap("missing") != nullptr)
    {
        return fail("Generated resource selection differs from the explicit application binding.");
    }
    lithe::StaticLayout<4U> generated;
    lithe::StaticLayout<4U> handwritten;
    constexpr auto kViews = handwrittenViews();
    constexpr lithe::SurfaceInputRequirements kOperations{true, true, false};
    if (!lithe::buildSurfaceLayout(generated,
                                   kDefinition,
                                   Form::views(),
                                   Configuration::kInput,
                                   kOperations,
                                   {},
                                   lithe::font5x7(),
                                   &generatedResources) ||
        !lithe::buildSurfaceLayout(handwritten,
                                   kExpected,
                                   kViews,
                                   Configuration::kInput,
                                   kOperations,
                                   {},
                                   lithe::font5x7(),
                                   &handwrittenResources))
    {
        return fail("Generated or handwritten layout could not be constructed.");
    }
    lithe::ViewController generatedController{*generated.root()};
    lithe::ViewController handwrittenController{*handwritten.root()};
    lithe::Application generatedApplication{Configuration::kViewport};
    lithe::Application handwrittenApplication{*lithe::displayViewport(kExpected)};
    static FrameDisplay generatedFrame;
    static FrameDisplay handwrittenFrame;
    if (!generatedApplication.pushScene(generatedController) ||
        !handwrittenApplication.pushScene(handwrittenController) ||
        !generatedApplication.render(generatedFrame) ||
        !handwrittenApplication.render(handwrittenFrame) ||
        generatedFrame.pixels != handwrittenFrame.pixels)
    {
        return fail("Initial generated and handwritten native frames differ.");
    }
    // Replay logical actions identically. No device or target configuration participates.
    constexpr std::array sequence{lithe::Key::down, lithe::Key::up, lithe::Key::select};
    for (lithe::Key key : sequence)
    {
        if (generatedApplication.handleKey(key) != handwrittenApplication.handleKey(key))
        {
            return fail("Generated and handwritten logical input handling differs.");
        }
        const bool generatedRendered = generatedApplication.render(generatedFrame);
        const bool handwrittenRendered = handwrittenApplication.render(handwrittenFrame);
        if (generatedRendered != handwrittenRendered ||
            generatedFrame.pixels != handwrittenFrame.pixels)
        {
            return fail("Logical input replay produced different native bytes.");
        }
    }
    std::FILE* output = std::fopen(arguments[1], "wb");
    if (output == nullptr)
    {
        return fail("Could not open native output.");
    }
    bool written = true;
    for (std::byte byte : generatedFrame.pixels)
    {
        if (std::fputc(std::to_integer<int>(byte), output) == EOF)
        {
            written = false;
            break;
        }
    }
    if (std::fclose(output) != 0 || !written)
    {
        return fail("Could not write complete native output.");
    }
    std::printf("PASS: %zu native bytes; surface compatible; target unevaluated.\n",
                generatedFrame.pixels.size());
    return 0;
}
