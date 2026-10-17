// Generated from a validated display definition; target feasibility is separate.
#pragma once
#include "lithe/display_definition.hpp"
#include "lithe/render_config.hpp"
#include <string_view>

namespace lithe::generated::assets::StorageMenu
{
struct DisplayConfiguration final
{
    static constexpr std::string_view kSourceSha256{ "090a0326cd60197f4930d865154a04d962465855797a85428ef32ed12a9f2303" };
    static constexpr std::string_view kInputSha256{ "e4ccff1999985019cb1b6359221ab27f1804d6e417457e28df51d6a7708c9f3c" };
    static constexpr DisplayDefinition kDefinition{
        "litheview.menu.h750.surface", { 480, 272 }, ViewportShape::rectangle,
        { 0, 0, 480, 272 },
        PixelFormat::rgb565, DisplayAddressSpace::pixels
    };
    static constexpr LogicalInputConfiguration kInput{
        true, true, false, false, true
    };
    static_assert(validateDisplayDefinition(kDefinition).has_value());
    static constexpr auto kViewport = *displayViewport(kDefinition);
    static constexpr PixelFormat kFormat{kDefinition.format};
    static_assert(supportsDestination(kFormat), "The library build must enable the selected destination format.");
    static constexpr auto kNative = *nativeDescriptor(kFormat);
    static_assert(kNative.storageBlock.width == 1);
    static_assert(kNative.storageBlock.height == 1);
    static_assert(kNative.storageBlock.bytes == 2U);
    static_assert(kNative.fieldCount == 3U);
    static_assert(kNative.channelModel == NativeChannelModel::rgb);
    static_assert(kNative.storesDestinationAlpha == false);
    static_assert(kNative.supportsSourceAlpha == true);
    static_assert(kNative.supportsSourceCoverage == true);
    static_assert(kNative.supportsSourceOver == true);
    static_assert(kNative.fields[0U].channel == NativeChannel::red && kNative.fields[0U].bitOffset == 11U && kNative.fields[0U].bitWidth == 5U && kNative.fields[0U].position.x == 0 && kNative.fields[0U].position.y == 0);
    static_assert(kNative.fields[1U].channel == NativeChannel::green && kNative.fields[1U].bitOffset == 5U && kNative.fields[1U].bitWidth == 6U && kNative.fields[1U].position.x == 0 && kNative.fields[1U].position.y == 0);
    static_assert(kNative.fields[2U].channel == NativeChannel::blue && kNative.fields[2U].bitOffset == 0U && kNative.fields[2U].bitWidth == 5U && kNative.fields[2U].position.x == 0 && kNative.fields[2U].position.y == 0);
};
} // namespace lithe::generated::assets::StorageMenu
