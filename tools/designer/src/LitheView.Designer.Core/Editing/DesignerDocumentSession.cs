// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Editing;

public sealed record FormEditResult
{
    public required bool Changed { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public enum FormZOrderOperation
{
    SendBackward,
    BringForward,
    SendToBack,
    BringToFront
}

public sealed class DesignerDocumentSession
{
    private readonly WidgetCatalog _catalog;
    private readonly List<FormDocument> _history;
    private readonly List<string> _descriptions = [];
    private int _position;
    private int _cleanPosition;

    public DesignerDocumentSession(FormDocument document, WidgetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(catalog);
        FormResolutionResult resolution = FormDocumentResolver.Resolve(document, catalog);
        if (!resolution.IsValid)
        {
            throw new ArgumentException(FormatDiagnostics(resolution.Diagnostics), nameof(document));
        }
        _catalog = catalog;
        _history = [document];
    }

    public event EventHandler? Changed;

    public FormDocument Document => _history[_position];

    public bool CanUndo => _position > 0;

    public bool CanRedo => _position + 1 < _history.Count;

    public bool IsDirty => _position != _cleanPosition;

    public string? UndoDescription => CanUndo ? _descriptions[_position - 1] : null;

    public string? RedoDescription => CanRedo ? _descriptions[_position] : null;

    public void MarkClean()
    {
        _cleanPosition = _position;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public FormEditResult AddWidget(string widgetId, string parentComponentId)
    {
        WidgetDefinition? widget = _catalog.Widgets.SingleOrDefault(item => item.Id == widgetId);
        FormComponent? parent = Document.Components.SingleOrDefault(
            item => item.ComponentId == parentComponentId);
        if (widget is null)
        {
            return Failed("LVED1001", "$.widgetId", $"Widget '{widgetId}' is not in the catalog.");
        }
        if (parent is null)
        {
            return Failed(
                "LVED1002",
                "$.parentComponentId",
                $"Parent component '{parentComponentId}' does not exist.");
        }
        if (widget.ParentRule.AllowedParentIds.Length > 0 &&
            !widget.ParentRule.AllowedParentIds.Contains(parent.WidgetId, StringComparer.Ordinal))
        {
            return Failed(
                "LVED1003",
                "$.parentComponentId",
                $"Widget '{widget.DisplayName}' cannot be parented by '{parent.WidgetId}'.");
        }

        string stem = widget.Xml.Element.Replace('-', '_');
        int ordinal = NextOrdinal(stem);
        string name = ToCamelCase(stem) + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string componentId = $"component.{stem}.{ordinal}";
        FormRectangle parentFrame = GetFrame(parent);
        int width = Math.Min(widget.DefaultGeometry.Width, parentFrame.Width);
        int height = Math.Min(widget.DefaultGeometry.Height, parentFrame.Height);
        FormRectangle frame = new(
            Math.Max(0, (parentFrame.Width - width) / 2),
            Math.Max(0, (parentFrame.Height - height) / 2),
            Math.Max(1, width),
            Math.Max(1, height));
        WidgetDefinition? parentWidget = _catalog.Widgets.SingleOrDefault(item => item.Id == parent.WidgetId);
        if (parentWidget is not null && FormTabRules.IsTabView(parentWidget))
        {
            // A new tab-view child is a page and fills the content area below the strip.
            FormRectangle content = FormTabRules.ContentFrame(parentFrame, FormTabRules.TabHeight(parent.Properties));
            frame = content with { Width = Math.Max(1, content.Width), Height = Math.Max(1, content.Height) };
        }
        List<FormPropertyValue> properties = [new FormRectangleValue("frame", frame)];
        List<FormBinding> bindings = [.. Document.Bindings];
        if (widget.RuntimeKind == "textField")
        {
            // A palette-created field needs an explicit, bounded application
            // binding. Creating the declaration allocates no runtime storage.
            const int capacityBytes = 32;
            FormBinding binding = NewTextBinding(bindings, capacityBytes);
            bindings.Add(binding);
            properties.Add(new FormReferenceValue("binding", FormReferenceKind.Binding, binding.Id));
            properties.Add(new FormUnsignedIntegerValue("max-length", capacityBytes - 1U));
        }
        if (widget.PropertyIds.Contains("text", StringComparer.Ordinal))
        {
            properties.Add(new FormTextValue("text", widget.DisplayName));
        }
        if (FormTabRules.IsTabWidget(widget))
        {
            properties.Add(new FormTextValue("items", FormTabRules.StarterTitles));
        }
        if (FormWheelRules.IsWheelPicker(widget) || widget.RuntimeKind == "dropDown")
        {
            properties.Add(new FormTextValue("items", FormWheelRules.StarterItems));
        }
        int siblingOrder = Document.Components
            .Where(item => item.ParentComponentId == parentComponentId)
            .Select(item => item.SiblingOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        FormComponent component = new()
        {
            ComponentId = componentId,
            Name = name,
            WidgetId = widget.Id,
            ParentComponentId = parentComponentId,
            SiblingOrder = siblingOrder,
            Properties = [.. properties],
            HistoricalAttributes = []
        };
        return Commit(
            Document with { Components = [.. Document.Components, component], Bindings = [.. bindings] },
            $"Add {widget.DisplayName}");
    }

    public FormEditResult SetFrame(string componentId, FormRectangle frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0)
        {
            return Failed("LVED1101", "$.frame", "Component width and height must be positive.");
        }
        return UpdateComponent(
            componentId,
            component => component with
            {
                Properties = ReplaceProperty(
                    component.Properties,
                    new FormRectangleValue("frame", frame))
            },
            "Change component geometry");
    }

    public FormEditResult SetFrames(IReadOnlyDictionary<string, FormRectangle> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
        {
            return Unchanged();
        }
        foreach ((string componentId, FormRectangle frame) in frames)
        {
            if (frame.Width <= 0 || frame.Height <= 0)
            {
                return Failed(
                    "LVED1101",
                    $"$.components['{componentId}'].frame",
                    "Component width and height must be positive.");
            }
            if (Document.Components.All(item => item.ComponentId != componentId))
            {
                return Failed(
                    "LVED1004",
                    "$.componentId",
                    $"Component '{componentId}' does not exist.");
            }
        }

        FormComponent[] components = [.. Document.Components];
        for (int index = 0; index < components.Length; ++index)
        {
            FormComponent component = components[index];
            if (frames.TryGetValue(component.ComponentId, out FormRectangle frame))
            {
                components[index] = component with
                {
                    Properties = ReplaceProperty(
                        component.Properties,
                        new FormRectangleValue("frame", frame))
                };
            }
        }
        return Commit(Document with { Components = components }, "Change component geometry");
    }

    public FormEditResult SetText(string componentId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return UpdateComponent(
            componentId,
            component => component with
            {
                Properties = ReplaceProperty(
                    component.Properties,
                    new FormTextValue("text", text))
            },
            "Change component text");
    }

    public FormEditResult SetProperty(string componentId, FormPropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        FormComponent? component = Document.Components.SingleOrDefault(
            item => item.ComponentId == componentId);
        if (component is null)
        {
            return Failed("LVED1004", "$.componentId", $"Component '{componentId}' does not exist.");
        }
        WidgetDefinition widget = _catalog.Widgets.Single(item => item.Id == component.WidgetId);
        if (!widget.PropertyIds.Contains(value.PropertyId, StringComparer.Ordinal))
        {
            return Failed(
                "LVED1102",
                "$.propertyId",
                $"Property '{value.PropertyId}' is not supported by '{widget.Id}'.");
        }
        return UpdateComponent(
            componentId,
            item => item with
            {
                Properties = ClearLatchForMomentary(
                    ReplaceProperty(item.Properties, value),
                    value.PropertyId)
            },
            $"Change {value.PropertyId}");
    }

    public FormEditResult ResetProperty(string componentId, string propertyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyId);
        return UpdateComponent(
            componentId,
            component => component with
            {
                Properties = ClearLatchForMomentary(
                    RemoveProperty(component.Properties, propertyId),
                    propertyId)
            },
            $"Reset {propertyId}");
    }

    // Changing a button to momentary in one edit also clears its checked value, matching
    // ButtonView::setBehavior(). Setting checked on a momentary button is still rejected
    // by strict resolution rather than silently discarded.
    private static FormPropertyValue[] ClearLatchForMomentary(
        FormPropertyValue[] properties,
        string editedPropertyId)
    {
        if (editedPropertyId != "button-behavior" ||
            properties.OfType<FormTextValue>().Any(item =>
                item.PropertyId == "button-behavior" && item.Value == "toggle"))
        {
            return properties;
        }
        return RemoveProperty(properties, "checked");
    }

    public FormEditResult Rename(string componentId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return UpdateComponent(
            componentId,
            component => component with { Name = name },
            "Rename component");
    }

    public FormEditResult AddResource(FormResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (Document.Resources.Any(item => item.Id == resource.Id))
        {
            return Failed(
                "LVED1701",
                "$.resource.id",
                $"Form resource '{resource.Id}' already exists.");
        }
        return Commit(
            Document with { Resources = [.. Document.Resources, resource] },
            $"Add {resource.Kind.ToString().ToLowerInvariant()} resource");
    }

    public FormEditResult AddResources(IReadOnlyList<FormResource> resources, string description)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (resources.Count == 0)
        {
            return Failed("LVED1702", "$.resources", "At least one form resource is required.");
        }
        return Commit(
            Document with { Resources = [.. Document.Resources, .. resources] },
            description);
    }

    public FormEditResult SetThemeColors(IReadOnlyList<FormThemeColor> themeColors)
    {
        ArgumentNullException.ThrowIfNull(themeColors);
        return Commit(
            Document with { ThemeColors = [.. themeColors] },
            "Edit theme color tokens");
    }

    /// <summary>Adds a theme color token, or changes an existing token's color, as one edit.</summary>
    public FormEditResult SetThemeColor(string tokenId, uint encodedColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        FormThemeColor color = new() { Id = tokenId, EncodedColor = encodedColor };
        FormThemeColor? existing = Document.ThemeColors.SingleOrDefault(item => item.Id == tokenId);
        if (existing == color)
        {
            return Unchanged();
        }
        FormThemeColor[] colors = existing is null
            ? [.. Document.ThemeColors, color]
            : [.. Document.ThemeColors.Select(item => item.Id == tokenId ? color : item)];
        return Commit(
            Document with { ThemeColors = colors },
            existing is null ? "Add theme color" : "Change theme color");
    }

    /// <summary>Renames a theme color token and every property reference to it in one edit.</summary>
    public FormEditResult RenameThemeColor(string tokenId, string newTokenId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTokenId);
        if (Document.ThemeColors.All(item => item.Id != tokenId))
        {
            return Failed("LVED1801", "$.tokenId", $"Theme color '{tokenId}' does not exist.");
        }
        if (tokenId == newTokenId)
        {
            return Unchanged();
        }
        if (Document.ThemeColors.Any(item => item.Id == newTokenId))
        {
            return Failed("LVED1803", "$.newTokenId", $"Theme color '{newTokenId}' already exists.");
        }
        FormThemeColor[] colors = [.. Document.ThemeColors.Select(item =>
            item.Id == tokenId ? item with { Id = newTokenId } : item)];
        FormComponent[] components = [.. Document.Components.Select(component => component with
        {
            Properties = [.. component.Properties.Select(property =>
                property is FormColorValue { ThemeTokenId: string token } && token == tokenId
                    ? new FormColorValue(property.PropertyId, newTokenId)
                    : property)]
        })];
        return Commit(
            Document with { ThemeColors = colors, Components = components },
            "Rename theme color");
    }

    /// <summary>Deletes an unused theme color token; a token still referenced is reported, not removed.</summary>
    public FormEditResult RemoveThemeColor(string tokenId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        if (Document.ThemeColors.All(item => item.Id != tokenId))
        {
            return Failed("LVED1801", "$.tokenId", $"Theme color '{tokenId}' does not exist.");
        }
        int uses = Document.Components.Sum(component => component.Properties.Count(property =>
            property is FormColorValue { ThemeTokenId: string token } && token == tokenId));
        if (uses > 0)
        {
            return Failed(
                "LVED1802",
                "$.tokenId",
                $"Theme color '{tokenId}' is used by {uses} color propert{(uses == 1 ? "y" : "ies")}; " +
                "choose other colors for them before deleting it.");
        }
        return Commit(
            Document with { ThemeColors = [.. Document.ThemeColors.Where(item => item.Id != tokenId)] },
            "Delete theme color");
    }

    public FormEditResult AssignAction(
        string componentId,
        string widgetActionId,
        string? formActionId)
    {
        return AssignAction(componentId, widgetActionId, formActionId, newAction: null);
    }

    public FormEditResult CreateAndAssignAction(
        string componentId,
        string widgetActionId,
        FormAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return AssignAction(componentId, widgetActionId, action.Id, action);
    }

    public FormEditResult Delete(string componentId)
    {
        return Delete([componentId]);
    }

    public FormEditResult Delete(IEnumerable<string> componentIds)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        HashSet<string> requested = new(componentIds, StringComparer.Ordinal);
        if (requested.Count == 0)
        {
            return Unchanged();
        }
        foreach (string componentId in requested)
        {
            FormComponent? component = Document.Components.SingleOrDefault(
                item => item.ComponentId == componentId);
            if (component is null)
            {
                return Failed(
                    "LVED1201",
                    "$.componentId",
                    $"Component '{componentId}' does not exist.");
            }
            if (component.ParentComponentId is null)
            {
                return Failed("LVED1202", "$.componentId", "The root component cannot be deleted.");
            }
        }

        HashSet<string> removed = [.. requested];
        bool expanded;
        do
        {
            expanded = false;
            foreach (FormComponent candidate in Document.Components)
            {
                if (candidate.ParentComponentId is not null &&
                    removed.Contains(candidate.ParentComponentId) &&
                    removed.Add(candidate.ComponentId))
                {
                    expanded = true;
                }
            }
        } while (expanded);

        FormComponent[] retained = [.. Document.Components.Where(item => !removed.Contains(item.ComponentId))];
        return Commit(
            Document with { Components = NormalizeSiblingOrder(retained) },
            requested.Count == 1 ? "Delete component" : "Delete components");
    }

    public FormEditResult Duplicate(string componentId)
    {
        return Duplicate([componentId]);
    }

    public FormEditResult Duplicate(IEnumerable<string> componentIds)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        HashSet<string> requested = new(componentIds, StringComparer.Ordinal);
        if (requested.Count == 0)
        {
            return Unchanged();
        }
        Dictionary<string, FormComponent> byId = Document.Components.ToDictionary(
            item => item.ComponentId,
            StringComparer.Ordinal);
        foreach (string componentId in requested)
        {
            if (!byId.TryGetValue(componentId, out FormComponent? component))
            {
                return Failed(
                    "LVED1301",
                    "$.componentId",
                    $"Component '{componentId}' does not exist.");
            }
            if (component.ParentComponentId is null)
            {
                return Failed("LVED1302", "$.componentId", "The root component cannot be duplicated.");
            }
        }

        HashSet<string> roots = [.. requested.Where(componentId =>
            !HasRequestedAncestor(componentId, requested, byId))];
        HashSet<string> reservedIds = [.. byId.Keys];
        HashSet<string> reservedNames = [.. Document.Components.Select(item => item.Name)];
        Dictionary<string, string> duplicateIds = new(StringComparer.Ordinal);
        List<FormComponent> duplicates = [];
        List<FormBinding> bindings = [.. Document.Bindings];
        foreach (FormComponent source in Document.Components.Where(item =>
                     roots.Any(rootId => IsSelfOrDescendant(item.ComponentId, rootId, byId))))
        {
            WidgetDefinition widget = _catalog.Widgets.Single(item => item.Id == source.WidgetId);
            string stem = widget.Xml.Element.Replace('-', '_');
            int ordinal = NextOrdinal(stem, reservedIds);
            string duplicateId = $"component.{stem}.{ordinal}";
            reservedIds.Add(duplicateId);
            duplicateIds.Add(source.ComponentId, duplicateId);
            string duplicateName = NextName(source.Name, reservedNames);
            reservedNames.Add(duplicateName);
            string? parentId = source.ParentComponentId;
            if (parentId is not null && duplicateIds.TryGetValue(parentId, out string? duplicateParentId))
            {
                parentId = duplicateParentId;
            }
            FormPropertyValue[] properties = CloneProperties(source.Properties);
            if (widget.RuntimeKind == "textField")
            {
                FormReferenceValue reference = properties.OfType<FormReferenceValue>()
                    .Single(item => item.PropertyId == "binding");
                FormBinding original = Document.Bindings.Single(item => item.Id == reference.ReferenceId);
                FormBinding binding = NewTextBinding(bindings, original.CapacityBytes);
                bindings.Add(binding);
                properties = ReplaceProperty(properties, reference with { ReferenceId = binding.Id });
            }
            if (roots.Contains(source.ComponentId))
            {
                FormComponent parent = byId[source.ParentComponentId!];
                FormRectangle frame = GetFrame(source);
                FormRectangle parentFrame = GetFrame(parent);
                properties = ReplaceProperty(
                    properties,
                    new FormRectangleValue("frame", OffsetWithinParent(frame, parentFrame)));
            }
            duplicates.Add(source with
            {
                ComponentId = duplicateId,
                Name = duplicateName,
                ParentComponentId = parentId,
                Properties = properties,
                HistoricalAttributes = [.. source.HistoricalAttributes]
            });
        }

        List<FormComponent> components = [.. Document.Components, .. duplicates];
        return Commit(
            Document with { Components = NormalizeSiblingOrder([.. components]), Bindings = [.. bindings] },
            roots.Count == 1 ? "Duplicate component" : "Duplicate components");
    }

    public FormEditResult ChangeZOrder(string componentId, FormZOrderOperation operation)
    {
        FormComponent? component = Document.Components.SingleOrDefault(
            item => item.ComponentId == componentId);
        if (component is null)
        {
            return Failed("LVED1401", "$.componentId", $"Component '{componentId}' does not exist.");
        }
        if (component.ParentComponentId is null)
        {
            return Failed("LVED1402", "$.componentId", "The root component has no sibling order.");
        }
        FormComponent[] siblings = [.. Document.Components
            .Where(item => item.ParentComponentId == component.ParentComponentId)
            .OrderBy(item => item.SiblingOrder)];
        int current = Array.FindIndex(siblings, item => item.ComponentId == componentId);
        int target = operation switch
        {
            FormZOrderOperation.SendBackward => Math.Max(0, current - 1),
            FormZOrderOperation.BringForward => Math.Min(siblings.Length - 1, current + 1),
            FormZOrderOperation.SendToBack => 0,
            FormZOrderOperation.BringToFront => siblings.Length - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        if (target == current)
        {
            return Unchanged();
        }
        List<FormComponent> reordered = [.. siblings];
        reordered.RemoveAt(current);
        reordered.Insert(target, component);
        Dictionary<string, int> orders = reordered
            .Select((item, index) => (item.ComponentId, index))
            .ToDictionary(item => item.ComponentId, item => item.index, StringComparer.Ordinal);
        FormComponent[] components = [.. Document.Components.Select(item =>
            orders.TryGetValue(item.ComponentId, out int order)
                ? item with { SiblingOrder = order }
                : item)];
        return Commit(Document with { Components = InPreorder(components) }, "Change component order");
    }

    public FormEditResult Reparent(string componentId, string parentComponentId)
    {
        FormComponent? component = Document.Components.SingleOrDefault(
            item => item.ComponentId == componentId);
        FormComponent? parent = Document.Components.SingleOrDefault(
            item => item.ComponentId == parentComponentId);
        if (component is null)
        {
            return Failed("LVED1501", "$.componentId", $"Component '{componentId}' does not exist.");
        }
        if (parent is null)
        {
            return Failed(
                "LVED1502",
                "$.parentComponentId",
                $"Parent component '{parentComponentId}' does not exist.");
        }
        if (component.ParentComponentId is null)
        {
            return Failed("LVED1503", "$.componentId", "The root component cannot be reparented.");
        }
        if (component.ParentComponentId == parentComponentId)
        {
            return Unchanged();
        }
        Dictionary<string, FormComponent> byId = Document.Components.ToDictionary(
            item => item.ComponentId,
            StringComparer.Ordinal);
        if (IsSelfOrDescendant(parentComponentId, componentId, byId))
        {
            return Failed("LVED1504", "$.parentComponentId", "A component cannot parent itself or an ancestor.");
        }
        WidgetDefinition widget = _catalog.Widgets.Single(item => item.Id == component.WidgetId);
        WidgetDefinition parentWidget = _catalog.Widgets.Single(item => item.Id == parent.WidgetId);
        if (parentWidget.ChildPolicy != WidgetChildPolicy.Many ||
            (widget.ParentRule.AllowedParentIds.Length > 0 &&
             !widget.ParentRule.AllowedParentIds.Contains(parent.WidgetId, StringComparer.Ordinal)))
        {
            return Failed(
                "LVED1505",
                "$.parentComponentId",
                $"Widget '{widget.DisplayName}' cannot be parented by '{parent.WidgetId}'.");
        }

        FormRectangle absolute = AbsoluteFrame(component, byId);
        FormRectangle parentAbsolute = AbsoluteFrame(parent, byId);
        FormRectangle local = absolute with
        {
            X = absolute.X - parentAbsolute.X,
            Y = absolute.Y - parentAbsolute.Y
        };
        int siblingOrder = Document.Components
            .Where(item => item.ParentComponentId == parentComponentId)
            .Select(item => item.SiblingOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        FormComponent[] components = [.. Document.Components.Select(item =>
            item.ComponentId == componentId
                ? item with
                {
                    ParentComponentId = parentComponentId,
                    SiblingOrder = siblingOrder,
                    Properties = ReplaceProperty(
                        item.Properties,
                        new FormRectangleValue("frame", local))
                }
                : item)];
        // The moved subtree must follow its new parent in document (preorder) order.
        return Commit(
            Document with { Components = NormalizeSiblingOrder(InPreorder(components)) },
            "Reparent component");
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }
        --_position;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }
        ++_position;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private FormEditResult UpdateComponent(
        string componentId,
        Func<FormComponent, FormComponent> update,
        string description)
    {
        int index = Array.FindIndex(
            Document.Components,
            item => item.ComponentId == componentId);
        if (index < 0)
        {
            return Failed("LVED1004", "$.componentId", $"Component '{componentId}' does not exist.");
        }
        FormComponent[] components = [.. Document.Components];
        components[index] = update(components[index]);
        return Commit(Document with { Components = components }, description);
    }

    private FormEditResult AssignAction(
        string componentId,
        string widgetActionId,
        string? formActionId,
        FormAction? newAction)
    {
        int componentIndex = Array.FindIndex(
            Document.Components,
            item => item.ComponentId == componentId);
        if (componentIndex < 0)
        {
            return Failed("LVED1601", "$.componentId", $"Component '{componentId}' does not exist.");
        }
        FormComponent component = Document.Components[componentIndex];
        WidgetDefinition widget = _catalog.Widgets.Single(item => item.Id == component.WidgetId);
        WidgetActionDefinition? widgetAction = widget.Actions.SingleOrDefault(
            item => item.Id == widgetActionId);
        if (widgetAction is null)
        {
            return Failed(
                "LVED1602",
                "$.widgetActionId",
                $"Widget action '{widgetActionId}' does not exist on '{widget.Id}'.");
        }
        WidgetPropertyDefinition source = _catalog.Properties.Single(
            item => item.Id == widgetAction.RuntimeActionSource);
        FormAction[] actions = newAction is null
            ? Document.Actions
            : [.. Document.Actions, newAction];
        FormPropertyValue[] properties;
        if (formActionId is null)
        {
            properties = RemoveProperty(component.Properties, source.Id);
        }
        else
        {
            FormAction? action = actions.SingleOrDefault(item => item.Id == formActionId);
            if (action is null)
            {
                return Failed(
                    "LVED1603",
                    "$.formActionId",
                    $"Form action '{formActionId}' does not exist.");
            }
            FormPropertyValue value = source.Kind switch
            {
                WidgetPropertyKind.ActionId => new FormReferenceValue(
                    source.Id,
                    FormReferenceKind.Action,
                    action.Id),
                WidgetPropertyKind.UnsignedInteger => new FormUnsignedIntegerValue(
                    source.Id,
                    action.RuntimeId),
                _ => throw new InvalidOperationException(
                    $"Widget action source '{source.Id}' has unsupported kind '{source.Kind}'.")
            };
            properties = ReplaceProperty(component.Properties, value);
        }
        FormComponent[] components = [.. Document.Components];
        components[componentIndex] = component with { Properties = properties };
        return Commit(
            Document with { Components = components, Actions = actions },
            newAction is null ? "Assign component action" : "Create and assign component action");
    }

    private FormEditResult Commit(FormDocument candidate, string description)
    {
        FormResolutionResult resolution = FormDocumentResolver.Resolve(candidate, _catalog);
        if (!resolution.IsValid)
        {
            return new FormEditResult { Changed = false, Diagnostics = resolution.Diagnostics };
        }
        if (_position + 1 < _history.Count)
        {
            _history.RemoveRange(_position + 1, _history.Count - _position - 1);
            _descriptions.RemoveRange(_position, _descriptions.Count - _position);
            if (_cleanPosition > _position)
            {
                _cleanPosition = -1;
            }
        }
        _history.Add(candidate);
        _descriptions.Add(description);
        ++_position;
        Changed?.Invoke(this, EventArgs.Empty);
        return new FormEditResult { Changed = true, Diagnostics = resolution.Diagnostics };
    }

    private int NextOrdinal(string stem)
    {
        return NextOrdinal(
            stem,
            new HashSet<string>(
                Document.Components.Select(item => item.ComponentId),
                StringComparer.Ordinal));
    }

    private static int NextOrdinal(string stem, HashSet<string> reservedIds)
    {
        for (int ordinal = 1; ordinal < int.MaxValue; ++ordinal)
        {
            string candidate = $"component.{stem}.{ordinal}";
            if (!reservedIds.Contains(candidate))
            {
                return ordinal;
            }
        }
        throw new InvalidOperationException("No component ordinal remains available.");
    }

    private static FormPropertyValue[] CloneProperties(FormPropertyValue[] properties)
    {
        return [.. properties.Select(property => property switch
        {
            FormReferenceListValue list => list with { ReferenceIds = [.. list.ReferenceIds] },
            FormUnsignedIntegerValue { PropertyId: "runtime-id" } value =>
                new FormUnsignedIntegerValue(value.PropertyId, 0U),
            _ => property
        })];
    }

    private static FormRectangle OffsetWithinParent(FormRectangle frame, FormRectangle parentFrame)
    {
        const int offset = 8;
        return frame with
        {
            X = Math.Min(Math.Max(0, parentFrame.Width - frame.Width), frame.X + offset),
            Y = Math.Min(Math.Max(0, parentFrame.Height - frame.Height), frame.Y + offset)
        };
    }

    private static string NextName(string source, HashSet<string> reservedNames)
    {
        for (int ordinal = 2; ordinal < int.MaxValue; ++ordinal)
        {
            string candidate = source + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!reservedNames.Contains(candidate))
            {
                return candidate;
            }
        }
        throw new InvalidOperationException("No component name ordinal remains available.");
    }

    private static bool HasRequestedAncestor(
        string componentId,
        HashSet<string> requested,
        Dictionary<string, FormComponent> components)
    {
        string? parentId = components[componentId].ParentComponentId;
        while (parentId is not null)
        {
            if (requested.Contains(parentId))
            {
                return true;
            }
            parentId = components[parentId].ParentComponentId;
        }
        return false;
    }

    private static bool IsSelfOrDescendant(
        string componentId,
        string possibleAncestorId,
        Dictionary<string, FormComponent> components)
    {
        string? currentId = componentId;
        while (currentId is not null)
        {
            if (currentId == possibleAncestorId)
            {
                return true;
            }
            currentId = components[currentId].ParentComponentId;
        }
        return false;
    }

    private static FormRectangle AbsoluteFrame(
        FormComponent component,
        Dictionary<string, FormComponent> components)
    {
        FormRectangle frame = GetFrame(component);
        string? parentId = component.ParentComponentId;
        while (parentId is not null)
        {
            FormComponent parent = components[parentId];
            FormRectangle parentFrame = GetFrame(parent);
            frame = frame with { X = frame.X + parentFrame.X, Y = frame.Y + parentFrame.Y };
            parentId = parent.ParentComponentId;
        }
        return frame;
    }

    private static FormPropertyValue[] ReplaceProperty(
        FormPropertyValue[] properties,
        FormPropertyValue replacement)
    {
        List<FormPropertyValue> updated = [.. properties.Where(
            item => item.PropertyId != replacement.PropertyId)];
        updated.Add(replacement);
        return [.. updated];
    }

    private static FormPropertyValue[] RemoveProperty(
        FormPropertyValue[] properties,
        string propertyId)
    {
        return [.. properties.Where(item => item.PropertyId != propertyId)];
    }

    private static FormRectangle GetFrame(FormComponent component)
    {
        return component.Properties
            .OfType<FormRectangleValue>()
            .Single(item => item.PropertyId == "frame").Value;
    }

    // Orders components parent-first with siblings by SiblingOrder, the order the resolver, XML
    // writer, and renderer use. A malformed hierarchy is returned unchanged for the resolver to report.
    private static FormComponent[] InPreorder(FormComponent[] components)
    {
        ILookup<string, FormComponent> children = components
            .Where(item => item.ParentComponentId is not null)
            .ToLookup(item => item.ParentComponentId!, StringComparer.Ordinal);
        List<FormComponent> ordered = new(components.Length);
        Stack<FormComponent> pending = new(components.Where(item => item.ParentComponentId is null).Reverse());
        while (pending.Count > 0 && ordered.Count <= components.Length)
        {
            FormComponent component = pending.Pop();
            ordered.Add(component);
            foreach (FormComponent child in children[component.ComponentId].OrderByDescending(item => item.SiblingOrder))
            {
                pending.Push(child);
            }
        }
        return ordered.Count == components.Length ? [.. ordered] : components;
    }

    private static FormComponent[] NormalizeSiblingOrder(FormComponent[] components)
    {
        Dictionary<string, int> next = new(StringComparer.Ordinal);
        FormComponent[] normalized = new FormComponent[components.Length];
        for (int index = 0; index < components.Length; ++index)
        {
            FormComponent component = components[index];
            string parentKey = component.ParentComponentId ?? string.Empty;
            next.TryGetValue(parentKey, out int order);
            normalized[index] = component with { SiblingOrder = order };
            next[parentKey] = order + 1;
        }
        return normalized;
    }

    private static FormBinding NewTextBinding(IReadOnlyList<FormBinding> bindings, int? capacityBytes)
    {
        for (int ordinal = 1; ; ++ordinal)
        {
            string suffix = ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string id = "binding.text." + suffix;
            string name = "text" + suffix;
            string symbol = "Text" + suffix;
            if (bindings.All(item => item.Id != id && item.RuntimeName != name && item.Symbol != symbol))
            {
                return new FormBinding
                {
                    Id = id, RuntimeName = name, Symbol = symbol,
                    Kind = FormBindingKind.MutableText, CapacityBytes = capacityBytes
                };
            }
        }
    }

    private static string ToCamelCase(string value)
    {
        string[] words = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "component";
        }
        return words[0] + string.Concat(words.Skip(1).Select(word =>
            char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static FormEditResult Failed(string code, string path, string message)
    {
        return new FormEditResult
        {
            Changed = false,
            Diagnostics =
            [
                new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message)
            ]
        };
    }

    private static FormEditResult Unchanged()
    {
        return new FormEditResult { Changed = false, Diagnostics = [] };
    }

    private static string FormatDiagnostics(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return string.Join(
            "; ",
            diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));
    }
}
