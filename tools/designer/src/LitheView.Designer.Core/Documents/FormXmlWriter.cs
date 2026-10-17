// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Core.Documents;

public static class FormXmlWriter
{
    public static FormXmlWriteResult Write(
        FormDocument document,
        WidgetCatalog catalog,
        bool includeHistoricalAttributes = false)
    {
        FormResolutionResult resolution = FormDocumentResolver.Resolve(
            document,
            catalog,
            includeHistoricalAttributes);
        if (!resolution.IsValid || resolution.Document is null)
        {
            return new FormXmlWriteResult { Diagnostics = resolution.Diagnostics };
        }
        return Write(resolution.Document, includeHistoricalAttributes);
    }

    public static FormXmlWriteResult Write(
        ResolvedFormDocument document,
        bool includeHistoricalAttributes = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<ProfileDiagnostic> diagnostics = [];
        try
        {
            using MemoryStream stream = new();
            XmlWriterSettings settings = new()
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.None,
                OmitXmlDeclaration = false,
                CloseOutput = false
            };
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteStartDocument();
                WriteMetadata(writer, document.Source);
                WriteComponent(writer, document, document.Components[0], includeHistoricalAttributes);
                writer.WriteEndDocument();
            }
            byte[] payload = stream.ToArray();
            byte[] utf8 = [.. payload, (byte)'\n'];
            return new FormXmlWriteResult
            {
                Utf8 = utf8,
                Sha256 = Convert.ToHexString(SHA256.HashData(utf8)).ToLowerInvariant(),
                Diagnostics = diagnostics
            };
        }
        catch (InvalidOperationException exception)
        {
            diagnostics.Add(new ProfileDiagnostic(
                "LVFX2001",
                ProfileDiagnosticSeverity.Error,
                "$",
                exception.Message));
            return new FormXmlWriteResult { Diagnostics = diagnostics };
        }
    }

    private static void WriteMetadata(XmlWriter writer, FormDocument document)
    {
        writer.WriteComment(FormXmlMetadata.Format(
            FormXmlMetadata.FormKind,
            ("schemaVersion", document.SchemaVersion.ToString(CultureInfo.InvariantCulture)),
            ("id", document.Id),
            ("displayName", document.DisplayName),
            ("surfaceProfileId", document.SurfaceProfileId)));
        foreach (FormAction action in document.Actions.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteComment(FormXmlMetadata.Format(
                FormXmlMetadata.ActionKind,
                ("id", action.Id),
                ("runtimeId", action.RuntimeId.ToString(CultureInfo.InvariantCulture)),
                ("symbol", action.Symbol)));
        }
        foreach (FormBinding binding in document.Bindings.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteComment(FormXmlMetadata.Format(
                FormXmlMetadata.BindingKind,
                ("id", binding.Id),
                ("runtimeName", binding.RuntimeName),
                ("symbol", binding.Symbol),
                ("kind", ToCamelCase(binding.Kind)),
                ("capacityBytes", binding.CapacityBytes?.ToString(CultureInfo.InvariantCulture))));
        }
        foreach (FormResource resource in document.Resources.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteComment(FormXmlMetadata.Format(
                FormXmlMetadata.ResourceKind,
                ("id", resource.Id),
                ("runtimeName", resource.RuntimeName),
                ("symbol", resource.Symbol),
                ("kind", ToCamelCase(resource.Kind))));
        }
        foreach (FormThemeColor theme in document.ThemeColors.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteComment(FormXmlMetadata.Format(
                FormXmlMetadata.ThemeColorKind,
                ("id", theme.Id),
                ("encodedColor", theme.EncodedColor.ToString("x8", CultureInfo.InvariantCulture))));
        }
        foreach (FormComponent component in document.Components.OrderBy(
                     item => item.ComponentId,
                     StringComparer.Ordinal))
        {
            foreach (FormColorValue color in component.Properties
                         .OfType<FormColorValue>()
                         .Where(item => item.ThemeTokenId is not null)
                         .OrderBy(item => item.PropertyId, StringComparer.Ordinal))
            {
                writer.WriteComment(FormXmlMetadata.Format(
                    FormXmlMetadata.ThemeReferenceKind,
                    ("componentId", component.ComponentId),
                    ("propertyId", color.PropertyId),
                    ("tokenId", color.ThemeTokenId)));
            }
        }
    }

    private static void WriteComponent(
        XmlWriter writer,
        ResolvedFormDocument document,
        ResolvedFormComponent component,
        bool includeHistoricalAttributes)
    {
        writer.WriteComment(FormXmlMetadata.Format(
            FormXmlMetadata.ComponentKind,
            ("id", component.Source.ComponentId),
            ("name", component.Source.Name)));
        writer.WriteStartElement(component.Widget.Xml.Element);

        Dictionary<string, ResolvedFormProperty> properties = component.Properties.ToDictionary(
            item => item.Descriptor.Id,
            StringComparer.Ordinal);
        foreach (string propertyId in component.Widget.PropertyIds)
        {
            if (properties.TryGetValue(propertyId, out ResolvedFormProperty? property))
            {
                WriteProperty(writer, document, property);
            }
        }
        if (includeHistoricalAttributes)
        {
            foreach (FormExtensionProperty attribute in component.Source.HistoricalAttributes
                         .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WriteAttributeString(attribute.Name, attribute.Value);
            }
        }

        foreach (ResolvedFormComponent child in document.Components
                     .Where(item => item.ParentIndex == component.Index)
                     .OrderBy(item => item.Source.SiblingOrder))
        {
            WriteComponent(writer, document, child, includeHistoricalAttributes);
        }
        writer.WriteEndElement();
    }

    private static void WriteProperty(
        XmlWriter writer,
        ResolvedFormDocument document,
        ResolvedFormProperty property)
    {
        WidgetPropertyDefinition descriptor = property.Descriptor;
        if (descriptor.XmlTransform == WidgetXmlTransform.ConsecutiveSequence)
        {
            FormReferenceListValue values = (FormReferenceListValue)property.Value;
            string prefix = descriptor.XmlName[..^1];
            for (int index = 0; index < values.ReferenceIds.Length; ++index)
            {
                writer.WriteAttributeString(
                    prefix + index.ToString(CultureInfo.InvariantCulture),
                    document.Resources[values.ReferenceIds[index]].RuntimeName);
            }
            return;
        }

        string? text = property.Value switch
        {
            FormBooleanValue boolean when descriptor.XmlTransform == WidgetXmlTransform.InvertBoolean =>
                ToBoolean(!boolean.Value),
            FormBooleanValue boolean => ToBoolean(boolean.Value),
            FormSignedIntegerValue signed => signed.Value.ToString(CultureInfo.InvariantCulture),
            FormUnsignedIntegerValue unsigned => unsigned.Value.ToString(CultureInfo.InvariantCulture),
            FormTextValue value when descriptor.XmlTransform == WidgetXmlTransform.TriStateBoolean =>
                value.Value switch
                {
                    "true" => "true",
                    "false" => "false",
                    _ => null
                },
            FormTextValue value => value.Value,
            FormRectangleValue rectangle => FormatRectangle(rectangle.Value),
            FormColorValue color =>
                "0x" + ResolveColor(document, color).ToString("x8", CultureInfo.InvariantCulture),
            FormReferenceValue reference => ResolveReference(document, reference),
            _ => throw new InvalidOperationException(
                $"Property '{descriptor.Id}' has no canonical XML representation.")
        };
        if (text is not null)
        {
            writer.WriteAttributeString(descriptor.XmlName, text);
        }
    }

    private static string ResolveReference(
        ResolvedFormDocument document,
        FormReferenceValue reference)
    {
        return reference.ReferenceKind switch
        {
            FormReferenceKind.Action =>
                document.Actions[reference.ReferenceId].RuntimeId.ToString(CultureInfo.InvariantCulture),
            FormReferenceKind.Binding => document.Bindings[reference.ReferenceId].RuntimeName,
            FormReferenceKind.Font or FormReferenceKind.Bitmap =>
                document.Resources[reference.ReferenceId].RuntimeName,
            _ => throw new InvalidOperationException("The reference kind is unsupported.")
        };
    }

    private static uint ResolveColor(ResolvedFormDocument document, FormColorValue color)
    {
        return color.EncodedColor ?? document.ThemeColors[color.ThemeTokenId!].EncodedColor;
    }

    private static string FormatRectangle(FormRectangle value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X},{value.Y},{value.Width},{value.Height}");
    }

    private static string ToBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
