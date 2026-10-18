// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Segments;

// Every compile validates its own serialized snapshot; no mutable validated cache.
public static class SegmentDocuments
{
    public const int MaximumBytes = 1_048_576;
    public const int MaximumSegments = 4096;
    public const int MaximumFields = 128;

    public static SegmentGlass ReadGlass(ReadOnlyMemory<byte> utf8)
    {
        List<ProfileDiagnostic> diagnostics = [];
        SegmentGlass? glass = DisplayDefinitionJson.Read<SegmentGlass>(utf8, "LVSG", diagnostics);
        if (glass is null)
        {
            throw new InvalidDataException(string.Join("; ", diagnostics.Select(d => d.Message)));
        }
        ValidateGlass(glass);
        return glass;
    }

    public static byte[] CanonicalGlass(SegmentGlass glass)
    {
        ValidateGlass(glass);
        // Field/shape/glyph declaration order is not semantic. Cell, stroke and group order is.
        SegmentGlass ordered = glass with
        {
            Shapes = glass.Shapes.OrderBy(s => s.Id).ToArray(),
            Icons = glass.Icons.OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(),
            Groups = glass.Groups.OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(),
            Texts = glass.Texts.OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => f with
            { Glyphs = f.Glyphs.OrderBy(g => g.Character, StringComparer.Ordinal).ToArray() }).ToArray(),
            Numbers = glass.Numbers.OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => f with
            { Glyphs = f.Glyphs.OrderBy(g => g.Character, StringComparer.Ordinal).ToArray() }).ToArray()
        };
        return JsonSerializer.SerializeToUtf8Bytes(ordered, DisplayDefinitionJson.Options);
    }

    public static string GlassHash(SegmentGlass glass) => GeneratedFileAccess.Hash(CanonicalGlass(glass));

    public static SegmentForm ReadForm(ReadOnlyMemory<byte> utf8, SegmentGlass glass)
    {
        Require(utf8.Length <= MaximumBytes, "Form exceeds the bounded serialized size.");
        using MemoryStream stream = new(utf8.ToArray(), writable: false);
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumBytes
        });
        XDocument document = XDocument.Load(reader);
        XElement root = document.Root ?? throw new InvalidDataException("A segmented-form root is required.");
        Require(root.Name == "segmented-form", "Expected a segmented-form, not a pixel form.");
        Attributes(root, "schemaVersion", "id", "glassId", "glassSha256");
        Require((string?)root.Attribute("schemaVersion") == "1", "Unsupported segmented form version.");
        Require(!root.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)), "Unexpected form text.");
        XElement[] elements = root.Elements().ToArray();
        Require(elements.Length <= MaximumFields, "Too many bindings.");
        List<SegmentBinding> bindings = [];
        foreach (XElement element in elements)
        {
            Require(element.Name == "bind", "Only bind elements are allowed.");
            Attributes(element, element.Attribute("preview") is null ? ["field", "input"] : ["field", "input", "preview"]);
            Require(!element.Elements().Any() && string.IsNullOrWhiteSpace(element.Value), "Bindings cannot contain content.");
            bindings.Add(new(element.Attribute("field")!.Value, element.Attribute("input")!.Value, (string?)element.Attribute("preview")));
        }
        SegmentForm form = new(root.Attribute("id")!.Value, root.Attribute("glassId")!.Value,
            root.Attribute("glassSha256")!.Value, bindings.ToArray());
        ValidateForm(form, glass);
        return form;
    }

    public static void ValidateForm(SegmentForm form, SegmentGlass glass)
    {
        ValidateGlass(glass);
        Require(Name(form.Id), "Form ID must be a bounded identifier.");
        Require(form.GlassId == glass.Id && form.GlassSha256 == GlassHash(glass), "Glass identity or snapshot hash changed.");
        Require(form.Bindings is { Length: <= MaximumFields }, "Invalid bindings.");
        HashSet<string> fields = new(StringComparer.Ordinal);
        HashSet<string> inputs = new(StringComparer.Ordinal);
        HashSet<int> writers = [];
        foreach (SegmentBinding binding in form.Bindings)
        {
            Require(binding is not null && Name(binding.Input) && inputs.Add(binding.Input), "Invalid or duplicate input name.");
            Require(fields.Add(binding.Field), "A field may be bound only once.");
            ValidatePreview(glass, binding);
            int[] ids = FieldSegments(glass, binding.Field);
            foreach (int id in ids)
                Require(writers.Add(id), "Two bindings write the same segment.");
        }
    }

    public static byte[] WriteForm(SegmentForm form, SegmentGlass glass)
    {
        ValidateForm(form, glass);
        XElement root = new("segmented-form", new XAttribute("schemaVersion", "1"),
            new XAttribute("id", form.Id), new XAttribute("glassId", form.GlassId),
            new XAttribute("glassSha256", form.GlassSha256), form.Bindings.Select(b =>
                new XElement("bind", new XAttribute("field", b.Field), new XAttribute("input", b.Input),
                    b.Preview is null ? null : new XAttribute("preview", b.Preview))));
        return System.Text.Encoding.UTF8.GetBytes(root.ToString() + "\n");
    }

    public static void ValidatePreview(SegmentGlass glass, SegmentBinding binding)
    {
        if (binding.Preview is not { } value) return;
        if (glass.Icons.Any(f => f.Id == binding.Field)) Require(value is "true" or "false", "Icon preview must be true or false.");
        else if (glass.Texts.FirstOrDefault(f => f.Id == binding.Field) is { } text)
            Require(value.Length <= text.Cells.Length && value.All(c => text.Glyphs.Any(g => g.Character[0] == c)), "Preview text exceeds its cells or glyph set.");
        else
        {
            Require(uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint number), "Preview needs an unsigned decimal number.");
            if (glass.Groups.FirstOrDefault(f => f.Id == binding.Field) is { } group) Require(number <= group.Maximum, "Preview exceeds group range.");
            else if (glass.Numbers.FirstOrDefault(f => f.Id == binding.Field) is { } digits)
                Require(number < Math.Pow(10, digits.Cells.Length), "Preview exceeds numeric cells.");
        }
    }

    internal static int[] FieldSegments(SegmentGlass glass, string field)
    {
        if (glass.Icons.FirstOrDefault(f => f.Id == field) is { } icon) return icon.Segments;
        if (glass.Groups.FirstOrDefault(f => f.Id == field) is { } group) return group.Segments;
        if (glass.Texts.FirstOrDefault(f => f.Id == field) is { } text) return text.Cells.SelectMany(c => c).ToArray();
        if (glass.Numbers.FirstOrDefault(f => f.Id == field) is { } number) return number.Cells.SelectMany(c => c).ToArray();
        throw new InvalidDataException($"Unknown field '{field}'.");
    }

    private static void ValidateGlass(SegmentGlass glass)
    {
        ArgumentNullException.ThrowIfNull(glass);
        Require(glass.SchemaVersion == 1 && Name(glass.Id), "Unsupported glass version or invalid ID.");
        Require(glass.SegmentCount is > 0 and <= MaximumSegments, "Invalid segment count.");
        Require(glass.ViewWidth is > 0 and <= 16384 && glass.ViewHeight is > 0 and <= 16384, "Invalid host viewport.");
        Require(glass.Shapes is { Length: > 0 and <= MaximumSegments }, "Invalid shapes.");
        HashSet<int> shaped = [];
        foreach (SegmentShape shape in glass.Shapes)
        {
            Require(shape is not null && shape.Id >= 0 && shape.Id < glass.SegmentCount && shaped.Add(shape.Id), "Invalid or duplicate segment shape.");
            Require(shape.Polygon is { Length: >= 3 and <= 32 }, "Polygon needs 3-32 points.");
            long area = 0;
            for (int i = 0; i < shape.Polygon.Length; ++i)
            {
                SegmentPoint p = shape.Polygon[i];
                SegmentPoint q = shape.Polygon[(i + 1) % shape.Polygon.Length];
                Require(p is not null && q is not null && p.X >= 0 && p.X <= glass.ViewWidth && p.Y >= 0 && p.Y <= glass.ViewHeight,
                    "Polygon point is outside the host viewport.");
                area += (long)p.X * q.Y - (long)p.Y * q.X;
            }
            Require(area != 0, "Polygon must have a nonzero signed area.");
        }
        Require(glass.Icons is not null && glass.Groups is not null && glass.Texts is not null && glass.Numbers is not null, "Field arrays are required.");
        Require(glass.Icons.Length + glass.Groups.Length + glass.Texts.Length + glass.Numbers.Length <= MaximumFields, "Too many fields.");
        HashSet<string> names = new(StringComparer.Ordinal);
        void Field(string name, int[] ids)
        {
            Require(Name(name) && names.Add(name), "Invalid or duplicate field ID.");
            Require(ids is { Length: > 0 and <= MaximumSegments } && ids.Distinct().Count() == ids.Length && ids.All(shaped.Contains),
                "A field needs unique segments with drawing geometry.");
        }
        void Cells(string id, int[][] cells, SegmentGlyph[] glyphs, bool numeric)
        {
            Require(cells is { Length: > 0 and <= 64 } && cells.All(c => c is { Length: > 0 and <= 32 }), "Invalid character cells.");
            Require(cells.All(c => c.Length == cells[0].Length), "Cell stroke counts must match.");
            if (numeric) Require(cells.Length <= 9, "Numbers support 1-9 decimal digits.");
            Field(id, cells.SelectMany(c => c).ToArray());
            Require(glyphs is { Length: > 0 and <= 95 }, "Invalid glyph set.");
            HashSet<string> characters = new(StringComparer.Ordinal);
            foreach (SegmentGlyph glyph in glyphs)
            {
                Require(glyph is not null && glyph.Character is { Length: 1 } && glyph.Character[0] is >= ' ' and <= '~' && characters.Add(glyph.Character), "Glyphs must have unique printable ASCII characters.");
                Require(glyph.Strokes is not null && glyph.Strokes.Length <= 32 && glyph.Strokes.Distinct().Count() == glyph.Strokes.Length &&
                    glyph.Strokes.All(s => s >= 0 && s < cells[0].Length), "Invalid glyph stroke.");
            }
            if (numeric) Require(characters.SetEquals(Enumerable.Range(0, 10).Select(n => n.ToString(CultureInfo.InvariantCulture))), "Numeric fields need exactly digits 0-9.");
        }
        foreach (SegmentIcon icon in glass.Icons) { Require(icon is not null, "Null icon."); Field(icon.Id, icon.Segments); }
        foreach (SegmentGroup group in glass.Groups)
        {
            Require(group is not null && group.Maximum is > 0 and <= 1_000_000, "Invalid group range.");
            Field(group.Id, group.Segments);
        }
        foreach (SegmentText text in glass.Texts) { Require(text is not null, "Null text field."); Cells(text.Id, text.Cells, text.Glyphs, false); }
        foreach (SegmentNumber number in glass.Numbers) { Require(number is not null, "Null number field."); Cells(number.Id, number.Cells, number.Glyphs, true); }
    }

    private static void Attributes(XElement element, params string[] expected) =>
        Require(element.Attributes().Select(a => a.Name.ToString()).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal)), "Missing or unknown XML attributes.");

    internal static bool Name(string? value) => value is { Length: > 0 and <= 64 } && value[0] is >= 'a' and <= 'z' &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9');

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
