// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

// The single display contract that the workspace, editor, service, preview and generation
// consume for RGB and native packed projects alike. Versions 2 and 3 use their locked
// definition and logical-input snapshots. Version 1 uses its explicit adaptation and falls
// back to the editing surface only when that legacy surface (for example a rotated one) has
// no compiled-definition equivalent; nothing here migrates or rewrites a project.
public sealed record DesignerDisplay
{
    // The identity a form's surfaceProfileId must name.
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    // Logical extent: pixels, emitters or cells according to the definition's address space.
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required SurfaceShape Shape { get; init; }

    public required SafeAreaProfile SafeArea { get; init; }

    // The rendering destination. Version-3 projects render native packed storage directly.
    public required SurfacePixelFormat Format { get; init; }

    public DisplayDefinitionProfile? Definition { get; init; }

    public string? DefinitionSha256 { get; init; }

    public required LogicalInputConfigurationProfile Input { get; init; }

    public required string InputSha256 { get; init; }

    // Version-1 MDP projects keep their historical RGB565 staging preview. Never set otherwise.
    public DisplaySurfaceProfile? LegacyStagingSurface { get; init; }

    public bool IsNativePacked => DisplayDefinitionValidator.IsNativePacked(Format);

    internal static DesignerDisplay FromDefinition(
        DisplayDefinitionProfile definition,
        string definitionSha256,
        LogicalInputConfigurationProfile input,
        string inputSha256,
        DisplaySurfaceProfile? editingSurface) => new()
    {
        Id = definition.Id,
        DisplayName = definition.DisplayName,
        Width = definition.Geometry.Width,
        Height = definition.Geometry.Height,
        Shape = definition.Geometry.Shape,
        SafeArea = definition.Geometry.SafeArea,
        Format = definition.Format,
        Definition = definition,
        DefinitionSha256 = definitionSha256,
        Input = input,
        InputSha256 = inputSha256,
        LegacyStagingSurface = LegacyStaging(editingSurface)
    };

    internal static DesignerDisplay FromEditingSurface(
        DisplaySurfaceProfile surface,
        LogicalInputConfigurationProfile input,
        string inputSha256) => new()
    {
        Id = surface.Id,
        DisplayName = surface.DisplayName,
        Width = surface.PixelSize.Width,
        Height = surface.PixelSize.Height,
        Shape = surface.Shape,
        SafeArea = surface.SafeArea,
        Format = surface.PainterSurface.Format,
        Input = input,
        InputSha256 = inputSha256,
        LegacyStagingSurface = LegacyStaging(surface)
    };

    private static DisplaySurfaceProfile? LegacyStaging(DisplaySurfaceProfile? surface) =>
        surface is { NativeDisplay.ColorModel: not NativeColorModel.Rgb } ? surface : null;
}
