// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/designer/preview_abi.h"

#include <algorithm>
#include <array>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <span>
#include <string_view>

namespace
{

constexpr std::int32_t kWidth{64};
constexpr std::int32_t kHeight{48};
constexpr std::uint8_t kUnchangedFill{0xa5U};
constexpr std::uint8_t kInvalidRequestFill{0x5aU};
constexpr std::size_t kMaximumFrameBytes{static_cast<std::size_t>(kWidth) *
                                         static_cast<std::size_t>(kHeight) * 4U};
constexpr std::string_view kSnapshot =
    "<panel frame=\"0,0,64,48\" backgroundColor=\"0x00223344\">"
    "<button frame=\"4,4,56,20\" runtimeID=\"7\" text=\"PREVIEW\" canFocus=\"1\"/>"
    "</panel>";
// A latched toggle with selected-state colors above a momentary button.
constexpr std::string_view kButtonSnapshot =
    "<panel frame=\"0,0,64,48\" backgroundColor=\"0x00223344\">"
    "<button frame=\"4,2,56,20\" runtimeID=\"7\" text=\"LATCH\" behavior=\"toggle\" "
    "selectedBackgroundColor=\"0x00fae4d6\" selectedForegroundColor=\"0x00803d1a\"/>"
    "<button frame=\"4,26,56,20\" runtimeID=\"8\" text=\"PUSH\"/>"
    "</panel>";
// A two-tab view whose pages differ only in background color.
constexpr std::string_view kTabSnapshot =
    "<panel frame=\"0,0,64,48\" backgroundColor=\"0x00223344\">"
    "<tabview frame=\"0,0,64,48\" runtimeID=\"9\" items=\"A|B\" tabHeight=\"12\" "
    "backgroundColor=\"0x00402818\" textColor=\"0x00ffffff\">"
    "<panel frame=\"0,12,64,36\" backgroundColor=\"0x00203040\"/>"
    "<panel frame=\"0,12,64,36\" backgroundColor=\"0x00183828\"/>"
    "</tabview>"
    "</panel>";
// A five-item wheel filling the surface with 16-pixel rows; its band spans y 16 through 31.
constexpr std::string_view kWheelSnapshot =
    "<panel frame=\"0,0,64,48\" backgroundColor=\"0x00223344\">"
    "<wheelpicker frame=\"0,0,64,48\" runtimeID=\"10\" items=\"A|B|C|D|E\" selectedIndex=\"2\" "
    "rowHeight=\"16\" backgroundColor=\"0x00402818\" textColor=\"0x00ffffff\"/>"
    "</panel>";
constexpr std::int32_t kWheelX{32};
constexpr std::int32_t kWheelAboveBandY{8};
constexpr std::int32_t kSecondTabX{48};
constexpr std::int32_t kTabStripY{6};
constexpr std::int32_t kToggleInsideX{10};
constexpr std::int32_t kToggleInsideY{10};
constexpr std::int32_t kOutsideToggleY{40};

// Fixed test buffers avoid making the host test's stack size part of the ABI evidence.
// NOLINTBEGIN(cppcoreguidelines-avoid-non-const-global-variables)
std::array<std::uint8_t, kMaximumFrameBytes> gPixels{};
std::array<LitheViewPreviewRect, LITHEVIEW_PREVIEW_MAX_DAMAGE_REGIONS> gDamage{};
std::array<char, LITHEVIEW_PREVIEW_MAX_DIAGNOSTIC_BYTES> gDiagnostic{};
// NOLINTEND(cppcoreguidelines-avoid-non-const-global-variables)

struct TestContext
{
    std::size_t failures{0U};

    void expect(bool condition, std::string_view message)
    {
        if (!condition)
        {
            ++failures;
            std::cerr << "FAIL " << message << '\n';
        }
    }
};

std::size_t bytesPerPixel(std::uint32_t format) noexcept
{
    switch (format)
    {
    case LITHEVIEW_PREVIEW_PIXEL_RGB565:
        return 2U;
    case LITHEVIEW_PREVIEW_PIXEL_RGB888:
        return 3U;
    case LITHEVIEW_PREVIEW_PIXEL_XRGB8888:
    case LITHEVIEW_PREVIEW_PIXEL_ARGB8888:
        return 4U;
    default:
        return 0U;
    }
}

LitheViewPreviewResult renderSurface(const LitheViewPreviewSurface& surface,
                                     std::size_t pixelCapacity,
                                     std::span<const LitheViewPreviewInput> inputs,
                                     std::uint32_t flags,
                                     std::string_view snapshot) noexcept
{
    std::fill(gPixels.begin(), gPixels.end(), kUnchangedFill);
    std::fill(gDamage.begin(), gDamage.end(), LitheViewPreviewRect{});
    std::fill(gDiagnostic.begin(), gDiagnostic.end(), '\0');
    const LitheViewPreviewRequest request{sizeof(LitheViewPreviewRequest),
                                          LITHEVIEW_PREVIEW_ABI_VERSION,
                                          LITHEVIEW_PREVIEW_SCHEMA_VERSION,
                                          flags,
                                          surface,
                                          std::bit_cast<const std::uint8_t*>(snapshot.data()),
                                          snapshot.size(),
                                          inputs.data(),
                                          inputs.size(),
                                          gPixels.data(),
                                          pixelCapacity,
                                          gDamage.data(),
                                          gDamage.size(),
                                          gDiagnostic.data(),
                                          gDiagnostic.size()};
    LitheViewPreviewResult result{};
    result.struct_size = sizeof(result);
    static_cast<void>(litheview_preview_render(&request, &result));
    return result;
}

LitheViewPreviewResult render(std::uint32_t format,
                              std::uint32_t shape,
                              std::uint32_t quantization,
                              std::span<const LitheViewPreviewInput> inputs = {},
                              std::uint32_t flags = 0U,
                              std::string_view snapshot = kSnapshot) noexcept
{
    const std::size_t pixelBytes = bytesPerPixel(format);
    const std::size_t stride = static_cast<std::size_t>(kWidth) * pixelBytes;
    const std::size_t frameBytes = stride * static_cast<std::size_t>(kHeight);
    const LitheViewPreviewSurface surface{sizeof(LitheViewPreviewSurface),
                                          format,
                                          shape,
                                          quantization,
                                          kWidth,
                                          kHeight,
                                          static_cast<std::uint32_t>(stride),
                                          0U};
    return renderSurface(surface, frameBytes, inputs, flags, snapshot);
}

// Native packed oracle scene on the same 64x48 logical extent: emitters for R2, cells for
// RRRR/RYRY. Later rectangles are children drawn over the root.
constexpr std::string_view kNativeSnapshot =
    "<panel frame=\"0,0,64,48\" backgroundColor=\"0x000000ff\">"
    "<panel frame=\"8,4,16,8\" backgroundColor=\"0x00000080\"/>"
    "<panel frame=\"32,16,16,8\" backgroundColor=\"0x0000ffff\"/>"
    "<panel frame=\"41,33,5,3\" backgroundColor=\"0x00000080\"/>"
    "</panel>";

struct OracleRect
{
    std::size_t x;
    std::size_t y;
    std::size_t width;
    std::size_t height;
    std::uint32_t red;
    std::uint32_t green;
};

constexpr std::array<OracleRect, 4U> kNativeScene{{{0U, 0U, 64U, 48U, 255U, 0U},
                                                   {8U, 4U, 16U, 8U, 128U, 0U},
                                                   {32U, 16U, 16U, 8U, 255U, 255U},
                                                   {41U, 33U, 5U, 3U, 128U, 0U}}};

// Row-major fields of one storage byte: TL bits 3:2, TR 7:6, BL 1:0, BR 5:4.
constexpr std::array<std::uint32_t, 4U> kFieldShift{2U, 6U, 0U, 4U};

OracleRect oracleColor(std::size_t x, std::size_t y) noexcept
{
    OracleRect color = kNativeScene.front();
    for (const OracleRect& rect : kNativeScene)
    {
        if (x >= rect.x && x < rect.x + rect.width && y >= rect.y && y < rect.y + rect.height)
        {
            color = rect;
        }
    }
    return color;
}

std::uint32_t nativeLevel(std::uint32_t channel) noexcept
{
    return ((channel * 3U) + 127U) / 255U;
}

// An independent statement of the software packing contract. R2 stores one red level per
// emitter. RRRR repeats a cell's red level in all four emitters. RYRY stores yellow,
// min(red, green), at TL/BR and the remaining red at TR/BL.
std::uint32_t expectedNativeByte(std::uint32_t format, std::size_t column, std::size_t row) noexcept
{
    std::uint32_t encoded = 0U;
    for (std::size_t field = 0U; field < kFieldShift.size(); ++field)
    {
        std::uint32_t level = 0U;
        if (format == LITHEVIEW_PREVIEW_PIXEL_R2_PACKED_2X2)
        {
            level = nativeLevel(oracleColor((column * 2U) + (field % 2U), (row * 2U) + (field / 2U)).red);
        }
        else
        {
            const OracleRect cell = oracleColor(column, row);
            const bool redYellow = format == LITHEVIEW_PREVIEW_PIXEL_RYRY2222;
            const std::uint32_t yellow = redYellow ? std::min(cell.red, cell.green) : cell.red;
            const bool yellowField = field == 0U || field == 3U;
            level = nativeLevel(yellowField ? yellow : (redYellow ? cell.red - yellow : cell.red));
        }
        encoded |= level << kFieldShift[field];
    }
    return encoded;
}

struct NativeCase
{
    std::uint32_t format;
    std::uint32_t stride;
    std::size_t columns;
    std::size_t rows;
};

constexpr std::array<NativeCase, 3U> kNativeCases{
    {{LITHEVIEW_PREVIEW_PIXEL_R2_PACKED_2X2, 32U, 32U, 24U},
     {LITHEVIEW_PREVIEW_PIXEL_RRRR2222, 64U, 64U, 48U},
     {LITHEVIEW_PREVIEW_PIXEL_RYRY2222, 64U, 64U, 48U}}};

LitheViewPreviewSurface nativeSurface(std::uint32_t format,
                                      std::int32_t width,
                                      std::int32_t height,
                                      std::uint32_t stride,
                                      std::uint32_t quantization =
                                          LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE) noexcept
{
    return {sizeof(LitheViewPreviewSurface),
            format,
            LITHEVIEW_PREVIEW_SHAPE_RECTANGLE,
            quantization,
            width,
            height,
            stride,
            0U};
}

bool matchesNativeOracle(const NativeCase& nativeCase, std::size_t stride) noexcept
{
    for (std::size_t row = 0U; row < nativeCase.rows; ++row)
    {
        for (std::size_t column = 0U; column < nativeCase.columns; ++column)
        {
            if (std::uint32_t{gPixels[(row * stride) + column]} !=
                expectedNativeByte(nativeCase.format, column, row))
            {
                return false;
            }
        }
    }
    return true;
}

void testFormats(TestContext& context)
{
    constexpr std::array<std::uint32_t, 4U> kFormats{LITHEVIEW_PREVIEW_PIXEL_RGB565,
                                                     LITHEVIEW_PREVIEW_PIXEL_RGB888,
                                                     LITHEVIEW_PREVIEW_PIXEL_XRGB8888,
                                                     LITHEVIEW_PREVIEW_PIXEL_ARGB8888};
    for (const std::uint32_t format : kFormats)
    {
        const LitheViewPreviewResult result = render(
            format, LITHEVIEW_PREVIEW_SHAPE_RECTANGLE, LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE);
        const std::size_t expected = static_cast<std::size_t>(kWidth) *
                                     static_cast<std::size_t>(kHeight) * bytesPerPixel(format);
        const std::array<std::uint8_t, kMaximumFrameBytes> fullFrame = gPixels;
        context.expect(result.status == LITHEVIEW_PREVIEW_STATUS_OK,
                       "each public preview pixel format renders");
        context.expect(result.frame_bytes == expected, "format reports the exact frame byte count");
        context.expect(result.damage_count != 0U, "real renderer reports a dirty region");
        context.expect(std::any_of(gPixels.begin(),
                                   gPixels.begin() + static_cast<std::ptrdiff_t>(expected),
                                   [](std::uint8_t value)
                                   {
                                       return value != 0U;
                                   }),
                       "real renderer changes caller-owned pixels");
        const LitheViewPreviewResult tiled = render(format,
                                                    LITHEVIEW_PREVIEW_SHAPE_RECTANGLE,
                                                    LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE,
                                                    {},
                                                    LITHEVIEW_PREVIEW_REQUEST_TILED);
        context.expect(tiled.status == LITHEVIEW_PREVIEW_STATUS_OK,
                       "real tiled preview renderer completes");
        context.expect(std::equal(gPixels.begin(),
                                  gPixels.begin() + static_cast<std::ptrdiff_t>(expected),
                                  fullFrame.begin()),
                       "tiled preview pixels match the full-frame renderer exactly");
    }
}

void testCircleAndQuantization(TestContext& context)
{
    const LitheViewPreviewResult circle = render(LITHEVIEW_PREVIEW_PIXEL_XRGB8888,
                                                 LITHEVIEW_PREVIEW_SHAPE_CIRCLE,
                                                 LITHEVIEW_PREVIEW_QUANTIZATION_RGB565);
    context.expect(circle.status == LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT,
                   "non-square circular surface fails closed");

    const LitheViewPreviewResult quantized = render(LITHEVIEW_PREVIEW_PIXEL_RGB888,
                                                    LITHEVIEW_PREVIEW_SHAPE_RECTANGLE,
                                                    LITHEVIEW_PREVIEW_QUANTIZATION_RED_2BPP);
    context.expect(quantized.status == LITHEVIEW_PREVIEW_STATUS_OK,
                   "target-specific quantization renders");
    bool onlyRed = true;
    for (std::size_t offset = 0U; offset < quantized.frame_bytes; offset += 3U)
    {
        onlyRed = onlyRed && gPixels[offset + 1U] == 0U && gPixels[offset + 2U] == 0U;
    }
    context.expect(onlyRed, "red-panel quantization suppresses green and blue channels");
}

void testNativePackedFormats(TestContext& context)
{
    for (const NativeCase& nativeCase : kNativeCases)
    {
        const std::size_t frameBytes = std::size_t{nativeCase.stride} * nativeCase.rows;
        const LitheViewPreviewSurface surface =
            nativeSurface(nativeCase.format, kWidth, kHeight, nativeCase.stride);
        const LitheViewPreviewResult full =
            renderSurface(surface, frameBytes, {}, 0U, kNativeSnapshot);
        context.expect(full.status == LITHEVIEW_PREVIEW_STATUS_OK && full.frame_bytes == frameBytes,
                       "native packed preview renders its exact storage size");
        context.expect(matchesNativeOracle(nativeCase, nativeCase.stride),
                       "native packed pixels match the independent packing oracle");
        context.expect(gPixels[frameBytes] == kUnchangedFill,
                       "native packed preview writes nothing past its frame");
        const LitheViewPreviewResult tiled = renderSurface(
            surface, frameBytes, {}, LITHEVIEW_PREVIEW_REQUEST_TILED, kNativeSnapshot);
        context.expect(tiled.status == LITHEVIEW_PREVIEW_STATUS_OK &&
                           matchesNativeOracle(nativeCase, nativeCase.stride),
                       "tiled native packed pixels match the independent oracle");
        const std::uint32_t padded = nativeCase.stride + 3U;
        const LitheViewPreviewResult withPadding =
            renderSurface(nativeSurface(nativeCase.format, kWidth, kHeight, padded),
                          std::size_t{padded} * nativeCase.rows,
                          {},
                          0U,
                          kNativeSnapshot);
        context.expect(withPadding.status == LITHEVIEW_PREVIEW_STATUS_OK &&
                           withPadding.frame_bytes == std::size_t{padded} * nativeCase.rows &&
                           matchesNativeOracle(nativeCase, padded),
                       "native packed block rows respect a padded stride");

        const LitheViewPreviewResult shortBuffer =
            renderSurface(surface, frameBytes - 1U, {}, 0U, kNativeSnapshot);
        context.expect(shortBuffer.status == LITHEVIEW_PREVIEW_STATUS_OUTPUT_TOO_SMALL &&
                           gPixels.front() == kUnchangedFill,
                       "insufficient native storage fails closed before rendering");
        const LitheViewPreviewResult narrowStride = renderSurface(
            nativeSurface(nativeCase.format, kWidth, kHeight, nativeCase.stride - 1U),
            frameBytes,
            {},
            0U,
            kNativeSnapshot);
        context.expect(narrowStride.status == LITHEVIEW_PREVIEW_STATUS_SIZE_LIMIT,
                       "a stride shorter than one storage-block row fails closed");
        const LitheViewPreviewResult quantized = renderSurface(
            nativeSurface(nativeCase.format,
                          kWidth,
                          kHeight,
                          nativeCase.stride,
                          LITHEVIEW_PREVIEW_QUANTIZATION_RED_2BPP),
            frameBytes,
            {},
            0U,
            kNativeSnapshot);
        context.expect(quantized.status == LITHEVIEW_PREVIEW_STATUS_UNSUPPORTED_FORMAT,
                       "native packed frames are never quantized RGB frames");
    }
    // R2 storage blocks are 2x2 emitters, so an odd logical extent has no whole blocks.
    const LitheViewPreviewResult oddWidth =
        renderSurface(nativeSurface(LITHEVIEW_PREVIEW_PIXEL_R2_PACKED_2X2, kWidth - 1, kHeight, 32U),
                      kMaximumFrameBytes,
                      {},
                      0U,
                      kNativeSnapshot);
    context.expect(oddWidth.status == LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT &&
                       gPixels.front() == kUnchangedFill,
                   "a packed extent without whole storage blocks fails closed");
    const LitheViewPreviewResult oddHeight =
        renderSurface(nativeSurface(LITHEVIEW_PREVIEW_PIXEL_R2_PACKED_2X2, kWidth, kHeight - 1, 32U),
                      kMaximumFrameBytes,
                      {},
                      0U,
                      kNativeSnapshot);
    context.expect(oddHeight.status == LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT,
                   "a packed height without whole block rows fails closed");
    const LitheViewPreviewResult unknown = renderSurface(
        nativeSurface(LITHEVIEW_PREVIEW_PIXEL_RYRY2222 + 1U, kWidth, kHeight, 64U),
        kMaximumFrameBytes,
        {},
        0U,
        kNativeSnapshot);
    context.expect(unknown.status == LITHEVIEW_PREVIEW_STATUS_UNSUPPORTED_FORMAT,
                   "an unknown pixel format fails closed");
}

void testInputAndInvalidRequests(TestContext& context)
{
    const std::array<LitheViewPreviewInput, 3U> inputs{
        LitheViewPreviewInput{
            sizeof(LitheViewPreviewInput), LITHEVIEW_PREVIEW_INPUT_FOCUS, 1, 0, 0, 0U, 0U, 0U},
        LitheViewPreviewInput{
            sizeof(LitheViewPreviewInput), LITHEVIEW_PREVIEW_INPUT_ENCODER, 1, 0, 0, 0U, 0U, 0U},
        LitheViewPreviewInput{sizeof(LitheViewPreviewInput),
                              LITHEVIEW_PREVIEW_INPUT_PUSH_BUTTON,
                              1,
                              0,
                              0,
                              0U,
                              0U,
                              0U}};
    const LitheViewPreviewResult inputResult = render(LITHEVIEW_PREVIEW_PIXEL_RGB565,
                                                      LITHEVIEW_PREVIEW_SHAPE_RECTANGLE,
                                                      LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE,
                                                      inputs);
    context.expect(inputResult.status == LITHEVIEW_PREVIEW_STATUS_OK &&
                       inputResult.accepted_input_count == inputs.size(),
                   "focus, encoder, and push-button inputs reach the real controller");

    std::fill(gPixels.begin(), gPixels.end(), kInvalidRequestFill);
    LitheViewPreviewSurface surface{sizeof(LitheViewPreviewSurface),
                                    LITHEVIEW_PREVIEW_PIXEL_RGB565,
                                    LITHEVIEW_PREVIEW_SHAPE_RECTANGLE,
                                    LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE,
                                    kWidth,
                                    kHeight,
                                    static_cast<std::uint32_t>(kWidth * 2),
                                    0U};
    const LitheViewPreviewRequest request{sizeof(LitheViewPreviewRequest),
                                          LITHEVIEW_PREVIEW_ABI_VERSION + 1U,
                                          LITHEVIEW_PREVIEW_SCHEMA_VERSION,
                                          0U,
                                          surface,
                                          std::bit_cast<const std::uint8_t*>(kSnapshot.data()),
                                          kSnapshot.size(),
                                          nullptr,
                                          0U,
                                          gPixels.data(),
                                          gPixels.size(),
                                          gDamage.data(),
                                          gDamage.size(),
                                          gDiagnostic.data(),
                                          gDiagnostic.size()};
    LitheViewPreviewResult result{};
    result.struct_size = sizeof(result);
    static_cast<void>(litheview_preview_render(&request, &result));
    context.expect(result.status == LITHEVIEW_PREVIEW_STATUS_ABI_MISMATCH,
                   "ABI mismatch fails explicitly");
    context.expect(std::all_of(gPixels.begin(),
                               gPixels.end(),
                               [](std::uint8_t value)
                               {
                                   return value == kInvalidRequestFill;
                               }),
                   "invalid requests leave caller pixel storage untouched");
}

constexpr LitheViewPreviewInput
makeInput(std::uint32_t kind, std::int32_t value, std::int32_t x = 0, std::int32_t y = 0) noexcept
{
    return {sizeof(LitheViewPreviewInput), kind, value, x, y, 0U, 0U, 0U};
}

using Frame = std::array<std::uint8_t, kMaximumFrameBytes>;

// Unwritten bytes keep the same fill in every render, so whole buffers compare exactly.
Frame renderButtons(TestContext& context,
                    std::span<const LitheViewPreviewInput> inputs,
                    std::string_view message,
                    std::string_view snapshot = kButtonSnapshot)
{
    const LitheViewPreviewResult result = render(LITHEVIEW_PREVIEW_PIXEL_RGB565,
                                                 LITHEVIEW_PREVIEW_SHAPE_RECTANGLE,
                                                 LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE,
                                                 inputs,
                                                 0U,
                                                 snapshot);
    context.expect(result.status == LITHEVIEW_PREVIEW_STATUS_OK &&
                       result.accepted_input_count == inputs.size(),
                   message);
    return gPixels;
}

void testButtonVariants(TestContext& context)
{
    constexpr LitheViewPreviewInput kFocus{makeInput(LITHEVIEW_PREVIEW_INPUT_FOCUS, 1)};
    constexpr LitheViewPreviewInput kNext{makeInput(LITHEVIEW_PREVIEW_INPUT_ENCODER, 1)};
    constexpr LitheViewPreviewInput kSelect{makeInput(LITHEVIEW_PREVIEW_INPUT_PUSH_BUTTON, 1)};
    constexpr LitheViewPreviewInput kPress{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                     LITHEVIEW_PREVIEW_POINTER_PRESSED,
                                                     kToggleInsideX,
                                                     kToggleInsideY)};
    constexpr LitheViewPreviewInput kRelease{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                       LITHEVIEW_PREVIEW_POINTER_RELEASED,
                                                       kToggleInsideX,
                                                       kToggleInsideY)};
    constexpr LitheViewPreviewInput kMoveOutside{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                           LITHEVIEW_PREVIEW_POINTER_MOVED,
                                                           kToggleInsideX,
                                                           kOutsideToggleY)};
    constexpr LitheViewPreviewInput kReleaseOutside{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                              LITHEVIEW_PREVIEW_POINTER_RELEASED,
                                                              kToggleInsideX,
                                                              kOutsideToggleY)};

    const Frame focused =
        renderButtons(context, std::array{kFocus}, "button variants accept a focus request");
    const Frame latched = renderButtons(
        context, std::array{kFocus, kSelect}, "push-button input reaches the toggle button");
    const Frame unlatched = renderButtons(
        context, std::array{kFocus, kSelect, kSelect}, "repeated push-button input is accepted");
    context.expect(latched != focused, "push-button activation latches a toggle button");
    context.expect(unlatched == focused, "a second activation releases the toggle button");

    const Frame clicked = renderButtons(
        context, std::array{kPress, kRelease}, "pointer press and release reach the toggle button");
    const Frame cancelled = renderButtons(context,
                                          std::array{kPress, kMoveOutside, kReleaseOutside},
                                          "captured pointer movement reaches the toggle button");
    context.expect(clicked == latched,
                   "pointer and push-button activation produce the same latched toggle");
    context.expect(cancelled == focused,
                   "releasing outside the toggle cancels activation without latching");

    const Frame selectedUnfocused =
        renderButtons(context, std::array{kFocus, kSelect, kNext}, "encoder input moves focus");
    const Frame momentaryFocused = renderButtons(
        context, std::array{kFocus, kNext}, "encoder input focuses the momentary button");
    const Frame momentaryActivated =
        renderButtons(context,
                      std::array{kFocus, kNext, kSelect},
                      "push-button input reaches the momentary button");
    context.expect(selectedUnfocused != momentaryFocused,
                   "an unfocused latched toggle shows its selected-state colors");
    context.expect(momentaryActivated == momentaryFocused,
                   "a momentary button does not latch after activation");
}

void testTabs(TestContext& context)
{
    constexpr LitheViewPreviewInput kFocus{makeInput(LITHEVIEW_PREVIEW_INPUT_FOCUS, 1)};
    constexpr LitheViewPreviewInput kRight{
        makeInput(LITHEVIEW_PREVIEW_INPUT_KEY, LITHEVIEW_PREVIEW_KEY_RIGHT)};
    constexpr LitheViewPreviewInput kSelect{makeInput(LITHEVIEW_PREVIEW_INPUT_PUSH_BUTTON, 1)};
    constexpr LitheViewPreviewInput kPress{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                     LITHEVIEW_PREVIEW_POINTER_PRESSED,
                                                     kSecondTabX,
                                                     kTabStripY)};
    constexpr LitheViewPreviewInput kRelease{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                       LITHEVIEW_PREVIEW_POINTER_RELEASED,
                                                       kSecondTabX,
                                                       kTabStripY)};
    constexpr LitheViewPreviewInput kReleaseOutside{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                              LITHEVIEW_PREVIEW_POINTER_RELEASED,
                                                              kSecondTabX,
                                                              kHeight + 8)};

    const Frame first =
        renderButtons(context, std::array{kFocus}, "a tab view accepts focus", kTabSnapshot);
    const Frame keyed = renderButtons(
        context, std::array{kFocus, kRight}, "a right key reaches the tab view", kTabSnapshot);
    const Frame pushed = renderButtons(context,
                                       std::array{kFocus, kSelect},
                                       "push-button input reaches the tab view",
                                       kTabSnapshot);
    const Frame tapped = renderButtons(
        context, std::array{kPress, kRelease}, "a tab tap reaches the tab view", kTabSnapshot);
    const Frame cancelled = renderButtons(context,
                                          std::array{kPress, kReleaseOutside},
                                          "a cancelled tab press is accepted",
                                          kTabSnapshot);
    context.expect(keyed != first, "the right key shows the second page");
    context.expect(pushed == keyed, "push-button activation advances to the second tab");
    context.expect(tapped == keyed, "tapping the second tab matches keyboard selection");
    context.expect(cancelled == first, "releasing outside the strip keeps the first page");
}

void testWheelPicker(TestContext& context)
{
    constexpr LitheViewPreviewInput kFocus{makeInput(LITHEVIEW_PREVIEW_INPUT_FOCUS, 1)};
    constexpr LitheViewPreviewInput kPush{makeInput(LITHEVIEW_PREVIEW_INPUT_PUSH_BUTTON, 1)};
    constexpr LitheViewPreviewInput kBackward{makeInput(LITHEVIEW_PREVIEW_INPUT_ENCODER, -1)};
    constexpr LitheViewPreviewInput kTap{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                   LITHEVIEW_PREVIEW_POINTER_PRESSED,
                                                   kWheelX,
                                                   kWheelAboveBandY)};
    constexpr LitheViewPreviewInput kLift{makeInput(LITHEVIEW_PREVIEW_INPUT_POINTER,
                                                    LITHEVIEW_PREVIEW_POINTER_RELEASED,
                                                    kWheelX,
                                                    kWheelAboveBandY)};

    const Frame focused =
        renderButtons(context, std::array{kFocus}, "a wheel accepts focus", kWheelSnapshot);
    const Frame editing = renderButtons(
        context, std::array{kFocus, kPush}, "push-button input reaches the wheel", kWheelSnapshot);
    const Frame confirmed = renderButtons(context,
                                          std::array{kFocus, kPush, kBackward, kPush},
                                          "encoder rotation while editing reaches the wheel",
                                          kWheelSnapshot);
    const Frame tapped = renderButtons(
        context, std::array{kTap, kLift}, "a tap above the band reaches the wheel", kWheelSnapshot);
    context.expect(editing != focused, "editing shows a different band than plain focus");
    context.expect(confirmed != focused, "an encoder step while editing changes the value");
    context.expect(tapped == confirmed,
                   "tapping the row above selects the same item as one step back");
}

} // namespace

int main()
{
    TestContext context{};
    context.expect(litheview_preview_abi_version() == LITHEVIEW_PREVIEW_ABI_VERSION,
                   "preview ABI reports its version");
    testFormats(context);
    testNativePackedFormats(context);
    testCircleAndQuantization(context);
    testInputAndInvalidRequests(context);
    testButtonVariants(context);
    testTabs(context);
    testWheelPicker(context);
    std::cout << "RESULT " << (context.failures == 0U ? "PASS" : "FAIL") << '\n';
    return context.failures == 0U ? 0 : 1;
}
