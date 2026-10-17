// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.DisplayBuild;

public enum ValidationStageStatus
{
    Unevaluated,
    Passed,
    Failed
}

public sealed record DisplayApplicationCompilationResult
{
    public required IReadOnlyDictionary<string, byte[]> Files { get; init; }
    public required IReadOnlyList<ResolvedResourceVariant> Resources { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public string? FormSha256 { get; init; }
    public string? DisplayDefinitionSha256 { get; init; }
    public string? LogicalInputSha256 { get; init; }
    public int? RequiredMaxChildrenPerView { get; init; }
    public int? RequiredMaxFocusableViews { get; init; }
    public long EstimatedResourceRomBytes { get; init; }
    public ValidationStageStatus SurfaceStatus => IsValid
        ? ValidationStageStatus.Passed : ValidationStageStatus.Failed;
    public ValidationStageStatus TargetStatus { get; init; } = ValidationStageStatus.Unevaluated;
    public bool IsValid => Files.Count != 0 &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

// Host-only composition of existing loaders and generators. No target is required
// or inferred from a display format. The generated output has no managed dependency.
public static class DisplayApplicationCompiler
{
    public static DisplayApplicationCompilationResult Compile(
        FormDocument form,
        WidgetCatalog catalog,
        DisplayDefinitionLoadResult definition,
        LogicalInputConfigurationLoadResult input,
        ProjectResourceSetLoadResult resources,
        FormSurfacePolicy? surfacePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resources);

        List<ProfileDiagnostic> diagnostics =
            [.. definition.Diagnostics, .. input.Diagnostics, .. resources.Diagnostics];
        if (!definition.IsValid || definition.Profile is null ||
            !input.IsValid || input.Configuration is null || !resources.IsValid)
        {
            return Failed(diagnostics);
        }

        FormResolutionResult resolvedForm = FormDocumentResolver.Resolve(form, catalog);
        diagnostics.AddRange(resolvedForm.Diagnostics);
        if (!resolvedForm.IsValid || resolvedForm.Document is null)
        {
            return Failed(diagnostics);
        }
        FormSurfaceValidationResult surface = FormSurfaceValidator.Validate(
            resolvedForm.Document, definition.Profile, input.Configuration, surfacePolicy);
        diagnostics.AddRange(surface.Diagnostics);
        ResourceSurfaceResolutionResult selected = ResourceSurfaceResolver.Resolve(
            resources, definition.Profile.Format, definition.Profile.ResourceFormats);
        diagnostics.AddRange(selected.Diagnostics);

        Dictionary<string, string> bindings = new(StringComparer.Ordinal);
        foreach (FormResource reference in resolvedForm.Document.Resources.Values)
        {
            var matches = selected.Variants.Select(variant =>
                (Variant: variant, Name: RuntimeBinding(reference.RuntimeName, variant)))
                .Where(item => item.Name is not null).ToArray();
            if (matches.Length != 1)
            {
                diagnostics.Add(Error("LVDB1001", "$.resources",
                    $"Resource runtime name '{reference.RuntimeName}' must identify exactly one loaded manifest or converter entry point/frame."));
                continue;
            }
            ResolvedResourceVariant resource = matches[0].Variant;
            bool kindMatches = reference.Kind == FormResourceKind.Font
                ? resource.Kind == ResourceKind.Font
                : resource.Kind is ResourceKind.Image or ResourceKind.Icon or ResourceKind.Animation;
            if (!kindMatches)
            {
                diagnostics.Add(Error("LVDB1002", "$.resources",
                    $"Resource '{reference.RuntimeName}' does not provide the declared form resource kind."));
                continue;
            }
            string runtimeBase = matches[0].Name!;
            if (bindings.TryGetValue(resource.ResourceId, out string? existing) && existing != runtimeBase)
            {
                diagnostics.Add(Error("LVDB1004", "$.resources",
                    $"Resource '{resource.ResourceId}' has conflicting runtime aliases."));
            }
            else
            {
                bindings.TryAdd(resource.ResourceId, runtimeBase);
            }
        }
        foreach (ResolvedResourceVariant variant in selected.Variants)
        {
            if (bindings.ContainsKey(variant.ResourceId) &&
                !definition.Profile.ResourceFormats.Contains(variant.Variant.Output.Format))
            {
                diagnostics.Add(Error("LVDB1003", "$.resourceFormats",
                    $"The display definition does not allow the selected source format for '{variant.ResourceId}'."));
            }
        }
        if (HasErrors(diagnostics))
        {
            return Failed(diagnostics);
        }

        DisplayDefinitionCppCompilationResult display = DisplayDefinitionCppCompiler.Compile(
            definition, input.Configuration);
        FormCppCompilationResult compiledForm = FormCppCompiler.Compile(form, catalog);
        var provider = SelectedResourceCppEmitter.Emit(selected, bindings);
        diagnostics.AddRange(display.Diagnostics);
        diagnostics.AddRange(compiledForm.Diagnostics);
        diagnostics.AddRange(provider.Diagnostics);
        if (!display.IsValid || display.HeaderUtf8 is null ||
            !compiledForm.IsValid || compiledForm.HeaderUtf8 is null ||
            !provider.IsValid || provider.HeaderText is null)
        {
            return Failed(diagnostics);
        }

        return new DisplayApplicationCompilationResult
        {
            Files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["display.hpp"] = display.HeaderUtf8,
                ["form.hpp"] = compiledForm.HeaderUtf8,
                ["resources.hpp"] = Encoding.UTF8.GetBytes(provider.HeaderText)
            },
            Resources = [.. selected.Variants.Where(item => bindings.ContainsKey(item.ResourceId))
                .OrderBy(item => item.ResourceId, StringComparer.Ordinal)],
            FormSha256 = compiledForm.SourceXmlSha256,
            DisplayDefinitionSha256 = DisplaySurfaceCanonicalizer.ComputeSha256(
                DisplayDefinitionCanonicalizer.Canonicalize(definition.Profile)),
            LogicalInputSha256 = DisplaySurfaceCanonicalizer.ComputeSha256(
                LogicalInputConfigurationCanonicalizer.Canonicalize(input.Configuration)),
            RequiredMaxChildrenPerView = CompiledFormCapacity.MaximumChildren(resolvedForm.Document),
            RequiredMaxFocusableViews = CompiledFormCapacity.FocusableViews(resolvedForm.Document),
            EstimatedResourceRomBytes = provider.EstimatedRomBytes,
            Diagnostics = diagnostics
        };
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics) =>
        diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);

    private static string? RuntimeBinding(string runtimeName, ResolvedResourceVariant resource)
    {
        resource.Variant.Converter.Options.TryGetValue("entryPoint", out string? entryPoint);
        if (resource.Kind != ResourceKind.Animation)
        {
            return runtimeName == resource.ResourceId || runtimeName == entryPoint ? runtimeName : null;
        }
        // Designer animation imports bind each frame explicitly. The provider
        // emits the same checked base-fN names from one static sequence.
        string prefix = entryPoint + "-f";
        if (string.IsNullOrEmpty(entryPoint) || !runtimeName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }
        string suffix = runtimeName[prefix.Length..];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int frame) &&
            frame >= 0 && frame < resource.Variant.Output.FrameCount &&
            suffix == frame.ToString(CultureInfo.InvariantCulture) ? entryPoint : null;
    }

    private static DisplayApplicationCompilationResult Failed(
        IReadOnlyList<ProfileDiagnostic> diagnostics) => new()
        {
            Files = new Dictionary<string, byte[]>(),
            Resources = [],
            Diagnostics = diagnostics
        };

    private static ProfileDiagnostic Error(string code, string path, string message) =>
        new(code, ProfileDiagnosticSeverity.Error, path, message);
}

// Bind the relevant resolved widget semantics at export time. Canonical XML
// alone does not identify the catalog RuntimeKind used to generate ViewSpecs.
internal static class CompiledFormCapacity
{
    public static int MaximumChildren(ResolvedFormDocument form) =>
        form.Components.Where(component => component.ParentIndex.HasValue)
            .GroupBy(component => component.ParentIndex).Select(group => group.Count()).DefaultIfEmpty(0).Max();

    public static int FocusableViews(ResolvedFormDocument form) => form.Components.Count(PotentiallyFocusable);

    private static bool PotentiallyFocusable(ResolvedFormComponent component)
    {
        string? policy = component.Properties.Select(item => item.Value).OfType<FormTextValue>()
            .FirstOrDefault(item => item.PropertyId == "focus-policy")?.Value;
        if (policy is "true" or "false")
        {
            return policy == "true";
        }
        return component.Widget.RuntimeKind is "button" or "checkbox" or "radio" or "textField" or "picker" or "softkey" or
            "tabBar" or "tabView" or "wheelPicker" or "dropDown";
    }
}
