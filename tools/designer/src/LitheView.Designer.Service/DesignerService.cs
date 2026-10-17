// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;
using LitheView.Designer.Schema.Generated;

namespace LitheView.Designer.Service;

internal sealed partial class DesignerService : IAsyncDisposable
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly string[] Capabilities = ["inspect", "edit", "render", "export", "handlers", "controlEvents", "project.options", "project.create", "project.createNative", "project.input", "project.setup", "project.addScreen"];
    private readonly string _libraryRoot;
    private readonly WidgetCatalog _catalog;
    private readonly string _catalogHash;
    private readonly DisplaySurfaceCatalogLoadResult _profiles;
    private readonly PreviewWorkerClient? _preview;

    public DesignerService(string libraryRoot, string? previewWorker)
    {
        _libraryRoot = AbsolutePath(libraryRoot, "Library root");
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(Path.Combine(_libraryRoot, "schema", "widgets.json"));
        if (!catalog.IsValid || catalog.Catalog is null || catalog.Sha256 is null)
        {
            throw new ServiceException("InvalidCatalog", Messages(catalog.Diagnostics));
        }
        if (catalog.Sha256 != BuiltInWidgetCatalog.CatalogSha256)
        {
            throw new ServiceException("CatalogMismatch", "Rebuild the designer service for this library's widget catalog.");
        }
        _catalog = catalog.Catalog;
        _catalogHash = catalog.Sha256;
        _profiles = ProfileCatalogLoader.LoadSurfaces(Path.Combine(_libraryRoot, "profiles", "display-surfaces"));
        if (!_profiles.IsValid)
        {
            throw new ServiceException("InvalidProfiles", Messages(_profiles.Diagnostics));
        }
        _preview = previewWorker is null ? null : new(AbsolutePath(previewWorker, "Preview worker"));
    }

    public async ValueTask<ServiceResponse> HandleAsync(string line)
    {
        long id = 0;
        try
        {
            if (line.Length > Protocol.MaximumLineCharacters)
            {
                throw new ServiceException("RequestTooLarge", "The request line exceeds the 8 MiB character limit.");
            }
            ServiceRequest request = JsonSerializer.Deserialize<ServiceRequest>(line, Protocol.JsonOptions)
                ?? throw new ServiceException("InvalidRequest", "The request must be a JSON object.");
            id = request.Id;
            if (request.ProtocolVersion != Protocol.Version)
            {
                throw new ServiceException("ProtocolMismatch", "This service requires protocolVersion 1.");
            }
            if (request.Id < 0 || request.Id > 9_007_199_254_740_991L || request.Params is null)
            {
                throw new ServiceException("InvalidRequest", "A nonnegative safe integer id and params object are required.");
            }
            if (request.Method == "hello")
            {
                if (request.Params.ProtocolVersion != Protocol.Version)
                {
                    throw new ServiceException("ProtocolMismatch", "The hello params require protocolVersion 1.");
                }
                return new(id, true, new
                {
                    protocolVersion = Protocol.Version,
                    serviceVersion = "0.2.14",
                    widgetCatalogSha256 = _catalogHash,
                    capabilities = Capabilities
                });
            }
            if (request.Method == "project.options")
            {
                return new(id, true, ProjectOptions());
            }
            if (request.Method == "project.create")
            {
                return new(id, true, CreateProject(request.Params.Project
                    ?? throw new ServiceException("InvalidRequest", "Project creation parameters are required.")));
            }
            if (request.Method is not ("inspect" or "edit" or "render" or "export" or "handlers" or "project.input" or "project.setup" or "project.addScreen"))
            {
                throw new ServiceException("UnknownMethod", "Unknown designer method: " + request.Method);
            }
            Context context = LoadContext(request.Params);
            object result = request.Method switch
            {
                "inspect" => Inspect(context, request.Params.DocumentVersion!.Value),
                "edit" => Edit(context, request.Params),
                "render" => await RenderAsync(context, request.Params.DocumentVersion!.Value).ConfigureAwait(false),
                "export" => Export(context, request.Params),
                "handlers" => DescribeHandlers(context, request.Params.DocumentVersion!.Value),
                "project.input" => ConfigureInput(context, request.Params),
                "project.setup" => ConfigureProject(context, request.Params),
                "project.addScreen" => AddScreen(context, request.Params),
                _ => throw new ServiceException("UnknownMethod", "Unknown designer method.")
            };
            return new(id, true, result);
        }
        catch (ServiceException exception)
        {
            return new(id, false, Error: new(exception.Code, exception.Message));
        }
        catch (JsonException exception)
        {
            return new(id, false, Error: new("InvalidRequest", exception.Message));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException or NotSupportedException or OverflowException or
            System.ComponentModel.Win32Exception)
        {
            return new(id, false, Error: new("OperationFailed", exception.Message));
        }
    }

    private Context LoadContext(RequestParameters parameters)
    {
        if (parameters.DocumentVersion is null or < 0 || parameters.Xml is null)
        {
            throw new ServiceException("InvalidRequest", "documentVersion and the current XML text are required.");
        }
        if (Encoding.UTF8.GetByteCount(parameters.Xml) > Protocol.MaximumXmlBytes)
        {
            throw new ServiceException("XmlSizeLimit", "The form XML exceeds the 1 MiB UTF-8 limit.");
        }
        string manifestPath = AbsolutePath(parameters.ManifestPath, "Manifest path");
        string formPath = AbsolutePath(parameters.FormPath, "Form path");
        string directory = Path.GetDirectoryName(manifestPath)!;
        DesignerProjectLoadResult loaded = DesignerProjectLoader.LoadFile(manifestPath);
        if (!loaded.IsValid || loaded.Manifest is null)
        {
            throw new ServiceException("InvalidProject", Messages(loaded.Diagnostics));
        }
        // Manifest validation rejects rooted/escaping relative paths. Membership
        // is checked before looking at either disk form contents or supplied XML.
        if (!loaded.Manifest.Forms.Any(relative => string.Equals(
            Path.GetFullPath(Path.Combine(directory, relative)), formPath, PathComparison)))
        {
            throw new ServiceException("UnlistedForm", "The requested form is not listed in this project manifest.");
        }
        List<DisplaySurfaceLoadResult> surfaces = [.. _profiles.Surfaces];
        string snapshotPath = Path.Combine(directory, "profiles", "display-surface.json");
        if (File.Exists(snapshotPath))
        {
            DisplaySurfaceLoadResult local = DisplaySurfaceProfileLoader.LoadFile(snapshotPath);
            if (!local.IsValid || local.Profile is null)
            {
                throw new ServiceException("InvalidSurface", Messages(local.Diagnostics));
            }
            surfaces.RemoveAll(item => item.Profile?.Id == local.Profile.Id);
            surfaces.Add(local);
        }
        DesignerProjectResolutionResult resolved = DesignerProjectResolver.Resolve(loaded, surfaces, directory);
        if (!resolved.IsValid || resolved.Project is null)
        {
            throw new ServiceException("InvalidProject", Messages(resolved.Diagnostics));
        }
        FormDocumentLoadResult form = FormXmlImporter.Load(Encoding.UTF8.GetBytes(parameters.Xml),
            _catalog, FormImportMode.Strict);
        DesignerWorkspace? workspace = form.Document is null ? null : new()
        {
            ProjectDirectory = directory,
            ManifestPath = manifestPath,
            FormPath = formPath,
            Manifest = loaded.Manifest,
            ResolvedProject = resolved.Project,
            Form = form.Document,
            Catalog = _catalog
        };
        List<ProfileDiagnostic> diagnostics = [.. resolved.Diagnostics, .. form.Diagnostics];
        if (workspace is not null)
        {
            // Surface compatibility is useful while editing, independently of
            // any eventual target's workspace, flash or transfer budget.
            diagnostics.AddRange(DesignerProductResolver.Resolve(workspace).Diagnostics);
        }
        return new(workspace, resolved.Project.Display, manifestPath, formPath,
            [.. diagnostics.Distinct()]);
    }

    private InspectResult Inspect(Context context, int version)
    {
        FormDocument? form = context.Workspace?.Form;
        HashSet<string> hiddenPages = form is null ? [] : FormTabRules.HiddenPageComponentIds(
            form.Components,
            widgetId => _catalog.Widgets.Any(widget => widget.Id == widgetId && FormTabRules.IsTabView(widget)));
        ComponentView[] components = form is null ? [] :
            [.. form.Components.Select(component => new ComponentView(
                component.ComponentId, component.Name, component.WidgetId, component.ParentComponentId,
                component.Properties.OfType<FormRectangleValue>().Single(item => item.PropertyId == "frame").Value,
                component.Properties.OfType<FormTextValue>().SingleOrDefault(item => item.PropertyId == "text")?.Value,
                [.. component.Properties
                    .Where(item => item is not FormRectangleValue)
                    .Select(item => new PropertyValueView(item.PropertyId, PropertyValues.ToView(item)))],
                hiddenPages.Contains(component.ComponentId),
                [.. WidgetActions(component.WidgetId).Select(action =>
                    new ActionAssignmentView(action.Id, PropertyValues.AssignedAction(form, component, action)))]))];
        return new(version, components,
            [.. _catalog.Widgets.Select(widget => new WidgetView(
                widget.Id, widget.DisplayName, widget.PaletteGroup, widget.Help, widget.PropertyIds,
                widget.ParentRule.AllowedParentIds, widget.ChildPolicy == WidgetChildPolicy.Many,
                [.. widget.Actions.Select(action =>
                    new WidgetActionView(action.Id, action.DisplayName, action.RuntimeActionSource))],
                [.. ControlEventCatalog.For(widget.RuntimeKind)], ControlEventCatalog.Policy(widget.RuntimeKind)))],
            [.. _catalog.Properties.Select(PropertyValues.Describe)],
            form is null ? [] : PropertyValues.Choices(form),
            new(context.Display.Width, context.Display.Height,
                context.Display.Format.ToString(), context.Display.Shape.ToString()),
            Diagnostics(context.Diagnostics));
    }

    private InspectResult Edit(Context context, RequestParameters parameters)
    {
        DesignerWorkspace workspace = RequireForm(context);
        EditOperation operation = parameters.Operation
            ?? throw new ServiceException("InvalidOperation", "An edit operation is required.");
        // One fresh session per request: VS Code owns document history and saves.
        DesignerDocumentSession session = new(workspace.Form, _catalog);
        HashSet<string> before = [.. workspace.Form.Components.Select(item => item.ComponentId)];
        FormEditResult result = operation.Kind switch
        {
            "add" => session.AddWidget(Required(operation.WidgetId, "widgetId"), Required(operation.ParentId, "parentId")),
            "frame" => session.SetFrame(Required(operation.ComponentId, "componentId"),
                operation.Frame ?? throw new ServiceException("InvalidOperation", "frame is required.")),
            "text" => session.SetText(Required(operation.ComponentId, "componentId"),
                operation.Text ?? throw new ServiceException("InvalidOperation", "text is required.")),
            "delete" => session.Delete(ComponentIds(operation)),
            "duplicate" => session.Duplicate(ComponentIds(operation)),
            "rename" => session.Rename(Required(operation.ComponentId, "componentId"), Required(operation.Name, "name")),
            "order" => session.ChangeZOrder(Required(operation.ComponentId, "componentId"), ZOrder(operation.Order)),
            "reparent" => session.Reparent(Required(operation.ComponentId, "componentId"),
                Required(operation.ParentId, "parentId")),
            "frames" => session.SetFrames(operation.Frames is { Count: > 0 } frames ? frames
                : throw new ServiceException("InvalidOperation", "frames is required.")),
            "themeColor" => session.SetThemeColor(Required(operation.TokenId, "tokenId"),
                PropertyValues.ParseEncodedColor(Required(operation.Color, "color"))),
            "renameThemeColor" => session.RenameThemeColor(Required(operation.TokenId, "tokenId"),
                Required(operation.NewTokenId, "newTokenId")),
            "deleteThemeColor" => session.RemoveThemeColor(Required(operation.TokenId, "tokenId")),
            "action" => AssignAction(session, operation),
            "property" => session.SetProperty(Required(operation.ComponentId, "componentId"),
                PropertyValues.Parse(
                    Definition(Required(operation.PropertyId, "propertyId")),
                    operation.Value ?? throw new ServiceException("InvalidOperation", "value is required."))),
            "reset" => session.ResetProperty(Required(operation.ComponentId, "componentId"),
                Definition(Required(operation.PropertyId, "propertyId")).Id),
            _ => throw new ServiceException("InvalidOperation", "Unknown edit operation: " + operation.Kind)
        };
        if (!result.IsValid)
        {
            throw new ServiceException("InvalidEdit", Messages(result.Diagnostics));
        }
        // A new component (the first duplicate root, in document order) becomes the selection.
        string? selected = operation.Kind is "add" or "duplicate"
            ? session.Document.Components.FirstOrDefault(item => !before.Contains(item.ComponentId))?.ComponentId
            : operation.Kind == "delete" ? null : operation.ComponentId;
        if (operation.Kind == "add" && operation.Frame is { } initialFrame)
        {
            FormEditResult placed = session.SetFrame(selected!, initialFrame);
            if (!placed.IsValid)
            {
                throw new ServiceException("InvalidEdit", Messages(placed.Diagnostics));
            }
            result = result with { Diagnostics = [.. result.Diagnostics, .. placed.Diagnostics] };
        }
        FormXmlWriteResult written = FormXmlWriter.Write(session.Document, _catalog);
        if (!written.IsValid || written.Utf8 is null)
        {
            throw new ServiceException("InvalidEdit", Messages(written.Diagnostics));
        }
        DesignerWorkspace edited = workspace with { Form = session.Document };
        ProfileDiagnostic[] diagnostics = [.. DesignerProductResolver.Resolve(edited).Diagnostics, .. result.Diagnostics];
        return Inspect(context with { Workspace = edited, Diagnostics = diagnostics }, parameters.DocumentVersion!.Value)
            with { Xml = Encoding.UTF8.GetString(written.Utf8), SelectedComponentId = selected };
    }

    private WidgetPropertyDefinition Definition(string propertyId)
    {
        WidgetPropertyDefinition definition = _catalog.Properties.SingleOrDefault(item => item.Id == propertyId)
            ?? throw new ServiceException("InvalidOperation", $"Unknown property '{propertyId}'.");
        if (!PropertyValues.IsEditable(definition))
        {
            throw new ServiceException(
                "InvalidOperation",
                $"Property '{propertyId}' is edited through its dedicated frame or resource operation.");
        }
        return definition;
    }

    private async Task<object> RenderAsync(Context context, int version)
    {
        DesignerWorkspace workspace = RequireForm(context);
        if (_preview is null)
        {
            throw new ServiceException("PreviewUnavailable", "Build the native preview worker and configure its path to render this form.");
        }
        PreviewSurface surface = PreviewSurfaceFactory.Create(context.Display);
        (int imageWidth, int imageHeight) = PreviewPresentation.ImageSize(surface);
        if (checked((long)imageWidth * imageHeight * 4) > Protocol.MaximumRgbaBytes)
        {
            throw new ServiceException("PreviewSizeLimit", "The preview exceeds the 16 MiB RGBA presentation limit.");
        }
        FormXmlWriteResult snapshot = FormXmlWriter.Write(workspace.Form, _catalog);
        if (!snapshot.IsValid || snapshot.Utf8 is null)
        {
            throw new ServiceException("InvalidForm", Messages(snapshot.Diagnostics));
        }
        PreviewWorkerCapabilities capabilities = await _preview.StartAsync().ConfigureAwait(false);
        if (capabilities.WidgetCatalogSha256 != _catalogHash)
        {
            await _preview.StopAsync().ConfigureAwait(false);
            throw new ServiceException("CatalogMismatch", "Rebuild the native preview worker for this library's widget catalog.");
        }
        PreviewFrame frame = await _preview.RenderAsync(new(surface, snapshot.Utf8),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        DiagnosticView[] diagnostics = Diagnostics(context.Diagnostics);
        if (frame.Status != PreviewStatus.Ok)
        {
            diagnostics = [.. diagnostics, new("NativePreview", "error", frame.Diagnostic, context.FormPath)];
        }
        // The presented image may exceed the logical extent: packed cells are shown as emitters.
        return new
        {
            documentVersion = version,
            width = imageWidth,
            height = imageHeight,
            rgbaBase64 = frame.Status == PreviewStatus.Ok ? Convert.ToBase64String(PreviewPixels.ToRgba(frame).Rgba) : string.Empty,
            status = frame.Status.ToString(),
            diagnostics
        };
    }

    private ScreenCreationPlan AddScreen(Context context, RequestParameters parameters)
    {
        DesignerWorkspace workspace = RequireForm(context);
        var paths = DesignerProjectPathService.Load(context.ManifestPath, _libraryRoot);
        if (!paths.ComponentDirectory.Contains("{form}", StringComparison.Ordinal) &&
            !DesignerProjectForms.LoadAll(workspace).Any(item => item.Form.Id == "form.main"))
            throw new ServiceException("ScreenOutputLayout", "Set componentDirectory to a per-screen path such as Tasks/UI/Generated/{form} before extending a project without a legacy form.main. Update its application includes to match.");
        return DesignerProjectForms.PlanAdd(workspace, Required(parameters.ScreenName, "Screen name"));
    }

    private object Export(Context context, RequestParameters parameters)
    {
        DesignerWorkspace workspace = RequireForm(context);
        FileInfo form = new(context.FormPath);
        if (form.Length > Protocol.MaximumXmlBytes ||
            parameters.Xml is null ||
            !string.Equals(NormalizeEditorLineEndings(File.ReadAllText(context.FormPath)),
                NormalizeEditorLineEndings(parameters.Xml), StringComparison.Ordinal))
        {
            throw new ServiceException("SaveRequired", "Save this form before generating its UI component.");
        }
        ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(context.ManifestPath, _libraryRoot);
        WidgetCatalogLoadResult selected = WidgetCatalogLoader.LoadFile(Path.Combine(paths.LibraryRoot, "schema", "widgets.json"));
        if (!selected.IsValid || selected.Sha256 != _catalogHash)
        {
            throw new ServiceException("CatalogMismatch", "The configured project library does not match this designer service's catalog.");
        }
        ComponentExportResult result = ComponentProjectExporter.Export(workspace, paths.ComponentDirectory);
        if (!result.IsValid)
        {
            throw new ServiceException("ExportFailed", Messages(result.Diagnostics));
        }
        return new
        {
            documentVersion = parameters.DocumentVersion,
            result.OutputDirectory,
            paths.BuildDirectory,
            paths.ApplicationRoot,
            paths.LibraryRoot,
            result.ManifestPath,
            diagnostics = Diagnostics(result.Diagnostics)
        };
    }

    // VS Code's text model normalizes CRLF/CR/LF even when a clean file on disk mixes them.
    // Ignore only that representation difference. Spaces, trailing newlines and content still
    // participate in the saved-source check; generation must never rewrite the source file.
    private static string NormalizeEditorLineEndings(string xml) =>
        xml.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static DesignerWorkspace RequireForm(Context context) => context.Workspace
        ?? throw new ServiceException("InvalidForm", Messages(context.Diagnostics));

    private static string Required(string? value, string name) => !string.IsNullOrWhiteSpace(value)
        ? value : throw new ServiceException("InvalidOperation", name + " is required.");

    private static string[] ComponentIds(EditOperation operation)
    {
        string[] ids = operation.ComponentIds is { Length: > 0 } many ? many
            : [Required(operation.ComponentId, "componentId")];
        if (ids.Length > Protocol.MaximumEditComponents || ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ServiceException("InvalidOperation",
                $"componentIds must name 1 through {Protocol.MaximumEditComponents} components.");
        }
        return ids;
    }

    private static FormZOrderOperation ZOrder(string? order) => order switch
    {
        "bringForward" => FormZOrderOperation.BringForward,
        "sendBackward" => FormZOrderOperation.SendBackward,
        "bringToFront" => FormZOrderOperation.BringToFront,
        "sendToBack" => FormZOrderOperation.SendToBack,
        _ => throw new ServiceException("InvalidOperation",
            "order must be bringForward, sendBackward, bringToFront, or sendToBack.")
    };

    private static FormEditResult AssignAction(DesignerDocumentSession session, EditOperation operation)
    {
        string componentId = Required(operation.ComponentId, "componentId");
        string widgetActionId = Required(operation.WidgetActionId, "widgetActionId");
        if (operation.NewAction is not { } created)
        {
            return session.AssignAction(componentId, widgetActionId, operation.ActionId);
        }
        if (created.RuntimeId is < 1 or > ushort.MaxValue)
        {
            throw new ServiceException("InvalidEdit", "An action runtime ID must be 1 through 65535.");
        }
        return session.CreateAndAssignAction(componentId, widgetActionId, new FormAction
        {
            Id = created.Id, RuntimeId = (ushort)created.RuntimeId, Symbol = created.Symbol
        });
    }

    private WidgetActionDefinition[] WidgetActions(string widgetId) =>
        _catalog.Widgets.SingleOrDefault(item => item.Id == widgetId)?.Actions ?? [];

    private static string AbsolutePath(string? path, string name) =>
        path is not null && Path.IsPathFullyQualified(path) ? Path.GetFullPath(path)
        : throw new ServiceException("InvalidPath", name + " must be an absolute path.");

    private static DiagnosticView[] Diagnostics(IEnumerable<ProfileDiagnostic> diagnostics) =>
        [.. diagnostics.Select(item => new DiagnosticView(item.Code, item.Severity.ToString().ToLowerInvariant(), item.Message, item.Path))];

    private static string Messages(IEnumerable<ProfileDiagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));

    public async ValueTask DisposeAsync()
    {
        if (_preview is not null)
        {
            await _preview.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record Context(DesignerWorkspace? Workspace, DesignerDisplay Display,
        string ManifestPath, string FormPath, ProfileDiagnostic[] Diagnostics);
}
