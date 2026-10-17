// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.DisplayBuild;

// Target checking consumes a successful surface product. It returns a separate
// report and never rewrites the compiled files, definition, or selected assets.
public static partial class TargetFeasibilityEvaluator
{
    public static TargetFeasibilityResult Evaluate(
        DisplayApplicationCompilationResult surface,
        ResolvedFormDocument form,
        DisplayDefinitionProfile definition,
        TargetProfile? target = null,
        TargetFeasibilityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new();
        List<ProfileDiagnostic> diagnostics = [];
        List<TargetFeasibilityConstraint> constraints = [];
        if (!surface.IsValid || surface.EstimatedResourceRomBytes < 0)
        {
            diagnostics.Add(Error("LVTF1001", "$", "Target evaluation requires an already successful surface build."));
            return Result(surface, target, constraints, diagnostics, prerequisiteFailed: true);
        }
        FormXmlWriteResult canonicalForm = FormXmlWriter.Write(form);
        if (!canonicalForm.IsValid || canonicalForm.Sha256 != surface.FormSha256 ||
            form.Source.SurfaceProfileId != definition.Id ||
            surface.RequiredMaxChildrenPerView != CompiledFormCapacity.MaximumChildren(form) ||
            surface.RequiredMaxFocusableViews != CompiledFormCapacity.FocusableViews(form))
        {
            diagnostics.Add(Error("LVTF1003", "$.form", "The resolved form and definition must match the compiled surface product."));
            return Result(surface, target, constraints, diagnostics, prerequisiteFailed: true);
        }
        IReadOnlyList<ProfileDiagnostic> definitionErrors = DisplayDefinitionValidator.Validate(definition);
        if (definitionErrors.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            diagnostics.AddRange(definitionErrors);
            return Result(surface, target, constraints, diagnostics, prerequisiteFailed: true);
        }
        string definitionHash = DisplaySurfaceCanonicalizer.ComputeSha256(
            DisplayDefinitionCanonicalizer.Canonicalize(definition));
        if (surface.DisplayDefinitionSha256 != definitionHash ||
            !ProfileRules.IsLowercaseSha256(surface.LogicalInputSha256))
        {
            diagnostics.Add(Error("LVTF1003", "$.surface",
                "The compiled product must retain matching display-definition and logical-input identities."));
            return Result(surface, target, constraints, diagnostics, prerequisiteFailed: true);
        }
        if (target is null)
        {
            if (options.SchemaVersion != 1 || options.ExpectedDisplayDefinitionId is not null ||
                options.ExpectedFormSha256 is not null || options.ResourcePlacement.HasValue ||
                options.ExpectedDisplayDefinitionSha256 is not null || options.ExpectedLogicalInputSha256 is not null ||
                options.TransferLimits is not null || options.Measurements is not { Length: 0 })
            {
                diagnostics.Add(Error("LVTF1004", "$.options", "Target evidence and integration options require an explicitly selected target."));
            }
            return Result(surface, null, constraints, diagnostics);
        }
        diagnostics.AddRange(TargetProfileValidator.Validate(target));
        if (!Enum.IsDefined(target.Runtime.RenderingMode) ||
            !Enum.IsDefined(target.Runtime.Architecture) || !Enum.IsDefined(target.Runtime.Environment) ||
            target.SurfaceCompatibility.PainterFormats.Any(format => !Enum.IsDefined(format)) ||
            target.SupportedResourceFormats.Any(format => !Enum.IsDefined(format)) ||
            !Enum.IsDefined(target.ResourceStorage.DefaultPlacement) ||
            target.ResourceStorage.AllowedPlacements.Any(placement => !Enum.IsDefined(placement)))
        {
            diagnostics.Add(Error("LVTF1002", "$.target", "The target requires supported rendering and resource configuration values."));
        }
        Dictionary<TargetMetric, TargetMetricEvidence> evidence = ValidateOptions(target, surface, options, diagnostics);
        if (diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return Result(surface, target, constraints, diagnostics);
        }

        AddConfiguration(surface, definition, target, options, constraints, diagnostics);
        AddCapacity(constraints, diagnostics, "children-per-view", surface.RequiredMaxChildrenPerView!.Value,
            target.Capacities.MaxChildrenPerView, "resolved form direct-child counts");
        AddCapacity(constraints, diagnostics, "focusable-views", surface.RequiredMaxFocusableViews!.Value,
            target.Capacities.MaxFocusableViews,
            "resolved ViewKind defaults and explicit focus policy; mutable visibility/enabled states are not exclusions");
        AddMeasuredCapacity(constraints, diagnostics, evidence, TargetMetric.ControllerDepthCount,
            "controller-depth", target.Capacities.MaxControllerDepth);
        AddMeasuredCapacity(constraints, diagnostics, evidence, TargetMetric.DamageRegionCount,
            "damage-regions", target.Capacities.MaxDamageRegions);

        long? minimumPayload = CalculateRenderPayload(definition, target, diagnostics);
        if (minimumPayload.HasValue)
        {
            BudgetLimit payloadLimit = target.Runtime.RenderingMode == RenderingMode.Tiled
                ? target.Budgets.TileWorkspace! : target.Budgets.Framebuffer!;
            AddKnown(constraints, diagnostics, "minimum-render-payload", minimumPayload.Value,
                payloadLimit.MaximumBytes, TargetConstraintUnit.Bytes, TargetEvidenceKind.Calculated,
                "tight native storage blocks for the selected tile/full-frame geometry; excludes padding and other RAM",
                payloadLimit.Source);
        }
        foreach ((TargetMetric metric, string id, BudgetLimit? limit) in BudgetMetrics(target))
        {
            AddMeasuredBudget(constraints, diagnostics, evidence, metric, id, limit);
        }
        TargetMetric allocationMetric = target.Runtime.RenderingMode == RenderingMode.Tiled
            ? TargetMetric.TileWorkspaceAllocatedBytes : TargetMetric.FramebufferAllocatedBytes;
        if (minimumPayload.HasValue && evidence.TryGetValue(allocationMetric, out TargetMetricEvidence? allocation))
        {
            AddKnown(constraints, diagnostics, "render-allocation-sufficiency", minimumPayload.Value,
                allocation.Value, TargetConstraintUnit.Bytes, allocation.Kind,
                $"minimum native payload compared with declared allocation: {allocation.Source}", allocation.Source,
                permitPass: allocation.Kind != TargetEvidenceKind.Estimated);
        }
        AddTransfer(constraints, diagnostics, evidence, options.TransferLimits);
        constraints.Add(new()
        {
            Id = "selected-resource-rom", Status = ValidationStageStatus.Unevaluated,
            EvidenceKind = TargetEvidenceKind.Estimated,
            EvidenceSource = "selected converter metadata; excludes linked code, alignment, other resources, and whole firmware flash",
            Used = surface.EstimatedResourceRomBytes, Unit = TargetConstraintUnit.Bytes, Required = false
        });
        return Result(surface, target, constraints, diagnostics, minimumPayload);
    }

    private static void AddConfiguration(
        DisplayApplicationCompilationResult surface, DisplayDefinitionProfile definition,
        TargetProfile target, TargetFeasibilityOptions options,
        List<TargetFeasibilityConstraint> constraints, List<ProfileDiagnostic> diagnostics)
    {
        if (options.ExpectedDisplayDefinitionId is null)
        {
            AddUnknown(constraints, "display-definition-binding", TargetConstraintUnit.Boolean, null,
                "Legacy surface family does not identify a v2 definition; supply an explicit integration binding.",
                target.SurfaceCompatibility.Family, required: true);
        }
        else
        {
            AddBoolean(constraints, diagnostics, "display-definition-binding",
                options.ExpectedDisplayDefinitionId == definition.Id,
                "options.expectedDisplayDefinitionId", target.SurfaceCompatibility.Family);
        }
        AddBoolean(constraints, diagnostics, "destination-format",
            target.SurfaceCompatibility.PainterFormats.Contains(definition.Format),
            $"selected compiled destination: {definition.Format}", "target.surfaceCompatibility.painterFormats");
        AddBoolean(constraints, diagnostics, "source-formats",
            surface.Resources.All(resource => target.SupportedResourceFormats.Contains(resource.Variant.Output.Format)),
            "selected surface-compatible resource variants", "target.supportedResourceFormats");
        ResourcePlacement selected = options.ResourcePlacement ?? target.ResourceStorage.DefaultPlacement;
        AddBoolean(constraints, diagnostics, "resource-placement-configuration",
            target.ResourceStorage.AllowedPlacements.Contains(selected),
            options.ResourcePlacement.HasValue ? $"options.resourcePlacement: {selected}" :
                $"target.resourceStorage.defaultPlacement: {selected}", "target.resourceStorage.allowedPlacements");
        bool hasResources = surface.Resources.Count != 0;
        AddUnknown(constraints, "resource-linker-placement", TargetConstraintUnit.Boolean, null,
            "Allowed/default placement is configuration; actual linked or provider-owned region placement is not measured.",
            "target.resourceStorage", hasResources);
        AddUnknown(constraints, "resource-alignment", TargetConstraintUnit.Bytes,
            null,
            $"Required alignment is {target.ResourceStorage.AlignmentBytes} bytes; selected resource alignment has no typed linker or runtime evidence in this stage.",
            "target.resourceStorage.alignmentBytes", hasResources);
    }

    private static long? CalculateRenderPayload(
        DisplayDefinitionProfile definition, TargetProfile target, List<ProfileDiagnostic> diagnostics)
    {
        int width = target.Runtime.Tile?.Width ?? definition.Geometry.Width;
        int height = target.Runtime.Tile?.Height ?? definition.Geometry.Height;
        DisplayStorageBlock block = definition.StorageBlock;
        if (width <= 0 || height <= 0 || width % block.Width != 0 || height % block.Height != 0)
        {
            diagnostics.Add(Error("LVTF1102", "$.target.runtime.tile",
                "The selected rendering geometry must contain complete native storage blocks; no rounding or rescaling is performed."));
            return null;
        }
        try
        {
            return checked((long)(width / block.Width) * (height / block.Height) * block.Bytes);
        }
        catch (OverflowException)
        {
            diagnostics.Add(Error("LVTF1102", "$.target.runtime", "Native render payload arithmetic exceeds the reporting range."));
            return null;
        }
    }

    private static TargetFeasibilityResult Result(
        DisplayApplicationCompilationResult surface, TargetProfile? target,
        IReadOnlyList<TargetFeasibilityConstraint> constraints, IReadOnlyList<ProfileDiagnostic> diagnostics,
        long? minimumPayload = null, bool prerequisiteFailed = false)
    {
        ValidationStageStatus status = target is null || prerequisiteFailed ? ValidationStageStatus.Unevaluated :
            diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error) ||
                constraints.Any(item => item.Status == ValidationStageStatus.Failed) ? ValidationStageStatus.Failed :
            constraints.Any(item => item.Required && item.Status != ValidationStageStatus.Passed)
                ? ValidationStageStatus.Unevaluated : ValidationStageStatus.Passed;
        return new()
        {
            SurfaceStatus = surface.SurfaceStatus, TargetStatus = status, TargetId = target?.Id,
            MinimumRenderPayloadBytes = minimumPayload, EstimatedResourceRomBytes = surface.EstimatedResourceRomBytes,
            Constraints = constraints, Diagnostics = diagnostics
        };
    }

    private static ProfileDiagnostic Error(string code, string path, string message) =>
        new(code, ProfileDiagnosticSeverity.Error, path, message);
}
