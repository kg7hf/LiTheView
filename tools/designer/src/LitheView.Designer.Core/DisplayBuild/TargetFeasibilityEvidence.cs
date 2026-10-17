// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.DisplayBuild;

public static partial class TargetFeasibilityEvaluator
{
    private static Dictionary<TargetMetric, TargetMetricEvidence> ValidateOptions(
        TargetProfile target, DisplayApplicationCompilationResult surface,
        TargetFeasibilityOptions options, List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<TargetMetric, TargetMetricEvidence> evidence = [];
        if (options.SchemaVersion != 1 ||
            options.ExpectedDisplayDefinitionId is not null && !ProfileRules.IsValidIdentifier(options.ExpectedDisplayDefinitionId) ||
            options.ExpectedFormSha256 is not null &&
                (!ProfileRules.IsLowercaseSha256(options.ExpectedFormSha256) || options.ExpectedFormSha256 != surface.FormSha256) ||
            options.ExpectedDisplayDefinitionSha256 is not null &&
                (!ProfileRules.IsLowercaseSha256(options.ExpectedDisplayDefinitionSha256) ||
                 options.ExpectedDisplayDefinitionSha256 != surface.DisplayDefinitionSha256) ||
            options.ExpectedLogicalInputSha256 is not null &&
                (!ProfileRules.IsLowercaseSha256(options.ExpectedLogicalInputSha256) ||
                 options.ExpectedLogicalInputSha256 != surface.LogicalInputSha256) ||
            options.ResourcePlacement.HasValue && !Enum.IsDefined(options.ResourcePlacement.Value))
        {
            diagnostics.Add(Error("LVTF1004", "$.options", "Evidence options require schema version 1, supported values, and matching explicit form identity."));
        }
        if (options.TransferLimits is TargetTransferLimits transfer &&
            (!ValidSource(transfer.Source) || transfer.MaximumWireBytes is <= 0 ||
             transfer.MaximumDurationMicroseconds is <= 0 ||
             transfer.MaximumWireBytes is null && transfer.MaximumDurationMicroseconds is null))
        {
            diagnostics.Add(Error("LVTF1004", "$.options.transferLimits", "Transfer limits require a source and at least one positive limit with explicit units."));
        }
        if (BudgetMetrics(target).Any(item => item.Limit is not null && !Enum.IsDefined(item.Limit.Enforcement)))
        {
            diagnostics.Add(Error("LVTF1002", "$.target.budgets", "Budget enforcement must be supported."));
        }
        if (options.Measurements is null)
        {
            diagnostics.Add(Error("LVTF1004", "$.options.measurements", "The evidence collection must not be null."));
            return evidence;
        }
        for (int index = 0; index < options.Measurements.Length; ++index)
        {
            TargetMetricEvidence? item = options.Measurements[index];
            string path = $"$.options.measurements[{index}]";
            if (item is null || !Enum.IsDefined(item.Metric) || !Enum.IsDefined(item.Kind) ||
                item.Value < 0 || !ValidSource(item.Source))
            {
                diagnostics.Add(Error("LVTF1004", path, "Evidence requires a known metric/kind, nonnegative value, and a bounded source."));
                continue;
            }
            if (!evidence.TryAdd(item.Metric, item))
            {
                diagnostics.Add(Error("LVTF1004", path, "A metric may have exactly one evidence value."));
            }
            bool applicable = item.Metric switch
            {
                TargetMetric.ControllerDepthCount or TargetMetric.DamageRegionCount => true,
                TargetMetric.TransferWireBytes => options.TransferLimits?.MaximumWireBytes is not null,
                TargetMetric.TransferDurationMicroseconds => options.TransferLimits?.MaximumDurationMicroseconds is not null,
                _ => BudgetMetrics(target).Any(budget => budget.Metric == item.Metric && budget.Limit is not null)
            };
            if (!applicable)
            {
                diagnostics.Add(Error("LVTF1005", path,
                    "The metric is inapplicable to this target's configured budgets, rendering mode, or explicit transfer limits."));
            }
        }
        return evidence;
    }

    private static bool ValidSource(string? source) =>
        !string.IsNullOrWhiteSpace(source) && source.Length <= 1024 && !source.Any(char.IsControl);

    private static (TargetMetric Metric, string Id, BudgetLimit? Limit)[] BudgetMetrics(TargetProfile target) =>
    [
        (TargetMetric.ArchiveTextBytes, "archive-text", target.Budgets.ArchiveText),
        (TargetMetric.InitializedDataBytes, "initialized-data", target.Budgets.InitializedData),
        (TargetMetric.BssBytes, "bss", target.Budgets.Bss),
        (TargetMetric.StackFrameBytes, "stack-frame", target.Budgets.StackFrame),
        (TargetMetric.StaticObjectBytes, "static-object", target.Budgets.StaticObject),
        (TargetMetric.TileWorkspaceAllocatedBytes, "allocated-tile-workspace", target.Budgets.TileWorkspace),
        (TargetMetric.FramebufferAllocatedBytes, "allocated-framebuffer", target.Budgets.Framebuffer),
        (TargetMetric.InternalFlashBytes, "internal-flash", target.Budgets.InternalFlash),
        (TargetMetric.InternalRamBytes, "internal-ram", target.Budgets.InternalRam),
        (TargetMetric.ExternalRamBytes, "external-ram", target.Budgets.ExternalRam)
    ];
}
