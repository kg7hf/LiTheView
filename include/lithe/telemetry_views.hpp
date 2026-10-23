// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/detail/index.hpp"
#include "lithe/view.hpp"

#include <cstddef>
#include <cstdint>
#include <optional>
#include <span>
#include <string_view>

namespace lithe
{

enum class PlotMode : std::uint8_t
{
    points,
    lines,
    linesAndPoints
};

enum class AxisOrientation : std::uint8_t
{
    horizontal,
    vertical
};

// Positions are normalized across the full axis: 0 is left/bottom, 65535 is
// right/top. Labels and the tick array are borrowed from the application.
struct AxisTick
{
    std::uint16_t position{0U};
    std::string_view label{};
};

class AxisView final : public View
{
public:
    static constexpr std::size_t kMaximumTicks{16U};
    static constexpr std::size_t kMaximumLabelBytes{16U};

    explicit AxisView(Rect frame, const Font& font = font5x7(), Style style = {}) noexcept
        : View(frame, style), mFont(&font) {}

    [[nodiscard]] bool setTicks(std::span<const AxisTick> ticks) noexcept;
    void setOrientation(AxisOrientation orientation) noexcept
    {
        mOrientation = orientation;
        invalidate();
    }
    [[nodiscard]] std::span<const AxisTick> ticks() const noexcept { return mTicks; }
    [[nodiscard]] AxisOrientation orientation() const noexcept { return mOrientation; }

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::span<const AxisTick> mTicks{};
    const Font* mFont{nullptr};
    AxisOrientation mOrientation{AxisOrientation::horizontal};
};

struct OverlayRange
{
    std::uint16_t start{0U};
    std::uint16_t end{0U};
    Color fill{Color::transparent()};
};

struct OverlayMarker
{
    std::uint16_t position{0U};
    Color color{Color::transparent()};
    std::string_view label{};
};

// A transparent, separately placed layer over a plot, waterfall or other view.
// Positions are normalized from left (0) to right (65535); data is borrowed.
class MarkerRangeView final : public View
{
public:
    static constexpr std::size_t kMaximumRanges{8U};
    static constexpr std::size_t kMaximumMarkers{16U};
    static constexpr std::size_t kMaximumLabelBytes{16U};

    explicit MarkerRangeView(Rect frame, const Font& font = font5x7(),
                             Style style = {}) noexcept
        : View(frame, style), mFont(&font) {}

    [[nodiscard]] bool setRanges(std::span<const OverlayRange> ranges) noexcept;
    [[nodiscard]] bool setMarkers(std::span<const OverlayMarker> markers) noexcept;
    [[nodiscard]] std::span<const OverlayRange> ranges() const noexcept { return mRanges; }
    [[nodiscard]] std::span<const OverlayMarker> markers() const noexcept { return mMarkers; }

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::span<const OverlayRange> mRanges{};
    std::span<const OverlayMarker> mMarkers{};
    const Font* mFont{nullptr};
};

struct MeterZone
{
    std::uint16_t start{0U};
    std::uint16_t end{0U};
    Color color{Color::transparent()};
};

// Integer-calibrated linear meter. The reading remains in application units;
// only its drawn length is clamped to the configured scale. No heap is used.
class MeterView final : public View
{
public:
    static constexpr std::size_t kMaximumTicks{8U};
    static constexpr std::size_t kMaximumZones{8U};
    static constexpr std::size_t kMaximumLabelBytes{16U};

    explicit MeterView(Rect frame, const Font& font = font5x7(),
                       Style style = {}) noexcept
        : View(frame, style), mFont(&font) {}

    [[nodiscard]] bool setScale(std::int32_t minimum, std::int32_t maximum,
                                std::span<const AxisTick> ticks = {}) noexcept;
    [[nodiscard]] bool setZones(std::span<const MeterZone> zones) noexcept;
    void setReading(std::int32_t reading) noexcept
    {
        mReading = reading;
        invalidate();
    }
    [[nodiscard]] std::int32_t reading() const noexcept { return mReading; }
    [[nodiscard]] std::int32_t minimum() const noexcept { return mMinimum; }
    [[nodiscard]] std::int32_t maximum() const noexcept { return mMaximum; }
    [[nodiscard]] std::span<const AxisTick> ticks() const noexcept { return mTicks; }
    [[nodiscard]] std::span<const MeterZone> zones() const noexcept { return mZones; }

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::span<const AxisTick> mTicks{};
    std::span<const MeterZone> mZones{};
    const Font* mFont{nullptr};
    std::int32_t mMinimum{0};
    std::int32_t mMaximum{100};
    std::int32_t mReading{0};
};

// A bounded, non-owning trace. The producer keeps samples alive and publishes a
// completed snapshot on the UI thread; this view never reads an ISR-owned buffer.
class PlotView final : public View
{
public:
    static constexpr std::size_t kMaximumSamples{4096U};

    explicit PlotView(Rect frame, Style style = {}) noexcept : View(frame, style) {}

    [[nodiscard]] bool setSamples(std::span<const std::uint16_t> samples) noexcept;
    // An independent, bounded trace on the same range and normalized x axis.
    // Its caller-owned snapshot follows the same lifetime rules as setSamples.
    [[nodiscard]] bool setSecondarySamples(std::span<const std::uint16_t> samples,
                                           Color color) noexcept;
    [[nodiscard]] bool setRange(std::uint16_t minimum, std::uint16_t maximum) noexcept;
    [[nodiscard]] bool setGrid(std::uint8_t verticalDivisions,
                               std::uint8_t horizontalDivisions,
                               Color color) noexcept;
    [[nodiscard]] bool setCursor(std::optional<std::size_t> sampleIndex, Color color) noexcept;
    void setMode(PlotMode mode) noexcept { mMode = mode; invalidate(); }
    std::span<const std::uint16_t> samples() const noexcept { return mSamples; }
    std::span<const std::uint16_t> secondarySamples() const noexcept { return mSecondarySamples; }
    std::uint16_t minimum() const noexcept { return mMinimum; }
    std::uint16_t maximum() const noexcept { return mMaximum; }
    PlotMode mode() const noexcept { return mMode; }

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    void drawTrace(Painter& painter, Rect frame, std::span<const std::uint16_t> samples,
                   Color color) const noexcept;
    std::span<const std::uint16_t> mSamples{};
    std::span<const std::uint16_t> mSecondarySamples{};
    Color mSecondaryColor{Color::transparent()};
    std::uint16_t mMinimum{0U};
    std::uint16_t mMaximum{255U};
    PlotMode mMode{PlotMode::lines};
    std::uint8_t mVerticalDivisions{0U};
    std::uint8_t mHorizontalDivisions{0U};
    Color mGridColor{Color::transparent()};
    // kMaximumSamples is outside every valid sample index and saves the
    // alignment padding of an optional in the fixed StaticLayout storage.
    std::size_t mCursor{kMaximumSamples};
    Color mCursorColor{Color::transparent()};
};

// A caller-owned four-bit intensity history. Two samples occupy one byte, with
// each row padded to a byte boundary. New rows appear at the top of the view.
// The supplied palette and storage must outlive the view. No heap is used.
class WaterfallView final : public View
{
public:
    static constexpr std::size_t kPaletteSize{16U};
    static constexpr std::int32_t kMaximumColumns{1024};
    static constexpr std::int32_t kMaximumRows{512};

    WaterfallView(Rect frame,
                  std::span<std::uint8_t> packedRows,
                  std::int32_t columns,
                  std::int32_t rows,
                  std::span<const Color> palette,
                  Style style = {}) noexcept;
    // Designer-created instances start unbound. The application supplies
    // storage and a palette before publishing rows; neither is allocated here.
    explicit WaterfallView(Rect frame, Style style = {}) noexcept : View(frame, style) {}
    [[nodiscard]] bool bindStorage(std::span<std::uint8_t> packedRows,
                                   std::int32_t columns,
                                   std::int32_t rows,
                                   std::span<const Color> palette) noexcept;

    [[nodiscard]] static constexpr std::size_t requiredBytes(std::int32_t columns,
                                                              std::int32_t rows) noexcept
    {
        if (columns <= 0 || columns > kMaximumColumns || rows <= 0 || rows > kMaximumRows)
            return 0U;
        return (detail::nonnegativeSize(columns) + 1U) / 2U * detail::nonnegativeSize(rows);
    }

    [[nodiscard]] bool validStorage() const noexcept { return mValid; }
    [[nodiscard]] bool pushRow(std::span<const std::uint8_t> intensities) noexcept;
    [[nodiscard]] bool setPalette(std::span<const Color> palette) noexcept;
    void clear() noexcept;
    std::int32_t rowCount() const noexcept { return mCount; }
    // Age zero is the newest row. Values are palette indices 0..15.
    [[nodiscard]] std::optional<std::uint8_t> sampleAt(std::int32_t age,
                                                        std::int32_t column) const noexcept;

protected:
    void drawSelf(Painter& painter, Rect absoluteFrame) noexcept override;

private:
    std::uint8_t paletteIndex(std::int32_t physicalRow, std::int32_t column) const noexcept;
    std::span<std::uint8_t> mPackedRows{};
    std::span<const Color> mPalette{};
    std::int32_t mColumns{0};
    std::int32_t mRows{0};
    std::int32_t mNextRow{0};
    std::int32_t mCount{0};
    bool mValid{false};
};

} // namespace lithe
