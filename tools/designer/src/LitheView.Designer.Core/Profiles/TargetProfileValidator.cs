// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

public static class TargetProfileValidator
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumTileDimension = 16_384;
    private const int MaximumAlignmentBytes = 4_096;
    private const int ExpectedMaxChildrenPerView = 16;
    private const int ExpectedMaxDamageRegions = 8;
    private const int ExpectedMaxControllerDepth = 8;
    private const int ExpectedMaxFocusableViews = 64;

    private static readonly HashSet<string> sAllowedOverrides = new(StringComparer.Ordinal)
    {
        "displayName",
        "compatibilityFamily",
        "surfaceCompatibility",
        "runtime",
        "capacities",
        "budgets",
        "resourceStorage",
        "supportedResourceFormats",
        "provenance"
    };

    public static IReadOnlyList<ProfileDiagnostic> Validate(TargetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        List<ProfileDiagnostic> diagnostics = [];
        ValidateSchema(profile, diagnostics);
        ValidateIdentity(profile, diagnostics);
        ValidateBaseProfile(profile.BaseProfile, diagnostics);
        ValidateSurfaceCompatibility(profile.SurfaceCompatibility, diagnostics);
        ValidateRuntime(profile.Runtime, diagnostics);
        ValidateCapacities(profile.Capacities, diagnostics);
        ValidateBudgets(profile.Budgets, profile.Runtime.RenderingMode, diagnostics);
        ValidateResourceStorage(profile.ResourceStorage, diagnostics);
        ValidateResourceFormats(profile.SupportedResourceFormats, diagnostics);
        ValidateProvenance(profile.Provenance, diagnostics);
        return diagnostics;
    }

    private static void ValidateSchema(
        TargetProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        if (profile.SchemaVersion != CurrentSchemaVersion)
        {
            AddError(
                diagnostics,
                "LVTP1001",
                "$.schemaVersion",
                $"Schema version {profile.SchemaVersion} is not supported; expected {CurrentSchemaVersion}.");
        }
        if (string.IsNullOrWhiteSpace(profile.Schema))
        {
            AddError(diagnostics, "LVTP1002", "$.$schema", "The schema reference must not be empty.");
        }
    }

    private static void ValidateIdentity(
        TargetProfile profile,
        List<ProfileDiagnostic> diagnostics)
    {
        ValidateIdentifier(profile.Id, "$.id", "LVTP1101", diagnostics);
        ValidateIdentifier(
            profile.CompatibilityFamily,
            "$.compatibilityFamily",
            "LVTP1102",
            diagnostics);
        if (string.IsNullOrWhiteSpace(profile.DisplayName) ||
            profile.DisplayName.Length > ProfileRules.MaximumDisplayNameLength)
        {
            AddError(
                diagnostics,
                "LVTP1103",
                "$.displayName",
                $"The display name must contain 1 through " +
                    $"{ProfileRules.MaximumDisplayNameLength} characters.");
        }
    }

    private static void ValidateBaseProfile(
        TargetProfileBaseReference? baseProfile,
        List<ProfileDiagnostic> diagnostics)
    {
        if (baseProfile is null)
        {
            return;
        }

        ValidateIdentifier(baseProfile.ProfileId, "$.baseProfile.profileId", "LVTP1201", diagnostics);
        if (!ProfileRules.IsLowercaseSha256(baseProfile.Sha256))
        {
            AddError(
                diagnostics,
                "LVTP1202",
                "$.baseProfile.sha256",
                "The base-profile hash must be 64 lowercase hexadecimal characters.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string item in baseProfile.ExplicitOverrides)
        {
            if (!sAllowedOverrides.Contains(item))
            {
                AddError(
                    diagnostics,
                    "LVTP1203",
                    "$.baseProfile.explicitOverrides",
                    $"'{item}' is not an overridable top-level target-profile field.");
            }
            if (!seen.Add(item))
            {
                AddError(
                    diagnostics,
                    "LVTP1204",
                    "$.baseProfile.explicitOverrides",
                    $"Override '{item}' is duplicated.");
            }
        }
    }

    private static void ValidateSurfaceCompatibility(
        SurfaceCompatibilityProfile compatibility,
        List<ProfileDiagnostic> diagnostics)
    {
        ValidateIdentifier(compatibility.Family, "$.surfaceCompatibility.family", "LVTP1301", diagnostics);
        if (compatibility.PainterFormats.Length == 0)
        {
            AddError(
                diagnostics,
                "LVTP1302",
                "$.surfaceCompatibility.painterFormats",
                "At least one painter format is required.");
        }
        AddDuplicateErrors(
            compatibility.PainterFormats,
            "$.surfaceCompatibility.painterFormats",
            "LVTP1303",
            diagnostics);
    }

    private static void ValidateRuntime(
        TargetRuntimeProfile runtime,
        List<ProfileDiagnostic> diagnostics)
    {
        if (runtime.RenderingMode == RenderingMode.FullFrame && runtime.Tile is not null)
        {
            AddError(
                diagnostics,
                "LVTP1401",
                "$.runtime.tile",
                "A full-frame target must not define tile geometry.");
        }
        if (runtime.RenderingMode == RenderingMode.Tiled && runtime.Tile is null)
        {
            AddError(
                diagnostics,
                "LVTP1402",
                "$.runtime.tile",
                "A tiled target requires tile geometry.");
        }
        if (runtime.Tile is not null &&
            (runtime.Tile.Width is < 1 or > MaximumTileDimension ||
             runtime.Tile.Height is < 1 or > MaximumTileDimension))
        {
            AddError(
                diagnostics,
                "LVTP1403",
                "$.runtime.tile",
                $"Tile dimensions must contain 1 through {MaximumTileDimension} pixels.");
        }
    }

    private static void ValidateCapacities(
        RuntimeCapacityProfile capacities,
        List<ProfileDiagnostic> diagnostics)
    {
        bool matchesCore = capacities.MaxChildrenPerView == ExpectedMaxChildrenPerView &&
            capacities.MaxDamageRegions == ExpectedMaxDamageRegions &&
            capacities.MaxControllerDepth == ExpectedMaxControllerDepth &&
            capacities.MaxFocusableViews == ExpectedMaxFocusableViews;
        if (!matchesCore)
        {
            AddError(
                diagnostics,
                "LVTP1501",
                "$.capacities",
                "Schema version 1 must match the current core capacities: children=16, damage=8, " +
                    "controllerDepth=8, focusableViews=64.");
        }
    }

    private static void ValidateBudgets(
        TargetBudgetProfile budgets,
        RenderingMode renderingMode,
        List<ProfileDiagnostic> diagnostics)
    {
        (string Name, BudgetLimit? Value)[] limits =
        [
            ("archiveText", budgets.ArchiveText),
            ("initializedData", budgets.InitializedData),
            ("bss", budgets.Bss),
            ("stackFrame", budgets.StackFrame),
            ("staticObject", budgets.StaticObject),
            ("tileWorkspace", budgets.TileWorkspace),
            ("framebuffer", budgets.Framebuffer),
            ("internalFlash", budgets.InternalFlash),
            ("internalRam", budgets.InternalRam),
            ("externalRam", budgets.ExternalRam)
        ];
        foreach ((string name, BudgetLimit? limit) in limits)
        {
            if (limit is null)
            {
                continue;
            }
            if (limit.MaximumBytes <= 0 || string.IsNullOrWhiteSpace(limit.Source) ||
                limit.Source.Length > 240 || limit.Source.Any(char.IsControl))
            {
                AddError(
                    diagnostics,
                    "LVTP1601",
                    $"$.budgets.{name}",
                    "A budget requires a positive byte ceiling and a bounded evidence source.");
            }
        }

        if (renderingMode == RenderingMode.Tiled &&
            (budgets.TileWorkspace is null || budgets.Framebuffer is not null))
        {
            AddError(
                diagnostics,
                "LVTP1602",
                "$.budgets",
                "A tiled target requires a tile-workspace budget and no framebuffer budget.");
        }
        if (renderingMode == RenderingMode.FullFrame &&
            (budgets.Framebuffer is null || budgets.TileWorkspace is not null))
        {
            AddError(
                diagnostics,
                "LVTP1603",
                "$.budgets",
                "A full-frame target requires a framebuffer budget and no tile-workspace budget.");
        }
    }

    private static void ValidateResourceStorage(
        ResourceStorageProfile storage,
        List<ProfileDiagnostic> diagnostics)
    {
        if (storage.AllowedPlacements.Length == 0 ||
            !storage.AllowedPlacements.Contains(storage.DefaultPlacement))
        {
            AddError(
                diagnostics,
                "LVTP1701",
                "$.resourceStorage",
                "Allowed placements must contain the default resource placement.");
        }
        AddDuplicateErrors(
            storage.AllowedPlacements,
            "$.resourceStorage.allowedPlacements",
            "LVTP1702",
            diagnostics);
        bool validAlignment = storage.AlignmentBytes is >= 1 and <= MaximumAlignmentBytes &&
            (storage.AlignmentBytes & (storage.AlignmentBytes - 1)) == 0;
        if (!validAlignment)
        {
            AddError(
                diagnostics,
                "LVTP1703",
                "$.resourceStorage.alignmentBytes",
                $"Resource alignment must be a power of two no larger than {MaximumAlignmentBytes} bytes.");
        }
    }

    private static void ValidateResourceFormats(
        ResourceFormat[] formats,
        List<ProfileDiagnostic> diagnostics)
    {
        if (formats.Length == 0)
        {
            AddError(
                diagnostics,
                "LVTP1801",
                "$.supportedResourceFormats",
                "At least one resource format is required.");
        }
        AddDuplicateErrors(
            formats,
            "$.supportedResourceFormats",
            "LVTP1802",
            diagnostics);
    }

    private static void ValidateProvenance(
        TargetProfileProvenance provenance,
        List<ProfileDiagnostic> diagnostics)
    {
        if (provenance.SourcePaths.Length == 0 ||
            provenance.SourcePaths.Any(path => !ProfileRules.IsSafeProjectPath(path)))
        {
            AddError(
                diagnostics,
                "LVTP1901",
                "$.provenance.sourcePaths",
                "Provenance requires project-relative, forward-slash source paths.");
        }
        AddDuplicateErrors(
            provenance.SourcePaths,
            "$.provenance.sourcePaths",
            "LVTP1902",
            diagnostics);
        if (string.IsNullOrWhiteSpace(provenance.Notes))
        {
            AddError(
                diagnostics,
                "LVTP1903",
                "$.provenance.notes",
                "Target-profile provenance notes are required.");
        }
    }

    private static void ValidateIdentifier(
        string value,
        string path,
        string code,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!ProfileRules.IsValidIdentifier(value))
        {
            AddError(
                diagnostics,
                code,
                path,
                "The value must be a lowercase dotted or hyphenated identifier no longer than " +
                    $"{ProfileRules.MaximumIdentifierLength} characters.");
        }
    }

    private static void AddDuplicateErrors<T>(
        IEnumerable<T> values,
        string path,
        string code,
        List<ProfileDiagnostic> diagnostics)
        where T : notnull
    {
        HashSet<T> seen = [];
        foreach (T value in values)
        {
            if (!seen.Add(value))
            {
                AddError(diagnostics, code, path, $"Value '{value}' is duplicated.");
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
