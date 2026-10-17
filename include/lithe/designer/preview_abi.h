// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

/*
 * LitheView designer preview ABI, version 1.
 *
 * This header is intentionally a C ABI.  Fixed-width integers, byte buffers,
 * explicit structure sizes, and caller-owned storage are the only data that
 * cross the boundary.  No C++ class layout, allocation, exception, string, or
 * container ABI is exposed to the designer or an extension.
 */

#include <stddef.h>
#include <stdint.h>

#if defined(__cplusplus)
extern "C"
{
#endif

    /*
     * The exported spellings below are part of the language-neutral ABI: C macros,
     * SCREAMING_SNAKE enumerators, snake_case fields/functions, and C enum widths
     * are deliberate compatibility choices rather than C++ implementation style.
     */
    // NOLINTBEGIN(cppcoreguidelines-macro-usage,performance-enum-size,readability-identifier-naming)

#define LITHEVIEW_PREVIEW_ABI_VERSION 1U
#define LITHEVIEW_PREVIEW_SCHEMA_VERSION 1U
#define LITHEVIEW_PREVIEW_MAX_VIEWS 64U
#define LITHEVIEW_PREVIEW_MAX_INPUTS 64U
#define LITHEVIEW_PREVIEW_MAX_DAMAGE_REGIONS 8U
#define LITHEVIEW_PREVIEW_MAX_DIAGNOSTIC_BYTES 512U
#define LITHEVIEW_PREVIEW_MAX_SNAPSHOT_BYTES 1048576U
#define LITHEVIEW_PREVIEW_MAX_SURFACE_DIMENSION 4096U

    typedef enum LitheViewPreviewStatus
    {
        LITHEVIEW_PREVIEW_STATUS_OK = 0,
        LITHEVIEW_PREVIEW_STATUS_INVALID_ARGUMENT = 1,
        LITHEVIEW_PREVIEW_STATUS_ABI_MISMATCH = 2,
        LITHEVIEW_PREVIEW_STATUS_SCHEMA_MISMATCH = 3,
        LITHEVIEW_PREVIEW_STATUS_SIZE_LIMIT = 4,
        LITHEVIEW_PREVIEW_STATUS_UNSUPPORTED_FORMAT = 5,
        LITHEVIEW_PREVIEW_STATUS_INVALID_SNAPSHOT = 6,
        LITHEVIEW_PREVIEW_STATUS_MISSING_RESOURCE = 7,
        LITHEVIEW_PREVIEW_STATUS_INVALID_INPUT = 8,
        LITHEVIEW_PREVIEW_STATUS_OUTPUT_TOO_SMALL = 9,
        LITHEVIEW_PREVIEW_STATUS_INTERNAL_ERROR = 10
    } LitheViewPreviewStatus;

    /*
     * Native packed formats render directly into their own storage; they are never an RGB
     * frame quantized afterwards and accept only LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE.
     * Their width and height are logical emitters (R2) or cells (RRRR/RYRY) and must
     * contain whole storage blocks: R2 uses 2x2 emitters per byte, the cell formats one cell
     * per byte. For every format stride_bytes advances one storage-block row, and a frame
     * occupies (height / block height) * stride_bytes bytes.
     */
    typedef enum LitheViewPreviewPixelFormat
    {
        LITHEVIEW_PREVIEW_PIXEL_RGB565 = 0,
        LITHEVIEW_PREVIEW_PIXEL_RGB888 = 1,
        LITHEVIEW_PREVIEW_PIXEL_XRGB8888 = 2,
        LITHEVIEW_PREVIEW_PIXEL_ARGB8888 = 3,
        LITHEVIEW_PREVIEW_PIXEL_R2_PACKED_2X2 = 4,
        LITHEVIEW_PREVIEW_PIXEL_RRRR2222 = 5,
        LITHEVIEW_PREVIEW_PIXEL_RYRY2222 = 6
    } LitheViewPreviewPixelFormat;

    typedef enum LitheViewPreviewShape
    {
        LITHEVIEW_PREVIEW_SHAPE_RECTANGLE = 0,
        LITHEVIEW_PREVIEW_SHAPE_CIRCLE = 1
    } LitheViewPreviewShape;

    typedef enum LitheViewPreviewQuantization
    {
        LITHEVIEW_PREVIEW_QUANTIZATION_NATIVE = 0,
        LITHEVIEW_PREVIEW_QUANTIZATION_RGB565 = 1,
        LITHEVIEW_PREVIEW_QUANTIZATION_RED_2BPP = 2,
        LITHEVIEW_PREVIEW_QUANTIZATION_RED_YELLOW_2BPP = 3
    } LitheViewPreviewQuantization;

    typedef enum LitheViewPreviewInputKind
    {
        LITHEVIEW_PREVIEW_INPUT_POINTER = 0,
        LITHEVIEW_PREVIEW_INPUT_KEY = 1,
        LITHEVIEW_PREVIEW_INPUT_ENCODER = 2,
        LITHEVIEW_PREVIEW_INPUT_PUSH_BUTTON = 3,
        LITHEVIEW_PREVIEW_INPUT_FOCUS = 4,
        LITHEVIEW_PREVIEW_INPUT_ELAPSED_TIME = 5
    } LitheViewPreviewInputKind;

    typedef enum LitheViewPreviewKey
    {
        LITHEVIEW_PREVIEW_KEY_UP = 0,
        LITHEVIEW_PREVIEW_KEY_DOWN = 1,
        LITHEVIEW_PREVIEW_KEY_LEFT = 2,
        LITHEVIEW_PREVIEW_KEY_RIGHT = 3,
        LITHEVIEW_PREVIEW_KEY_SELECT = 4,
        LITHEVIEW_PREVIEW_KEY_BACK = 5
    } LitheViewPreviewKey;

    typedef enum LitheViewPreviewPointerPhase
    {
        LITHEVIEW_PREVIEW_POINTER_PRESSED = 0,
        LITHEVIEW_PREVIEW_POINTER_MOVED = 1,
        LITHEVIEW_PREVIEW_POINTER_RELEASED = 2,
        LITHEVIEW_PREVIEW_POINTER_CANCELLED = 3
    } LitheViewPreviewPointerPhase;

    typedef struct LitheViewPreviewSurface
    {
        uint32_t struct_size;
        uint32_t pixel_format;
        uint32_t shape;
        uint32_t quantization;
        int32_t width;
        int32_t height;
        uint32_t stride_bytes;
        uint32_t reserved;
    } LitheViewPreviewSurface;

    typedef struct LitheViewPreviewInput
    {
        uint32_t struct_size;
        uint32_t kind;
        int32_t value;
        int32_t x;
        int32_t y;
        uint32_t timestamp_ms;
        uint32_t contact_id;
        uint32_t reserved;
    } LitheViewPreviewInput;

    typedef struct LitheViewPreviewRect
    {
        int32_t x;
        int32_t y;
        int32_t width;
        int32_t height;
    } LitheViewPreviewRect;

    typedef struct LitheViewPreviewRequest
    {
        uint32_t struct_size;
        uint32_t abi_version;
        uint32_t schema_version;
        uint32_t flags;
        LitheViewPreviewSurface surface;
        const uint8_t* snapshot;
        size_t snapshot_size;
        const LitheViewPreviewInput* inputs;
        size_t input_count;
        uint8_t* pixels;
        size_t pixel_capacity;
        LitheViewPreviewRect* damage_regions;
        size_t damage_capacity;
        char* diagnostic;
        size_t diagnostic_capacity;
    } LitheViewPreviewRequest;

    typedef struct LitheViewPreviewResult
    {
        uint32_t struct_size;
        uint32_t status;
        uint32_t diagnostic_code;
        uint32_t flags;
        size_t frame_bytes;
        size_t damage_count;
        uint32_t next_wakeup_ms;
        uint32_t accepted_input_count;
    } LitheViewPreviewResult;

/* Result flag: at least one view animation remains active after this frame. */
#define LITHEVIEW_PREVIEW_RESULT_ANIMATION_RUNNING 0x00000001U
/* Request flag: render through the fixed-height tiled path into the caller-owned frame. */
#define LITHEVIEW_PREVIEW_REQUEST_TILED 0x00000001U

    uint32_t litheview_preview_abi_version(void);

    LitheViewPreviewStatus litheview_preview_render(const LitheViewPreviewRequest* request,
                                                    LitheViewPreviewResult* result);

    // NOLINTEND(cppcoreguidelines-macro-usage,performance-enum-size,readability-identifier-naming)

#if defined(__cplusplus)
}
#endif
