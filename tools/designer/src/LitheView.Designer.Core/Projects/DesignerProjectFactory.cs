// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

public static class DesignerProjectFactory
{
    public static DesignerProjectLoadResult Create(
        string schemaReference,
        string id,
        string displayName,
        DisplaySurfaceLoadResult surface)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(surface);
        List<ProfileDiagnostic> diagnostics = [.. surface.Diagnostics];
        bool inputsValid = surface.IsValid && surface.Profile is not null && surface.Sha256 is not null;
        if (!inputsValid)
        {
            if (diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error))
            {
                diagnostics.Add(
                    new ProfileDiagnostic(
                        "LVPM0007",
                        ProfileDiagnosticSeverity.Error,
                        "$",
                        "Project creation requires one valid display surface."));
            }
            return new DesignerProjectLoadResult { Diagnostics = diagnostics };
        }

        DesignerProjectManifest manifest = new()
        {
            Schema = schemaReference,
            SchemaVersion = 1,
            Id = id,
            DisplayName = displayName,
            SchemaCompatibility = new ProjectSchemaCompatibility
            {
                DisplaySurface = 1,
                ProjectManifest = 1,
                Form = 1,
                ResourceManifest = 1,
                ExtensionManifest = 1
            },
            SurfaceProfile = new LockedProfileReference
            {
                Id = surface.Profile!.Id,
                Sha256 = surface.Sha256!
            },
            Forms = [],
            ResourceManifests = [],
            Extensions = []
        };
        return DesignerProjectLoader.Load(DesignerProjectCanonicalizer.Canonicalize(manifest));
    }
}
