// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/designer/generated_widget_metadata.hpp"
#include "lithe/designer/preview_abi.h"
#include "lithe/designer/preview_protocol.hpp"

#include <algorithm>
#include <array>
#include <bit>
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <limits>
#include <span>
#include <string_view>

#if defined(_WIN32)
#include <windows.h>
#else
#include <cstdio>
#endif

namespace
{

namespace protocol = lithe::designer::protocol;

#if defined(_WIN32)
using Stream = HANDLE;
// Windows hands the frame to the designer through a designer-owned named mapping.
constexpr bool kInlinePixels{false};
#else
using Stream = std::FILE*;
// Other hosts have no named mapping; the frame follows the render response on stdout.
constexpr bool kInlinePixels{true};
#endif

constexpr std::size_t kLengthPrefixBytes{4U};
constexpr std::size_t kRequestHeaderBytes{protocol::kRecordHeaderBytes};
constexpr std::size_t kMaximumResponseBytes{
    protocol::kRecordHeaderBytes + protocol::kResponseFixedPayloadBytes +
    (LITHEVIEW_PREVIEW_MAX_DAMAGE_REGIONS * protocol::kDamageRegionBytes) +
    LITHEVIEW_PREVIEW_MAX_DIAGNOSTIC_BYTES};
constexpr int kExitMalformedHeader{5};
constexpr int kExitProtocolWrite{6};
constexpr int kExitHelloWrite{7};
constexpr int kExitRenderWrite{8};
constexpr int kExitShutdownWrite{9};
constexpr int kExitUnsupportedWrite{10};

struct RecordHeader
{
    std::uint32_t magic{0U};
    std::uint16_t major{0U};
    std::uint16_t minor{0U};
    std::uint32_t messageType{0U};
    std::uint32_t flags{0U};
    std::uint64_t requestId{0U};
    std::uint32_t payloadBytes{0U};
};

struct RenderPayload
{
    std::uint32_t abiVersion{0U};
    std::uint32_t schemaVersion{0U};
    LitheViewPreviewSurface surface{};
    std::uint32_t inputCount{0U};
    std::uint32_t mappingNameBytes{0U};
    std::uint32_t snapshotBytes{0U};
    std::uint64_t pixelCapacity{0U};
};

#if defined(_WIN32)
class SharedPixelMapping final
{
public:
    SharedPixelMapping() noexcept = default;

    ~SharedPixelMapping()
    {
        close();
    }

    SharedPixelMapping(const SharedPixelMapping&) = delete;
    SharedPixelMapping& operator=(const SharedPixelMapping&) = delete;
    SharedPixelMapping(SharedPixelMapping&&) = delete;
    SharedPixelMapping& operator=(SharedPixelMapping&&) = delete;

    bool open(std::span<const wchar_t> name, std::size_t capacity) noexcept
    {
        close();
        if (name.empty() || name.front() == L'\0' || capacity == 0U)
        {
            return false;
        }
        mHandle = OpenFileMappingW(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, name.data());
        if (mHandle == nullptr)
        {
            return false;
        }
        mPixels = static_cast<std::uint8_t*>(
            MapViewOfFile(mHandle, FILE_MAP_READ | FILE_MAP_WRITE, 0U, 0U, capacity));
        if (mPixels == nullptr)
        {
            close();
            return false;
        }
        mCapacity = capacity;
        return true;
    }

    std::uint8_t* data() noexcept
    {
        return mPixels;
    }

    std::size_t capacity() const noexcept
    {
        return mCapacity;
    }

private:
    void close() noexcept
    {
        if (mPixels != nullptr)
        {
            static_cast<void>(UnmapViewOfFile(mPixels));
            mPixels = nullptr;
        }
        if (mHandle != nullptr)
        {
            static_cast<void>(CloseHandle(mHandle));
            mHandle = nullptr;
        }
        mCapacity = 0U;
    }

    HANDLE mHandle{nullptr};
    std::uint8_t* mPixels{nullptr};
    std::size_t mCapacity{0U};
};
#endif

// Fixed worker storage avoids heap and request-dependent stack growth at the protocol boundary.
// NOLINTBEGIN(cppcoreguidelines-avoid-non-const-global-variables)
std::array<std::byte, protocol::kMaximumRecordBytes> gRequestStorage{};
std::array<std::byte, kMaximumResponseBytes> gResponseStorage{};
std::array<LitheViewPreviewInput, LITHEVIEW_PREVIEW_MAX_INPUTS> gInputs{};
std::array<LitheViewPreviewRect, LITHEVIEW_PREVIEW_MAX_DAMAGE_REGIONS> gDamage{};
std::array<char, LITHEVIEW_PREVIEW_MAX_DIAGNOSTIC_BYTES> gDiagnostic{};
#if !defined(_WIN32)
// Untouched pages of this bound cost no memory; a frame uses only the bytes it needs.
static_assert(protocol::kMaximumInlineFrameBytes ==
              std::uint64_t{LITHEVIEW_PREVIEW_MAX_SURFACE_DIMENSION} *
                  LITHEVIEW_PREVIEW_MAX_SURFACE_DIMENSION * 4U);
std::array<std::uint8_t, protocol::kMaximumInlineFrameBytes> gInlinePixels{};
#endif
// NOLINTEND(cppcoreguidelines-avoid-non-const-global-variables)

#if defined(_WIN32)
bool readExact(HANDLE input, std::span<std::byte> destination) noexcept
{
    std::size_t complete = 0U;
    while (complete < destination.size())
    {
        const std::size_t remaining = destination.size() - complete;
        const DWORD chunk = static_cast<DWORD>(
            std::min(remaining, static_cast<std::size_t>(std::numeric_limits<DWORD>::max())));
        DWORD read = 0U;
        if (ReadFile(input, destination.data() + complete, chunk, &read, nullptr) == FALSE ||
            read == 0U)
        {
            return false;
        }
        complete += read;
    }
    return true;
}

bool writeExact(HANDLE output, std::span<const std::byte> source) noexcept
{
    std::size_t complete = 0U;
    while (complete < source.size())
    {
        const std::size_t remaining = source.size() - complete;
        const DWORD chunk = static_cast<DWORD>(
            std::min(remaining, static_cast<std::size_t>(std::numeric_limits<DWORD>::max())));
        DWORD written = 0U;
        if (WriteFile(output, source.data() + complete, chunk, &written, nullptr) == FALSE ||
            written == 0U)
        {
            return false;
        }
        complete += written;
    }
    return true;
}
#else
bool readExact(std::FILE* input, std::span<std::byte> destination) noexcept
{
    return std::fread(destination.data(), 1U, destination.size(), input) == destination.size();
}

bool writeExact(std::FILE* output, std::span<const std::byte> source) noexcept
{
    return std::fwrite(source.data(), 1U, source.size(), output) == source.size() &&
           std::fflush(output) == 0;
}
#endif

std::uint16_t readU16(std::span<const std::byte> bytes, std::size_t offset) noexcept
{
    const auto low = std::to_integer<std::uint16_t>(bytes[offset]);
    const auto high = std::to_integer<std::uint16_t>(bytes[offset + 1U]);
    return low | static_cast<std::uint16_t>(high << protocol::kBitsPerByte);
}

std::uint32_t readU32(std::span<const std::byte> bytes, std::size_t offset) noexcept
{
    std::uint32_t value = 0U;
    for (std::size_t index = 0U; index < sizeof(value); ++index)
    {
        value |= static_cast<std::uint32_t>(std::to_integer<std::uint8_t>(bytes[offset + index]))
                 << (index * protocol::kBitsPerByte);
    }
    return value;
}

std::int32_t readI32(std::span<const std::byte> bytes, std::size_t offset) noexcept
{
    return static_cast<std::int32_t>(readU32(bytes, offset));
}

std::uint64_t readU64(std::span<const std::byte> bytes, std::size_t offset) noexcept
{
    std::uint64_t value = 0U;
    for (std::size_t index = 0U; index < sizeof(value); ++index)
    {
        value |= static_cast<std::uint64_t>(std::to_integer<std::uint8_t>(bytes[offset + index]))
                 << (index * protocol::kBitsPerByte);
    }
    return value;
}

void writeU16(std::span<std::byte> bytes, std::size_t offset, std::uint16_t value) noexcept
{
    for (std::size_t index = 0U; index < sizeof(value); ++index)
    {
        bytes[offset + index] =
            std::byte{static_cast<std::uint8_t>(value >> (index * protocol::kBitsPerByte))};
    }
}

void writeU32(std::span<std::byte> bytes, std::size_t offset, std::uint32_t value) noexcept
{
    for (std::size_t index = 0U; index < sizeof(value); ++index)
    {
        bytes[offset + index] =
            std::byte{static_cast<std::uint8_t>(value >> (index * protocol::kBitsPerByte))};
    }
}

void writeI32(std::span<std::byte> bytes, std::size_t offset, std::int32_t value) noexcept
{
    writeU32(bytes, offset, static_cast<std::uint32_t>(value));
}

void writeU64(std::span<std::byte> bytes, std::size_t offset, std::uint64_t value) noexcept
{
    for (std::size_t index = 0U; index < sizeof(value); ++index)
    {
        bytes[offset + index] =
            std::byte{static_cast<std::uint8_t>(value >> (index * protocol::kBitsPerByte))};
    }
}

bool parseRecordHeader(std::span<const std::byte> record, RecordHeader& header) noexcept
{
    if (record.size() < kRequestHeaderBytes)
    {
        return false;
    }
    header.magic = readU32(record, protocol::record_offset::kMagic);
    header.major = readU16(record, protocol::record_offset::kMajor);
    header.minor = readU16(record, protocol::record_offset::kMinor);
    header.messageType = readU32(record, protocol::record_offset::kMessageType);
    header.flags = readU32(record, protocol::record_offset::kFlags);
    header.requestId = readU64(record, protocol::record_offset::kRequestId);
    header.payloadBytes = readU32(record, protocol::record_offset::kPayloadBytes);
    return header.magic == protocol::kMagic &&
           header.payloadBytes == record.size() - kRequestHeaderBytes;
}

bool parseRenderPayload(std::span<const std::byte> payload, RenderPayload& render) noexcept
{
    if (payload.size() < protocol::kRenderFixedPayloadBytes)
    {
        return false;
    }
    render.abiVersion = readU32(payload, protocol::render_offset::kAbiVersion);
    render.schemaVersion = readU32(payload, protocol::render_offset::kSchemaVersion);
    render.surface.struct_size = sizeof(LitheViewPreviewSurface);
    render.surface.pixel_format = readU32(payload, protocol::render_offset::kPixelFormat);
    render.surface.shape = readU32(payload, protocol::render_offset::kShape);
    render.surface.quantization = readU32(payload, protocol::render_offset::kQuantization);
    render.surface.width = readI32(payload, protocol::render_offset::kWidth);
    render.surface.height = readI32(payload, protocol::render_offset::kHeight);
    render.surface.stride_bytes = readU32(payload, protocol::render_offset::kStrideBytes);
    render.surface.reserved = 0U;
    render.inputCount = readU32(payload, protocol::render_offset::kInputCount);
    render.mappingNameBytes = readU32(payload, protocol::render_offset::kMappingNameBytes);
    render.snapshotBytes = readU32(payload, protocol::render_offset::kSnapshotBytes);
    render.pixelCapacity = readU64(payload, protocol::render_offset::kPixelCapacity);
    if (render.inputCount > LITHEVIEW_PREVIEW_MAX_INPUTS ||
        (render.mappingNameBytes == 0U && !kInlinePixels) ||
        render.mappingNameBytes > protocol::kMaximumMappingNameBytes ||
        render.snapshotBytes == 0U || render.snapshotBytes > LITHEVIEW_PREVIEW_MAX_SNAPSHOT_BYTES)
    {
        return false;
    }
    const std::size_t variableBytes =
        static_cast<std::size_t>(render.mappingNameBytes) +
        static_cast<std::size_t>(render.snapshotBytes) +
        (static_cast<std::size_t>(render.inputCount) * protocol::kInputBytes);
    return variableBytes == payload.size() - protocol::kRenderFixedPayloadBytes;
}

bool decodeInputs(std::span<const std::byte> bytes, std::uint32_t count) noexcept
{
    if (bytes.size() != static_cast<std::size_t>(count) * protocol::kInputBytes)
    {
        return false;
    }
    for (std::size_t index = 0U; index < count; ++index)
    {
        const std::size_t offset = index * protocol::kInputBytes;
        gInputs[index] = {sizeof(LitheViewPreviewInput),
                          readU32(bytes, offset + protocol::input_offset::kKind),
                          readI32(bytes, offset + protocol::input_offset::kValue),
                          readI32(bytes, offset + protocol::input_offset::kX),
                          readI32(bytes, offset + protocol::input_offset::kY),
                          readU32(bytes, offset + protocol::input_offset::kTimestampMilliseconds),
                          readU32(bytes, offset + protocol::input_offset::kContactId),
                          0U};
        if (readU32(bytes, offset + protocol::input_offset::kReserved0) != 0U ||
            readU32(bytes, offset + protocol::input_offset::kReserved1) != 0U)
        {
            return false;
        }
    }
    return true;
}

#if defined(_WIN32)
bool utf8MappingName(std::span<const std::byte> encoded,
                     std::array<wchar_t, protocol::kMaximumMappingNameBytes + 1U>& wide,
                     std::wstring_view& result) noexcept
{
    const std::string_view name{std::bit_cast<const char*>(encoded.data()), encoded.size()};
    if (!name.starts_with(protocol::kMappingNamePrefix) ||
        name.find_first_of("/:") != std::string_view::npos)
    {
        return false;
    }
    const int converted = MultiByteToWideChar(CP_UTF8,
                                              MB_ERR_INVALID_CHARS,
                                              name.data(),
                                              static_cast<int>(name.size()),
                                              wide.data(),
                                              static_cast<int>(wide.size() - 1U));
    if (converted <= 0)
    {
        return false;
    }
    wide[static_cast<std::size_t>(converted)] = L'\0';
    result = {wide.data(), static_cast<std::size_t>(converted)};
    return true;
}
#endif

std::size_t
beginResponse(std::uint32_t messageType, std::uint64_t requestId, std::size_t payloadBytes) noexcept
{
    const std::size_t recordBytes = protocol::kRecordHeaderBytes + payloadBytes;
    std::span<std::byte> output{gResponseStorage};
    writeU32(output, 0U, static_cast<std::uint32_t>(recordBytes));
    writeU32(output, kLengthPrefixBytes + protocol::record_offset::kMagic, protocol::kMagic);
    writeU16(output, kLengthPrefixBytes + protocol::record_offset::kMajor, protocol::kMajorVersion);
    writeU16(output, kLengthPrefixBytes + protocol::record_offset::kMinor, protocol::kMinorVersion);
    writeU32(output, kLengthPrefixBytes + protocol::record_offset::kMessageType, messageType);
    writeU32(output, kLengthPrefixBytes + protocol::record_offset::kFlags, 0U);
    writeU64(output, kLengthPrefixBytes + protocol::record_offset::kRequestId, requestId);
    writeU32(output,
             kLengthPrefixBytes + protocol::record_offset::kPayloadBytes,
             static_cast<std::uint32_t>(payloadBytes));
    return kLengthPrefixBytes + protocol::kRecordHeaderBytes;
}

bool writeWorkerFailure(Stream output,
                        std::uint32_t requestType,
                        std::uint64_t requestId,
                        protocol::WorkerStatus status,
                        std::string_view diagnostic) noexcept
{
    const std::size_t diagnosticBytes = std::min(
        diagnostic.size(), static_cast<std::size_t>(LITHEVIEW_PREVIEW_MAX_DIAGNOSTIC_BYTES));
    const std::size_t payloadBytes = protocol::kResponseFixedPayloadBytes + diagnosticBytes;
    const std::size_t payload =
        beginResponse(requestType | protocol::kResponseBit, requestId, payloadBytes);
    std::span<std::byte> outputBytes{gResponseStorage};
    writeU32(outputBytes,
             payload + protocol::response_offset::kWorkerStatus,
             static_cast<std::uint32_t>(status));
    writeU32(outputBytes, payload + protocol::response_offset::kPreviewStatus, 0U);
    writeU32(outputBytes, payload + protocol::response_offset::kFlags, 0U);
    writeU32(outputBytes, payload + protocol::response_offset::kDamageCount, 0U);
    writeU64(outputBytes, payload + protocol::response_offset::kFrameBytes, 0U);
    writeU64(outputBytes, payload + protocol::response_offset::kRenderMicroseconds, 0U);
    writeU32(outputBytes, payload + protocol::response_offset::kNextWakeupMilliseconds, 0U);
    writeU32(outputBytes, payload + protocol::response_offset::kAcceptedInputCount, 0U);
    writeU32(outputBytes,
             payload + protocol::response_offset::kDiagnosticBytes,
             static_cast<std::uint32_t>(diagnosticBytes));
    writeU32(outputBytes, payload + protocol::response_offset::kDiagnosticCode, 0U);
    std::memcpy(outputBytes.data() + payload + protocol::kResponseFixedPayloadBytes,
                diagnostic.data(),
                diagnosticBytes);
    return writeExact(output,
                      std::span<const std::byte>{outputBytes.data(), payload + payloadBytes});
}

bool writeHello(Stream output, std::uint64_t requestId) noexcept
{
    const std::size_t payload = beginResponse(protocol::responseType(protocol::MessageType::hello),
                                              requestId,
                                              protocol::kHelloPayloadBytes);
    std::span<std::byte> outputBytes{gResponseStorage};
    writeU32(outputBytes,
             payload + protocol::hello_offset::kWorkerStatus,
             static_cast<std::uint32_t>(protocol::WorkerStatus::ok));
    writeU16(
        outputBytes, payload + protocol::hello_offset::kProtocolMajor, protocol::kMajorVersion);
    writeU16(
        outputBytes, payload + protocol::hello_offset::kProtocolMinor, protocol::kMinorVersion);
    writeU32(
        outputBytes, payload + protocol::hello_offset::kAbiVersion, LITHEVIEW_PREVIEW_ABI_VERSION);
    writeU32(outputBytes,
             payload + protocol::hello_offset::kSchemaVersion,
             LITHEVIEW_PREVIEW_SCHEMA_VERSION);
    writeU32(outputBytes,
             payload + protocol::hello_offset::kMaximumControlPayloadBytes,
             protocol::kMaximumControlPayloadBytes);
    writeU32(outputBytes,
             payload + protocol::hello_offset::kMaximumSnapshotBytes,
             LITHEVIEW_PREVIEW_MAX_SNAPSHOT_BYTES);
    writeU32(outputBytes,
             payload + protocol::hello_offset::kMaximumSurfaceDimension,
             LITHEVIEW_PREVIEW_MAX_SURFACE_DIMENSION);
    writeU32(outputBytes,
             payload + protocol::hello_offset::kCapabilities,
             (kInlinePixels ? protocol::kCapabilityInlinePixels : 0U) |
                 protocol::kCapabilityNativePackedSurfaces);
    static_assert(lithe::designer::kWidgetCatalogSha256.size() ==
                  protocol::kCatalogSha256Characters);
    std::memcpy(outputBytes.data() + payload + protocol::hello_offset::kCatalogSha256,
                lithe::designer::kWidgetCatalogSha256.data(),
                lithe::designer::kWidgetCatalogSha256.size());
    return writeExact(
        output,
        std::span<const std::byte>{outputBytes.data(), payload + protocol::kHelloPayloadBytes});
}

// An inline frame, when present, follows the diagnostic and is counted in the record length.
bool writeRenderResponse(Stream output,
                         std::uint64_t requestId,
                         const LitheViewPreviewResult& result,
                         std::uint64_t renderMicroseconds,
                         std::span<const std::uint8_t> inlineFrame) noexcept
{
    const std::size_t diagnosticBytes = std::char_traits<char>::length(gDiagnostic.data());
    const std::size_t damageBytes = result.damage_count * protocol::kDamageRegionBytes;
    const std::size_t payloadBytes =
        protocol::kResponseFixedPayloadBytes + damageBytes + diagnosticBytes + inlineFrame.size();
    const std::size_t payload = beginResponse(
        protocol::responseType(protocol::MessageType::render), requestId, payloadBytes);
    std::span<std::byte> outputBytes{gResponseStorage};
    const protocol::WorkerStatus workerStatus = result.status == LITHEVIEW_PREVIEW_STATUS_OK
                                                    ? protocol::WorkerStatus::ok
                                                    : protocol::WorkerStatus::previewFailed;
    writeU32(outputBytes,
             payload + protocol::response_offset::kWorkerStatus,
             static_cast<std::uint32_t>(workerStatus));
    writeU32(outputBytes, payload + protocol::response_offset::kPreviewStatus, result.status);
    writeU32(outputBytes, payload + protocol::response_offset::kFlags, result.flags);
    writeU32(outputBytes,
             payload + protocol::response_offset::kDamageCount,
             static_cast<std::uint32_t>(result.damage_count));
    writeU64(outputBytes, payload + protocol::response_offset::kFrameBytes, result.frame_bytes);
    writeU64(
        outputBytes, payload + protocol::response_offset::kRenderMicroseconds, renderMicroseconds);
    writeU32(outputBytes,
             payload + protocol::response_offset::kNextWakeupMilliseconds,
             result.next_wakeup_ms);
    writeU32(outputBytes,
             payload + protocol::response_offset::kAcceptedInputCount,
             result.accepted_input_count);
    writeU32(outputBytes,
             payload + protocol::response_offset::kDiagnosticBytes,
             static_cast<std::uint32_t>(diagnosticBytes));
    writeU32(
        outputBytes, payload + protocol::response_offset::kDiagnosticCode, result.diagnostic_code);
    std::size_t cursor = payload + protocol::kResponseFixedPayloadBytes;
    for (std::size_t index = 0U; index < result.damage_count; ++index)
    {
        writeI32(outputBytes, cursor + protocol::damage_offset::kX, gDamage[index].x);
        writeI32(outputBytes, cursor + protocol::damage_offset::kY, gDamage[index].y);
        writeI32(outputBytes, cursor + protocol::damage_offset::kWidth, gDamage[index].width);
        writeI32(outputBytes, cursor + protocol::damage_offset::kHeight, gDamage[index].height);
        cursor += protocol::kDamageRegionBytes;
    }
    std::memcpy(outputBytes.data() + cursor, gDiagnostic.data(), diagnosticBytes);
    cursor += diagnosticBytes;
    return writeExact(output, std::span<const std::byte>{outputBytes.data(), cursor}) &&
           writeExact(output, std::as_bytes(inlineFrame));
}

bool handleRender(Stream output,
                  const RecordHeader& header,
                  std::span<const std::byte> payload) noexcept
{
    RenderPayload render{};
    if (!parseRenderPayload(payload, render) ||
        render.pixelCapacity > std::numeric_limits<std::size_t>::max())
    {
        return writeWorkerFailure(output,
                                  header.messageType,
                                  header.requestId,
                                  protocol::WorkerStatus::malformedRecord,
                                  "LVPW0001: malformed render payload");
    }
    std::size_t cursor = protocol::kRenderFixedPayloadBytes;
    const std::span<const std::byte> encodedName = payload.subspan(cursor, render.mappingNameBytes);
    cursor += render.mappingNameBytes;
    const std::span<const std::byte> snapshot = payload.subspan(cursor, render.snapshotBytes);
    cursor += render.snapshotBytes;
    const std::span<const std::byte> inputBytes = payload.subspan(cursor);
    if (!decodeInputs(inputBytes, render.inputCount))
    {
        return writeWorkerFailure(output,
                                  header.messageType,
                                  header.requestId,
                                  protocol::WorkerStatus::malformedRecord,
                                  "LVPW0002: malformed input event array");
    }

    std::uint8_t* pixels = nullptr;
    std::size_t pixelCapacity = 0U;
#if defined(_WIN32)
    std::array<wchar_t, protocol::kMaximumMappingNameBytes + 1U> wideName{};
    std::wstring_view mappingName{};
    if (!utf8MappingName(encodedName, wideName, mappingName))
    {
        return writeWorkerFailure(output,
                                  header.messageType,
                                  header.requestId,
                                  protocol::WorkerStatus::mappingRejected,
                                  "LVPW0003: shared-memory name is outside the preview namespace");
    }
    SharedPixelMapping mapping{};
    const std::span<const wchar_t> terminatedMappingName{wideName.data(), mappingName.size() + 1U};
    if (!mapping.open(terminatedMappingName, static_cast<std::size_t>(render.pixelCapacity)))
    {
        return writeWorkerFailure(output,
                                  header.messageType,
                                  header.requestId,
                                  protocol::WorkerStatus::mappingOpenFailed,
                                  "LVPW0004: designer-owned pixel mapping could not be opened");
    }
    pixels = mapping.data();
    pixelCapacity = mapping.capacity();
#else
    if (!encodedName.empty())
    {
        return writeWorkerFailure(
            output,
            header.messageType,
            header.requestId,
            protocol::WorkerStatus::mappingRejected,
            "LVPW0007: shared-memory pixels need Windows; request inline pixels");
    }
    if (render.pixelCapacity > gInlinePixels.size())
    {
        return writeWorkerFailure(output,
                                  header.messageType,
                                  header.requestId,
                                  protocol::WorkerStatus::mappingRejected,
                                  "LVPW0008: inline frame exceeds the worker limit");
    }
    // Start from zeroed pixels, as a fresh designer-owned mapping does on Windows.
    pixelCapacity = render.pixelCapacity;
    std::fill_n(gInlinePixels.begin(), pixelCapacity, std::uint8_t{0U});
    pixels = gInlinePixels.data();
#endif

    std::fill(gDamage.begin(), gDamage.end(), LitheViewPreviewRect{});
    std::fill(gDiagnostic.begin(), gDiagnostic.end(), '\0');
    LitheViewPreviewRequest request{sizeof(LitheViewPreviewRequest),
                                    render.abiVersion,
                                    render.schemaVersion,
                                    header.flags,
                                    render.surface,
                                    std::bit_cast<const std::uint8_t*>(snapshot.data()),
                                    snapshot.size(),
                                    gInputs.data(),
                                    render.inputCount,
                                    pixels,
                                    pixelCapacity,
                                    gDamage.data(),
                                    gDamage.size(),
                                    gDiagnostic.data(),
                                    gDiagnostic.size()};
    LitheViewPreviewResult result{};
    result.struct_size = sizeof(result);
    const auto started = std::chrono::steady_clock::now();
    static_cast<void>(litheview_preview_render(&request, &result));
    const auto elapsed = std::chrono::steady_clock::now() - started;
    const auto microseconds =
        std::chrono::duration_cast<std::chrono::microseconds>(elapsed).count();
    const std::uint64_t boundedMicroseconds =
        microseconds < 0 ? 0U : static_cast<std::uint64_t>(microseconds);
    // A rejected or failed render returns no frame. The ABI never reports more than the capacity.
    std::span<const std::uint8_t> inlineFrame{};
    if (kInlinePixels && result.status == LITHEVIEW_PREVIEW_STATUS_OK &&
        result.frame_bytes <= pixelCapacity)
    {
        inlineFrame = {pixels, result.frame_bytes};
    }
    return writeRenderResponse(output, header.requestId, result, boundedMicroseconds, inlineFrame);
}

} // namespace

// The loop is the complete fail-closed worker state machine and keeps dispatch order visible.
int main() // NOLINT(readability-function-cognitive-complexity)
{
#if defined(_WIN32)
    HANDLE input = GetStdHandle(STD_INPUT_HANDLE);
    HANDLE output = GetStdHandle(STD_OUTPUT_HANDLE);
    if (input == INVALID_HANDLE_VALUE || output == INVALID_HANDLE_VALUE || input == nullptr ||
        output == nullptr)
    {
        return 2;
    }
#else
    std::FILE* const input = stdin;
    std::FILE* const output = stdout;
#endif

    for (;;)
    {
        std::array<std::byte, kLengthPrefixBytes> encodedLength{};
        if (!readExact(input, encodedLength))
        {
            return 0;
        }
        const std::uint32_t recordBytes = readU32(encodedLength, 0U);
        if (recordBytes < protocol::kRecordHeaderBytes ||
            recordBytes > protocol::kMaximumRecordBytes)
        {
            return 3;
        }
        std::span<std::byte> record{gRequestStorage.data(), recordBytes};
        if (!readExact(input, record))
        {
            return 4;
        }

        RecordHeader header{};
        if (!parseRecordHeader(record, header))
        {
            return kExitMalformedHeader;
        }
        if (header.major != protocol::kMajorVersion || header.minor > protocol::kMinorVersion)
        {
            if (!writeWorkerFailure(output,
                                    header.messageType,
                                    header.requestId,
                                    protocol::WorkerStatus::protocolMismatch,
                                    "LVPW0005: worker protocol version mismatch"))
            {
                return kExitProtocolWrite;
            }
            continue;
        }

        const std::span<const std::byte> payload = record.subspan(protocol::kRecordHeaderBytes);
        switch (static_cast<protocol::MessageType>(header.messageType))
        {
        case protocol::MessageType::hello:
            if (!payload.empty() || !writeHello(output, header.requestId))
            {
                return kExitHelloWrite;
            }
            break;
        case protocol::MessageType::render:
            if (!handleRender(output, header, payload))
            {
                return kExitRenderWrite;
            }
            break;
        case protocol::MessageType::shutdown:
            if (!payload.empty() ||
                !writeWorkerFailure(
                    output, header.messageType, header.requestId, protocol::WorkerStatus::ok, {}))
            {
                return kExitShutdownWrite;
            }
            return 0;
        default:
            if (!writeWorkerFailure(output,
                                    header.messageType,
                                    header.requestId,
                                    protocol::WorkerStatus::unsupportedMessage,
                                    "LVPW0006: unsupported worker message type"))
            {
                return kExitUnsupportedWrite;
            }
            break;
        }
    }
}
