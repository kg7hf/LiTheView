// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/application.hpp"
#include "lithe/designer/generated_widget_metadata.hpp"
#include "lithe/designer/preview_abi.h"
#include "lithe/layout.hpp"
#include "lithe/pixel_format.hpp"
#include "lithe/storage_layout.hpp"
#include "lithe/tiled_renderer.hpp"
#include "lithe/xml_layout.hpp"

#include <algorithm>
#include <array>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <expected>
#include <limits>
#include <span>
#include <string_view>

namespace
{

static_assert(lithe::designer::kWidgetSchemaVersion == LITHEVIEW_PREVIEW_SCHEMA_VERSION);

constexpr std::size_t kAnimationFrameCapacity{256U};
constexpr std::uint32_t kDiagnosticInvalidArgument{1001U};
constexpr std::uint32_t kDiagnosticAbiMismatch{1002U};
constexpr std::uint32_t kDiagnosticSchemaMismatch{1003U};
constexpr std::uint32_t kDiagnosticSizeLimit{1004U};
constexpr std::uint32_t kDiagnosticUnsupportedFormat{1005U};
constexpr std::uint32_t kDiagnosticInvalidSnapshot{1006U};
constexpr std::uint32_t kDiagnosticMissingResource{1007U};
constexpr std::uint32_t kDiagnosticInvalidInput{1008U};
constexpr std::uint32_t kDiagnosticOutputTooSmall{1009U};
constexpr std::uint32_t kDiagnosticInternalError{1010U};
constexpr std::uint32_t kMaximumChannelValue{255U};
constexpr std::uint32_t kRed2BitMaximum{3U};
constexpr std::uint32_t kRed2BitStep{kMaximumChannelValue / kRed2BitMaximum};
constexpr std::uint32_t kRedYellowChannelThreshold{96U};
constexpr std::uint8_t kMixedYellowGreen{128U};
constexpr std::int32_t kPreviewTileHeight{8};
constexpr std::size_t kMaximumTileWorkspaceBytes{LITHEVIEW_PREVIEW_MAX_SURFACE_DIMENSION *
                                                 static_cast<std::size_t>(kPreviewTileHeight) * 4U};
constexpr std::size_t kPreviewBindingCapacity{64U};
constexpr std::size_t kPreviewBindingBytes{256U};

class PreviewResources final : public lithe::ResourceProvider
{
public:
    const lithe::Bitmap* bitmap([[maybe_unused]] std::string_view name) const noexcept override
    {
        return nullptr;
    }

    lithe::MutableTextBuffer textBuffer(std::string_view name) const noexcept override
    {
        if (name.empty())
        {
            return {};
        }
        for (std::size_t index = 0U; index < mCount; ++index)
        {
            if (mBindings[index].name == name)
            {
                return mBindings[index].text;
            }
        }
        if (mCount == mBindings.size())
        {
            return {};
        }
        Binding& binding = mBindings[mCount++];
        binding.name = name;
        return binding.text;
    }

    lithe::MutableTextBuffer textEditBuffer(std::string_view name) const noexcept override
    {
        for (std::size_t index = 0U; index < mCount; ++index)
        {
            if (mBindings[index].name == name)
            {
                return mBindings[index].edit;
            }
        }
        return {};
    }

private:
    struct Binding
    {
        std::string_view name;
        std::array<char, kPreviewBindingBytes> text{};
        std::array<char, kPreviewBindingBytes> edit{};
    };

    mutable std::array<Binding, kPreviewBindingCapacity> mBindings{};
    mutable std::size_t mCount{0U};
};

class PreviewDisplay final : public lithe::Display
{
public:
    explicit PreviewDisplay(lithe::Surface surface) noexcept : mSurface(surface)
    {
    }

    lithe::Surface surface() noexcept override
    {
        return mSurface;
    }

    void present([[maybe_unused]] std::span<const lithe::Rect> regions) noexcept override
    {
    }

private:
    lithe::Surface mSurface;
};

class PreviewTileSink final : public lithe::TileSink
{
public:
    PreviewTileSink(std::span<std::byte> destination,
                    std::size_t strideBytes,
                    lithe::PixelFormat format) noexcept
        : mDestination(destination), mStrideBytes(strideBytes), mFormat(format)
    {
    }

    bool beginFrame(lithe::Size frameSize, lithe::PixelFormat format) noexcept override
    {
        mComplete = false;
        return frameSize.width > 0 && frameSize.height > 0 && format == mFormat;
    }

    // Tiles start on storage-block boundaries. Rows are block rows, so the same copy serves
    // byte-addressed RGB and native packed storage without any intermediate conversion.
    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        const auto tile = pixels.layout();
        const lithe::StorageBlock block = lithe::storageBlock(mFormat);
        if (!tile || pixels.format != mFormat || destination.x < 0 || destination.y < 0 ||
            destination.width != pixels.width || destination.height != pixels.height ||
            destination.x % block.width != 0 || destination.y % block.height != 0)
        {
            return false;
        }
        const std::size_t firstRow = lithe::detail::nonnegativeSize(destination.y / block.height);
        std::size_t columnOffset = 0U;
        if (!lithe::detail::checkedMultiply(
                lithe::detail::nonnegativeSize(destination.x / block.width),
                block.bytes,
                columnOffset))
        {
            return false;
        }
        for (std::size_t row = 0U; row < tile->rowCount; ++row)
        {
            std::size_t sourceOffset = 0U;
            std::size_t destinationOffset = 0U;
            if (!lithe::detail::checkedMultiply(row, pixels.strideBytes, sourceOffset) ||
                !lithe::detail::checkedMultiply(firstRow + row, mStrideBytes, destinationOffset) ||
                columnOffset > mDestination.size() ||
                destinationOffset > mDestination.size() - columnOffset)
            {
                return false;
            }
            destinationOffset += columnOffset;
            if (sourceOffset > pixels.data.size() ||
                tile->rowBytes > pixels.data.size() - sourceOffset ||
                tile->rowBytes > mDestination.size() - destinationOffset)
            {
                return false;
            }
            std::ranges::copy(pixels.data.subspan(sourceOffset, tile->rowBytes),
                              mDestination.subspan(destinationOffset).begin());
        }
        return true;
    }

    bool endFrame(bool complete) noexcept override
    {
        mComplete = complete;
        return true;
    }

    bool complete() const noexcept
    {
        return mComplete;
    }

private:
    std::span<std::byte> mDestination;
    std::size_t mStrideBytes{0U};
    lithe::PixelFormat mFormat{lithe::PixelFormat::rgb565};
    bool mComplete{false};
};

// Fixed thread-local workspace keeps tiled preview off both the stack and heap.
thread_local std::array<std::byte, kMaximumTileWorkspaceBytes>
    gTileWorkspace{}; // NOLINT(cppcoreguidelines-avoid-non-const-global-variables)

void initializeResult(LitheViewPreviewResult& result) noexcept
{
    result.status = LITHEVIEW_PREVIEW_STATUS_INTERNAL_ERROR;
    result.diagnostic_code = kDiagnosticInternalError;
    result.flags = 0U;
    result.frame_bytes = 0U;
    result.damage_count = 0U;
    result.next_wakeup_ms = 0U;
    result.accepted_input_count = 0U;
}

void writeDiagnostic(const LitheViewPreviewRequest& request, std::string_view message) noexcept
{
    if (request.diagnostic == nullptr || request.diagnostic_capacity == 0U)
    {
        return;
    }
    std::span<char> destination{request.diagnostic, request.diagnostic_capacity};
    const std::size_t count = std::min(message.size(), request.diagnostic_capacity - 1U);
    std::memcpy(destination.data(), message.data(), count);
    destination[count] = '\0';
}

LitheViewPreviewStatus fail(const LitheViewPreviewRequest& request,
                            LitheViewPreviewResult& result,
                            LitheViewPreviewStatus status,
                            std::uint32_t diagnosticCode,
                            std::string_view message) noexcept
{
    result.status = status;
    result.diagnostic_code = diagnosticCode;
    writeDiagnostic(request, message);
    return status;
}

// Logical extents follow the core storage rules: stride advances one storage-block row and a
// packed extent must contain whole blocks. Byte-addressed RGB formats have one-pixel blocks.
std::expected<std::size_t, lithe::StorageError> frameSize(const LitheViewPreviewSurface& surface,
                                                          lithe::PixelFormat format) noexcept
{
    if (surface.width <= 0 || surface.height <= 0 ||
        lithe::detail::nonnegativeSize(surface.width) > LITHEVIEW_PREVIEW_MAX_SURFACE_DIMENSION ||
        lithe::detail::nonnegativeSize(surface.height) > LITHEVIEW_PREVIEW_MAX_SURFACE_DIMENSION)
    {
        return std::unexpected{lithe::StorageError::invalidExtent};
    }
    const auto layout =
        lithe::storageLayout(format, {surface.width, surface.height}, surface.stride_bytes);
    if (!layout)
    {
        return std::unexpected{layout.error()};
    }
    std::size_t frameBytes = 0U;
    if (!lithe::detail::checkedMultiply(layout->rowCount, layout->strideBytes, frameBytes))
    {
        return std::unexpected{lithe::StorageError::arithmeticOverflow};
    }
    return frameBytes;
}

bool nativePacked(lithe::PixelFormat format) noexcept
{
    return format == lithe::PixelFormat::r2Packed2x2 || format == lithe::PixelFormat::rrrr2222 ||
           format == lithe::PixelFormat::ryry2222;
}

bool translateFormat(std::uint32_t value,
                     lithe::PixelFormat& format,
                     std::size_t& pixelBytes) noexcept
{
    switch (value)
    {
    case LITHEVIEW_PREVIEW_PIXEL_RGB565:
        format = lithe::PixelFormat::rgb565;
        break;
    case LITHEVIEW_PREVIEW_PIXEL_RGB888:
        format = lithe::PixelFormat::rgb888;
        break;
    case LITHEVIEW_PREVIEW_PIXEL_XRGB8888:
        format = lithe::PixelFormat::xrgb8888;
        break;
    case LITHEVIEW_PREVIEW_PIXEL_ARGB8888:
        format = lithe::PixelFormat::argb8888;
        break;
    case LITHEVIEW_PREVIEW_PIXEL_R2_PACKED_2X2:
        format = lithe::PixelFormat::r2Packed2x2;
        break;
    case LITHEVIEW_PREVIEW_PIXEL_RRRR2222:
        format = lithe::PixelFormat::rrrr2222;
        break;
    case LITHEVIEW_PREVIEW_PIXEL_RYRY2222:
        format = lithe::PixelFormat::ryry2222;
        break;
    default:
        return false;
    }
    pixelBytes = lithe::bytesPerPixel(format);
    return true;
}

bool translateViewport(const LitheViewPreviewSurface& surface, lithe::Viewport& viewport) noexcept
{
    const lithe::Rect bounds{0, 0, surface.width, surface.height};
    switch (surface.shape)
    {
    case LITHEVIEW_PREVIEW_SHAPE_RECTANGLE:
        viewport = lithe::Viewport::rectangle(bounds);
        return true;
    case LITHEVIEW_PREVIEW_SHAPE_CIRCLE:
        if (surface.width != surface.height)
        {
            return false;
        }
        viewport = lithe::Viewport::circle(bounds);
        return true;
    default:
        return false;
    }
}

bool inputEnumValuesValid(const LitheViewPreviewInput& input) noexcept
{
    if (input.struct_size != sizeof(LitheViewPreviewInput))
    {
        return false;
    }
    switch (input.kind)
    {
    case LITHEVIEW_PREVIEW_INPUT_POINTER:
        return input.value >= LITHEVIEW_PREVIEW_POINTER_PRESSED &&
               input.value <= LITHEVIEW_PREVIEW_POINTER_CANCELLED &&
               input.contact_id <= std::numeric_limits<std::uint8_t>::max();
    case LITHEVIEW_PREVIEW_INPUT_KEY:
        return input.value >= LITHEVIEW_PREVIEW_KEY_UP && input.value <= LITHEVIEW_PREVIEW_KEY_BACK;
    case LITHEVIEW_PREVIEW_INPUT_ENCODER:
        return input.value != 0;
    case LITHEVIEW_PREVIEW_INPUT_PUSH_BUTTON:
    case LITHEVIEW_PREVIEW_INPUT_FOCUS:
        return input.value == 0 || input.value == 1;
    case LITHEVIEW_PREVIEW_INPUT_ELAPSED_TIME:
        return input.value >= 0;
    default:
        return false;
    }
}

lithe::Key translateKey(std::int32_t value) noexcept
{
    switch (value)
    {
    case LITHEVIEW_PREVIEW_KEY_UP:
        return lithe::Key::up;
    case LITHEVIEW_PREVIEW_KEY_DOWN:
        return lithe::Key::down;
    case LITHEVIEW_PREVIEW_KEY_LEFT:
        return lithe::Key::left;
    case LITHEVIEW_PREVIEW_KEY_RIGHT:
        return lithe::Key::right;
    case LITHEVIEW_PREVIEW_KEY_SELECT:
        return lithe::Key::select;
    case LITHEVIEW_PREVIEW_KEY_BACK:
    default:
        break;
    }
    return lithe::Key::back;
}

lithe::PointerPhase translatePointerPhase(std::int32_t value) noexcept
{
    switch (value)
    {
    case LITHEVIEW_PREVIEW_POINTER_PRESSED:
        return lithe::PointerPhase::pressed;
    case LITHEVIEW_PREVIEW_POINTER_MOVED:
        return lithe::PointerPhase::moved;
    case LITHEVIEW_PREVIEW_POINTER_RELEASED:
        return lithe::PointerPhase::released;
    case LITHEVIEW_PREVIEW_POINTER_CANCELLED:
    default:
        break;
    }
    return lithe::PointerPhase::cancelled;
}

void deliverInput(lithe::Application& application, const LitheViewPreviewInput& input) noexcept
{
    switch (input.kind)
    {
    case LITHEVIEW_PREVIEW_INPUT_POINTER:
        static_cast<void>(application.handlePointer({{input.x, input.y},
                                                     translatePointerPhase(input.value),
                                                     static_cast<std::uint8_t>(input.contact_id),
                                                     input.timestamp_ms}));
        break;
    case LITHEVIEW_PREVIEW_INPUT_KEY:
        static_cast<void>(application.handleKey(translateKey(input.value)));
        break;
    case LITHEVIEW_PREVIEW_INPUT_ENCODER:
        static_cast<void>(
            application.handleKey(input.value < 0 ? lithe::Key::up : lithe::Key::down));
        break;
    case LITHEVIEW_PREVIEW_INPUT_PUSH_BUTTON:
        if (input.value != 0)
        {
            static_cast<void>(application.handleKey(lithe::Key::select));
        }
        break;
    case LITHEVIEW_PREVIEW_INPUT_FOCUS:
        if (input.value != 0)
        {
            if (lithe::ViewController* controller = application.activeController())
            {
                controller->activate();
            }
        }
        break;
    case LITHEVIEW_PREVIEW_INPUT_ELAPSED_TIME:
        static_cast<void>(application.advanceTime(static_cast<std::uint32_t>(input.value)));
        break;
    default:
        break;
    }
}

lithe::Color
readPixel(std::span<const std::byte> pixels, lithe::PixelFormat format, std::size_t offset) noexcept
{
    switch (format)
    {
    case lithe::PixelFormat::rgb565:
        return lithe::PixelFormatTraits<lithe::PixelFormat::rgb565>::read(pixels, offset);
    case lithe::PixelFormat::rgb888:
        return lithe::PixelFormatTraits<lithe::PixelFormat::rgb888>::read(pixels, offset);
    case lithe::PixelFormat::xrgb8888:
        return lithe::PixelFormatTraits<lithe::PixelFormat::xrgb8888>::read(pixels, offset);
    case lithe::PixelFormat::argb8888:
        return lithe::PixelFormatTraits<lithe::PixelFormat::argb8888>::read(pixels, offset);
    case lithe::PixelFormat::rrrr2222:
    case lithe::PixelFormat::ryry2222:
    case lithe::PixelFormat::r2Packed2x2:
        // Native packed frames are rendered in place and never read back for quantization.
        return {};
    }
    return {};
}

void writePixel(std::span<std::byte> pixels,
                lithe::PixelFormat format,
                std::size_t offset,
                lithe::Color color) noexcept
{
    switch (format)
    {
    case lithe::PixelFormat::rgb565:
        lithe::PixelFormatTraits<lithe::PixelFormat::rgb565>::write(pixels, offset, color);
        break;
    case lithe::PixelFormat::rgb888:
        lithe::PixelFormatTraits<lithe::PixelFormat::rgb888>::write(pixels, offset, color);
        break;
    case lithe::PixelFormat::xrgb8888:
        lithe::PixelFormatTraits<lithe::PixelFormat::xrgb8888>::write(pixels, offset, color);
        break;
    case lithe::PixelFormat::argb8888:
        lithe::PixelFormatTraits<lithe::PixelFormat::argb8888>::write(pixels, offset, color);
        break;
    case lithe::PixelFormat::rrrr2222:
    case lithe::PixelFormat::ryry2222:
    case lithe::PixelFormat::r2Packed2x2:
        // Native packed frames are rendered in place and never quantized afterwards.
        break;
    }
}

lithe::Color quantize(lithe::Color color, std::uint32_t quantization) noexcept
{
    switch (quantization)
    {
    case LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE:
        return color;
    case LITHEVIEW_PREVIEW_QUANTIZATION_RGB565:
        return lithe::Color::fromRgb565(color.rgb565());
    case LITHEVIEW_PREVIEW_QUANTIZATION_RED_2BPP:
    {
        const std::uint32_t level =
            (static_cast<std::uint32_t>(color.red) * kRed2BitMaximum) / kMaximumChannelValue;
        return {static_cast<std::uint8_t>(level * kRed2BitStep), 0U, 0U, color.alpha};
    }
    case LITHEVIEW_PREVIEW_QUANTIZATION_RED_YELLOW_2BPP:
    {
        const bool red = color.red >= kRedYellowChannelThreshold;
        const bool yellow = color.green >= kRedYellowChannelThreshold;
        if (red && yellow)
        {
            return {static_cast<std::uint8_t>(kMaximumChannelValue),
                    kMixedYellowGreen,
                    0U,
                    color.alpha};
        }
        if (yellow)
        {
            return {static_cast<std::uint8_t>(kMaximumChannelValue),
                    static_cast<std::uint8_t>(kMaximumChannelValue),
                    0U,
                    color.alpha};
        }
        return {red ? static_cast<std::uint8_t>(kMaximumChannelValue) : std::uint8_t{},
                0U,
                0U,
                color.alpha};
    }
    default:
        return {};
    }
}

void quantizeFrame(const LitheViewPreviewSurface& descriptor,
                   lithe::PixelFormat format,
                   std::size_t pixelBytes,
                   std::span<std::byte> pixels) noexcept
{
    if (descriptor.quantization == LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE)
    {
        return;
    }
    for (std::int32_t y = 0; y < descriptor.height; ++y)
    {
        const std::size_t row = static_cast<std::size_t>(y) * descriptor.stride_bytes;
        for (std::int32_t x = 0; x < descriptor.width; ++x)
        {
            const std::size_t offset = row + (static_cast<std::size_t>(x) * pixelBytes);
            const lithe::Color color = readPixel(pixels, format, offset);
            writePixel(pixels, format, offset, quantize(color, descriptor.quantization));
        }
    }
}

std::uint32_t nextAnimationWakeup(std::span<const lithe::ViewSpec> specs) noexcept
{
    std::uint32_t next = 0U;
    for (const lithe::ViewSpec& spec : specs)
    {
        if (spec.kind != lithe::ViewKind::animatedImage || !spec.animationAutoplay ||
            spec.animationFrameCount == 0U)
        {
            continue;
        }
        if (next == 0U || spec.animationIntervalMs < next)
        {
            next = spec.animationIntervalMs;
        }
    }
    return next;
}

} // namespace

extern "C" uint32_t litheview_preview_abi_version(void)
{
    return LITHEVIEW_PREVIEW_ABI_VERSION;
}

// The ordered checks are the single fail-closed C ABI transaction boundary.
extern "C" LitheViewPreviewStatus // NOLINTNEXTLINE(readability-function-cognitive-complexity)
litheview_preview_render(const LitheViewPreviewRequest* request, LitheViewPreviewResult* result)
{
    if (request == nullptr || result == nullptr ||
        result->struct_size != sizeof(LitheViewPreviewResult))
    {
        return LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT;
    }
    initializeResult(*result);
    if (request->struct_size != sizeof(LitheViewPreviewRequest) ||
        request->surface.struct_size != sizeof(LitheViewPreviewSurface) ||
        request->snapshot == nullptr || request->pixels == nullptr ||
        request->damage_regions == nullptr || request->diagnostic == nullptr ||
        request->diagnostic_capacity == 0U)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT,
                    kDiagnosticInvalidArgument,
                    "LVPR1001: invalid preview request pointers or structure sizes");
    }
    std::span<char> diagnostics{request->diagnostic, request->diagnostic_capacity};
    diagnostics.front() = '\0';
    if (request->abi_version != LITHEVIEW_PREVIEW_ABI_VERSION)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_ABI_MISMATCH,
                    kDiagnosticAbiMismatch,
                    "LVPR1002: preview ABI version mismatch");
    }
    if (request->schema_version != LITHEVIEW_PREVIEW_SCHEMA_VERSION)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_SCHEMA_MISMATCH,
                    kDiagnosticSchemaMismatch,
                    "LVPR1003: widget schema version mismatch");
    }
    if ((request->flags & ~LITHEVIEW_PREVIEW_REQUEST_TILED) != 0U)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT,
                    kDiagnosticInvalidArgument,
                    "LVPR1001: unsupported preview request flags");
    }
    if (request->snapshot_size == 0U ||
        request->snapshot_size > LITHEVIEW_PREVIEW_MAX_SNAPSHOT_BYTES ||
        request->input_count > LITHEVIEW_PREVIEW_MAX_INPUTS)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_SIZE_LIMIT,
                    kDiagnosticSizeLimit,
                    "LVPR1004: snapshot or input count exceeds the preview limit");
    }
    if (request->input_count != 0U && request->inputs == nullptr)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT,
                    kDiagnosticInvalidArgument,
                    "LVPR1001: input count requires an input array");
    }
    const std::span<const LitheViewPreviewInput> inputs{request->inputs, request->input_count};
    for (const LitheViewPreviewInput& input : inputs)
    {
        if (!inputEnumValuesValid(input))
        {
            return fail(*request,
                        *result,
                        LITHEVIEW_PREVIEW_STATUS_INVALID_INPUT,
                        kDiagnosticInvalidInput,
                        "LVPR1008: invalid preview input event");
        }
    }

    lithe::PixelFormat format{};
    std::size_t pixelBytes = 0U;
    if (!translateFormat(request->surface.pixel_format, format, pixelBytes) ||
        request->surface.quantization > LITHEVIEW_PREVIEW_QUANTIZATION_RED_YELLOW_2BPP ||
        (nativePacked(format) &&
         request->surface.quantization != LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE))
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_UNSUPPORTED_FORMAT,
                    kDiagnosticUnsupportedFormat,
                    "LVPR1005: unsupported pixel format or quantization profile");
    }
    lithe::Viewport viewport{};
    if (!translateViewport(request->surface, viewport))
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT,
                    kDiagnosticInvalidArgument,
                    "LVPR1001: invalid surface shape or dimensions");
    }
    const auto frame = frameSize(request->surface, format);
    if (!frame && frame.error() == lithe::StorageError::misalignedExtent)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT,
                    kDiagnosticInvalidArgument,
                    "LVPR1001: surface extent does not contain whole native storage blocks");
    }
    if (!frame)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_SIZE_LIMIT,
                    kDiagnosticSizeLimit,
                    "LVPR1004: surface dimensions or stride exceed the preview limit");
    }
    const std::size_t frameBytes = *frame;
    if (request->pixel_capacity < frameBytes ||
        request->damage_capacity < LITHEVIEW_PREVIEW_MAX_DAMAGE_REGIONS)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_OUTPUT_TOO_SMALL,
                    kDiagnosticOutputTooSmall,
                    "LVPR1009: caller-owned pixel or damage storage is too small");
    }

    const std::string_view xml{std::bit_cast<const char*>(request->snapshot),
                               request->snapshot_size};
    std::array<lithe::ViewSpec, LITHEVIEW_PREVIEW_MAX_VIEWS> specs{};
    std::array<std::string_view, kAnimationFrameCapacity> animationFrames{};
    std::array<lithe::WidgetStateStyle, LITHEVIEW_PREVIEW_MAX_VIEWS> stateStyles{};
    const lithe::XmlLayoutResult parsed = lithe::XmlLayoutParser::parse(
        xml, specs, lithe::XmlParseOptions{true, animationFrames, stateStyles});
    if (!parsed)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INVALID_SNAPSHOT,
                    kDiagnosticInvalidSnapshot,
                    "LVPR1006: strict XML snapshot parsing failed");
    }
    const std::span<const lithe::ViewSpec> parsedSpecs{specs.data(), parsed.nodeCount};
    if (parsedSpecs.empty() || parsedSpecs.front().frame.x != 0 ||
        parsedSpecs.front().frame.y != 0 ||
        parsedSpecs.front().frame.width != request->surface.width ||
        parsedSpecs.front().frame.height != request->surface.height)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INVALID_SNAPSHOT,
                    kDiagnosticInvalidSnapshot,
                    "LVPR1006: root frame does not match the resolved preview surface");
    }

    lithe::StaticLayout<LITHEVIEW_PREVIEW_MAX_VIEWS> layout{};
    PreviewResources resources{};
    const lithe::LayoutResult built = layout.build(
        parsedSpecs,
        lithe::font5x7(),
        &resources,
        std::span<const std::string_view>{animationFrames.data(), parsed.animationFrameCount});
    if (!built)
    {
        const bool missing = built.error == lithe::LayoutError::missingResource;
        return fail(*request,
                    *result,
                    missing ? LITHEVIEW_PREVIEW_STATUS_MISSING_RESOURCE
                            : LITHEVIEW_PREVIEW_STATUS_INVALID_SNAPSHOT,
                    missing ? kDiagnosticMissingResource : kDiagnosticInvalidSnapshot,
                    missing ? "LVPR1007: snapshot references an unavailable preview resource"
                            : "LVPR1006: snapshot could not build a bounded static layout");
    }

    std::span<std::byte> pixels =
        std::as_writable_bytes(std::span<std::uint8_t>{request->pixels, frameBytes});
    lithe::Surface surface{pixels,
                           format,
                           request->surface.width,
                           request->surface.height,
                           request->surface.stride_bytes};
    if (!surface.valid() || layout.root() == nullptr)
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INTERNAL_ERROR,
                    kDiagnosticInternalError,
                    "LVPR1010: validated preview state could not be constructed");
    }

    std::fill(pixels.begin(), pixels.end(), std::byte{});
    PreviewDisplay display{surface};
    lithe::ViewController controller{*layout.root()};
    lithe::Application application{display, viewport};
    if (!application.push(controller))
    {
        return fail(*request,
                    *result,
                    LITHEVIEW_PREVIEW_STATUS_INTERNAL_ERROR,
                    kDiagnosticInternalError,
                    "LVPR1010: preview controller activation failed");
    }
    for (const LitheViewPreviewInput& input : inputs)
    {
        deliverInput(application, input);
        ++result->accepted_input_count;
    }

    lithe::DamageTracker damage{};
    if ((request->flags & LITHEVIEW_PREVIEW_REQUEST_TILED) != 0U)
    {
        const std::int32_t tileHeight = request->surface.height < kPreviewTileHeight
                                            ? request->surface.height
                                            : kPreviewTileHeight;
        std::size_t tileBytes = 0U;
        if (!lithe::detail::checkedMultiply(request->surface.stride_bytes,
                                            lithe::detail::nonnegativeSize(tileHeight),
                                            tileBytes) ||
            tileBytes > gTileWorkspace.size())
        {
            return fail(*request,
                        *result,
                        LITHEVIEW_PREVIEW_STATUS_INTERNAL_ERROR,
                        kDiagnosticInternalError,
                        "LVPR1010: tiled preview workspace calculation failed");
        }
        lithe::Surface workspace{std::span<std::byte>{gTileWorkspace}.first(tileBytes),
                                 format,
                                 request->surface.width,
                                 tileHeight,
                                 request->surface.stride_bytes};
        PreviewTileSink sink{pixels, request->surface.stride_bytes, format};
        const lithe::TileRenderResult tiled = lithe::TiledRenderer::render(
            application, {request->surface.width, request->surface.height}, workspace, sink);
        if (!tiled || !sink.complete())
        {
            return fail(*request,
                        *result,
                        LITHEVIEW_PREVIEW_STATUS_INTERNAL_ERROR,
                        kDiagnosticInternalError,
                        "LVPR1010: real tiled renderer did not complete the preview frame");
        }
        damage.add({0, 0, request->surface.width, request->surface.height});
    }
    else
    {
        static_cast<void>(application.renderSurface(surface, damage, true));
    }
    quantizeFrame(request->surface, format, pixelBytes, pixels);
    result->frame_bytes = frameBytes;
    result->damage_count = damage.size();
    std::span<LitheViewPreviewRect> damageOutput{request->damage_regions, request->damage_capacity};
    for (std::size_t index = 0U; index < damage.size(); ++index)
    {
        const lithe::Rect region = damage.regions()[index];
        damageOutput[index] = {region.x, region.y, region.width, region.height};
    }
    if (application.animationsRunning())
    {
        result->flags |= LITHEVIEW_PREVIEW_RESULT_ANIMATION_RUNNING;
        result->next_wakeup_ms = nextAnimationWakeup(parsedSpecs);
    }
    result->status = LITHEVIEW_PREVIEW_STATUS_OK;
    result->diagnostic_code = 0U;
    return LITHEVIEW_PREVIEW_STATUS_OK;
}
