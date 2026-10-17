// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.DisplayBuild;

public enum TargetEvidenceKind
{
    Calculated,
    Estimated,
    CrossBuildMeasured,
    HardwareMeasured
}

// The metric fixes its unit and scope; a source-file byte count is never a
// substitute for archive text or a whole linked firmware region.
public enum TargetMetric
{
    ArchiveTextBytes,
    InitializedDataBytes,
    BssBytes,
    StackFrameBytes,
    StaticObjectBytes,
    TileWorkspaceAllocatedBytes,
    FramebufferAllocatedBytes,
    InternalFlashBytes,
    InternalRamBytes,
    ExternalRamBytes,
    DamageRegionCount,
    ControllerDepthCount,
    TransferWireBytes,
    TransferDurationMicroseconds
}

public sealed record TargetMetricEvidence
{
    public required TargetMetric Metric { get; init; }
    public required long Value { get; init; }
    public required TargetEvidenceKind Kind { get; init; }
    public required string Source { get; init; }
}

public sealed record TargetTransferLimits
{
    public long? MaximumWireBytes { get; init; }
    public long? MaximumDurationMicroseconds { get; init; }
    public required string Source { get; init; }
}

public sealed record TargetFeasibilityOptions
{
    public int SchemaVersion { get; init; } = 1;
    public string? ExpectedDisplayDefinitionId { get; init; }
    public string? ExpectedFormSha256 { get; init; }
    public string? ExpectedDisplayDefinitionSha256 { get; init; }
    public string? ExpectedLogicalInputSha256 { get; init; }
    public ResourcePlacement? ResourcePlacement { get; init; }
    public TargetMetricEvidence[] Measurements { get; init; } = [];
    public TargetTransferLimits? TransferLimits { get; init; }
}

public enum TargetConstraintUnit
{
    Boolean,
    Bytes,
    Count,
    Microseconds
}

public sealed record TargetFeasibilityConstraint
{
    public required string Id { get; init; }
    public required ValidationStageStatus Status { get; init; }
    public TargetEvidenceKind? EvidenceKind { get; init; }
    public required string EvidenceSource { get; init; }
    public string? LimitSource { get; init; }
    public long? Used { get; init; }
    public long? Maximum { get; init; }
    public required TargetConstraintUnit Unit { get; init; }
    public required bool Required { get; init; }
}

public sealed record TargetFeasibilityResult
{
    public required ValidationStageStatus SurfaceStatus { get; init; }
    public required ValidationStageStatus TargetStatus { get; init; }
    public string? TargetId { get; init; }
    public long? MinimumRenderPayloadBytes { get; init; }
    public required long EstimatedResourceRomBytes { get; init; }
    public required IReadOnlyList<TargetFeasibilityConstraint> Constraints { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}
