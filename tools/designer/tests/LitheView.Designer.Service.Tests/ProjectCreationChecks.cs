// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static async Task CheckProjectCreationAsync(DesignerService service, string library, string directory, bool native)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "CMakeLists.txt"), "# Application-owned build\n");
        Directory.CreateDirectory(Path.Combine(directory, ".vscode"));
        string tasks = Path.Combine(directory, ".vscode", "tasks.json");
        File.WriteAllText(tasks, "// Existing tasks must remain byte-identical.\n");
        ProjectCreationRequest request = new()
        {
            ApplicationRoot = directory, ProjectDirectory = Path.Combine(directory, "Tasks", "UI"),
            DisplayName = "First Light", ProjectId = "test.first-light",
            SurfaceId = "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565", LogicalInput = "outputOnly",
            ComponentDirectory = "Tasks/UI/Generated", BuildDirectory = "out/first light"
        };
        ValueTask<ServiceResponse> Create(ProjectCreationRequest value) => service.HandleAsync(JsonSerializer.Serialize(
            new ServiceRequest { Id = ++_requestId, ProtocolVersion = 1, Method = "project.create", Params = new() { Project = value } }, Protocol.JsonOptions));
        ServiceResponse invalid = await Create(request with { ComponentDirectory = "Tasks/UI/forms" }).ConfigureAwait(false);
        Require(!invalid.Ok && !Directory.Exists(request.ProjectDirectory), "Invalid output paths must publish no project files.");
        ServiceResponse created = await Create(request).ConfigureAwait(false);
        Require(created.Ok && !Directory.Exists(Path.Combine(directory, "Tasks/UI/Generated")), "Creation must save the blank form without generating code: " + created.Error?.Message);
        string manifest = Result(created).GetProperty("manifestPath").GetString()!;
        WidgetCatalog catalog = WidgetCatalogLoader.LoadFile(Path.Combine(library, "schema/widgets.json")).Catalog!;
        DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(Path.Combine(library, "profiles/display-surfaces"));
        DesignerWorkspace workspace = DesignerWorkspaceService.Open(manifest, profiles, catalog).Workspace!;
        string xml = File.ReadAllText(workspace.FormPath);
        Require(workspace.Form.Components.Length == 1 && workspace.Form.Actions.Length == 0 && !xml.Contains("borderWidth=", StringComparison.Ordinal),
            "The new form must contain a blank root panel with no border or action contracts.");
        ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(manifest, library);
        Require(paths.ComponentDirectory == Path.GetFullPath(Path.Combine(directory, "Tasks/UI/Generated")) && paths.ApplicationRoot == directory,
            "The committed path sidecar must resolve relative to the application and manifest.");
        Require(File.ReadAllText(tasks) == "// Existing tasks must remain byte-identical.\n" &&
            File.ReadAllText(Path.Combine(directory, "CMakeLists.txt")) == "# Application-owned build\n", "Creation must preserve firmware files and VS Code tasks.");
        ServiceResponse collision = await Create(request).ConfigureAwait(false);
        Require(!collision.Ok && File.ReadAllText(workspace.FormPath) == xml, "Repeated creation must reject existing UI files without rewriting them.");
        foreach ((string property, string value) in new[] { ("background-color", "#00303030"), ("border-color", "#00ffffff") })
        {
            ServiceResponse edited = await PropertyAsync(service, workspace, xml, "component.main.root", property, value).ConfigureAwait(false);
            Require(edited.Ok, "The blank panel must accept a color property: " + edited.Error?.Message);
            xml = Result(edited).GetProperty("xml").GetString()!;
        }
        ServiceResponse border = await PropertyAsync(service, workspace, xml, "component.main.root", "border-width", 2).ConfigureAwait(false);
        Require(border.Ok, "The developer must be able to add the border through the property editor.");
        xml = Result(border).GetProperty("xml").GetString()!;
        File.WriteAllText(workspace.FormPath, xml);
        ServiceResponse generated = await RequestAsync(service, workspace, xml, "export").ConfigureAwait(false);
        Require(generated.Ok && File.Exists(Path.Combine(paths.ComponentDirectory, "form.hpp")),
            "Generate after saving the edited form must produce the component: " + generated.Error?.Message);
        if (native)
        {
            ServiceResponse render = await RequestAsync(service, workspace, xml, "render").ConfigureAwait(false);
            byte[] rgba = Convert.FromBase64String(Result(render).GetProperty("rgbaBase64").GetString()!);
            Require(render.Ok && Result(render).GetProperty("status").GetString() == "Ok" && rgba.Take(4).SequenceEqual(new byte[] { 255, 255, 255, 255 }),
                "Native rendering must show the white border at the top-left pixel.");
            Require(rgba[(136 * 480 + 240) * 4] is >= 40 and <= 55, "The middle of the box must contain its gray fill.");
        }
        string blocked = Path.Combine(directory, "blocked");
        Directory.CreateDirectory(blocked);
        File.WriteAllText(Path.Combine(blocked, "project.litheview.paths.json"), "keep");
        ServiceResponse sidecarCollision = await Create(request with { ProjectDirectory = blocked }).ConfigureAwait(false);
        Require(!sidecarCollision.Ok && !File.Exists(Path.Combine(blocked, "project.litheview.json")) &&
            !File.Exists(Path.Combine(blocked, "forms/main.litheview.xml")), "Sidecar collision must be detected before publishing forms or manifests.");
        DisplaySurfaceProfile basis = profiles.Surfaces.Single(item => item.Profile?.Id == request.SurfaceId).Profile!;
        DisplaySurfaceProfile custom = basis with
        {
            Id = "test.round-custom", Shape = SurfaceShape.Circle,
            PixelSize = new() { Width = 128, Height = 128 }, PhysicalSize = new() { DiameterMillimeters = 32M },
            SafeArea = new() { X = 20, Y = 20, Width = 88, Height = 88 }
        };
        ServiceResponse round = await Create(request with
        {
            ProjectDirectory = Path.Combine(directory, "round"), SurfaceId = null,
            CustomSurface = JsonDocument.Parse(DisplaySurfaceCanonicalizer.Canonicalize(custom)).RootElement.Clone(),
            LogicalInput = "encoderSelect", ComponentDirectory = "round/Generated", BuildDirectory = "out/round"
        }).ConfigureAwait(false);
        Require(round.Ok, "A custom circular display and independent encoder input must be created.");
        DesignerWorkspace roundWorkspace = DesignerWorkspaceService.Open(Result(round).GetProperty("manifestPath").GetString()!, profiles, catalog).Workspace!;
        Require(roundWorkspace.Form.Components.Length == 1 && roundWorkspace.Form.Actions.Length == 0,
            "A round display must also start with a blank root and no tutorial controls.");
        string conflictDirectory = Path.Combine(directory, "collision-output");
        Directory.CreateDirectory(conflictDirectory);
        File.WriteAllText(Path.Combine(conflictDirectory, "CMakeLists.txt"), "keep this output");
        ServiceResponse conflict = await Create(request with
        {
            ProjectDirectory = Path.Combine(directory, "recoverable"), ComponentDirectory = "collision-output"
        }).ConfigureAwait(false);
        Require(conflict.Ok, "Creating source does not export into a conflicting output folder.");
        DesignerWorkspace conflictWorkspace = DesignerWorkspaceService.Open(Result(conflict).GetProperty("manifestPath").GetString()!, profiles, catalog).Workspace!;
        ServiceResponse conflictExport = await RequestAsync(service, conflictWorkspace, File.ReadAllText(conflictWorkspace.FormPath), "export").ConfigureAwait(false);
        Require(conflictExport.Error?.Code == "ExportFailed" &&
            File.Exists(Result(conflict).GetProperty("manifestPath").GetString()) &&
            File.ReadAllText(Path.Combine(conflictDirectory, "CMakeLists.txt")) == "keep this output",
            "An export collision must retain the source project, report its location, and preserve unowned output.");
    }
}
