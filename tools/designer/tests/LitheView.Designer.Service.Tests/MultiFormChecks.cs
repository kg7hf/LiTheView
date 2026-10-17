// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using LitheView.Designer.Core.Projects;
namespace LitheView.Designer.Service.Tests;
internal static partial class Program
{
    private static async Task CheckMultiFormAsync(DesignerService service, string library, string directory)
    {
        var workspace = CreateWorkspace(library, directory);
        string xml = File.ReadAllText(workspace.FormPath);
        async Task<ServiceResponse> Send(string method, string formPath, string text, string? name = null) =>
            await service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest
            {
                Id = ++_requestId, ProtocolVersion = 1, Method = method,
                Params = new() { ManifestPath = workspace.ManifestPath, FormPath = formPath, Xml = text, DocumentVersion = 1, ScreenName = name }
            }, Protocol.JsonOptions)).ConfigureAwait(false);
        var added = await Send("project.addScreen", workspace.FormPath, xml, "About").ConfigureAwait(false);
        Require(added.Ok, "Plan screen creation: " + added.Error?.Message);
        var plan = Result(added);
        string about = plan.GetProperty("formPath").GetString()!;
        string aboutXml = plan.GetProperty("xml").GetString()!;
        Require(!File.Exists(about), "Service proposes Add screen without writing documents.");
        File.WriteAllText(about, aboutXml);
        File.WriteAllText(workspace.ManifestPath, plan.GetProperty("after").GetString());
        var duplicate = await Send("project.addScreen", about, aboutXml, "about").ConfigureAwait(false);
        Require(!duplicate.Ok, "Case-colliding screen rejected.");
        var handlers = await Send("handlers", about, aboutXml).ConfigureAwait(false);
        Require(handlers.Ok && Result(handlers).GetProperty("handlerPath").GetString()!.EndsWith(Path.Combine("handlers", "About.hpp"), StringComparison.Ordinal) &&
            Result(handlers).GetProperty("className").GetString() == "AboutForm" &&
            Result(handlers).GetProperty("baseType").GetString() == "lithe::generated::About", "Each new screen owns its typed class and handler file.");
        var exported = await Send("export", about, aboutXml).ConfigureAwait(false);
        Require(exported.Ok, "Second screen exports through the standard service: " + exported.Error?.Message);
        Require(Result(exported).GetProperty("outputDirectory").GetString()!.EndsWith("About", StringComparison.Ordinal), "Export is isolated per screen.");
        Require(File.ReadAllText(workspace.FormPath) == xml, "Adding/generating preserves the original form.");
    }
}
