// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Documents;

public static class FormXmlImporter
{
    private const int MaximumInputBytes = 1024 * 1024;

    public static FormDocumentLoadResult LoadFile(
        string path,
        WidgetCatalog catalog,
        FormImportMode mode)
    {
        try
        {
            FileInfo file = new(path);
            if (file.Length > MaximumInputBytes)
            {
                return Failed(
                    "LVFX0001",
                    path,
                    $"Form XML is {file.Length} bytes; the maximum is {MaximumInputBytes}.");
            }
            return Load(File.ReadAllBytes(path), catalog, mode);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failed("LVFX0002", path, exception.Message);
        }
    }

    public static FormDocumentLoadResult Load(
        ReadOnlyMemory<byte> utf8,
        WidgetCatalog catalog,
        FormImportMode mode)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (utf8.Length is 0 or > MaximumInputBytes)
        {
            return Failed(
                "LVFX0001",
                "$",
                $"Form XML must contain 1 through {MaximumInputBytes} bytes.");
        }

        List<ProfileDiagnostic> diagnostics = [];
        XDocument xml;
        try
        {
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumInputBytes,
                IgnoreWhitespace = true,
                IgnoreComments = false,
                CloseInput = true
            };
            using MemoryStream stream = new(utf8.ToArray(), writable: false);
            using XmlReader reader = XmlReader.Create(stream, settings);
            xml = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (Exception exception) when (
            exception is XmlException or InvalidOperationException or DecoderFallbackException)
        {
            return Failed("LVFX0003", "$", exception.Message);
        }

        if (xml.Root is null)
        {
            return Failed("LVFX0004", "$", "The form XML has no root element.");
        }

        string sourceHash = Convert.ToHexString(SHA256.HashData(utf8.Span)).ToLowerInvariant();
        ImportState state = new(catalog, mode, diagnostics, sourceHash);
        state.ReadTopLevelMetadata(xml);
        state.ReadComponent(xml.Root, parentId: null, siblingOrder: 0);
        state.ValidateTopLevelNodes(xml);

        FormDocument document = state.BuildDocument();
        FormResolutionResult resolution = FormDocumentResolver.Resolve(
            document,
            catalog,
            allowHistoricalAttributes: mode == FormImportMode.Compatibility);
        diagnostics.AddRange(resolution.Diagnostics);
        if (HasErrors(diagnostics) || resolution.Document is null)
        {
            return new FormDocumentLoadResult { Diagnostics = diagnostics };
        }

        FormXmlWriteResult canonical = FormXmlWriter.Write(
            resolution.Document,
            includeHistoricalAttributes: mode == FormImportMode.Compatibility);
        diagnostics.AddRange(canonical.Diagnostics);
        if (!canonical.IsValid)
        {
            return new FormDocumentLoadResult { Diagnostics = diagnostics };
        }

        return new FormDocumentLoadResult
        {
            Document = document,
            CanonicalXmlUtf8 = canonical.Utf8,
            Sha256 = canonical.Sha256,
            Diagnostics = diagnostics
        };
    }

    private static FormDocumentLoadResult Failed(string code, string path, string message)
    {
        return new FormDocumentLoadResult
        {
            Diagnostics =
            [
                new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message)
            ]
        };
    }

    private static bool HasErrors(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        return diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error);
    }

    private sealed class ImportState(
        WidgetCatalog catalog,
        FormImportMode mode,
        List<ProfileDiagnostic> diagnostics,
        string sourceHash)
    {
        private readonly WidgetCatalog catalog = catalog;
        private readonly FormImportMode mode = mode;
        private readonly List<ProfileDiagnostic> diagnostics = diagnostics;
        private readonly string sourceHash = sourceHash;
        private readonly Dictionary<string, WidgetPropertyDefinition> properties = catalog.Properties.ToDictionary(item => item.Id, StringComparer.Ordinal);
        private readonly List<FormComponent> components = [];
        private readonly List<FormAction> actions = [];
        private readonly List<FormBinding> bindings = [];
        private readonly List<FormResource> resources = [];
        private readonly List<FormThemeColor> themes = [];
        private readonly Dictionary<(string ComponentId, string PropertyId), string> themeReferences = [];
        private readonly HashSet<(string ComponentId, string PropertyId)> consumedThemeReferences = [];
        private readonly Dictionary<string, int> widgetNameCounts = new(StringComparer.Ordinal);
        private string formId = string.Empty;
        private string displayName = string.Empty;
        private string? surfaceProfileId;
        private int schemaVersion = 1;
        private bool hasFormMetadata;

        public void ReadTopLevelMetadata(XDocument document)
        {
            foreach (XComment comment in document.Nodes().OfType<XComment>())
            {
                if (!FormXmlMetadata.TryParse(comment, out string kind, out XElement metadata))
                {
                    if (comment.Value.TrimStart().StartsWith("litheview-", StringComparison.Ordinal))
                    {
                        AddError("LVFX0101", Path(comment), "Malformed LitheView metadata comment.");
                    }
                    continue;
                }

                switch (kind)
                {
                    case FormXmlMetadata.FormKind:
                        ReadFormMetadata(metadata, comment);
                        break;
                    case FormXmlMetadata.ActionKind:
                        ReadActionMetadata(metadata, comment);
                        break;
                    case FormXmlMetadata.BindingKind:
                        ReadBindingMetadata(metadata, comment);
                        break;
                    case FormXmlMetadata.ResourceKind:
                        ReadResourceMetadata(metadata, comment);
                        break;
                    case FormXmlMetadata.ThemeColorKind:
                        ReadThemeMetadata(metadata, comment);
                        break;
                    case FormXmlMetadata.ThemeReferenceKind:
                        ReadThemeReferenceMetadata(metadata, comment);
                        break;
                    case FormXmlMetadata.ComponentKind:
                        break;
                    default:
                        AddError("LVFX0102", Path(comment), $"Unknown metadata kind '{kind}'.");
                        break;
                }
            }

            if (!hasFormMetadata)
            {
                formId = $"form.imported.{sourceHash[..12]}";
                displayName = "Imported Form";
                AddCompatibilityDiagnostic(
                    "LVFX0103",
                    "$",
                    "The form metadata was missing; a deterministic document identity was created.");
            }
        }

        public void ReadComponent(XElement element, string? parentId, int siblingOrder)
        {
            if (element.Name.Namespace != XNamespace.None)
            {
                AddError("LVFX0201", Path(element), "XML namespaces are not supported by the layout dialect.");
                return;
            }

            WidgetDefinition? widget = ResolveWidget(element);
            if (widget is null)
            {
                return;
            }

            (string componentId, string name) = ReadComponentMetadata(element, widget);
            List<FormPropertyValue> values = [];
            List<FormExtensionProperty> historical = [];
            HashSet<string> seenProperties = new(StringComparer.Ordinal);
            Dictionary<string, SortedDictionary<int, string>> sequences = new(StringComparer.Ordinal);

            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration || attribute.Name.Namespace != XNamespace.None)
                {
                    AddError("LVFX0202", Path(attribute), "Namespaced attributes are unsupported.");
                    continue;
                }
                string nameText = attribute.Name.LocalName;
                if (element.Name.LocalName == "object" && nameText == "className")
                {
                    continue;
                }

                if (!TryResolveProperty(widget, nameText, out WidgetPropertyDefinition? definition, out int? sequence))
                {
                    if (mode == FormImportMode.Strict)
                    {
                        AddError("LVFX0203", Path(attribute), $"Attribute '{nameText}' is unsupported.");
                    }
                    else
                    {
                        historical.Add(new FormExtensionProperty { Name = nameText, Value = attribute.Value });
                        AddWarning(
                            "LVFX0204",
                            Path(attribute),
                            $"Historical attribute '{nameText}' was preserved for compatibility.");
                    }
                    continue;
                }

                if (sequence.HasValue)
                {
                    if (!sequences.TryGetValue(definition.Id, out SortedDictionary<int, string>? entries))
                    {
                        entries = [];
                        sequences.Add(definition.Id, entries);
                    }
                    if (!entries.TryAdd(sequence.Value, attribute.Value))
                    {
                        AddError("LVFX0205", Path(attribute), "Animation frame indexes must be unique.");
                    }
                    continue;
                }

                if (!seenProperties.Add(definition.Id))
                {
                    AddError(
                        "LVFX0206",
                        Path(attribute),
                        $"Property '{definition.Id}' was supplied through more than one XML name.");
                    continue;
                }
                FormPropertyValue? parsed = ParseProperty(definition, attribute.Value, attribute);
                if (parsed is FormColorValue color &&
                    themeReferences.TryGetValue((componentId, definition.Id), out string? tokenId))
                {
                    FormThemeColor? token = themes.SingleOrDefault(item => item.Id == tokenId);
                    if (token is null || color.EncodedColor != token.EncodedColor)
                    {
                        AddError(
                            "LVFX0222",
                            Path(attribute),
                            "Theme reference metadata must identify a declared token with the same concrete color.");
                    }
                    else
                    {
                        parsed = new FormColorValue(definition.Id, tokenId);
                        consumedThemeReferences.Add((componentId, definition.Id));
                    }
                }
                if (parsed is not null)
                {
                    values.Add(parsed);
                }
            }

            foreach ((string propertyId, SortedDictionary<int, string> entries) in sequences)
            {
                if (!seenProperties.Add(propertyId) ||
                    entries.Count == 0 ||
                    entries.Keys.Where((value, index) => value != index).Any())
                {
                    AddError(
                        "LVFX0207",
                        Path(element),
                        $"Animation property '{propertyId}' must be consecutive from frame zero.");
                    continue;
                }
                string[] references = [.. entries.Values.Select(value => ResolveResource(FormResourceKind.Bitmap, value, Path(element)).Id)];
                values.Add(new FormReferenceListValue(
                    propertyId,
                    FormReferenceKind.Bitmap,
                    references));
            }

            components.Add(new FormComponent
            {
                ComponentId = componentId,
                Name = name,
                WidgetId = widget.Id,
                ParentComponentId = parentId,
                SiblingOrder = siblingOrder,
                Properties = [.. values],
                HistoricalAttributes = [.. historical]
            });

            int childOrder = 0;
            foreach (XNode node in element.Nodes())
            {
                if (node is XElement child)
                {
                    ReadComponent(child, componentId, childOrder++);
                }
                else if (node is XText text && !string.IsNullOrWhiteSpace(text.Value))
                {
                    AddError("LVFX0208", Path(text), "Visible text must be stored in widget attributes.");
                }
                else if (node is XComment comment &&
                         comment != element.Elements().FirstOrDefault()?.PreviousNode &&
                         FormXmlMetadata.TryParse(comment, out string kind, out _) &&
                         kind != FormXmlMetadata.ComponentKind)
                {
                    AddError("LVFX0209", Path(comment), "Metadata declarations are only valid before the root.");
                }
            }
        }

        public void ValidateTopLevelNodes(XDocument document)
        {
            foreach ((string componentId, string propertyId) in themeReferences.Keys)
            {
                if (!consumedThemeReferences.Contains((componentId, propertyId)))
                {
                    AddError(
                        "LVFX0406",
                        "$",
                        $"Theme reference for '{componentId}.{propertyId}' does not bind a color property.");
                }
            }
            foreach (XNode node in document.Nodes())
            {
                if (node is XText text && !string.IsNullOrWhiteSpace(text.Value))
                {
                    AddError("LVFX0301", Path(text), "Text outside the form root is not permitted.");
                }
                else if (node is XProcessingInstruction instruction &&
                         instruction.Target != "xml")
                {
                    AddCompatibilityDiagnostic(
                        "LVFX0302",
                        Path(instruction),
                        $"Processing instruction '{instruction.Target}' is not retained.");
                }
                else if (node is XComment comment &&
                         !FormXmlMetadata.TryParse(comment, out _, out _) &&
                         !string.IsNullOrWhiteSpace(comment.Value))
                {
                    AddCompatibilityDiagnostic(
                        "LVFX0303",
                        Path(comment),
                        "A non-metadata XML comment is not retained by normalization.");
                }
            }
        }

        public FormDocument BuildDocument()
        {
            return new FormDocument
            {
                SchemaVersion = schemaVersion,
                Id = formId,
                DisplayName = displayName,
                SurfaceProfileId = surfaceProfileId,
                Components = [.. components],
                Actions = [.. actions],
                Bindings = [.. bindings],
                Resources = [.. resources],
                ThemeColors = [.. themes]
            };
        }

        private WidgetDefinition? ResolveWidget(XElement element)
        {
            string elementName = element.Name.LocalName;
            IEnumerable<WidgetDefinition> candidates;
            if (elementName == "object")
            {
                string? className = (string?)element.Attribute("className");
                if (className is null)
                {
                    AddError("LVFX0210", Path(element), "Legacy object elements require className.");
                    return null;
                }
                candidates = catalog.Widgets.Where(widget =>
                    widget.Xml.LegacyClass == className ||
                    widget.Xml.LegacyClassAliases.Contains(className, StringComparer.Ordinal));
            }
            else
            {
                candidates = catalog.Widgets.Where(widget =>
                    widget.Xml.Element == elementName ||
                    widget.Xml.ElementAliases.Contains(elementName, StringComparer.Ordinal));
            }

            WidgetDefinition[] matches = [.. candidates];
            if (matches.Length == 1)
            {
                return matches[0];
            }
            if (matches.Length > 1)
            {
                WidgetDefinition? discriminated = matches.FirstOrDefault(widget =>
                    widget.Xml.DiscriminatorPropertyId is not null &&
                    HasPropertyAttribute(element, properties[widget.Xml.DiscriminatorPropertyId]));
                discriminated ??= matches.FirstOrDefault(widget => widget.Xml.DiscriminatorPropertyId is null);
                if (discriminated is not null)
                {
                    return discriminated;
                }
            }

            AddError(
                "LVFX0211",
                Path(element),
                $"Element '{elementName}' does not identify exactly one supported widget.");
            return null;
        }

        private (string Id, string Name) ReadComponentMetadata(
            XElement element,
            WidgetDefinition widget)
        {
            if (element.PreviousNode is XComment comment &&
                FormXmlMetadata.TryParse(comment, out string kind, out XElement metadata) &&
                kind == FormXmlMetadata.ComponentKind)
            {
                ValidateMetadataAttributes(kind, metadata, comment);
                return (
                    FormXmlMetadata.Required(metadata, "id"),
                    FormXmlMetadata.Required(metadata, "name"));
            }

            int count = widgetNameCounts.TryGetValue(widget.RuntimeKind, out int previous)
                ? previous + 1
                : 1;
            widgetNameCounts[widget.RuntimeKind] = count;
            AddCompatibilityDiagnostic(
                "LVFX0212",
                Path(element),
                "Component metadata was missing; deterministic identity and name values were created.");
            return ($"component.{components.Count:D4}", $"{widget.RuntimeKind}{count}");
        }

        private bool TryResolveProperty(
            WidgetDefinition widget,
            string xmlName,
            out WidgetPropertyDefinition definition,
            out int? sequence)
        {
            foreach (string propertyId in widget.PropertyIds)
            {
                WidgetPropertyDefinition candidate = properties[propertyId];
                if (candidate.XmlTransform == WidgetXmlTransform.ConsecutiveSequence)
                {
                    string prefix = candidate.XmlName[..^1];
                    if (xmlName.StartsWith(prefix, StringComparison.Ordinal) &&
                        int.TryParse(
                            xmlName.AsSpan(prefix.Length),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int index))
                    {
                        definition = candidate;
                        sequence = index;
                        return true;
                    }
                }
                else if (candidate.XmlName == xmlName ||
                         candidate.XmlAliases.Contains(xmlName, StringComparer.Ordinal))
                {
                    definition = candidate;
                    sequence = null;
                    return true;
                }
            }
            definition = null!;
            sequence = null;
            return false;
        }

        private FormPropertyValue? ParseProperty(
            WidgetPropertyDefinition definition,
            string text,
            XAttribute attribute)
        {
            if (definition.XmlTransform == WidgetXmlTransform.InvertBoolean)
            {
                return TryBoolean(text, out bool inverted)
                    ? new FormBooleanValue(definition.Id, !inverted)
                    : Invalid(definition, attribute);
            }
            if (definition.XmlTransform == WidgetXmlTransform.TriStateBoolean)
            {
                return TryBoolean(text, out bool focusable)
                    ? new FormTextValue(definition.Id, focusable ? "true" : "false")
                    : Invalid(definition, attribute);
            }

            return definition.Kind switch
            {
                WidgetPropertyKind.Boolean => TryBoolean(text, out bool boolean)
                    ? new FormBooleanValue(definition.Id, boolean)
                    : Invalid(definition, attribute),
                WidgetPropertyKind.SignedInteger => TrySigned(text, out long signed)
                    ? new FormSignedIntegerValue(definition.Id, signed)
                    : Invalid(definition, attribute),
                WidgetPropertyKind.UnsignedInteger or WidgetPropertyKind.Duration =>
                    TryUnsigned(text, out ulong unsigned)
                        ? new FormUnsignedIntegerValue(definition.Id, unsigned)
                        : Invalid(definition, attribute),
                WidgetPropertyKind.ActionId => ParseAction(definition, text, attribute),
                WidgetPropertyKind.BindingId => ParseBinding(definition, text, attribute),
                WidgetPropertyKind.Font => ParseResource(
                    definition,
                    FormReferenceKind.Font,
                    FormResourceKind.Font,
                    text,
                    attribute),
                WidgetPropertyKind.Bitmap => ParseResource(
                    definition,
                    FormReferenceKind.Bitmap,
                    FormResourceKind.Bitmap,
                    text,
                    attribute),
                WidgetPropertyKind.Color => TryUnsigned(text, out ulong color) && color <= uint.MaxValue
                    ? new FormColorValue(definition.Id, (uint)color)
                    : Invalid(definition, attribute),
                WidgetPropertyKind.Rectangle => TryRectangle(text, out FormRectangle rectangle)
                    ? new FormRectangleValue(definition.Id, rectangle)
                    : Invalid(definition, attribute),
                WidgetPropertyKind.Enum or WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText =>
                    new FormTextValue(definition.Id, text),
                _ => Invalid(definition, attribute)
            };
        }

        private FormPropertyValue? ParseAction(
            WidgetPropertyDefinition definition,
            string text,
            XAttribute attribute)
        {
            if (!TryUnsigned(text, out ulong value) || value > ushort.MaxValue)
            {
                return Invalid(definition, attribute);
            }
            if (value == 0U)
            {
                return null;
            }

            ushort runtimeId = (ushort)value;
            FormAction? action = actions.SingleOrDefault(item => item.RuntimeId == runtimeId);
            if (action is null)
            {
                action = new FormAction
                {
                    Id = $"action.runtime-{runtimeId}",
                    RuntimeId = runtimeId,
                    Symbol = $"Action{runtimeId}"
                };
                actions.Add(action);
                AddCompatibilityDiagnostic(
                    "LVFX0220",
                    Path(attribute),
                    $"Action metadata for runtime ID {runtimeId} was synthesized.");
            }
            return new FormReferenceValue(definition.Id, FormReferenceKind.Action, action.Id);
        }

        private FormReferenceValue ParseBinding(
            WidgetPropertyDefinition definition,
            string text,
            XAttribute attribute)
        {
            FormBinding? binding = bindings.SingleOrDefault(item => item.RuntimeName == text);
            if (binding is null)
            {
                string id = StableReferenceId("binding", text);
                binding = new FormBinding
                {
                    Id = id,
                    RuntimeName = text,
                    Symbol = Symbol("Binding", text, id),
                    Kind = FormBindingKind.MutableText,
                    CapacityBytes = null
                };
                bindings.Add(binding);
                AddCompatibilityDiagnostic(
                    "LVFX0221",
                    Path(attribute),
                    $"Binding metadata for '{text}' was synthesized.");
            }
            return new FormReferenceValue(definition.Id, FormReferenceKind.Binding, binding.Id);
        }

        private FormReferenceValue ParseResource(
            WidgetPropertyDefinition definition,
            FormReferenceKind referenceKind,
            FormResourceKind resourceKind,
            string text,
            XAttribute attribute)
        {
            FormResource resource = ResolveResource(resourceKind, text, Path(attribute));
            return new FormReferenceValue(definition.Id, referenceKind, resource.Id);
        }

        private FormResource ResolveResource(FormResourceKind kind, string runtimeName, string path)
        {
            FormResource? resource = resources.SingleOrDefault(item =>
                item.Kind == kind && item.RuntimeName == runtimeName);
            if (resource is not null)
            {
                return resource;
            }

            string id = StableReferenceId($"resource.{ToCamelCase(kind)}", runtimeName);
            resource = new FormResource
            {
                Id = id,
                RuntimeName = runtimeName,
                Symbol = Symbol(kind.ToString(), runtimeName, id),
                Kind = kind
            };
            resources.Add(resource);
            AddCompatibilityDiagnostic(
                "LVFX0222",
                path,
                $"Resource metadata for '{runtimeName}' was synthesized.");
            return resource;
        }

        private void ReadFormMetadata(XElement metadata, XComment source)
        {
            ValidateMetadataAttributes(FormXmlMetadata.FormKind, metadata, source);
            if (hasFormMetadata)
            {
                AddError("LVFX0401", Path(source), "Form metadata may appear only once.");
                return;
            }
            hasFormMetadata = true;
            schemaVersion = FormXmlMetadata.OptionalInt32(metadata, "schemaVersion") ?? 0;
            formId = FormXmlMetadata.Required(metadata, "id");
            displayName = FormXmlMetadata.Required(metadata, "displayName");
            surfaceProfileId = (string?)metadata.Attribute("surfaceProfileId");
        }

        private void ReadActionMetadata(XElement metadata, XComment source)
        {
            ValidateMetadataAttributes(FormXmlMetadata.ActionKind, metadata, source);
            int? runtimeId = FormXmlMetadata.OptionalInt32(metadata, "runtimeId");
            if (!runtimeId.HasValue || runtimeId.Value is <= 0 or > ushort.MaxValue)
            {
                AddError("LVFX0402", Path(source), "Action runtimeId must fit a nonzero 16-bit value.");
                return;
            }
            actions.Add(new FormAction
            {
                Id = FormXmlMetadata.Required(metadata, "id"),
                RuntimeId = (ushort)runtimeId.Value,
                Symbol = FormXmlMetadata.Required(metadata, "symbol")
            });
        }

        private void ReadBindingMetadata(XElement metadata, XComment source)
        {
            ValidateMetadataAttributes(FormXmlMetadata.BindingKind, metadata, source);
            if (!TryEnum(metadata, "kind", out FormBindingKind kind))
            {
                AddError("LVFX0403", Path(source), "Binding metadata has an unsupported kind.");
                return;
            }
            bindings.Add(new FormBinding
            {
                Id = FormXmlMetadata.Required(metadata, "id"),
                RuntimeName = FormXmlMetadata.Required(metadata, "runtimeName"),
                Symbol = FormXmlMetadata.Required(metadata, "symbol"),
                Kind = kind,
                CapacityBytes = FormXmlMetadata.OptionalInt32(metadata, "capacityBytes")
            });
        }

        private void ReadResourceMetadata(XElement metadata, XComment source)
        {
            ValidateMetadataAttributes(FormXmlMetadata.ResourceKind, metadata, source);
            if (!TryEnum(metadata, "kind", out FormResourceKind kind))
            {
                AddError("LVFX0404", Path(source), "Resource metadata has an unsupported kind.");
                return;
            }
            resources.Add(new FormResource
            {
                Id = FormXmlMetadata.Required(metadata, "id"),
                RuntimeName = FormXmlMetadata.Required(metadata, "runtimeName"),
                Symbol = FormXmlMetadata.Required(metadata, "symbol"),
                Kind = kind
            });
        }

        private void ReadThemeMetadata(XElement metadata, XComment source)
        {
            ValidateMetadataAttributes(FormXmlMetadata.ThemeColorKind, metadata, source);
            uint? encodedColor = FormXmlMetadata.OptionalHex32(metadata, "encodedColor");
            if (!encodedColor.HasValue)
            {
                AddError("LVFX0405", Path(source), "Theme color metadata requires an 8-digit hex value.");
                return;
            }
            themes.Add(new FormThemeColor
            {
                Id = FormXmlMetadata.Required(metadata, "id"),
                EncodedColor = encodedColor.Value
            });
        }

        private void ReadThemeReferenceMetadata(XElement metadata, XComment source)
        {
            ValidateMetadataAttributes(FormXmlMetadata.ThemeReferenceKind, metadata, source);
            string componentId = FormXmlMetadata.Required(metadata, "componentId");
            string propertyId = FormXmlMetadata.Required(metadata, "propertyId");
            string tokenId = FormXmlMetadata.Required(metadata, "tokenId");
            if (!themeReferences.TryAdd((componentId, propertyId), tokenId))
            {
                AddError("LVFX0406", Path(source), "Theme reference metadata must be unique.");
            }
        }

        private void ValidateMetadataAttributes(
            string kind,
            XElement metadata,
            XComment source)
        {
            string[] allowed = kind switch
            {
                FormXmlMetadata.FormKind =>
                ["schemaVersion", "id", "displayName", "surfaceProfileId"],
                FormXmlMetadata.ActionKind => ["id", "runtimeId", "symbol"],
                FormXmlMetadata.BindingKind =>
                ["id", "runtimeName", "symbol", "kind", "capacityBytes"],
                FormXmlMetadata.ResourceKind =>
                ["id", "runtimeName", "symbol", "kind"],
                FormXmlMetadata.ThemeColorKind => ["id", "encodedColor"],
                FormXmlMetadata.ThemeReferenceKind => ["componentId", "propertyId", "tokenId"],
                FormXmlMetadata.ComponentKind => ["id", "name"],
                _ => []
            };
            foreach (XAttribute attribute in metadata.Attributes())
            {
                if (allowed.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                {
                    continue;
                }

                string message =
                    $"Metadata '{kind}' contains unsupported attribute '{attribute.Name.LocalName}'.";
                if (mode == FormImportMode.Strict)
                {
                    AddError("LVFX0104", Path(source), message);
                }
                else
                {
                    AddWarning("LVFX0105", Path(source), $"{message} It is not retained by normalization.");
                }
            }
        }

        private void AddCompatibilityDiagnostic(string code, string path, string message)
        {
            if (mode == FormImportMode.Strict)
            {
                AddError(code, path, message);
            }
            else
            {
                AddWarning(code, path, message);
            }
        }

        private void AddError(string code, string path, string message)
        {
            diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
        }

        private void AddWarning(string code, string path, string message)
        {
            diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Warning, path, message));
        }

        private FormPropertyValue? Invalid(
            WidgetPropertyDefinition definition,
            XAttribute attribute)
        {
            AddError(
                "LVFX0501",
                Path(attribute),
                $"Value '{attribute.Value}' is invalid for property '{definition.Id}'.");
            return null;
        }

        private static bool HasPropertyAttribute(
            XElement element,
            WidgetPropertyDefinition definition)
        {
            if (definition.XmlTransform == WidgetXmlTransform.ConsecutiveSequence)
            {
                string prefix = definition.XmlName[..^1];
                return element.Attributes().Any(attribute =>
                    attribute.Name.Namespace == XNamespace.None &&
                    attribute.Name.LocalName.StartsWith(prefix, StringComparison.Ordinal));
            }
            return element.Attributes().Any(attribute =>
                attribute.Name.Namespace == XNamespace.None &&
                (attribute.Name.LocalName == definition.XmlName ||
                 definition.XmlAliases.Contains(attribute.Name.LocalName, StringComparer.Ordinal)));
        }

        private static bool TryBoolean(string text, out bool value)
        {
            if (text is "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }
            if (text is "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }
            value = false;
            return false;
        }

        private static bool TrySigned(string text, out long value)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return long.TryParse(
                    text.AsSpan(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out value);
            }
            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryUnsigned(string text, out ulong value)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(
                    text.AsSpan(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out value);
            }
            return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryRectangle(string text, out FormRectangle value)
        {
            string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 4 &&
                parts.All(item => int.TryParse(
                    item,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out _)))
            {
                value = new FormRectangle(
                    int.Parse(parts[0], CultureInfo.InvariantCulture),
                    int.Parse(parts[1], CultureInfo.InvariantCulture),
                    int.Parse(parts[2], CultureInfo.InvariantCulture),
                    int.Parse(parts[3], CultureInfo.InvariantCulture));
                return true;
            }
            value = default;
            return false;
        }

        private static bool TryEnum<TEnum>(XElement metadata, string name, out TEnum value)
            where TEnum : struct, Enum
        {
            return Enum.TryParse((string?)metadata.Attribute(name), ignoreCase: true, out value) &&
                   Enum.IsDefined(value);
        }

        private static string StableReferenceId(string prefix, string runtimeName)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{prefix}\n{runtimeName}"));
            return $"{prefix}.{Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant()}";
        }

        private static string Symbol(string prefix, string runtimeName, string id)
        {
            string stem = FormSymbolRules.ToCppIdentifier($"{prefix} {runtimeName}");
            return stem + id[^4..].ToUpperInvariant();
        }

        private static string ToCamelCase<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            string name = value.ToString();
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        private static string Path(XObject value)
        {
            if (value is IXmlLineInfo line && line.HasLineInfo())
            {
                return $"line {line.LineNumber}, column {line.LinePosition}";
            }
            return "$";
        }
    }
}
