// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public static partial class FormSurfaceValidator
{
    // This deliberately mirrors the portable surface_layout.hpp contract.
    // Failures are values; the shared fixture fixes their stable identities.
    public static ProfileDiagnostic? ValidatePlacement(
        DisplayDefinitionProfile definition,
        FormRectangle rectangle,
        SurfacePlacementPolicy policy,
        string path = "$.frame")
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (DisplayDefinitionValidator.Validate(definition).Any(item =>
            item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return LayoutError("LVSL3001", path, "Placement requires a valid display definition.");
        }
        if (!Enum.IsDefined(policy))
        {
            return LayoutError("LVSL3002", path, "The placement policy is not supported.");
        }
        if (!GeometryFits(rectangle))
        {
            return LayoutError("LVSL3003", path,
                "Placement requires positive dimensions and representable exclusive edges.");
        }
        DisplayDefinitionGeometry geometry = definition.Geometry;
        if (policy == SurfacePlacementPolicy.SafeArea)
        {
            SafeAreaProfile safe = geometry.SafeArea;
            return Contains(new(safe.X, safe.Y, safe.Width, safe.Height), rectangle) ? null :
                LayoutError("LVSL3004", path, "The rectangle does not fit the declared safe area.");
        }
        if (policy == SurfacePlacementPolicy.VisibleShape)
        {
            return ContainsPixel(geometry, rectangle.X, rectangle.Y) &&
                ContainsPixel(geometry, rectangle.X + rectangle.Width - 1, rectangle.Y) &&
                ContainsPixel(geometry, rectangle.X, rectangle.Y + rectangle.Height - 1) &&
                ContainsPixel(geometry, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1)
                ? null : LayoutError("LVSL3005", path,
                    "Every corner pixel center must lie inside the visible shape.");
        }
        return HasVisiblePixel(geometry, rectangle) ? null :
            LayoutError("LVSL3006", path, "The rectangle is completely clipped by the visible shape.");
    }

    public static ProfileDiagnostic? ValidateInputRequirements(
        LogicalInputConfigurationProfile input,
        SurfaceInputRequirements requirements,
        string path = "$.inputRequirements")
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(requirements);
        if (requirements.Activation && !(input.Pointer || input.Keys || input.Select))
        {
            return LayoutError("LVSL3009", path, "Activation requires pointer input, keys, or logical select.");
        }
        if (requirements.Navigation && !(input.Pointer || input.Keys || input.Encoder))
        {
            return LayoutError("LVSL3010", path, "Navigation requires pointer input, keys, or an encoder.");
        }
        return requirements.Text && !input.Text
            ? LayoutError("LVSL3011", path, "Text entry requires logical text input.") : null;
    }

    private static ProfileDiagnostic LayoutError(string code, string path, string message) =>
        new(code, ProfileDiagnosticSeverity.Error, path, message);

    private static bool GeometryFits(FormRectangle rectangle) =>
        rectangle.Width > 0 && rectangle.Height > 0 &&
        (long)rectangle.X + rectangle.Width <= int.MaxValue &&
        (long)rectangle.Y + rectangle.Height <= int.MaxValue;

    private static bool Contains(FormRectangle outer, FormRectangle inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y &&
        (long)inner.X + inner.Width <= (long)outer.X + outer.Width &&
        (long)inner.Y + inner.Height <= (long)outer.Y + outer.Height;

    private static bool ContainsPixel(DisplayDefinitionGeometry geometry, int x, int y)
    {
        if (x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height)
        {
            return false;
        }
        if (geometry.Shape != SurfaceShape.Circle)
        {
            return true;
        }
        long dx = (2L * x) + 1 - geometry.Width;
        long dy = (2L * y) + 1 - geometry.Height;
        return dx * dx + dy * dy <= (long)geometry.Width * geometry.Width;
    }

    private static bool HasVisiblePixel(DisplayDefinitionGeometry geometry, FormRectangle rectangle)
    {
        int left = Math.Max(0, rectangle.X);
        int top = Math.Max(0, rectangle.Y);
        int right = Math.Min(geometry.Width, rectangle.X + rectangle.Width);
        int bottom = Math.Min(geometry.Height, rectangle.Y + rectangle.Height);
        if (left >= right || top >= bottom)
        {
            return false;
        }
        // The pixel nearest the circle center is sufficient: if it is outside,
        // every other pixel in this rectangular intersection is farther away.
        return ContainsPixel(geometry,
            Math.Clamp(geometry.Width / 2, left, right - 1),
            Math.Clamp(geometry.Height / 2, top, bottom - 1));
    }
}
