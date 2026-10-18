// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

public static class DesignerProjectValidator
{
    public static IReadOnlyList<ProfileDiagnostic> Validate(DesignerProjectManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<ProfileDiagnostic> diagnostics = [];
        if (manifest.SchemaCompatibility is null || manifest.Forms is null || manifest.ResourceManifests is null || manifest.Extensions is null)
        {
            AddError(diagnostics, "LVPM1405", "$", "Project collections and schema compatibility cannot be null.");
            return diagnostics;
        }
        if (manifest.SchemaVersion == 4)
        {
            ValidateIdentity(manifest, diagnostics);
            ValidatePaths(manifest.Forms, "$.forms", diagnostics);
            if (string.IsNullOrWhiteSpace(manifest.Schema) || manifest.DisplayKind != "segmented" ||
                manifest.SurfaceProfile is not null || manifest.DisplayDefinition is not null ||
                manifest.SegmentGlass is null || !Segments.SegmentDocuments.Name(manifest.SegmentGlass.Id) ||
                !ProfileRules.IsLowercaseSha256(manifest.SegmentGlass.Sha256) || manifest.LogicalInput is null ||
                manifest.ResourceManifests.Length != 0 || manifest.Extensions.Length != 0 || manifest.Forms.Length == 0)
                AddError(diagnostics, "LVPM1401", "$", "Version 4 requires a segmented glass, logical input and forms, without raster resources or extensions.");
            if (manifest.LogicalInput is not null) ValidateProfileReference(manifest.LogicalInput, "$.logicalInput", diagnostics);
            ProjectSchemaCompatibility c = manifest.SchemaCompatibility;
            if (c.ProjectManifest != 4 || c.SegmentGlass != 1 || c.LogicalInput != 1 || c.Form != 1 ||
                c.DisplaySurface is not null || c.DisplayDefinition is not null || c.ResourceManifest != 1 || c.ExtensionManifest != 1)
                AddError(diagnostics, "LVPM1402", "$.schemaCompatibility", "Unsupported segmented project contracts.");
            return diagnostics;
        }
        if (manifest.DisplayKind is not null || manifest.SegmentGlass is not null || manifest.SchemaCompatibility.SegmentGlass is not null)
            AddError(diagnostics, "LVPM1403", "$", "Segmented fields require a version-4 project.");
        ValidateSchema(manifest, diagnostics);
        ValidateIdentity(manifest, diagnostics);
        ValidateSchemaCompatibility(manifest, diagnostics);
        ValidateSurfaceReference(manifest, diagnostics);
        ValidateProductReferences(manifest, diagnostics);
        ValidatePaths(manifest.Forms, "$.forms", diagnostics);
        ValidatePaths(manifest.ResourceManifests, "$.resourceManifests", diagnostics);
        ValidateExtensions(manifest.Extensions, diagnostics);
        return diagnostics;
    }

    private static void ValidateSchema(
        DesignerProjectManifest manifest,
        List<ProfileDiagnostic> diagnostics)
    {
        if (manifest.SchemaVersion is not (1 or 2 or 3))
        {
            AddError(
                diagnostics,
                "LVPM1001",
                "$.schemaVersion",
                $"Schema version {manifest.SchemaVersion} is not supported; expected 1, 2 or 3.");
        }
        if (string.IsNullOrWhiteSpace(manifest.Schema))
        {
            AddError(diagnostics, "LVPM1002", "$.$schema", "The schema reference must not be empty.");
        }
    }

    private static void ValidateIdentity(
        DesignerProjectManifest manifest,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!ProfileRules.IsValidIdentifier(manifest.Id))
        {
            AddError(
                diagnostics,
                "LVPM1101",
                "$.id",
                "The project ID must be a bounded lowercase dotted or hyphenated identifier.");
        }
        if (string.IsNullOrWhiteSpace(manifest.DisplayName) ||
            manifest.DisplayName.Length > ProfileRules.MaximumDisplayNameLength)
        {
            AddError(
                diagnostics,
                "LVPM1102",
                "$.displayName",
                $"The display name must contain 1 through " +
                    $"{ProfileRules.MaximumDisplayNameLength} characters.");
        }
    }

    private static void ValidateSchemaCompatibility(
        DesignerProjectManifest manifest,
        List<ProfileDiagnostic> diagnostics)
    {
        ProjectSchemaCompatibility compatibility = manifest.SchemaCompatibility;
        bool allCurrent =
            (manifest.SchemaVersion == 3 ? compatibility.DisplaySurface is null : compatibility.DisplaySurface == 1) &&
            compatibility.ProjectManifest == manifest.SchemaVersion &&
            (manifest.SchemaVersion == 1
                ? compatibility.DisplayDefinition is null && compatibility.LogicalInput is null
                : compatibility.DisplayDefinition == 2 && compatibility.LogicalInput == 1) &&
            compatibility.Form == 1 &&
            compatibility.ResourceManifest == 1 &&
            compatibility.ExtensionManifest == 1;
        if (!allCurrent)
        {
            AddError(
                diagnostics,
                "LVPM1201",
                "$.schemaCompatibility",
                "Project compatibility must match its manifest version and supported display/input contracts.");
        }
    }

    private static void ValidateSurfaceReference(
        DesignerProjectManifest manifest,
        List<ProfileDiagnostic> diagnostics)
    {
        if (manifest.SchemaVersion == 3)
        {
            if (manifest.SurfaceProfile is not null)
            {
                AddError(diagnostics, "LVPM1203", "$.surfaceProfile",
                    "Version-3 native display projects have no RGB editing surface or staging profile.");
            }
            return;
        }
        if (manifest.SurfaceProfile is null)
        {
            AddError(diagnostics, "LVPM1203", "$.surfaceProfile",
                "Version-1 and version-2 projects require a locked editing-surface profile.");
            return;
        }
        ValidateProfileReference(manifest.SurfaceProfile, "$.surfaceProfile", diagnostics);
    }

    private static void ValidateProductReferences(
        DesignerProjectManifest manifest,
        List<ProfileDiagnostic> diagnostics)
    {
        if (manifest.SchemaVersion == 1)
        {
            if (manifest.DisplayDefinition is not null || manifest.LogicalInput is not null)
            {
                AddError(diagnostics, "LVPM1202", "$",
                    "Version-1 projects cannot declare version-2 product snapshots.");
            }
            return;
        }
        if (manifest.DisplayDefinition is null || manifest.LogicalInput is null)
        {
            AddError(diagnostics, "LVPM1202", "$",
                $"Version-{manifest.SchemaVersion} projects require locked display-definition and logical-input snapshots.");
            return;
        }
        ValidateProfileReference(manifest.DisplayDefinition, "$.displayDefinition", diagnostics);
        ValidateProfileReference(manifest.LogicalInput, "$.logicalInput", diagnostics);
    }

    private static void ValidateProfileReference(
        LockedProfileReference reference,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!ProfileRules.IsValidIdentifier(reference.Id))
        {
            AddError(
                diagnostics,
                "LVPM1303",
                $"{path}.id",
                "A profile reference requires a bounded lowercase identifier.");
        }
        if (!ProfileRules.IsLowercaseSha256(reference.Sha256))
        {
            AddError(
                diagnostics,
                "LVPM1304",
                $"{path}.sha256",
                "A profile lock must contain 64 lowercase hexadecimal characters.");
        }
    }

    private static void ValidatePaths(
        IEnumerable<string> paths,
        string diagnosticPath,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            if (!ProfileRules.IsSafeProjectPath(path))
            {
                AddError(
                    diagnostics,
                    "LVPM1401",
                    diagnosticPath,
                    $"Project path '{path}' must be relative, forward-slash normalized, and contained.");
            }
            if (!seen.Add(path))
            {
                AddError(
                    diagnostics,
                    "LVPM1402",
                    diagnosticPath,
                    $"Project path '{path}' is duplicated.");
            }
        }
    }

    private static void ValidateExtensions(
        ProjectExtensionReference[] extensions,
        List<ProfileDiagnostic> diagnostics)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ProjectExtensionReference extension in extensions)
        {
            if (!ProfileRules.IsValidIdentifier(extension.Id) || !ids.Add(extension.Id))
            {
                AddError(
                    diagnostics,
                    "LVPM1501",
                    "$.extensions",
                    $"Extension ID '{extension.Id}' is invalid or duplicated.");
            }
            if (string.IsNullOrWhiteSpace(extension.Version) || extension.Version.Length > 64)
            {
                AddError(
                    diagnostics,
                    "LVPM1502",
                    "$.extensions.version",
                    "An extension version must contain 1 through 64 characters.");
            }
            if (!ProfileRules.IsSafeProjectPath(extension.PackagePath))
            {
                AddError(
                    diagnostics,
                    "LVPM1503",
                    "$.extensions.packagePath",
                    "An extension package path must remain inside the project root.");
            }
            if (!ProfileRules.IsLowercaseSha256(extension.Sha256))
            {
                AddError(
                    diagnostics,
                    "LVPM1504",
                    "$.extensions.sha256",
                    "An extension lock must contain 64 lowercase hexadecimal characters.");
            }
        }
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }
}
