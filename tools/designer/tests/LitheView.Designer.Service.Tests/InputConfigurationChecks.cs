// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static async Task CheckInputConfigurationAsync(DesignerService service, string library, string application)
    {
        Directory.CreateDirectory(application);
        WidgetCatalog catalog = WidgetCatalogLoader.LoadFile(Path.Combine(library, "schema/widgets.json")).Catalog!;
        DisplaySurfaceCatalogLoadResult surfaces = ProfileCatalogLoader.LoadSurfaces(Path.Combine(library, "profiles/display-surfaces"));
        foreach (bool native in new[] { false, true })
        {
            string name = native ? "native" : "rgb";
            ServiceResponse created = await service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest
            {
                Id = ++_requestId, ProtocolVersion = 1, Method = "project.create", Params = new() { Project = new()
                {
                    ApplicationRoot = application, ProjectDirectory = Path.Combine(application, name),
                    ProjectId = "test.input." + name, DisplayName = "Input lesson",
                    SurfaceId = native ? null : "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565",
                    NativeDisplayId = native ? "litheview.definition.software.synthetic-red-cell" : null,
                    LogicalInput = "outputOnly", ComponentDirectory = name + "/Generated", BuildDirectory = "build/" + name
                } }
            }, Protocol.JsonOptions)).ConfigureAwait(false);
            Require(created.Ok, "Input lesson project creation: " + created.Error?.Message);
            string manifestPath = Result(created).GetProperty("manifestPath").GetString()!;
            DesignerWorkspace workspace = DesignerWorkspaceService.Open(manifestPath, surfaces, catalog).Workspace!;
            string inputPath = Path.Combine(workspace.ProjectDirectory, DesignerProductResolver.InputRelativePath);
            string definitionPath = Path.Combine(workspace.ProjectDirectory, DesignerProductResolver.DefinitionRelativePath);
            string definition = File.ReadAllText(definitionPath);
            string xml = File.ReadAllText(workspace.FormPath);
            string handlerPath = Path.Combine(workspace.ProjectDirectory, "form_handlers.hpp");
            File.WriteAllText(handlerPath, "// handwritten behavior remains unchanged");
            ValueTask<ServiceResponse> Configure(string? choice) => service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest
            {
                Id = ++_requestId, ProtocolVersion = 1, Method = "project.input", Params = new()
                {
                    ManifestPath = manifestPath, FormPath = workspace.FormPath,
                    Xml = xml, DocumentVersion = 9, LogicalInput = choice
                }
            }, Protocol.JsonOptions));
            foreach (string choice in new[] { "touch", "touchKeys", "encoderSelect", "outputOnly" })
            {
                string before = File.ReadAllText(manifestPath);
                string inputBefore = File.ReadAllText(inputPath);
                ServiceResponse result = await Configure(choice).ConfigureAwait(false);
                Require(result.Ok && Result(result).GetProperty("edits").GetArrayLength() == 2,
                    "Input changes propose a paired edit: " + result.Error?.Message);
                Require(File.ReadAllText(manifestPath) == before && File.ReadAllText(inputPath) == inputBefore,
                    "The service only proposes an input edit; VS Code owns applying and saving it.");
                foreach (JsonElement edit in Result(result).GetProperty("edits").EnumerateArray())
                {
                    string path = edit.GetProperty("path").GetString()!;
                    Require((path == inputPath || path == manifestPath) &&
                        File.ReadAllText(path) == edit.GetProperty("before").GetString(), "Input plan preconditions match its two owned documents.");
                    File.WriteAllText(path, edit.GetProperty("after").GetString());
                }
                DesignerWorkspaceLoadResult reopened = DesignerWorkspaceService.Open(manifestPath, surfaces, catalog);
                Require(reopened.IsValid && reopened.Workspace!.Manifest.SchemaVersion == (native ? 3 : 2),
                    "The changed input and manifest locks reopen without a display or schema migration.");
                workspace = reopened.Workspace!;
                Require(workspace.ResolvedProject.Display.Input.Pointer == (choice is "touch" or "touchKeys") &&
                    workspace.ResolvedProject.Display.Input.Encoder == (choice == "encoderSelect"),
                    "The resolved input matches the chosen capabilities.");
                ServiceResponse repeated = await Configure(choice).ConfigureAwait(false);
                Require(repeated.Ok && Result(repeated).GetProperty("edits").GetArrayLength() == 0 &&
                    Result(repeated).GetProperty("currentInput").GetString() == choice, "Selecting current input is a no-op.");
                ServiceResponse generated = await RequestAsync(service, workspace, xml, "export").ConfigureAwait(false);
                Require(generated.Ok, "Regeneration after changing input uses existing component output: " + generated.Error?.Message);
                var exported = LogicalInputConfigurationLoader.LoadFile(Path.Combine(application, name, "Generated/logical-input.json"));
                Require(exported.IsValid && exported.Sha256 == workspace.Manifest.LogicalInput!.Sha256,
                    "Generated input carries the updated lock and selected capabilities.");
            }
            ServiceResponse invalid = await Configure("unsupported").ConfigureAwait(false);
            Require(!invalid.Ok && invalid.Error?.Code == "InvalidInput", "Unknown choices are rejected.");
            Require(File.ReadAllText(workspace.FormPath) == xml && File.ReadAllText(definitionPath) == definition &&
                File.ReadAllText(handlerPath) == "// handwritten behavior remains unchanged", "Input changes preserve the form, display and handler.");
            string lockedInput = File.ReadAllText(inputPath);
            File.WriteAllText(inputPath, lockedInput.Replace("\"pointer\":false", "\"pointer\":true", StringComparison.Ordinal));
            ServiceResponse stale = await Configure("touch").ConfigureAwait(false);
            Require(!stale.Ok && stale.Error?.Code == "InvalidProject", "Configure Input must not silently repair a tampered snapshot lock.");
            File.WriteAllText(inputPath, lockedInput);
        }
    }
}
