// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.DisplayBuild;

public static partial class TargetFeasibilityEvaluator
{
    private static void AddKnown(
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics,
        string id, long used, long maximum, TargetConstraintUnit unit,
        TargetEvidenceKind kind, string source, string limitSource, bool permitPass = true)
    {
        ValidationStageStatus status = used > maximum ? ValidationStageStatus.Failed :
            permitPass ? ValidationStageStatus.Passed : ValidationStageStatus.Unevaluated;
        constraints.Add(new()
        {
            Id = id, Status = status, EvidenceKind = kind, EvidenceSource = source,
            LimitSource = limitSource, Used = used, Maximum = maximum, Unit = unit, Required = true
        });
        if (status == ValidationStageStatus.Failed)
        {
            diagnostics.Add(Error("LVTF1101", $"$.constraints.{id}",
                $"'{id}' requires {used} {unit} and exceeds the declared limit of {maximum} {unit}."));
        }
    }

    private static void AddBoolean(
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics,
        string id, bool compatible, string source, string limitSource)
    {
        constraints.Add(new()
        {
            Id = id, Status = compatible ? ValidationStageStatus.Passed : ValidationStageStatus.Failed,
            EvidenceKind = TargetEvidenceKind.Calculated, EvidenceSource = source,
            LimitSource = limitSource, Unit = TargetConstraintUnit.Boolean, Required = true
        });
        if (!compatible)
        {
            diagnostics.Add(Error("LVTF1101", $"$.constraints.{id}", $"The selected target is incompatible with '{id}'."));
        }
    }

    private static void AddUnknown(
        List<TargetFeasibilityConstraint> constraints, string id, TargetConstraintUnit unit,
        long? maximum, string source, string? limitSource, bool required)
    {
        constraints.Add(new()
        {
            Id = id, Status = ValidationStageStatus.Unevaluated, EvidenceSource = source,
            LimitSource = limitSource, Maximum = maximum, Unit = unit, Required = required
        });
    }

    private static void AddCapacity(
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics,
        string id, int count, int maximum, string source) =>
        AddKnown(constraints, diagnostics, id, count, maximum, TargetConstraintUnit.Count,
            TargetEvidenceKind.Calculated, source, $"target.capacities.{id}");

    private static void AddMeasuredCapacity(
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics,
        Dictionary<TargetMetric, TargetMetricEvidence> evidence,
        TargetMetric metric, string id, int maximum)
    {
        if (evidence.TryGetValue(metric, out TargetMetricEvidence? known))
        {
            AddKnown(constraints, diagnostics, id, known.Value, maximum, TargetConstraintUnit.Count,
                known.Kind, known.Source, $"target.capacities.{id}", known.Kind != TargetEvidenceKind.Estimated);
        }
        else
        {
            AddUnknown(constraints, id, TargetConstraintUnit.Count, maximum,
                "Application behavior evidence is absent; view nesting does not determine this capacity.",
                $"target.capacities.{id}", required: true);
        }
    }

    private static void AddMeasuredBudget(
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics,
        Dictionary<TargetMetric, TargetMetricEvidence> evidence,
        TargetMetric metric, string id, BudgetLimit? limit)
    {
        if (limit is null)
        {
            AddUnknown(constraints, id, TargetConstraintUnit.Bytes, null,
                "No corresponding target budget is configured.", null, required: false);
            return;
        }
        if (!evidence.TryGetValue(metric, out TargetMetricEvidence? known))
        {
            AddUnknown(constraints, id, TargetConstraintUnit.Bytes, limit.MaximumBytes,
                "Matching typed cost evidence has not been supplied; source text and resource estimates are not substitutes.",
                limit.Source, required: true);
            return;
        }
        bool measured = known.Kind is TargetEvidenceKind.CrossBuildMeasured or TargetEvidenceKind.HardwareMeasured;
        bool needsMeasurement = limit.Enforcement is BudgetEnforcement.ArchiveGate or BudgetEnforcement.BuildGate or
            BudgetEnforcement.CompilerDiagnostic or BudgetEnforcement.LinkerRegion;
        bool permitPass = limit.Enforcement == BudgetEnforcement.AdvisoryEstimate || measured ||
            known.Kind == TargetEvidenceKind.Calculated && !needsMeasurement;
        AddKnown(constraints, diagnostics, id, known.Value, limit.MaximumBytes, TargetConstraintUnit.Bytes,
            known.Kind, known.Source, limit.Source, permitPass);
    }

    private static void AddTransfer(
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics,
        Dictionary<TargetMetric, TargetMetricEvidence> evidence, TargetTransferLimits? limits)
    {
        AddTransferMetric(constraints, diagnostics, evidence, TargetMetric.TransferWireBytes,
            "transfer-wire-bytes", TargetConstraintUnit.Bytes, limits?.MaximumWireBytes, limits?.Source);
        AddTransferMetric(constraints, diagnostics, evidence, TargetMetric.TransferDurationMicroseconds,
            "transfer-duration", TargetConstraintUnit.Microseconds, limits?.MaximumDurationMicroseconds, limits?.Source);
    }

    private static void AddTransferMetric(
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics,
        Dictionary<TargetMetric, TargetMetricEvidence> evidence, TargetMetric metric,
        string id, TargetConstraintUnit unit, long? maximum, string? limitSource)
    {
        if (!maximum.HasValue || !evidence.TryGetValue(metric, out TargetMetricEvidence? known))
        {
            AddUnknown(constraints, id, unit, maximum,
                "Transfer requires explicit integration limits and matching evidence; no bus or protocol is inferred.",
                limitSource, maximum.HasValue);
            return;
        }
        bool permitPass = known.Kind == TargetEvidenceKind.HardwareMeasured ||
            metric == TargetMetric.TransferWireBytes &&
                known.Kind is TargetEvidenceKind.Calculated or TargetEvidenceKind.CrossBuildMeasured;
        AddKnown(constraints, diagnostics, id, known.Value, maximum.Value, unit,
            known.Kind, known.Source, limitSource!, permitPass);
    }
}
