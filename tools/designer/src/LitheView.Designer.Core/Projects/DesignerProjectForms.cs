// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.RegularExpressions;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.IO;

namespace LitheView.Designer.Core.Projects;

// Forms are components of one UI project, never tasks or application entry points.
public static class DesignerProjectForms
{
    public static string Name(string path)
    {
        string name = Path.GetFileName(path);
        const string suffix = ".litheview.xml";
        if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A screen must use the .litheview.xml extension.");
        name = name[..^suffix.Length];
        if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidDataException("Screen filenames require 1-64 letters, digits or underscores, beginning with a letter, and cannot be reserved device names.");
        return name;
    }

    public static bool HasOwnDirectory(DesignerWorkspace workspace, string output) =>
        output.Contains("{form}", StringComparison.Ordinal) ||
        (workspace.Manifest.Forms.Length > 1 && workspace.Form.Id != "form.main");

    public static string OutputDirectory(DesignerWorkspace workspace, string output)
    {
        if (output.Contains("{form}", StringComparison.Ordinal))
            return output.Replace("{form}", Name(workspace.FormPath), StringComparison.Ordinal);
        // Legacy single-form projects used form.main. Its location must not depend
        // on manifest ordering (the canonicalizer sorts form paths).
        if (workspace.Manifest.Forms.Length > 1 && LoadAll(workspace).Count(item => item.Form.Id == "form.main") != 1)
            throw new InvalidDataException("Multi-form projects without a legacy form.main require {form} in componentDirectory.");
        // Preserve the original single-form output when a second screen is added.
        return HasOwnDirectory(workspace, output) ? Path.Combine(output, Name(workspace.FormPath)) : output;
    }

    public static string HandlerPath(DesignerWorkspace workspace, string output) =>
        HasOwnDirectory(workspace, output)
            ? Path.Combine(workspace.ProjectDirectory, "handlers", Name(workspace.FormPath) + ".hpp")
            : Path.Combine(workspace.ProjectDirectory, "form_handlers.hpp");

    public static IReadOnlyList<DesignerWorkspace> LoadAll(DesignerWorkspace workspace)
    {
        List<DesignerWorkspace> forms = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> symbols = new(StringComparer.Ordinal);
        foreach (string relative in workspace.Manifest.Forms)
        {
            string path = GeneratedFileAccess.Contained(workspace.ProjectDirectory, relative);
            // The editor's selected form can be unsaved; other screens are read from disk.
            FormDocument form;
            if (path.Equals(workspace.FormPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                form = workspace.Form;
            else
            {
                var imported = FormXmlImporter.LoadFile(path, workspace.Catalog, FormImportMode.Strict);
                if (!imported.IsValid || imported.Document is null)
                    throw new InvalidDataException($"Cannot read screen '{relative}'.");
                form = imported.Document;
            }
            var resolved = FormDocumentResolver.Resolve(form, workspace.Catalog);
            if (!resolved.IsValid || resolved.Document is null)
                throw new InvalidDataException($"Invalid screen '{relative}'.");
            if (workspace.Manifest.Forms.Length > 1 &&
                (!names.Add(Name(path)) || !ids.Add(form.Id) || !symbols.Add(resolved.Document.CppSymbol)))
                throw new InvalidDataException("Screens require unique filenames, form IDs and generated C++ names within their UI project.");
            forms.Add(workspace with { FormPath = path, Form = form });
        }
        return forms;
    }

    // The editor applies this plan as one WorkspaceEdit, with optimistic source checks.
    public static ScreenCreationPlan PlanAdd(DesignerWorkspace workspace, string name)
    {
        if (Name(name + ".litheview.xml") != name) throw new InvalidDataException("Use a screen name, not a path.");
        var existing = LoadAll(workspace);
        string relative = "forms/" + name + ".litheview.xml";
        string path = GeneratedFileAccess.Contained(workspace.ProjectDirectory, relative);
        if (existing.Any(item => Name(item.FormPath).Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            File.Exists(path) || Directory.Exists(path))
            throw new InvalidDataException("A screen with this name already exists. Choose another name.");
        string before = File.ReadAllText(workspace.ManifestPath);
        var current = DesignerProjectLoader.Load(Encoding.UTF8.GetBytes(before));
        if (current.Sha256 != workspace.ResolvedProject.ManifestSha256)
            throw new InvalidDataException("The project changed. Refresh before adding a screen.");
        var display = workspace.ResolvedProject.Display;
        var root = workspace.Form.Components.Single(item => item.ParentComponentId is null);
        FormDocument form = workspace.Form with
        {
            Id = "form." + name.ToLowerInvariant(), DisplayName = name,
            Components = [root with
            {
                ComponentId = "component." + name.ToLowerInvariant() + ".root", Name = "rootPanel",
                Properties = [new FormRectangleValue("frame", new(0, 0, display.Width, display.Height)),
                    new FormColorValue("background-color", 0x00201810U)], HistoricalAttributes = []
            }], Actions = [], Bindings = [], Resources = [], ThemeColors = []
        };
        var manifest = workspace.Manifest with { Forms = [.. workspace.Manifest.Forms, relative] };
        var checkedManifest = DesignerProjectLoader.Load(DesignerProjectCanonicalizer.Canonicalize(manifest));
        if (!checkedManifest.IsValid) throw new InvalidDataException("The project cannot accept another screen.");
        // Validate the candidate against every existing identity before publishing anything.
        var resolved = FormDocumentResolver.Resolve(form, workspace.Catalog);
        if (!resolved.IsValid || existing.Any(item => item.Form.Id == form.Id ||
            FormDocumentResolver.Resolve(item.Form, workspace.Catalog).Document!.CppSymbol == resolved.Document!.CppSymbol))
            throw new InvalidDataException("This screen name conflicts with an existing form ID or C++ name.");
        var xml = FormXmlWriter.Write(form, workspace.Catalog);
        if (!xml.IsValid || xml.Utf8 is null) throw new InvalidDataException("Cannot create the screen form.");
        return new(workspace.ManifestPath, before, Encoding.UTF8.GetString(checkedManifest.CanonicalUtf8!),
            path, Encoding.UTF8.GetString(xml.Utf8));
    }
}

public sealed record ScreenCreationPlan(string ManifestPath, string Before, string After, string FormPath, string Xml);
