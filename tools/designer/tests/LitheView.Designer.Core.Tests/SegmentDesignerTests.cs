// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using LitheView.Designer.Core.Segments;

namespace LitheView.Designer.Core.Tests;

internal static class SegmentDesignerTests
{
    public static void Run(string repositoryRoot)
    {
        string tiny = Path.Combine(repositoryRoot, "examples", "efm32tg-stk3300-menu-1-complete", "UI", "design");
        byte[] glass = File.ReadAllBytes(Path.Combine(tiny, "glass.segment-display.json"));
        byte[] form = File.ReadAllBytes(Path.Combine(tiny, "presentation.litheview.xml"));
        SegmentCompilation compiled = SegmentCppCompiler.Compile(glass, form);
        SegmentGlass definition = SegmentDocuments.ReadGlass(glass);
        Require(definition.SegmentCount == 160 && definition.Groups[0].Segments.SequenceEqual([140, 147, 146, 145, 144, 143, 142, 141]), "Tiny Gecko ring mapping changed.");
        Require(compiled.GlassSha256 == "b5451ebf501794e0fcac3010968cbeb73a3a451fb8ef9d9c0592abe453b3da8a", "Canonical glass hash changed.");
        Require(SegmentDocuments.GlassHash(definition with { Shapes = definition.Shapes.Reverse().ToArray() }) == compiled.GlassSha256,
            "Declaration order must not change glass identity.");
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Require(compiled.HeaderUtf8.SequenceEqual(SegmentCppCompiler.Compile(glass, form).HeaderUtf8), "Generated output depends on culture.");
        }
        finally { CultureInfo.CurrentCulture = saved; }
        string header = Encoding.UTF8.GetString(compiled.HeaderUtf8);
        Require(!header.Contains('\r') && !header.Contains("Paul R. Decker", StringComparison.Ordinal) && !header.Contains("void*", StringComparison.Ordinal),
            "Generated output has incorrect ownership, newlines or untyped callbacks.");

        string json = Encoding.UTF8.GetString(glass);
        foreach (string bad in new[]
        {
            json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1", StringComparison.Ordinal),
            json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal),
            json.Replace("\"segmentCount\": 160", "\"segmentCount\": 0", StringComparison.Ordinal),
            json.Replace("\"segmentCount\": 160", "\"target\": null, \"segmentCount\": 160", StringComparison.Ordinal),
            json.Replace("\"shapes\": [", "\"shapes\": [null,", StringComparison.Ordinal),
            json.Replace("\"viewWidth\": 870", "\"viewWidth\": 1", StringComparison.Ordinal)
        }) Reject(() => SegmentDocuments.ReadGlass(Encoding.UTF8.GetBytes(bad)));
        Reject(() => SegmentDocuments.ReadGlass(new byte[SegmentDocuments.MaximumBytes + 1]));
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            obj => obj["icons"] = null,
            obj => obj["texts"]![0]!["cells"]![0] = null,
            obj => obj["texts"]![0]!["glyphs"]![0]!["strokes"] = new JsonArray(99),
            obj => obj["numbers"]![0]!["glyphs"]!.AsArray().RemoveAt(0),
            obj => obj["groups"]![0]!["segments"] = new JsonArray(140, 140),
            obj => obj["groups"]![0]!["maximum"] = 0,
            obj => obj["icons"]![0]!["id"] = "bad;code",
            obj => obj["shapes"]![0]!["id"] = 160,
            obj => obj["shapes"]![0]!["polygon"] = new JsonArray(new JsonObject { ["x"] = 0, ["y"] = 0 })
        })
        {
            JsonObject obj = JsonNode.Parse(glass)!.AsObject();
            mutate(obj);
            Reject(() => SegmentDocuments.ReadGlass(Encoding.UTF8.GetBytes(obj.ToJsonString())));
        }
        string xml = Encoding.UTF8.GetString(form);
        foreach (string bad in new[]
        {
            xml.Replace(compiled.GlassSha256, new string('0', 64), StringComparison.Ordinal),
            xml.Replace("input=\"label\"", "input=\"bad;code\"", StringComparison.Ordinal),
            xml.Replace("input=\"value\"", "input=\"label\"", StringComparison.Ordinal),
            xml.Replace("field=\"value\"", "field=\"missing\"", StringComparison.Ordinal),
            xml.Replace("schemaVersion=\"1\"", "schemaVersion=\"1\" ignored=\"x\"", StringComparison.Ordinal),
            xml.Replace("</segmented-form>", "<bind field=\"ringPiece0\" input=\"piece\" /></segmented-form>", StringComparison.Ordinal),
            "<!DOCTYPE segmented-form [<!ENTITY external SYSTEM 'file:///secret'>]>" + xml,
            "<panel />"
        }) Reject(() => SegmentCppCompiler.Compile(glass, Encoding.UTF8.GetBytes(bad)));

        string output = Path.Combine(repositoryRoot, "build", "segmented-designer-tests", Guid.NewGuid().ToString("N"), "form.hpp");
        SegmentCppCompiler.Export(glass, form, output);
        byte[] original = File.ReadAllBytes(output);
        Reject(() => SegmentCppCompiler.Export(glass, Encoding.UTF8.GetBytes(xml.Replace(compiled.GlassSha256, new string('0', 64), StringComparison.Ordinal)), output));
        Require(original.SequenceEqual(File.ReadAllBytes(output)), "Validation failure changed existing output.");
        SegmentCppCompiler.Export(glass, form, output);
        File.WriteAllText(output, "// handwritten\n");
        Reject(() => SegmentCppCompiler.Export(glass, form, output));
        Require(File.ReadAllText(output) == "// handwritten\n", "Export replaced handwritten content.");

        string small = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "segmented-icons");
        byte[] smallGlass = File.ReadAllBytes(Path.Combine(small, "glass.segment-display.json"));
        byte[] smallForm = File.ReadAllBytes(Path.Combine(small, "status.litheview.xml"));
        SegmentCompilation smallCompiled = SegmentCppCompiler.Compile(smallGlass, smallForm);
        Require(Encoding.UTF8.GetString(smallCompiled.HeaderUtf8).Contains("SegmentFrame<17>", StringComparison.Ordinal), "Synthetic fixture inherited Tiny Gecko geometry.");
        string piece = Encoding.UTF8.GetString(smallForm).Replace("id=\"status\"", "id=\"piece\"", StringComparison.Ordinal)
            .Replace("field=\"level\" input=\"level\"", "field=\"piece\" input=\"piece\"", StringComparison.Ordinal);
        SegmentCppCompiler.Compile(smallGlass, Encoding.UTF8.GetBytes(piece));
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or XmlException or IOException or ArgumentException) { return; }
        throw new InvalidOperationException("Invalid segmented input or unsafe output was accepted.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
