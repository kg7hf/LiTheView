// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service;

internal sealed partial class DesignerService
{
    private static bool SupportsCreation(DisplaySurfaceProfile? profile) =>
        profile is { NativeDisplay.ColorModel: NativeColorModel.Rgb } &&
        profile.PainterSurface.Format is SurfacePixelFormat.Rgb565 or SurfacePixelFormat.Rgb888 or
            SurfacePixelFormat.Xrgb8888 or SurfacePixelFormat.Argb8888;

    private object ProjectOptions() => new
    {
        surfaces = _profiles.Surfaces.Where(item => item.IsValid && SupportsCreation(item.Profile))
            .Select(item => new
            {
                id = item.Profile!.Id, name = item.Profile.DisplayName,
                width = item.Profile.PixelSize.Width, height = item.Profile.PixelSize.Height,
                shape = item.Profile.Shape.ToString(), format = item.Profile.PainterSurface.Format.ToString()
            }).OrderBy(item => item.name).ToArray(),
        nativeDisplays = NativeDisplays().Select(item => new
        {
            id = item.Profile!.Id, name = item.Profile.DisplayName,
            width = item.Profile.Geometry.Width, height = item.Profile.Geometry.Height,
            shape = item.Profile.Geometry.Shape.ToString(), format = item.Profile.Format.ToString(),
            addressSpace = item.Profile.AddressSpace.ToString()
        }).ToArray(),
        logicalInputs = new[] { "outputOnly", "touch", "touchKeys", "encoderSelect" }
    };

    private DisplayDefinitionLoadResult[] NativeDisplays() => NativeDisplayDefinitionFactory.LoadCatalog(
        Path.Combine(_libraryRoot, "profiles", "display-definitions"));

    private object CreateProject(ProjectCreationRequest request)
    {
        string application = AbsolutePath(request.ApplicationRoot, "Application root");
        string directory = AbsolutePath(request.ProjectDirectory, "UI project directory");
        if (!Directory.Exists(application) || !IsWithin(directory, application))
        {
            throw new ServiceException("InvalidProjectPath", "Create the UI inside the open application directory.");
        }
        if (IsWithin(directory, _libraryRoot) && !IsWithin(directory, Path.Combine(_libraryRoot, "build")))
        {
            throw new ServiceException("InvalidProjectPath", "Choose an application UI directory outside LiTheView library sources.");
        }
        int selections = (request.CustomSurface is null ? 0 : 1) + (request.SurfaceId is null ? 0 : 1) +
            (request.NativeDisplayId is null ? 0 : 1) + (request.CustomNativeDisplay is null ? 0 : 1);
        if (selections != 1)
        {
            throw new ServiceException("InvalidSurface", "Select one catalog display or define one custom display.");
        }
        bool native = request.NativeDisplayId is not null || request.CustomNativeDisplay is not null;
        DisplayDefinitionLoadResult? definition = request.CustomNativeDisplay is { } nativeCustom
            ? CreateNativeDefinition(request, nativeCustom)
            : request.NativeDisplayId is { } nativeId
                ? NativeDisplays().SingleOrDefault(item => item.Profile?.Id == nativeId) : null;
        DisplaySurfaceLoadResult? surface = native ? null : request.CustomSurface is { } custom
            ? DisplaySurfaceProfileLoader.Load(Encoding.UTF8.GetBytes(custom.GetRawText()))
            : _profiles.Surfaces.SingleOrDefault(item => item.Profile?.Id == request.SurfaceId);
        if (native && definition is not { IsValid: true })
        {
            throw new ServiceException("InvalidSurface", definition is null ? "Choose a native display from the current catalog."
                : Messages(definition.Diagnostics));
        }
        if (!native && (surface is not { IsValid: true } || !SupportsCreation(surface.Profile)))
        {
            throw new ServiceException("InvalidSurface", surface is null ? "The display is not in this library's catalog."
                : "Choose a supported RGB display. " + Messages(surface.Diagnostics));
        }
        LogicalInputConfigurationProfile input = SelectInput(request.LogicalInput);
        DesignerProjectPaths paths = new()
        {
            ApplicationRoot = Path.GetRelativePath(directory, application).Replace('\\', '/'),
            LibraryRoot = Path.GetRelativePath(application, _libraryRoot).Replace('\\', '/'),
            ComponentDirectory = request.ComponentDirectory,
            BuildDirectory = request.BuildDirectory
        };
        DesignerWorkspaceLoadResult created = native ? DesignerWorkspaceService.CreateNativeDisplayProject(new()
        {
            ProjectDirectory = directory, ProjectId = request.ProjectId, DisplayName = request.DisplayName,
            Definition = definition!, Input = input, Catalog = _catalog,
            AllowExistingDirectory = true, Paths = paths
        }) : DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = directory, ProjectId = request.ProjectId, DisplayName = request.DisplayName,
            SchemaReference = "https://litheview.dev/schema/project-manifest.schema.json",
            Surface = surface!, Input = input, Catalog = _catalog,
            AllowExistingDirectory = true, Paths = paths
        }, _profiles);
        if (!created.IsValid || created.Workspace is null)
        {
            throw new ServiceException("ProjectCreationFailed", Messages(created.Diagnostics));
        }
        DesignerWorkspace workspace = created.Workspace;
        ResolvedDesignerProjectPaths resolved = DesignerProjectPathService.Load(workspace.ManifestPath, _libraryRoot);
        return new
        {
            manifestPath = workspace.ManifestPath, formPath = workspace.FormPath,
            applicationRoot = application, libraryRoot = _libraryRoot,
            outputDirectory = resolved.ComponentDirectory, buildDirectory = resolved.BuildDirectory,
            diagnostics = created.Diagnostics.Select(item => new
            {
                code = item.Code, severity = item.Severity.ToString().ToLowerInvariant(),
                message = item.Message, path = item.Path
            }).ToArray()
        };
    }

    private static DisplayDefinitionLoadResult CreateNativeDefinition(ProjectCreationRequest request,
        NativeDisplayCreationRequest custom)
    {
        SurfacePixelFormat format = custom.Format switch
        {
            "r2Packed2x2" => SurfacePixelFormat.R2Packed2x2,
            "rrrr2222" => SurfacePixelFormat.Rrrr2222,
            "ryry2222" => SurfacePixelFormat.Ryry2222,
            _ => throw new ServiceException("InvalidSurface", "Choose a supported native packed format.")
        };
        SurfaceShape shape = custom.Shape switch
        {
            "rectangle" => SurfaceShape.Rectangle,
            "square" => SurfaceShape.Square,
            "circle" => SurfaceShape.Circle,
            _ => throw new ServiceException("InvalidSurface", "Choose rectangle, square or circle.")
        };
        return NativeDisplayDefinitionFactory.Create(request.ProjectId + ".display", request.DisplayName + " display",
            format, custom.Width, custom.Height, shape, custom.SafeArea, custom.SourceReference);
    }

    private static LogicalInputConfigurationProfile SelectInput(string choice) => choice switch
    {
        "outputOnly" => Input("output-only"),
        "touch" => Input("touch") with { Pointer = true },
        "touchKeys" => Input("touch-keys") with { Pointer = true, Keys = true, Select = true },
        "encoderSelect" => Input("encoder-select") with { Encoder = true, Select = true },
        _ => throw new ServiceException("InvalidInput", "Choose output only, touch, touch and keys, or encoder and select.")
    };

    private static LogicalInputConfigurationProfile Input(string suffix) => new()
    {
        Schema = "https://litheview.dev/schema/logical-input-configuration.schema.json",
        SchemaVersion = 1, Id = "litheview.input." + suffix,
        Pointer = false, Keys = false, Text = false, Encoder = false, Select = false
    };

    private static bool IsWithin(string candidate, string parent)
    {
        string relative = Path.GetRelativePath(parent, candidate);
        return !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
