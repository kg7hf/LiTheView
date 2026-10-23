// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "Application/sdr_screen.hpp"

#include <array>
#include <cstdio>
#include <span>

namespace
{
class Display final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return lithe::Surface{lithe::Rgb565Surface{pixels, 480, 272, 480}};
    }
    void present(std::span<const lithe::Rect>) noexcept override { ++frames; }
    std::array<lithe::Rgb565Pixel, 480U * 272U> pixels{};
    unsigned frames{};
};
bool click(sdr::Screen& screen, std::int32_t x, std::int32_t y)
{
    return screen.pointer({{x, y}, lithe::PointerPhase::pressed, 0U}) &&
           screen.pointer({{x, y}, lithe::PointerPhase::released, 0U});
}
} // namespace

int main(int argc, char** argv)
{
    Display display;
    sdr::Screen screen{display};
    if (!screen.initialize() || !screen.render() || display.frames == 0U ||
        screen.historyRows() != 1)
        return 1;
    const auto* plot = screen.getSpectrumPlot();
    const auto* overlay = screen.getSpectrumOverlay();
    const auto* meter = screen.getSignalMeter();
    if (plot == nullptr || plot->secondarySamples().size() != sdr::Screen::kBins ||
        plot->samples().size() != sdr::Screen::kBins)
        return 10;
    if (overlay == nullptr || overlay->ranges().size() != 1U ||
        overlay->markers().size() != 2U || meter == nullptr ||
        meter->minimum() != -120 || meter->maximum() != 0 ||
        meter->reading() < -120 || meter->reading() > 0)
        return 14;
    const auto movingMarker = overlay->markers()[0U].position;
    if (!screen.tick() || overlay->markers()[0U].position <= movingMarker)
        return 15;
    for (std::size_t index = 0U; index < plot->samples().size(); ++index)
        if (plot->secondarySamples()[index] < plot->samples()[index])
            return 11;
    const auto initialFrequency = screen.frequencyHz();
    const auto* axis = screen.getFrequencyAxis();
    if (axis == nullptr || axis->ticks().size() != 3U ||
        axis->ticks()[1U].label != "14.031")
        return 12;
    if (!click(screen, 110, 243) || screen.frequencyHz() != initialFrequency + 1000U)
        return 2;
    if (axis->ticks()[1U].label != "14.032" ||
        !click(screen, 340, 243) || axis->ticks()[0U].label != "14.027" ||
        axis->ticks()[2U].label != "14.037" ||
        !click(screen, 340, 243) || axis->ticks()[0U].label != "14.007")
        return 13;
    if (!click(screen, 262, 243) || !screen.held())
        return 3;
    const auto heldRows = screen.historyRows();
    if (!screen.tick() || screen.historyRows() != heldRows ||
        !click(screen, 262, 243) || screen.held())
        return 4;
    for (int index = 0; index < 70; ++index)
        if (!screen.tick())
            return 5;
    if (screen.historyRows() != sdr::Screen::kHistoryRows ||
        screen.historySample(0, 164).value_or(0U) < 8U || !screen.render())
        return 6;
    if (display.pixels[121U * 480U + 340U] ==
        lithe::Color::rgb(0x020a20U).rgb565())
        return 7;
    if (argc == 2)
    {
        auto* image = std::fopen(argv[1], "wb");
        if (image == nullptr)
            return 8;
        std::fputs("P6\n480 272\n255\n", image);
        for (const auto pixel : display.pixels)
        {
            const auto color = lithe::Color::fromRgb565(pixel);
            std::fputc(color.red, image);
            std::fputc(color.green, image);
            std::fputc(color.blue, image);
        }
        if (std::fclose(image) != 0)
            return 9;
    }
    std::puts("PASS: shared SDR UI controls, synthetic trace and bounded history");
    return 0;
}
