# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

"""Pack the checked-in gallop sheets into deterministic C++ animation resources."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


GRID_COLUMNS = 4
GRID_ROWS = 2
# The checked-in demo intentionally favors visual fidelity. Memory-constrained
# products can regenerate at 64x48 or 32x24 without changing the resource API.
# Alpha1 costs width*height/8 bytes per frame; indexed4 costs width*height/2.
# Changing these constants and regenerating both headers is the only geometry
# change required by GallopResources because each Bitmap descriptor takes its
# dimensions and stride from the generated constants.
FRAME_WIDTH = 96
FRAME_HEIGHT = 72
ALPHA_THRESHOLD = 48
PALETTE = (
    (0, 0, 0, 0),
    (20, 21, 25, 255),
    (44, 45, 53, 255),
    (84, 34, 14, 255),
    (125, 50, 15, 255),
    (169, 74, 18, 255),
    (201, 105, 28, 255),
    (230, 165, 72, 255),
    (245, 201, 106, 255),
    (43, 183, 173, 255),
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--alpha-sheet", type=Path, required=True)
    parser.add_argument("--color-sheet", type=Path, required=True)
    parser.add_argument("--alpha-header", type=Path, required=True)
    parser.add_argument("--indexed-header", type=Path, required=True)
    return parser.parse_args()


def fnv1a(payload: bytes) -> int:
    checksum = 0x811C9DC5
    for value in payload:
        checksum ^= value
        checksum = (checksum * 0x01000193) & 0xFFFFFFFF
    return checksum


def load_cells(path: Path) -> list[Image.Image]:
    sheet = Image.open(path).convert("RGBA")
    if sheet.width % GRID_COLUMNS != 0 or sheet.height % GRID_ROWS != 0:
        raise ValueError(f"{path} does not divide into a 4 by 2 grid")
    cell_width = sheet.width // GRID_COLUMNS
    cell_height = sheet.height // GRID_ROWS
    return [
        sheet.crop(
            (
                (index % GRID_COLUMNS) * cell_width,
                (index // GRID_COLUMNS) * cell_height,
                ((index % GRID_COLUMNS) + 1) * cell_width,
                ((index // GRID_COLUMNS) + 1) * cell_height,
            )
        )
        for index in range(GRID_COLUMNS * GRID_ROWS)
    ]


def resized_alpha(cell: Image.Image) -> Image.Image:
    return cell.getchannel("A").resize(
        (FRAME_WIDTH, FRAME_HEIGHT), Image.Resampling.LANCZOS
    )


def pack_alpha1(cell: Image.Image) -> bytes:
    alpha = resized_alpha(cell)
    stride = (FRAME_WIDTH + 7) // 8
    payload = bytearray(stride * FRAME_HEIGHT)
    for y in range(FRAME_HEIGHT):
        for x in range(FRAME_WIDTH):
            if alpha.getpixel((x, y)) >= ALPHA_THRESHOLD:
                payload[(y * stride) + (x // 8)] |= 0x80 >> (x % 8)
    return bytes(payload)


def nearest_palette_index(red: int, green: int, blue: int) -> int:
    best_index = 1
    best_distance = 3 * 255 * 255
    for index, color in enumerate(PALETTE[1:], start=1):
        distance = (
            ((red - color[0]) * (red - color[0]))
            + ((green - color[1]) * (green - color[1]))
            + ((blue - color[2]) * (blue - color[2]))
        )
        if distance < best_distance:
            best_index = index
            best_distance = distance
    return best_index


def pack_indexed4(alpha_cell: Image.Image, color_cell: Image.Image) -> bytes:
    alpha = resized_alpha(alpha_cell)
    color = color_cell.resize((FRAME_WIDTH, FRAME_HEIGHT), Image.Resampling.LANCZOS)
    stride = (FRAME_WIDTH + 1) // 2
    payload = bytearray(stride * FRAME_HEIGHT)
    for y in range(FRAME_HEIGHT):
        for x in range(FRAME_WIDTH):
            if alpha.getpixel((x, y)) < ALPHA_THRESHOLD:
                continue
            red, green, blue, _ = color.getpixel((x, y))
            index = nearest_palette_index(red, green, blue)
            offset = (y * stride) + (x // 2)
            if x % 2 == 0:
                payload[offset] |= index << 4
            else:
                payload[offset] |= index
    return bytes(payload)


def byte_rows(frames: list[bytes]) -> str:
    rows: list[str] = []
    for frame_index, frame in enumerate(frames):
        rows.append(f"    // Frame {frame_index}")
        for offset in range(0, len(frame), 12):
            values = ", ".join(f"0x{value:02x}" for value in frame[offset : offset + 12])
            rows.append(f"    {values},")
        rows.append("")
    return "\n".join(rows).rstrip()


def write_alpha_header(path: Path, frames: list[bytes]) -> None:
    payload = b"".join(frames)
    stride = (FRAME_WIDTH + 7) // 8
    frame_bytes = stride * FRAME_HEIGHT
    text = f"""#pragma once

#include <array>
#include <cstddef>
#include <cstdint>

namespace lithe::demo::assets
{{

inline constexpr std::size_t kGallopFrameCount{{8U}};
inline constexpr std::uint16_t kGallopFrameWidth{{{FRAME_WIDTH}U}};
inline constexpr std::uint16_t kGallopFrameHeight{{{FRAME_HEIGHT}U}};
inline constexpr std::uint16_t kGallopFrameStrideBytes{{{stride}U}};
inline constexpr std::size_t kGallopFrameBytes{{
    std::size_t{{kGallopFrameStrideBytes}} * kGallopFrameHeight}};
inline constexpr std::uint32_t kGallopFramesChecksum{{0x{fnv1a(payload):08x}U}};

// These eight frames are the ROM-ready form of
// tests/rendering/assets/animations/gallop_sprite_sheet.png. The source sheet is a 4-by-2 grid of transparent
// 128-by-96 cells. Each cell is reduced to a {FRAME_WIDTH}-by-{FRAME_HEIGHT} alpha mask and packed in
// display order: eight pixels per byte, most-significant bit first. A set bit selects the Bitmap
// tint at render time; a clear bit leaves the destination unchanged. The animation is independent
// of the framebuffer color format and contains no embedded color table.
//
// Keeping the decoded masks in constexpr storage is intentional for small embedded targets. The
// application does not need a filesystem, image decoder, heap allocation, or startup conversion.
// Regenerate this file with tools/generate_gallop_frames.py; update the checksum and resource tests
// as one reviewed change.
inline constexpr std::array<std::uint8_t, kGallopFrameCount * kGallopFrameBytes> kGallopBits{{
{byte_rows(frames)}
}};

static_assert(kGallopBits.size() == kGallopFrameCount * kGallopFrameBytes);

}} // namespace lithe::demo::assets
"""
    path.write_text(text, encoding="utf-8", newline="\n")


def write_indexed_header(path: Path, frames: list[bytes]) -> None:
    payload = b"".join(frames)
    stride = (FRAME_WIDTH + 1) // 2
    frame_bytes = stride * FRAME_HEIGHT
    palette_rows = ["    Color::transparent(),"]
    palette_rows.extend(
        f"    Color::rgb(0x{red:02x}{green:02x}{blue:02x}U),"
        for red, green, blue, _ in PALETTE[1:]
    )
    text = f"""#pragma once

#include "gallop_frames.hpp"

#include "lithe/types.hpp"

#include <array>
#include <cstddef>
#include <cstdint>

namespace lithe::demo::assets
{{

inline constexpr std::uint16_t kGallopIndexed4FrameStrideBytes{{{stride}U}};
inline constexpr std::size_t kGallopIndexed4FrameBytes{{
    std::size_t{{kGallopIndexed4FrameStrideBytes}} * kGallopFrameHeight}};
inline constexpr std::uint32_t kGallopIndexed4FramesChecksum{{0x{fnv1a(payload):08x}U}};

// Indexed4 stores two palette indexes per byte. The high nibble is the left pixel and the low
// nibble is the right pixel. Palette entry zero is transparent by this asset's choice, not by the
// BitmapFormat contract. The palette and payload are immutable borrowed storage and must outlive
// every Bitmap that refers to them. Regenerate both arrays with tools/generate_gallop_frames.py.
inline constexpr std::array<Color, {len(PALETTE)}> kGallopPalette{{
{chr(10).join(palette_rows)}
}};

inline constexpr std::array<std::uint8_t,
                            kGallopFrameCount * kGallopIndexed4FrameBytes>
    kGallopIndexed4Bits{{
{byte_rows(frames)}
}};

static_assert(kGallopIndexed4Bits.size() ==
              kGallopFrameCount * kGallopIndexed4FrameBytes);

}} // namespace lithe::demo::assets
"""
    path.write_text(text, encoding="utf-8", newline="\n")


def main() -> None:
    arguments = parse_arguments()
    alpha_cells = load_cells(arguments.alpha_sheet)
    color_cells = load_cells(arguments.color_sheet)
    alpha_frames = [pack_alpha1(cell) for cell in alpha_cells]
    indexed_frames = [
        pack_indexed4(alpha_cell, color_cell)
        for alpha_cell, color_cell in zip(alpha_cells, color_cells, strict=True)
    ]
    write_alpha_header(arguments.alpha_header, alpha_frames)
    write_indexed_header(arguments.indexed_header, indexed_frames)
    print(
        f"alpha1 bytes={sum(map(len, alpha_frames))} "
        f"fnv1a=0x{fnv1a(b''.join(alpha_frames)):08x}"
    )
    print(
        f"indexed4 bytes={sum(map(len, indexed_frames))} "
        f"fnv1a=0x{fnv1a(b''.join(indexed_frames)):08x}"
    )


if __name__ == "__main__":
    main()
