// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public static partial class FormSurfaceValidator
{
    public static SurfacePopupPlacementResult PlacePopup(
        DisplayDefinitionProfile definition,
        FormRectangle anchored,
        SurfacePopupFallback fallback = SurfacePopupFallback.None,
        SurfacePopupSize? centeredFallbackSize = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (DisplayDefinitionValidator.Validate(definition).Any(item =>
            item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return PopupError("LVSL3001", "Popup placement requires a valid display definition.");
        }
        if (!Enum.IsDefined(fallback) || fallback == SurfacePopupFallback.None && centeredFallbackSize.HasValue)
        {
            return PopupError("LVSL3002", "A fallback size requires an explicit supported fallback policy.");
        }
        if (!GeometryFits(anchored) || centeredFallbackSize is SurfacePopupSize specified &&
            (specified.Width <= 0 || specified.Height <= 0))
        {
            return PopupError("LVSL3003", "Popup dimensions and exclusive edges must be positive and representable.");
        }
        SafeAreaProfile safe = definition.Geometry.SafeArea;
        if (Contains(new(safe.X, safe.Y, safe.Width, safe.Height), anchored))
        {
            return new() { Frame = anchored };
        }
        SurfacePopupSize size = centeredFallbackSize ?? new(anchored.Width, anchored.Height);
        if (fallback == SurfacePopupFallback.None || size.Width > safe.Width || size.Height > safe.Height)
        {
            return PopupError("LVSL3012", "Neither the requested popup nor the declared fallback fits the safe area.");
        }
        return new()
        {
            Frame = new(safe.X + (safe.Width - size.Width) / 2,
                safe.Y + (safe.Height - size.Height) / 2, size.Width, size.Height),
            UsedFallback = true
        };
    }

    private static SurfacePopupPlacementResult PopupError(string code, string message) =>
        new() { Diagnostic = LayoutError(code, "$.popup", message) };
}
