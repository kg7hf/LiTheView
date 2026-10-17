// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Documents;

// These policies describe logical layout decisions, not a driver or an MCU.
// They are an application sidecar so existing form XML remains unchanged.
public enum SurfacePlacementPolicy
{
    SafeArea,
    VisibleShape,
    ClipToViewport
}

// Catalog input capabilities describe available widget handlers. They do not
// require every listed device. Applications declare the operations they need.
public sealed record SurfaceInputRequirements
{
    public bool Activation { get; init; }
    public bool Navigation { get; init; }
    public bool Text { get; init; }
}

public sealed record FormComponentPlacementPolicy
{
    public required string ComponentId { get; init; }
    public required SurfacePlacementPolicy Placement { get; init; }
}

public sealed record FormSurfacePolicy
{
    public SurfaceInputRequirements InputRequirements { get; init; } = new();
    public FormComponentPlacementPolicy[] Components { get; init; } = [];
}

// A small logical projection of a resolved form or borrowed ViewSpec array.
// Initial scroll offset, visibility, and enabled state do not change whether
// the declared geometry can be reached at some valid scroll position.
public readonly record struct SurfaceLayoutNode(
    int? ParentIndex,
    FormRectangle Frame,
    SurfacePlacementPolicy Placement,
    bool Scrollable = false,
    int ContentHeight = 0);

public enum SurfacePopupFallback
{
    None,
    CenteredSafeArea
}

public readonly record struct SurfacePopupSize(int Width, int Height);

public sealed record SurfacePopupPlacementResult
{
    public FormRectangle? Frame { get; init; }
    public bool UsedFallback { get; init; }
    public Profiles.ProfileDiagnostic? Diagnostic { get; init; }
    public bool IsValid => Frame.HasValue && Diagnostic is null;
}
