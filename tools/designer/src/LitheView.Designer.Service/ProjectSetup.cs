// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service;

internal sealed partial class DesignerService
{
    private sealed record SetupDocument(string Path, string Before);

    // Propose source-document edits only. The editor owns saving, undo and recovery.
    private static object ConfigureProject(Context context, RequestParameters parameters)
    {
        DesignerWorkspace workspace = RequireForm(context);
        if (workspace.Manifest.SchemaVersion is not (2 or 3))
        {
            throw new ServiceException("SetupMigrationRequired", "Project setup requires a version-2 or version-3 project.");
        }
        string directory = workspace.ProjectDirectory;
        SetupDocument Read(string relative) => new(Path.Combine(directory, relative), ReadInputDocument(Path.Combine(directory, relative)));
        SetupDocument inputDocument = Read(DesignerProductResolver.InputRelativePath);
        SetupDocument definitionDocument = Read(DesignerProductResolver.DefinitionRelativePath);
        SetupDocument? surfaceDocument = workspace.Manifest.SchemaVersion == 2 ? Read("profiles/display-surface.json") : null;
        SetupDocument manifestDocument = new(context.ManifestPath, ReadInputDocument(context.ManifestPath));
        SetupDocument[] documents = surfaceDocument is null
            ? [inputDocument, definitionDocument, manifestDocument]
            : [inputDocument, definitionDocument, surfaceDocument, manifestDocument];
        DisplayDefinitionLoadResult definition = DisplayDefinitionProfileLoader.Load(Encoding.UTF8.GetBytes(definitionDocument.Before));
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.Load(Encoding.UTF8.GetBytes(inputDocument.Before));
        DisplaySurfaceLoadResult? surface = surfaceDocument is null ? null
            : DisplaySurfaceProfileLoader.Load(Encoding.UTF8.GetBytes(surfaceDocument.Before));
        DesignerProjectLoadResult manifest = DesignerProjectLoader.Load(Encoding.UTF8.GetBytes(manifestDocument.Before));
        if (!definition.IsValid || definition.Sha256 != context.Display.DefinitionSha256 ||
            !input.IsValid || input.Sha256 != context.Display.InputSha256 ||
            !manifest.IsValid || manifest.Sha256 != workspace.ResolvedProject.ManifestSha256 ||
            (surfaceDocument is not null && (surface is not { IsValid: true } || surface.Sha256 != workspace.ResolvedProject.SurfaceSha256)))
        {
            throw new ServiceException("ProjectChanged", "Project settings changed while being read. Reopen Project setup.");
        }
        string snapshot = DisplaySurfaceCanonicalizer.ComputeSha256(JsonSerializer.SerializeToUtf8Bytes(documents, Protocol.JsonOptions));
        List<InputFileEdit> edits = [];
        if (parameters.Setup is { } request)
        {
            if (request.Snapshot != snapshot)
            {
                throw new ServiceException("ProjectChanged", "Project settings changed since setup opened. Reopen Project setup.");
            }
            DisplayDefinitionLoadResult updatedDefinition = definition;
            DisplaySurfaceLoadResult? updatedSurface = surface;
            if (request.CustomSurface is { } customSurface)
            {
                if (surface is null || request.CustomNativeDisplay is not null)
                {
                    throw new ServiceException("InvalidSurface", "Keep this project's RGB or packed display family. Create a separate project to change families.");
                }
                DisplaySurfaceLoadResult candidate = DisplaySurfaceProfileLoader.Load(Encoding.UTF8.GetBytes(customSurface.GetRawText()));
                if (!candidate.IsValid || !SupportsCreation(candidate.Profile))
                {
                    throw new ServiceException("InvalidSurface", "Enter a supported RGB display. " + Messages(candidate.Diagnostics));
                }
                DisplaySurfaceProfile old = surface.Profile!;
                DisplaySurfaceProfile value = candidate.Profile!;
                // Identity, rotation, legacy capability annotations and unedited provenance remain stable.
                ProfileProvenance provenance = value.Provenance with
                {
                    Notes = old.Provenance.Notes,
                    SourceSha256 = value.Provenance.SourceUrl == old.Provenance.SourceUrl &&
                        value.Provenance.SourceDocument == old.Provenance.SourceDocument ? old.Provenance.SourceSha256 : null
                };
                DisplaySurfaceProfile updated = old with
                {
                    Shape = value.Shape, PixelSize = value.PixelSize, PhysicalSize = value.PhysicalSize,
                    PainterSurface = value.PainterSurface, NativeDisplay = value.NativeDisplay,
                    SafeArea = value.SafeArea, Provenance = provenance
                };
                updatedSurface = DisplaySurfaceProfileLoader.Load(DisplaySurfaceCanonicalizer.Canonicalize(updated));
                if (!updatedSurface.IsValid) throw new ServiceException("InvalidSurface", Messages(updatedSurface.Diagnostics));
                // Do not replace an unchanged definition with a newly adapted one.
                if (updatedSurface.Sha256 != surface.Sha256)
                {
                    DisplayDefinitionLoadResult adapted = DisplayDefinitionProfileLoader.AdaptV1(updatedSurface);
                    updatedDefinition = DisplayDefinitionProfileLoader.Load(DisplayDefinitionCanonicalizer.Canonicalize(adapted.Profile! with
                    {
                        Schema = definition.Profile!.Schema, Id = definition.Profile.Id, DisplayName = definition.Profile.DisplayName,
                        ResourceFormats = definition.Profile.ResourceFormats
                    }));
                }
            }
            if (request.CustomNativeDisplay is { } customNative)
            {
                if (surface is not null) throw new ServiceException("InvalidSurface", "Keep this project's RGB display family; create a separate project for packed storage.");
                DisplayDefinitionProfile old = definition.Profile!;
                DisplayDefinitionLoadResult candidate = NativeDisplayDefinitionFactory.Create(old.Id, old.DisplayName,
                    ParseNativeFormat(customNative.Format), customNative.Width, customNative.Height,
                    ParseShape(customNative.Shape), customNative.SafeArea, customNative.SourceReference);
                if (!candidate.IsValid) throw new ServiceException("InvalidSurface", Messages(candidate.Diagnostics));
                updatedDefinition = DisplayDefinitionProfileLoader.Load(DisplayDefinitionCanonicalizer.Canonicalize(candidate.Profile! with
                {
                    Schema = old.Schema,
                    Geometry = candidate.Profile!.Geometry with { RotationDegrees = old.Geometry.RotationDegrees },
                    ResourceFormats = old.ResourceFormats,
                    Source = customNative.SourceReference == old.Source.Reference ? old.Source : candidate.Profile.Source
                }));
            }
            if (!updatedDefinition.IsValid) throw new ServiceException("InvalidSurface", Messages(updatedDefinition.Diagnostics));
            LogicalInputConfigurationProfile updatedInput = request.Input is { } selected ? input.Configuration! with
            {
                Pointer = selected.Pointer, Keys = selected.Keys, Text = selected.Text, Encoder = selected.Encoder, Select = selected.Select
            } : input.Configuration!;
            byte[] inputBytes = LogicalInputConfigurationCanonicalizer.Canonicalize(updatedInput);
            string inputSha = DisplaySurfaceCanonicalizer.ComputeSha256(inputBytes);
            DesignerProjectManifest updatedManifest = workspace.Manifest with
            {
                LogicalInput = new() { Id = updatedInput.Id, Sha256 = inputSha },
                DisplayDefinition = new() { Id = updatedDefinition.Profile!.Id, Sha256 = updatedDefinition.Sha256! },
                SurfaceProfile = updatedSurface is null ? null : new() { Id = updatedSurface.Profile!.Id, Sha256 = updatedSurface.Sha256! }
            };
            void Add(SetupDocument document, byte[] bytes) => edits.Add(new(document.Path, document.Before, Encoding.UTF8.GetString(bytes)));
            if (inputSha != input.Sha256) Add(inputDocument, inputBytes);
            if (updatedDefinition.Sha256 != definition.Sha256) Add(definitionDocument, updatedDefinition.CanonicalUtf8!);
            if (surfaceDocument is not null && updatedSurface!.Sha256 != surface!.Sha256) Add(surfaceDocument, updatedSurface.CanonicalUtf8!);
            // Manifest last: a partial save cannot export with inconsistent snapshot locks.
            if (edits.Count > 0) Add(manifestDocument, DesignerProjectCanonicalizer.Canonicalize(updatedManifest));
        }
        return new
        {
            documentVersion = parameters.DocumentVersion, snapshot, documents, edits,
            displaySurface = surface is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(surface.CanonicalUtf8!),
            displayDefinition = JsonSerializer.Deserialize<JsonElement>(definition.CanonicalUtf8!),
            input = JsonSerializer.Deserialize<JsonElement>(input.CanonicalUtf8!)
        };
    }

    private static SurfacePixelFormat ParseNativeFormat(string value) => value switch
    {
        "r2Packed2x2" => SurfacePixelFormat.R2Packed2x2,
        "rrrr2222" => SurfacePixelFormat.Rrrr2222,
        "ryry2222" => SurfacePixelFormat.Ryry2222,
        _ => throw new ServiceException("InvalidSurface", "Choose a supported packed format.")
    };

    private static SurfaceShape ParseShape(string value) => value switch
    {
        "rectangle" => SurfaceShape.Rectangle, "square" => SurfaceShape.Square, "circle" => SurfaceShape.Circle,
        _ => throw new ServiceException("InvalidSurface", "Choose a supported shape.")
    };
}
