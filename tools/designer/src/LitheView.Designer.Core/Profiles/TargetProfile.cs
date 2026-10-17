// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public enum TargetArchitecture
{
    Win32X64,
    CortexM33,
    CortexM7
}

public enum OperatingEnvironment
{
    Windows,
    BareMetal,
    FreeRtos
}

public enum RenderingMode
{
    FullFrame,
    Tiled
}

public enum ResourcePlacement
{
    InternalFlash,
    ExternalQspi,
    HostFile,
    ApplicationProvider
}

public enum ResourceFormat
{
    Alpha1,
    Alpha8,
    Indexed4,
    Rgb565,
    Rgb888,
    Xrgb8888,
    Argb8888,
    R2Packed2x2
}

public enum BudgetEnforcement
{
    ArchiveGate,
    BuildGate,
    CompilerDiagnostic,
    LinkerRegion,
    CompileTimeAssertion,
    AdvisoryEstimate
}

public sealed record TargetProfileBaseReference
{
    public required string ProfileId { get; init; }

    public required string Sha256 { get; init; }

    public required string[] ExplicitOverrides { get; init; }
}

public sealed record SurfaceCompatibilityProfile
{
    public required string Family { get; init; }

    public required SurfacePixelFormat[] PainterFormats { get; init; }
}

public sealed record TileGeometry
{
    public required int Width { get; init; }

    public required int Height { get; init; }
}

public sealed record TargetRuntimeProfile
{
    public required TargetArchitecture Architecture { get; init; }

    public required OperatingEnvironment Environment { get; init; }

    public required RenderingMode RenderingMode { get; init; }

    public required TileGeometry? Tile { get; init; }
}

public sealed record RuntimeCapacityProfile
{
    public required int MaxChildrenPerView { get; init; }

    public required int MaxDamageRegions { get; init; }

    public required int MaxControllerDepth { get; init; }

    public required int MaxFocusableViews { get; init; }
}

public sealed record BudgetLimit
{
    public required long MaximumBytes { get; init; }

    public required BudgetEnforcement Enforcement { get; init; }

    public required string Source { get; init; }
}

public sealed record TargetBudgetProfile
{
    public required BudgetLimit? ArchiveText { get; init; }

    public required BudgetLimit? InitializedData { get; init; }

    public required BudgetLimit? Bss { get; init; }

    public required BudgetLimit? StackFrame { get; init; }

    public required BudgetLimit? StaticObject { get; init; }

    public required BudgetLimit? TileWorkspace { get; init; }

    public required BudgetLimit? Framebuffer { get; init; }

    public required BudgetLimit? InternalFlash { get; init; }

    public required BudgetLimit? InternalRam { get; init; }

    public required BudgetLimit? ExternalRam { get; init; }
}

public sealed record ResourceStorageProfile
{
    public required ResourcePlacement DefaultPlacement { get; init; }

    public required ResourcePlacement[] AllowedPlacements { get; init; }

    public required int AlignmentBytes { get; init; }
}

public sealed record TargetProfileProvenance
{
    public required string[] SourcePaths { get; init; }

    public required string Notes { get; init; }
}

public sealed record TargetProfile
{
    [System.Text.Json.Serialization.JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public required int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string CompatibilityFamily { get; init; }

    public required TargetProfileBaseReference? BaseProfile { get; init; }

    public required SurfaceCompatibilityProfile SurfaceCompatibility { get; init; }

    public required TargetRuntimeProfile Runtime { get; init; }

    public required RuntimeCapacityProfile Capacities { get; init; }

    public required TargetBudgetProfile Budgets { get; init; }

    public required ResourceStorageProfile ResourceStorage { get; init; }

    public required ResourceFormat[] SupportedResourceFormats { get; init; }

    public required TargetProfileProvenance Provenance { get; init; }
}
