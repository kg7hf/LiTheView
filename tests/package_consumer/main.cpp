// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include <array>
#include <lithe/lithe.hpp>
#include <span>

namespace
{

class ConsumerDisplay final : public lithe::Display
{
public:
    lithe::Surface surface() noexcept override
    {
        return {mPixels, 16, 16, 16};
    }

    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        mPresentations += regions.size();
    }

    std::size_t presentations() const noexcept
    {
        return mPresentations;
    }

private:
    std::array<lithe::Pixel, 16U * 16U> mPixels{};
    std::size_t mPresentations{0};
};

} // namespace

int main()
{
    ConsumerDisplay display{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{display.surface(), damage};
    painter.fillRect({0, 0, 16, 16}, lithe::Color::rgb(0x2d6cdfU));
    display.present(damage.regions());
    return display.presentations() == 1U ? 0 : 1;
}
