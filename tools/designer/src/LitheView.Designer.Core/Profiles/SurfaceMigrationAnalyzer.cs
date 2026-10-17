// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Profiles;

[Flags]
public enum SurfaceMigrationKind
{
    None = 0,
    Identity = 1 << 0,
    CompatibilityFamily = 1 << 1,
    PhysicalSize = 1 << 2,
    Geometry = 1 << 3,
    Color = 1 << 4,
    SafeArea = 1 << 5,
    Input = 1 << 6
}

public sealed record SurfaceMigrationResult
{
    public required SurfaceMigrationKind Kind { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool RequiresMigration => Kind != SurfaceMigrationKind.None;
}

public static class SurfaceMigrationAnalyzer
{
    public static SurfaceMigrationResult Compare(
        DisplaySurfaceProfile current,
        DisplaySurfaceProfile proposed)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);

        SurfaceMigrationKind kind = SurfaceMigrationKind.None;
        List<ProfileDiagnostic> diagnostics = [];
        AddChange(
            current.Id != proposed.Id || current.DisplayName != proposed.DisplayName,
            SurfaceMigrationKind.Identity,
            "LVDS3001",
            "identity",
            "Surface identity or display name changed.",
            ref kind,
            diagnostics);
        AddChange(
            current.CompatibilityFamily != proposed.CompatibilityFamily,
            SurfaceMigrationKind.CompatibilityFamily,
            "LVDS3002",
            "compatibilityFamily",
            "Compatibility family changed; display-compatible resources must be validated again.",
            ref kind,
            diagnostics);
        AddChange(
            current.PhysicalSize != proposed.PhysicalSize,
            SurfaceMigrationKind.PhysicalSize,
            "LVDS3003",
            "physicalSize",
            "Physical dimensions changed; review legibility and target-size guidance.",
            ref kind,
            diagnostics);
        AddChange(
            current.Shape != proposed.Shape ||
                current.PixelSize != proposed.PixelSize ||
                current.CoordinateSystem != proposed.CoordinateSystem,
            SurfaceMigrationKind.Geometry,
            "LVDS3004",
            "geometry",
            "Shape, pixel dimensions, origin, or rotation changed; layout migration is required.",
            ref kind,
            diagnostics);
        AddChange(
            current.PainterSurface != proposed.PainterSurface ||
                current.NativeDisplay != proposed.NativeDisplay,
            SurfaceMigrationKind.Color,
            "LVDS3005",
            "color",
            "Painter or native color contract changed; resource migration is required.",
            ref kind,
            diagnostics);
        AddChange(
            current.SafeArea != proposed.SafeArea,
            SurfaceMigrationKind.SafeArea,
            "LVDS3006",
            "safeArea",
            "Safe area changed; clipped or newly exposed controls require review.",
            ref kind,
            diagnostics);
        AddChange(
            !current.InputCapabilities.ToHashSet().SetEquals(proposed.InputCapabilities),
            SurfaceMigrationKind.Input,
            "LVDS3007",
            "inputCapabilities",
            "Input capabilities changed; interactions and focus paths require review.",
            ref kind,
            diagnostics);
        AddChange(
            current.Schema != proposed.Schema ||
                current.SchemaVersion != proposed.SchemaVersion ||
                current.Provenance != proposed.Provenance,
            SurfaceMigrationKind.Identity,
            "LVDS3008",
            "metadata",
            "Schema or provenance metadata changed; the new profile identity requires review.",
            ref kind,
            diagnostics);

        return new SurfaceMigrationResult { Kind = kind, Diagnostics = diagnostics };
    }

    private static void AddChange(
        bool changed,
        SurfaceMigrationKind change,
        string code,
        string path,
        string message,
        ref SurfaceMigrationKind kind,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!changed)
        {
            return;
        }

        kind |= change;
        diagnostics.Add(
            new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Warning, path, message));
    }
}
