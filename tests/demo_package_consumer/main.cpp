// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "configuration.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <span>

namespace
{

class NativeDisplay final : public lithe::Display
{
public:
    using Storage = typename lithe::PixelFormatTraits<kFormat>::Storage;
    static constexpr auto kWidth = lithe::demo::Demo::kWidth;
    static constexpr auto kHeight = lithe::demo::Demo::kHeight;
    static constexpr std::size_t kPixelCount =
        lithe::detail::nonnegativeSize(kWidth) * lithe::detail::nonnegativeSize(kHeight);

    lithe::Surface surface() noexcept override
    {
        return lithe::Surface{lithe::PixelSurface<kFormat>{pixels, kWidth, kHeight, kWidth}};
    }
    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        presentations += regions.empty() ? 0U : 1U;
    }

    std::array<Storage, kPixelCount> pixels{};
    std::size_t presentations{0U};
};

std::uint32_t crc32(std::span<const std::byte> data) noexcept
{
    std::uint32_t crc = 0xffffffffU;
    for (const auto value : data)
    {
        crc ^= std::to_integer<std::uint32_t>(value);
        for (std::size_t bit = 0U; bit < 8U; ++bit)
        {
            crc = (crc >> 1U) ^ ((crc & 1U) != 0U ? 0xedb88320U : 0U);
        }
    }
    return crc ^ 0xffffffffU;
}

// These are application-owned static objects. The installed scene library owns
// neither the full-frame output storage nor the platform presentation adapter.
NativeDisplay display{};
lithe::demo::Demo demo{};

} // namespace

int main(int argc, char* argv[])
{
    const std::span<char*> arguments{argv, lithe::detail::nonnegativeSize(argc)};
    if (arguments.size() != 2U || !demo.initialize() || !demo.render(display) ||
        display.presentations != 1U)
    {
        return 1;
    }
    const auto surface = display.surface();
    const auto bytes = std::span<const std::byte>{surface.data}.first(surface.usedBytes());
    if (crc32(bytes) != kExpectedCrc)
    {
        std::puts("Initial scene differs from the retained independent host/board baseline.");
        return 1;
    }
    auto* output = std::fopen(arguments[1U], "wb");
    if (output == nullptr)
    {
        return 1;
    }
    bool written = true;
    for (const auto value : bytes)
    {
        if (std::fputc(std::to_integer<int>(value), output) == EOF)
        {
            written = false;
            break;
        }
    }
    const bool closed = std::fclose(output) == 0;
    std::printf("framebuffer_bytes=%zu presentations=%zu\n", bytes.size(), display.presentations);
    return written && closed ? 0 : 1;
}
