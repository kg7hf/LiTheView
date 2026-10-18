// LiTheView segmented output; owner: tinyGeckoA03/home
// Project input (LF) SHA-256: b2758e74dd7911ff21d559360dd562bf4d6ce0b934802488c3286fbe2a2db2d3
// Glass input (LF) SHA-256: b5451ebf501794e0fcac3010968cbeb73a3a451fb8ef9d9c0592abe453b3da8a
// Input input (LF) SHA-256: a46d8284f452ebe267563d56dea356003e09e2a0890031703a079162e2de8834
// Form input (LF) SHA-256: 401a53e9c8a0a25a5609af5a4ee591a5932b8d50919629c353b52ed581e9dbeb
// Glass SHA-256: b5451ebf501794e0fcac3010968cbeb73a3a451fb8ef9d9c0592abe453b3da8a
// Form source SHA-256: adcb4f9a47ee4edb9d8e069286e4a86cc754d54da96cef6d343266771974824b
// Generated presentation only. Application state and device behavior remain handwritten.
#pragma once
#include <lithe/segment_frame.hpp>
#include <array>
#include <cstdint>
#include <optional>
#include <string_view>
namespace lithe_generated::screen_home
{
using Frame = lithe::SegmentFrame<160>;
struct Values
{
    std::string_view field_label{};
    std::uint32_t field_value{};
    std::uint32_t field_volumePercent{};
};
inline constexpr std::array<std::array<std::size_t, 14>, 7> cells_label{{
        {{121U, 122U, 42U, 2U, 1U, 81U, 61U, 101U, 82U, 102U, 62U, 22U, 41U, 21U}},
        {{123U, 124U, 44U, 4U, 3U, 83U, 63U, 103U, 84U, 104U, 64U, 24U, 43U, 23U}},
        {{125U, 126U, 46U, 6U, 5U, 85U, 65U, 105U, 86U, 106U, 66U, 26U, 45U, 25U}},
        {{127U, 128U, 48U, 8U, 7U, 87U, 67U, 107U, 88U, 108U, 68U, 28U, 47U, 27U}},
        {{149U, 130U, 50U, 10U, 29U, 109U, 89U, 129U, 90U, 110U, 70U, 30U, 69U, 49U}},
        {{151U, 132U, 52U, 12U, 31U, 111U, 91U, 131U, 92U, 112U, 72U, 32U, 71U, 51U}},
        {{153U, 134U, 54U, 14U, 33U, 113U, 93U, 133U, 94U, 114U, 74U, 34U, 73U, 53U}},
}};
inline constexpr bool write_label(Frame& frame, std::size_t position, char character) noexcept
{
    if (position >= 7U) return false;
    switch (character)
    {
    case 32:
        return true;
    case 65:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][1U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][6U])) return false;
        if (!frame.set(cells_label[position][10U])) return false;
        return true;
    case 69:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][3U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][6U])) return false;
        if (!frame.set(cells_label[position][10U])) return false;
        return true;
    case 70:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][6U])) return false;
        if (!frame.set(cells_label[position][10U])) return false;
        return true;
    case 71:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][3U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][10U])) return false;
        return true;
    case 72:
        if (!frame.set(cells_label[position][1U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][6U])) return false;
        if (!frame.set(cells_label[position][10U])) return false;
        return true;
    case 73:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][3U])) return false;
        if (!frame.set(cells_label[position][8U])) return false;
        if (!frame.set(cells_label[position][12U])) return false;
        return true;
    case 76:
        if (!frame.set(cells_label[position][3U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        return true;
    case 77:
        if (!frame.set(cells_label[position][1U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][7U])) return false;
        if (!frame.set(cells_label[position][9U])) return false;
        return true;
    case 78:
        if (!frame.set(cells_label[position][1U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][7U])) return false;
        if (!frame.set(cells_label[position][11U])) return false;
        return true;
    case 79:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][1U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][3U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        return true;
    case 80:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][1U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][6U])) return false;
        if (!frame.set(cells_label[position][10U])) return false;
        return true;
    case 83:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][3U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][6U])) return false;
        if (!frame.set(cells_label[position][10U])) return false;
        return true;
    case 84:
        if (!frame.set(cells_label[position][0U])) return false;
        if (!frame.set(cells_label[position][8U])) return false;
        if (!frame.set(cells_label[position][12U])) return false;
        return true;
    case 85:
        if (!frame.set(cells_label[position][1U])) return false;
        if (!frame.set(cells_label[position][2U])) return false;
        if (!frame.set(cells_label[position][3U])) return false;
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        return true;
    case 86:
        if (!frame.set(cells_label[position][4U])) return false;
        if (!frame.set(cells_label[position][5U])) return false;
        if (!frame.set(cells_label[position][9U])) return false;
        if (!frame.set(cells_label[position][13U])) return false;
        return true;
    case 89:
        if (!frame.set(cells_label[position][7U])) return false;
        if (!frame.set(cells_label[position][9U])) return false;
        if (!frame.set(cells_label[position][12U])) return false;
        return true;
    default: return false;
    }
}
inline constexpr std::array<std::array<std::size_t, 7>, 4> cells_value{{
        {{18U, 58U, 118U, 138U, 98U, 38U, 78U}},
        {{17U, 57U, 117U, 137U, 97U, 37U, 77U}},
        {{16U, 56U, 116U, 136U, 96U, 36U, 76U}},
        {{15U, 55U, 115U, 135U, 95U, 35U, 75U}},
}};
inline constexpr bool write_value(Frame& frame, std::size_t position, char character) noexcept
{
    if (position >= 4U) return false;
    switch (character)
    {
    case 48:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        if (!frame.set(cells_value[position][3U])) return false;
        if (!frame.set(cells_value[position][4U])) return false;
        if (!frame.set(cells_value[position][5U])) return false;
        return true;
    case 49:
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        return true;
    case 50:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][3U])) return false;
        if (!frame.set(cells_value[position][4U])) return false;
        if (!frame.set(cells_value[position][6U])) return false;
        return true;
    case 51:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        if (!frame.set(cells_value[position][3U])) return false;
        if (!frame.set(cells_value[position][6U])) return false;
        return true;
    case 52:
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        if (!frame.set(cells_value[position][5U])) return false;
        if (!frame.set(cells_value[position][6U])) return false;
        return true;
    case 53:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        if (!frame.set(cells_value[position][3U])) return false;
        if (!frame.set(cells_value[position][5U])) return false;
        if (!frame.set(cells_value[position][6U])) return false;
        return true;
    case 54:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        if (!frame.set(cells_value[position][3U])) return false;
        if (!frame.set(cells_value[position][4U])) return false;
        if (!frame.set(cells_value[position][5U])) return false;
        if (!frame.set(cells_value[position][6U])) return false;
        return true;
    case 55:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        return true;
    case 56:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        if (!frame.set(cells_value[position][3U])) return false;
        if (!frame.set(cells_value[position][4U])) return false;
        if (!frame.set(cells_value[position][5U])) return false;
        if (!frame.set(cells_value[position][6U])) return false;
        return true;
    case 57:
        if (!frame.set(cells_value[position][0U])) return false;
        if (!frame.set(cells_value[position][1U])) return false;
        if (!frame.set(cells_value[position][2U])) return false;
        if (!frame.set(cells_value[position][3U])) return false;
        if (!frame.set(cells_value[position][5U])) return false;
        if (!frame.set(cells_value[position][6U])) return false;
        return true;
    default: return false;
    }
}
[[nodiscard]] inline constexpr std::optional<Frame> encode(const Values& values) noexcept
{
    Frame frame{};
    if (values.field_label.size() > 7U) return {};
    for (std::size_t i = 0; i < values.field_label.size(); ++i)
        if (!write_label(frame, i, values.field_label[i])) return {};
    if (values.field_value >= 10000U) return {};
    auto rest_value = values.field_value;
    for (std::size_t remaining = 4U; remaining > 0; --remaining)
    {
        constexpr std::string_view digits{"0123456789"};
        if (!write_value(frame, remaining - 1U, digits[rest_value % 10U])) return {};
        rest_value /= 10U;
        if (rest_value == 0) break;
    }
    if (values.field_volumePercent > 100U) return {};
    const auto count_volumePercent = (std::uint64_t{values.field_volumePercent} * 8U + 50U) / 100U;
    if (!frame.set(140U, 0U < count_volumePercent)) return {};
    if (!frame.set(147U, 1U < count_volumePercent)) return {};
    if (!frame.set(146U, 2U < count_volumePercent)) return {};
    if (!frame.set(145U, 3U < count_volumePercent)) return {};
    if (!frame.set(144U, 4U < count_volumePercent)) return {};
    if (!frame.set(143U, 5U < count_volumePercent)) return {};
    if (!frame.set(142U, 6U < count_volumePercent)) return {};
    if (!frame.set(141U, 7U < count_volumePercent)) return {};
    return frame;
}
}
