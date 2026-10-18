// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Segments;

namespace LitheView.Designer.Service;

internal sealed partial class DesignerService
{
    private async ValueTask<object> HandleSegmentedAsync(string method, RequestParameters p)
    {
        if (p.DocumentVersion is null or < 0 || p.Xml is null || Encoding.UTF8.GetByteCount(p.Xml) > Protocol.MaximumXmlBytes)
            throw new ServiceException("InvalidRequest", "A bounded XML document and version are required.");
        string manifest = AbsolutePath(p.ManifestPath, "Manifest"), path = AbsolutePath(p.FormPath, "Form");
        SegmentProject project = SegmentProject.Load(manifest, path, Encoding.UTF8.GetBytes(p.Xml));
        SegmentForm form = project.Forms[path];
        switch (method)
        {
        case "inspect": return InspectSegments(project.Glass, form, p.DocumentVersion.Value);
        case "handlers": return new { documentVersion = p.DocumentVersion, passive = true };
        case "project.setup": return ConfigureSegments(project, p);
        case "project.addScreen":
        {
            string name = Required(p.ScreenName, "Screen name");
            if (DesignerProjectForms.Name(name + ".litheview.xml") != name ||
                !System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9]{0,63}$"))
                throw new InvalidDataException("Use a screen name starting with a letter, followed by letters or digits.");
            string formId = char.ToLowerInvariant(name[0]) + name[1..];
            string relative = "forms/" + name + ".litheview.xml";
            string target = Path.Combine(project.Directory, relative);
            if (File.Exists(target) || Directory.Exists(target) || project.Forms.Values.Any(f => f.Id.Equals(formId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Screen already exists.");
            var updated = project.Manifest with { Forms = [.. project.Manifest.Forms, relative] };
            var validated = DesignerProjectLoader.Load(DesignerProjectCanonicalizer.Canonicalize(updated));
            if (!validated.IsValid) throw new InvalidDataException(Messages(validated.Diagnostics));
            return new ScreenCreationPlan(manifest, Encoding.UTF8.GetString(SegmentProject.Read(manifest)),
                Encoding.UTF8.GetString(validated.CanonicalUtf8!), target,
                Encoding.UTF8.GetString(SegmentDocuments.WriteForm(new(formId, project.Glass.Id, SegmentDocuments.GlassHash(project.Glass), []), project.Glass)));
        }
        case "edit":
        {
            EditOperation edit = p.Operation ?? throw new InvalidDataException("Edit operation required.");
            if (edit.Kind != "segmentBinding" || edit.ComponentId is null) throw new InvalidDataException("Use a segmented field binding edit.");
            List<SegmentBinding> bindings = form.Bindings.Where(b => b.Field != edit.ComponentId).ToList();
            if (!string.IsNullOrEmpty(edit.Name)) bindings.Add(new(edit.ComponentId, edit.Name, edit.Text));
            SegmentForm changed = form with { Bindings = bindings.ToArray() };
            string xml = Encoding.UTF8.GetString(SegmentDocuments.WriteForm(changed, project.Glass));
            return new { documentVersion = p.DocumentVersion, xml, diagnostics = Array.Empty<DiagnosticView>() };
        }
        case "export":
            if (NormalizeEditorLineEndings(Encoding.UTF8.GetString(SegmentProject.Read(path))) != NormalizeEditorLineEndings(p.Xml))
                throw new InvalidDataException("Save the segmented form before generating.");
            return new { outputDirectory = SegmentProject.Export(manifest, _libraryRoot), targetStatus = "unevaluated", diagnostics = Array.Empty<DiagnosticView>() };
        case "render":
            if (_preview is null) throw new ServiceException("PreviewUnavailable", "Prepare the native preview worker first.");
            var frame = await _preview.RenderSegmentsAsync(project.Glass, form, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return new { documentVersion = p.DocumentVersion, width = project.Glass.ViewWidth, height = project.Glass.ViewHeight,
                rgbaBase64 = Convert.ToBase64String(frame.Rgba), segmentBytesBase64 = Convert.ToBase64String(frame.Segments),
                status = "Ok", diagnostics = Array.Empty<DiagnosticView>() };
        default: throw new InvalidDataException("This command does not apply to segmented glass; use Configure.");
        }
    }

    private static object InspectSegments(SegmentGlass glass, SegmentForm form, int version)
    {
        var fields = glass.Icons.Select(f => (f.Id, Kind: "icon", Maximum: 1))
            .Concat(glass.Groups.Select(f => (f.Id, Kind: "group", f.Maximum)))
            .Concat(glass.Texts.Select(f => (f.Id, Kind: "text", Maximum: f.Cells.Length)))
            .Concat(glass.Numbers.Select(f => (f.Id, Kind: "number", Maximum: (int)Math.Pow(10, f.Cells.Length) - 1)));
        return new { documentVersion = version, displayKind = "segmented", components = Array.Empty<ComponentView>(),
            widgets = Array.Empty<WidgetView>(), properties = Array.Empty<PropertyDescriptorView>(), choices = Array.Empty<ChoiceView>(),
            surface = new SurfaceView(glass.ViewWidth, glass.ViewHeight, "Segmented", "Fixed glass"),
            segmentFields = fields.Select(f => new { id = f.Id, kind = f.Kind, maximum = f.Maximum,
                input = form.Bindings.FirstOrDefault(b => b.Field == f.Id)?.Input,
                preview = form.Bindings.FirstOrDefault(b => b.Field == f.Id)?.Preview ?? (f.Kind == "text" ? "" : f.Kind == "icon" ? "false" : "0") }).ToArray(),
            diagnostics = Array.Empty<DiagnosticView>() };
    }

    private static object ConfigureSegments(SegmentProject project, RequestParameters p)
    {
        SetupDocument Read(string relative) { string path = Path.Combine(project.Directory, relative); return new(path, Encoding.UTF8.GetString(SegmentProject.Read(path))); }
        SetupDocument input = Read(SegmentProject.InputPath), glassDocument = Read(SegmentProject.GlassPath);
        SetupDocument[] forms = project.Manifest.Forms.Select(Read).ToArray();
        SetupDocument manifest = new(project.ManifestPath, Encoding.UTF8.GetString(SegmentProject.Read(project.ManifestPath)));
        SetupDocument[] documents = [input, glassDocument, .. forms, manifest];
        // Re-resolve the bytes we captured, not a mixture of different on-disk revisions.
        var current = SegmentProject.Load(project.ManifestPath);
        if (SegmentDocuments.GlassHash(current.Glass) != SegmentDocuments.GlassHash(project.Glass) ||
            documents.Any(d => Encoding.UTF8.GetString(SegmentProject.Read(d.Path)) != d.Before))
            throw new InvalidDataException("Project changed while Configure opened.");
        string snapshot = DisplaySurfaceCanonicalizer.ComputeSha256(JsonSerializer.SerializeToUtf8Bytes(documents, Protocol.JsonOptions));
        List<InputFileEdit> edits = [];
        if (p.Setup is { } request)
        {
            if (request.Snapshot != snapshot) throw new InvalidDataException("Project changed; reopen Configure.");
            if (request.CustomSurface is not null || request.CustomNativeDisplay is not null) throw new InvalidDataException("Keep the segmented display kind.");
            SegmentGlass glass = request.SegmentGlass is { } candidate ? SegmentDocuments.ReadGlass(Encoding.UTF8.GetBytes(candidate.GetRawText())) : project.Glass;
            string hash = SegmentDocuments.GlassHash(glass);
            LogicalInputConfigurationProfile nextInput = request.Input is { } selected ? project.Input with
            { Pointer = selected.Pointer, Keys = selected.Keys, Text = selected.Text, Encoder = selected.Encoder, Select = selected.Select } : project.Input;
            byte[] inputBytes = LogicalInputConfigurationCanonicalizer.Canonicalize(nextInput);
            string inputHash = DisplaySurfaceCanonicalizer.ComputeSha256(inputBytes);
            void Add(SetupDocument d, byte[] bytes) => edits.Add(new(d.Path, d.Before, Encoding.UTF8.GetString(bytes)));
            if (inputHash != project.Manifest.LogicalInput!.Sha256) Add(input, inputBytes);
            if (hash != project.Manifest.SegmentGlass!.Sha256)
            {
                Add(glassDocument, SegmentDocuments.CanonicalGlass(glass));
                foreach (SetupDocument d in forms)
                {
                    SegmentForm old = SegmentDocuments.ReadForm(Encoding.UTF8.GetBytes(d.Before), project.Glass);
                    Add(d, SegmentDocuments.WriteForm(old with { GlassId = glass.Id, GlassSha256 = hash }, glass));
                }
            }
            if (edits.Count > 0) Add(manifest, DesignerProjectCanonicalizer.Canonicalize(project.Manifest with
            { SegmentGlass = new() { Id = glass.Id, Sha256 = hash }, LogicalInput = new() { Id = nextInput.Id, Sha256 = inputHash } }));
        }
        return new { documentVersion = p.DocumentVersion, displayKind = "segmented", snapshot, documents, edits,
            segmentGlass = project.Glass, input = project.Input };
    }
}
