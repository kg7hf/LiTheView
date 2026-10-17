// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.App;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.App.Tests;

internal static partial class Program
{
    private static void NativeProjectWizardCreatesBlankForms(DesignerRepositoryContext repository)
    {
        string directory = Path.Combine(repository.RepositoryRoot, "build", "native-wizard-app-tests", Guid.NewGuid().ToString("N"));
        foreach (DisplayDefinitionLoadResult definition in NativeDisplayDefinitionFactory.LoadCatalog(
                     Path.Combine(repository.RepositoryRoot, "profiles/display-definitions")))
        {
            using NewProjectWizard wizard = new(repository);
            SelectWizardSurface(Descendants(wizard).OfType<ListBox>()
                .Single(control => control.AccessibleName == "Checked display surfaces"), definition.Profile!.DisplayName + " (native)");
            ComboBox input = Descendants(wizard).OfType<ComboBox>()
                .Single(control => control.AccessibleName == "Application logical input");
            Assert(input.Enabled, "Native display selection must keep logical input independent.");
            input.SelectedIndex = 2;
            CheckBox tutorial = Descendants(wizard).OfType<CheckBox>()
                .Single(control => control.AccessibleName == "Include button and status tutorial");
            Assert(!tutorial.Enabled && !tutorial.Checked, "Native projects begin with a blank form.");
            FindPathTextBox(wizard, "Project name").Text = definition.Profile.Format.ToString();
            FindPathTextBox(wizard, "Parent folder").Text = directory;
            Assert(wizard.TryCreateWorkspace(), "The native catalog choice must create a workspace.");
            Assert(wizard.CreatedWorkspace is { Manifest.SchemaVersion: 3, Form.Components.Length: 1 } workspace &&
                workspace.ResolvedProject.Display.Format == definition.Profile.Format &&
                workspace.ResolvedProject.Display.Width == definition.Profile.Geometry.Width &&
                workspace.ResolvedProject.Display.Input!.Encoder,
                "The desktop wizard must preserve the native format, address space and selected input.");
        }
    }

    private static void CustomNativeDialogUsesCheckedGeometry(DesignerRepositoryContext repository)
    {
        using CustomSurfaceDialog dialog = new(repository);
        FindPathTextBox(dialog, "Display name").Text = "Custom packed panel";
        ComboBox format = Descendants(dialog).OfType<ComboBox>()
            .Single(control => control.AccessibleName == "Painter color format");
        NumericUpDown width = Descendants(dialog).OfType<NumericUpDown>()
            .Single(control => control.AccessibleName == "Logical width");
        NumericUpDown height = Descendants(dialog).OfType<NumericUpDown>()
            .Single(control => control.AccessibleName == "Logical height");
        FindPathTextBox(dialog, "Source document").Text = "Custom panel specification, revision 1";
        foreach (SurfacePixelFormat native in new[] { SurfacePixelFormat.R2Packed2x2, SurfacePixelFormat.Rrrr2222, SurfacePixelFormat.Ryry2222 })
        {
            format.SelectedItem = native;
            width.Value = 24;
            height.Value = 16;
            Assert(dialog.TryCreateSurface(), "Custom native geometry must validate through the shared definition factory.");
            Assert(dialog.Definition?.Profile is { } definition && definition.Format == native &&
                definition.Geometry.Width == 24 && definition.Geometry.Height == 16 &&
                definition.Source.Status == DisplaySourceStatus.Unverified && dialog.Surface is null,
                "Custom native displays must keep declared geometry and avoid RGB staging or invented evidence.");
        }
        format.SelectedItem = SurfacePixelFormat.R2Packed2x2;
        width.Value = 23;
        Assert(!dialog.TryCreateSurface(), "An incomplete R2 storage block must not close the dialog successfully.");
    }
}
