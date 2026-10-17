// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Schema;
namespace LitheView.Designer.Core.Tests;
internal static class MultiFormTests
{
    public static void Run(string library)
    {
        var profiles = ProfileCatalogLoader.LoadSurfaces(Path.Combine(library, "profiles/display-surfaces"));
        var catalog = WidgetCatalogLoader.LoadFile(Path.Combine(library, "schema/widgets.json")).Catalog!;
        string root = Path.Combine(library, "build/multi-form-tests", Guid.NewGuid().ToString("N"));
        var opened = DesignerWorkspaceService.CreateNew(new()
        {
            ProjectDirectory = Path.Combine(root, "Tasks/UI"), ProjectId = "test.multi-form", DisplayName = "Home",
            SchemaReference = "project-manifest.schema.json", Catalog = catalog,
            Surface = profiles.Surfaces.Single(item => item.Profile!.Id == "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565")
        }, profiles);
        Require(opened.IsValid, "Create fixture.");
        var home = DesignerWorkspaceService.Open(opened.Workspace!.ManifestPath, profiles, catalog).Workspace!;
        string flat = Path.Combine(root, "legacy");
        Require(ComponentProjectExporter.Export(home, flat).IsValid, "Legacy first screen exports.");
        byte[] original = File.ReadAllBytes(Path.Combine(flat, "form.hpp"));
        File.WriteAllBytes(Path.Combine(root, "before.hpp"), original);
        var plan = DesignerProjectForms.PlanAdd(home, "Settings");
        Require(!File.Exists(plan.FormPath), "Add screen only proposes source changes.");
        Require(File.ReadAllText(home.ManifestPath) == plan.Before, "Plan must not mutate manifest.");
        foreach (string invalid in new[] { "../escape", "CON", "Settings/child", "bad name", "", "LPT1" })
        {
            bool failed = false;
            try { DesignerProjectForms.PlanAdd(home, invalid); } catch (Exception error) when (error is ArgumentException or InvalidDataException) { failed = true; }
            Require(failed, "Reject invalid portable screen name: " + invalid);
        }
        File.WriteAllText(plan.FormPath, plan.Xml);
        File.WriteAllText(home.ManifestPath, plan.After);
        string homePath = home.FormPath;
        home = DesignerWorkspaceService.Open(home.ManifestPath, profiles, catalog).Workspace!;
        home = DesignerProjectForms.LoadAll(home).Single(item => item.FormPath == homePath);
        var forms = DesignerProjectForms.LoadAll(home).OrderBy(item => item.Form.Id == "form.main" ? 0 : 1).ToArray();
        Require(forms.Length == 2 && forms[1].Form.Components.Length == 1 && forms[1].Form.Actions.Length == 0,
            "New screen is a blank panel in the shared project.");
        Require(forms[1].Form.Components[0].Properties.OfType<FormRectangleValue>().Single().Value == new FormRectangle(0, 0, 480, 272), "Use the shared display extent.");
        foreach (var form in forms) Require(ComponentProjectExporter.Export(form, flat).IsValid, "Export legacy layout without moving the first form.");
        Require(original.SequenceEqual(File.ReadAllBytes(Path.Combine(flat, "form.hpp"))) && File.Exists(Path.Combine(flat, "Settings/form.hpp")), "Adding a screen preserves the first include path and bytes.");
        string template = Path.Combine(root, "Generated/{form}");
        foreach (var form in forms) Require(ComponentProjectExporter.Export(form, template).IsValid, "Export per-form layout.");
        Require(File.ReadAllText(Path.Combine(root, "Generated/Settings/product_config.hpp")).Contains("namespace lithe::simulator::Settings", StringComparison.Ordinal), "Portable aliases have independent namespaces.");
        Require(DesignerProjectForms.HandlerPath(forms[1], template).EndsWith(Path.Combine("handlers", "Settings.hpp"), StringComparison.Ordinal), "Handwritten handlers have stable separate paths.");
        string settingsOutput = Path.Combine(root, "Generated/Settings/form.hpp");
        byte[] settingsBefore = File.ReadAllBytes(settingsOutput);
        File.AppendAllText(Path.Combine(root, "Generated/main/form.hpp"), "// user conflict");
        Require(!ComponentProjectExporter.Export(home, template).IsValid && settingsBefore.SequenceEqual(File.ReadAllBytes(settingsOutput)), "A conflict must preserve other forms.");
        bool collision = false;
        try { DesignerProjectForms.PlanAdd(home, "settings"); } catch (InvalidDataException) { collision = true; }
        Require(collision, "Case-insensitive screen collisions fail on every host.");
        var duplicate = forms[1].Form with { Id = home.Form.Id };
        File.WriteAllBytes(forms[1].FormPath, FormXmlWriter.Write(duplicate, catalog).Utf8!);
        Require(!ComponentProjectExporter.Export(home, template).IsValid, "Duplicate form identities block generation.");
        // Two screens select the same native converter output. CMake must compile it once.
        string fixture = Path.Combine(library, "tests/rendering/display-model-generated");
        foreach (string relative in new[] { "resources/gallop.resource.json", "assets/gallop_source.png",
            "generated/resources/gallop-alpha1.cpp", "generated/resources/gallop-r2.cpp" })
        {
            string target = Path.Combine(home.ProjectDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(fixture, relative), target);
        }
        var picture = FormXmlImporter.LoadFile(Path.Combine(fixture, "rk043fn48h-rgb565.litheview.xml"), catalog, FormImportMode.Strict).Document!;
        foreach (var screen in forms)
        {
            var content = picture with { Id = screen.Form.Id, DisplayName = screen.Form.DisplayName, SurfaceProfileId = screen.Form.SurfaceProfileId };
            File.WriteAllBytes(screen.FormPath, FormXmlWriter.Write(content, catalog).Utf8!);
        }
        File.WriteAllBytes(home.ManifestPath, DesignerProjectCanonicalizer.Canonicalize(home.Manifest with { ResourceManifests = ["resources/gallop.resource.json"] }));
        var resources = DesignerWorkspaceService.Open(home.ManifestPath, profiles, catalog).Workspace!;
        string shared = Path.Combine(root, "Shared");
        foreach (var screen in DesignerProjectForms.LoadAll(resources))
        {
            var result = ComponentProjectExporter.Export(screen, shared);
            Require(result.IsValid, "Export shared resource: " + string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        }
        // Names which previously collided with another form's asset namespace.
        foreach (string name in new[] { "Foo", "FooAssets" })
        {
            var current = DesignerWorkspaceService.Open(home.ManifestPath, profiles, catalog).Workspace!;
            var addition = DesignerProjectForms.PlanAdd(current, name);
            File.WriteAllText(addition.FormPath, addition.Xml);
            File.WriteAllText(addition.ManifestPath, addition.After);
        }
        var expanded = DesignerWorkspaceService.Open(home.ManifestPath, profiles, catalog).Workspace!;
        foreach (var screen in DesignerProjectForms.LoadAll(expanded))
            Require(ComponentProjectExporter.Export(screen, shared).IsValid, "Export collision-regression consumer inputs.");
        File.WriteAllText(Path.Combine(library, "build/multi-form-tests/latest-consumer.txt"), shared);
        string script = Path.Combine(root, "shared-sources.cmake");
        string generated = Path.Combine(root, "Shared").Replace('\\', '/');
        File.WriteAllText(script, $"include(\"{generated}/resources.cmake\")\nset(first \"${{LITHEVIEW_SELECTED_RESOURCE_SOURCES}}\")\ninclude(\"{generated}/Settings/resources.cmake\")\nif(NOT first STREQUAL LITHEVIEW_SELECTED_RESOURCE_SOURCES OR first STREQUAL \"\")\nmessage(FATAL_ERROR \"Shared native resource paths differ\")\nendif()\n");
        var start = new System.Diagnostics.ProcessStartInfo("cmake") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-P"); start.ArgumentList.Add(script);
        using var process = System.Diagnostics.Process.Start(start)!;
        string processError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Require(process.ExitCode == 0, "Shared assets must use one compiler source path: " + processError);

    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
