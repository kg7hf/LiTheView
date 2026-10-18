// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#include "src/designer_preview/segment_preview.hpp"

#include <bit>
#include <vector>

using Bytes = std::vector<std::byte>;
void word(Bytes& bytes, std::uint32_t value)
{
    if constexpr (std::endian::native == std::endian::big)
        value = std::byteswap(value);
    const auto encoded = std::as_bytes(std::span{&value, 1U});
    bytes.insert(bytes.end(), encoded.begin(), encoded.end());
}
Bytes request(std::uint32_t kind, std::uint32_t value)
{
    Bytes bytes;
    for (auto n : {8U, 8U, 17U, 1U, 2U})
        word(bytes, n);
    // A sparse two-electrode field; text 'T' uses only its second stroke.
    for (auto n : {kind,
                   2U,
                   kind == 2 ? 2U : 0U,
                   kind == 1 ? 3U : 1U,
                   kind == 2 ? 1U : 0U,
                   value,
                   kind == 2 ? 1U : 0U,
                   3U,
                   9U})
        word(bytes, n);
    if (kind == 2)
        for (auto n : {84U, 2U, 84U})
            word(bytes, n);
    for (auto id : {3U, 9U})
        for (auto n : {id, 4U, 1U, 1U, 6U, 1U, 6U, 6U, 1U, 6U})
            word(bytes, n);
    return bytes;
}
int main()
{
    static lithe::designer::segments::Renderer renderer;
    const auto render = [](std::span<const std::byte> payload)
    {
        return renderer.render(payload);
    };
    auto bytes = request(0, 1);
    auto image = render(bytes);
    if (!image || image->bits.size() != 3 || image->bits[0] != std::byte{8} ||
        image->bits[1] != std::byte{2} || image->bits[2] != std::byte{0})
        return 1;
    if (image->rgba.size() != 256 || image->rgba[0] != std::byte{230} ||
        image->rgba[(2 * 8 + 2) * 4] != std::byte{25})
        return 2;
    for (std::size_t size = 0; size < bytes.size(); ++size)
        if (render(std::span<const std::byte>{bytes}.first(size)))
            return 3;
    bytes.push_back(std::byte{0});
    if (render(bytes))
        return 4;
    if (render(request(4, 0)) || render(request(0, 2)) || render(request(1, 4)))
        return 5;
    image = render(request(2, 0));
    if (!image || image->bits[0] != std::byte{0} || image->bits[1] != std::byte{2})
        return 6;
    for (std::uint32_t value = 0; value <= 3; ++value)
    {
        image = render(request(1, value));
        if (!image || image->bits[0] != (value > 0 ? std::byte{8} : std::byte{0}) ||
            image->bits[1] != (value == 3 ? std::byte{2} : std::byte{0}))
            return 7;
    }
    image = render(request(0, 0));
    if (!image || image->bits[0] != std::byte{0} || image->bits[1] != std::byte{0})
        return 8;
    return 0;
}
