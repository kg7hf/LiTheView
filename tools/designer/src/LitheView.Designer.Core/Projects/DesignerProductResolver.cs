// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Projects;

public sealed record DesignerProductResolutionResult
{
    public DisplayDefinitionLoadResult? Definition { get; init; }
    public LogicalInputConfigurationLoadResult? Input { get; init; }
    public ResolvedFormDocument? ResolvedForm { get; init; }
    public ProjectResourceSetLoadResult? Resources { get; init; }
    public string? FormSha256 { get; init; }
    public string? ResolvedSha256 { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => Definition is { IsValid: true } && Input is { IsValid: true } &&
        ResolvedForm is not null && Resources is { IsValid: true } &&
        FormSha256 is not null && ResolvedSha256 is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

// Product contracts are independent of any MCU. Version 2 has an authoritative
// logical-input snapshot; the v1 editing surface's input hints are historical.
public static class DesignerProductResolver
{
    public const string DefinitionRelativePath = "profiles/display-definition.json";
    public const string InputRelativePath = "profiles/logical-input.json";

    public static DesignerProductResolutionResult Resolve(DesignerWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return ResolveForm(ResolveSnapshots(workspace), workspace.Form, workspace.Catalog, workspace.Manifest,
            workspace.ProjectDirectory, workspace.ResolvedProject.ManifestSha256, workspace.ResolvedProject.SurfaceSha256);
    }

    private static DesignerProductResolutionResult ResolveForm(DesignerProductResolutionResult snapshots,
        FormDocument formDocument, WidgetCatalog catalog, DesignerProjectManifest manifest, string projectDirectory,
        string manifestSha256, string? surfaceSha256)
    {
        List<ProfileDiagnostic> diagnostics = [.. snapshots.Diagnostics];
        if (HasErrors(diagnostics) || snapshots.Definition?.Profile is not { } definition ||
            snapshots.Input?.Configuration is not { } input)
        {
            return snapshots;
        }
        FormResolutionResult form = FormDocumentResolver.Resolve(formDocument, catalog);
        diagnostics.AddRange(form.Diagnostics);
        FormXmlWriteResult canonicalForm = FormXmlWriter.Write(formDocument, catalog);
        diagnostics.AddRange(canonicalForm.Diagnostics);
        if (form.Document is not null)
        {
            diagnostics.AddRange(FormSurfaceValidator.Validate(form.Document, definition, input).Diagnostics);
        }
        ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(manifest, projectDirectory);
        diagnostics.AddRange(resources.Diagnostics);
        if (resources.IsValid)
        {
            diagnostics.AddRange(ResourceSurfaceResolver.Resolve(resources, definition.Format,
                definition.ResourceFormats).Diagnostics);
        }
        if (form.Document is null || canonicalForm.Utf8 is null || HasErrors(diagnostics))
        {
            return snapshots with { Diagnostics = diagnostics };
        }
        string formHash = DisplaySurfaceCanonicalizer.ComputeSha256(canonicalForm.Utf8);
        var resourceIdentity = resources.Resources.OrderBy(item => item.Manifest.Id, StringComparer.Ordinal)
            .Select(item => new { item.Manifest.Id, item.ManifestSha256 }).ToArray();
        // Native display projects have no editing surface, so their identity omits it.
        byte[] identity = surfaceSha256 is null
            ? JsonSerializer.SerializeToUtf8Bytes(new
            {
                project = manifestSha256,
                definition = snapshots.Definition.Sha256,
                input = snapshots.Input.Sha256,
                form = formHash,
                resources = resourceIdentity
            })
            : JsonSerializer.SerializeToUtf8Bytes(new
            {
                project = manifestSha256,
                surface = surfaceSha256,
                definition = snapshots.Definition.Sha256,
                input = snapshots.Input.Sha256,
                form = formHash,
                resources = resourceIdentity
            });
        return snapshots with
        {
            ResolvedForm = form.Document,
            Resources = resources,
            FormSha256 = formHash,
            ResolvedSha256 = DisplaySurfaceCanonicalizer.ComputeSha256(identity),
            Diagnostics = diagnostics
        };
    }

    internal static DesignerProductResolutionResult ResolveSnapshots(DesignerWorkspace workspace)
    {
        ResolvedDesignerProject resolved = workspace.ResolvedProject;
        List<ProfileDiagnostic> diagnostics = [.. DesignerProjectValidator.Validate(workspace.Manifest)];
        string manifestHash = DesignerProjectCanonicalizer.ComputeSha256(
            DesignerProjectCanonicalizer.Canonicalize(workspace.Manifest));
        DesignerProductResolutionResult snapshots;
        if (resolved.Surface is null)
        {
            // Version 3: the native snapshots are the whole display identity.
            if (manifestHash != resolved.ManifestSha256 || resolved.SurfaceSha256 is not null ||
                workspace.Manifest.SurfaceProfile is not null)
            {
                diagnostics.Add(Error("LVDP1001", "$", "The workspace changed after its project or display identity was resolved."));
            }
            if (HasErrors(diagnostics))
            {
                return new() { Diagnostics = diagnostics };
            }
            snapshots = ResolveNativeSnapshots(workspace.Manifest, workspace.ProjectDirectory);
        }
        else
        {
            DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.Load(
                DisplaySurfaceCanonicalizer.Canonicalize(resolved.Surface));
            diagnostics.AddRange(surface.Diagnostics);
            if (manifestHash != resolved.ManifestSha256 ||
                surface.Sha256 != resolved.SurfaceSha256 ||
                surface.Sha256 != workspace.Manifest.SurfaceProfile?.Sha256 ||
                surface.Profile?.Id != workspace.Manifest.SurfaceProfile?.Id)
            {
                diagnostics.Add(Error("LVDP1001", "$", "The workspace changed after its project or editing-surface identity was resolved."));
            }
            if (HasErrors(diagnostics))
            {
                return new() { Diagnostics = diagnostics };
            }
            snapshots = ResolveSnapshots(workspace.Manifest, surface, workspace.ProjectDirectory);
        }
        // The editor, preview and generation all consumed this display; it must still be current.
        if (!HasErrors(snapshots.Diagnostics) &&
            (snapshots.Definition?.Sha256 != resolved.Display.DefinitionSha256 ||
             snapshots.Input?.Sha256 != resolved.Display.InputSha256))
        {
            return snapshots with
            {
                Diagnostics = [.. snapshots.Diagnostics, Error("LVDP1001", "$",
                    "The workspace display contract no longer matches its locked or adapted snapshots.")]
            };
        }
        return snapshots;
    }

    internal static DesignerProductResolutionResult ResolveSnapshots(DesignerProjectManifest manifest,
        DisplaySurfaceLoadResult surface, string projectDirectory)
    {
        List<ProfileDiagnostic> diagnostics = [.. surface.Diagnostics];
        DisplayDefinitionLoadResult adapted = DisplayDefinitionProfileLoader.AdaptV1(surface);
        if (manifest.SchemaVersion == 1)
        {
            diagnostics.AddRange(adapted.Diagnostics);
            LogicalInputConfigurationLoadResult legacyInput = DefaultInput(surface.Profile!);
            diagnostics.AddRange(legacyInput.Diagnostics);
            diagnostics.Add(new("LVDP1002", ProfileDiagnosticSeverity.Warning, "$",
                "Legacy project: display and input are explicitly adapted without rewriting project files or changing native interpretation."));
            return new() { Definition = adapted, Input = legacyInput, Diagnostics = diagnostics };
        }

        (DisplayDefinitionLoadResult definition, LogicalInputConfigurationLoadResult input) =
            LoadSnapshots(manifest, projectDirectory, diagnostics);
        if (surface.Profile!.NativeDisplay.ColorModel != NativeColorModel.Rgb ||
            !adapted.IsValid || adapted.Profile is null || definition.Profile is not { } actual ||
            actual.Id != adapted.Profile.Id || actual.Geometry != adapted.Profile.Geometry ||
            actual.Format != adapted.Profile.Format || actual.AddressSpace != adapted.Profile.AddressSpace)
        {
            diagnostics.Add(Error("LVDP1004", "$.displayDefinition",
                "The version-2 definition must preserve the RGB editing surface's identity, geometry and storage format; native packed displays use a version-3 project instead of MDP staging."));
        }
        return new() { Definition = definition, Input = input, Diagnostics = diagnostics };
    }

    // Version 3 locks a native packed definition directly. No editing surface, RGB
    // staging adapter or MCU target participates, and input is never derived from display.
    internal static DesignerProductResolutionResult ResolveNativeSnapshots(DesignerProjectManifest manifest,
        string projectDirectory)
    {
        List<ProfileDiagnostic> diagnostics = [];
        (DisplayDefinitionLoadResult definition, LogicalInputConfigurationLoadResult input) =
            LoadSnapshots(manifest, projectDirectory, diagnostics);
        if (definition.Profile is { } profile && !DisplayDefinitionValidator.IsNativePacked(profile.Format))
        {
            diagnostics.Add(Error("LVDP1008", "$.displayDefinition",
                "Version-3 projects describe native packed displays; RGB displays keep their version-2 editing surface."));
        }
        return new() { Definition = definition, Input = input, Diagnostics = diagnostics };
    }

    private static (DisplayDefinitionLoadResult Definition, LogicalInputConfigurationLoadResult Input) LoadSnapshots(
        DesignerProjectManifest manifest, string projectDirectory, List<ProfileDiagnostic> diagnostics)
    {
        DisplayDefinitionLoadResult definition = DisplayDefinitionProfileLoader.LoadFile(
            Path.Combine(projectDirectory, DefinitionRelativePath));
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(
            Path.Combine(projectDirectory, InputRelativePath));
        diagnostics.AddRange(definition.Diagnostics);
        diagnostics.AddRange(input.Diagnostics);
        CheckLock(manifest.DisplayDefinition!, definition.Profile?.Id, definition.Sha256,
            "$.displayDefinition", diagnostics);
        CheckLock(manifest.LogicalInput!, input.Configuration?.Id, input.Sha256,
            "$.logicalInput", diagnostics);
        return (definition, input);
    }

    internal static LogicalInputConfigurationLoadResult DefaultInput(DisplaySurfaceProfile surface)
    {
        LogicalInputConfigurationProfile input = new()
        {
            Schema = "https://litheview.dev/schema/logical-input-configuration.schema.json",
            SchemaVersion = 1,
            Id = "litheview.input.project",
            Pointer = surface.InputCapabilities.Contains(InputCapability.Touch),
            Keys = surface.InputCapabilities.Contains(InputCapability.Keyboard),
            Text = false,
            Encoder = surface.InputCapabilities.Contains(InputCapability.Encoder),
            Select = surface.InputCapabilities.Contains(InputCapability.PushButton)
        };
        return LogicalInputConfigurationLoader.Load(LogicalInputConfigurationCanonicalizer.Canonicalize(input));
    }

    private static void CheckLock(LockedProfileReference reference, string? id, string? hash,
        string path, List<ProfileDiagnostic> diagnostics)
    {
        if (reference.Id != id || reference.Sha256 != hash)
        {
            diagnostics.Add(Error("LVDP1003", path, "The project snapshot does not match its locked identity and canonical hash."));
        }
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics) =>
        diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);

    private static ProfileDiagnostic Error(string code, string path, string message) =>
        new(code, ProfileDiagnosticSeverity.Error, path, message);
}
