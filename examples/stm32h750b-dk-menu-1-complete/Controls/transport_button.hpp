// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Application/playback.hpp"
#include "lithe/widgets.hpp"

#include <array>
#include <cstddef>
namespace menu::controls
{
namespace symbols
{
enum class Shape
{
    play,
    pause,
    stop
};
// Fixed 16x16, MSB-first alpha masks, generated entirely at compile time.
// No RGB asset, runtime resizing, heap allocation or borrowed temporary exists.
consteval auto mask(Shape shape)
{
    std::array<std::byte, 32U> bits{};
    for (std::size_t y = 0; y < 16U; ++y)
    {
        for (std::size_t x = 0; x < 16U; ++x)
        {
            const auto distance = y < 8U ? 7U - y : y - 8U;
            const bool on =
                shape == Shape::play
                    ? (x >= 3U && x <= 12U && distance < 6U && x - 3U <= 10U - 2U * distance)
                : shape == Shape::pause
                    ? (y >= 3U && y < 13U && ((x >= 3U && x < 7U) || (x >= 9U && x < 13U)))
                    : (x >= 3U && x < 13U && y >= 3U && y < 13U);
            if (on)
                bits[y * 2U + x / 8U] |= std::byte{0x80} >> (x % 8U);
        }
    }
    return bits;
}
inline constexpr auto kPlayBits = mask(Shape::play);
inline constexpr auto kPauseBits = mask(Shape::pause);
inline constexpr auto kStopBits = mask(Shape::stop);
inline constexpr lithe::Bitmap kPlay{kPlayBits, 16, 16, 2U, lithe::BitmapFormat::alpha1};
inline constexpr lithe::Bitmap kPause{kPauseBits, 16, 16, 2U, lithe::BitmapFormat::alpha1};
inline constexpr lithe::Bitmap kStop{kStopBits, 16, 16, 2U, lithe::BitmapFormat::alpha1};
static_assert(kPlayBits.size() + kPauseBits.size() + kStopBits.size() == 96U);
} // namespace symbols
class PlayPauseButton : public lithe::ButtonView
{
public:
    using ButtonView::ButtonView;
    void show(PlaybackState state) noexcept
    {
        const bool playing = state == PlaybackState::playing;
        setText(playing ? "PAUSE" : "PLAY");
        setIcon(playing ? symbols::kPause : symbols::kPlay);
    }
};
class StopButton : public lithe::ButtonView
{
public:
    using ButtonView::ButtonView;
    void show() noexcept
    {
        setText("STOP");
        setIcon(symbols::kStop);
    }
};
} // namespace menu::controls
