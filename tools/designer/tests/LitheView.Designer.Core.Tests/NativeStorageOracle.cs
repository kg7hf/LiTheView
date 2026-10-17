// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Preview;

namespace LitheView.Designer.Core.Tests;

// An independent statement of the native packing contract, used to check renderer output
// without reading it back through LitheView code. Fills are opaque rectangles in logical
// units (emitters for R2, cells for RRRR/RYRY); later fills cover earlier ones.
internal static class NativeStorageOracle
{
    internal sealed record Fill(int X, int Y, int Width, int Height, int Red, int Green);

    // Row-major fields of one storage byte: TL bits 3:2, TR 7:6, BL 1:0, BR 5:4.
    private static readonly int[] FieldShift = [2, 6, 0, 4];

    public static byte[] Expected(PreviewPixelFormat format, int width, int height, int stride,
        IReadOnlyList<Fill> scene)
    {
        bool emitters = format == PreviewPixelFormat.R2Packed2x2;
        int columns = emitters ? width / 2 : width;
        int rows = emitters ? height / 2 : height;
        byte[] expected = new byte[stride * rows];
        for (int row = 0; row < rows; ++row)
        {
            for (int column = 0; column < columns; ++column)
            {
                int encoded = 0;
                for (int field = 0; field < 4; ++field)
                {
                    int level;
                    if (emitters)
                    {
                        // R2 stores one red level per emitter of the 2x2 block.
                        level = Level(ColorAt(scene, (column * 2) + (field % 2), (row * 2) + (field / 2)).Red);
                    }
                    else
                    {
                        // RRRR repeats the red level; RYRY stores yellow, min(red, green), at
                        // TL/BR and the remaining red at TR/BL.
                        Fill cell = ColorAt(scene, column, row);
                        bool redYellow = format == PreviewPixelFormat.Ryry2222;
                        int yellow = redYellow ? Math.Min(cell.Red, cell.Green) : cell.Red;
                        bool yellowField = field is 0 or 3;
                        level = Level(yellowField ? yellow : redYellow ? cell.Red - yellow : cell.Red);
                    }
                    encoded |= level << FieldShift[field];
                }
                expected[(row * stride) + column] = (byte)encoded;
            }
        }
        return expected;
    }

    private static int Level(int channel) => ((channel * 3) + 127) / 255;

    private static Fill ColorAt(IReadOnlyList<Fill> scene, int x, int y) =>
        scene.Last(fill => x >= fill.X && x < fill.X + fill.Width && y >= fill.Y && y < fill.Y + fill.Height);
}
