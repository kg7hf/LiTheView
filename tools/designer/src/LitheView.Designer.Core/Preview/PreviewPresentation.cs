// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Preview;

public sealed record PreviewImage(int Width, int Height, byte[] Rgba);

// The final presentation boundary. The worker returns completed native storage; this only
// expands it to straight RGBA for the editor's displayed image. Packed formats are shown at
// emitter resolution, one opaque pixel per 2-bit emitter level: a 608 by 512 R2 emitter
// surface is 608 by 512 pixels and a 304 by 256 cell surface is 608 by 512 pixels.
public static class PreviewPresentation
{
    private const int EmitterLevelScale = 85;

    public static (int Width, int Height) ImageSize(PreviewSurface surface) =>
        surface.PixelFormat is PreviewPixelFormat.Rrrr2222 or PreviewPixelFormat.Ryry2222
            ? (checked(surface.Width * 2), checked(surface.Height * 2))
            : (surface.Width, surface.Height);

    public static PreviewImage ToRgba(PreviewFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        PreviewSurface surface = frame.Surface;
        if (frame.Pixels.Length != surface.RequiredFrameBytes())
        {
            throw new ArgumentException("The preview frame does not match its surface storage.", nameof(frame));
        }
        (int width, int height) = ImageSize(surface);
        byte[] rgba = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                WritePixel(frame.Pixels, surface, x, y, rgba.AsSpan(((y * width) + x) * 4, 4));
            }
        }
        return new PreviewImage(width, height, rgba);
    }

    private static void WritePixel(byte[] pixels, PreviewSurface surface, int x, int y, Span<byte> target)
    {
        int row = y / surface.BlockHeight;
        switch (surface.PixelFormat)
        {
            case PreviewPixelFormat.Rgb565:
            {
                int source = (row * surface.StrideBytes) + (x * 2);
                int packed = pixels[source] | (pixels[source + 1] << 8);
                Opaque(target, ExpandFiveBits((packed >> 11) & 31), ExpandSixBits((packed >> 5) & 63),
                    ExpandFiveBits(packed & 31));
                break;
            }
            case PreviewPixelFormat.Rgb888:
            {
                int source = (row * surface.StrideBytes) + (x * 3);
                Opaque(target, pixels[source], pixels[source + 1], pixels[source + 2]);
                break;
            }
            case PreviewPixelFormat.Xrgb8888:
            case PreviewPixelFormat.Argb8888:
            {
                int source = (row * surface.StrideBytes) + (x * 4);
                Opaque(target, pixels[source + 2], pixels[source + 1], pixels[source]);
                if (surface.PixelFormat == PreviewPixelFormat.Argb8888)
                {
                    target[3] = pixels[source + 3];
                }
                break;
            }
            case PreviewPixelFormat.R2Packed2x2:
            {
                // Emitter (x, y) is field (y % 2, x % 2) of the byte for its 2x2 block.
                byte level = Level(pixels[(row * surface.StrideBytes) + (x / 2)], ((y % 2) * 2) + (x % 2));
                Opaque(target, level, 0, 0);
                break;
            }
            case PreviewPixelFormat.Rrrr2222:
            case PreviewPixelFormat.Ryry2222:
            {
                // The image doubles each axis; (x, y) is emitter (x % 2, y % 2) of cell (x / 2, y / 2).
                int field = ((y % 2) * 2) + (x % 2);
                byte level = Level(pixels[((y / 2) * surface.StrideBytes) + (x / 2)], field);
                bool yellow = surface.PixelFormat == PreviewPixelFormat.Ryry2222 && field is 0 or 3;
                Opaque(target, level, yellow ? level : (byte)0, 0);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(surface), "The preview format has no presentation adapter.");
        }
    }

    // Row-major fields of a storage byte: TL bits 3:2, TR 7:6, BL 1:0, BR 5:4.
    private static byte Level(byte encoded, int field)
    {
        int shift = ((field % 2) * 4) + (field < 2 ? 2 : 0);
        return (byte)(((encoded >> shift) & 3) * EmitterLevelScale);
    }

    private static void Opaque(Span<byte> target, byte red, byte green, byte blue)
    {
        target[0] = red;
        target[1] = green;
        target[2] = blue;
        target[3] = byte.MaxValue;
    }

    private static byte ExpandFiveBits(int value) => (byte)((value << 3) | (value >> 2));

    private static byte ExpandSixBits(int value) => (byte)((value << 2) | (value >> 4));
}
