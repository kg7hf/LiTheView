// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.Segments;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static async Task CheckSegmentedAsync(DesignerService service, string library, string directory, bool native)
    {
        string manifest = SegmentProject.Create(directory,
            File.ReadAllBytes(Path.Combine(library, "examples/efm32tg-stk3300-menu-1-complete/UI/design/glass.segment-display.json")),
            File.ReadAllBytes(Path.Combine(library, "profiles/logical-inputs/encoder-select.json")));
        string path = Path.Combine(directory, "forms/home.litheview.xml"), xml = File.ReadAllText(path);
        ValueTask<ServiceResponse> Request(string method, EditOperation? edit = null, ProjectSetupRequest? setup = null, string? screen = null) =>
            service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest { Id = ++_requestId, ProtocolVersion = 1, Method = method,
                Params = new() { ManifestPath = manifest, FormPath = path, Xml = xml, DocumentVersion = 1, Operation = edit, Setup = setup, ScreenName = screen } }, Protocol.JsonOptions));
        var inspected = await Request("inspect").ConfigureAwait(false);
        Require(inspected.Ok && Result(inspected).GetProperty("displayKind").GetString() == "segmented", "Blank segmented project opens: " + inspected.Error?.Message);
        Require(Result(inspected).GetProperty("widgets").GetArrayLength() == 0, "Segmented palette contains no raster controls.");
        foreach (var binding in new[] { ("label", "PLAY"), ("value", "0"), ("volumeRing", "50") })
        {
            var edited = await Request("edit", new() { Kind = "segmentBinding", ComponentId = binding.Item1, Name = binding.Item1, Text = binding.Item2 }).ConfigureAwait(false);
            Require(edited.Ok, "Field edit succeeds: " + edited.Error?.Message);
            xml = Result(edited).GetProperty("xml").GetString()!;
        }
        Require(!(await Request("export").ConfigureAwait(false)).Ok, "Unsaved segmented form cannot export.");
        File.WriteAllText(path, xml);
        var overlapping = await Request("edit", new() { Kind = "segmentBinding", ComponentId = "ringPiece0", Name = "piece", Text = "true" }).ConfigureAwait(false);
        Require(!overlapping.Ok, "Overlapping electrode writers rejected.");
        Require(!(await Request("edit", new() { Kind = "segmentBinding", ComponentId = "label", Name = "label", Text = "TOO LONG" }).ConfigureAwait(false)).Ok,
            "Text overflow rejected before edit.");
        var handlers = await Request("handlers").ConfigureAwait(false);
        Require(handlers.Ok && Result(handlers).GetProperty("passive").GetBoolean() && !Result(handlers).TryGetProperty("handlerPath", out _), "No handlers or FSM generated for passive fields.");
        var added = await Request("project.addScreen", screen: "volume").ConfigureAwait(false);
        Require(added.Ok, "Segmented Add screen produces an edit plan.");
        var add = Result(added);
        File.WriteAllText(add.GetProperty("formPath").GetString()!, add.GetProperty("xml").GetString());
        File.WriteAllText(manifest, add.GetProperty("after").GetString());
        Require(SegmentProject.Load(manifest).Forms.Count == 2, "Two screens share one locked project.");
        var exported = await Request("export").ConfigureAwait(false);
        Require(exported.Ok, "Segmented project export succeeds: " + exported.Error?.Message);
        string output = Result(exported).GetProperty("outputDirectory").GetString()!;
        Require(File.Exists(Path.Combine(output, "home.hpp")) && File.Exists(Path.Combine(output, "volume.hpp")), "Every screen is generated.");
        string generated = File.ReadAllText(Path.Combine(output, "home.hpp"));
        if (OperatingSystem.IsWindows())
        {
            string originalXml = xml;
            xml = xml.Replace("preview=\"50\"", "preview=\"49\"", StringComparison.Ordinal);
            File.WriteAllText(path, xml);
            using (FileStream locked = new(Path.Combine(output, "volume.hpp"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var failed = await Request("export").ConfigureAwait(false);
                Require(!failed.Ok && File.ReadAllText(Path.Combine(output, "home.hpp")) == generated,
                    "A later output I/O failure restores the earlier output.");
            }
            xml = originalXml; File.WriteAllText(path, xml);
        }
        File.WriteAllText(Path.Combine(output, "volume.hpp"), "// handwritten sentinel");
        Require(!(await Request("export").ConfigureAwait(false)).Ok && File.ReadAllText(Path.Combine(output, "home.hpp")) == generated,
            "All destinations are preflighted before replacing any output.");
        var current = Result(await Request("project.setup").ConfigureAwait(false));
        SegmentProject project = SegmentProject.Load(manifest);
        var changed = project.Glass with { ViewWidth = project.Glass.ViewWidth + 1 };
        var setup = new ProjectSetupRequest { Snapshot = current.GetProperty("snapshot").GetString()!,
            SegmentGlass = JsonSerializer.SerializeToElement(changed, Protocol.JsonOptions),
            Input = new() { Pointer = false, Keys = true, Text = false, Encoder = true, Select = true } };
        var plan = await Request("project.setup", setup: setup).ConfigureAwait(false);
        Require(plan.Ok && File.ReadAllText(path) == xml, "Configure proposes changes without writing files.");
        foreach (var edit in Result(plan).GetProperty("edits").EnumerateArray()) File.WriteAllText(edit.GetProperty("path").GetString()!, edit.GetProperty("after").GetString());
        xml = File.ReadAllText(path);
        Require(SegmentProject.Load(manifest).Glass.ViewWidth == changed.ViewWidth, "Configure relocks all forms and input.");
        Require(!(await Request("project.setup", setup: setup).ConfigureAwait(false)).Ok, "Stale Configure snapshot rejected.");
        if (native)
        {
            int[] ring = [140, 147, 146, 145, 144, 143, 142, 141];
            int[] thresholds = [7, 19, 32, 44, 57, 69, 82, 94];
            // Independent A03 COM/S mapping for PLAY and the rightmost zero. This
            // does not read generated tables or the imported JSON glyph descriptors.
            HashSet<int> expected = [15, 55, 115, 135, 95, 35];
            string strokes = "ABCDEFGHJKMNPQ";
            string[] letters = ["ABEFGM", "DEF", "ABCEFGM", "HKP"];
            for (int cell = 0; cell < letters.Length; ++cell)
            {
                int left = cell * 2 + 1, right = left + 1;
                int[] map = [120 + left, 120 + right, 40 + right, right, left, 80 + left, 60 + left,
                    100 + left, 80 + right, 100 + right, 60 + right, 20 + right, 40 + left, 20 + left];
                foreach (char stroke in letters[cell]) expected.Add(map[strokes.IndexOf(stroke)]);
            }
            for (int volume = 0; volume <= 100; ++volume)
            {
                var edited = await Request("edit", new() { Kind = "segmentBinding", ComponentId = "volumeRing", Name = "volumeRing", Text = volume.ToString(System.Globalization.CultureInfo.InvariantCulture) }).ConfigureAwait(false);
                xml = Result(edited).GetProperty("xml").GetString()!;
                var rendered = await Request("render").ConfigureAwait(false);
                Require(rendered.Ok, "Native segmented render: " + rendered.Error?.Message);
                var result = Result(rendered);
                byte[] bits = Convert.FromBase64String(result.GetProperty("segmentBytesBase64").GetString()!);
                Require(bits.Length == 20 && ring.Select((id, i) => ((bits[id / 8] & (1 << (id % 8))) != 0) == (volume >= thresholds[i])).All(v => v),
                    "Native ring matches independent threshold oracle at " + volume);
                Require(Enumerable.Range(0, 160).Where(id => !ring.Contains(id)).All(id =>
                    ((bits[id / 8] & (1 << (id % 8))) != 0) == expected.Contains(id)), "Text and numeric native bits match the independent A03 oracle.");
                Require(Convert.FromBase64String(result.GetProperty("rgbaBase64").GetString()!).Length == changed.ViewWidth * changed.ViewHeight * 4, "Host geometry expands after native segment encoding.");
            }
        }
        string saved = File.ReadAllText(manifest);
        File.WriteAllText(manifest, saved.Replace("\"schemaVersion\":4", "\"surfaceProfile\":null,\"schemaVersion\":4", StringComparison.Ordinal));
        Require(!DesignerProjectLoader.LoadFile(manifest).IsValid, "Segmented manifest rejects even null raster fields.");
        File.WriteAllText(manifest, saved);
        File.AppendAllText(Path.Combine(directory, SegmentProject.GlassPath), " invalid");
        Require(!(await Request("inspect").ConfigureAwait(false)).Ok, "Broken glass fails closed.");
    }
}
