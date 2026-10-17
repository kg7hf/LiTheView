// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include <array>
#include <cstddef>
#include <lithe/lithe.hpp>

namespace
{

constexpr lithe::Size kFrameSize{16, 16};
constexpr auto kViewport = lithe::Viewport::rectangle({0, 0, 16, 16});
static_assert(kViewport.valid());

// Synchronous streaming consumer. There is no framebuffer, Display subclass,
// profile loader, example-library dependency, or target SDK in this program.
class RedTileSink final : public lithe::TileSink
{
public:
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        return size.width == 16 && size.height == 16 && format == lithe::PixelFormat::rgb565;
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        if (!pixels.valid() || pixels.format != lithe::PixelFormat::rgb565 || destination.x != 0 ||
            destination.y != mNextRow || destination.width != 16 || destination.height != 4 ||
            pixels.width != 16 || pixels.height != 4 || pixels.strideBytes != 32U ||
            pixels.data.size() != 128U)
        {
            return false;
        }
        for (std::size_t offset = 0U; offset < pixels.data.size(); offset += 2U)
        {
            // Exact RGB565 byte encoding for full red, specified independently.
            if (pixels.data[offset] != std::byte{0x00} ||
                pixels.data[offset + 1U] != std::byte{0xf8})
            {
                return false;
            }
        }
        mNextRow += 4;
        return true;
    }
    bool endFrame(bool complete) noexcept override
    {
        return complete && mNextRow == 16;
    }

private:
    std::int32_t mNextRow{0};
};

} // namespace

int main()
{
    lithe::Style style{};
    style.background = lithe::Color::rgb(0xff0000U);
    lithe::View root{kViewport.bounds, style};
    lithe::ViewController controller{root};
    lithe::Application application{kViewport};
    if (!application.pushScene(controller))
    {
        return 1;
    }
    std::array<lithe::Pixel, 64U> pixels{};
    static_assert(sizeof(pixels) == 128U);
    const lithe::Surface workspace{pixels, 16, 4, 16};
    RedTileSink sink{};
    const auto result = lithe::TiledRenderer::render(application, kFrameSize, workspace, sink);
    return result.ok() && result.tileCount == 4U && result.pixelsTransferred == 256U ? 0 : 1;
}
