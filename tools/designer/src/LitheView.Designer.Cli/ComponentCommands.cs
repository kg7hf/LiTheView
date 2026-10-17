// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Cli;

internal static class ComponentCommands
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length != 4 || arguments[1] != "export-project")
        {
            Console.Error.WriteLine("component export-project <project.json> <designer-checkout>");
            return 2;
        }
        try
        {
            var paths = DesignerProjectPathService.Load(arguments[2], arguments[3]);
            var profiles = ProfileCatalogLoader.LoadSurfaces(Path.Combine(paths.LibraryRoot, "profiles", "display-surfaces"));
            var catalog = WidgetCatalogLoader.LoadFile(Path.Combine(paths.LibraryRoot, "schema", "widgets.json"));
            Print(profiles.Diagnostics);
            Print(catalog.Diagnostics);
            if (!profiles.IsValid || !catalog.IsValid || catalog.Catalog is null)
            {
                return 1;
            }
            var loaded = DesignerWorkspaceService.Open(arguments[2], profiles, catalog.Catalog);
            Print(loaded.Diagnostics);
            if (!loaded.IsValid || loaded.Workspace is null)
            {
                return 1;
            }
            bool valid = true;
            foreach (var screen in DesignerProjectForms.LoadAll(loaded.Workspace))
            {
                var result = ComponentProjectExporter.Export(screen, paths.ComponentDirectory);
                Print(result.Diagnostics);
                if (result.IsValid)
                {
                    Console.WriteLine($"UI component generated: {result.OutputDirectory}");
                    Console.WriteLine("Surface passed; target unevaluated. Build your application with its existing CMake configuration.");
                }
                valid &= result.IsValid;
                if (!valid) break;
            }
            return valid ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or JsonException)
        {
            Console.Error.WriteLine($"LVUI4102 Error: {exception.Message}");
            return 1;
        }
    }

    private static void Print(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.Severity}: {diagnostic.Message}");
        }
    }
}
