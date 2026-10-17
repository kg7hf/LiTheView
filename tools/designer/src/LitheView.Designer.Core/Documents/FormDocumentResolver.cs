// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Documents;

public static class FormDocumentResolver
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumComponents = 512;

    public static FormResolutionResult Resolve(
        FormDocument document,
        WidgetCatalog catalog,
        bool allowHistoricalAttributes = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(catalog);

        List<ProfileDiagnostic> diagnostics = [];
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            AddError(
                diagnostics,
                "LVFD1001",
                "$.schemaVersion",
                $"Only form schema version {CurrentSchemaVersion} is supported.");
        }
        ValidateIdentity(document.Id, document.DisplayName, "$", diagnostics);
        if (document.Components.Length is 0 or > MaximumComponents)
        {
            AddError(
                diagnostics,
                "LVFD1002",
                "$.components",
                $"A form requires 1 through {MaximumComponents} components.");
        }

        Dictionary<string, WidgetDefinition> widgets = catalog.Widgets.ToDictionary(
            item => item.Id,
            StringComparer.Ordinal);
        Dictionary<string, WidgetPropertyDefinition> properties = catalog.Properties.ToDictionary(
            item => item.Id,
            StringComparer.Ordinal);
        Dictionary<string, FormAction> actions = ValidateActions(document.Actions, diagnostics);
        Dictionary<string, FormBinding> bindings = ValidateBindings(document.Bindings, diagnostics);
        Dictionary<string, FormResource> resources = ValidateResources(document.Resources, diagnostics);
        Dictionary<string, FormThemeColor> themes = ValidateThemes(document.ThemeColors, diagnostics);

        List<ResolvedFormComponent> resolvedComponents = [];
        Dictionary<string, (FormComponent Component, WidgetDefinition Widget, int Index)> components =
            new(StringComparer.Ordinal);
        HashSet<string> names = new(StringComparer.Ordinal);
        HashSet<string> symbols = new(StringComparer.Ordinal);
        HashSet<ushort> runtimeIds = [];
        int rootCount = 0;

        for (int index = 0; index < document.Components.Length; ++index)
        {
            FormComponent component = document.Components[index];
            string path = $"$.components[{index}]";
            ValidateIdentity(component.ComponentId, component.Name, path, diagnostics);
            if (!components.TryAdd(component.ComponentId, (component, null!, index)))
            {
                AddError(
                    diagnostics,
                    "LVFD1101",
                    $"{path}.componentId",
                    $"Component ID '{component.ComponentId}' is duplicated.");
            }
            if (!names.Add(component.Name))
            {
                AddError(
                    diagnostics,
                    "LVFD1102",
                    $"{path}.name",
                    $"Component name '{component.Name}' is duplicated.");
            }

            string symbol = FormSymbolRules.ToCppIdentifier(component.Name);
            if (!symbols.Add(symbol))
            {
                AddError(
                    diagnostics,
                    "LVFD1103",
                    $"{path}.name",
                    $"Component name '{component.Name}' collides as C++ symbol '{symbol}'.");
            }

            if (!widgets.TryGetValue(component.WidgetId, out WidgetDefinition? widget))
            {
                AddError(
                    diagnostics,
                    "LVFD1104",
                    $"{path}.widgetId",
                    $"Widget '{component.WidgetId}' is not present in the loaded catalog.");
                continue;
            }
            components[component.ComponentId] = (component, widget, index);

            int? parentIndex = ValidateHierarchy(
                component,
                widget,
                index,
                components,
                ref rootCount,
                path,
                diagnostics);
            ResolvedFormProperty[] resolvedProperties = ValidateProperties(
                component,
                widget,
                properties,
                actions,
                bindings,
                resources,
                themes,
                runtimeIds,
                allowHistoricalAttributes,
                path,
                diagnostics);
            resolvedComponents.Add(new ResolvedFormComponent
            {
                Source = component,
                Widget = widget,
                Index = index,
                ParentIndex = parentIndex,
                CppSymbol = symbol,
                Properties = resolvedProperties
            });
        }

        ValidateSiblingOrder(document.Components, diagnostics);
        ValidateTabPages(resolvedComponents, diagnostics);
        if (rootCount != 1 ||
            document.Components.Length > 0 && document.Components[0].ParentComponentId is not null)
        {
            AddError(
                diagnostics,
                "LVFD1105",
                "$.components",
                "A form must contain exactly one root, and that root must be the first component.");
        }

        if (HasErrors(diagnostics))
        {
            return new FormResolutionResult { Diagnostics = diagnostics };
        }

        return new FormResolutionResult
        {
            Document = new ResolvedFormDocument
            {
                Source = document,
                CppSymbol = FormSymbolRules.ToCppIdentifier(document.DisplayName),
                Components = [.. resolvedComponents],
                Actions = actions,
                Bindings = bindings,
                Resources = resources,
                ThemeColors = themes
            },
            Diagnostics = diagnostics
        };
    }

    private static int? ValidateHierarchy(
        FormComponent component,
        WidgetDefinition widget,
        int index,
        Dictionary<string, (FormComponent Component, WidgetDefinition Widget, int Index)> components,
        ref int rootCount,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (component.ParentComponentId is null)
        {
            ++rootCount;
            if (!widget.ParentRule.RootAllowed)
            {
                AddError(
                    diagnostics,
                    "LVFD1110",
                    $"{path}.parentComponentId",
                    $"Widget '{widget.Id}' cannot be a form root.");
            }
            return null;
        }

        if (!components.TryGetValue(component.ParentComponentId, out var parent) ||
            parent.Index >= index ||
            parent.Widget is null)
        {
            AddError(
                diagnostics,
                "LVFD1111",
                $"{path}.parentComponentId",
                "A parent must identify an earlier component in preorder.");
            return null;
        }
        if (parent.Widget.ChildPolicy != WidgetChildPolicy.Many ||
            !widget.ParentRule.AllowedParentIds.Contains(parent.Widget.Id, StringComparer.Ordinal))
        {
            AddError(
                diagnostics,
                "LVFD1112",
                $"{path}.parentComponentId",
                $"Widget '{widget.Id}' is not permitted under '{parent.Widget.Id}'.");
        }
        return parent.Index;
    }

    private static ResolvedFormProperty[] ValidateProperties(
        FormComponent component,
        WidgetDefinition widget,
        Dictionary<string, WidgetPropertyDefinition> definitions,
        IReadOnlyDictionary<string, FormAction> actions,
        IReadOnlyDictionary<string, FormBinding> bindings,
        IReadOnlyDictionary<string, FormResource> resources,
        IReadOnlyDictionary<string, FormThemeColor> themes,
        HashSet<ushort> runtimeIds,
        bool allowHistoricalAttributes,
        string componentPath,
        List<ProfileDiagnostic> diagnostics)
    {
        List<ResolvedFormProperty> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        HashSet<string> allowed = widget.PropertyIds.ToHashSet(StringComparer.Ordinal);
        for (int index = 0; index < component.Properties.Length; ++index)
        {
            FormPropertyValue value = component.Properties[index];
            string path = $"{componentPath}.properties[{index}]";
            if (!seen.Add(value.PropertyId) ||
                !definitions.TryGetValue(value.PropertyId, out WidgetPropertyDefinition? definition) ||
                !allowed.Contains(value.PropertyId))
            {
                AddError(
                    diagnostics,
                    "LVFD1120",
                    path,
                    $"Property '{value.PropertyId}' is duplicated, unknown, or unsupported by '{widget.Id}'.");
                continue;
            }
            if (!ValidatePropertyValue(
                    value,
                    definition,
                    actions,
                    bindings,
                    resources,
                    themes,
                    diagnostics,
                    path))
            {
                continue;
            }

            if (value is FormUnsignedIntegerValue { PropertyId: "runtime-id" } runtimeId)
            {
                if (runtimeId.Value > ushort.MaxValue ||
                    runtimeId.Value != 0U && !runtimeIds.Add((ushort)runtimeId.Value))
                {
                    AddError(
                        diagnostics,
                        "LVFD1121",
                        path,
                        "Runtime IDs must fit 16 bits and be unique when nonzero.");
                }
            }
            result.Add(new ResolvedFormProperty { Descriptor = definition, Value = value });
        }

        foreach (string propertyId in widget.PropertyIds)
        {
            if (definitions[propertyId].Required && !seen.Contains(propertyId))
            {
                AddError(
                    diagnostics,
                    "LVFD1122",
                    componentPath,
                    $"Required property '{propertyId}' is missing.");
            }
        }

        ValidateRelatedProperties(result, widget, componentPath, diagnostics);

        if (!allowHistoricalAttributes && component.HistoricalAttributes.Length > 0)
        {
            AddError(
                diagnostics,
                "LVFD1123",
                $"{componentPath}.historicalAttributes",
                "Historical extension attributes are not permitted in strict output.");
        }
        HashSet<string> historicalNames = new(StringComparer.Ordinal);
        foreach (FormExtensionProperty attribute in component.HistoricalAttributes)
        {
            if (!IsXmlName(attribute.Name) || !historicalNames.Add(attribute.Name))
            {
                AddError(
                    diagnostics,
                    "LVFD1124",
                    $"{componentPath}.historicalAttributes",
                    "Historical attribute names must be valid and unique.");
            }
        }
        return [.. result.OrderBy(item => item.Descriptor.Id, StringComparer.Ordinal)];
    }

    private static bool ValidatePropertyValue(
        FormPropertyValue value,
        WidgetPropertyDefinition definition,
        IReadOnlyDictionary<string, FormAction> actions,
        IReadOnlyDictionary<string, FormBinding> bindings,
        IReadOnlyDictionary<string, FormResource> resources,
        IReadOnlyDictionary<string, FormThemeColor> themes,
        List<ProfileDiagnostic> diagnostics,
        string path)
    {
        bool valid = definition.Kind switch
        {
            WidgetPropertyKind.Boolean => value is FormBooleanValue,
            WidgetPropertyKind.SignedInteger =>
                value is FormSignedIntegerValue signed && InRange(signed.Value, definition),
            WidgetPropertyKind.UnsignedInteger or WidgetPropertyKind.Duration =>
                value is FormUnsignedIntegerValue unsigned && InRange(unsigned.Value, definition),
            WidgetPropertyKind.ActionId =>
                value is FormReferenceValue action &&
                action.ReferenceKind == FormReferenceKind.Action &&
                actions.ContainsKey(action.ReferenceId),
            WidgetPropertyKind.BindingId =>
                value is FormReferenceValue binding &&
                binding.ReferenceKind == FormReferenceKind.Binding &&
                bindings.ContainsKey(binding.ReferenceId),
            WidgetPropertyKind.Font =>
                value is FormReferenceValue font &&
                font.ReferenceKind == FormReferenceKind.Font &&
                ResourceMatches(resources, font.ReferenceId, FormResourceKind.Font),
            WidgetPropertyKind.Bitmap =>
                value is FormReferenceValue bitmap &&
                bitmap.ReferenceKind == FormReferenceKind.Bitmap &&
                ResourceMatches(resources, bitmap.ReferenceId, FormResourceKind.Bitmap),
            WidgetPropertyKind.AnimationFrames =>
                value is FormReferenceListValue frames &&
                frames.ReferenceKind == FormReferenceKind.Bitmap &&
                frames.ReferenceIds.Length > 0 &&
                frames.ReferenceIds.All(item => ResourceMatches(resources, item, FormResourceKind.Bitmap)),
            WidgetPropertyKind.Color =>
                value is FormColorValue color &&
                (color.EncodedColor.HasValue ^ color.ThemeTokenId is not null) &&
                color.EncodedColor != FormDocumentLimits.UnspecifiedStyleStateColor &&
                (color.ThemeTokenId is null || themes.ContainsKey(color.ThemeTokenId)),
            WidgetPropertyKind.Rectangle =>
                value is FormRectangleValue rectangle &&
                rectangle.Value.Width > 0 &&
                rectangle.Value.Height > 0,
            WidgetPropertyKind.Enum =>
                value is FormTextValue enumValue &&
                definition.EnumValues.Contains(enumValue.Value, StringComparer.Ordinal),
            WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText =>
                value is FormTextValue text && IsSafeText(text.Value),
            _ => false
        };
        if (!valid)
        {
            AddError(
                diagnostics,
                "LVFD1130",
                path,
                $"Property '{definition.Id}' has the wrong typed value, range, or reference.");
        }
        return valid;
    }

    private static void ValidateRelatedProperties(
        List<ResolvedFormProperty> properties,
        WidgetDefinition widget,
        string componentPath,
        List<ProfileDiagnostic> diagnostics)
    {
        ValidateButtonBehavior(properties, widget, componentPath, diagnostics);
        ValidateTabTitles(properties, widget, componentPath, diagnostics);
        ValidateWheelItems(properties, widget, componentPath, diagnostics);
        FormReferenceListValue? frames = properties
            .Select(item => item.Value)
            .OfType<FormReferenceListValue>()
            .SingleOrDefault(item => item.PropertyId == "animation-frames");
        FormUnsignedIntegerValue? repeat = properties
            .Select(item => item.Value)
            .OfType<FormUnsignedIntegerValue>()
            .SingleOrDefault(item => item.PropertyId == "repeat-index");
        if (frames is not null &&
            (frames.ReferenceIds.Length > ushort.MaxValue ||
             repeat is not null && repeat.Value >= (ulong)frames.ReferenceIds.Length))
        {
            AddError(
                diagnostics,
                "LVFD1131",
                componentPath,
                "Animation frame count must fit 16 bits and repeat-index must identify a frame.");
        }
    }

    private static void ValidateButtonBehavior(
        List<ResolvedFormProperty> properties,
        WidgetDefinition widget,
        string componentPath,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!widget.PropertyIds.Contains("button-behavior", StringComparer.Ordinal))
        {
            return;
        }
        bool isChecked = properties
            .Select(item => item.Value)
            .OfType<FormBooleanValue>()
            .Any(item => item.PropertyId == "checked" && item.Value);
        string behavior = properties
            .Select(item => item.Value)
            .OfType<FormTextValue>()
            .SingleOrDefault(item => item.PropertyId == "button-behavior")?.Value ?? "momentary";
        if (isChecked && behavior != "toggle")
        {
            AddError(
                diagnostics,
                "LVFD1132",
                componentPath,
                "Only a toggle button can be checked; select toggle behavior or clear checked.");
        }
    }

    private static void ValidateTabTitles(
        List<ResolvedFormProperty> properties,
        WidgetDefinition widget,
        string componentPath,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!FormTabRules.IsTabWidget(widget))
        {
            return;
        }
        FormPropertyValue[] values = [.. properties.Select(item => item.Value)];
        int count = FormTabRules.CountTitles(FormTabRules.Titles(values));
        if (count is 0 or > FormTabRules.MaximumTabs || FormTabRules.SelectedIndex(values) >= (ulong)count)
        {
            AddError(
                diagnostics,
                "LVFD1133",
                componentPath,
                $"Tabs need 1 through {FormTabRules.MaximumTabs} pipe-delimited titles in Items, " +
                "and Selected Index must name one of them.");
        }
    }

    private static void ValidateWheelItems(
        List<ResolvedFormProperty> properties,
        WidgetDefinition widget,
        string componentPath,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!FormWheelRules.IsWheelPicker(widget) && widget.RuntimeKind != "dropDown")
        {
            return;
        }
        FormPropertyValue[] values = [.. properties.Select(item => item.Value)];
        int count = FormWheelRules.CountItems(values);
        if (count is 0 or > FormWheelRules.MaximumItems || FormTabRules.SelectedIndex(values) >= (ulong)count)
        {
            AddError(
                diagnostics,
                "LVFD1136",
                componentPath,
                $"A wheel picker or drop-down needs 1 through {FormWheelRules.MaximumItems} pipe-delimited items in Items, " +
                "and Selected Index must name one of them.");
        }
    }

    // Page structure is advisory: a tab view is built up one page at a time, and the runtime
    // behaves deterministically (an extra tab shows no page; an extra page is never shown).
    private static void ValidateTabPages(
        List<ResolvedFormComponent> components,
        List<ProfileDiagnostic> diagnostics)
    {
        foreach (ResolvedFormComponent tabView in components.Where(item => FormTabRules.IsTabView(item.Widget)))
        {
            FormPropertyValue[] values = [.. tabView.Properties.Select(item => item.Value)];
            string path = $"$.components[{tabView.Index}]";
            ResolvedFormComponent[] pages = [.. components.Where(item => item.ParentIndex == tabView.Index)];
            int tabs = FormTabRules.CountTitles(FormTabRules.Titles(values));
            if (pages.Length != tabs)
            {
                AddWarning(
                    diagnostics,
                    "LVFD1134",
                    path,
                    $"Tab view '{tabView.Source.Name}' has {tabs} tab(s) but {pages.Length} page(s); " +
                    "each tab shows the direct child at its index.");
            }
            FormRectangle? frame = FrameOf(tabView);
            if (frame is null)
            {
                continue;
            }
            FormRectangle content = FormTabRules.ContentFrame(frame.Value, FormTabRules.TabHeight(values));
            foreach (ResolvedFormComponent page in pages)
            {
                FormRectangle? pageFrame = FrameOf(page);
                if (pageFrame is not null && !FormTabRules.Contains(content, pageFrame.Value))
                {
                    AddWarning(
                        diagnostics,
                        "LVFD1135",
                        $"$.components[{page.Index}]",
                        $"Page '{page.Source.Name}' should lie within its tab view's content area " +
                        $"({content.X},{content.Y},{content.Width},{content.Height}); it would cover the tab strip.");
                }
            }
        }
    }

    private static FormRectangle? FrameOf(ResolvedFormComponent component)
    {
        return component.Properties.Select(item => item.Value).OfType<FormRectangleValue>()
            .SingleOrDefault(item => item.PropertyId == "frame")?.Value;
    }

    private static bool IsSafeText(string value)
    {
        return value.Length <= ushort.MaxValue && value.All(character =>
            character is '\t' or '\n' or '\r' || !char.IsControl(character));
    }

    private static Dictionary<string, FormAction> ValidateActions(
        IEnumerable<FormAction> values,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, FormAction> result = new(StringComparer.Ordinal);
        HashSet<ushort> runtimeIds = [];
        HashSet<string> symbols = new(StringComparer.Ordinal);
        foreach (FormAction value in values)
        {
            if (!FormSymbolRules.IsStableId(value.Id) ||
                value.RuntimeId == 0U ||
                !result.TryAdd(value.Id, value) ||
                !runtimeIds.Add(value.RuntimeId) ||
                !symbols.Add(value.Symbol) ||
                FormSymbolRules.ToCppIdentifier(value.Symbol) != value.Symbol)
            {
                AddError(
                    diagnostics,
                    "LVFD1201",
                    "$.actions",
                    $"Action '{value.Id}' has an invalid or duplicate ID, runtime ID, or symbol.");
            }
        }
        return result;
    }

    private static Dictionary<string, FormBinding> ValidateBindings(
        IEnumerable<FormBinding> values,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, FormBinding> result = new(StringComparer.Ordinal);
        HashSet<string> runtimeNames = new(StringComparer.Ordinal);
        HashSet<string> symbols = new(StringComparer.Ordinal);
        foreach (FormBinding value in values)
        {
            if (!FormSymbolRules.IsStableId(value.Id) ||
                !FormSymbolRules.IsRuntimeName(value.RuntimeName) ||
                value.CapacityBytes is <= 0 or > ushort.MaxValue ||
                !result.TryAdd(value.Id, value) ||
                !runtimeNames.Add(value.RuntimeName) ||
                !symbols.Add(value.Symbol) ||
                FormSymbolRules.ToCppIdentifier(value.Symbol) != value.Symbol)
            {
                AddError(
                    diagnostics,
                    "LVFD1202",
                    "$.bindings",
                    $"Binding '{value.Id}' has invalid or duplicate metadata.");
            }
        }
        return result;
    }

    private static Dictionary<string, FormResource> ValidateResources(
        IEnumerable<FormResource> values,
        List<ProfileDiagnostic> diagnostics)
    {
        Dictionary<string, FormResource> result = new(StringComparer.Ordinal);
        HashSet<(FormResourceKind Kind, string Name)> runtimeNames = [];
        HashSet<string> symbols = new(StringComparer.Ordinal);
        foreach (FormResource value in values)
        {
            if (!FormSymbolRules.IsStableId(value.Id) ||
                !FormSymbolRules.IsRuntimeName(value.RuntimeName) ||
                !result.TryAdd(value.Id, value) ||
                !runtimeNames.Add((value.Kind, value.RuntimeName)) ||
                !symbols.Add(value.Symbol) ||
                FormSymbolRules.ToCppIdentifier(value.Symbol) != value.Symbol)
            {
                AddError(
                    diagnostics,
                    "LVFD1203",
                    "$.resources",
                    $"Resource '{value.Id}' has invalid or duplicate metadata.");
            }
        }
        return result;
    }

    private static Dictionary<string, FormThemeColor> ValidateThemes(
        IEnumerable<FormThemeColor> values,
        List<ProfileDiagnostic> diagnostics)
    {
        FormThemeColor[] bounded = [.. values];
        Dictionary<string, FormThemeColor> result = new(StringComparer.Ordinal);
        if (bounded.Length > FormDocumentLimits.MaximumThemeColors)
        {
            AddError(
                diagnostics,
                "LVFD1204",
                "$.themeColors",
                $"A form supports at most {FormDocumentLimits.MaximumThemeColors} theme color tokens.");
        }
        foreach (FormThemeColor value in bounded)
        {
            if (!value.Id.StartsWith("theme.", StringComparison.Ordinal) ||
                !FormSymbolRules.IsStableId(value.Id) ||
                value.EncodedColor == FormDocumentLimits.UnspecifiedStyleStateColor ||
                !result.TryAdd(value.Id, value))
            {
                AddError(
                    diagnostics,
                    "LVFD1204",
                    "$.themeColors",
                    $"Theme color '{value.Id}' must use a unique stable 'theme.' ID and a supported color value.");
            }
        }
        return result;
    }

    private static void ValidateSiblingOrder(
        IEnumerable<FormComponent> components,
        List<ProfileDiagnostic> diagnostics)
    {
        foreach (IGrouping<string?, FormComponent> siblings in components.GroupBy(
                     item => item.ParentComponentId,
                     StringComparer.Ordinal))
        {
            int expected = 0;
            foreach (FormComponent component in siblings.OrderBy(item => item.SiblingOrder))
            {
                if (component.SiblingOrder != expected)
                {
                    AddError(
                        diagnostics,
                        "LVFD1210",
                        "$.components",
                        "Sibling order values must be unique and consecutive from zero.");
                    break;
                }
                ++expected;
            }
        }
    }

    private static void ValidateIdentity(
        string id,
        string displayName,
        string path,
        List<ProfileDiagnostic> diagnostics)
    {
        if (!FormSymbolRules.IsStableId(id) ||
            string.IsNullOrWhiteSpace(displayName) ||
            displayName.Length > 128 ||
            !FormSymbolRules.IsRuntimeName(displayName))
        {
            AddError(
                diagnostics,
                "LVFD1010",
                path,
                "IDs must be stable ASCII identifiers and names must be bounded printable text.");
        }
    }

    private static bool InRange(long value, WidgetPropertyDefinition definition)
    {
        return (!definition.Minimum.HasValue || value >= definition.Minimum.Value) &&
               (!definition.Maximum.HasValue || value <= definition.Maximum.Value);
    }

    private static bool InRange(ulong value, WidgetPropertyDefinition definition)
    {
        return (!definition.Minimum.HasValue ||
                definition.Minimum.Value >= 0 && value >= (ulong)definition.Minimum.Value) &&
               (!definition.Maximum.HasValue ||
                definition.Maximum.Value >= 0 && value <= (ulong)definition.Maximum.Value);
    }

    private static bool ResourceMatches(
        IReadOnlyDictionary<string, FormResource> resources,
        string id,
        FormResourceKind kind)
    {
        return resources.TryGetValue(id, out FormResource? resource) && resource.Kind == kind;
    }

    private static bool IsXmlName(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsXmlNameStart(value[0]))
        {
            return false;
        }
        return value.Skip(1).All(character => IsXmlNameStart(character) || char.IsAsciiDigit(character));
    }

    private static bool IsXmlNameStart(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_' or '-';
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }

    private static void AddWarning(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Warning, path, message));
    }
}
