// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Projects;

public sealed record NewNativeDisplayProjectRequest
{
    public required string ProjectDirectory { get; init; }

    public required string ProjectId { get; init; }

    public required string DisplayName { get; init; }

    // An explicit native packed definition. A version-1 surface adaptation is rejected
    // rather than reinterpreted, because its storage remains RGB staging.
    public required DisplayDefinitionLoadResult Definition { get; init; }

    // Always explicit: nothing is inferred from the display definition.
    public required LogicalInputConfigurationProfile Input { get; init; }

    public bool AllowExistingDirectory { get; init; }

    public DesignerProjectPaths? Paths { get; init; }

    public required WidgetCatalog Catalog { get; init; }
}

public static partial class DesignerWorkspaceService
{
    // Version-3 projects resolve from their own snapshots and never consult a surface catalog.
    private static readonly DisplaySurfaceCatalogLoadResult NoSurfaces = new() { Surfaces = [], Diagnostics = [] };

    // Creates a version-3 project with no editing surface, RGB staging profile or MCU
    // target, then opens it through the same workspace path as every other project.
    public static DesignerWorkspaceLoadResult CreateNativeDisplayProject(
        NewNativeDisplayProjectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ProfileDiagnostic> diagnostics = [.. request.Definition.Diagnostics];
        if (request.Definition.RequiresMigration)
        {
            diagnostics.Add(Error("LVDP1009", "$.displayDefinition",
                "A version-1 surface adaptation keeps its RGB staging interpretation; " +
                "select an explicit native display definition."));
            return Failed(diagnostics);
        }
        if (!request.Definition.IsValid || request.Definition is not
            { Profile: { } definition, CanonicalUtf8: { } definitionBytes, Sha256: { } definitionSha256 })
        {
            return Failed(diagnostics);
        }
        byte[] currentDefinition = DisplayDefinitionCanonicalizer.Canonicalize(definition);
        if (!currentDefinition.SequenceEqual(definitionBytes) ||
            DisplaySurfaceCanonicalizer.ComputeSha256(currentDefinition) != definitionSha256)
        {
            diagnostics.Add(Error("LVDP1001", "$.displayDefinition",
                "The supplied display definition changed after its canonical snapshot was loaded."));
            return Failed(diagnostics);
        }
        if (!DisplayDefinitionValidator.IsNativePacked(definition.Format))
        {
            diagnostics.Add(Error("LVDP1008", "$.displayDefinition",
                "Version-3 projects describe native packed displays; RGB displays keep their version-2 editing surface."));
            return Failed(diagnostics);
        }
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.Load(
            LogicalInputConfigurationCanonicalizer.Canonicalize(request.Input));
        diagnostics.AddRange(input.Diagnostics);
        if (!input.IsValid || input is not
            { Configuration: { } configuration, CanonicalUtf8: { } inputBytes, Sha256: { } inputSha256 })
        {
            return Failed(diagnostics);
        }

        DesignerProjectManifest manifest = new()
        {
            Schema = ProjectManifestSchema,
            SchemaVersion = 3,
            Id = request.ProjectId,
            DisplayName = request.DisplayName,
            SchemaCompatibility = new()
            {
                ProjectManifest = 3,
                DisplayDefinition = 2,
                LogicalInput = 1,
                Form = 1,
                ResourceManifest = 1,
                ExtensionManifest = 1
            },
            DisplayDefinition = new() { Id = definition.Id, Sha256 = definitionSha256 },
            LogicalInput = new() { Id = configuration.Id, Sha256 = inputSha256 },
            Forms = [DefaultFormRelativePath],
            ResourceManifests = [],
            Extensions = []
        };
        DesignerProjectLoadResult loaded = DesignerProjectLoader.Load(
            DesignerProjectCanonicalizer.Canonicalize(manifest));
        diagnostics.AddRange(loaded.Diagnostics);
        if (!loaded.IsValid || loaded.CanonicalUtf8 is null)
        {
            return Failed(diagnostics);
        }

        // The form uses the definition's logical extent: 512 by 384 emitters stay 512 by 384.
        FormDocument form = CreateDefaultForm(
            request.DisplayName,
            definition.Id,
            definition.Geometry.Width,
            definition.Geometry.Height);
        FormResolutionResult resolvedForm = FormDocumentResolver.Resolve(form, request.Catalog);
        diagnostics.AddRange(resolvedForm.Diagnostics);
        if (resolvedForm.Document is not null)
        {
            diagnostics.AddRange(
                FormSurfaceValidator.Validate(resolvedForm.Document, definition, configuration).Diagnostics);
        }
        FormXmlWriteResult formWrite = FormXmlWriter.Write(form, request.Catalog);
        diagnostics.AddRange(formWrite.Diagnostics);
        if (resolvedForm.Document is null || !formWrite.IsValid || formWrite.Utf8 is null ||
            diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return Failed(diagnostics);
        }

        string? destination = ProjectDestination(request.ProjectDirectory, diagnostics);
        if (destination is null)
        {
            return Failed(diagnostics);
        }
        if (request.Paths is not null)
        {
            try
            {
                DesignerProjectPathService.Resolve(Path.Combine(destination, ManifestFileName), request.Paths, manifest);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(Error("LVDW0001", "$.projectDirectory", exception.Message));
                return Failed(diagnostics);
            }
        }
        List<(string RelativePath, byte[] Contents)> files =
        [
            (ManifestFileName, loaded.CanonicalUtf8),
            (DefaultFormRelativePath, formWrite.Utf8),
            (DesignerProductResolver.DefinitionRelativePath, definitionBytes),
            (DesignerProductResolver.InputRelativePath, inputBytes)
        ];
        if (request.Paths is not null)
        {
            files.Add(("project.litheview.paths.json", DesignerProjectPathService.Serialize(request.Paths)));
        }
        if (!PublishNewProject(destination, request.AllowExistingDirectory, files, diagnostics))
        {
            return Failed(diagnostics);
        }

        return Open(Path.Combine(destination, ManifestFileName), NoSurfaces, request.Catalog, diagnostics);
    }
}
