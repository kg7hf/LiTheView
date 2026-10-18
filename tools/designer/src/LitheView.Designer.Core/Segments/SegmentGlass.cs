// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Segments;

// Host geometry is deliberately separate from logical segment storage and MCU wiring.
public sealed record SegmentPoint(int X, int Y);
public sealed record SegmentShape(int Id, SegmentPoint[] Polygon);
public sealed record SegmentIcon(string Id, int[] Segments);
public sealed record SegmentGroup(string Id, int[] Segments, int Maximum);
public sealed record SegmentGlyph(string Character, int[] Strokes);
public sealed record SegmentText(string Id, int[][] Cells, SegmentGlyph[] Glyphs);
public sealed record SegmentNumber(string Id, int[][] Cells, SegmentGlyph[] Glyphs);

public sealed record SegmentGlass(
    int SchemaVersion,
    string Id,
    int SegmentCount,
    int ViewWidth,
    int ViewHeight,
    SegmentShape[] Shapes,
    SegmentIcon[] Icons,
    SegmentGroup[] Groups,
    SegmentText[] Texts,
    SegmentNumber[] Numbers);

public sealed record SegmentBinding(string Field, string Input, string? Preview = null);
public sealed record SegmentForm(string Id, string GlassId, string GlassSha256, SegmentBinding[] Bindings);
