// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using LitheView.Designer.Core.DisplayBuild;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Tests;

internal static class TargetFeasibilityTests
{
    private sealed record Product(
        DisplayApplicationCompilationResult Surface,
        ResolvedFormDocument Form,
        DisplayDefinitionProfile Definition);

    public static void Run(string repositoryRoot)
    {
        WidgetCatalog catalog = WidgetCatalogLoader.LoadFile(Path.Combine(repositoryRoot, "schema", "widgets.json")).Catalog!;
        TargetProfile target = TargetProfileLoader.LoadFile(Path.Combine(repositoryRoot, "profiles", "targets",
            "stm32u545-standard-rgb565-tiled.json")).Profile!;
        Product red = LoadProduct(repositoryRoot, catalog, "synthetic-r2");
        Product redCell = LoadProduct(repositoryRoot, catalog, "synthetic-red-cell");
        Product yellow = LoadProduct(repositoryRoot, catalog, "synthetic-red-yellow");
        Product rgb565 = LoadProduct(repositoryRoot, catalog, "rk043fn48h-rgb565");
        Product xrgb = LoadProduct(repositoryRoot, catalog, "rk043fn48h-xrgb8888");
        TargetProfile redTarget = ForProduct(target, red, 512, 2, 256);
        CheckStages(red, redTarget);
        CheckStorage(red, redCell, yellow, rgb565, xrgb, target);
        CheckCompatibility(red, redTarget);
        CheckEvidence(red, redTarget);
        CheckTransfer(red, redTarget);
        CheckIdentity(red, redTarget, rgb565);
        CheckCountsAndCompleteScope(repositoryRoot, catalog, target, rgb565.Definition);
    }

    private static void CheckStages(Product product, TargetProfile target)
    {
        string[] fileHashes = HashFiles(product.Surface);
        byte[] definitionBytes = DisplayDefinitionCanonicalizer.Canonicalize(product.Definition);
        TargetFeasibilityResult absent = Evaluate(product);
        Require(absent.IsValid && absent.TargetStatus == ValidationStageStatus.Unevaluated &&
            absent.SurfaceStatus == ValidationStageStatus.Passed && absent.MinimumRenderPayloadBytes is null,
            "A valid host surface needs no target and must not infer a rendering strategy.");
        TargetFeasibilityResult constrained = Evaluate(product,
            target with { Budgets = target.Budgets with { TileWorkspace = Limit(255) } }, Bound(product));
        Require(constrained.TargetStatus == ValidationStageStatus.Failed &&
            Constraint(constrained, "minimum-render-payload").Used == 256 &&
            Constraint(constrained, "minimum-render-payload").Status == ValidationStageStatus.Failed,
            "A 255-byte target rejects the independently known 256-byte native tile payload.");
        TargetFeasibilityResult sufficient = Evaluate(product, target, Bound(product));
        Require(sufficient.IsValid && sufficient.TargetStatus == ValidationStageStatus.Unevaluated &&
            Constraint(sufficient, "minimum-render-payload").Status == ValidationStageStatus.Passed &&
            Constraint(sufficient, "allocated-tile-workspace").Status == ValidationStageStatus.Unevaluated &&
            Constraint(sufficient, "archive-text").Used is null &&
            Constraint(sufficient, "controller-depth").Used is null,
            "A sufficient payload allowance does not certify allocation, archive size, or controller behavior.");
        Require(Constraint(sufficient, "resource-linker-placement").Required &&
            Constraint(sufficient, "resource-linker-placement").Status == ValidationStageStatus.Unevaluated &&
            Constraint(sufficient, "resource-alignment").Required &&
            Constraint(sufficient, "resource-placement-configuration").Status == ValidationStageStatus.Passed,
            "Allowed placement does not certify the selected resources' linker location or alignment.");
        Require(Constraint(sufficient, "selected-resource-rom").EvidenceKind == TargetEvidenceKind.Estimated &&
            Constraint(sufficient, "selected-resource-rom").Used == product.Surface.EstimatedResourceRomBytes,
            "Resource metadata remains a separate estimate with an explicit scope.");
        TargetFeasibilityResult tinyFlash = Evaluate(product,
            target with { Budgets = target.Budgets with { InternalFlash = Limit(1, BudgetEnforcement.LinkerRegion) } },
            Bound(product));
        Require(product.Surface.EstimatedResourceRomBytes > 1 &&
            Constraint(tinyFlash, "internal-flash").Status == ValidationStageStatus.Unevaluated &&
            Constraint(tinyFlash, "internal-flash").Used is null,
            "Selected resource estimates must not substitute for a whole linked flash measurement.");
        Require(fileHashes.SequenceEqual(HashFiles(product.Surface)) &&
            definitionBytes.AsSpan().SequenceEqual(DisplayDefinitionCanonicalizer.Canonicalize(product.Definition)) &&
            product.Surface.SurfaceStatus == ValidationStageStatus.Passed &&
            product.Surface.TargetStatus == ValidationStageStatus.Unevaluated,
            "Target failure must leave surface files, geometry, identity, and prior status unchanged.");
    }

    private static void CheckStorage(
        Product red, Product redCell, Product yellow, Product rgb565, Product xrgb, TargetProfile template)
    {
        foreach ((Product product, int width, int height) in new[] { (red, 512, 2), (redCell, 256, 1), (yellow, 256, 1) })
        {
            TargetFeasibilityResult result = Evaluate(product, ForProduct(template, product, width, height, 256), Bound(product));
            Require(result.MinimumRenderPayloadBytes == 256, "All three native products require exactly 256 bytes for one native row.");
        }
        Require(Evaluate(rgb565, ForProduct(template, rgb565, 480, 4, 8000), Bound(rgb565)).MinimumRenderPayloadBytes == 3840 &&
            Evaluate(xrgb, ForProduct(template, xrgb, 480, 4, 8000), Bound(xrgb)).MinimumRenderPayloadBytes == 7680,
            "RGB565 and XRGB8888 builds must retain distinct byte costs for identical logical tile geometry.");
        foreach ((Product product, long bytes) in new[] { (rgb565, 261120L), (xrgb, 522240L) })
        {
            TargetProfile full = ForProduct(template, product, 480, 4, 8000) with
            {
                Runtime = template.Runtime with { RenderingMode = RenderingMode.FullFrame, Tile = null },
                Budgets = template.Budgets with { TileWorkspace = null, Framebuffer = Limit(600000) }
            };
            Require(Evaluate(product, full, Bound(product)).MinimumRenderPayloadBytes == bytes,
                "Full-frame calculation must use native storage rather than the target's old tile geometry.");
        }
        TargetFeasibilityResult incomplete = Evaluate(red, ForProduct(template, red, 512, 1, 256), Bound(red));
        Require(HasCode(incomplete, "LVTF1102") && incomplete.TargetStatus == ValidationStageStatus.Failed,
            "An R2 tile must contain complete 2x2 native blocks; height is never silently rounded.");
        TargetFeasibilityResult tooSmall = Evaluate(red, ForProduct(template, red, 512, 2, 256),
            Bound(red) with { Measurements = [Evidence(TargetMetric.TileWorkspaceAllocatedBytes, 255)] });
        Require(Constraint(tooSmall, "render-allocation-sufficiency").Status == ValidationStageStatus.Failed,
            "A reported allocation below the minimum native payload cannot pass merely because it is below the budget.");
    }

    private static void CheckCompatibility(Product product, TargetProfile target)
    {
        TargetFeasibilityResult unbound = Evaluate(product, target);
        Require(Constraint(unbound, "display-definition-binding").Status == ValidationStageStatus.Unevaluated,
            "A legacy family string cannot silently bind a version-2 definition.");
        Require(Constraint(Evaluate(product, target, Bound(product) with
            { ExpectedDisplayDefinitionId = "litheview.definition.software.unrelated" }), "display-definition-binding").Status ==
            ValidationStageStatus.Failed, "A mismatched explicit definition binding must fail.");
        Require(Constraint(Evaluate(product, target with
            { SurfaceCompatibility = target.SurfaceCompatibility with { PainterFormats = [SurfacePixelFormat.Rgb565] } },
            Bound(product)), "destination-format").Status == ValidationStageStatus.Failed,
            "Target destination support must be checked independently of memory allowances.");
        Require(Constraint(Evaluate(product, target with { SupportedResourceFormats = [ResourceFormat.Argb8888] },
            Bound(product)), "source-formats").Status == ValidationStageStatus.Failed,
            "Selected resource formats must be accepted by the target.");
        Require(Constraint(Evaluate(product, target, Bound(product) with { ResourcePlacement = ResourcePlacement.HostFile }),
            "resource-placement-configuration").Status == ValidationStageStatus.Failed,
            "Explicit resource placement must be one of the allowed target configurations.");
    }

    private static void CheckEvidence(Product product, TargetProfile target)
    {
        TargetMetricEvidence archive = Evidence(TargetMetric.ArchiveTextBytes, 1, TargetEvidenceKind.Estimated);
        TargetFeasibilityResult estimated = Evaluate(product, target, Bound(product) with { Measurements = [archive] });
        Require(Constraint(estimated, "archive-text").Status == ValidationStageStatus.Unevaluated &&
            Constraint(estimated, "archive-text").EvidenceKind == TargetEvidenceKind.Estimated,
            "An under-limit archive estimate must not certify an archive gate.");
        Require(Constraint(Evaluate(product, target, Bound(product) with
            { Measurements = [archive with { Kind = TargetEvidenceKind.Calculated }] }), "archive-text").Status ==
            ValidationStageStatus.Unevaluated, "Calculated source costs are not cross-build archive measurements.");
        Require(Constraint(Evaluate(product, target, Bound(product) with
            { Measurements = [archive with { Kind = TargetEvidenceKind.CrossBuildMeasured }] }), "archive-text").Status ==
            ValidationStageStatus.Passed, "An applicable measured archive cost can pass its specific budget.");
        Require(Evaluate(product, target, Bound(product) with
            { Measurements = [archive with { Value = target.Budgets.ArchiveText!.MaximumBytes + 1 }] }).TargetStatus ==
            ValidationStageStatus.Failed, "An over-limit estimated cost must expose the target feasibility failure.");
        TargetProfile advisory = target with
        {
            Budgets = target.Budgets with { ArchiveText = target.Budgets.ArchiveText! with { Enforcement = BudgetEnforcement.AdvisoryEstimate } }
        };
        Require(Constraint(Evaluate(product, advisory, Bound(product) with { Measurements = [archive] }), "archive-text").Status ==
            ValidationStageStatus.Passed, "An estimate may satisfy an explicitly advisory estimate limit.");
        foreach (TargetMetricEvidence invalid in new[]
        {
            archive with { Metric = Enum.Parse<TargetMetric>("255") },
            archive with { Kind = Enum.Parse<TargetEvidenceKind>("255") },
            archive with { Value = -1 }, archive with { Source = "" }, archive with { Source = "line\nbreak" },
            archive with { Source = new string('x', 1025) }
        })
        {
            Require(HasCode(Evaluate(product, target, Bound(product) with { Measurements = [invalid] }), "LVTF1004"),
                "Invalid typed evidence must fail direct managed callers as well as JSON callers.");
        }
        Require(HasCode(Evaluate(product, target, Bound(product) with { Measurements = [archive, archive] }), "LVTF1004") &&
            HasCode(Evaluate(product, target, Bound(product) with { Measurements = null! }), "LVTF1004") &&
            HasCode(Evaluate(product, target, Bound(product) with { Measurements = [null!] }), "LVTF1004"),
            "Duplicate and null evidence must fail explicitly.");
        foreach (TargetMetric metric in new[] { TargetMetric.FramebufferAllocatedBytes, TargetMetric.InternalFlashBytes, TargetMetric.TransferWireBytes })
        {
            Require(HasCode(Evaluate(product, target, Bound(product) with { Measurements = [Evidence(metric, 1)] }), "LVTF1005"),
                "Evidence without an applicable configured constraint must fail.");
        }
        Require(HasCode(Evaluate(product, target, Bound(product) with { SchemaVersion = 2 }), "LVTF1004") &&
            HasCode(Evaluate(product, target, Bound(product) with { ResourcePlacement = Enum.Parse<ResourcePlacement>("255") }), "LVTF1004"),
            "Options versions and placement values must be validated for direct callers.");
        foreach (TargetRuntimeProfile runtime in new[]
        {
            target.Runtime with { Architecture = Enum.Parse<TargetArchitecture>("255") },
            target.Runtime with { Environment = Enum.Parse<OperatingEnvironment>("255") }
        })
        {
            Require(HasCode(Evaluate(product, target with { Runtime = runtime }, Bound(product)), "LVTF1002"),
                "Directly constructed targets must not accept undefined architecture or environment values.");
        }
    }

    private static void CheckTransfer(Product product, TargetProfile target)
    {
        TargetFeasibilityOptions options = Bound(product) with
        {
            TransferLimits = new() { MaximumWireBytes = 50000, MaximumDurationMicroseconds = 100000, Source = "fixture explicit transport contract" },
            Measurements = [Evidence(TargetMetric.TransferWireBytes, 49152),
                Evidence(TargetMetric.TransferDurationMicroseconds, 30000, TargetEvidenceKind.HardwareMeasured)]
        };
        TargetFeasibilityResult measured = Evaluate(product, target, options);
        Require(Constraint(measured, "transfer-wire-bytes").Status == ValidationStageStatus.Passed &&
            Constraint(measured, "transfer-duration").Status == ValidationStageStatus.Passed &&
            Constraint(measured, "transfer-duration").Unit == TargetConstraintUnit.Microseconds,
            "Explicit wire bytes and hardware duration measurements must retain distinct units and evidence.");
        foreach (TargetEvidenceKind kind in new[] { TargetEvidenceKind.Estimated, TargetEvidenceKind.Calculated, TargetEvidenceKind.CrossBuildMeasured })
        {
            TargetFeasibilityResult unmeasured = Evaluate(product, target, options with
            { Measurements = [options.Measurements[0], options.Measurements[1] with { Kind = kind }] });
            Require(Constraint(unmeasured, "transfer-duration").Status == ValidationStageStatus.Unevaluated,
                "An offline or estimated result must not certify physical transfer timing.");
        }
        Require(Evaluate(product, target, options with
        { Measurements = [options.Measurements[0], options.Measurements[1] with { Value = 100001 }] }).TargetStatus ==
            ValidationStageStatus.Failed, "A known excessive transfer duration must fail its target constraint.");
        foreach (TargetTransferLimits invalid in new[]
        {
            new TargetTransferLimits { Source = "fixture" },
            new TargetTransferLimits { Source = "", MaximumWireBytes = 1 },
            new TargetTransferLimits { Source = "fixture", MaximumWireBytes = 0 },
            new TargetTransferLimits { Source = "fixture", MaximumDurationMicroseconds = -1 }
        })
        {
            Require(HasCode(Evaluate(product, target, Bound(product) with { TransferLimits = invalid }), "LVTF1004"),
                "Transfer integration limits need positive units and an explicit source.");
        }
    }

    private static void CheckIdentity(Product product, TargetProfile target, Product rgb565)
    {
        TargetFeasibilityOptions bound = Bound(product);
        foreach (TargetFeasibilityOptions stale in new[]
        {
            bound with { ExpectedFormSha256 = new string('0', 64) },
            bound with { ExpectedDisplayDefinitionSha256 = new string('0', 64) },
            bound with { ExpectedLogicalInputSha256 = new string('0', 64) },
            bound with { ExpectedFormSha256 = "malformed" }
        })
        {
            Require(HasCode(Evaluate(product, target, stale), "LVTF1004"), "Stale or malformed evidence identities must fail.");
        }
        DisplayDefinitionProfile resized = rgb565.Definition with
        { Geometry = rgb565.Definition.Geometry with { Width = 481 } };
        DisplayDefinitionProfile reformatted = rgb565.Definition with
        {
            Format = SurfacePixelFormat.Xrgb8888,
            StorageBlock = DisplayDefinitionValidator.StorageBlockFor(SurfacePixelFormat.Xrgb8888),
            Fields = DisplayDefinitionValidator.FieldsFor(SurfacePixelFormat.Xrgb8888),
            Composition = DisplayDefinitionValidator.CompositionFor(SurfacePixelFormat.Xrgb8888)
        };
        foreach (DisplayDefinitionProfile altered in new[] { resized, reformatted })
        {
            Require(DisplayDefinitionValidator.Validate(altered).Count == 0, "Identity test mutations must themselves be valid definitions.");
            TargetFeasibilityResult result = Evaluate(rgb565 with { Definition = altered }, target);
            Require(HasCode(result, "LVTF1003") && result.TargetStatus == ValidationStageStatus.Unevaluated,
                "A reused ID cannot bind changed display geometry or format to previously compiled files.");
        }
        TargetFeasibilityResult missingIdentity = Evaluate(product with
        { Surface = product.Surface with { DisplayDefinitionSha256 = null } }, target);
        Require(HasCode(missingIdentity, "LVTF1003") && missingIdentity.TargetStatus == ValidationStageStatus.Unevaluated,
            "Missing compiled definition identity must fail the prerequisite without pretending to evaluate the target.");
        ResolvedFormDocument wrongKinds = product.Form with
        {
            Components = [.. product.Form.Components.Select(component => component.Widget.RuntimeKind == "button"
                ? component with { Widget = component.Widget with { RuntimeKind = "label" } } : component)]
        };
        Require(FormXmlWriter.Write(wrongKinds).Sha256 == product.Surface.FormSha256,
            "The mixed-catalog regression must preserve the canonical XML hash.");
        Require(HasCode(Evaluate(product with { Form = wrongKinds }, target), "LVTF1003"),
            "Different resolved RuntimeKind capacity facts cannot be attached to the existing compiled form.");
        Require(HasCode(Evaluate(product with
            { Surface = product.Surface with { RequiredMaxFocusableViews = null } }, target), "LVTF1003"),
            "Missing compiled semantic capacity facts must fail the prerequisite.");
        TargetFeasibilityResult invalidSurface = Evaluate(product with
        { Surface = product.Surface with { Files = new Dictionary<string, byte[]>() } }, target);
        Require(HasCode(invalidSurface, "LVTF1001") && invalidSurface.SurfaceStatus == ValidationStageStatus.Failed &&
            invalidSurface.TargetStatus == ValidationStageStatus.Unevaluated, "A failed surface build must not enter target evaluation.");
        Require(HasCode(Evaluate(product, options: bound), "LVTF1004"), "Target evidence without target selection must not be silently accepted.");
    }

    private static void CheckCountsAndCompleteScope(
        string repositoryRoot, WidgetCatalog catalog, TargetProfile template, DisplayDefinitionProfile definition)
    {
        FormDocument form = PlainForm(definition, 1);
        // Scroll handlers are not automatic focusability. Explicit focus policy
        // remains authoritative even though all views have mutable states.
        FormComponent scroll = Component(1, "scroll", 0, new(0, 0, 100, 100));
        FormComponent label = Component(2, "label", 0, new(100, 0, 100, 100),
            new FormTextValue("focus-policy", "true"));
        FormComponent button = Component(3, "button", 0, new(200, 0, 100, 100),
            new FormTextValue("focus-policy", "false"));
        Product counted = CompilePlain(repositoryRoot, catalog, definition,
            form with { Components = [form.Components[0], scroll, label, button] });
        TargetProfile target = ForProduct(template, counted, 480, 4, 3840);
        TargetFeasibilityResult counts = Evaluate(counted, target, Bound(counted));
        Require(Constraint(counts, "children-per-view").Used == 3 && Constraint(counts, "focusable-views").Used == 1,
            "Focus capacity must follow runtime ViewKind defaults and explicit policy, not all widget input handlers.");
        Product tooManyChildren = CompilePlain(repositoryRoot, catalog, definition, PlainForm(definition, 17));
        Require(Constraint(Evaluate(tooManyChildren, target, Bound(tooManyChildren)), "children-per-view").Status ==
            ValidationStageStatus.Failed, "A valid surface can exceed the target's child capacity.");
        FormComponent[] deep = new FormComponent[11];
        deep[0] = Component(0, "panel", null, new(0, 0, definition.Geometry.Width, definition.Geometry.Height));
        for (int index = 1; index < deep.Length; ++index)
        {
            deep[index] = Component(index, index == 10 ? "button" : "panel", index - 1, new(0, 0, 1, 1)) with { SiblingOrder = 0 };
        }
        Product deepProduct = CompilePlain(repositoryRoot, catalog, definition, form with { Components = deep });
        Require(Constraint(Evaluate(deepProduct, target, Bound(deepProduct)), "controller-depth").Used is null,
            "Eleven nested views do not imply eleven scene controllers.");
        TargetProfile minimal = target with
        {
            Budgets = new()
            {
                ArchiveText = null, InitializedData = null, Bss = null, StackFrame = null,
                StaticObject = null, TileWorkspace = Limit(3840), Framebuffer = null,
                InternalFlash = null, InternalRam = null, ExternalRam = null
            }
        };
        TargetFeasibilityResult complete = Evaluate(counted, minimal, Bound(counted) with
        {
            Measurements = [Evidence(TargetMetric.TileWorkspaceAllocatedBytes, 3840),
                Evidence(TargetMetric.ControllerDepthCount, 1), Evidence(TargetMetric.DamageRegionCount, 1)]
        });
        Require(complete.TargetStatus == ValidationStageStatus.Passed && complete.IsValid &&
            Constraint(complete, "internal-flash").Status == ValidationStageStatus.Unevaluated &&
            !Constraint(complete, "internal-flash").Required,
            "Only all applicable configured limits can produce a pass; unconfigured whole-firmware budgets remain unevaluated.");
    }

    private static Product LoadProduct(string root, WidgetCatalog catalog, string name)
    {
        string fixture = Path.Combine(root, "tests", "rendering", "display-model-generated");
        DisplayDefinitionLoadResult definition = DisplayDefinitionProfileLoader.LoadFile(
            Path.Combine(root, "profiles", "display-definitions", name + ".json"));
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(Path.Combine(fixture, name + ".litheview.xml"), catalog, FormImportMode.Strict);
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(
            Path.Combine(root, "profiles", "logical-inputs", "encoder-select.json"));
        ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(["resources/gallop.resource.json"], fixture);
        DisplayApplicationCompilationResult surface = DisplayApplicationCompiler.Compile(form.Document!, catalog, definition, input, resources);
        Require(surface.IsValid, string.Join("; ", surface.Diagnostics.Select(item => item.Message)));
        return new(surface, FormDocumentResolver.Resolve(form.Document!, catalog).Document!, definition.Profile!);
    }

    private static Product CompilePlain(string root, WidgetCatalog catalog, DisplayDefinitionProfile definition, FormDocument form)
    {
        DisplayDefinitionLoadResult loaded = DisplayDefinitionProfileLoader.Load(DisplayDefinitionCanonicalizer.Canonicalize(definition));
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(
            Path.Combine(root, "profiles", "logical-inputs", "encoder-select.json"));
        DisplayApplicationCompilationResult surface = DisplayApplicationCompiler.Compile(form, catalog, loaded, input,
            new() { Resources = [], Diagnostics = [] });
        Require(surface.IsValid, string.Join("; ", surface.Diagnostics.Select(item => item.Message)));
        return new(surface, FormDocumentResolver.Resolve(form, catalog).Document!, definition);
    }

    private static FormDocument PlainForm(DisplayDefinitionProfile definition, int children) => new()
    {
        SchemaVersion = 1, Id = "form.target-tests", DisplayName = "Target Tests", SurfaceProfileId = definition.Id,
        Components = [Component(0, "panel", null, new(0, 0, definition.Geometry.Width, definition.Geometry.Height)),
            .. Enumerable.Range(1, children).Select(index => Component(index, "button", 0, new(index, 0, 1, 1)))],
        Actions = [], Bindings = [], Resources = [], ThemeColors = []
    };

    private static FormComponent Component(int index, string kind, int? parent, FormRectangle frame, params FormPropertyValue[] properties) => new()
    {
        ComponentId = $"component.target-{index}", Name = $"Target{index}", WidgetId = $"lithe.widget.{kind}",
        ParentComponentId = parent.HasValue ? $"component.target-{parent.Value}" : null,
        SiblingOrder = parent.HasValue ? index - 1 : 0,
        Properties = [new FormRectangleValue("frame", frame), .. properties], HistoricalAttributes = []
    };

    private static TargetProfile ForProduct(TargetProfile template, Product product, int width, int height, long maximum) => template with
    {
        SurfaceCompatibility = template.SurfaceCompatibility with { PainterFormats = [product.Definition.Format] },
        Runtime = template.Runtime with { RenderingMode = RenderingMode.Tiled, Tile = new() { Width = width, Height = height } },
        Budgets = template.Budgets with { TileWorkspace = Limit(maximum), Framebuffer = null },
        SupportedResourceFormats = [.. Enum.GetValues<ResourceFormat>()]
    };

    private static BudgetLimit Limit(long bytes, BudgetEnforcement enforcement = BudgetEnforcement.CompileTimeAssertion) =>
        new() { MaximumBytes = bytes, Enforcement = enforcement, Source = "fixture declared byte ceiling" };

    private static TargetMetricEvidence Evidence(TargetMetric metric, long value, TargetEvidenceKind kind = TargetEvidenceKind.Calculated) =>
        new() { Metric = metric, Value = value, Kind = kind, Source = "fixture supplied cost evidence; not a real hardware measurement" };

    private static TargetFeasibilityOptions Bound(Product product) => new()
    {
        ExpectedDisplayDefinitionId = product.Definition.Id, ExpectedFormSha256 = product.Surface.FormSha256,
        ExpectedDisplayDefinitionSha256 = product.Surface.DisplayDefinitionSha256,
        ExpectedLogicalInputSha256 = product.Surface.LogicalInputSha256
    };

    private static TargetFeasibilityResult Evaluate(Product product, TargetProfile? target = null, TargetFeasibilityOptions? options = null) =>
        TargetFeasibilityEvaluator.Evaluate(product.Surface, product.Form, product.Definition, target, options);

    private static TargetFeasibilityConstraint Constraint(TargetFeasibilityResult result, string id) =>
        result.Constraints.Single(item => item.Id == id);

    private static bool HasCode(TargetFeasibilityResult result, string code) =>
        !result.IsValid && result.Diagnostics.Any(item => item.Code == code);

    private static string[] HashFiles(DisplayApplicationCompilationResult surface) =>
        [.. surface.Files.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Key + ":" + Convert.ToHexString(SHA256.HashData(item.Value)))];

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
