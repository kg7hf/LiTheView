// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Preview;

public static class PreviewSurfaceFactory
{
    // The workspace's shared display contract. Version-1 packed-display projects keep their historical
    // RGB565 staging preview; every other project renders its own destination format.
    public static PreviewSurface Create(DesignerDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (display.LegacyStagingSurface is { } legacy)
        {
            return Create(legacy);
        }
        PreviewPixelFormat format = display.Format switch
        {
            SurfacePixelFormat.Rgb565 => PreviewPixelFormat.Rgb565,
            SurfacePixelFormat.Rgb888 => PreviewPixelFormat.Rgb888,
            SurfacePixelFormat.Xrgb8888 => PreviewPixelFormat.Xrgb8888,
            SurfacePixelFormat.Argb8888 => PreviewPixelFormat.Argb8888,
            SurfacePixelFormat.R2Packed2x2 => PreviewPixelFormat.R2Packed2x2,
            SurfacePixelFormat.Rrrr2222 => PreviewPixelFormat.Rrrr2222,
            SurfacePixelFormat.Ryry2222 => PreviewPixelFormat.Ryry2222,
            _ => throw new ArgumentOutOfRangeException(nameof(display))
        };
        PreviewShape shape = display.Shape == SurfaceShape.Circle ? PreviewShape.Circle : PreviewShape.Rectangle;
        // Packed displays render their own storage: whole blocks per row, no RGB staging.
        PreviewSurface surface = new(format, shape, PreviewQuantization.Native, display.Width, display.Height, 0);
        return surface with { StrideBytes = surface.RowBytes };
    }

    public static PreviewSurface Create(DisplaySurfaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        PreviewPixelFormat format = profile.PainterSurface.Format switch
        {
            SurfacePixelFormat.Rgb565 => PreviewPixelFormat.Rgb565,
            SurfacePixelFormat.Rgb888 => PreviewPixelFormat.Rgb888,
            SurfacePixelFormat.Xrgb8888 => PreviewPixelFormat.Xrgb8888,
            SurfacePixelFormat.Argb8888 => PreviewPixelFormat.Argb8888,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        PreviewQuantization quantization = profile.NativeDisplay.ColorModel switch
        {
            NativeColorModel.Rgb => PreviewQuantization.Native,
            NativeColorModel.RedLuminanceSubpixels => PreviewQuantization.Red2Bpp,
            NativeColorModel.RedYellowFullPixel => PreviewQuantization.RedYellow2Bpp,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        PreviewShape shape = profile.Shape == SurfaceShape.Circle
            ? PreviewShape.Circle
            : PreviewShape.Rectangle;
        int bytesPerPixel = format switch
        {
            PreviewPixelFormat.Rgb565 => 2,
            PreviewPixelFormat.Rgb888 => 3,
            PreviewPixelFormat.Xrgb8888 or PreviewPixelFormat.Argb8888 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        return new PreviewSurface(
            format,
            shape,
            quantization,
            profile.PixelSize.Width,
            profile.PixelSize.Height,
            checked(profile.PixelSize.Width * bytesPerPixel));
    }
}
