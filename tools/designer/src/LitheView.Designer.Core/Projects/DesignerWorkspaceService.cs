// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Projects;

public sealed record NewDesignerProjectRequest
{
    public required string ProjectDirectory { get; init; }

    public required string ProjectId { get; init; }

    public required string DisplayName { get; init; }

    public required string SchemaReference { get; init; }

    public required DisplaySurfaceLoadResult Surface { get; init; }

    public LogicalInputConfigurationProfile? Input { get; init; }

    public bool IncludeTutorialStarter { get; init; }

    public DesignerProjectPaths? Paths { get; init; }

    // Opt in when adding a UI to an existing application directory. Existing
    // files are never replaced, including collisions with starter filenames.
    public bool AllowExistingDirectory { get; init; }

    public required WidgetCatalog Catalog { get; init; }
}

public sealed record DesignerWorkspace
{
    public required string ProjectDirectory { get; init; }

    public required string ManifestPath { get; init; }

    public required string FormPath { get; init; }

    public required DesignerProjectManifest Manifest { get; init; }

    public required ResolvedDesignerProject ResolvedProject { get; init; }

    public required FormDocument Form { get; init; }

    public required WidgetCatalog Catalog { get; init; }
}

public sealed record DesignerWorkspaceLoadResult
{
    public DesignerWorkspace? Workspace { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        Workspace is not null && Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public sealed record DesignerCppExportResult
{
    public string? HeaderPath { get; init; }

    public string? IntegrationPath { get; init; }

    public string? CandidatePath { get; init; }

    public string? SourceXmlSha256 { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid =>
        HeaderPath is not null &&
        IntegrationPath is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static partial class DesignerWorkspaceService
{
    public const string ManifestFileName = "project.litheview.json";
    public const string DefaultFormRelativePath = "forms/main.litheview.xml";
    private const string ProjectManifestSchema = "https://litheview.dev/schema/project-manifest.schema.json";

    public static DesignerWorkspaceLoadResult CreateNew(
        NewDesignerProjectRequest request,
        DisplaySurfaceCatalogLoadResult profiles)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profiles);

        List<ProfileDiagnostic> diagnostics = [.. profiles.Diagnostics];
        if (request.Surface.CanonicalUtf8 is null)
        {
            diagnostics.Add(Error(
                "LVDW0008",
                "$.profiles",
                "Project creation requires a canonical display-surface snapshot."));
            return Failed(diagnostics);
        }
        DesignerProjectLoadResult created = DesignerProjectFactory.Create(
            request.SchemaReference,
            request.ProjectId,
            request.DisplayName,
            request.Surface);
        diagnostics.AddRange(created.Diagnostics);
        if (!created.IsValid || created.Manifest is null || request.Surface.Profile is null)
        {
            return Failed(diagnostics);
        }

        byte[] currentSurface = DisplaySurfaceCanonicalizer.Canonicalize(request.Surface.Profile);
        if (!currentSurface.SequenceEqual(request.Surface.CanonicalUtf8) ||
            DisplaySurfaceCanonicalizer.ComputeSha256(currentSurface) != request.Surface.Sha256)
        {
            diagnostics.Add(Error("LVDP1001", "$.surfaceProfile",
                "The supplied editing surface changed after its canonical snapshot was loaded."));
            return Failed(diagnostics);
        }

        DisplayDefinitionLoadResult? definition = null;
        LogicalInputConfigurationLoadResult? input = null;
        if (request.Surface.Profile.NativeDisplay.ColorModel == NativeColorModel.Rgb)
        {
            DisplayDefinitionLoadResult adapted = DisplayDefinitionProfileLoader.AdaptV1(request.Surface);
            diagnostics.AddRange(adapted.Diagnostics);
            if (adapted.Profile is not null)
            {
                definition = DisplayDefinitionProfileLoader.Load(
                    DisplayDefinitionCanonicalizer.Canonicalize(adapted.Profile));
                diagnostics.AddRange(definition.Diagnostics);
            }
            input = request.Input is null
                ? DesignerProductResolver.DefaultInput(request.Surface.Profile)
                : LogicalInputConfigurationLoader.Load(
                    LogicalInputConfigurationCanonicalizer.Canonicalize(request.Input));
            diagnostics.AddRange(input.Diagnostics);
            if (definition is not { IsValid: true } || !input.IsValid)
            {
                return Failed(diagnostics);
            }
        }
        else
        {
            diagnostics.Add(new("LVDP1005", ProfileDiagnosticSeverity.Warning, "$.surfaceProfile",
                "This project retains its version-1 MDP RGB staging interpretation; a native product is a separate version-3 project created from an explicit packed definition."));
            if (request.Input is not null || request.IncludeTutorialStarter)
            {
                diagnostics.Add(Error("LVDP1006", "$",
                    "Independent input and the tutorial starter require a supported RGB editing surface; legacy MDP staging is preserved unchanged."));
                return Failed(diagnostics);
            }
        }
        DesignerProjectManifest manifest = created.Manifest with
        {
            Forms = [DefaultFormRelativePath],
            Schema = definition is null ? created.Manifest.Schema : ProjectManifestSchema,
            SchemaVersion = definition is null ? 1 : 2,
            SchemaCompatibility = created.Manifest.SchemaCompatibility with
            {
                ProjectManifest = definition is null ? 1 : 2,
                DisplayDefinition = definition is null ? null : 2,
                LogicalInput = definition is null ? null : 1
            },
            DisplayDefinition = definition is null ? null : new()
            {
                Id = definition.Profile!.Id, Sha256 = definition.Sha256!
            },
            LogicalInput = input is null ? null : new()
            {
                Id = input.Configuration!.Id, Sha256 = input.Sha256!
            }
        };
        DesignerProjectLoadResult finalManifest = DesignerProjectLoader.Load(
            DesignerProjectCanonicalizer.Canonicalize(manifest));
        diagnostics.AddRange(finalManifest.Diagnostics);
        if (!finalManifest.IsValid || finalManifest.Manifest is null || finalManifest.CanonicalUtf8 is null)
        {
            return Failed(diagnostics);
        }

        FormDocument form = CreateDefaultForm(
            request.DisplayName,
            request.Surface.Profile.Id,
            request.Surface.Profile.PixelSize.Width,
            request.Surface.Profile.PixelSize.Height);
        if (request.IncludeTutorialStarter)
        {
            DesignerTutorialStarterResult starter = DesignerTutorialStarter.CreateForm(
                form, definition!.Profile!, input!.Configuration!);
            diagnostics.AddRange(starter.Diagnostics);
            if (starter.Form is null)
            {
                return Failed(diagnostics);
            }
            form = starter.Form;
        }
        FormXmlWriteResult formWrite = FormXmlWriter.Write(form, request.Catalog);
        diagnostics.AddRange(formWrite.Diagnostics);
        if (!formWrite.IsValid || formWrite.Utf8 is null ||
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
            (ManifestFileName, finalManifest.CanonicalUtf8),
            (DefaultFormRelativePath, formWrite.Utf8),
            ("profiles/display-surface.json", request.Surface.CanonicalUtf8!)
        ];
        if (request.Paths is not null)
        {
            files.Add(("project.litheview.paths.json", DesignerProjectPathService.Serialize(request.Paths)));
        }
        if (definition is not null && input is not null)
        {
            files.Add((DesignerProductResolver.DefinitionRelativePath, definition.CanonicalUtf8!));
            files.Add((DesignerProductResolver.InputRelativePath, input.CanonicalUtf8!));
        }
        if (request.IncludeTutorialStarter)
        {
            files.Add(("simulation/application.hpp", DesignerTutorialStarter.ApplicationSource));
        }
        if (!PublishNewProject(destination, request.AllowExistingDirectory, files, diagnostics))
        {
            return Failed(diagnostics);
        }

        return Open(
            Path.Combine(destination, ManifestFileName),
            profiles,
            request.Catalog,
            diagnostics);
    }

    private static string? ProjectDestination(string projectDirectory, List<ProfileDiagnostic> diagnostics)
    {
        try
        {
            string destination = Path.GetFullPath(projectDirectory);
            GeneratedFileAccess.RejectReparsePath(destination);
            GeneratedFileAccess.RejectFileAncestors(destination);
            return destination;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(Error("LVDW0001", "$.projectDirectory", exception.Message));
            return null;
        }
    }

    // Files are staged beside the destination and published without merging into or
    // overwriting existing files. RGB and native display creation share this path.
    private static bool PublishNewProject(
        string destination,
        bool allowExistingDirectory,
        IEnumerable<(string RelativePath, byte[] Contents)> files,
        List<ProfileDiagnostic> diagnostics)
    {
        if (File.Exists(destination) || Directory.Exists(destination) && !allowExistingDirectory)
        {
            diagnostics.Add(Error(
                "LVDW0002",
                "$.projectDirectory",
                "The new project directory already exists; creation never merges with or overwrites it."));
            return false;
        }

        string? parent = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(parent))
        {
            diagnostics.Add(Error(
                "LVDW0003",
                "$.projectDirectory",
                "The new project directory must have a parent directory."));
            return false;
        }
        string staging = Path.Combine(
            parent,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.litheview-new");
        try
        {
            foreach ((string relativePath, byte[] contents) in files)
            {
                AtomicFileWriter.WriteAllBytes(Path.Combine(staging, relativePath), contents);
            }
            if (Directory.Exists(destination))
            {
                PublishIntoExistingDirectory(staging, destination);
                TryDeleteStagingDirectory(staging);
            }
            else
            {
                Directory.Move(staging, destination);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            diagnostics.Add(Error("LVDW0004", "$.projectDirectory", exception.Message));
            TryDeleteStagingDirectory(staging);
            return false;
        }
        return true;
    }

    private static void PublishIntoExistingDirectory(string staging, string destination)
    {
        // Preflight every file before publishing any. The manifest is published
        // last so a partial copy cannot appear to be a complete designer project.
        string[] stagedFiles = Directory.GetFiles(staging, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(staging, path) == ManifestFileName ? 1 : 0)
            .ToArray();
        foreach (string source in stagedFiles)
        {
            string target = Path.Combine(destination, Path.GetRelativePath(staging, source));
            GeneratedFileAccess.RejectReparsePath(target);
            GeneratedFileAccess.RejectFileAncestors(target);
            if (File.Exists(target) || Directory.Exists(target))
            {
                throw new IOException($"Project creation would replace existing file '{target}'. Choose another UI folder or open its existing project.");
            }
        }
        List<(string Path, byte[] Contents)> published = [];
        try
        {
            foreach (string source in stagedFiles)
            {
                string target = Path.Combine(destination, Path.GetRelativePath(staging, source));
                byte[] contents = File.ReadAllBytes(source);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target, overwrite: false);
                published.Add((target, contents));
            }
        }
        catch (IOException)
        {
            RollBackPublishedFiles(published);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            RollBackPublishedFiles(published);
            throw;
        }
    }

    private static void RollBackPublishedFiles(List<(string Path, byte[] Contents)> published)
    {
        foreach ((string path, byte[] contents) in published)
        {
            try
            {
                // Only undo our own new files; preserve concurrent edits.
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(contents))
                {
                    File.Delete(path);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static DesignerWorkspaceLoadResult Open(
        string manifestPath,
        DisplaySurfaceCatalogLoadResult profiles,
        WidgetCatalog catalog)
    {
        return Open(manifestPath, profiles, catalog, []);
    }

    public static FormXmlWriteResult SaveForm(DesignerWorkspace workspace, FormDocument form)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(form);

        FormXmlWriteResult result = FormXmlWriter.Write(form, workspace.Catalog);
        if (result.IsValid && result.Utf8 is not null)
        {
            AtomicFileWriter.WriteAllBytes(workspace.FormPath, result.Utf8);
        }
        return result;
    }

    public static DesignerCppExportResult ExportCpp(
        DesignerWorkspace workspace,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        FormCppCompilationResult compilation = FormCppCompiler.Compile(
            workspace.Form,
            workspace.Catalog);
        if (!compilation.IsValid ||
            compilation.HeaderUtf8 is null ||
            compilation.IntegrationUtf8 is null)
        {
            return new DesignerCppExportResult { Diagnostics = compilation.Diagnostics };
        }

        string directory = Path.GetFullPath(outputDirectory);
        string headerPath = Path.Combine(directory, "main_form.hpp");
        string integrationPath = Path.Combine(directory, "main_form.cpp");
        string candidatePath = integrationPath + ".candidate";
        try
        {
            byte[]? existing = File.Exists(integrationPath)
                ? File.ReadAllBytes(integrationPath)
                : null;
            PreservedRegionMergeResult merged = PreservedRegionMerger.Merge(
                compilation.IntegrationUtf8,
                existing);
            if (!merged.IsValid || merged.Utf8 is null)
            {
                AtomicFileWriter.WriteAllBytes(candidatePath, compilation.IntegrationUtf8);
                List<ProfileDiagnostic> diagnostics =
                [
                    .. compilation.Diagnostics,
                    .. merged.Diagnostics
                ];
                diagnostics.Add(Error(
                    "LVDW0010",
                    integrationPath,
                    "Existing user code was not overwritten; reconcile the generated .candidate file."));
                return new DesignerCppExportResult
                {
                    CandidatePath = candidatePath,
                    SourceXmlSha256 = compilation.SourceXmlSha256,
                    Diagnostics = diagnostics
                };
            }
            AtomicFileWriter.WriteAllBytes(headerPath, compilation.HeaderUtf8);
            AtomicFileWriter.WriteAllBytes(integrationPath, merged.Utf8);
            return new DesignerCppExportResult
            {
                HeaderPath = headerPath,
                IntegrationPath = integrationPath,
                SourceXmlSha256 = compilation.SourceXmlSha256,
                Diagnostics = [.. compilation.Diagnostics, .. merged.Diagnostics]
            };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new DesignerCppExportResult
            {
                Diagnostics =
                [
                    Error("LVDW0009", outputDirectory, exception.Message)
                ]
            };
        }
    }

    private static DesignerWorkspaceLoadResult Open(
        string manifestPath,
        DisplaySurfaceCatalogLoadResult profiles,
        WidgetCatalog catalog,
        List<ProfileDiagnostic> diagnostics)
    {
        DesignerProjectLoadResult loaded = DesignerProjectLoader.LoadFile(manifestPath);
        diagnostics.AddRange(loaded.Diagnostics);
        string fullManifest = Path.GetFullPath(manifestPath);
        string projectDirectory = Path.GetDirectoryName(fullManifest)!;
        DisplaySurfaceCatalogLoadResult projectProfiles = LoadProjectProfiles(projectDirectory, profiles);
        diagnostics.AddRange(projectProfiles.Diagnostics);
        DesignerProjectResolutionResult resolution = DesignerProjectResolver.Resolve(
            loaded,
            projectProfiles.Surfaces,
            projectDirectory);
        diagnostics.AddRange(resolution.Diagnostics);
        if (!resolution.IsValid || resolution.Project is null || loaded.Manifest is null)
        {
            return Failed(diagnostics);
        }
        if (loaded.Manifest.Forms.Length == 0)
        {
            diagnostics.Add(Error("LVDW0005", "$.forms", "The designer requires at least one form."));
            return Failed(diagnostics);
        }

        string formPath = ResolveProjectPath(projectDirectory, loaded.Manifest.Forms[0]);
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(formPath, catalog, FormImportMode.Strict);
        diagnostics.AddRange(form.Diagnostics);
        if (!form.IsValid || form.Document is null)
        {
            return Failed(diagnostics);
        }
        DesignerWorkspace workspace = new()
        {
            ProjectDirectory = projectDirectory,
            ManifestPath = fullManifest,
            FormPath = formPath,
            Manifest = loaded.Manifest,
            ResolvedProject = resolution.Project,
            Form = form.Document,
            Catalog = catalog
        };
        if (workspace.Manifest.SchemaVersion == 1)
        {
            diagnostics.Add(new("LVDP1002", ProfileDiagnosticSeverity.Warning, "$",
                "Legacy project opened without migration; modern product export requires explicit display/input adaptation."));
        }
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error)
            ? Failed(diagnostics)
            : new() { Workspace = workspace, Diagnostics = diagnostics };
    }

    private static FormDocument CreateDefaultForm(
        string projectName,
        string displayId,
        int width,
        int height)
    {
        return new FormDocument
        {
            SchemaVersion = 1,
            Id = "form.main",
            DisplayName = $"{projectName} Main Form",
            SurfaceProfileId = displayId,
            Components =
            [
                new FormComponent
                {
                    ComponentId = "component.main.root",
                    Name = "rootPanel",
                    WidgetId = "lithe.widget.panel",
                    ParentComponentId = null,
                    SiblingOrder = 0,
                    Properties =
                    [
                        new FormRectangleValue(
                            "frame",
                            new FormRectangle(0, 0, width, height)),
                        // Form colors use the legacy inverse-alpha/BGR encoding.
                        new FormColorValue("background-color", 0x00201810U)
                    ],
                    HistoricalAttributes = []
                }
            ],
            Actions = [],
            Bindings = [],
            Resources = [],
            ThemeColors = []
        };
    }

    private static DisplaySurfaceCatalogLoadResult LoadProjectProfiles(
        string projectDirectory,
        DisplaySurfaceCatalogLoadResult repositoryProfiles)
    {
        List<ProfileDiagnostic> diagnostics = [];
        List<DisplaySurfaceLoadResult> localSurfaces = [];
        string surfacePath = Path.Combine(projectDirectory, "profiles", "display-surface.json");
        if (File.Exists(surfacePath))
        {
            DisplaySurfaceLoadResult local = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
            diagnostics.AddRange(local.Diagnostics);
            if (local.IsValid)
            {
                localSurfaces.Add(local);
            }
        }

        HashSet<string> localSurfaceIds = [.. localSurfaces.Select(item => item.Profile!.Id)];
        return new DisplaySurfaceCatalogLoadResult
        {
            Surfaces =
            [
                .. localSurfaces,
                .. repositoryProfiles.Surfaces.Where(item =>
                    item.Profile is null || !localSurfaceIds.Contains(item.Profile.Id))
            ],
            Diagnostics = diagnostics
        };
    }

    private static string ResolveProjectPath(string projectDirectory, string relativePath)
    {
        string root = Path.GetFullPath(projectDirectory) + Path.DirectorySeparatorChar;
        string resolved = Path.GetFullPath(Path.Combine(projectDirectory, relativePath));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A project path resolves outside the project directory.");
        }
        return resolved;
    }

    private static void TryDeleteStagingDirectory(string staging)
    {
        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The primary creation diagnostic remains authoritative. A stale staging directory is
            // deliberately hidden and uniquely named so a later run cannot treat it as a project.
        }
    }

    private static ProfileDiagnostic Error(string code, string path, string message)
    {
        return new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message);
    }

    private static DesignerWorkspaceLoadResult Failed(IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        return new DesignerWorkspaceLoadResult { Diagnostics = diagnostics };
    }
}
