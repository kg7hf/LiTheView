// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static async Task CheckNativeProjectCreationAsync(DesignerService service, string library,
        string application, bool preview)
    {
        Directory.CreateDirectory(application);
        ValueTask<ServiceResponse> Create(ProjectCreationRequest value) => service.HandleAsync(JsonSerializer.Serialize(
            new ServiceRequest { Id = ++_requestId, ProtocolVersion = 1, Method = "project.create", Params = new() { Project = value } }, Protocol.JsonOptions));
        WidgetCatalog catalog = WidgetCatalogLoader.LoadFile(Path.Combine(library, "schema/widgets.json")).Catalog!;
        DisplaySurfaceCatalogLoadResult noSurfaces = new() { Surfaces = [], Diagnostics = [] };
        ServiceResponse options = await service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest
        {
            Id = ++_requestId, ProtocolVersion = 1, Method = "project.options", Params = new()
        }, Protocol.JsonOptions)).ConfigureAwait(false);
        Require(options.Ok && Result(options).GetProperty("nativeDisplays").GetArrayLength() >= 3,
            "Project options must offer native definitions independently of RGB surfaces.");
        foreach ((string name, string format, int width, int height) in new[]
        {
            ("synthetic-r2", "r2Packed2x2", 512, 384),
            ("synthetic-red-cell", "rrrr2222", 256, 192),
            ("synthetic-red-yellow", "ryry2222", 256, 192)
        })
        {
            foreach (bool custom in new[] { false, true })
            {
                string folder = name + (custom ? "-custom" : "-catalog");
                ProjectCreationRequest request = new()
                {
                    ApplicationRoot = application, ProjectDirectory = Path.Combine(application, folder),
                    ProjectId = "test.native." + folder, DisplayName = folder,
                    NativeDisplayId = custom ? null : "litheview.definition.software." + name,
                    CustomNativeDisplay = custom ? new()
                    {
                        Format = format, Width = 24, Height = 16, Shape = "rectangle",
                        SourceReference = "Developer panel specification, revision 1, page 3"
                    } : null,
                    LogicalInput = custom ? "encoderSelect" : "outputOnly",
                    ComponentDirectory = folder + "/Generated code", BuildDirectory = "build/" + folder
                };
                ServiceResponse created = await Create(request).ConfigureAwait(false);
                Require(created.Ok, "Native wizard creation must succeed: " + created.Error?.Message);
                string manifestPath = Result(created).GetProperty("manifestPath").GetString()!;
                DesignerWorkspace workspace = DesignerWorkspaceService.Open(manifestPath, noSurfaces, catalog).Workspace!;
                Require(workspace.Manifest.SchemaVersion == 3 && workspace.Form.Components.Length == 1 &&
                    workspace.ResolvedProject.Display.Width == (custom ? 24 : width) &&
                    workspace.ResolvedProject.Display.Height == (custom ? 16 : height) &&
                    !File.Exists(Path.Combine(workspace.ProjectDirectory, "profiles/display-surface.json")),
                    "Native creation must preserve logical geometry and publish only native snapshots and a blank form.");
                ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(manifestPath, library);
                Require(paths.ApplicationRoot == application && paths.ComponentDirectory == Path.Combine(application, folder, "Generated code") &&
                    !Directory.Exists(paths.ComponentDirectory), "Native path settings must be committed before explicit generation.");
                if (custom)
                {
                    Require(workspace.ResolvedProject.Display.Definition!.Source.Status == DisplaySourceStatus.Unverified &&
                        workspace.ResolvedProject.Display.Input!.Encoder, "Custom source evidence stays unverified and input remains independent.");
                }
                string xml = File.ReadAllText(workspace.FormPath);
                ServiceResponse generated = await RequestAsync(service, workspace, xml, "export").ConfigureAwait(false);
                Require(generated.Ok && File.Exists(Path.Combine(paths.ComponentDirectory, "form.hpp")),
                    "Wizard native project must generate through the ordinary component path: " + generated.Error?.Message);
                using JsonDocument component = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(paths.ComponentDirectory, "component.json")));
                Require(component.RootElement.GetProperty("targetStatus").GetString() == "unevaluated",
                    "Creating a display must not imply any MCU budget evaluation.");
                if (preview)
                {
                    ServiceResponse rendered = await RequestAsync(service, workspace, xml, "render").ConfigureAwait(false);
                    int scale = format == "r2Packed2x2" ? 1 : 2;
                    Require(rendered.Ok && Result(rendered).GetProperty("status").GetString() == "Ok" &&
                        Result(rendered).GetProperty("width").GetInt32() == (custom ? 24 : width) * scale &&
                        Result(rendered).GetProperty("height").GetInt32() == (custom ? 16 : height) * scale,
                        "Native wizard geometry must reach the worker without reinterpretation.");
                }
            }
        }
        NativeDisplayCreationRequest valid = new()
        {
            Format = "r2Packed2x2", Width = 24, Height = 16, Shape = "rectangle", SourceReference = "Panel specification"
        };
        ProjectCreationRequest basis = new()
        {
            ApplicationRoot = application, ProjectDirectory = Path.Combine(application, "invalid"),
            ProjectId = "test.native.invalid", DisplayName = "Invalid native",
            CustomNativeDisplay = valid, LogicalInput = "touch",
            ComponentDirectory = "invalid/Generated", BuildDirectory = "build/invalid"
        };
        foreach (ProjectCreationRequest invalid in new[]
        {
            basis with { SurfaceId = "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565" },
            basis with { CustomNativeDisplay = null, NativeDisplayId = "unknown" },
            basis with { CustomNativeDisplay = valid with { Format = "rgb565" } },
            basis with { CustomNativeDisplay = valid with { Width = 23 } },
            basis with { CustomNativeDisplay = valid with { Width = 4096, Height = 4096, Format = "rrrr2222" } },
            basis with { CustomNativeDisplay = valid with { Shape = "circle" } },
            basis with { CustomNativeDisplay = valid with { SourceReference = "" } },
            basis with { CustomNativeDisplay = valid with { SafeArea = new() { X = 0, Y = 0, Width = 25, Height = 16 } } },
            basis with { LogicalInput = "unknown" },
            basis with { ComponentDirectory = "invalid/forms" },
            basis with { ComponentDirectory = "invalid/profiles" },
            basis with { BuildDirectory = "invalid" }
        })
        {
            ServiceResponse refused = await Create(invalid).ConfigureAwait(false);
            Require(!refused.Ok && !Directory.Exists(basis.ProjectDirectory),
                "Invalid native definitions, mixed selections, input or paths must publish nothing.");
        }
        Directory.CreateDirectory(basis.ProjectDirectory);
        string sentinel = Path.Combine(basis.ProjectDirectory, "project.litheview.paths.json");
        File.WriteAllText(sentinel, "// application-owned sentinel");
        ServiceResponse collision = await Create(basis).ConfigureAwait(false);
        Require(!collision.Ok && File.ReadAllText(sentinel) == "// application-owned sentinel" &&
            !File.Exists(Path.Combine(basis.ProjectDirectory, "project.litheview.json")) &&
            !Directory.Exists(Path.Combine(basis.ProjectDirectory, "forms")),
            "An existing native path sidecar must stop the whole publication before any project file is written.");
        ServiceResponse circle = await Create(basis with
        {
            ProjectDirectory = Path.Combine(application, "round-native"), ComponentDirectory = "round-native/Generated",
            CustomNativeDisplay = valid with { Height = 24, Shape = "circle" }
        }).ConfigureAwait(false);
        Require(circle.Ok, "Custom native circles must derive an inscribed safe area without manual JSON.");
    }
}
