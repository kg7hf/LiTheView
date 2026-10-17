// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.App;

internal sealed record DesignerRepositoryContext
{
    public required string RepositoryRoot { get; init; }

    public required DisplaySurfaceCatalogLoadResult Profiles { get; init; }

    public required WidgetCatalog Catalog { get; init; }

    public required string PreviewWorkerPath { get; init; }

    public static DesignerRepositoryContext Load(string repositoryRoot)
    {
        string root = Path.GetFullPath(repositoryRoot);
        DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(
            Path.Combine(root, "profiles", "display-surfaces"));
        if (!profiles.IsValid)
        {
            throw new InvalidDataException(FormatDiagnostics(profiles.Diagnostics));
        }

        WidgetCatalogLoadResult widgets = WidgetCatalogLoader.LoadFile(
            Path.Combine(root, "schema", "widgets.json"));
        if (!widgets.IsValid || widgets.Catalog is null)
        {
            throw new InvalidDataException(FormatDiagnostics(widgets.Diagnostics));
        }
        return new DesignerRepositoryContext
        {
            RepositoryRoot = root,
            Profiles = profiles,
            Catalog = widgets.Catalog,
            PreviewWorkerPath = Path.Combine(
                root,
                "build",
                "windows-mingw-debug",
                "litheview_designer_preview_worker.exe")
        };
    }

    private static string FormatDiagnostics(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));
    }
}

internal static class RepositoryLocator
{
    private const int MaximumParentSearchDepth = 12;

    public static string Find(string[] args)
    {
        for (int index = 0; index + 1 < args.Length; ++index)
        {
            if (string.Equals(args[index], "--repository-root", StringComparison.Ordinal))
            {
                return Validate(args[index + 1]);
            }
        }

        string? current = Directory.GetCurrentDirectory();
        for (int depth = 0; depth < MaximumParentSearchDepth && current is not null; ++depth)
        {
            if (IsRepositoryRoot(current))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidDataException(
            "The LitheView repository root was not found. Start the designer from the repository " +
            "or pass --repository-root <path>.");
    }

    private static string Validate(string path)
    {
        string root = Path.GetFullPath(path);
        if (!IsRepositoryRoot(root))
        {
            throw new InvalidDataException($"'{root}' is not a LitheView repository root.");
        }
        return root;
    }

    private static bool IsRepositoryRoot(string path)
    {
        return File.Exists(Path.Combine(path, "schema", "widgets.json")) &&
            Directory.Exists(Path.Combine(path, "profiles", "display-surfaces"));
    }
}
