// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "fixture.hpp"

namespace
{

class FrameDisplay final : public lithe::Display
{
public:
    explicit FrameDisplay(std::span<std::byte> pixels) noexcept : mPixels(pixels)
    {
    }
    lithe::Surface surface() noexcept override
    {
        return {mPixels, strategy_fixture::kFormat, 8, 8, strategy_fixture::kRowBytes};
    }
    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        if (!regions.empty())
        {
            ++presentations;
        }
    }

    std::size_t presentations{0U};

private:
    std::span<std::byte> mPixels;
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
    std::array<std::byte, kFrameBytes> frame{};
    FrameDisplay display{frame};
    PatternView root{};
    lithe::ViewController controller{root};
    lithe::Application application{kViewport};
    if (!application.pushScene(controller) || !application.render(display) ||
        display.presentations != 1U || !matchesOracle(frame))
    {
        return fail("Full-frame rendering failed its independent native-byte oracle");
    }
    std::FILE* output = std::fopen(arguments[1], "wb");
    if (output == nullptr)
    {
        return fail("Cannot open native output file");
    }
    const bool written = writeBytes(output, frame);
    const bool closed = std::fclose(output) == 0;
    if (!written || !closed)
    {
        return fail("Could not finish writing native output");
    }
    report(frame.size(), 0U);
    return 0;
}
