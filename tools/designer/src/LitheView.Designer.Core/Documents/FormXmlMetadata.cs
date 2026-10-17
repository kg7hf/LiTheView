// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Xml.Linq;

namespace LitheView.Designer.Core.Documents;

internal static class FormXmlMetadata
{
    public const string FormKind = "litheview-form";
    public const string ActionKind = "litheview-action";
    public const string BindingKind = "litheview-binding";
    public const string ResourceKind = "litheview-resource";
    public const string ThemeColorKind = "litheview-theme-color";
    public const string ThemeReferenceKind = "litheview-theme-reference";
    public const string ComponentKind = "litheview-component";

    public static bool TryParse(XComment comment, out string kind, out XElement metadata)
    {
        string value = comment.Value.Trim();
        int separator = value.IndexOf(' ');
        kind = separator < 0 ? value : value[..separator];
        string attributes = separator < 0 ? string.Empty : value[(separator + 1)..];
        if (!kind.StartsWith("litheview-", StringComparison.Ordinal))
        {
            metadata = null!;
            return false;
        }

        try
        {
            metadata = XElement.Parse($"<metadata {attributes}/>", LoadOptions.None);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            metadata = null!;
            return false;
        }
    }

    public static string Format(string kind, params (string Name, string? Value)[] values)
    {
        string attributes = string.Join(
            ' ',
            values
                .Where(item => item.Value is not null)
                .Select(item => $"{item.Name}=\"{Escape(item.Value!)}\""));
        return attributes.Length == 0 ? kind : $"{kind} {attributes}";
    }

    public static string Required(XElement metadata, string name)
    {
        return (string?)metadata.Attribute(name) ?? string.Empty;
    }

    public static int? OptionalInt32(XElement metadata, string name)
    {
        string? value = (string?)metadata.Attribute(name);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    public static long? OptionalInt64(XElement metadata, string name)
    {
        string? value = (string?)metadata.Attribute(name);
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : null;
    }

    public static uint? OptionalHex32(XElement metadata, string name)
    {
        string? value = (string?)metadata.Attribute(name);
        return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed)
            ? parsed
            : null;
    }

    private static string Escape(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal)
                    .Replace("\"", "&quot;", StringComparison.Ordinal)
                    .Replace("<", "&lt;", StringComparison.Ordinal)
                    .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
