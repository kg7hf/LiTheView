// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/telemetry_views.hpp"

#include "lithe/detail/arithmetic.hpp"

#include <algorithm>

namespace lithe
{

namespace
{
bool validTicks(std::span<const AxisTick> ticks, std::size_t maximumCount,
                std::size_t maximumLabelBytes) noexcept
{
    if (ticks.size() > maximumCount)
        return false;
    std::uint16_t previous{0U};
    bool first{true};
    for (const AxisTick& tick : ticks)
    {
        if ((!first && tick.position <= previous) || tick.label.size() > maximumLabelBytes)
            return false;
        previous = tick.position;
        first = false;
    }
    return true;
}

std::int32_t normalizedX(Rect frame, std::uint16_t position) noexcept
{
    return detail::saturatingAdd(
        frame.x, detail::multiplyDivide(position, frame.width - 1, 65535));
}
} // namespace

bool AxisView::setTicks(std::span<const AxisTick> ticks) noexcept
{
    if (!validTicks(ticks, kMaximumTicks, kMaximumLabelBytes))
        return false;
    mTicks = ticks;
    invalidate();
    return true;
}

void AxisView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    if (absoluteFrame.empty())
        return;
    const Style colors = resolvedStyle();
    if (mOrientation == AxisOrientation::horizontal)
    {
        const std::int32_t baseline = absoluteFrame.y;
        painter.drawLine({absoluteFrame.x, baseline},
                         {absoluteFrame.right() - 1, baseline}, colors.foreground);
        for (const AxisTick& tick : mTicks)
        {
            const std::int32_t x = normalizedX(absoluteFrame, tick.position);
            painter.drawLine({x, baseline},
                             {x, std::min(absoluteFrame.bottom() - 1, baseline + 3)},
                             colors.foreground);
            if (tick.label.empty() || absoluteFrame.height <= 4)
                continue;
            const TextMetrics metrics = Painter::measureText(tick.label, *mFont, colors.textScale);
            const std::int32_t labelWidth = std::min(metrics.width, absoluteFrame.width);
            const std::int32_t left = std::clamp(x - labelWidth / 2, absoluteFrame.x,
                                                  absoluteFrame.right() - labelWidth);
            painter.drawText({left, baseline + 4, labelWidth,
                              absoluteFrame.bottom() - (baseline + 4)},
                             tick.label, *mFont, colors.foreground,
                             {HorizontalAlign::left, VerticalAlign::center}, colors.textScale);
        }
    }
    else
    {
        const std::int32_t baseline = absoluteFrame.right() - 1;
        painter.drawLine({baseline, absoluteFrame.y},
                         {baseline, absoluteFrame.bottom() - 1}, colors.foreground);
        for (const AxisTick& tick : mTicks)
        {
            const std::int32_t y = detail::saturatingSubtract(
                absoluteFrame.bottom() - 1,
                detail::multiplyDivide(tick.position, absoluteFrame.height - 1, 65535));
            painter.drawLine({std::max(absoluteFrame.x, baseline - 3), y},
                             {baseline, y}, colors.foreground);
            if (tick.label.empty() || absoluteFrame.width <= 4)
                continue;
            const TextMetrics metrics = Painter::measureText(tick.label, *mFont, colors.textScale);
            const std::int32_t labelHeight = std::min(metrics.height, absoluteFrame.height);
            const std::int32_t top = std::clamp(y - labelHeight / 2, absoluteFrame.y,
                                                 absoluteFrame.bottom() - labelHeight);
            painter.drawText({absoluteFrame.x, top, absoluteFrame.width - 4, labelHeight},
                             tick.label, *mFont, colors.foreground,
                             {HorizontalAlign::right, VerticalAlign::center}, colors.textScale);
        }
    }
}

bool MarkerRangeView::setRanges(std::span<const OverlayRange> ranges) noexcept
{
    if (ranges.size() > kMaximumRanges)
        return false;
    std::uint16_t previousEnd{0U};
    bool first{true};
    for (const OverlayRange& range : ranges)
    {
        if (range.start >= range.end || (!first && range.start < previousEnd))
            return false;
        previousEnd = range.end;
        first = false;
    }
    mRanges = ranges;
    invalidate();
    return true;
}

bool MarkerRangeView::setMarkers(std::span<const OverlayMarker> markers) noexcept
{
    if (markers.size() > kMaximumMarkers)
        return false;
    std::uint16_t previous{0U};
    bool first{true};
    for (const OverlayMarker& marker : markers)
    {
        if ((!first && marker.position <= previous) ||
            marker.label.size() > kMaximumLabelBytes)
            return false;
        previous = marker.position;
        first = false;
    }
    mMarkers = markers;
    invalidate();
    return true;
}

void MarkerRangeView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    if (absoluteFrame.empty())
        return;
    for (const OverlayRange& range : mRanges)
    {
        const std::int32_t left = normalizedX(absoluteFrame, range.start);
        const std::int32_t right = normalizedX(absoluteFrame, range.end);
        if (range.fill.alpha != 0U)
        {
            const std::int32_t width = std::max<std::int32_t>(
                1, detail::saturatingSubtract(right, left));
            painter.fillRect({left, absoluteFrame.y,
                              width, absoluteFrame.height}, range.fill);
        }
    }
    const Style colors = resolvedStyle();
    for (const OverlayMarker& marker : mMarkers)
    {
        const std::int32_t x = normalizedX(absoluteFrame, marker.position);
        if (marker.color.alpha == 0U)
            continue;
        painter.drawLine({x, absoluteFrame.y}, {x, absoluteFrame.bottom() - 1}, marker.color);
        if (marker.label.empty() || absoluteFrame.height < 8)
            continue;
        const TextMetrics metrics = Painter::measureText(marker.label, *mFont, colors.textScale);
        const std::int32_t width = std::min(metrics.width, absoluteFrame.width);
        const std::int32_t left = std::clamp(x - width / 2, absoluteFrame.x,
                                              absoluteFrame.right() - width);
        painter.drawText({left, absoluteFrame.y, width, absoluteFrame.height},
                         marker.label, *mFont, marker.color,
                         {HorizontalAlign::left, VerticalAlign::top}, colors.textScale);
    }
}

bool MeterView::setScale(std::int32_t minimum, std::int32_t maximum,
                         std::span<const AxisTick> ticks) noexcept
{
    if (minimum >= maximum || !validTicks(ticks, kMaximumTicks, kMaximumLabelBytes))
        return false;
    mMinimum = minimum;
    mMaximum = maximum;
    mTicks = ticks;
    invalidate();
    return true;
}

bool MeterView::setZones(std::span<const MeterZone> zones) noexcept
{
    if (zones.size() > kMaximumZones)
        return false;
    std::uint16_t previousEnd{0U};
    bool first{true};
    for (const MeterZone& zone : zones)
    {
        if (zone.start >= zone.end || (!first && zone.start < previousEnd))
            return false;
        previousEnd = zone.end;
        first = false;
    }
    mZones = zones;
    invalidate();
    return true;
}

void MeterView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    if (absoluteFrame.empty())
        return;
    const Style colors = resolvedStyle();
    const bool showScale = absoluteFrame.height >= 16 && !mTicks.empty();
    const std::int32_t barHeight = absoluteFrame.height - (showScale ? 12 : 0);
    const Rect bar{absoluteFrame.x, absoluteFrame.y, absoluteFrame.width, barHeight};
    const std::int64_t numerator =
        std::int64_t(std::clamp(mReading, mMinimum, mMaximum)) - mMinimum;
    const std::int64_t denominator = std::int64_t(mMaximum) - mMinimum;
    // The quotient is in [0, 65535] even for the full signed 32-bit range.
    const auto position = std::uint16_t(numerator * 65535 / denominator);
    const std::int32_t filled = detail::multiplyDivide(position, bar.width, 65535);
    if (filled > 0)
        painter.fillRect({bar.x, bar.y, filled, bar.height}, colors.accent);
    for (const MeterZone& zone : mZones)
    {
        if (zone.color.alpha == 0U)
            continue;
        const std::int32_t left = detail::multiplyDivide(zone.start, bar.width, 65535);
        const std::int32_t right = std::min(filled,
            detail::multiplyDivide(zone.end, bar.width, 65535));
        if (right > left)
            painter.fillRect({detail::saturatingAdd(bar.x, left), bar.y,
                              right - left, bar.height}, zone.color);
    }
    if (colors.border.alpha != 0U)
        painter.strokeRect(bar, colors.border, 1U);
    if (!showScale)
        return;
    for (const AxisTick& tick : mTicks)
    {
        const std::int32_t x = normalizedX(absoluteFrame, tick.position);
        painter.drawLine({x, bar.bottom()}, {x, bar.bottom() + 2}, colors.foreground);
        if (tick.label.empty())
            continue;
        const TextMetrics metrics = Painter::measureText(tick.label, *mFont, colors.textScale);
        const std::int32_t width = std::min(metrics.width, absoluteFrame.width);
        const std::int32_t left = std::clamp(x - width / 2, absoluteFrame.x,
                                              absoluteFrame.right() - width);
        painter.drawText({left, bar.bottom() + 3, width,
                          absoluteFrame.bottom() - (bar.bottom() + 3)},
                         tick.label, *mFont, colors.foreground,
                         {HorizontalAlign::left, VerticalAlign::center}, colors.textScale);
    }
}

bool PlotView::setSamples(std::span<const std::uint16_t> samples) noexcept
{
    if (samples.size() > kMaximumSamples)
        return false;
    mSamples = samples;
    invalidate();
    return true;
}

bool PlotView::setSecondarySamples(std::span<const std::uint16_t> samples,
                                   Color color) noexcept
{
    if (samples.size() > kMaximumSamples)
        return false;
    mSecondarySamples = samples;
    mSecondaryColor = color;
    invalidate();
    return true;
}

bool PlotView::setRange(std::uint16_t minimum, std::uint16_t maximum) noexcept
{
    if (minimum >= maximum)
        return false;
    mMinimum = minimum;
    mMaximum = maximum;
    invalidate();
    return true;
}

bool PlotView::setGrid(std::uint8_t verticalDivisions,
                       std::uint8_t horizontalDivisions,
                       Color color) noexcept
{
    if (verticalDivisions > 16U || horizontalDivisions > 16U)
        return false;
    mVerticalDivisions = verticalDivisions;
    mHorizontalDivisions = horizontalDivisions;
    mGridColor = color;
    invalidate();
    return true;
}

bool PlotView::setCursor(std::optional<std::size_t> sampleIndex, Color color) noexcept
{
    if (sampleIndex && *sampleIndex >= kMaximumSamples)
        return false;
    mCursor = sampleIndex.value_or(kMaximumSamples);
    mCursorColor = color;
    invalidate();
    return true;
}

void PlotView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    if (absoluteFrame.empty())
        return;

    const Style colors = resolvedStyle();
    const std::int32_t count = std::int32_t(mSamples.size());
    const std::int32_t xSpan = absoluteFrame.width - 1;
    const std::int32_t ySpan = absoluteFrame.height - 1;
    if (mGridColor.alpha != 0U)
    {
        for (std::int32_t division = 1; division < mVerticalDivisions; ++division)
        {
            const auto x = detail::saturatingAdd(
                absoluteFrame.x, detail::multiplyDivide(division, xSpan, mVerticalDivisions));
            painter.drawLine({x, absoluteFrame.y}, {x, absoluteFrame.bottom() - 1}, mGridColor);
        }
        for (std::int32_t division = 1; division < mHorizontalDivisions; ++division)
        {
            const auto y = detail::saturatingAdd(
                absoluteFrame.y, detail::multiplyDivide(division, ySpan, mHorizontalDivisions));
            painter.drawLine({absoluteFrame.x, y}, {absoluteFrame.right() - 1, y}, mGridColor);
        }
    }
    if (mCursor < mSamples.size() && mCursorColor.alpha != 0U)
    {
        // LITHEVIEW_CAST_BOUNDARY: setCursor bounds the index below 4096.
        const auto cursorIndex = static_cast<std::int32_t>(mCursor);
        const auto x = detail::saturatingAdd(
            absoluteFrame.x,
            count == 1 ? 0 : detail::multiplyDivide(cursorIndex, xSpan, count - 1));
        painter.drawLine({x, absoluteFrame.y}, {x, absoluteFrame.bottom() - 1}, mCursorColor);
    }
    // Draw the reference underneath the live trace so crossings remain legible.
    drawTrace(painter, absoluteFrame, mSecondarySamples, mSecondaryColor);
    drawTrace(painter, absoluteFrame, mSamples,
              colors.foreground.alpha == 0U ? colors.accent : colors.foreground);
    const Style border = resolvedStyle();
    if (border.borderWidth != 0U && border.border.alpha != 0U)
        painter.strokeRect(absoluteFrame, border.border, border.borderWidth);
}

void PlotView::drawTrace(Painter& painter, Rect frame,
                         std::span<const std::uint16_t> samples, Color color) const noexcept
{
    if (samples.empty() || color.alpha == 0U)
        return;
    // Both spans are bounded to kMaximumSamples by their setters.
    const std::int32_t count = std::int32_t(samples.size());
    const std::int32_t denominator = std::int32_t(mMaximum) - std::int32_t(mMinimum);
    const std::int32_t xSpan = frame.width - 1;
    const std::int32_t ySpan = frame.height - 1;
    Point previous{};
    for (std::int32_t index = 0; index < count; ++index)
    {
        const std::int32_t value =
            std::clamp(std::int32_t(samples[std::size_t(index)]),
                       std::int32_t(mMinimum), std::int32_t(mMaximum));
        const Point current{
            detail::saturatingAdd(frame.x,
                                  count == 1 ? 0 : detail::multiplyDivide(index, xSpan, count - 1)),
            detail::saturatingSubtract(
                detail::saturatingSubtract(frame.bottom(), 1),
                detail::multiplyDivide(value - std::int32_t(mMinimum), ySpan, denominator))};
        if (mMode != PlotMode::points && index == 0)
            painter.fillRect({current.x, current.y, 1, 1}, color);
        else if (mMode != PlotMode::points)
            painter.drawLine(previous, current, color);
        if (mMode != PlotMode::lines)
            painter.fillRect({detail::saturatingSubtract(current.x, 1),
                              detail::saturatingSubtract(current.y, 1), 3, 3}, color);
        previous = current;
    }
}

WaterfallView::WaterfallView(Rect frame,
                             std::span<std::uint8_t> packedRows,
                             std::int32_t columns,
                             std::int32_t rows,
                             std::span<const Color> palette,
                             Style style) noexcept
    : View(frame, style), mPackedRows(packedRows), mPalette(palette), mColumns(columns), mRows(rows)
{
    const std::size_t needed = requiredBytes(columns, rows);
    mValid = needed != 0U && packedRows.size() >= needed && palette.size() == kPaletteSize;
}

bool WaterfallView::bindStorage(std::span<std::uint8_t> packedRows,
                                std::int32_t columns,
                                std::int32_t rows,
                                std::span<const Color> palette) noexcept
{
    const std::size_t needed = requiredBytes(columns, rows);
    if (needed == 0U || packedRows.size() < needed || palette.size() != kPaletteSize)
        return false;
    mPackedRows = packedRows;
    mColumns = columns;
    mRows = rows;
    mPalette = palette;
    mNextRow = 0;
    mCount = 0;
    mValid = true;
    invalidate();
    return true;
}

bool WaterfallView::pushRow(std::span<const std::uint8_t> intensities) noexcept
{
    if (!mValid || intensities.size() != detail::nonnegativeSize(mColumns))
        return false;
    const std::size_t rowBytes = (detail::nonnegativeSize(mColumns) + 1U) / 2U;
    const std::size_t offset = detail::nonnegativeSize(mNextRow) * rowBytes;
    std::fill_n(mPackedRows.begin() + std::ptrdiff_t(offset), std::ptrdiff_t(rowBytes), 0U);
    for (std::size_t column = 0U; column < intensities.size(); ++column)
    {
        const std::uint8_t index = intensities[column] >> 4U;
        std::uint8_t& cell = mPackedRows[offset + column / 2U];
        if (column % 2U == 0U)
            cell = (cell & 0x0fU) | std::uint8_t(index << 4U);
        else
            cell = (cell & 0xf0U) | index;
    }
    mNextRow = (mNextRow + 1) % mRows;
    if (mCount < mRows)
        ++mCount;
    invalidate();
    return true;
}

bool WaterfallView::setPalette(std::span<const Color> palette) noexcept
{
    if (palette.size() != kPaletteSize)
        return false;
    mPalette = palette;
    mValid = requiredBytes(mColumns, mRows) != 0U &&
             mPackedRows.size() >= requiredBytes(mColumns, mRows);
    invalidate();
    return true;
}

void WaterfallView::clear() noexcept
{
    mNextRow = 0;
    mCount = 0;
    invalidate();
}

std::optional<std::uint8_t> WaterfallView::sampleAt(std::int32_t age,
                                                     std::int32_t column) const noexcept
{
    if (!mValid || age < 0 || age >= mCount || column < 0 || column >= mColumns)
        return std::nullopt;
    const std::int32_t physicalRow = (mNextRow + mRows - 1 - age) % mRows;
    return paletteIndex(physicalRow, column);
}

std::uint8_t WaterfallView::paletteIndex(std::int32_t physicalRow,
                                         std::int32_t column) const noexcept
{
    const std::size_t rowBytes = (detail::nonnegativeSize(mColumns) + 1U) / 2U;
    const std::size_t offset = detail::nonnegativeSize(physicalRow) * rowBytes +
                               detail::nonnegativeSize(column) / 2U;
    const std::uint8_t cell = mPackedRows[offset];
    return column % 2 == 0 ? std::uint8_t(cell >> 4U) : std::uint8_t(cell & 0x0fU);
}

void WaterfallView::drawSelf(Painter& painter, Rect absoluteFrame) noexcept
{
    View::drawSelf(painter, absoluteFrame);
    if (!mValid || absoluteFrame.empty())
        return;

    for (std::int32_t age = 0; age < mCount; ++age)
    {
        const std::int32_t physicalRow = (mNextRow + mRows - 1 - age) % mRows;
        const std::int32_t top = detail::saturatingAdd(
            absoluteFrame.y, detail::multiplyDivide(age, absoluteFrame.height, mRows));
        const std::int32_t bottom = detail::saturatingAdd(
            absoluteFrame.y, detail::multiplyDivide(age + 1, absoluteFrame.height, mRows));
        if (bottom <= top)
            continue;
        std::int32_t column = 0;
        while (column < mColumns)
        {
            const std::uint8_t index = paletteIndex(physicalRow, column);
            const std::int32_t first = column;
            do
            {
                ++column;
            } while (column < mColumns && paletteIndex(physicalRow, column) == index);
            const std::int32_t left = detail::saturatingAdd(
                absoluteFrame.x, detail::multiplyDivide(first, absoluteFrame.width, mColumns));
            const std::int32_t right = detail::saturatingAdd(
                absoluteFrame.x, detail::multiplyDivide(column, absoluteFrame.width, mColumns));
            if (right > left && mPalette[index].alpha != 0U)
                painter.fillRect({left, top, right - left, bottom - top}, mPalette[index]);
        }
    }
    const Style border = resolvedStyle();
    if (border.borderWidth != 0U && border.border.alpha != 0U)
        painter.strokeRect(absoluteFrame, border.border, border.borderWidth);
}

} // namespace lithe
