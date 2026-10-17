// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Preview;

namespace LitheView.Designer.Service;

// This conversion is presentation-only. The native worker renders directly in the
// project's selected surface format, including native packed storage; no reduced-color
// staging is involved. Core's PreviewPresentation is the shared decoder.
internal static class PreviewPixels
{
    public static PreviewImage ToRgba(PreviewFrame frame)
    {
        (int width, int height) = PreviewPresentation.ImageSize(frame.Surface);
        if (checked((long)width * height * 4) > Protocol.MaximumRgbaBytes ||
            frame.Pixels.Length != frame.Surface.RequiredFrameBytes())
        {
            throw new ServiceException("PreviewSizeLimit", "The native preview frame has an invalid or excessive size.");
        }
        return PreviewPresentation.ToRgba(frame);
    }
}
