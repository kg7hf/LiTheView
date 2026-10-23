// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/application.hpp"
#include "lithe/telemetry_views.hpp"
#include "Tasks/UI/Generated/Sdr/form.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <span>
#include <string_view>

namespace sdr
{

// A display exercise, not a receiver: all samples are deterministic synthetic
// data. DSP, tuning hardware and transport stay outside this UI object.
class Screen final : public lithe::generated::SDRSpectrum::Instance<>
{
public:
    static constexpr std::int32_t kWidth{480};
    static constexpr std::int32_t kHeight{272};
    static constexpr std::int32_t kBins{228};
    static constexpr std::int32_t kHistoryRows{64};

    explicit Screen(lithe::Display& display) noexcept : mApplication{display} {}

    [[nodiscard]] bool initialize() noexcept
    {
        if (!build(lithe::generatedSans()))
            return false;
        auto* waterfall = getSpectrumWaterfall();
        auto* plot = getSpectrumPlot();
        auto* axis = getFrequencyAxis();
        auto* overlay = getSpectrumOverlay();
        auto* meter = getSignalMeter();
        if (waterfall == nullptr || plot == nullptr ||
            axis == nullptr || overlay == nullptr || meter == nullptr ||
            getFrequencyLabel() == nullptr || controller() == nullptr ||
            !waterfall->bindStorage(mHistory, kBins, kHistoryRows, mPalette) ||
            !plot->setGrid(8U, 4U, lithe::Color::rgb(0x203c50U)) ||
            !plot->setSecondarySamples(mPeakTrace, lithe::Color::rgb(0xf4d16eU)) ||
            !overlay->setRanges(kAnalysisRange) ||
            !overlay->setMarkers(mMarkerBuffers[mPublishedMarkers]) ||
            !meter->setScale(-120, 0, kMeterTicks) ||
            !meter->setZones(kMeterZones) ||
            !axis->setTicks(mAxisTicks) ||
            !mApplication.pushScene(*controller()))
            return false;
        updateScale();
        return tick() && mApplication.render();
    }

    [[nodiscard]] bool tick() noexcept
    {
        if (mHeld)
            return true;
        mPhase = (mPhase + 1U) % 256U;
        auto& nextTrace = mTraceBuffers[1U - mPublishedTrace];
        std::int32_t level{0};
        const std::int32_t moving = 70 + std::int32_t(mPhase % 33U);
        const std::int32_t center = mWide ? 164 : 145;
        for (std::int32_t index = 0; index < kBins; ++index)
        {
            const std::int32_t first = std::max<std::int32_t>(0, 118 - 9 * std::abs(index - moving));
            const std::int32_t second = std::max<std::int32_t>(0, 166 - 13 * std::abs(index - center));
            const std::int32_t noise = (index * 37 + std::int32_t(mPhase) * 11) % 23;
            const std::int32_t amplitude = std::min<std::int32_t>(255, 30 + noise + first + second);
            nextTrace[std::size_t(index)] = std::uint16_t(amplitude);
            const auto previousPeak = mPeakTrace[std::size_t(index)];
            mPeakTrace[std::size_t(index)] =
                std::max<std::uint16_t>(nextTrace[std::size_t(index)],
                                        previousPeak == 0U ? 0U : std::uint16_t(previousPeak - 1U));
            mLine[std::size_t(index)] = std::uint8_t(amplitude);
            level = std::max(level, amplitude);
        }
        // Illustrative application calibration for synthetic input, not RF dBm.
        getSignalMeter()->setReading(-120 + level * 120 / 255);
        const std::size_t nextMarkers = 1U - mPublishedMarkers;
        auto& markers = mMarkerBuffers[nextMarkers];
        markers[0U].position = std::uint16_t(moving * 65535 / (kBins - 1));
        if (!getSpectrumWaterfall()->pushRow(mLine) ||
            !getSpectrumPlot()->setSamples(nextTrace) ||
            !getSpectrumOverlay()->setMarkers(markers))
            return false;
        mPublishedTrace = 1U - mPublishedTrace;
        mPublishedMarkers = nextMarkers;
        return true;
    }

    [[nodiscard]] bool key(lithe::Key key) noexcept { (void)mApplication.handleKey(key); return true; }
    [[nodiscard]] bool pointer(lithe::PointerEvent event) noexcept
    {
        (void)mApplication.handlePointer(event);
        return true;
    }
    void cancelPointer() noexcept
    {
        (void)controller()->cancelPointerCapture();
    }
    [[nodiscard]] bool render() noexcept { return mApplication.render(); }
    std::uint32_t frequencyHz() const noexcept { return mFrequencyHz; }
    bool held() const noexcept { return mHeld; }
    std::int32_t historyRows() noexcept { return getSpectrumWaterfall()->rowCount(); }
    std::optional<std::uint8_t> historySample(std::int32_t age,
                                               std::int32_t column) noexcept
    {
        return getSpectrumWaterfall()->sampleAt(age, column);
    }

private:
    static constexpr std::array<lithe::Color, lithe::WaterfallView::kPaletteSize>
    coldPalette() noexcept
    {
        return {lithe::Color::rgb(0x020a20U), lithe::Color::rgb(0x061141U),
                lithe::Color::rgb(0x071d65U), lithe::Color::rgb(0x0b2d8eU),
                lithe::Color::rgb(0x0c41b7U), lithe::Color::rgb(0x0757dcU),
                lithe::Color::rgb(0x006ff0U), lithe::Color::rgb(0x008eefU),
                lithe::Color::rgb(0x00b1deU), lithe::Color::rgb(0x00cbb1U),
                lithe::Color::rgb(0x30dc71U), lithe::Color::rgb(0x82e449U),
                lithe::Color::rgb(0xd5e545U), lithe::Color::rgb(0xffc542U),
                lithe::Color::rgb(0xff8640U), lithe::Color::rgb(0xffffffU)};
    }
    static constexpr std::array<lithe::Color, lithe::WaterfallView::kPaletteSize>
    warmPalette() noexcept
    {
        return {lithe::Color::rgb(0x160809U), lithe::Color::rgb(0x270b12U),
                lithe::Color::rgb(0x3e0d1bU), lithe::Color::rgb(0x551025U),
                lithe::Color::rgb(0x75152dU), lithe::Color::rgb(0x982036U),
                lithe::Color::rgb(0xb92b39U), lithe::Color::rgb(0xd33c3aU),
                lithe::Color::rgb(0xe75435U), lithe::Color::rgb(0xf47735U),
                lithe::Color::rgb(0xf89836U), lithe::Color::rgb(0xfbb340U),
                lithe::Color::rgb(0xfdcb5aU), lithe::Color::rgb(0xffdf82U),
                lithe::Color::rgb(0xffeeb4U), lithe::Color::rgb(0xffffffU)};
    }
    void updateFrequency() noexcept
    {
        std::uint32_t digits = mFrequencyHz;
        constexpr std::array<std::size_t, 8U> positions{9U, 8U, 7U, 5U, 4U, 3U, 1U, 0U};
        for (const std::size_t position : positions)
        {
            mFrequencyText[position] = char('0' + digits % 10U);
            digits /= 10U;
        }
        mFrequencyText[2U] = '.';
        mFrequencyText[6U] = '.';
        getFrequencyLabel()->setText({mFrequencyText.data(), 10U});
    }
    void updateScale() noexcept
    {
        const std::uint32_t offset = mWide ? 25'000U : 5'000U;
        const std::array<std::uint32_t, 3U> values{
            mFrequencyHz - offset, mFrequencyHz, mFrequencyHz + offset};
        for (std::size_t index = 0U; index < values.size(); ++index)
        {
            const std::uint32_t megahertz = values[index] / 1'000'000U;
            const std::uint32_t kilohertz = (values[index] / 1'000U) % 1'000U;
            auto& label = mAxisLabels[index];
            label[0U] = char('0' + megahertz / 10U);
            label[1U] = char('0' + megahertz % 10U);
            label[2U] = '.';
            label[3U] = char('0' + kilohertz / 100U);
            label[4U] = char('0' + (kilohertz / 10U) % 10U);
            label[5U] = char('0' + kilohertz % 10U);
        }
        getFrequencyAxis()->invalidate();
    }
    void onTuneDownButtonClicked() noexcept override
    {
        constexpr std::uint32_t minimumHz{1'000'000U};
        if (mFrequencyHz > minimumHz)
        {
            mFrequencyHz -= 1000U;
            updateFrequency();
            updateScale();
        }
    }
    void onTuneUpButtonClicked() noexcept override
    {
        constexpr std::uint32_t maximumHz{30'000'000U};
        if (mFrequencyHz < maximumHz)
        {
            mFrequencyHz += 1000U;
            updateFrequency();
            updateScale();
        }
    }
    void onModeButtonClicked() noexcept override
    {
        mModeIndex = (mModeIndex + 1U) % kModes.size();
        getModeButton()->setText(kModes[mModeIndex]);
    }
    void onHoldButtonClicked() noexcept override
    {
        mHeld = !mHeld;
        getHoldButton()->setText(mHeld ? "RUN" : "HOLD");
    }
    void onSpanButtonClicked() noexcept override
    {
        mWide = !mWide;
        getSpanButton()->setText(mWide ? "WIDE" : "NARROW");
        updateScale();
    }
    void onPaletteButtonClicked() noexcept override
    {
        mWarm = !mWarm;
        (void)getSpectrumWaterfall()->setPalette(mWarm ? mWarmPalette : mPalette);
    }

    static constexpr std::array<std::string_view, 3U> kModes{"USB", "LSB", "AM"};
    static constexpr std::array<lithe::OverlayRange, 1U> kAnalysisRange{{
        {30000U, 35500U, lithe::Color::rgba(0x236dbb55U)}}};
    static constexpr std::array<lithe::OverlayMarker, 2U> kInitialMarkers{{
        {0U, lithe::Color::rgb(0xff7842U), {}},
        {32768U, lithe::Color::rgb(0xffc542U), "VFO"}}};
    static constexpr std::array<lithe::AxisTick, 3U> kMeterTicks{{
        {0U, "-120"}, {32768U, "-60"}, {65535U, "0"}}};
    static constexpr std::array<lithe::MeterZone, 3U> kMeterZones{{
        {0U, 32768U, lithe::Color::rgb(0x30dc71U)},
        {32768U, 49151U, lithe::Color::rgb(0xffc542U)},
        {49151U, 65535U, lithe::Color::rgb(0xff6040U)}}};
    std::array<std::uint8_t,
               lithe::WaterfallView::requiredBytes(kBins, kHistoryRows)> mHistory{};
    std::array<lithe::Color, lithe::WaterfallView::kPaletteSize> mPalette{coldPalette()};
    std::array<lithe::Color, lithe::WaterfallView::kPaletteSize> mWarmPalette{warmPalette()};
    lithe::Application mApplication;
    std::array<std::array<std::uint16_t, kBins>, 2U> mTraceBuffers{};
    // This policy belongs to the example, not PlotView: a held peak decays by
    // one sample unit per update while the generic view only draws two spans.
    std::array<std::uint16_t, kBins> mPeakTrace{};
    std::size_t mPublishedTrace{0U};
    std::array<std::array<lithe::OverlayMarker, 2U>, 2U> mMarkerBuffers{
        kInitialMarkers, kInitialMarkers};
    std::size_t mPublishedMarkers{0U};
    std::array<std::uint8_t, kBins> mLine{};
    std::array<char, 12U> mFrequencyText{};
    std::array<std::array<char, 6U>, 3U> mAxisLabels{};
    std::array<lithe::AxisTick, 3U> mAxisTicks{{
        {0U, {mAxisLabels[0U].data(), 6U}},
        {32768U, {mAxisLabels[1U].data(), 6U}},
        {65535U, {mAxisLabels[2U].data(), 6U}}}};
    std::uint32_t mFrequencyHz{14031800U};
    std::uint32_t mPhase{0U};
    std::size_t mModeIndex{0U};
    bool mHeld{false};
    bool mWide{true};
    bool mWarm{false};
};

} // namespace sdr
