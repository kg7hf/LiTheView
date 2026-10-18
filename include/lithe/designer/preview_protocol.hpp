// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <cstddef>
#include <cstdint>
#include <string_view>
#include <utility>

namespace lithe::designer::protocol
{

inline constexpr std::uint32_t kMagic{0x3157504cU}; // "LPW1" in little-endian byte order.
inline constexpr std::uint16_t kMajorVersion{1U};
inline constexpr std::uint16_t kMinorVersion{0U};
inline constexpr std::uint32_t kRecordHeaderBytes{28U};
inline constexpr std::uint32_t kRenderFixedPayloadBytes{52U};
inline constexpr std::uint32_t kInputBytes{32U};
inline constexpr std::uint32_t kResponseFixedPayloadBytes{48U};
inline constexpr std::uint32_t kHelloPayloadBytes{96U};
inline constexpr std::uint32_t kMaximumMappingNameBytes{128U};
inline constexpr std::uint32_t kMaximumControlPayloadBytes{1048576U + 4096U};
inline constexpr std::uint32_t kMaximumRecordBytes{kRecordHeaderBytes +
                                                   kMaximumControlPayloadBytes};
inline constexpr std::string_view kMappingNamePrefix{"Local\\LitheView.Preview."};
// Hello capability: a render request with an empty mapping name receives its pixels inline,
// after the diagnostic, instead of through a designer-owned shared mapping. Workers on hosts
// without named shared memory set it. Windows raster requests use shared memory.
inline constexpr std::uint32_t kCapabilityInlinePixels{0x00000001U};
// Hello capability: render requests may name the native packed pixel formats. The designer
// never sends them to a worker that does not report this bit.
inline constexpr std::uint32_t kCapabilitySegments{0x00000004U};
inline constexpr std::uint32_t kCapabilityNativePackedSurfaces{0x00000002U};
// The largest inline frame: the maximum surface at four bytes per pixel.
inline constexpr std::uint64_t kMaximumInlineFrameBytes{std::uint64_t{4096U} * 4096U * 4U};
inline constexpr std::uint32_t kResponseBit{0x80000000U};
inline constexpr std::size_t kBitsPerByte{8U};
inline constexpr std::size_t kDamageRegionBytes{16U};
inline constexpr std::size_t kCatalogSha256Characters{64U};

namespace record_offset
{
inline constexpr std::size_t kMagic{0U};
inline constexpr std::size_t kMajor{4U};
inline constexpr std::size_t kMinor{6U};
inline constexpr std::size_t kMessageType{8U};
inline constexpr std::size_t kFlags{12U};
inline constexpr std::size_t kRequestId{16U};
inline constexpr std::size_t kPayloadBytes{24U};
} // namespace record_offset

namespace render_offset
{
inline constexpr std::size_t kAbiVersion{0U};
inline constexpr std::size_t kSchemaVersion{4U};
inline constexpr std::size_t kPixelFormat{8U};
inline constexpr std::size_t kShape{12U};
inline constexpr std::size_t kQuantization{16U};
inline constexpr std::size_t kWidth{20U};
inline constexpr std::size_t kHeight{24U};
inline constexpr std::size_t kStrideBytes{28U};
inline constexpr std::size_t kInputCount{32U};
inline constexpr std::size_t kMappingNameBytes{36U};
inline constexpr std::size_t kSnapshotBytes{40U};
inline constexpr std::size_t kPixelCapacity{44U};
} // namespace render_offset

namespace input_offset
{
inline constexpr std::size_t kKind{0U};
inline constexpr std::size_t kValue{4U};
inline constexpr std::size_t kX{8U};
inline constexpr std::size_t kY{12U};
inline constexpr std::size_t kTimestampMilliseconds{16U};
inline constexpr std::size_t kContactId{20U};
inline constexpr std::size_t kReserved0{24U};
inline constexpr std::size_t kReserved1{28U};
} // namespace input_offset

namespace response_offset
{
inline constexpr std::size_t kWorkerStatus{0U};
inline constexpr std::size_t kPreviewStatus{4U};
inline constexpr std::size_t kFlags{8U};
inline constexpr std::size_t kDamageCount{12U};
inline constexpr std::size_t kFrameBytes{16U};
inline constexpr std::size_t kRenderMicroseconds{24U};
inline constexpr std::size_t kNextWakeupMilliseconds{32U};
inline constexpr std::size_t kAcceptedInputCount{36U};
inline constexpr std::size_t kDiagnosticBytes{40U};
inline constexpr std::size_t kDiagnosticCode{44U};
} // namespace response_offset

namespace segment_response_offset
{
inline constexpr std::size_t kStatus{0U};
inline constexpr std::size_t kWidth{4U};
inline constexpr std::size_t kHeight{8U};
inline constexpr std::size_t kSegmentBytes{12U};
inline constexpr std::size_t kPixelBytes{16U};
inline constexpr std::size_t kPayloadBytes{20U};
} // namespace segment_response_offset

namespace hello_offset
{
inline constexpr std::size_t kWorkerStatus{0U};
inline constexpr std::size_t kProtocolMajor{4U};
inline constexpr std::size_t kProtocolMinor{6U};
inline constexpr std::size_t kAbiVersion{8U};
inline constexpr std::size_t kSchemaVersion{12U};
inline constexpr std::size_t kMaximumControlPayloadBytes{16U};
inline constexpr std::size_t kMaximumSnapshotBytes{20U};
inline constexpr std::size_t kMaximumSurfaceDimension{24U};
inline constexpr std::size_t kCapabilities{28U};
inline constexpr std::size_t kCatalogSha256{32U};
} // namespace hello_offset

namespace damage_offset
{
inline constexpr std::size_t kX{0U};
inline constexpr std::size_t kY{4U};
inline constexpr std::size_t kWidth{8U};
inline constexpr std::size_t kHeight{12U};
} // namespace damage_offset

// These values are serialized as fixed-width 32-bit protocol fields.
enum class MessageType : std::uint32_t // NOLINT(performance-enum-size)
{
    hello = 1U,
    render = 2U,
    shutdown = 3U,
    segments = 4U
};

constexpr std::uint32_t responseType(MessageType type) noexcept
{
    return std::to_underlying(type) | kResponseBit;
}

enum class WorkerStatus : std::uint32_t // NOLINT(performance-enum-size)
{
    ok = 0U,
    malformedRecord = 1U,
    protocolMismatch = 2U,
    unsupportedMessage = 3U,
    mappingRejected = 4U,
    mappingOpenFailed = 5U,
    previewFailed = 6U,
    internalError = 7U
};

} // namespace lithe::designer::protocol
