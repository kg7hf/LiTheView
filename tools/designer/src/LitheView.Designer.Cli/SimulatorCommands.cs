// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;
using LitheView.Designer.Core.Simulation;

namespace LitheView.Designer.Cli;

internal static class SimulatorCommands
{
    private static readonly JsonSerializerOptions PathJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static int Run(string[] arguments)
    {
        try
        {
            return RunChecked(arguments);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or JsonException)
        {
            Console.Error.WriteLine($"LVSIM3001 Error: {exception.Message}");
            return 1;
        }
    }

    private static int RunChecked(string[] arguments)
    {
        if (arguments.Length is 7 or 8 && arguments[1] == "create-tutorial" &&
            (arguments.Length == 7 || arguments[7] == "--existing"))
        {
            DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.LoadFile(arguments[3]);
            LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(arguments[4]);
            DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(arguments[5]);
            WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(arguments[6]);
            WriteDiagnostics(surface.Diagnostics);
            WriteDiagnostics(input.Diagnostics);
            WriteDiagnostics(profiles.Diagnostics);
            WriteDiagnostics(catalog.Diagnostics);
            if (!surface.IsValid || !input.IsValid || !profiles.IsValid || !catalog.IsValid || catalog.Catalog is null)
            {
                return 1;
            }
            DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(new()
            {
                ProjectDirectory = arguments[2], ProjectId = "tutorial.first-light", DisplayName = "First light",
                SchemaReference = "project-manifest.schema.json", Surface = surface, Input = input.Configuration,
                Catalog = catalog.Catalog, IncludeTutorialStarter = true,
                AllowExistingDirectory = arguments.Length == 8
            }, profiles);
            WriteDiagnostics(created.Diagnostics);
            if (created.IsValid)
            {
                Console.WriteLine($"Tutorial project created: {created.Workspace!.ManifestPath}");
            }
            return created.IsValid ? 0 : 1;
        }
        if (arguments.Length == 7 && arguments[1] == "configure-paths")
        {
            ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Save(arguments[2], new()
            {
                ApplicationRoot = arguments[3], LibraryRoot = arguments[4],
                ComponentDirectory = arguments[5], BuildDirectory = arguments[6]
            });
            Console.WriteLine($"Project paths saved: {paths.SettingsPath}");
            return 0;
        }
        if (arguments.Length == 4 && arguments[1] == "paths")
        {
            ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(arguments[2], arguments[3]);
            Console.WriteLine(JsonSerializer.Serialize(paths, PathJsonOptions));
            return 0;
        }
        if (arguments.Length == 4 && arguments[1] == "export-project")
        {
            ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(arguments[2], arguments[3]);
            return Export(arguments[2], Path.Combine(arguments[3], "profiles", "display-surfaces"),
                Path.Combine(arguments[3], "schema", "widgets.json"), paths.ComponentDirectory, paths.BuildDirectory);
        }
        if (arguments.Length == 6 && arguments[1] == "export")
        {
            return Export(arguments[2], arguments[3], arguments[4], arguments[5]);
        }
        if (arguments.Length is 4 or 5 && arguments[1] is "build" or "build-project")
        {
            ResolvedDesignerProjectPaths? paths = arguments[1] == "build-project"
                ? DesignerProjectPathService.Load(arguments[2], arguments[3]) : null;
            using CancellationTokenSource cancellation = new();
            ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += onCancel;
            try
            {
                SimulatorBuildResult result = SimulatorBuildService.BuildAsync(paths?.ComponentDirectory ?? arguments[2],
                    paths?.LibraryRoot ?? arguments[3], arguments.Length == 5 ? arguments[4] : "Debug",
                    Console.WriteLine, cancellation.Token, buildDirectory: paths?.BuildDirectory)
                    .GetAwaiter().GetResult();
                WriteDiagnostics(result.Diagnostics);
                return result.IsValid ? 0 : 1;
            }
            finally
            {
                Console.CancelKeyPress -= onCancel;
            }
        }
        Console.Error.WriteLine("simulator export <project.json> <surface-catalog> <widgets.json> <output-directory>");
        Console.Error.WriteLine("simulator build <simulation-directory> <LitheView-directory> [Debug|Release]");
        Console.Error.WriteLine("simulator create-tutorial <project-directory> <surface.json> <input.json> <surface-catalog> <widgets.json> [--existing]");
        Console.Error.WriteLine("simulator configure-paths <project.json> <application-root> <library> <component-output> <build-root>");
        Console.Error.WriteLine("simulator paths <project.json> <designer-checkout>");
        Console.Error.WriteLine("simulator export-project <project.json> <designer-checkout>");
        Console.Error.WriteLine("simulator build-project <project.json> <designer-checkout> [Debug|Release]");
        return 2;
    }

    private static int Export(string manifest, string surfaceCatalog, string widgetCatalog, string output, string? build = null)
    {
        DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(surfaceCatalog);
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(widgetCatalog);
        WriteDiagnostics(profiles.Diagnostics);
        WriteDiagnostics(catalog.Diagnostics);
        if (!profiles.IsValid || !catalog.IsValid || catalog.Catalog is null)
        {
            return 1;
        }
        DesignerWorkspaceLoadResult loaded = DesignerWorkspaceService.Open(manifest, profiles, catalog.Catalog);
        WriteDiagnostics(loaded.Diagnostics);
        if (!loaded.IsValid || loaded.Workspace is null)
        {
            return 1;
        }
        SimulatorExportResult result = SimulatorProjectExporter.Export(loaded.Workspace, output, build);
        WriteDiagnostics(result.Diagnostics);
        if (result.IsValid)
        {
            Console.WriteLine($"UI component and simulation exported: {result.OutputDirectory}");
            Console.WriteLine("Surface passed; target unevaluated. application.hpp remains developer-owned.");
        }
        return result.IsValid ? 0 : 1;
    }

    private static void WriteDiagnostics(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        foreach (ProfileDiagnostic diagnostic in diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.Severity}: {diagnostic.Path}: {diagnostic.Message}");
        }
    }
}
