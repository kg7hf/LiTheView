// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static readonly byte[] OpaqueRed = [255, 0, 0, 255];
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
    private static int _checks;
    private static long _requestId;

    public static async Task<int> Main(string[] arguments)
    {
        if (arguments.Length > 0 && arguments[0] == "--service")
        {
            return await Service.Program.Main(arguments[1..]).ConfigureAwait(false);
        }
        if (arguments.Length is < 1 or > 2)
        {
            Console.Error.WriteLine("Usage: Service.Tests <library root> [native preview worker]");
            return 2;
        }
        string library = Path.GetFullPath(arguments[0]);
        string? worker = arguments.Length == 2 ? Path.GetFullPath(arguments[1]) : null;
        string directory = Path.Combine(library, "build", "designer-service-tests", Guid.NewGuid().ToString("N"));
        DesignerWorkspace workspace = CreateWorkspace(library, directory);
        string original = File.ReadAllText(workspace.FormPath);
        await using DesignerService service = new(library, worker);
        try
        {
            await CheckSegmentedAsync(service, library, Path.Combine(directory, "segmented"), worker is not null).ConfigureAwait(false);
            await CheckProtocolAsync(service, workspace, original).ConfigureAwait(false);
            await CheckNativeDisplayWorkflowAsync(service, workspace, library, Path.Combine(directory, "native-display"),
                worker is not null).ConfigureAwait(false);
            await CheckControlEventsAsync(service, workspace, library).ConfigureAwait(false);
            await CheckEditsAsync(service, workspace, original).ConfigureAwait(false);
            await CheckPropertiesAsync(service, workspace, original).ConfigureAwait(false);
            await CheckTabsAsync(service, workspace, original).ConfigureAwait(false);
            await CheckWheelAsync(service, workspace, original).ConfigureAwait(false);
            await CheckStructureAsync(service, workspace, original).ConfigureAwait(false);
            await CheckThemeAndActionsAsync(service, workspace, original).ConfigureAwait(false);
            await CheckExportAsync(service, workspace, original, library).ConfigureAwait(false);
            await CheckExportLineEndingsAsync(service, library, Path.Combine(directory, "line-endings")).ConfigureAwait(false);
            await CheckProjectCreationAsync(service, library, Path.Combine(directory, "new-application"), worker is not null).ConfigureAwait(false);
            await CheckNativeProjectCreationAsync(service, library, Path.Combine(directory, "native-application"), worker is not null).ConfigureAwait(false);
            await CheckInputConfigurationAsync(service, library, Path.Combine(directory, "input-application")).ConfigureAwait(false);
            await CheckProjectSetupAsync(service, Path.Combine(directory, "setup-application")).ConfigureAwait(false);
            await CheckMultiFormAsync(service, library, Path.Combine(directory, "multi-form")).ConfigureAwait(false);
            await CheckStreamingAsync(service).ConfigureAwait(false);
            CheckPixels();
            if (worker is not null)
            {
                await CheckNativeAsync(service, workspace, original).ConfigureAwait(false);
                foreach (SurfacePixelFormat format in new[] { SurfacePixelFormat.Rgb888, SurfacePixelFormat.Xrgb8888 })
                {
                    DesignerWorkspace native = CreateWorkspace(library, Path.Combine(directory, format.ToString()), format);
                    await CheckNativeAsync(service, native, File.ReadAllText(native.FormPath)).ConfigureAwait(false);
                }
            }
            await CheckUnavailablePreviewAsync(library, workspace, original).ConfigureAwait(false);
            await CheckProcessAsync(library, worker, workspace, original).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(directory, "validation.json"), JsonSerializer.Serialize(new
            {
                checksPassed = _checks,
                nativePreviewTested = worker is not null,
                libraryRoot = library,
                previewWorker = worker
            }, ReportOptions));
            Console.WriteLine($"PASS: {_checks} service checks. Evidence: {directory}");
            return 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static DesignerWorkspace CreateWorkspace(string library, string directory,
        SurfacePixelFormat format = SurfacePixelFormat.Rgb565)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(Path.Combine(library, "schema", "widgets.json"));
        DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(Path.Combine(library, "profiles", "display-surfaces"));
        DisplaySurfaceLoadResult surface = profiles.Surfaces.Single(item =>
            item.Profile?.Id == "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565");
        if (format != SurfacePixelFormat.Rgb565)
        {
            DisplaySurfaceProfile source = profiles.Surfaces.Single(item =>
                item.Profile?.Id == "litheview.surface.stm32h750b-dk-rk043fn48h-xrgb8888").Profile!;
            surface = DisplaySurfaceProfileLoader.Load(DisplaySurfaceCanonicalizer.Canonicalize(source with
            {
                Id = "test.surface." + format.ToString().ToLowerInvariant(),
                PainterSurface = source.PainterSurface with
                {
                    Format = format, StorageBitsPerPixel = format == SurfacePixelFormat.Rgb888 ? 24 : 32
                }
            }));
        }
        DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = Path.Combine(directory, "Control Panel", "Tasks", "UI"),
            ProjectId = "test.vscode-designer", DisplayName = "VS Code service test",
            SchemaReference = "project-manifest.schema.json", Surface = surface,
            Catalog = catalog.Catalog!, IncludeTutorialStarter = true
        }, profiles);
        Require(created.IsValid && created.Workspace is not null, "A simulator-first test project must be created.");
        return created.Workspace!;
    }

    // Version-3 packed projects through the same VS Code service calls as RGB projects:
    // inspect, edit, save, handlers, native render and Generate UI, with no MCU target.
    private static async Task CheckNativeDisplayWorkflowAsync(DesignerService service, DesignerWorkspace template,
        string library, string directory, bool native)
    {
        string fixture = Path.Combine(library, "tests", "designer-fixtures", "native-synthetic-r2-smoke");
        string fixtureForm = Path.Combine(fixture, DesignerWorkspaceService.DefaultFormRelativePath);
        ServiceResponse checkedFixture = await RequestAsync(service, template with
        {
            ManifestPath = Path.Combine(fixture, DesignerWorkspaceService.ManifestFileName),
            FormPath = fixtureForm
        }, File.ReadAllText(fixtureForm), "inspect").ConfigureAwait(false);
        Require(checkedFixture.Ok && Result(checkedFixture).GetProperty("surface").GetProperty("format").GetString() == "R2Packed2x2",
            "The checked native fixture must open through the shared editor: " + checkedFixture.Error?.Message);

        WidgetCatalog catalog = WidgetCatalogLoader.LoadFile(Path.Combine(library, "schema", "widgets.json")).Catalog!;
        LogicalInputConfigurationProfile input = LogicalInputConfigurationLoader.LoadFile(
            Path.Combine(library, "profiles", "logical-inputs", "encoder-select.json")).Configuration!;
        // The presented image is at emitter resolution; its first two pixels are TL and TR emitters.
        foreach ((string name, string format, int width, int height, byte[] topLeft, byte[] topRight) in new[]
                 {
                     ("synthetic-r2", "R2Packed2x2", 512, 384, new byte[] { 255, 0, 0, 255 }, new byte[] { 255, 0, 0, 255 }),
                     ("synthetic-red-cell", "Rrrr2222", 256, 192, new byte[] { 255, 0, 0, 255 }, new byte[] { 255, 0, 0, 255 }),
                     ("synthetic-red-yellow", "Ryry2222", 256, 192, new byte[] { 0, 0, 0, 255 }, new byte[] { 255, 0, 0, 255 })
                 })
        {
            DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNativeDisplayProject(new()
            {
                ProjectDirectory = Path.Combine(directory, name), ProjectId = "test.native-service." + name,
                DisplayName = "Native service " + name,
                Definition = DisplayDefinitionProfileLoader.LoadFile(Path.Combine(library, "profiles", "display-definitions", name + ".json")),
                Input = input, Catalog = catalog
            });
            Require(created.IsValid, $"Native {name} creation failed.");
            DesignerWorkspace workspace = created.Workspace!;
            string xml = File.ReadAllText(workspace.FormPath);
            ServiceResponse inspected = await RequestAsync(service, workspace, xml, "inspect").ConfigureAwait(false);
            JsonElement surface = inspected.Ok ? Result(inspected).GetProperty("surface") : default;
            Require(inspected.Ok && surface.GetProperty("width").GetInt32() == width &&
                surface.GetProperty("height").GetInt32() == height && surface.GetProperty("format").GetString() == format,
                $"Native {name} must open at its logical extent and packed format: " + inspected.Error?.Message);

            ServiceResponse added = await RequestAsync(service, workspace, xml, "edit", new()
            {
                Kind = "add", WidgetId = "lithe.widget.button", ParentId = "component.main.root"
            }).ConfigureAwait(false);
            Require(added.Ok, "The service must add a button to a native form: " + added.Error?.Message);
            xml = Result(added).GetProperty("xml").GetString()!;
            ServiceResponse recolored = await PropertyAsync(service, workspace, xml, "component.main.root",
                "background-color", "#000000ff").ConfigureAwait(false);
            Require(recolored.Ok, "The service must recolor a native panel: " + recolored.Error?.Message);
            xml = Result(recolored).GetProperty("xml").GetString()!;
            File.WriteAllText(workspace.FormPath, xml);

            ServiceResponse handlers = await RequestAsync(service, workspace, xml, "handlers").ConfigureAwait(false);
            JsonElement handler = handlers.Ok ? Result(handlers).GetProperty("handlers")[0] : default;
            Require(handlers.Ok && handler.GetProperty("method").GetString() == "onButton1Clicked" &&
                handler.GetProperty("accessor").GetString() == "getButton1",
                "Native forms must expose the named button handler and accessor contract.");
            if (native)
            {
                ServiceResponse render = await RequestAsync(service, workspace, xml, "render").ConfigureAwait(false);
                JsonElement frame = render.Ok ? Result(render) : default;
                byte[] rgba = render.Ok ? Convert.FromBase64String(frame.GetProperty("rgbaBase64").GetString()!) : [];
                Require(render.Ok && frame.GetProperty("status").GetString() == "Ok" &&
                    frame.GetProperty("width").GetInt32() == 512 && frame.GetProperty("height").GetInt32() == 384 &&
                    rgba.Length == 512 * 384 * 4 && rgba.AsSpan(0, 4).SequenceEqual(topLeft) &&
                    rgba.AsSpan(4, 4).SequenceEqual(topRight),
                    $"Native {name} must render packed storage and present it at emitter resolution: " + render.Error?.Message);
            }
            ServiceResponse generated = await RequestAsync(service, workspace, xml, "export").ConfigureAwait(false);
            ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(workspace.ManifestPath, library);
            Require(generated.Ok && File.Exists(Path.Combine(paths.ComponentDirectory, "form.hpp")),
                $"Native {name} must generate its component through the service: " + generated.Error?.Message);
            using JsonDocument component = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(paths.ComponentDirectory, "component.json")));
            Require(component.RootElement.GetProperty("destinationFormat").GetString() == format &&
                component.RootElement.GetProperty("targetStatus").GetString() == "unevaluated",
                $"The native {name} component must record its packed destination with the target unevaluated.");
        }
    }

    private static async Task CheckProtocolAsync(DesignerService service, DesignerWorkspace workspace, string xml)
    {
        ServiceResponse hello = await service.HandleAsync("{\"protocolVersion\":1,\"id\":1,\"method\":\"hello\",\"params\":{\"protocolVersion\":1}}").ConfigureAwait(false);
        Require(hello.Ok && Result(hello).GetProperty("widgetCatalogSha256").GetString()?.Length == 64,
            "Handshake must identify protocol and compiled catalog.");
        Require(!(await service.HandleAsync("{}").ConfigureAwait(false)).Ok, "Malformed request must be rejected.");
        Require(!(await service.HandleAsync("null").ConfigureAwait(false)).Ok, "JSON null must be rejected.");
        ServiceResponse wrongProtocol = await service.HandleAsync("{\"protocolVersion\":2,\"id\":3,\"method\":\"hello\",\"params\":{\"protocolVersion\":2}}").ConfigureAwait(false);
        Require(wrongProtocol.Id == 3 && wrongProtocol.Error?.Code == "ProtocolMismatch", "Version failure must retain its request id.");
        ServiceResponse inspected = await RequestAsync(service, workspace, xml, "inspect").ConfigureAwait(false);
        Require(inspected.Ok && Result(inspected).GetProperty("components").GetArrayLength() > 2,
            "Inspection must expose real tutorial components.");
        ServiceResponse unknown = await RequestAsync(service, workspace, xml, "launchHardware").ConfigureAwait(false);
        Require(unknown.Error?.Code == "UnknownMethod", "Service must not accept arbitrary execution methods.");
        ServiceResponse unlisted = await RequestAsync(service, workspace with
        {
            FormPath = Path.Combine(workspace.ProjectDirectory, "forms", "unlisted.xml")
        }, xml, "inspect").ConfigureAwait(false);
        Require(unlisted.Error?.Code == "UnlistedForm", "Request form must belong to the supplied manifest.");
        ServiceResponse relative = await RequestAsync(service, workspace with { FormPath = "forms/main.litheview.xml" }, xml, "inspect").ConfigureAwait(false);
        Require(relative.Error?.Code == "InvalidPath", "Relative request paths must be rejected.");
        ServiceResponse invalidXml = await RequestAsync(service, workspace, "<", "inspect").ConfigureAwait(false);
        JsonElement invalid = Result(invalidXml);
        Require(invalidXml.Ok && invalid.GetProperty("components").GetArrayLength() == 0 &&
            invalid.GetProperty("diagnostics").GetArrayLength() > 0, "Invalid source edits must remain diagnosable.");
        ServiceResponse oversized = await RequestAsync(service, workspace, new string('x', Protocol.MaximumXmlBytes + 1), "inspect").ConfigureAwait(false);
        Require(oversized.Error?.Code == "XmlSizeLimit", "XML size must be bounded before parsing.");
        ServiceResponse dtd = await RequestAsync(service, workspace,
            "<!DOCTYPE panel [<!ENTITY test SYSTEM 'file:///unrelated'>]><panel>&test;</panel>", "inspect").ConfigureAwait(false);
        Require(Result(dtd).GetProperty("components").GetArrayLength() == 0, "XML external entities must never be evaluated.");
    }

    private static async Task CheckEditsAsync(DesignerService service, DesignerWorkspace workspace, string original)
    {
        string root = workspace.Form.Components.Single(item => item.ParentComponentId is null).ComponentId;
        ServiceResponse added = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "add", WidgetId = "lithe.widget.button", ParentId = root, Frame = new(37, 43, 109, 39)
        }).ConfigureAwait(false);
        Require(added.Ok, "Button addition must succeed: " + added.Error?.Message);
        JsonElement view = Result(added);
        string id = view.GetProperty("selectedComponentId").GetString()!;
        string xml = view.GetProperty("xml").GetString()!;
        JsonElement placed = view.GetProperty("components").EnumerateArray().Single(item =>
            item.GetProperty("id").GetString() == id).GetProperty("frame");
        Require(placed.GetProperty("x").GetInt32() == 37 && placed.GetProperty("y").GetInt32() == 43 &&
            placed.GetProperty("width").GetInt32() == 109 && placed.GetProperty("height").GetInt32() == 39,
            "Adding a widget must honor its requested initial frame in the same returned document edit.");
        Require(view.GetProperty("documentVersion").GetInt32() == 7 && xml != original &&
            File.ReadAllText(workspace.FormPath) == original, "Edits must return source-version XML without writing the form.");
        ServiceResponse changedText = await RequestAsync(service, workspace, xml, "edit", new()
        {
            Kind = "text", ComponentId = id, Text = "A<&>\"B"
        }).ConfigureAwait(false);
        Require(changedText.Ok, "Text edit must accept XML-sensitive characters.");
        xml = Result(changedText).GetProperty("xml").GetString()!;
        ServiceResponse moved = await RequestAsync(service, workspace, xml, "edit", new()
        {
            Kind = "frame", ComponentId = id, Frame = new(23, 29, 101, 37)
        }).ConfigureAwait(false);
        Require(moved.Ok && Result(moved).GetProperty("components").EnumerateArray().Single(item =>
            item.GetProperty("id").GetString() == id).GetProperty("frame").GetProperty("x").GetInt32() == 23,
            "Geometry edit must change the intended component.");
        xml = Result(moved).GetProperty("xml").GetString()!;
        ServiceResponse deleted = await RequestAsync(service, workspace, xml, "edit", new() { Kind = "delete", ComponentId = id }).ConfigureAwait(false);
        Require(deleted.Ok && Result(deleted).GetProperty("components").GetArrayLength() == workspace.Form.Components.Length,
            "Delete must remove the added component.");
        ServiceResponse fresh = await RequestAsync(service, workspace, original, "inspect").ConfigureAwait(false);
        Require(Result(fresh).GetProperty("components").GetArrayLength() == workspace.Form.Components.Length,
            "A request with original source must not inherit another request's editing history.");
        File.WriteAllText(workspace.FormPath, "<temporarily-invalid");
        ServiceResponse authoritative = await RequestAsync(service, workspace, xml, "inspect").ConfigureAwait(false);
        Require(authoritative.Ok && Result(authoritative).GetProperty("components").GetArrayLength() == workspace.Form.Components.Length + 1,
            "Valid in-memory source must load even when disk source is malformed.");
        File.WriteAllText(workspace.FormPath, original);
        ServiceResponse rootDelete = await RequestAsync(service, workspace, original, "edit", new() { Kind = "delete", ComponentId = root }).ConfigureAwait(false);
        Require(rootDelete.Error?.Code == "InvalidEdit", "Deleting the root must be rejected through shared editing rules.");
        ServiceResponse negativeSize = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "frame", ComponentId = root, Frame = new(0, 0, -1, 10)
        }).ConfigureAwait(false);
        Require(negativeSize.Error?.Code == "InvalidEdit", "Nonpositive geometry must be rejected.");
        ServiceResponse invalidPlacement = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "add", WidgetId = "lithe.widget.button", ParentId = root, Frame = new(0, 0, -1, 10)
        }).ConfigureAwait(false);
        Require(invalidPlacement.Error?.Code == "InvalidEdit" && invalidPlacement.Result is null &&
            File.ReadAllText(workspace.FormPath) == original,
            "An invalid initial frame must reject the complete addition without publishing or saving partial XML.");
    }

    private static async Task CheckPropertiesAsync(DesignerService service, DesignerWorkspace workspace, string original)
    {
        ServiceResponse inspected = await RequestAsync(service, workspace, original, "inspect").ConfigureAwait(false);
        JsonElement view = Result(inspected);
        JsonElement behaviorDescriptor = view.GetProperty("properties").EnumerateArray().Single(item =>
            item.GetProperty("id").GetString() == "button-behavior");
        Require(behaviorDescriptor.GetProperty("kind").GetString() == "enum" &&
            behaviorDescriptor.GetProperty("enumValues").EnumerateArray().Select(item => item.GetString())
                .SequenceEqual(["momentary", "toggle"]) &&
            behaviorDescriptor.GetProperty("editable").GetBoolean(),
            "Inspection must describe schema property kinds and enum values.");
        Require(!view.GetProperty("properties").EnumerateArray().Single(item =>
                item.GetProperty("id").GetString() == "frame").GetProperty("editable").GetBoolean(),
            "Frame must stay on its dedicated geometry operation.");
        Require(view.GetProperty("widgets").EnumerateArray().Single(item =>
                item.GetProperty("id").GetString() == "lithe.widget.button").GetProperty("properties")
            .EnumerateArray().Any(item => item.GetString() == "selected-background-color"),
            "Widget views must list the properties each widget accepts.");

        string root = workspace.Form.Components.Single(item => item.ParentComponentId is null).ComponentId;
        ServiceResponse added = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "add", WidgetId = "lithe.widget.button", ParentId = root, Frame = new(10, 10, 120, 36)
        }).ConfigureAwait(false);
        string id = Result(added).GetProperty("selectedComponentId").GetString()!;
        string xml = Result(added).GetProperty("xml").GetString()!;

        ServiceResponse toggle = await PropertyAsync(service, workspace, xml, id, "button-behavior", "toggle").ConfigureAwait(false);
        Require(toggle.Ok, "An enum property edit must succeed: " + toggle.Error?.Message);
        xml = Result(toggle).GetProperty("xml").GetString()!;
        ServiceResponse latched = await PropertyAsync(service, workspace, xml, id, "checked", true).ConfigureAwait(false);
        Require(latched.Ok, "A Boolean property edit must succeed: " + latched.Error?.Message);
        xml = Result(latched).GetProperty("xml").GetString()!;
        ServiceResponse colored = await PropertyAsync(service, workspace, xml, id, "selected-background-color", "#00fae4d6")
            .ConfigureAwait(false);
        Require(colored.Ok, "A literal color edit must succeed: " + colored.Error?.Message);
        xml = Result(colored).GetProperty("xml").GetString()!;
        Require(xml.Contains("behavior=\"toggle\"", StringComparison.Ordinal) &&
            xml.Contains("checked=\"true\"", StringComparison.Ordinal) &&
            xml.Contains("selectedBackgroundColor=\"0x00fae4d6\"", StringComparison.Ordinal),
            "Property edits must return canonical XML for the edited button.");
        JsonElement values = Component(Result(colored), id).GetProperty("properties");
        Require(Value(values, "button-behavior").GetString() == "toggle" && Value(values, "checked").GetBoolean() &&
            Value(values, "selected-background-color").GetString() == "#00fae4d6",
            "Inspection must report authored property values in their editor shapes.");

        ServiceResponse momentary = await PropertyAsync(service, workspace, xml, id, "button-behavior", "momentary")
            .ConfigureAwait(false);
        Require(momentary.Ok && !Component(Result(momentary), id).GetProperty("properties").EnumerateArray()
                .Any(item => item.GetProperty("id").GetString() == "checked"),
            "Changing to momentary must clear the latched value in the same document edit.");
        ServiceResponse reset = await RequestAsync(service, workspace, Result(momentary).GetProperty("xml").GetString()!, "edit",
            new() { Kind = "reset", ComponentId = id, PropertyId = "button-behavior" }).ConfigureAwait(false);
        Require(reset.Ok && !Component(Result(reset), id).GetProperty("properties").EnumerateArray()
                .Any(item => item.GetProperty("id").GetString() == "button-behavior"),
            "Reset must remove an authored value so the schema default applies.");

        ServiceResponse checkedMomentary = await PropertyAsync(service, workspace, xml, id, "button-behavior", "momentary")
            .ConfigureAwait(false);
        string momentaryXml = Result(checkedMomentary).GetProperty("xml").GetString()!;
        ServiceResponse rejected = await PropertyAsync(service, workspace, momentaryXml, id, "checked", true).ConfigureAwait(false);
        Require(rejected.Error?.Code == "InvalidEdit" && rejected.Error.Message.Contains("LVFD1132", StringComparison.Ordinal),
            "Checking a momentary button must fail through the shared validator.");
        Require((await PropertyAsync(service, workspace, xml, id, "checked", "yes").ConfigureAwait(false)).Error?.Code == "InvalidEdit",
            "A value of the wrong JSON type must be rejected.");
        Require((await PropertyAsync(service, workspace, xml, id, "selected-background-color", "#123").ConfigureAwait(false))
            .Error?.Code == "InvalidEdit", "A malformed color must be rejected.");
        Require((await PropertyAsync(service, workspace, xml, id, "focused-background-color", "theme.missing").ConfigureAwait(false))
            .Error?.Code == "InvalidEdit", "An undeclared theme token must be rejected.");
        Require((await PropertyAsync(service, workspace, xml, id, "items", "A|B").ConfigureAwait(false))
            .Error?.Code == "InvalidEdit", "A property the widget does not accept must be rejected.");
        Require((await PropertyAsync(service, workspace, xml, id, "frame", "0,0,1,1").ConfigureAwait(false))
            .Error?.Code == "InvalidOperation", "Frame must not be edited through the generic property operation.");
        Require((await PropertyAsync(service, workspace, xml, id, "no-such-property", true).ConfigureAwait(false))
            .Error?.Code == "InvalidOperation", "Unknown properties must be rejected.");
        Require(File.ReadAllText(workspace.FormPath) == original, "Property edits must never write the form.");
    }

    private static async Task CheckTabsAsync(DesignerService service, DesignerWorkspace workspace, string original)
    {
        JsonElement widgets = Result(await RequestAsync(service, workspace, original, "inspect").ConfigureAwait(false))
            .GetProperty("widgets");
        JsonElement tabWidget = widgets.EnumerateArray().Single(item =>
            item.GetProperty("id").GetString() == "lithe.widget.tab-view");
        Require(tabWidget.GetProperty("container").GetBoolean() &&
            widgets.EnumerateArray().Single(item => item.GetProperty("id").GetString() == "lithe.widget.panel")
                .GetProperty("allowedParentIds").EnumerateArray().Any(item => item.GetString() == "lithe.widget.tab-view"),
            "Widget views must say which widgets are containers and where each widget may be added.");

        string root = workspace.Form.Components.Single(item => item.ParentComponentId is null).ComponentId;
        ServiceResponse addedTabs = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "add", WidgetId = "lithe.widget.tab-view", ParentId = root, Frame = new(10, 10, 300, 200)
        }).ConfigureAwait(false);
        Require(addedTabs.Ok, "A tab view addition must succeed: " + addedTabs.Error?.Message);
        string tabs = Result(addedTabs).GetProperty("selectedComponentId").GetString()!;
        Require(Value(Component(Result(addedTabs), tabs).GetProperty("properties"), "items").GetString() == "TAB 1|TAB 2" &&
            Result(addedTabs).GetProperty("diagnostics").EnumerateArray().Any(item =>
                item.GetProperty("code").GetString() == "LVFD1134" &&
                item.GetProperty("severity").GetString() == "warning"),
            "A new tab view must start with titles and warn about its missing pages.");
        string xml = Result(addedTabs).GetProperty("xml").GetString()!;
        string[] pages = new string[2];
        for (int index = 0; index < pages.Length; ++index)
        {
            ServiceResponse page = await RequestAsync(service, workspace, xml, "edit", new()
            {
                Kind = "add", WidgetId = "lithe.widget.panel", ParentId = tabs
            }).ConfigureAwait(false);
            Require(page.Ok, "A page addition must succeed: " + page.Error?.Message);
            pages[index] = Result(page).GetProperty("selectedComponentId").GetString()!;
            xml = Result(page).GetProperty("xml").GetString()!;
            JsonElement frame = Component(Result(page), pages[index]).GetProperty("frame");
            Require(frame.GetProperty("x").GetInt32() == 0 && frame.GetProperty("y").GetInt32() == 32 &&
                frame.GetProperty("width").GetInt32() == 300 && frame.GetProperty("height").GetInt32() == 168,
                "A page added without a frame must fill the tab view's content area.");
        }
        JsonElement built = Result(await RequestAsync(service, workspace, xml, "inspect").ConfigureAwait(false));
        Require(!Component(built, pages[0]).GetProperty("previewHidden").GetBoolean() &&
            Component(built, pages[1]).GetProperty("previewHidden").GetBoolean() &&
            !built.GetProperty("diagnostics").EnumerateArray().Any(item =>
                item.GetProperty("code").GetString() == "LVFD1134"),
            "Only the selected page is visible once every tab has a page.");

        ServiceResponse second = await PropertyAsync(service, workspace, xml, tabs, "selected-index", 1).ConfigureAwait(false);
        Require(second.Ok && Component(Result(second), pages[0]).GetProperty("previewHidden").GetBoolean() &&
            !Component(Result(second), pages[1]).GetProperty("previewHidden").GetBoolean(),
            "Selected Index chooses the design-time page shown by the preview.");
        ServiceResponse beyond = await PropertyAsync(service, workspace, xml, tabs, "selected-index", 2).ConfigureAwait(false);
        Require(beyond.Error?.Code == "InvalidEdit" && beyond.Error.Message.Contains("LVFD1133", StringComparison.Ordinal),
            "A selected tab beyond the titles must fail through the shared validator.");
        Require(File.ReadAllText(workspace.FormPath) == original, "Tab edits must never write the form.");
    }

    private static async Task CheckWheelAsync(DesignerService service, DesignerWorkspace workspace, string original)
    {
        string root = workspace.Form.Components.Single(item => item.ParentComponentId is null).ComponentId;
        ServiceResponse added = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "add", WidgetId = "lithe.widget.wheel-picker", ParentId = root, Frame = new(10, 10, 120, 140)
        }).ConfigureAwait(false);
        Require(added.Ok, "A wheel picker addition must succeed: " + added.Error?.Message);
        string wheel = Result(added).GetProperty("selectedComponentId").GetString()!;
        JsonElement properties = Component(Result(added), wheel).GetProperty("properties");
        Require(Value(properties, "items").GetString() == "ITEM 1|ITEM 2|ITEM 3|ITEM 4|ITEM 5" &&
            !properties.EnumerateArray().Any(item => item.GetProperty("id").GetString() == "row-height"),
            "A new wheel picker must start with items and leave Row Height at its default.");
        string xml = Result(added).GetProperty("xml").GetString()!;
        ServiceResponse rows = await PropertyAsync(service, workspace, xml, wheel, "row-height", 32).ConfigureAwait(false);
        Require(rows.Ok && Result(rows).GetProperty("xml").GetString()!.Contains("rowHeight=\"32\"", StringComparison.Ordinal),
            "Row Height must reach the returned XML: " + rows.Error?.Message);
        Require((await PropertyAsync(service, workspace, xml, wheel, "row-height", 11).ConfigureAwait(false))
            .Error?.Code == "InvalidEdit", "A row height below the minimum must be rejected.");
        ServiceResponse beyond = await PropertyAsync(service, workspace, xml, wheel, "selected-index", 5).ConfigureAwait(false);
        Require(beyond.Error?.Code == "InvalidEdit" && beyond.Error.Message.Contains("LVFD1136", StringComparison.Ordinal),
            "A selected item beyond the list must fail through the shared validator.");
        Require(File.ReadAllText(workspace.FormPath) == original, "Wheel edits must never write the form.");
    }

    private static async Task CheckStructureAsync(DesignerService service, DesignerWorkspace workspace, string original)
    {
        const string Status = "component.main.status";
        const string Activate = "component.main.activate";
        const string Reset = "component.main.reset";
        string root = workspace.Form.Components.Single(item => item.ParentComponentId is null).ComponentId;

        ServiceResponse renamed = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "rename", ComponentId = Activate, Name = "startButton" }).ConfigureAwait(false);
        Require(renamed.Ok && Component(Result(renamed), Activate).GetProperty("name").GetString() == "startButton" &&
            Result(renamed).GetProperty("xml").GetString()!.Contains("name=\"startButton\"", StringComparison.Ordinal),
            "Rename must change the component name in the returned XML: " + renamed.Error?.Message);
        ServiceResponse duplicateName = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "rename", ComponentId = Activate, Name = "statusLabel" }).ConfigureAwait(false);
        Require(duplicateName.Error?.Code == "InvalidEdit" &&
            duplicateName.Error.Message.Contains("LVFD1102", StringComparison.Ordinal),
            "A duplicate component name must fail through the shared validator.");

        ServiceResponse duplicated = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "duplicate", ComponentId = Activate }).ConfigureAwait(false);
        string copy = Result(duplicated).GetProperty("selectedComponentId").GetString()!;
        JsonElement copyView = Component(Result(duplicated), copy);
        JsonElement sourceFrame = Component(Result(duplicated), Activate).GetProperty("frame");
        Require(duplicated.Ok && copy != Activate && copyView.GetProperty("name").GetString() == "activateButton2" &&
            copyView.GetProperty("frame").GetProperty("y").GetInt32() == sourceFrame.GetProperty("y").GetInt32() + 8,
            "Duplicate must select an offset copy with a fresh name.");
        ServiceResponse duplicatedMany = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "duplicate", ComponentIds = [Status, Reset] }).ConfigureAwait(false);
        Require(duplicatedMany.Ok && Result(duplicatedMany).GetProperty("components").GetArrayLength() ==
            workspace.Form.Components.Length + 2, "Duplicating several components must copy each one.");

        ServiceResponse front = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "order", ComponentId = Status, Order = "bringToFront" }).ConfigureAwait(false);
        string frontXml = Result(front).GetProperty("xml").GetString()!;
        Require(front.Ok && frontXml.IndexOf("name=\"statusLabel\"", StringComparison.Ordinal) >
            frontXml.IndexOf("name=\"resetButton\"", StringComparison.Ordinal),
            "Bring to front must make the component the last sibling.");
        Require((await RequestAsync(service, workspace, original, "edit",
                new() { Kind = "order", ComponentId = Status, Order = "upward" }).ConfigureAwait(false)).Error?.Code ==
            "InvalidOperation", "An unknown order must be rejected.");

        ServiceResponse panel = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "add", WidgetId = "lithe.widget.panel", ParentId = root, Frame = new(4, 6, 300, 200)
        }).ConfigureAwait(false);
        string panelId = Result(panel).GetProperty("selectedComponentId").GetString()!;
        string panelXml = Result(panel).GetProperty("xml").GetString()!;
        JsonElement before = Component(Result(panel), Status).GetProperty("frame");
        ServiceResponse moved = await RequestAsync(service, workspace, panelXml, "edit",
            new() { Kind = "reparent", ComponentId = Status, ParentId = panelId }).ConfigureAwait(false);
        Require(moved.Ok, "Reparent must succeed: " + moved.Error?.Message);
        JsonElement after = Component(Result(moved), Status);
        Require(moved.Ok && after.GetProperty("parentId").GetString() == panelId &&
            after.GetProperty("frame").GetProperty("x").GetInt32() == before.GetProperty("x").GetInt32() - 4 &&
            after.GetProperty("frame").GetProperty("y").GetInt32() == before.GetProperty("y").GetInt32() - 6,
            "Reparent must keep the component's position on the display: " + moved.Error?.Message);
        Require((await RequestAsync(service, workspace, original, "edit",
                new() { Kind = "reparent", ComponentId = root, ParentId = Status }).ConfigureAwait(false)).Error?.Code ==
            "InvalidEdit", "The root must not be reparented.");

        ServiceResponse aligned = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "frames", Frames = new() { [Status] = new(10, 20, 30, 18), [Reset] = new(10, 60, 30, 22) }
        }).ConfigureAwait(false);
        Require(aligned.Ok &&
            Component(Result(aligned), Status).GetProperty("frame").GetProperty("x").GetInt32() == 10 &&
            Component(Result(aligned), Reset).GetProperty("frame").GetProperty("width").GetInt32() == 30,
            "Several frames must change in one edit.");
        ServiceResponse deletedMany = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "delete", ComponentIds = [Status, Reset] }).ConfigureAwait(false);
        Require(deletedMany.Ok && Result(deletedMany).GetProperty("components").GetArrayLength() ==
            workspace.Form.Components.Length - 2, "Deleting several components must remove each one.");
        Require((await RequestAsync(service, workspace, original, "edit",
                new() { Kind = "delete", ComponentIds = [] }).ConfigureAwait(false)).Error?.Code == "InvalidOperation",
            "A delete without components must be rejected.");
        Require(File.ReadAllText(workspace.FormPath) == original, "Structural edits must never write the form.");
    }

    private static async Task CheckThemeAndActionsAsync(DesignerService service, DesignerWorkspace workspace, string original)
    {
        const string Activate = "component.main.activate";
        ServiceResponse added = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "themeColor", TokenId = "theme.brand", Color = "#00df6c2d" }).ConfigureAwait(false);
        Require(added.Ok && Result(added).GetProperty("choices").EnumerateArray().Any(item =>
                item.GetProperty("id").GetString() == "theme.brand" && item.GetProperty("color").GetString() == "#00df6c2d"),
            "Adding a theme color must list it as a choice: " + added.Error?.Message);
        string xml = Result(added).GetProperty("xml").GetString()!;
        ServiceResponse used = await PropertyAsync(service, workspace, xml, Activate, "background-color", "theme.brand")
            .ConfigureAwait(false);
        xml = Result(used).GetProperty("xml").GetString()!;
        ServiceResponse recolored = await RequestAsync(service, workspace, xml, "edit",
            new() { Kind = "themeColor", TokenId = "theme.brand", Color = "#00ff0000" }).ConfigureAwait(false);
        xml = Result(recolored).GetProperty("xml").GetString()!;
        Require(recolored.Ok && xml.Contains("encodedColor=\"00ff0000\"", StringComparison.Ordinal) &&
            xml.Contains("backgroundColor=\"0x00ff0000\"", StringComparison.Ordinal),
            "Changing a token's color must update every component that uses it.");
        ServiceResponse renamed = await RequestAsync(service, workspace, xml, "edit",
            new() { Kind = "renameThemeColor", TokenId = "theme.brand", NewTokenId = "theme.accent" }).ConfigureAwait(false);
        Require(renamed.Ok && Value(Component(Result(renamed), Activate).GetProperty("properties"), "background-color")
                .GetString() == "theme.accent",
            "Renaming a token must rewrite its references: " + renamed.Error?.Message);
        xml = Result(renamed).GetProperty("xml").GetString()!;
        ServiceResponse inUse = await RequestAsync(service, workspace, xml, "edit",
            new() { Kind = "deleteThemeColor", TokenId = "theme.accent" }).ConfigureAwait(false);
        Require(inUse.Error?.Code == "InvalidEdit" && inUse.Error.Message.Contains("LVED1802", StringComparison.Ordinal),
            "A token in use must not be deleted.");
        ServiceResponse badName = await RequestAsync(service, workspace, xml, "edit",
            new() { Kind = "renameThemeColor", TokenId = "theme.accent", NewTokenId = "accent" }).ConfigureAwait(false);
        Require(badName.Error?.Code == "InvalidEdit" && badName.Error.Message.Contains("LVFD1204", StringComparison.Ordinal),
            "A token ID outside theme.* must fail through the shared validator.");
        ServiceResponse unused = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "themeColor", TokenId = "theme.spare", Color = "00112233" }).ConfigureAwait(false);
        ServiceResponse removed = await RequestAsync(service, workspace, Result(unused).GetProperty("xml").GetString()!, "edit",
            new() { Kind = "deleteThemeColor", TokenId = "theme.spare" }).ConfigureAwait(false);
        Require(removed.Ok && !Result(removed).GetProperty("choices").EnumerateArray().Any(item =>
                item.GetProperty("id").GetString() == "theme.spare"), "An unused token must be deletable.");

        JsonElement inspected = Result(await RequestAsync(service, workspace, original, "inspect").ConfigureAwait(false));
        JsonElement buttonActions = inspected.GetProperty("widgets").EnumerateArray().Single(item =>
            item.GetProperty("id").GetString() == "lithe.widget.button").GetProperty("actions");
        JsonElement assigned = Component(inspected, Activate).GetProperty("actions").EnumerateArray().Single();
        Require(buttonActions.EnumerateArray().Single().GetProperty("source").GetString() == "runtime-id" &&
            assigned.GetProperty("widgetActionId").GetString() == "activate" &&
            assigned.GetProperty("actionId").GetString() == "action.activate" &&
            inspected.GetProperty("choices").EnumerateArray().Any(item =>
                item.GetProperty("id").GetString() == "action.reset" && item.GetProperty("runtimeId").GetInt32() == 2),
            "Inspection must describe widget actions, current assignments, and action runtime IDs.");

        ServiceResponse taken = await RequestAsync(service, workspace, original, "edit",
            new() { Kind = "action", ComponentId = Activate, WidgetActionId = "activate", ActionId = "action.reset" })
            .ConfigureAwait(false);
        Require(taken.Error?.Code == "InvalidEdit" && taken.Error.Message.Contains("LVFD1121", StringComparison.Ordinal),
            "A button action must not reuse another component's runtime ID.");
        ServiceResponse created = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "action", ComponentId = Activate, WidgetActionId = "activate",
            NewAction = new() { Id = "action.start", RuntimeId = 5, Symbol = "Start" }
        }).ConfigureAwait(false);
        Require(created.Ok && Component(Result(created), Activate).GetProperty("actions").EnumerateArray().Single()
                .GetProperty("actionId").GetString() == "action.start" &&
            Value(Component(Result(created), Activate).GetProperty("properties"), "runtime-id").GetInt32() == 5,
            "Creating and assigning an action must add it and set the runtime ID: " + created.Error?.Message);
        ServiceResponse cleared = await RequestAsync(service, workspace, Result(created).GetProperty("xml").GetString()!, "edit",
            new() { Kind = "action", ComponentId = Activate, WidgetActionId = "activate", ActionId = null })
            .ConfigureAwait(false);
        // Null fields are omitted from responses, so an unassigned action has no actionId.
        Require(cleared.Ok && !Component(Result(cleared), Activate).GetProperty("actions").EnumerateArray().Single()
                .TryGetProperty("actionId", out _),
            "Clearing an assignment must leave the widget action unassigned.");
        ServiceResponse duplicateSymbol = await RequestAsync(service, workspace, original, "edit", new()
        {
            Kind = "action", ComponentId = Activate, WidgetActionId = "activate",
            NewAction = new() { Id = "action.again", RuntimeId = 6, Symbol = "Reset" }
        }).ConfigureAwait(false);
        Require(duplicateSymbol.Error?.Code == "InvalidEdit" &&
            duplicateSymbol.Error.Message.Contains("LVFD1201", StringComparison.Ordinal),
            "A duplicate action symbol must fail through the shared validator.");
        Require(File.ReadAllText(workspace.FormPath) == original, "Theme and action edits must never write the form.");
    }

    private static ValueTask<ServiceResponse> PropertyAsync<T>(DesignerService service, DesignerWorkspace workspace,
        string xml, string componentId, string propertyId, T value) =>
        RequestAsync(service, workspace, xml, "edit", new()
        {
            Kind = "property", ComponentId = componentId, PropertyId = propertyId,
            Value = JsonSerializer.SerializeToElement(value)
        });

    private static JsonElement Component(JsonElement view, string id) =>
        view.GetProperty("components").EnumerateArray().Single(item => item.GetProperty("id").GetString() == id);

    private static JsonElement Value(JsonElement properties, string id) =>
        properties.EnumerateArray().Single(item => item.GetProperty("id").GetString() == id).GetProperty("value");

    private static async Task CheckExportAsync(DesignerService service, DesignerWorkspace workspace, string xml, string library)
    {
        DesignerProjectPathService.Save(workspace.ManifestPath, new()
        {
            ApplicationRoot = "../..", LibraryRoot = library, ComponentDirectory = "Tasks/UI/Generated UI",
            BuildDirectory = "Build Output/UI"
        });
        string output = Path.Combine(workspace.ProjectDirectory, "Generated UI");
        ServiceResponse handlers = await RequestAsync(service, workspace, xml, "handlers").ConfigureAwait(false);
        Require(handlers.Ok && Result(handlers).GetProperty("include").GetString() == "Generated UI/product_config.hpp" &&
            Result(handlers).GetProperty("handlerPath").GetString() == Path.Combine(workspace.ProjectDirectory, "form_handlers.hpp") &&
            Result(handlers).GetProperty("handlers").GetArrayLength() > 0 &&
            !File.Exists(Path.Combine(workspace.ProjectDirectory, "form_handlers.hpp")),
            "Handler plans follow application paths and never write handwritten source.");
        ServiceResponse dirty = await RequestAsync(service, workspace, xml + "\n", "export").ConfigureAwait(false);
        Require(dirty.Error?.Code == "SaveRequired" && !Directory.Exists(output), "Unsaved export must not create outputs.");
        ServiceResponse exported = await RequestAsync(service, workspace, xml, "export").ConfigureAwait(false);
        Require(exported.Ok && Result(exported).GetProperty("outputDirectory").GetString() == output,
            "Export must honor application-owned output paths: " + exported.Error?.Message);
        string application = Path.Combine(output, "application.hpp");
        Require(!File.Exists(application) && !File.Exists(Path.Combine(output, "win32.cpp")) &&
            !Directory.Exists(Path.Combine(output, ".vscode")) && File.Exists(Path.Combine(output, "component.json")),
            "Generate UI must emit only a component, without an application, simulator or editor scaffold.");
        File.AppendAllText(application, "\n// Keep developer code.\n");
        string owned = File.ReadAllText(application);
        ServiceResponse repeated = await RequestAsync(service, workspace, xml, "export").ConfigureAwait(false);
        Require(repeated.Ok && File.ReadAllText(application) == owned && File.ReadAllText(workspace.FormPath) == xml,
            "Regeneration must preserve handwritten application code and source form.");
        string generated = Path.Combine(output, "form.hpp");
        File.AppendAllText(generated, "\n// Conflict.\n");
        string conflict = File.ReadAllText(generated);
        ServiceResponse refused = await RequestAsync(service, workspace, xml, "export").ConfigureAwait(false);
        Require(refused.Error?.Code == "ExportFailed" && File.ReadAllText(generated) == conflict,
            "Regeneration must refuse conflicts in previously generated files.");
    }

    private static async Task CheckExportLineEndingsAsync(DesignerService service, string library, string directory)
    {
        DesignerWorkspace workspace = CreateWorkspace(library, directory);
        DesignerProjectPathService.Save(workspace.ManifestPath, new()
        {
            ApplicationRoot = "../..", LibraryRoot = library, ComponentDirectory = "Tasks/UI/Generated UI",
            BuildDirectory = "Build Output/UI"
        });
        string original = File.ReadAllText(workspace.FormPath);
        string lf = original.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        string mixed = lf.Replace("</panel>\n", "</panel>\r\n", StringComparison.Ordinal);
        Require(mixed != lf && mixed != crlf, "Fixture must have mixed line endings.");
        foreach ((string disk, string editor) in new[]
        {
            (mixed, lf), (crlf, lf), (lf, crlf), (lf.Replace('\n', '\r'), lf)
        })
        {
            File.WriteAllText(workspace.FormPath, disk);
            ServiceResponse exported = await RequestAsync(service, workspace, editor, "export").ConfigureAwait(false);
            Require(exported.Ok, "Saved editor text with normalized line endings must export: " + exported.Error?.Message);
            Require(File.ReadAllText(workspace.FormPath) == disk, "Export must not rewrite source line endings.");
        }
        File.WriteAllText(workspace.FormPath, mixed);
        foreach (string changed in new[] { lf + "\n", lf.Replace("<panel ", "<panel  ", StringComparison.Ordinal),
                                          lf.Replace("ACTIVATE", "UNSAVED", StringComparison.Ordinal) })
        {
            Require(changed != lf, "Unsaved fixture must change the editor text.");
            ServiceResponse refused = await RequestAsync(service, workspace, changed, "export").ConfigureAwait(false);
            Require(refused.Error?.Code == "SaveRequired", "Real unsaved content changes must still be rejected.");
        }
    }

    private static async Task CheckStreamingAsync(DesignerService service)
    {
        const string hello = "{\"protocolVersion\":1,\"id\":19,\"method\":\"hello\",\"params\":{\"protocolVersion\":1}}";
        using StringReader input = new(new string('x', Protocol.MaximumLineCharacters + 1) + "\n" + hello + "\r\n{}\n" + hello);
        using StringWriter output = new(CultureInfo.InvariantCulture);
        await Service.Program.RunAsync(input, output, service).ConfigureAwait(false);
        string[] lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Require(lines.Length == 4, "One response is required per line, including the final EOF-terminated line.");
        using JsonDocument oversized = JsonDocument.Parse(lines[0]);
        Require(oversized.RootElement.GetProperty("error").GetProperty("code").GetString() == "RequestTooLarge",
            "Oversized input must be drained without becoming unbounded.");
        using JsonDocument recovered = JsonDocument.Parse(lines[3]);
        Require(recovered.RootElement.GetProperty("ok").GetBoolean(), "Protocol must recover after oversized and malformed lines.");
    }

    private static void CheckPixels()
    {
        CheckPixel(PreviewPixelFormat.Rgb565, [0, 248], OpaqueRed);
        CheckPixel(PreviewPixelFormat.Rgb888, [17, 33, 65], [17, 33, 65, 255]);
        CheckPixel(PreviewPixelFormat.Xrgb8888, [65, 33, 17, 0], [17, 33, 65, 255]);
        CheckPixel(PreviewPixelFormat.Argb8888, [65, 33, 17, 99], [17, 33, 65, 99]);
        PreviewSurface surface = new(PreviewPixelFormat.Rgb888, PreviewShape.Rectangle, PreviewQuantization.Native, 1, 2, 4);
        byte[] rgba = PreviewPixels.ToRgba(new(1, surface, [1, 2, 3, 99, 4, 5, 6, 88], [], PreviewStatus.Ok, 0, "", false, 0, 0, TimeSpan.Zero)).Rgba;
        ReadOnlySpan<byte> expected = [1, 2, 3, 255, 4, 5, 6, 255];
        Require(rgba.AsSpan().SequenceEqual(expected), "Presentation conversion must honor native row stride.");
        // Packed storage is expanded only here, one pixel per emitter: TL 3:2, TR 7:6, BL 1:0, BR 5:4.
        CheckPacked(PreviewPixelFormat.R2Packed2x2, 2, 0x4e,
            [255, 0, 0, 255, 85, 0, 0, 255, 170, 0, 0, 255, 0, 0, 0, 255]);
        CheckPacked(PreviewPixelFormat.Rrrr2222, 1, 0x8d,
            [255, 0, 0, 255, 170, 0, 0, 255, 85, 0, 0, 255, 0, 0, 0, 255]);
        CheckPacked(PreviewPixelFormat.Ryry2222, 1, 0x8d,
            [255, 255, 0, 255, 170, 0, 0, 255, 85, 0, 0, 255, 0, 0, 0, 255]);
    }

    private static void CheckPacked(PreviewPixelFormat format, int extent, byte encoded, byte[] expected)
    {
        PreviewSurface surface = new(format, PreviewShape.Rectangle, PreviewQuantization.Native, extent, extent, 1);
        PreviewImage image = PreviewPixels.ToRgba(new(1, surface, [encoded], [], PreviewStatus.Ok, 0, "", false, 0, 0, TimeSpan.Zero));
        Require(image.Width == 2 && image.Height == 2 && image.Rgba.AsSpan().SequenceEqual(expected),
            "Packed presentation must expand each stored emitter level for " + format);
    }

    private static void CheckPixel(PreviewPixelFormat format, byte[] source, byte[] expected)
    {
        PreviewSurface surface = new(format, PreviewShape.Rectangle, PreviewQuantization.Native, 1, 1, source.Length);
        byte[] rgba = PreviewPixels.ToRgba(new(1, surface, source, [], PreviewStatus.Ok, 0, "", false, 0, 0, TimeSpan.Zero)).Rgba;
        Require(rgba.AsSpan().SequenceEqual(expected), "Native channel presentation must be correct for " + format);
    }

    private static async Task CheckNativeAsync(DesignerService service, DesignerWorkspace workspace, string xml)
    {
        ServiceResponse rendered = await RequestAsync(service, workspace, xml, "render").ConfigureAwait(false);
        Require(rendered.Ok, "Native render must succeed: " + rendered.Error?.Message);
        JsonElement result = Result(rendered);
        Require(result.GetProperty("status").GetString() == "Ok", "Native C++ renderer must accept the tutorial snapshot.");
        byte[] pixels = Convert.FromBase64String(result.GetProperty("rgbaBase64").GetString()!);
        Require(pixels.Length == 480 * 272 * 4 && pixels.Where((_, index) => index % 4 != 3).Any(value => value != 0),
            "Preview must return a complete nonempty RGBA frame at the selected native size.");
    }

    private static async Task CheckUnavailablePreviewAsync(string library, DesignerWorkspace workspace, string xml)
    {
        await using DesignerService noWorker = new(library, null);
        ServiceResponse unavailable = await RequestAsync(noWorker, workspace, xml, "render").ConfigureAwait(false);
        Require(unavailable.Error?.Code == "PreviewUnavailable", "Missing preview configuration must have an actionable error.");
        Require((await RequestAsync(noWorker, workspace, xml, "inspect").ConfigureAwait(false)).Ok,
            "Missing preview must not disable document inspection.");
        await using DesignerService missingWorker = new(library, Path.Combine(workspace.ProjectDirectory, "missing-worker.exe"));
        Require(!(await RequestAsync(missingWorker, workspace, xml, "render").ConfigureAwait(false)).Ok &&
            (await RequestAsync(missingWorker, workspace, xml, "inspect").ConfigureAwait(false)).Ok,
            "Native process startup failure must remain isolated from document editing.");
    }

    private static async Task CheckProcessAsync(string library, string? worker, DesignerWorkspace workspace, string xml)
    {
        ProcessStartInfo start = new(Environment.ProcessPath!)
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        }
        start.ArgumentList.Add("--service");
        start.ArgumentList.Add("--library-root");
        start.ArgumentList.Add(library);
        if (worker is not null)
        {
            start.ArgumentList.Add("--preview-worker");
            start.ArgumentList.Add(worker);
        }
        using Process process = Process.Start(start)!;
        try
        {
            await process.StandardInput.WriteLineAsync("{\"protocolVersion\":1,\"id\":1,\"method\":\"hello\",\"params\":{\"protocolVersion\":1}}").ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            string? hello = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            Require(hello is not null && JsonDocument.Parse(hello).RootElement.GetProperty("ok").GetBoolean(),
                "A live process must respond before stdin EOF, with only JSON on stdout.");
            if (worker is not null)
            {
                await process.StandardInput.WriteLineAsync(Serialize(workspace, xml, "render", null)).ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
                string? frame = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
                Require(frame is not null && JsonDocument.Parse(frame).RootElement.GetProperty("ok").GetBoolean(),
                    "Long-lived process must render after the handshake.");
            }
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            string errors = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            Require(process.ExitCode == 0 && errors.Length == 0, "EOF must shut down the service cleanly: " + errors);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static ValueTask<ServiceResponse> RequestAsync(DesignerService service, DesignerWorkspace workspace,
        string xml, string method, EditOperation? operation = null) => service.HandleAsync(Serialize(workspace, xml, method, operation));

    private static string Serialize(DesignerWorkspace workspace, string xml, string method, EditOperation? operation) =>
        JsonSerializer.Serialize(new ServiceRequest
        {
            Id = ++_requestId, ProtocolVersion = 1, Method = method,
            Params = new() { ManifestPath = workspace.ManifestPath, FormPath = workspace.FormPath,
                Xml = xml, DocumentVersion = 7, Operation = operation }
        }, Protocol.JsonOptions);

    private static JsonElement Result(ServiceResponse response) => JsonSerializer.SerializeToElement(response.Result, Protocol.JsonOptions);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
        ++_checks;
    }
}
