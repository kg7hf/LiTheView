// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public static partial class FormSurfaceValidator
{
    public static ProfileDiagnostic? ValidateLayout(
        DisplayDefinitionProfile definition,
        IReadOnlyList<SurfaceLayoutNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(nodes);
        if (DisplayDefinitionValidator.Validate(definition).Any(item =>
            item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return LayoutError("LVSL3001", "$", "Layout requires a valid display definition.");
        }
        if (nodes.Count == 0)
        {
            return LayoutError("LVSL3007", "$.components", "Layout requires one root followed by its descendants.");
        }
        for (int index = 0; index < nodes.Count; ++index)
        {
            SurfaceLayoutNode node = nodes[index];
            string path = $"$.components[{index}].frame";
            if (index == 0 ? node.ParentIndex.HasValue :
                node.ParentIndex is null || node.ParentIndex < 0 || node.ParentIndex >= index)
            {
                return LayoutError("LVSL3007", path, "There must be one first root, and parents must precede children.");
            }
            if (!Enum.IsDefined(node.Placement))
            {
                return LayoutError("LVSL3002", path, "The placement policy is not supported.");
            }
            if (!GeometryFits(node.Frame))
            {
                return LayoutError("LVSL3003", path, "Layout geometry must have positive, representable bounds.");
            }
        }
        for (int index = 0; index < nodes.Count; ++index)
        {
            ProfileDiagnostic? error = nodes[index].Placement == SurfacePlacementPolicy.ClipToViewport
                ? ValidateClippedNode(definition.Geometry, nodes, index)
                : ValidateCompleteNode(definition, nodes, index);
            if (error is not null)
            {
                return error;
            }
        }
        return null;
    }

    private static ProfileDiagnostic? ValidateCompleteNode(
        DisplayDefinitionProfile definition,
        IReadOnlyList<SurfaceLayoutNode> nodes,
        int index)
    {
        SurfaceLayoutNode node = nodes[index];
        long x = node.Frame.X;
        long lowY = node.Frame.Y;
        long highY = lowY;
        int width = node.Frame.Width;
        int height = node.Frame.Height;
        string path = $"$.components[{index}].frame";
        int? parentIndex = node.ParentIndex;
        while (parentIndex is int parent)
        {
            SurfaceLayoutNode ancestor = nodes[parent];
            int contentHeight = EffectiveContentHeight(nodes, parent);
            if (x < 0 || x + width > ancestor.Frame.Width)
            {
                return Unreachable(path);
            }
            lowY = Math.Max(lowY, 0);
            highY = Math.Min(highY, (long)contentHeight - height);
            if (lowY > highY)
            {
                return Unreachable(path);
            }
            if (ancestor.Scrollable)
            {
                lowY -= contentHeight - ancestor.Frame.Height;
            }
            lowY = Math.Max(lowY, 0);
            highY = Math.Min(highY, (long)ancestor.Frame.Height - height);
            if (lowY > highY)
            {
                return Unreachable(path);
            }
            x += ancestor.Frame.X;
            lowY += ancestor.Frame.Y;
            highY += ancestor.Frame.Y;
            if (!TranslatedBoundsFit(x, x + width, lowY, highY + height))
            {
                return LayoutError("LVSL3003", path, "Translated geometry exceeds the logical coordinate range.");
            }
            parentIndex = ancestor.ParentIndex;
        }
        DisplayDefinitionGeometry geometry = definition.Geometry;
        if (node.Placement == SurfacePlacementPolicy.SafeArea)
        {
            SafeAreaProfile safe = geometry.SafeArea;
            if (x < safe.X || x + width > (long)safe.X + safe.Width ||
                Math.Max(lowY, safe.Y) > Math.Min(highY, (long)safe.Y + safe.Height - height))
            {
                return LayoutError("LVSL3004", path, "No reachable position fits the declared safe area.");
            }
            return null;
        }
        long candidateY = Math.Clamp(((long)geometry.Height - height) / 2, lowY, highY);
        return ValidatePlacement(definition,
            new(checked((int)x), checked((int)candidateY), width, height),
            SurfacePlacementPolicy.VisibleShape, path);
    }

    private static ProfileDiagnostic? ValidateClippedNode(
        DisplayDefinitionGeometry geometry,
        IReadOnlyList<SurfaceLayoutNode> nodes,
        int index)
    {
        SurfaceLayoutNode node = nodes[index];
        long left = node.Frame.X;
        long right = left + node.Frame.Width - 1;
        long top = node.Frame.Y;
        long bottom = top + node.Frame.Height - 1;
        string path = $"$.components[{index}].frame";
        int? parentIndex = node.ParentIndex;
        while (parentIndex is int parent)
        {
            SurfaceLayoutNode ancestor = nodes[parent];
            int contentHeight = EffectiveContentHeight(nodes, parent);
            left = Math.Max(left, 0);
            right = Math.Min(right, ancestor.Frame.Width - 1L);
            top = Math.Max(top, 0);
            bottom = Math.Min(bottom, contentHeight - 1L);
            if (left > right || top > bottom)
            {
                return FullyClipped(path);
            }
            if (ancestor.Scrollable)
            {
                top -= contentHeight - ancestor.Frame.Height;
            }
            top = Math.Max(top, 0);
            bottom = Math.Min(bottom, ancestor.Frame.Height - 1L);
            if (top > bottom)
            {
                return FullyClipped(path);
            }
            left += ancestor.Frame.X;
            right += ancestor.Frame.X;
            top += ancestor.Frame.Y;
            bottom += ancestor.Frame.Y;
            if (!TranslatedBoundsFit(left, right + 1, top, bottom + 1))
            {
                return LayoutError("LVSL3003", path, "Translated geometry exceeds the logical coordinate range.");
            }
            parentIndex = ancestor.ParentIndex;
        }
        left = Math.Max(left, 0);
        right = Math.Min(right, geometry.Width - 1L);
        top = Math.Max(top, 0);
        bottom = Math.Min(bottom, geometry.Height - 1L);
        if (left > right || top > bottom)
        {
            return FullyClipped(path);
        }
        int x = checked((int)Math.Clamp(geometry.Width / 2L, left, right));
        int y = checked((int)Math.Clamp(geometry.Height / 2L, top, bottom));
        return ContainsPixel(geometry, x, y) ? null : FullyClipped(path);
    }

    private static int EffectiveContentHeight(IReadOnlyList<SurfaceLayoutNode> nodes, int index)
    {
        SurfaceLayoutNode node = nodes[index];
        if (!node.Scrollable)
        {
            return node.Frame.Height;
        }
        int height = Math.Max(node.Frame.Height, node.ContentHeight);
        for (int child = index + 1; child < nodes.Count; ++child)
        {
            if (nodes[child].ParentIndex == index)
            {
                FormRectangle frame = nodes[child].Frame;
                height = Math.Max(height, frame.Y + frame.Height);
            }
        }
        return height;
    }

    private static bool TranslatedBoundsFit(long left, long right, long top, long bottom) =>
        left >= int.MinValue && top >= int.MinValue && right <= int.MaxValue && bottom <= int.MaxValue;

    private static ProfileDiagnostic Unreachable(string path) =>
        LayoutError("LVSL3008", path, "The whole control cannot be reached within an ancestor viewport or scroll region.");

    private static ProfileDiagnostic FullyClipped(string path) =>
        LayoutError("LVSL3006", path, "Every possible position is clipped by an ancestor or the visible shape.");
}
