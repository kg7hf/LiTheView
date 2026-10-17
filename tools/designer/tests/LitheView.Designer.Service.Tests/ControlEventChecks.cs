// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static async Task CheckControlEventsAsync(DesignerService service, DesignerWorkspace workspace, string library)
    {
        string xml = File.ReadAllText(Path.Combine(library, "tests", "designer-fixtures", "control-events", "form.xml"));
        ServiceResponse inspect = await RequestAsync(service, workspace, xml, "inspect").ConfigureAwait(false);
        Require(inspect.Ok, "Event fixture can be inspected.");
        ServiceResponse described = await RequestAsync(service, workspace, xml, "handlers").ConfigureAwait(false);
        Require(described.Ok, "Typed handlers can be described: " + described.Error?.Message);
        JsonElement[] handlers = [.. Result(described).GetProperty("handlers").EnumerateArray()];
        Require(handlers.Length == 9, "Fixture exposes exactly nine supported component/event pairs.");
        foreach (JsonElement component in Result(inspect).GetProperty("components").EnumerateArray())
        {
            JsonElement widget = Result(inspect).GetProperty("widgets").EnumerateArray()
                .Single(item => item.GetProperty("id").GetString() == component.GetProperty("widgetId").GetString());
            foreach (JsonElement descriptor in widget.GetProperty("events").EnumerateArray())
            {
                JsonElement handler = handlers.Single(item =>
                    item.GetProperty("componentId").GetString() == component.GetProperty("id").GetString() &&
                    item.GetProperty("eventId").GetString() == descriptor.GetProperty("id").GetString());
                Require(handler.GetProperty("parameterType").GetString() == descriptor.GetProperty("parameterType").GetString(),
                    "Inspector and generator share the typed event signature.");
            }
        }
        foreach (JsonElement widget in Result(inspect).GetProperty("widgets").EnumerateArray())
        {
            Require(!string.IsNullOrWhiteSpace(widget.GetProperty("eventPolicy").GetString()),
                "Every widget explicitly documents its event policy.");
        }
        string textXml = File.ReadAllText(Path.Combine(library, "tests", "designer-fixtures", "text-events", "form.xml"));
        ServiceResponse textInspected = await RequestAsync(service, workspace, textXml, "inspect").ConfigureAwait(false);
        ServiceResponse textDescribed = await RequestAsync(service, workspace, textXml, "handlers").ConfigureAwait(false);
        Require(textInspected.Ok && textDescribed.Ok, "Text form inspection and handler discovery succeed.");
        JsonElement[] textHandlers = [.. Result(textDescribed).GetProperty("handlers").EnumerateArray()];
        Require(textHandlers.Length == 3, "Text field exposes change, commit and cancel separately.");
        foreach (var expected in new[] { ("textChanged", "TextChange"), ("editCommitted", "TextEdit"), ("editCancelled", "TextEdit") })
        {
            JsonElement handler = textHandlers.Single(item => item.GetProperty("eventId").GetString() == expected.Item1);
            Require(handler.GetProperty("componentId").GetString() == "setpoint" &&
                handler.GetProperty("parameterType").GetString() == "lithe::" + expected.Item2,
                "Text handler uses typed source identity and payload.");
        }
        Require(!File.Exists(Result(described).GetProperty("handlerPath").GetString()),
            "Handler discovery leaves application-owned source alone.");
        string dropDownXml = File.ReadAllText(Path.Combine(library, "tests", "designer-fixtures", "drop-down-events", "form.xml"));
        ServiceResponse dropDownInspected = await RequestAsync(service, workspace, dropDownXml, "inspect").ConfigureAwait(false);
        ServiceResponse dropDownDescribed = await RequestAsync(service, workspace, dropDownXml, "handlers").ConfigureAwait(false);
        Require(dropDownInspected.Ok && dropDownDescribed.Ok, "Drop-down inspection and handler discovery succeed.");
        JsonElement[] dropDownHandlers = [.. Result(dropDownDescribed).GetProperty("handlers").EnumerateArray()];
        Require(dropDownHandlers.Length == 3, "Drop-down exposes change, commit and cancel separately.");
        foreach (var expected in new[] { ("selectionChanged", "SelectionChange"), ("editCommitted", "SelectionEdit"), ("editCancelled", "SelectionEdit") })
        {
            JsonElement handler = dropDownHandlers.Single(item => item.GetProperty("eventId").GetString() == expected.Item1);
            Require(handler.GetProperty("componentId").GetString() == "mode" &&
                handler.GetProperty("parameterType").GetString() == "lithe::" + expected.Item2,
                "Drop-down handler keeps typed identity and payload.");
        }
        ServiceResponse added = await RequestAsync(service, workspace, dropDownXml, "edit", new()
        {
            Kind = "add", WidgetId = "lithe.widget.drop-down", ParentId = "root", Frame = new(20, 100, 160, 28)
        }).ConfigureAwait(false);
        Require(added.Ok, "Drop-down palette insertion supplies a valid item model.");
        string id = Result(added).GetProperty("selectedComponentId").GetString()!;
        string updatedXml = Result(added).GetProperty("xml").GetString()!;
        Require((await PropertyAsync(service, workspace, updatedXml, id, "selected-index", 5).ConfigureAwait(false))
            .Error?.Code == "InvalidEdit", "Drop-down inspector rejects an index outside its items.");
    }
}
