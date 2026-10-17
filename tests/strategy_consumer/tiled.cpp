// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "fixture.hpp"

namespace
{

class NativeFileSink final : public lithe::TileSink
{
public:
    explicit NativeFileSink(std::FILE* output) noexcept : mOutput(output)
    {
    }
    bool beginFrame(lithe::Size size, lithe::PixelFormat format) noexcept override
    {
        ++begins;
        return begins == 1U && size.width == 8 && size.height == 8 &&
               format == strategy_fixture::kFormat;
    }
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        using namespace strategy_fixture;
        if (begins != 1U || ends != 0U || !pixels.valid() || pixels.format != kFormat ||
            destination.x != 0 || destination.y != nextRow || destination.width != 8 ||
            destination.height != 2 || pixels.width != 8 || pixels.height != 2 ||
            pixels.strideBytes != kRowBytes || pixels.data.size() != kTileBytes ||
            !matchesOracle(pixels.data, bytesWritten))
        {
            return false;
        }
        // The sink consumes each native strip synchronously. It has no mutable
        // frame buffer; output is checked against immutable oracle bytes first.
        if (!writeBytes(mOutput, pixels.data))
        {
            return false;
        }
        bytesWritten += pixels.data.size();
        nextRow += 2;
        ++writes;
        return true;
    }
    bool endFrame(bool complete) noexcept override
    {
        ++ends;
        completed = complete && begins == 1U && ends == 1U && writes == 4U && nextRow == 8 &&
                    bytesWritten == strategy_fixture::kFrameBytes;
        return completed;
    }

    std::size_t begins{0U};
    std::size_t writes{0U};
    std::size_t ends{0U};
    std::size_t bytesWritten{0U};
    std::int32_t nextRow{0};
    bool completed{false};

private:
    std::FILE* mOutput;
};

} // namespace

int main(int argc, char* argv[])
{
    using namespace strategy_fixture;
    if (argc != 2)
    {
        return fail("Usage: litheview_strategy_consumer output-native.bin");
    }
    const std::span arguments{argv, lithe::detail::nonnegativeSize(argc)};
    PatternView root{};
    lithe::ViewController controller{root};
    lithe::Application application{kViewport};
    if (!application.pushScene(controller))
    {
        return fail("Scene initialization failed");
    }
    std::FILE* output = std::fopen(arguments[1], "wb");
    if (output == nullptr)
    {
        return fail("Cannot open native output file");
    }
    std::array<std::byte, kTileBytes> workspace{};
    const lithe::Surface tile{workspace, kFormat, 8, 2, kRowBytes};
    NativeFileSink sink{output};
    const auto result = lithe::TiledRenderer::render(application, {8, 8}, tile, sink);
    const bool closed = std::fclose(output) == 0;
    if (!result || !sink.completed || result.tileCount != 4U || result.pixelsTransferred != 64U ||
        !closed)
    {
        return fail(
            "Tiled rendering or native output failed its independent oracle/completion checks");
    }
    report(workspace.size(), result.tileCount);
    return 0;
}
