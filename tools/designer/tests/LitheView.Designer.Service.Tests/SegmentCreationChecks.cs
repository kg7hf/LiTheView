// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using LitheView.Designer.Core.Segments;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static async Task CheckSegmentCreationAsync(DesignerService service, string library, string application)
    {
        Directory.CreateDirectory(application);
        string sentinel = Path.Combine(application, "application.cpp");
        File.WriteAllText(sentinel, "// application-owned FSM");
        JsonElement glass = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(library,
            "examples/efm32tg-stk3300-menu-1-complete/UI/design/glass.segment-display.json")));
        ProjectCreationRequest options = new()
        {
            ApplicationRoot = application, ProjectDirectory = Path.Combine(application, "UI", "Designer"),
            ProjectId = "application.custom-menu", DisplayName = "My segmented menu", SegmentGlass = glass,
            LogicalInput = "custom", Input = new() { Pointer = false, Keys = true, Text = false, Encoder = true, Select = true },
            ComponentDirectory = "UI/Generated", BuildDirectory = "build"
        };
        ValueTask<ServiceResponse> Create(ProjectCreationRequest value) => service.HandleAsync(JsonSerializer.Serialize(
            new ServiceRequest { Id = ++_requestId, ProtocolVersion = 1, Method = "project.create", Params = new() { Project = value } }, Protocol.JsonOptions));
        foreach (var bad in new[] {
            options with { SurfaceId = "also-selected" }, options with { ProjectId = "bad id!" },
            options with { ComponentDirectory = "UI/Designer/profiles" }, options with { ComponentDirectory = "UI/Designer" },
            options with { LogicalInput = "custom", Input = null }, options with { LogicalInput = "touch" },
            options with { SegmentGlass = JsonSerializer.Deserialize<JsonElement>("{}") } })
        {
            var failed = await Create(bad).ConfigureAwait(false);
            Require(!failed.Ok && !Directory.Exists(options.ProjectDirectory), "Invalid segmented creation publishes no project: " + failed.Error?.Message);
        }
        var result = await Create(options).ConfigureAwait(false);
        Require(result.Ok, "Segmented New Project succeeds: " + result.Error?.Message);
        string manifest = Result(result).GetProperty("manifestPath").GetString()!;
        var project = SegmentProject.Load(manifest);
        Require(project.Manifest.Id == options.ProjectId && project.Manifest.DisplayName == options.DisplayName,
            "Segmented creation retains application-selected identity.");
        Require(project.Forms.Single().Value.Bindings.Length == 0 && project.Input.Encoder && project.Input.Keys && !project.Input.Pointer,
            "Creation starts blank with independent custom logical inputs.");
        Require(DesignerProjectPathService.Load(manifest, library).ComponentDirectory == Path.Combine(application, "UI", "Generated"),
            "Sidecar resolves application-owned output paths.");
        byte[] before = File.ReadAllBytes(manifest);
        Require(!(await Create(options).ConfigureAwait(false)).Ok && before.SequenceEqual(File.ReadAllBytes(manifest)),
            "Creation never replaces an existing project.");
        Require(File.ReadAllText(sentinel) == "// application-owned FSM" && !File.Exists(Path.Combine(application, "UI/Generated/form_handlers.hpp")),
            "Application code remains unchanged and no FSM or handler is created.");
        var synthetic = options with { ProjectDirectory = Path.Combine(application, "Synthetic"), Input = null, LogicalInput = "outputOnly",
            SegmentGlass = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(library, "tests/designer-fixtures/segmented-icons/glass.segment-display.json"))) };
        Require((await Create(synthetic).ConfigureAwait(false)).Ok, "Unrelated synthetic glass uses the same creation path.");
    }
}
