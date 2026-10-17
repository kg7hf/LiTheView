// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using LitheView.Designer.App;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Resources;

namespace LitheView.Designer.App.Tests;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        DesignerRepositoryContext repository = DesignerRepositoryContext.Load(repositoryRoot);
        (string Name, Action Body)[] tests =
        [
            ("classic RAD shell exposes required work areas", () => ShellExposesRequiredWorkAreas(repository)),
            ("new-project workflow starts with the display contract", () => WizardStartsWithDisplayContract(repository)),
            ("native wizard creates blank forms with independent input", () => NativeProjectWizardCreatesBlankForms(repository)),
            ("custom native dialog checks storage geometry", () => CustomNativeDialogUsesCheckedGeometry(repository)),
            ("keyboard workflow exposes stable shortcuts", () => KeyboardWorkflowExposesStableShortcuts(repository)),
            ("generation workflow has no build or run actions", () =>
                SimulationCommandsExposeLifecycle(repository)),
            ("wizard separates logical input and tutorial choices", () =>
                WizardSeparatesInputAndTutorial(repository)),
            ("wizard existing folder preserves application files", () =>
                WizardExistingFolderPreservesApplication(repository)),
            ("project paths configure checked export and reject invalid settings", () =>
                ProjectPathsConfigureCheckedExport(repository)),
            ("simulation menu exports checked project and preserves application code", () =>
                SimulationMenuExportsCheckedProject(repository)),
            ("font workflow exposes deterministic display-scoped inputs", () =>
                FontWorkflowExposesDeterministicDisplayScopedInputs(repository)),
            ("image workflow exposes crop format and tint inputs", () =>
                ImageWorkflowExposesRepresentationInputs(repository)),
            ("image converter matches the checked deterministic golden", () =>
                ImageConverterMatchesDeterministicGolden(repository)),
            ("animation workflow exposes grid timing and playback inputs", () =>
                AnimationWorkflowExposesGridTimingAndPlayback(repository)),
            ("animation converter matches the checked deterministic golden", () =>
                AnimationConverterMatchesDeterministicGolden(repository)),
            ("theme workflow exposes bounded stable color tokens", ThemeWorkflowExposesBoundedTokens),
            ("schema properties are editable without markup", () => SchemaPropertiesAreEditable(repository)),
            ("action assignment exposes stable form actions", ActionAssignmentExposesStableIds)
        ];
        int failures = 0;
        foreach ((string name, Action body) in tests)
        {
            try
            {
                body();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                ++failures;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }
        Console.WriteLine($"RESULT {tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private static void ShellExposesRequiredWorkAreas(DesignerRepositoryContext repository)
    {
        using DesignerMainForm form = new(repository, startupProjectPath: null);
        string[] requiredNames =
        [
            "Project explorer",
            "Widget palette",
            "Native LitheView display preview",
            "Form component hierarchy",
            "Selected component properties",
            "Selected component actions",
            "Designer diagnostics and output",
            "Nonvisual component tray"
        ];
        HashSet<string> actual = [.. Descendants(form)
            .Select(control => control.AccessibleName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()];
        foreach (string required in requiredNames)
        {
            Assert(actual.Contains(required), $"Required work area '{required}' has no accessible identity.");
        }
    }

    private static void WizardStartsWithDisplayContract(DesignerRepositoryContext repository)
    {
        using NewProjectWizard wizard = new(repository);
        Assert(
            string.Equals(wizard.AccessibleName, "New LitheView project wizard", StringComparison.Ordinal),
            "New-project wizard has no stable accessible identity.");
        ListBox? surfaceList = Descendants(wizard).OfType<ListBox>().SingleOrDefault(
            control => control.AccessibleName == "Checked display surfaces");
        Assert(surfaceList is not null && surfaceList.Items.Count >= 5, "Display selection is not the first workflow.");
        ListView? details = Descendants(wizard).OfType<ListView>().SingleOrDefault(
            control => control.AccessibleName == "Selected surface engineering details");
        Assert(details is not null && details.Items.Count >= 8, "Engineering surface details are not visible.");
    }

    private static void KeyboardWorkflowExposesStableShortcuts(DesignerRepositoryContext repository)
    {
        using DesignerMainForm form = new(repository, startupProjectPath: null);
        Dictionary<string, Keys> shortcuts = Descendants(form)
            .OfType<MenuStrip>()
            .SelectMany(menu => menu.Items.OfType<ToolStripMenuItem>())
            .SelectMany(Flatten)
            .Where(item => item.ShortcutKeys != Keys.None)
            .ToDictionary(item => item.Text!.Replace("&", string.Empty, StringComparison.Ordinal),
                item => item.ShortcutKeys,
                StringComparer.Ordinal);
        Assert(shortcuts["New Project..."] == (Keys.Control | Keys.N), "New Project shortcut changed.");
        Assert(shortcuts["Open Project..."] == (Keys.Control | Keys.O), "Open Project shortcut changed.");
        Assert(shortcuts["Save"] == (Keys.Control | Keys.S), "Save shortcut changed.");
        Assert(shortcuts["Undo"] == (Keys.Control | Keys.Z), "Undo shortcut changed.");
        Assert(shortcuts["Redo"] == (Keys.Control | Keys.Y), "Redo shortcut changed.");
        Assert(shortcuts["Delete"] == Keys.Delete, "Delete shortcut changed.");
        Assert(shortcuts["Duplicate"] == (Keys.Control | Keys.D), "Duplicate shortcut changed.");
        Assert(shortcuts["Edit Text"] == Keys.F2, "In-place text shortcut changed.");
        Assert(
            shortcuts["Import Font..."] == (Keys.Control | Keys.Alt | Keys.F),
            "Font import shortcut changed.");
        Assert(
            shortcuts["Import Image..."] == (Keys.Control | Keys.Alt | Keys.I),
            "Image import shortcut changed.");
        Assert(
            shortcuts["Import Icon..."] == (Keys.Control | Keys.Alt | Keys.K),
            "Icon import shortcut changed.");
        Assert(
            shortcuts["Import Animation..."] == (Keys.Control | Keys.Alt | Keys.A),
            "Animation import shortcut changed.");
        Assert(
            shortcuts["Edit Theme Color Tokens..."] == (Keys.Control | Keys.Alt | Keys.T),
            "Theme token editor shortcut changed.");
        Assert(
            shortcuts["Bring to Front"] == (Keys.Control | Keys.Shift | Keys.OemCloseBrackets),
            "Bring-to-front shortcut changed.");
        TreeView? hierarchy = Descendants(form).OfType<TreeView>().SingleOrDefault(
            control => control.AccessibleName == "Form component hierarchy");
        Assert(hierarchy?.AllowDrop == true, "Hierarchy reparenting is not enabled.");
        PreviewCanvas? canvas = Descendants(form).OfType<PreviewCanvas>().SingleOrDefault();
        Assert(
            canvas is { ShowGrid: true, SnapToGrid: true, GridSize: 8 },
            "The checked grid and snap defaults changed.");
    }

    private static void FontWorkflowExposesDeterministicDisplayScopedInputs(
        DesignerRepositoryContext repository)
    {
        string manifestPath = Path.Combine(
            repository.RepositoryRoot,
            "tests",
            "designer-fixtures",
            "document-compiler-smoke",
            DesignerWorkspaceService.ManifestFileName);
        using FontImportDialog dialog = new(manifestPath, SurfacePixelFormat.Rgb565);
        HashSet<string> accessibleNames = [.. Descendants(dialog)
            .Select(control => control.AccessibleName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()];
        string[] required =
        [
            "Source font file",
            "Font resource ID",
            "Font pixel size",
            "Font visual weight",
            "Font coverage",
            "Font repertoire",
            "Selected font repertoire preview",
            "Font origin",
            "Font SPDX license ID",
            "Font redistribution approval"
        ];
        foreach (string name in required)
        {
            Assert(accessibleNames.Contains(name), $"Font workflow input '{name}' is absent.");
        }
        string allText = string.Join(
            " ",
            Descendants(dialog).Select(control => control.Text));
        Assert(
            !allText.Contains("target profile", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("internal flash", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("external flash", StringComparison.OrdinalIgnoreCase),
            "Font resource editing leaked deployment policy into the visual designer.");
    }

    private static void ImageWorkflowExposesRepresentationInputs(
        DesignerRepositoryContext repository)
    {
        string manifestPath = Path.Combine(
            repository.RepositoryRoot,
            "tests",
            "designer-fixtures",
            "document-compiler-smoke",
            DesignerWorkspaceService.ManifestFileName);
        using ImageImportDialog dialog = new(
            manifestPath,
            SurfacePixelFormat.Xrgb8888,
            ResourceKind.Icon);
        HashSet<string> accessibleNames = [.. Descendants(dialog)
            .Select(control => control.AccessibleName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()];
        string[] required =
        [
            "Source raster image file",
            "Image resource ID",
            "Image crop X",
            "Image crop width",
            "Image output width",
            "Image bitmap format",
            "Image scale sampling",
            "Image alpha handling",
            "Image dither mode",
            "Tintable icon mask",
            "Icon preview tint",
            "Indexed4 ARGB palette",
            "Image crop and tint preview",
            "Image origin",
            "Image SPDX license ID",
            "Image redistribution approval"
        ];
        foreach (string name in required)
        {
            Assert(accessibleNames.Contains(name), $"Image workflow input '{name}' is absent.");
        }
        string allText = string.Join(" ", Descendants(dialog).Select(control => control.Text));
        Assert(
            !allText.Contains("target profile", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("internal flash", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("external flash", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("QSPI", StringComparison.OrdinalIgnoreCase),
            "Image resource editing leaked deployment policy into the visual designer.");
    }

    private static void ImageConverterMatchesDeterministicGolden(
        DesignerRepositoryContext repository)
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"litheview-image-golden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string inputPath = Path.Combine(
                repository.RepositoryRoot,
                "tests", "rendering", "assets",
                "animations",
                "gallop_source.png");
            string firstSource = Path.Combine(temporaryDirectory, "first.cpp");
            string firstMetrics = Path.Combine(temporaryDirectory, "first.json");
            string secondSource = Path.Combine(temporaryDirectory, "second.cpp");
            string secondMetrics = Path.Combine(temporaryDirectory, "second.json");
            ImageConversionRequest template = new()
            {
                InputImagePath = inputPath,
                OutputSourcePath = firstSource,
                OutputMetricsPath = firstMetrics,
                Symbol = "GallopPreview",
                EntryPoint = "gallopPreview",
                Kind = ResourceKind.Image,
                Tintable = false,
                Format = ResourceFormat.Indexed4,
                CropX = 0,
                CropY = 0,
                CropWidth = 1536,
                CropHeight = 1024,
                OutputWidth = 32,
                OutputHeight = 24,
                ScaleMode = ImageScaleMode.Bilinear,
                AlphaMode = ImageAlphaMode.Preserve,
                AlphaThreshold = 64,
                Dither = ImageDitherMode.Ordered4x4,
                TintArgb = "ffffffff",
                MatteArgb = "ff000000",
                PaletteArgb =
                    "00000000,ff141519,ff2c2d35,ff543422,ff7d321f,ffa94a22," +
                    "ffc96936,ffe6a548,fff5c96a,ff2bb7ad"
            };
            string converterPath = Path.Combine(repository.RepositoryRoot, "tools", "convert-image.ps1");
            PowerShellImageConversionRunner runner = new(converterPath);
            ImageConversionResult first = runner.ConvertAsync(template).GetAwaiter().GetResult();
            ImageConversionResult second = runner.ConvertAsync(template with
            {
                OutputSourcePath = secondSource,
                OutputMetricsPath = secondMetrics
            }).GetAwaiter().GetResult();

            Assert(first.IsValid, string.Join("; ", first.Diagnostics.Select(item => item.Message)));
            Assert(second.IsValid, string.Join("; ", second.Diagnostics.Select(item => item.Message)));
            ImageConversionMetrics metrics = first.Metrics ??
                throw new InvalidOperationException("The golden conversion returned no metrics.");
            Assert(metrics.SourceSha256 ==
                "71656c0af13f37460e70d9d674867ea6787fef02767ee7c461089d9926d47344",
                "The golden source-image hash changed.");
            Assert(metrics.PixelBytes == 384L, "The golden pixel payload changed.");
            Assert(metrics.PaletteBytes == 40L, "The golden palette payload changed.");
            Assert(metrics.EstimatedRomBytes == 424L, "The golden representation size changed.");
            Assert(metrics.PixelChecksumFnv1a32 == "0xd92b3725", "The golden pixel checksum changed.");
            Assert(metrics.PaletteChecksumFnv1a32 == "0xffcb16b1", "The golden palette checksum changed.");
            Assert(metrics.DataChecksumFnv1a32 == "0xfbae5dd1", "The golden data checksum changed.");
            Assert(
                File.ReadAllBytes(firstSource).SequenceEqual(File.ReadAllBytes(secondSource)),
                "Identical image conversions generated different C++.");
            Assert(
                File.ReadAllBytes(firstMetrics).SequenceEqual(File.ReadAllBytes(secondMetrics)),
                "Identical image conversions generated different metrics.");
            string generated = File.ReadAllText(firstSource);
            Assert(
                generated.Contains("static_assert(kGallopPreviewBitmap.valid());", StringComparison.Ordinal),
                "Generated image C++ lost its compile-time validity check.");
            Assert(
                generated.Contains("// clang-format off", StringComparison.Ordinal) &&
                generated.Contains("// clang-format on", StringComparison.Ordinal),
                "Generated packed tables lost their formatter guards.");
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void AnimationWorkflowExposesGridTimingAndPlayback(
        DesignerRepositoryContext repository)
    {
        string manifestPath = Path.Combine(
            repository.RepositoryRoot,
            "tests",
            "designer-fixtures",
            "document-compiler-smoke",
            DesignerWorkspaceService.ManifestFileName);
        using AnimationImportDialog dialog = new(manifestPath, SurfacePixelFormat.Xrgb8888);
        HashSet<string> accessibleNames = [.. Descendants(dialog)
            .Select(control => control.AccessibleName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()];
        string[] required =
        [
            "Source animation sprite sheet",
            "Animation resource ID",
            "Animation sheet X",
            "Animation source frame width",
            "Animation grid columns",
            "Animation frame count",
            "Animation output frame width",
            "Animation bitmap format",
            "Animation scale sampling",
            "Animation alpha handling",
            "Animation dither mode",
            "Tintable animation frames",
            "Animation preview tint",
            "Animation indexed4 palette",
            "Animation frame interval",
            "Animation repeat index",
            "Animation autoplay",
            "Animation frame-grid and playback preview",
            "Animation origin",
            "Animation SPDX license ID",
            "Animation redistribution approval"
        ];
        foreach (string name in required)
        {
            Assert(accessibleNames.Contains(name), $"Animation workflow input '{name}' is absent.");
        }
        string allText = string.Join(" ", Descendants(dialog).Select(control => control.Text));
        Assert(
            !allText.Contains("target profile", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("internal flash", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("external flash", StringComparison.OrdinalIgnoreCase) &&
            !allText.Contains("QSPI", StringComparison.OrdinalIgnoreCase),
            "Animation resource editing leaked deployment policy into the visual designer.");
    }

    private static void AnimationConverterMatchesDeterministicGolden(
        DesignerRepositoryContext repository)
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"litheview-animation-golden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string inputPath = Path.Combine(
                repository.RepositoryRoot,
                "tests", "rendering", "assets",
                "animations",
                "gallop_sprite_sheet.png");
            string firstSource = Path.Combine(temporaryDirectory, "first.cpp");
            string firstMetrics = Path.Combine(temporaryDirectory, "first.json");
            string secondSource = Path.Combine(temporaryDirectory, "second.cpp");
            string secondMetrics = Path.Combine(temporaryDirectory, "second.json");
            AnimationConversionRequest template = new()
            {
                InputSpriteSheetPath = inputPath,
                OutputSourcePath = firstSource,
                OutputMetricsPath = firstMetrics,
                Symbol = "GallopAnimation",
                EntryPoint = "gallopAnimation",
                Tintable = false,
                Format = ResourceFormat.Indexed4,
                SheetX = 0,
                SheetY = 0,
                SourceFrameWidth = 128,
                SourceFrameHeight = 96,
                Columns = 4,
                FrameCount = 8,
                OutputWidth = 96,
                OutputHeight = 72,
                ScaleMode = ImageScaleMode.Bilinear,
                AlphaMode = ImageAlphaMode.Preserve,
                AlphaThreshold = 64,
                Dither = ImageDitherMode.Ordered4x4,
                TintArgb = "ffffffff",
                MatteArgb = "ff000000",
                PaletteArgb =
                    "00000000,ff141519,ff2c2d35,ff543422,ff7d321f,ffa94a22," +
                    "ffc96936,ffe6a548,fff5c96a,ff2bb7ad",
                FrameIntervalMilliseconds = 100,
                RepeatIndex = 0,
                Autoplay = true
            };
            string converterPath = Path.Combine(repository.RepositoryRoot, "tools", "convert-animation.ps1");
            PowerShellAnimationConversionRunner runner = new(converterPath);
            AnimationConversionResult first = runner.ConvertAsync(template).GetAwaiter().GetResult();
            AnimationConversionResult second = runner.ConvertAsync(template with
            {
                OutputSourcePath = secondSource,
                OutputMetricsPath = secondMetrics
            }).GetAwaiter().GetResult();

            Assert(first.IsValid, string.Join("; ", first.Diagnostics.Select(item => item.Message)));
            Assert(second.IsValid, string.Join("; ", second.Diagnostics.Select(item => item.Message)));
            AnimationConversionMetrics metrics = first.Metrics ??
                throw new InvalidOperationException("The golden animation conversion returned no metrics.");
            Assert(metrics.SourceSha256 ==
                "5adbcac3e1ebe185e4e4e7f78bad6e9036e766d0555db1e1a76c3c65286f0ebe",
                "The golden sprite-sheet hash changed.");
            Assert(metrics.FrameCount == 8 && metrics.FramePixelBytes == 3456L,
                "The golden frame layout changed.");
            Assert(metrics.PixelBytes == 27648L, "The golden animation pixel payload changed.");
            Assert(metrics.PaletteBytes == 40L, "The golden animation palette payload changed.");
            Assert(metrics.EstimatedRomBytes == 27688L, "The golden animation representation size changed.");
            Assert(metrics.PixelChecksumFnv1a32 == "0x201d91e6", "The golden animation pixel checksum changed.");
            Assert(metrics.PaletteChecksumFnv1a32 == "0xffcb16b1", "The golden animation palette checksum changed.");
            Assert(metrics.DataChecksumFnv1a32 == "0xe1d19ad2", "The golden animation data checksum changed.");
            Assert(
                File.ReadAllBytes(firstSource).SequenceEqual(File.ReadAllBytes(secondSource)),
                "Identical animation conversions generated different C++.");
            Assert(
                File.ReadAllBytes(firstMetrics).SequenceEqual(File.ReadAllBytes(secondMetrics)),
                "Identical animation conversions generated different metrics.");
            string generated = File.ReadAllText(firstSource);
            Assert(
                generated.Contains("std::span<const Bitmap> gallopAnimation() noexcept", StringComparison.Ordinal),
                "Generated animation C++ lost its borrowed frame accessor.");
            Assert(
                generated.Contains("gallopAnimation-f0", StringComparison.Ordinal) &&
                generated.Contains("gallopAnimation-f7", StringComparison.Ordinal),
                "Generated animation C++ lost its concrete ResourceProvider naming walkthrough.");
            Assert(
                generated.Contains("// clang-format off", StringComparison.Ordinal) &&
                generated.Contains("// clang-format on", StringComparison.Ordinal),
                "Generated animation tables lost their formatter guards.");
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void SchemaPropertiesAreEditable(DesignerRepositoryContext repository)
    {
        string manifestPath = Path.Combine(
            repository.RepositoryRoot,
            "tests",
            "designer-fixtures",
            "document-compiler-smoke",
            "project.litheview.json");
        DesignerWorkspaceLoadResult result = DesignerWorkspaceService.Open(
            manifestPath,
            repository.Profiles,
            repository.Catalog);
        Assert(result.IsValid && result.Workspace is not null, "Checked designer workspace did not open.");
        DesignerDocumentSession session = new(result.Workspace!.Form, repository.Catalog);
        List<FormEditResult> edits = [];
        ComponentInspectorModel inspector = new(
            session,
            repository.Catalog,
            "component.main.open",
            edits.Add);
        PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(inspector);
        PropertyDescriptor? visible = properties.Find("visible", ignoreCase: false);
        PropertyDescriptor? foreground = properties.Find("foreground-color", ignoreCase: false);
        Assert(visible is not null && foreground is not null, "Schema-backed properties are absent.");
        visible!.SetValue(inspector, false);
        Assert(edits.Last().IsValid, "Schema-backed property edit failed validation.");
        foreground!.SetValue(inspector, "#11223344");
        Assert(edits.Last().IsValid, "Schema-backed color edit failed validation.");
        FormBooleanValue? stored = session.Document.Components
            .Single(item => item.ComponentId == "component.main.open")
            .Properties.OfType<FormBooleanValue>()
            .SingleOrDefault(item => item.PropertyId == "visible");
        Assert(stored?.Value == false, "Schema-backed property edit did not reach the document.");
        FormColorValue? storedColor = session.Document.Components
            .Single(item => item.ComponentId == "component.main.open")
            .Properties.OfType<FormColorValue>()
            .SingleOrDefault(item => item.PropertyId == "foreground-color");
        Assert(storedColor?.EncodedColor == 0x11223344U, "Hex color editing changed the encoded value.");
        ComponentInspectorModel panelInspector = new(
            session,
            repository.Catalog,
            "component.main.root",
            edits.Add);
        Assert(
            TypeDescriptor.GetProperties(panelInspector).Find("Text", ignoreCase: false) is null,
            "The inspector exposed an unsupported panel text property.");
    }

    private static void ThemeWorkflowExposesBoundedTokens()
    {
        using ThemeColorEditorDialog dialog = new([]);
        HashSet<string> accessible = [.. Descendants(dialog)
            .Select(control => control.AccessibleName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()];
        string[] required =
        [
            "Theme color token list",
            "Theme token stable ID",
            "Theme token encoded color",
            "Theme token color preview",
            "Add standard theme tokens"
        ];
        foreach (string name in required)
        {
            Assert(accessible.Contains(name), $"Theme editor lost accessible control '{name}'.");
        }
        Button standard = Descendants(dialog).OfType<Button>().Single(
            control => control.AccessibleName == "Add standard theme tokens");
        dialog.Show();
        Application.DoEvents();
        standard.PerformClick();
        int added = dialog.ThemeColors.Length;
        // A second click must not duplicate tokens that already exist.
        standard.PerformClick();
        dialog.Hide();
        // Ten standard tokens, including the selected pair the toggle-button slice introduced.
        Assert(
            added == 10 && dialog.ThemeColors.Length == added &&
            dialog.ThemeColors.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == added &&
            dialog.ThemeColors.All(item => item.Id.StartsWith("theme.", StringComparison.Ordinal)),
            "Standard theme token creation is not bounded and stable.");
    }

    private static void ActionAssignmentExposesStableIds()
    {
        FormAction[] actions =
        [
            new FormAction { Id = "action.back", RuntimeId = 100, Symbol = "Back" },
            new FormAction { Id = "action.open", RuntimeId = 101, Symbol = "Open" }
        ];
        using ActionAssignmentDialog dialog = new("Activate", actions, "action.open");
        ComboBox? assignment = Descendants(dialog).OfType<ComboBox>().SingleOrDefault(
            control => control.AccessibleName == "Form action assignment");
        Assert(
            assignment?.SelectedItem?.ToString()?.Contains("action.open", StringComparison.Ordinal) == true,
            "Current stable action ID is not selected in the assignment editor.");
    }

    private static IEnumerable<ToolStripMenuItem> Flatten(ToolStripMenuItem root)
    {
        yield return root;
        foreach (ToolStripMenuItem child in root.DropDownItems.OfType<ToolStripMenuItem>())
        {
            foreach (ToolStripMenuItem descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "schema", "widgets.json")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
