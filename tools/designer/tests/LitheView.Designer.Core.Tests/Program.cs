// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Schema;
using LitheView.Designer.Schema.Generated;

namespace LitheView.Designer.Core.Tests;

internal static class Program
{
    private const string H750ProfileSha256 =
        "335b8cf89656605b35abfdd8c55796665082fbf0b1af763e6fb17d728982ef10";
    private const string RoundProfileSha256 =
        "64fb13970d93d65df209ecee57e1795a88cffc7514d7716d95564c5a699963a8";
    private const string SyntheticTargetSha256 =
        "41ebb2fff109c3eb87d6f3f3916bbded5d8e44958dad80f28060130e285f5fdf";
    private const string U545TargetSha256 =
        "8b81386d6babfedaa8e957fcc99cd6f03975cf47506578b7fa363c875291ccb2";
    private const string ResolvedProjectSha256 =
        "c0f365abc1497c37d555b4f98aaa7a1854955e6ef9809d12250a95f026e6b84d";
    private const string WidgetCatalogSha256 =
        "0a62fd6a3f77b871d0c3a1e40c1cd6a5210ac4f1c3647b542fc3487d8ea03016";

    private sealed record TestCase(string Name, Action Body);

    public static int Main()
    {
        string repositoryRoot = FindRepositoryRoot();
        string h750ProfilePath = Path.Combine(
            repositoryRoot,
            "profiles",
            "display-surfaces",
            "stm32h750b-dk-rk043fn48h-rgb565.json");
        string roundProfilePath = Path.Combine(
            repositoryRoot,
            "profiles",
            "display-surfaces",
            "gc9a01-1.28-round-240-rgb565.json");
        string targetDirectory = Path.Combine(repositoryRoot, "profiles", "targets");
        string syntheticTargetPath = Path.Combine(
            targetDirectory,
            "synthetic-m7-rgb565-full-frame.json");
        string u545TargetPath = Path.Combine(
            targetDirectory,
            "stm32u545-standard-rgb565-tiled.json");
        string projectPath = Path.Combine(
            repositoryRoot,
            "tests",
            "designer-fixtures",
            "display-profile-smoke",
            "project.litheview.json");
        string surfaceDirectory = Path.Combine(repositoryRoot, "profiles", "display-surfaces");
        string widgetCatalogPath = Path.Combine(repositoryRoot, "schema", "widgets.json");
        string resourceProjectRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "designer-fixtures",
            "resource-manifest-smoke");
        string resourceManifestPath = Path.Combine(
            resourceProjectRoot,
            "resources",
            "status-icon.resource.json");

        TestCase[] tests =
        [
            new("segmented glass and form compilation are bounded and preserve ownership", () => SegmentDesignerTests.Run(repositoryRoot)),
            new("multi-form output and screen creation preserve ownership", () => MultiFormTests.Run(repositoryRoot)),
            new("component generation owns no application and migrates safely", () => ComponentExportTests.Run(repositoryRoot)),
            new("display definitions remain independent of targets", () => DisplayDefinitionTests.Run(repositoryRoot)),
            new("designer products persist independent display and input contracts", () => DesignerProductTests.Run(repositoryRoot)),
            new("native display projects resolve without an editing surface or target", () => NativeDisplayProjectTests.Run(repositoryRoot)),
            new("native display projects open, edit, save, preview and generate", () =>
                NativeDisplayWorkflowTests.Run(repositoryRoot, PreviewWorkerPath(repositoryRoot))),
            new("designer project paths preserve source ownership and relocation", () => DesignerProjectPathTests.Run(repositoryRoot)),
            new("target-free application export is deterministic", () => DisplayApplicationTests.Run(repositoryRoot)),
            new("simulation export preserves application ownership and target independence", () => SimulatorProjectTests.Run(repositoryRoot)),
            new("surface layout and input share portable diagnostics", () => SurfaceLayoutTests.Run(repositoryRoot)),
            new("legacy budget evidence remains unknown until measured", () => LegacyBudgetEvidenceTests.Run(repositoryRoot)),
            new("target feasibility preserves independent evidence stages", () => TargetFeasibilityTests.Run(repositoryRoot)),
            new("native resource storage metrics are checked", () => NativeResourceTests.LayoutAndMetricsAreChecked(repositoryRoot)),
            new("native resource selection and provider are deterministic", () => NativeResourceTests.SelectionAndProviderAreDeterministic(repositoryRoot)),
            new("native conversion matches an independent packing oracle", () => NativeResourceTests.ConverterMatchesIndependentPackingOracle(repositoryRoot)),
            new("checked profiles validate", () => CheckedProfilesValidate(h750ProfilePath, roundProfilePath)),
            new("checked surface catalog validates", () => CheckedSurfaceCatalogValidates(surfaceDirectory)),
            new("duplicate catalog IDs fail closed", () => DuplicateCatalogIdsFailClosed(h750ProfilePath)),
            new("duplicate properties fail", () => DuplicatePropertiesFail(h750ProfilePath)),
            new("unknown properties fail", () => UnknownPropertiesFail(h750ProfilePath)),
            new("null required values fail", () => NullRequiredValuesFail(h750ProfilePath)),
            new("oversized profiles fail", OversizedProfilesFail),
            new("circle geometry fails closed", () => CircleGeometryFailsClosed(roundProfilePath)),
            new("capability order is canonical", () => CapabilityOrderIsCanonical(h750ProfilePath)),
            new("surface semantics affect identity", () => SurfaceSemanticsAffectIdentity(h750ProfilePath)),
            new("pixel format combinations fail closed", () => PixelFormatCombinationsFailClosed(h750ProfilePath)),
            new("native transfer depth fails closed", () => NativeTransferDepthFailsClosed(h750ProfilePath)),
            new(
                "checked targets validate",
                () => CheckedTargetsValidate(targetDirectory, syntheticTargetPath, u545TargetPath)),
            new(
                "project resolves its locked display surface",
                () => ProjectResolves(projectPath, surfaceDirectory)),
            new(
                "all project fixtures resolve",
                () => AllProjectFixturesResolve(
                    Path.Combine(repositoryRoot, "tests", "designer-fixtures"),
                    surfaceDirectory)),
            new(
                "project factory locks the selected display surface",
                () => ProjectFactoryLocksSelectedProfiles(
                    h750ProfilePath,
                    surfaceDirectory)),
            new(
                "designer workspace creation is canonical and contained",
                () => DesignerWorkspaceCreationIsCanonical(
                    surfaceDirectory,
                    h750ProfilePath,
                    widgetCatalogPath)),
            new(
                "designer workspace creation never overwrites",
                () => DesignerWorkspaceCreationNeverOverwrites(
                    surfaceDirectory,
                    h750ProfilePath,
                    widgetCatalogPath)),
            new(
                "project-local custom surface snapshot resolves",
                () => ProjectLocalCustomSurfaceSnapshotResolves(
                    surfaceDirectory,
                    h750ProfilePath,
                    widgetCatalogPath)),
            new(
                "project surface lock fails closed",
                () => ProjectSurfaceLockFails(projectPath, surfaceDirectory)),
            new("project paths stay contained", () => ProjectPathsStayContained(projectPath)),
            new("surface migration is explicit", () => SurfaceMigrationIsExplicit(h750ProfilePath)),
            new("checked widget catalog validates", () => CheckedWidgetCatalogValidates(widgetCatalogPath)),
            new("widget catalog structure fails closed", () => WidgetCatalogStructureFailsClosed(widgetCatalogPath)),
            new("widget semantics fail closed", () => WidgetSemanticsFailClosed(widgetCatalogPath)),
            new("widget catalog order is canonical", () => WidgetCatalogOrderIsCanonical(widgetCatalogPath)),
            new("widget compatibility fails closed", () => WidgetCompatibilityFailsClosed(widgetCatalogPath)),
            new("widget rename migration is deterministic", () => WidgetRenameMigrationIsDeterministic(widgetCatalogPath)),
            new("widget compiler is deterministic", () => WidgetCompilerIsDeterministic(widgetCatalogPath)),
            new("generated widget artifacts are fresh", () => GeneratedWidgetArtifactsAreFresh(repositoryRoot, widgetCatalogPath)),
            new("typed form reaches canonical XML fixed point", () => TypedFormReachesFixedPoint(widgetCatalogPath)),
            new("resource metadata reaches canonical XML fixed point", () => ResourceMetadataReachesFixedPoint(
                widgetCatalogPath)),
            new("legacy form normalization is explicit", () => LegacyFormNormalizationIsExplicit(widgetCatalogPath)),
            new("strict form import rejects unsafe or lossy XML", () => StrictFormImportRejectsUnsafeXml(widgetCatalogPath)),
            new("form symbols fail closed on collision", () => FormSymbolsFailClosed(widgetCatalogPath)),
            new("form budgets remain explicit deployment analysis", () => FormBudgetsRemainExplicitDeploymentAnalysis(
                widgetCatalogPath,
                u545TargetPath)),
            new("form C++ compiler is deterministic", () => FormCppCompilerIsDeterministic(widgetCatalogPath)),
            new("generated user regions preserve exact bodies", () => GeneratedUserRegionsPreserveExactBodies(
                widgetCatalogPath)),
            new("malformed user regions fail closed", () => MalformedUserRegionsFailClosed(widgetCatalogPath)),
            new("every widget property reaches XML and C++", () => EveryWidgetPropertyReachesXmlAndCpp(
                widgetCatalogPath)),
            new("button variants reach XML and C++", () => ButtonVariantTests.VariantsReachXmlAndCpp(
                widgetCatalogPath)),
            new("momentary buttons cannot latch", () => ButtonVariantTests.MomentaryButtonsCannotLatch(
                widgetCatalogPath)),
            new("preview input scripts are bounded", ButtonVariantTests.PreviewInputScriptIsBounded),
            new("tabs reach XML and C++", () => TabViewTests.TabsReachXmlAndCpp(widgetCatalogPath)),
            new("tab rules fail closed", () => TabViewTests.TabRulesFailClosed(widgetCatalogPath)),
            new("session builds pages and hides unselected ones", () =>
                TabViewTests.SessionBuildsPagesAndHidesUnselectedOnes(widgetCatalogPath)),
            new("wheel reaches XML and C++", () => WheelPickerTests.WheelReachesXmlAndCpp(widgetCatalogPath)),
            new("wheel rules fail closed", () => WheelPickerTests.WheelRulesFailClosed(widgetCatalogPath)),
            new("session adds a wheel with starter items", () =>
                WheelPickerTests.SessionAddsAWheelWithStarterItems(widgetCatalogPath)),
            new("reparent into a later container keeps document order", () =>
                EditingParityTests.ReparentIntoLaterContainerKeepsDocumentOrder(widgetCatalogPath)),
            new("theme tokens rename and delete safely", () =>
                EditingParityTests.ThemeTokensRenameAndDeleteSafely(widgetCatalogPath)),
            new("generated form fixture is fresh", () => GeneratedFormFixtureIsFresh(
                repositoryRoot,
                widgetCatalogPath,
                "document-compiler-smoke",
                "main_form")),
            new("generated typed control fixture is fresh", () =>
            {
                string root = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "control-handlers");
                WidgetCatalog catalog = LoadWidgetCatalog(widgetCatalogPath);
                FormDocumentLoadResult form = FormXmlImporter.LoadFile(Path.Combine(root, "form.xml"), catalog, FormImportMode.Strict);
                AssertValid(form);
                FormCppCompilationResult compiled = FormCppCompiler.Compile(form.Document!, catalog);
                AssertValid(compiled);
                AssertGeneratedFile(Path.Combine(root, "form.hpp"), compiled.HeaderUtf8!);
            }),
            new("generated event fixture is fresh", () =>
            {
                string root = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "control-events");
                WidgetCatalog catalog = LoadWidgetCatalog(widgetCatalogPath);
                FormDocumentLoadResult form = FormXmlImporter.LoadFile(Path.Combine(root, "form.xml"), catalog, FormImportMode.Strict);
                AssertValid(form);
                FormCppCompilationResult compiled = FormCppCompiler.Compile(form.Document!, catalog);
                AssertValid(compiled);
                AssertGeneratedFile(Path.Combine(root, "form.hpp"), compiled.HeaderUtf8!);
            }),
            new("generated text event fixture is fresh", () =>
            {
                string root = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "text-events");
                WidgetCatalog catalog = LoadWidgetCatalog(widgetCatalogPath);
                FormDocumentLoadResult form = FormXmlImporter.LoadFile(Path.Combine(root, "form.xml"), catalog, FormImportMode.Strict);
                AssertValid(form);
                FormCppCompilationResult compiled = FormCppCompiler.Compile(form.Document!, catalog);
                AssertValid(compiled);
                AssertGeneratedFile(Path.Combine(root, "form.hpp"), compiled.HeaderUtf8!);
                string header = Encoding.UTF8.GetString(compiled.HeaderUtf8!);
                Assert(header.Contains("kControlEventContractVersion >= 2U", StringComparison.Ordinal),
                    "Text handlers must diagnose old Core dependencies at compile time.");
                Assert(string.Join(",", ControlEventCatalog.For("textField").Select(item => item.ParameterType)) ==
                    "lithe::TextChange,lithe::TextEdit,lithe::TextEdit",
                    "Text payloads must not reuse index-based selection events.");
            }),
            new("selectable-list palette and typed handlers", () =>
            {
                string root = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "selectable-list");
                WidgetCatalog catalog = LoadWidgetCatalog(widgetCatalogPath);
                FormDocumentLoadResult form = FormXmlImporter.LoadFile(Path.Combine(root, "form.xml"), catalog, FormImportMode.Strict);
                AssertValid(form);
                FormCppCompilationResult compiled = FormCppCompiler.Compile(form.Document!, catalog);
                AssertValid(compiled);
                AssertGeneratedFile(Path.Combine(root, "form.hpp"), compiled.HeaderUtf8!);
                string header = Encoding.UTF8.GetString(compiled.HeaderUtf8!);
                Assert(header.Contains("kControlEventContractVersion >= 4U", StringComparison.Ordinal), "List needs Core contract 4.");
                Assert(header.Contains("onEntryListSelectionChanged([[maybe_unused]] lithe::SelectionChange", StringComparison.Ordinal) &&
                    header.Contains("onEntryListItemActivated([[maybe_unused]] lithe::ItemActivation", StringComparison.Ordinal),
                    "Generated list handlers must separate selection from activation.");
                Assert(ControlEventCatalog.For("list").Select(item => item.Id).SequenceEqual(
                    ["selectionChanged", "itemActivated"]), "List handler catalog differs.");
                DesignerDocumentSession session = new(form.Document!, catalog);
                Assert(session.AddWidget("lithe.widget.list", "root").IsValid, "Palette inserts an empty list awaiting application data.");
                session.Undo();
                Assert(!session.SetProperty("entryList", new FormTextValue("items", new string('|', 64))).IsValid,
                    "More than 64 preview items rejected.");
                Assert(!session.SetProperty("entryList", new FormUnsignedIntegerValue("selected-index", 6U)).IsValid,
                    "Selected index must identify an item.");
                FormXmlWriteResult saved = FormXmlWriter.Write(session.Document, catalog);
                AssertValid(saved);
                AssertValid(FormXmlImporter.Load(saved.Utf8!, catalog, FormImportMode.Strict));
            }),
            new("drop-down palette, properties, round trip and generated events", () =>
            {
                string root = Path.Combine(repositoryRoot, "tests", "designer-fixtures", "drop-down-events");
                WidgetCatalog catalog = LoadWidgetCatalog(widgetCatalogPath);
                FormDocumentLoadResult form = FormXmlImporter.LoadFile(Path.Combine(root, "form.xml"), catalog, FormImportMode.Strict);
                AssertValid(form);
                FormCppCompilationResult compiled = FormCppCompiler.Compile(form.Document!, catalog);
                AssertValid(compiled);
                AssertGeneratedFile(Path.Combine(root, "form.hpp"), compiled.HeaderUtf8!);
                string header = Encoding.UTF8.GetString(compiled.HeaderUtf8!);
                Assert(header.Contains("kControlEventContractVersion >= 3U", StringComparison.Ordinal), "Drop-down needs Core contract 3.");
                Assert(ControlEventCatalog.For("dropDown").Select(item => item.Id).SequenceEqual(
                    ["selectionChanged", "editCommitted", "editCancelled"]), "Drop-down handler catalog differs.");
                DesignerDocumentSession session = new(form.Document!, catalog);
                Assert(session.AddWidget("lithe.widget.drop-down", "root").IsValid, "Palette insertion provides a valid item model.");
                session.Undo();
                session.Redo();
                AssertValid(FormCppCompiler.Compile(session.Document, catalog));
                session.Undo();
                Assert(!session.SetProperty("mode", new FormTextValue("items", "")).IsValid, "Empty items rejected.");
                Assert(!session.SetProperty("mode", new FormTextValue("items", new string('|', 255))).IsValid, "Too many items rejected.");
                Assert(!session.SetProperty("mode", new FormUnsignedIntegerValue("selected-index", 3U)).IsValid, "Invalid index rejected.");
                Assert(!session.SetProperty("mode", new FormUnsignedIntegerValue("row-height", 11U)).IsValid, "Short rows rejected.");
                Assert(session.SetProperty("mode", new FormUnsignedIntegerValue("selected-index", 2U)).IsValid, "Valid index accepted.");
                Assert(session.SetProperty("mode", new FormUnsignedIntegerValue("row-height", 24U)).IsValid, "Row height accepted.");
                Assert(session.Rename("mode", "operatingMode").IsValid, "Rename accepted.");
                FormXmlWriteResult saved = FormXmlWriter.Write(session.Document, catalog);
                AssertValid(saved);
                FormDocumentLoadResult reopened = FormXmlImporter.Load(saved.Utf8!, catalog, FormImportMode.Strict);
                AssertValid(reopened);
                FormCppCompilationResult regenerated = FormCppCompiler.Compile(reopened.Document!, catalog);
                AssertValid(regenerated);
                string renamed = Encoding.UTF8.GetString(regenerated.HeaderUtf8!);
                Assert(renamed.Contains("onOperatingModeEditCommitted", StringComparison.Ordinal) &&
                    renamed.Contains("onOperatingModeEditCancelled", StringComparison.Ordinal), "Renamed typed hooks survive save/reopen/generation.");
            }),
            new("text fields own independent bounded bindings", () =>
            {
                WidgetCatalog catalog = LoadWidgetCatalog(widgetCatalogPath);
                FormDocumentLoadResult form = FormXmlImporter.LoadFile(
                    Path.Combine(repositoryRoot, "tests", "designer-fixtures", "text-events", "form.xml"),
                    catalog, FormImportMode.Strict);
                AssertValid(form);
                DesignerDocumentSession session = new(form.Document!, catalog);
                Assert(session.AddWidget("lithe.widget.text-field", "root").IsValid, "Palette text field must be valid.");
                Assert(session.Document.Bindings.Length == 2 && session.Document.Bindings[^1].CapacityBytes == 32,
                    "Palette insertion must declare bounded storage atomically.");
                string firstBinding = session.Document.Bindings[^1].Id;
                Assert(session.AddWidget("lithe.widget.text-field", "root").IsValid &&
                    session.Document.Bindings[^1].Id != firstBinding, "Each field needs distinct storage.");
                session.Undo();
                session.Undo();
                Assert(session.Document.Bindings.Length == 1, "Undo removes newly declared bindings too.");
                Assert(session.Duplicate("setpoint").IsValid && session.Document.Bindings.Length == 2 &&
                    session.Document.Bindings[^1].CapacityBytes == 4, "Duplication retains capacity with a new binding.");
                var reference = session.Document.Components[^1].Properties.OfType<FormReferenceValue>()
                    .Single(item => item.PropertyId == "binding");
                Assert(reference.ReferenceId == session.Document.Bindings[^1].Id,
                    "Duplicated field must not alias the original mutable text.");
                AssertValid(FormCppCompiler.Compile(session.Document, catalog));
            }),
            new("generated button variant fixture is fresh", () => GeneratedFormFixtureIsFresh(
                repositoryRoot,
                widgetCatalogPath,
                "button-variants-smoke",
                "button_variants")),
            new("generated tab fixture is fresh", () => GeneratedFormFixtureIsFresh(
                repositoryRoot,
                widgetCatalogPath,
                "tab-view-smoke",
                "tab_settings")),
            new("generated wheel fixture is fresh", () => GeneratedFormFixtureIsFresh(
                repositoryRoot,
                widgetCatalogPath,
                "wheel-picker-smoke",
                "alarm_time")),
            new("form surface lock changes the work product", () => FormSurfaceLockChangesWorkProduct(
                widgetCatalogPath,
                h750ProfilePath)),
            new(
                "designer edit transactions are atomic and reversible",
                () => DesignerEditTransactionsAreAtomic(widgetCatalogPath)),
            new("preview protocol rejects hostile records", PreviewProtocolRejectsHostileRecords),
            new("preview protocol rejects stale responses", PreviewProtocolRejectsStaleResponses),
            new("preview protocol carries inline frames", PreviewProtocolCarriesInlineFrames),
            new("preview surfaces describe native packed storage", () => NativePreviewTests.PackedSurfacesDescribeNativeStorage(repositoryRoot)),
            new("preview workers negotiate native packed surfaces", NativePreviewTests.PackedCapabilityIsNegotiated),
            new(
                "checked resource manifest validates and verifies",
                () => ResourceManifestTests.CheckedManifestValidatesAndVerifies(
                    resourceManifestPath,
                    resourceProjectRoot)),
            new(
                "resource canonicalization is order independent",
                () => ResourceManifestTests.CanonicalizationIsOrderIndependent(resourceManifestPath)),
            new(
                "resource compliance policy can fail product validation",
                () => ResourceManifestTests.CompliancePolicyCanFailProductValidation(resourceManifestPath)),
            new(
                "resource artifact drift fails closed",
                () => ResourceManifestTests.ArtifactDriftFailsClosed(
                    resourceManifestPath,
                    resourceProjectRoot)),
            new(
                "resource duplicate properties and unsafe paths fail",
                () => ResourceManifestTests.DuplicatePropertiesAndUnsafePathsFail(resourceManifestPath)),
            new(
                "resource output and ownership fail closed",
                () => ResourceManifestTests.VariantOutputAndOwnershipFailClosed(resourceManifestPath)),
            new(
                "project resolution includes locked resources",
                () => ResourceManifestTests.ProjectResolutionIncludesLockedResources(
                    Path.Combine(resourceProjectRoot, "project.litheview.json"),
                    surfaceDirectory,
                    resourceProjectRoot)),
            new(
                "resource surface compatibility fails closed",
                () => ResourceManifestTests.SurfaceCompatibilityFailsClosed(
                    Path.Combine(resourceProjectRoot, "project.litheview.json"),
                    resourceProjectRoot,
                    Path.Combine(
                        surfaceDirectory,
                        "stm32h750b-dk-rk043fn48h-rgb565.json"))),
            new(
                "font repertoire is canonical and bounded",
                ResourceManifestTests.FontRepertoireIsCanonicalAndBounded),
            new(
                "checked font metrics use the strict converter contract",
                () => ResourceManifestTests.CheckedFontMetricsUseStrictContract(repositoryRoot)),
            new(
                "font resource import is deterministic and transactional",
                () => ResourceManifestTests.FontResourceImportIsDeterministicAndTransactional(projectPath, repositoryRoot)),
            new(
                "image resource import is deterministic and transactional",
                () => ResourceManifestTests.ImageResourceImportIsDeterministicAndTransactional(projectPath, repositoryRoot)),
            new(
                "animation resource import is deterministic and transactional",
                () => ResourceManifestTests.AnimationResourceImportIsDeterministicAndTransactional(projectPath, repositoryRoot)),
            new(
                "preview surface factory maps checked profiles",
                () => PreviewSurfaceFactoryMapsCheckedProfiles(h750ProfilePath, roundProfilePath)),
            new("native preview worker rejects unknown messages", () =>
                NativePreviewWorkerRejectsUnknownMessagesAsync(repositoryRoot).GetAwaiter().GetResult()),
            new("native preview worker renders and restarts", () => NativePreviewWorkerRendersAndRestartsAsync(
                repositoryRoot).GetAwaiter().GetResult()),
            new("native preview worker renders packed storage", () => NativePreviewTests.WorkerRendersPackedStorageAsync(
                PreviewWorkerPath(repositoryRoot)).GetAwaiter().GetResult()),
            new("native preview worker rejects malformed packed requests", () =>
                NativePreviewTests.WorkerRejectsMalformedPackedRequestsAsync(PreviewWorkerPath(repositoryRoot)).GetAwaiter().GetResult())
        ];

        int failures = 0;
        foreach (TestCase test in tests)
        {
            try
            {
                test.Body();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                ++failures;
                Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
            }
        }

        Console.WriteLine($"RESULT {tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckedProfilesValidate(string h750Path, string roundPath)
    {
        DisplaySurfaceLoadResult h750 = DisplaySurfaceProfileLoader.LoadFile(h750Path);
        DisplaySurfaceLoadResult round = DisplaySurfaceProfileLoader.LoadFile(roundPath);

        AssertValid(h750);
        AssertValid(round);
        Assert(h750.Profile!.PixelSize == new PixelSize { Width = 480, Height = 272 }, "H750 pixels differ.");
        Assert(h750.Profile.Shape == SurfaceShape.Rectangle, "H750 shape differs.");
        Assert(round.Profile!.PixelSize == new PixelSize { Width = 240, Height = 240 }, "Round pixels differ.");
        Assert(round.Profile.Shape == SurfaceShape.Circle, "Round shape differs.");
        Assert(h750.Sha256 == H750ProfileSha256, "H750 profile identity changed.");
        Assert(round.Sha256 == RoundProfileSha256, "Round profile identity changed.");
        Assert(h750.Sha256 != round.Sha256, "Distinct profiles share a hash.");
    }

    private static void CheckedSurfaceCatalogValidates(string surfaceDirectory)
    {
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["litheview.surface.gc9a01-1-28-round-240-rgb565"] =
                "64fb13970d93d65df209ecee57e1795a88cffc7514d7716d95564c5a699963a8",
            ["litheview.surface.synthetic-red-yellow-staging"] =
                "47be848df113c2c0d1c01c525864f5a5433738e35d7abb42c0d94866fd1af9ca",
            ["litheview.surface.synthetic-red-staging"] =
                "df421d1ebe0efca1f053d3a5bb3321f753c4c4cea7bc8f5681643bf88e93b522",
            ["litheview.surface.stm32h750b-dk-rk043fn48h-rgb565"] = H750ProfileSha256,
            ["litheview.surface.stm32h750b-dk-rk043fn48h-xrgb8888"] =
                "97dad1722b7e782c38e8da83534dd63de2ed45200e392b35589852224a03710f"
        };
        string[] paths = Directory.GetFiles(surfaceDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Assert(paths.Length == expected.Count, "Unexpected checked surface-profile count.");
        foreach (string path in paths)
        {
            DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.LoadFile(path);
            AssertValid(result);
            Assert(expected.TryGetValue(result.Profile!.Id, out string? hash), "Unknown checked surface profile.");
            Assert(result.Sha256 == hash, $"Surface profile '{result.Profile.Id}' identity changed.");
        }
    }

    private static void DuplicateCatalogIdsFailClosed(string profilePath)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "LitheView.Designer.Core.Tests",
            Guid.NewGuid().ToString("N"));
        string surfaceDirectory = Path.Combine(temporaryRoot, "surfaces");
        string targetDirectory = Path.Combine(temporaryRoot, "targets");
        Directory.CreateDirectory(surfaceDirectory);
        Directory.CreateDirectory(targetDirectory);
        try
        {
            File.Copy(profilePath, Path.Combine(surfaceDirectory, "first.json"));
            File.Copy(profilePath, Path.Combine(surfaceDirectory, "second.json"));
            ProfileCatalogLoadResult result = ProfileCatalogLoader.Load(
                surfaceDirectory,
                targetDirectory);
            Assert(!result.IsValid, "A duplicate surface ID must invalidate the catalog.");
            Assert(
                result.Diagnostics.Any(item => item.Code == "LVRP0003"),
                "A duplicate surface ID must report LVRP0003.");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static void DuplicatePropertiesFail(string profilePath)
    {
        string source = File.ReadAllText(profilePath);
        string mutated = source.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
            StringComparison.Ordinal);
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.Load(Encoding.UTF8.GetBytes(mutated));
        AssertDiagnostic(result, "LVDS0005");
    }

    private static void UnknownPropertiesFail(string profilePath)
    {
        string source = File.ReadAllText(profilePath);
        string mutated = source.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"unexpectedProperty\": true,",
            StringComparison.Ordinal);
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.Load(Encoding.UTF8.GetBytes(mutated));
        AssertDiagnostic(result, "LVDS0003");
    }

    private static void NullRequiredValuesFail(string profilePath)
    {
        string source = File.ReadAllText(profilePath);
        string mutated = source.Replace(
            "\"displayName\": \"STM32H750B-DK RK043FN48H RGB565\",",
            "\"displayName\": null,",
            StringComparison.Ordinal);
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.Load(Encoding.UTF8.GetBytes(mutated));
        AssertDiagnostic(result, "LVDS0003");
    }

    private static void OversizedProfilesFail()
    {
        byte[] oversized = new byte[1_048_577];
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.Load(oversized);
        AssertDiagnostic(result, "LVDS0007");
    }

    private static void CircleGeometryFailsClosed(string profilePath)
    {
        DisplaySurfaceLoadResult checkedProfile = DisplaySurfaceProfileLoader.LoadFile(profilePath);
        AssertValid(checkedProfile);

        DisplaySurfaceProfile invalid = checkedProfile.Profile! with
        {
            PixelSize = new PixelSize { Width = 241, Height = 240 }
        };
        byte[] json = DisplaySurfaceCanonicalizer.Canonicalize(invalid);
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.Load(json);
        AssertDiagnostic(result, "LVDS1202");
    }

    private static void CapabilityOrderIsCanonical(string profilePath)
    {
        DisplaySurfaceLoadResult checkedProfile = DisplaySurfaceProfileLoader.LoadFile(profilePath);
        AssertValid(checkedProfile);

        DisplaySurfaceProfile first = checkedProfile.Profile! with
        {
            InputCapabilities = [InputCapability.PushButton, InputCapability.Encoder]
        };
        DisplaySurfaceProfile second = checkedProfile.Profile with
        {
            InputCapabilities = [InputCapability.Encoder, InputCapability.PushButton]
        };
        byte[] firstCanonical = DisplaySurfaceCanonicalizer.Canonicalize(first);
        byte[] secondCanonical = DisplaySurfaceCanonicalizer.Canonicalize(second);
        Assert(firstCanonical.AsSpan().SequenceEqual(secondCanonical), "Capability order changed canonical output.");
    }

    private static void SurfaceSemanticsAffectIdentity(string profilePath)
    {
        DisplaySurfaceLoadResult checkedProfile = DisplaySurfaceProfileLoader.LoadFile(profilePath);
        AssertValid(checkedProfile);

        DisplaySurfaceProfile original = checkedProfile.Profile!;
        DisplaySurfaceProfile changedShape = original with { Shape = SurfaceShape.Square };
        DisplaySurfaceProfile changedPixels = original with
        {
            PixelSize = original.PixelSize with { Width = original.PixelSize.Width + 1 }
        };
        DisplaySurfaceProfile changedColor = original with
        {
            PainterSurface = new PainterSurfaceProfile
            {
                Format = SurfacePixelFormat.Xrgb8888,
                ColorDepthBits = 24,
                StorageBitsPerPixel = 32
            }
        };

        string originalHash = Hash(original);
        Assert(Hash(changedShape) != originalHash, "Shape did not affect surface identity.");
        Assert(Hash(changedPixels) != originalHash, "Pixel dimensions did not affect surface identity.");
        Assert(Hash(changedColor) != originalHash, "Color depth did not affect surface identity.");
    }

    private static void PixelFormatCombinationsFailClosed(string profilePath)
    {
        DisplaySurfaceLoadResult checkedProfile = DisplaySurfaceProfileLoader.LoadFile(profilePath);
        AssertValid(checkedProfile);

        DisplaySurfaceProfile invalid = checkedProfile.Profile! with
        {
            PainterSurface = checkedProfile.Profile.PainterSurface with { StorageBitsPerPixel = 32 }
        };
        byte[] json = DisplaySurfaceCanonicalizer.Canonicalize(invalid);
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.Load(json);
        AssertDiagnostic(result, "LVDS1501");
    }

    private static void NativeTransferDepthFailsClosed(string profilePath)
    {
        DisplaySurfaceLoadResult checkedProfile = DisplaySurfaceProfileLoader.LoadFile(profilePath);
        AssertValid(checkedProfile);

        DisplaySurfaceProfile invalid = checkedProfile.Profile! with
        {
            NativeDisplay = checkedProfile.Profile.NativeDisplay with { ColorDepthBits = 16 }
        };
        byte[] json = DisplaySurfaceCanonicalizer.Canonicalize(invalid);
        DisplaySurfaceLoadResult result = DisplaySurfaceProfileLoader.Load(json);
        AssertDiagnostic(result, "LVDS1603");
    }

    private static void CheckedTargetsValidate(
        string targetDirectory,
        string syntheticPath,
        string u545Path)
    {
        TargetProfileLoadResult synthetic = TargetProfileLoader.LoadFile(syntheticPath);
        TargetProfileLoadResult u545 = TargetProfileLoader.LoadFile(u545Path);

        AssertValid(synthetic);
        AssertValid(u545);
        Assert(synthetic.Sha256 == SyntheticTargetSha256, "Synthetic target identity changed.");
        Assert(u545.Sha256 == U545TargetSha256, "U545 target identity changed.");
        Assert(synthetic.Profile!.Runtime.RenderingMode == RenderingMode.FullFrame, "Synthetic target mode differs.");
        Assert(u545.Profile!.Runtime.Tile == new TileGeometry { Width = 480, Height = 4 }, "U545 tile differs.");
        Assert(
            u545.Profile.Budgets.ArchiveText?.MaximumBytes == 85_200,
            "U545 archive budget differs.");
        Assert(
            u545.Profile.Budgets.TileWorkspace?.MaximumBytes == 3_840,
            "U545 tile workspace differs.");
        Assert(
            synthetic.Profile.Budgets.InternalFlash?.Enforcement == BudgetEnforcement.AdvisoryEstimate,
            "Synthetic ceilings must not claim linker enforcement.");

        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["litheview.target.synthetic-m7-rgb565-full-frame"] = SyntheticTargetSha256,
            ["litheview.target.synthetic-m7-xrgb8888-full-frame"] =
                "b6ff021bd63391a1492ca224db606110d74a80e1b5d5330e6d1c97d0bfa26c79",
            ["litheview.target.stm32u545-round-rgb565-tiled"] =
                "1d640c0c0ecfecdf802913709a689dd563323db23fc8eb748955bc71654f699b",
            ["litheview.target.stm32u545-standard-rgb565-tiled"] = U545TargetSha256
        };
        string[] paths = Directory.GetFiles(targetDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Assert(paths.Length == expected.Count, "Unexpected checked target-profile count.");
        string repositoryRoot = Path.GetFullPath(Path.Combine(targetDirectory, "..", ".."));
        foreach (string path in paths)
        {
            TargetProfileLoadResult result = TargetProfileLoader.LoadFile(path);
            AssertValid(result);
            Assert(expected.TryGetValue(result.Profile!.Id, out string? hash), "Unknown checked target profile.");
            TargetSourcesExist(result.Profile, path, repositoryRoot);
            Assert(result.Sha256 == hash, $"Target profile '{result.Profile.Id}' identity changed.");
        }
        string fixtures = Path.Combine(repositoryRoot, "tests", "rendering", "display-model-generated", "targets");
        foreach (string path in Directory.GetFiles(fixtures, "*.json").Where(path => !path.EndsWith(".evidence.json", StringComparison.Ordinal)))
        {
            TargetProfileLoadResult result = TargetProfileLoader.LoadFile(path);
            AssertValid(result);
            TargetSourcesExist(result.Profile!, path, repositoryRoot);
        }
    }

    private static void TargetSourcesExist(TargetProfile profile, string path, string repositoryRoot)
    {
        Assert(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, profile.Schema)),
            $"Missing target schema referenced by {path}.");
        foreach (string source in profile.Provenance.SourcePaths)
        {
            Assert(File.Exists(Path.Combine(repositoryRoot, source)), $"Missing target provenance source: {source}");
        }
    }

    private static void ProjectResolves(
        string projectPath,
        string surfaceDirectory)
    {
        DesignerProjectLoadResult project = DesignerProjectLoader.LoadFile(projectPath);
        DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
        DesignerProjectResolutionResult result = DesignerProjectResolver.Resolve(
            project,
            catalog.Surfaces);

        AssertValid(result);
        Assert(result.Project!.ResolvedSha256 == ResolvedProjectSha256, "Resolved project identity changed.");
        Assert(result.Project.Resources.Count == 0, "The surface-only project gained resources.");
    }

    private static void AllProjectFixturesResolve(
        string projectDirectory,
        string surfaceDirectory)
    {
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["litheview.project.button-variants-smoke"] =
                "64fd9bc0161c5be9220ed79015e6d645db68576feea16fdf60fdb90ebae74648",
            ["litheview.project.display-profile-smoke"] =
                "c0f365abc1497c37d555b4f98aaa7a1854955e6ef9809d12250a95f026e6b84d",
            ["litheview.project.document-compiler-smoke"] =
                "07b90b143389fe61de50f76748920a8cfa1c2b9a76310adf669b96f3fe151f77",
            ["litheview.project.h750-high-color-smoke"] =
                "0ca526a65938183439defb86d60aaa4847a2751f775c4f8a4aeac67e238a21f5",
            ["litheview.project.synthetic-red-staging-smoke"] =
                "d23236fe69109af0e3b419e69a4fcd93f53ebe940d61ccba1690141dc0185d16",
            ["litheview.project.synthetic-red-yellow-smoke"] =
                "95dd4e358e1a78e87d7feef36982f6225d59beb6db7fadcf67cc05b13cc32edf",
            ["litheview.project.native-synthetic-r2-smoke"] =
                "ddc7e8e46a9b99efe74bb10b89f037a1ef2f64b114e5ba17a5aa4d27f75eb8b7",
            ["litheview.project.resource-manifest-smoke"] =
                "f0ef81eb5a6a024b49ba40a38e149fd3261ffd4847ff9aef7bf60357951277dd",
            ["litheview.project.round-surface-smoke"] =
                "f292b96f21e3f6c7ca7cee9e1fae3e7c01136cf4e6c20b5f103884e6fc9ba93b",
            ["litheview.project.tab-view-smoke"] =
                "917ba4f68900252001e83607d2b2863159030ca6aad56b3d506cb322d0a7ff42",
            ["litheview.project.wheel-picker-smoke"] =
                "7cafbe17fb7f57c30bccab5a253783cbd3f167759f21088ee0891ba1a1a22302"
        };
        DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
        string[] paths = Directory.GetFiles(
            projectDirectory,
            "project.litheview.json",
            SearchOption.AllDirectories);
        Assert(paths.Length == expected.Count, "Unexpected checked designer-project count.");
        foreach (string path in paths)
        {
            DesignerProjectResolutionResult result = DesignerProjectResolver.Resolve(
                DesignerProjectLoader.LoadFile(path),
                catalog.Surfaces,
                Path.GetDirectoryName(path)!);
            AssertValid(result);
            Assert(
                expected.TryGetValue(result.Project!.Manifest.Id, out string? hash),
                "Unknown checked designer project.");
            Assert(result.Project.ResolvedSha256 == hash, "Resolved project identity changed.");
        }
    }

    private static void ProjectFactoryLocksSelectedProfiles(
        string surfacePath,
        string surfaceDirectory)
    {
        DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
        DesignerProjectLoadResult project = DesignerProjectFactory.Create(
            "../../../schema/project-manifest.schema.json",
            "litheview.project.factory-test",
            "Project Factory Test",
            surface);
        AssertValid(project);
        Assert(project.Manifest!.SurfaceProfile!.Sha256 == surface.Sha256, "Surface lock differs.");

        DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
        DesignerProjectResolutionResult resolution = DesignerProjectResolver.Resolve(
            project,
            catalog.Surfaces);
        AssertValid(resolution);
        Assert(resolution.Project!.Surface!.Id == surface.Profile!.Id, "Factory surface resolution differs.");
    }

    private static void DesignerWorkspaceCreationIsCanonical(
        string surfaceDirectory,
        string surfacePath,
        string catalogPath)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"litheview-designer-workspace-{Guid.NewGuid():N}");
        string projectDirectory = Path.Combine(temporaryRoot, "created-project");
        try
        {
            DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
            WidgetCatalogLoadResult widgets = WidgetCatalogLoader.LoadFile(catalogPath);
            Assert(profiles.IsValid, "Checked profiles did not load for workspace creation.");
            Assert(widgets.IsValid && widgets.Catalog is not null, "Widget catalog did not load.");
            DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(
                new NewDesignerProjectRequest
                {
                    ProjectDirectory = projectDirectory,
                    ProjectId = "litheview.project.created-test",
                    DisplayName = "Created Test",
                    SchemaReference = "litheview://schema/project-manifest/v1",
                    Surface = DisplaySurfaceProfileLoader.LoadFile(surfacePath),
                    Catalog = widgets.Catalog!
                },
                profiles);
            AssertValid(created);
            DesignerWorkspace workspace = created.Workspace!;
            Assert(File.Exists(workspace.ManifestPath), "Created project manifest is missing.");
            Assert(File.Exists(workspace.FormPath), "Created form is missing.");
            Assert(
                workspace.Form.Components.Length == 1 &&
                    workspace.Form.Components[0].WidgetId == "lithe.widget.panel",
                "Created project does not contain exactly one root panel.");
            FormRectangle frame = workspace.Form.Components[0].Properties
                .OfType<FormRectangleValue>()
                .Single(item => item.PropertyId == "frame").Value;
            Assert(frame == new FormRectangle(0, 0, 480, 272), "Root panel does not match the surface.");

            DesignerWorkspaceLoadResult reopened = DesignerWorkspaceService.Open(
                workspace.ManifestPath,
                profiles,
                widgets.Catalog!);
            AssertValid(reopened);
            byte[] firstManifest = File.ReadAllBytes(workspace.ManifestPath);
            byte[] canonicalManifest = DesignerProjectCanonicalizer.Canonicalize(workspace.Manifest);
            Assert(firstManifest.SequenceEqual(canonicalManifest), "Created manifest is not canonical.");

            FormXmlWriteResult save = DesignerWorkspaceService.SaveForm(workspace, workspace.Form);
            AssertValid(save);
            Assert(
                File.ReadAllBytes(workspace.FormPath).SequenceEqual(save.Utf8),
                "Saved form is not canonical.");

            string exportDirectory = Path.Combine(projectDirectory, "generated");
            DesignerCppExportResult export = DesignerWorkspaceService.ExportCpp(
                workspace,
                exportDirectory);
            Assert(export.IsValid, "Workspace C++ export failed.");
            Assert(File.Exists(export.HeaderPath!), "Workspace C++ header export is missing.");
            Assert(File.Exists(export.IntegrationPath!), "Workspace C++ integration export is missing.");
            byte[] firstHeader = File.ReadAllBytes(export.HeaderPath!);
            byte[] firstIntegration = File.ReadAllBytes(export.IntegrationPath!);
            DesignerCppExportResult repeated = DesignerWorkspaceService.ExportCpp(
                workspace,
                exportDirectory);
            Assert(repeated.IsValid, "Repeated workspace C++ export failed.");
            Assert(
                firstHeader.SequenceEqual(File.ReadAllBytes(repeated.HeaderPath!)) &&
                    firstIntegration.SequenceEqual(File.ReadAllBytes(repeated.IntegrationPath!)),
                "Repeated workspace C++ export is not deterministic.");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static void DesignerWorkspaceCreationNeverOverwrites(
        string surfaceDirectory,
        string surfacePath,
        string catalogPath)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"litheview-designer-no-overwrite-{Guid.NewGuid():N}");
        string projectDirectory = Path.Combine(temporaryRoot, "existing-project");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            string markerPath = Path.Combine(projectDirectory, "owner-data.txt");
            File.WriteAllText(markerPath, "preserve");
            DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
            WidgetCatalogLoadResult widgets = WidgetCatalogLoader.LoadFile(catalogPath);
            DesignerWorkspaceLoadResult result = DesignerWorkspaceService.CreateNew(
                new NewDesignerProjectRequest
                {
                    ProjectDirectory = projectDirectory,
                    ProjectId = "litheview.project.no-overwrite-test",
                    DisplayName = "No Overwrite Test",
                    SchemaReference = "litheview://schema/project-manifest/v1",
                    Surface = DisplaySurfaceProfileLoader.LoadFile(surfacePath),
                    Catalog = widgets.Catalog!
                },
                profiles);
            Assert(!result.IsValid, "Workspace creation overwrote an existing directory.");
            Assert(File.ReadAllText(markerPath) == "preserve", "Existing project-directory data changed.");
            Assert(
                result.Diagnostics.Any(item => item.Code == "LVDW0002"),
                "Existing-directory failure did not produce the stable diagnostic.");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static void ProjectLocalCustomSurfaceSnapshotResolves(
        string surfaceDirectory,
        string surfacePath,
        string catalogPath)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"litheview-designer-custom-surface-{Guid.NewGuid():N}");
        string projectDirectory = Path.Combine(temporaryRoot, "custom-surface-project");
        try
        {
            DisplaySurfaceCatalogLoadResult profiles = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
            WidgetCatalogLoadResult widgets = WidgetCatalogLoader.LoadFile(catalogPath);
            DisplaySurfaceLoadResult source = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
            AssertValid(source);
            DisplaySurfaceProfile customProfile = source.Profile! with
            {
                Id = "litheview.surface.custom.workspace-test",
                DisplayName = "Custom Workspace Test Surface",
                PhysicalSize = new PhysicalSize
                {
                    WidthMillimeters = 96M,
                    HeightMillimeters = 54M
                },
                Provenance = source.Profile.Provenance with
                {
                    Model = "Owner-defined test surface",
                    Notes = "Test-only profile that is not present in the repository catalog."
                }
            };
            DisplaySurfaceLoadResult custom = DisplaySurfaceProfileLoader.Load(
                DisplaySurfaceCanonicalizer.Canonicalize(customProfile));
            AssertValid(custom);

            DesignerWorkspaceLoadResult created = DesignerWorkspaceService.CreateNew(
                new NewDesignerProjectRequest
                {
                    ProjectDirectory = projectDirectory,
                    ProjectId = "litheview.project.custom-surface-test",
                    DisplayName = "Custom Surface Test",
                    SchemaReference = "litheview://schema/project-manifest/v1",
                    Surface = custom,
                    Catalog = widgets.Catalog!
                },
                profiles);
            AssertValid(created);
            Assert(
                created.Workspace!.ResolvedProject.Surface!.Id == customProfile.Id,
                "Project-local custom surface did not resolve.");
            Assert(
                File.Exists(Path.Combine(projectDirectory, "profiles", "display-surface.json")),
                "Project-local custom surface snapshot is missing.");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static void ProjectSurfaceLockFails(
        string projectPath,
        string surfaceDirectory)
    {
        DesignerProjectLoadResult checkedProject = DesignerProjectLoader.LoadFile(projectPath);
        AssertValid(checkedProject);
        DesignerProjectManifest invalid = checkedProject.Manifest! with
        {
            SurfaceProfile = checkedProject.Manifest.SurfaceProfile! with { Sha256 = new string('0', 64) }
        };
        DesignerProjectLoadResult reloaded = DesignerProjectLoader.Load(
            DesignerProjectCanonicalizer.Canonicalize(invalid));
        DisplaySurfaceCatalogLoadResult catalog = ProfileCatalogLoader.LoadSurfaces(surfaceDirectory);
        DesignerProjectResolutionResult result = DesignerProjectResolver.Resolve(
            reloaded,
            catalog.Surfaces);
        AssertDiagnostic(result, "LVRP1102");
    }

    private static void ProjectPathsStayContained(string projectPath)
    {
        DesignerProjectLoadResult checkedProject = DesignerProjectLoader.LoadFile(projectPath);
        AssertValid(checkedProject);
        DesignerProjectManifest invalid = checkedProject.Manifest! with { Forms = ["../outside.xml"] };
        DesignerProjectLoadResult result = DesignerProjectLoader.Load(
            DesignerProjectCanonicalizer.Canonicalize(invalid));
        AssertDiagnostic(result, "LVPM1401");
    }

    private static void SurfaceMigrationIsExplicit(string surfacePath)
    {
        DisplaySurfaceLoadResult checkedSurface = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
        AssertValid(checkedSurface);
        DisplaySurfaceProfile current = checkedSurface.Profile!;
        SurfaceMigrationResult unchanged = SurfaceMigrationAnalyzer.Compare(current, current);
        Assert(!unchanged.RequiresMigration, "An unchanged surface required migration.");

        DisplaySurfaceProfile proposed = current with
        {
            PixelSize = current.PixelSize with { Width = current.PixelSize.Width + 1 },
            PainterSurface = new PainterSurfaceProfile
            {
                Format = SurfacePixelFormat.Xrgb8888,
                ColorDepthBits = 24,
                StorageBitsPerPixel = 32
            }
        };
        SurfaceMigrationResult changed = SurfaceMigrationAnalyzer.Compare(current, proposed);
        Assert(changed.RequiresMigration, "A semantic surface change was transparent.");
        Assert(changed.Kind.HasFlag(SurfaceMigrationKind.Geometry), "Geometry migration was not reported.");
        Assert(changed.Kind.HasFlag(SurfaceMigrationKind.Color), "Color migration was not reported.");
        Assert(changed.Diagnostics.Any(item => item.Code == "LVDS3004"), "Geometry diagnostic differs.");
        Assert(changed.Diagnostics.Any(item => item.Code == "LVDS3005"), "Color diagnostic differs.");
    }

    private static void CheckedWidgetCatalogValidates(string catalogPath)
    {
        WidgetCatalogLoadResult result = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(result);
        string[] expectedKinds =
        [
            "animatedImage",
            "axis",
            "button",
            "checkbox",
            "dropDown",
            "icon",
            "image",
            "label",
            "line",
            "list",
            "markerRange",
            "meter",
            "panel",
            "picker",
            "plot",
            "progress",
            "radio",
            "scroll",
            "softkey",
            "tabBar",
            "tabView",
            "table",
            "textField",
            "waterfall",
            "wheelPicker"
        ];
        string[] actualKinds = [.. result.Catalog!.Widgets
            .Select(widget => widget.RuntimeKind)
            .Order(StringComparer.Ordinal)];
        Assert(actualKinds.SequenceEqual(expectedKinds), "The built-in ViewKind inventory differs.");
        Assert(result.Catalog.Properties.Length == 49, "The built-in property inventory differs.");
        Assert(result.Sha256 == WidgetCatalogSha256, "The widget catalog identity changed.");
        Assert(BuiltInWidgetCatalog.Widgets.Count == expectedKinds.Length, "Generated C# widgets differ.");
        Assert(
            BuiltInWidgetCatalog.CatalogSha256 == WidgetCatalogSha256,
            "Generated C# catalog identity differs.");
        GeneratedWidgetDescriptor label = BuiltInWidgetCatalog.Widgets.Single(
            widget => widget.Id == "lithe.widget.label");
        Assert(
            label.LegacyClassAliases.Contains("LCDTEXTVIEW", StringComparer.Ordinal),
            "The legacy label alias is not modeled.");
    }

    private static void WidgetCatalogStructureFailsClosed(string catalogPath)
    {
        string source = File.ReadAllText(catalogPath);
        string unknown = source.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"unexpectedProperty\": true,",
            StringComparison.Ordinal);
        AssertDiagnostic(
            WidgetCatalogLoader.Load(Encoding.UTF8.GetBytes(unknown)),
            "LVWS0003");

        string duplicate = source.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
            StringComparison.Ordinal);
        AssertDiagnostic(
            WidgetCatalogLoader.Load(Encoding.UTF8.GetBytes(duplicate)),
            "LVWS0005");
    }

    private static void WidgetSemanticsFailClosed(string catalogPath)
    {
        WidgetCatalogLoadResult checkedCatalog = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(checkedCatalog);
        WidgetCatalog duplicate = checkedCatalog.Catalog! with
        {
            Properties = [.. checkedCatalog.Catalog.Properties, checkedCatalog.Catalog.Properties[0]]
        };
        AssertDiagnostic(
            WidgetCatalogLoader.Load(WidgetCatalogCanonicalizer.Canonicalize(duplicate)),
            "LVWS1101");

        WidgetPropertyDefinition[] invalidProperties = [.. checkedCatalog.Catalog.Properties
            .Select(property => property.Id == "animation-interval"
                ? property with { DefaultValue = ParseJson("0") }
                : property)];
        WidgetCatalog invalidDefault = checkedCatalog.Catalog with { Properties = invalidProperties };
        AssertDiagnostic(
            WidgetCatalogLoader.Load(WidgetCatalogCanonicalizer.Canonicalize(invalidDefault)),
            "LVWS1801");
    }

    private static void WidgetCatalogOrderIsCanonical(string catalogPath)
    {
        WidgetCatalogLoadResult original = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(original);
        WidgetCatalog reordered = original.Catalog! with
        {
            Properties = [.. original.Catalog.Properties.Reverse()],
            Widgets = [.. original.Catalog.Widgets.Reverse()]
        };
        WidgetCatalogLoadResult reloaded = WidgetCatalogLoader.Load(
            WidgetCatalogCanonicalizer.Canonicalize(reordered));
        AssertValid(reloaded);
        Assert(reloaded.Sha256 == original.Sha256, "Catalog order affected semantic identity.");
        Assert(
            reloaded.CanonicalUtf8!.AsSpan().SequenceEqual(original.CanonicalUtf8),
            "Catalog order affected canonical bytes.");
    }

    private static void WidgetCompatibilityFailsClosed(string catalogPath)
    {
        WidgetCatalogLoadResult previous = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(previous);
        const string removedId = "accent-color";
        WidgetCatalog removed = previous.Catalog! with
        {
            Properties = [.. previous.Catalog.Properties.Where(property => property.Id != removedId)],
            Widgets = [.. previous.Catalog.Widgets.Select(widget => widget with
            {
                PropertyIds = [.. widget.PropertyIds.Where(propertyId => propertyId != removedId)]
            })]
        };
        WidgetCatalogLoadResult current = WidgetCatalogLoader.Load(
            WidgetCatalogCanonicalizer.Canonicalize(removed));
        AssertValid(current);
        WidgetCatalogCompatibilityResult result = WidgetCatalogCompatibilityAnalyzer.Compare(
            previous,
            current);
        Assert(!result.IsCompatible, "A removed property was accepted as compatible.");
        Assert(
            result.Diagnostics.Any(diagnostic => diagnostic.Code == "LVWC1101"),
            "A removed property did not report LVWC1101.");
    }

    private static void WidgetRenameMigrationIsDeterministic(string catalogPath)
    {
        WidgetCatalogLoadResult previous = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(previous);
        const string oldId = "accent-color";
        const string newId = "highlight-color";
        WidgetCatalog migrated = previous.Catalog! with
        {
            CompatibilityVersion = 2,
            Properties = [.. previous.Catalog.Properties.Select(property => property.Id == oldId
                ? property with { Id = newId }
                : property)],
            Widgets = [.. previous.Catalog.Widgets.Select(widget => widget with
            {
                PropertyIds = [.. widget.PropertyIds.Select(propertyId =>
                    propertyId == oldId ? newId : propertyId)]
            })],
            Migrations =
            [
                new WidgetMigration
                {
                    FromCompatibilityVersion = 1,
                    ToCompatibilityVersion = 2,
                    Kind = WidgetMigrationKind.RenameProperty,
                    FromId = oldId,
                    ToId = newId
                }
            ]
        };
        WidgetCatalogLoadResult current = WidgetCatalogLoader.Load(
            WidgetCatalogCanonicalizer.Canonicalize(migrated));
        AssertValid(current);
        WidgetCatalogCompatibilityResult compatibility = WidgetCatalogCompatibilityAnalyzer.Compare(
            previous,
            current);
        AssertDiagnosticsValid(compatibility.IsCompatible, compatibility.Diagnostics);
        Assert(
            WidgetCatalogMigrator.ResolvePropertyId(current.Catalog!, null, oldId, 1) == newId,
            "The property rename migration did not resolve deterministically.");
    }

    private static void WidgetCompilerIsDeterministic(string catalogPath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(catalog);
        WidgetSchemaCompilationResult baseline = WidgetSchemaCompiler.Compile(catalog);
        AssertValid(baseline);

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-SA");
            WidgetSchemaCompilationResult localized = WidgetSchemaCompiler.Compile(catalog);
            AssertValid(localized);
            AssertCompilationEqual(baseline, localized);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static void GeneratedWidgetArtifactsAreFresh(string repositoryRoot, string catalogPath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(catalog);
        WidgetSchemaCompilationResult compilation = WidgetSchemaCompiler.Compile(catalog);
        AssertValid(compilation);
        AssertGeneratedFile(
            Path.Combine(
                repositoryRoot,
                "tools",
                "designer",
                "src",
                "LitheView.Designer.Schema",
                "Generated",
                "BuiltInWidgetCatalog.g.cs"),
            compilation.CSharpUtf8!);
        AssertGeneratedFile(
            Path.Combine(repositoryRoot, "include", "lithe", "designer", "generated_widget_metadata.hpp"),
            compilation.CppUtf8!);
        AssertGeneratedFile(
            Path.Combine(repositoryRoot, "docs", "WIDGET_PROPERTY_REFERENCE.md"),
            compilation.DocumentationUtf8!);
    }

    private static void TypedFormReachesFixedPoint(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        FormDocument form = CreateTypedForm();
        FormXmlWriteResult first = FormXmlWriter.Write(form, catalog);
        AssertValid(first);

        FormDocumentLoadResult imported = FormXmlImporter.Load(
            first.Utf8!,
            catalog,
            FormImportMode.Strict);
        AssertValid(imported);
        Assert(
            imported.CanonicalXmlUtf8!.AsSpan().SequenceEqual(first.Utf8),
            "Canonical form XML did not reach a fixed point after one normalization.");
        Assert(imported.Sha256 == first.Sha256, "Canonical form identity changed after strict import.");
        Assert(imported.Document!.Components.Length == 4, "The typed form hierarchy was not preserved.");
        Assert(imported.Document.Actions.Length == 2, "Action declarations were not preserved.");
        Assert(imported.Document.Bindings.Length == 1, "Binding declarations were not preserved.");
        Assert(
            imported.Document.ThemeColors.Single().EncodedColor == 0x00223344U,
            "Theme color declarations were not preserved.");
        FormColorValue? rootColor = imported.Document.Components[0].Properties
            .OfType<FormColorValue>()
            .SingleOrDefault(item => item.PropertyId == "background-color");
        Assert(
            rootColor?.ThemeTokenId == "theme.surface",
            "The named theme reference was flattened during XML save and reopen.");
    }

    private static void ResourceMetadataReachesFixedPoint(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        FormDocument form = CreateTypedForm() with
        {
            Resources =
            [
                new FormResource
                {
                    Id = "resource.icon.run",
                    RuntimeName = "run-icon",
                    Symbol = "RunIcon",
                    Kind = FormResourceKind.Bitmap
                }
            ]
        };
        FormXmlWriteResult written = FormXmlWriter.Write(form, catalog);
        AssertValid(written);
        FormDocumentLoadResult imported = FormXmlImporter.Load(
            written.Utf8!,
            catalog,
            FormImportMode.Strict);
        AssertValid(imported);
        FormResource resource = imported.Document!.Resources.Single();
        Assert(resource.RuntimeName == "run-icon", "Logical resource identity was not retained.");
        Assert(resource.Kind == FormResourceKind.Bitmap, "Logical resource kind was not retained.");
        Assert(
            imported.CanonicalXmlUtf8!.AsSpan().SequenceEqual(written.Utf8),
            "Resource metadata did not reach a canonical XML fixed point.");
    }

    private static void LegacyFormNormalizationIsExplicit(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        const string legacyXml =
            "<object className=\"LCDVIEW\" frame=\"0,0,320,240\" wireframeID=\"legacy-root\">" +
            "<object className=\"LCDBUTTONVIEW\" frame=\"8,8,100,32\" runtimeID=\"41\" " +
            "text=\"OPEN\" productTag=\"legacy-button\"/>" +
            "</object>";

        FormDocumentLoadResult strict = FormXmlImporter.Load(
            Encoding.UTF8.GetBytes(legacyXml),
            catalog,
            FormImportMode.Strict);
        Assert(!strict.IsValid, "Strict import accepted designer metadata loss and unknown attributes.");

        FormDocumentLoadResult compatibility = FormXmlImporter.Load(
            Encoding.UTF8.GetBytes(legacyXml),
            catalog,
            FormImportMode.Compatibility);
        AssertValid(compatibility);
        Assert(
            compatibility.Diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Warning),
            "Compatibility normalization did not report its synthesized metadata.");
        Assert(
            compatibility.Document!.Components.Sum(item => item.HistoricalAttributes.Length) == 2,
            "Historical extension attributes were not preserved explicitly.");

        FormDocumentLoadResult normalized = FormXmlImporter.Load(
            compatibility.CanonicalXmlUtf8!,
            catalog,
            FormImportMode.Compatibility);
        AssertValid(normalized);
        Assert(
            normalized.CanonicalXmlUtf8!.AsSpan().SequenceEqual(compatibility.CanonicalXmlUtf8),
            "Compatibility XML did not reach a fixed point.");
    }

    private static void StrictFormImportRejectsUnsafeXml(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        const string dtdXml =
            "<!DOCTYPE view [<!ENTITY external SYSTEM \"file:///should-not-be-read\">]>" +
            "<view frame=\"0,0,1,1\" text=\"&external;\"/>";
        FormDocumentLoadResult dtd = FormXmlImporter.Load(
            Encoding.UTF8.GetBytes(dtdXml),
            catalog,
            FormImportMode.Compatibility);
        Assert(!dtd.IsValid, "The form importer accepted a DTD or external entity.");

        byte[] oversized = new byte[(1024 * 1024) + 1];
        FormDocumentLoadResult size = FormXmlImporter.Load(oversized, catalog, FormImportMode.Strict);
        Assert(!size.IsValid, "The form importer accepted an oversized document.");

        const string unknownMetadata =
            "<!--litheview-form schemaVersion=\"1\" id=\"form.main\" displayName=\"Main\" mystery=\"x\"-->" +
            "<!--litheview-component id=\"component.main.root\" name=\"root\"-->" +
            "<panel frame=\"0,0,1,1\"/>";
        FormDocumentLoadResult unknown = FormXmlImporter.Load(
            Encoding.UTF8.GetBytes(unknownMetadata),
            catalog,
            FormImportMode.Strict);
        Assert(
            !unknown.IsValid && unknown.Diagnostics.Any(item => item.Code == "LVFX0104"),
            "Strict import accepted an unknown metadata attribute.");
    }

    private static void FormSymbolsFailClosed(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        FormDocument form = CreateTypedForm();
        FormComponent[] components = [.. form.Components];
        components[2] = components[2] with { Name = "open-button" };
        components[1] = components[1] with { Name = "open_button" };
        FormResolutionResult result = FormDocumentResolver.Resolve(
            form with { Components = components },
            catalog);
        Assert(
            !result.IsValid && result.Diagnostics.Any(item => item.Code == "LVFD1103"),
            "C++ symbol collisions were not diagnosed before generation.");
    }

    private static void FormBudgetsRemainExplicitDeploymentAnalysis(string catalogPath, string targetPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        TargetProfileLoadResult target = TargetProfileLoader.LoadFile(targetPath);
        AssertValid(target);
        FormDocument form = CreateTypedForm() with
        {
            Resources =
            [
                new FormResource
                {
                    Id = "resource.icon.run",
                    RuntimeName = "run-icon",
                    Symbol = "RunIcon",
                    Kind = FormResourceKind.Bitmap
                }
            ]
        };
        FormResolutionResult resolution = FormDocumentResolver.Resolve(form, catalog);
        AssertValid(resolution);
        FormBudgetReport valid = FormBudgetAnalyzer.Analyze(
            resolution.Document!,
            new FormBudgetRequest
            {
                Target = target.Profile!,
                RequiredRenderWorkspaceBytes = 3840,
                GeneratedArchiveBytes = 4096
            });
        AssertValid(valid);
        Assert(
            valid.Uses.Single(item => item.Name == "generated-archive").Used == 4096,
            "The caller-supplied generated archive measurement changed.");

        FormBudgetReport oversized = FormBudgetAnalyzer.Analyze(
            resolution.Document!,
            new FormBudgetRequest
            {
                Target = target.Profile!,
                RequiredRenderWorkspaceBytes = target.Profile!.Budgets.TileWorkspace!.MaximumBytes + 1,
                GeneratedArchiveBytes = target.Profile.Budgets.ArchiveText!.MaximumBytes + 1
            });
        Assert(!oversized.IsValid, "Target workspace and archive limits were not enforced.");
        Assert(
            oversized.Diagnostics.Count(item => item.Code == "LVFB1401") == 2,
            "Budget failures were not diagnosed independently.");
    }

    private static void FormCppCompilerIsDeterministic(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        FormCppCompilationResult first = FormCppCompiler.Compile(CreateTypedForm(), catalog);
        FormCppCompilationResult second = FormCppCompiler.Compile(CreateTypedForm(), catalog);
        AssertValid(first);
        AssertValid(second);
        Assert(
            first.HeaderUtf8!.AsSpan().SequenceEqual(second.HeaderUtf8),
            "Generated form headers are not deterministic.");
        Assert(
            first.IntegrationUtf8!.AsSpan().SequenceEqual(second.IntegrationUtf8),
            "Generated integration shells are not deterministic.");
        string header = Encoding.UTF8.GetString(first.HeaderUtf8!);
        Assert(header.Contains("Form final\n{", StringComparison.Ordinal), "Generated C++ is not Allman style.");
        Assert(
            header.Contains("kSourceXmlSha256", StringComparison.Ordinal),
            "Generated C++ is not bound to its canonical XML identity.");
    }

    private static void GeneratedUserRegionsPreserveExactBodies(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        FormCppCompilationResult compilation = FormCppCompiler.Compile(CreateTypedForm(), catalog);
        AssertValid(compilation);
        string generated = Encoding.UTF8.GetString(compilation.IntegrationUtf8!);
        const string begin = "// LITHEVIEW USER CODE BEGIN form.main.includes\n";
        const string end = "// LITHEVIEW USER CODE END form.main.includes\n";
        const string preservedBody = "#include \"application.hpp\"\r\n// Developer bytes stay exact.  \r\n";
        string existing = generated.Replace(begin + end, begin + preservedBody + end, StringComparison.Ordinal);
        PreservedRegionMergeResult merged = PreservedRegionMerger.Merge(
            compilation.IntegrationUtf8,
            Encoding.UTF8.GetBytes(existing));
        AssertValid(merged);
        string mergedText = Encoding.UTF8.GetString(merged.Utf8!);
        Assert(mergedText.Contains(preservedBody, StringComparison.Ordinal), "User-code bytes were changed.");
        Assert(!mergedText.Contains(begin + end, StringComparison.Ordinal), "An empty region replaced user code.");
    }

    private static void MalformedUserRegionsFailClosed(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        FormCppCompilationResult compilation = FormCppCompiler.Compile(CreateTypedForm(), catalog);
        AssertValid(compilation);
        const string nested =
            "// LITHEVIEW USER CODE BEGIN one\n" +
            "// LITHEVIEW USER CODE BEGIN two\n" +
            "// LITHEVIEW USER CODE END two\n" +
            "// LITHEVIEW USER CODE END one\n";
        PreservedRegionMergeResult malformed = PreservedRegionMerger.Merge(
            compilation.IntegrationUtf8!,
            Encoding.UTF8.GetBytes(nested));
        Assert(!malformed.IsValid, "Nested user-code regions were accepted.");

        const string mismatched =
            "// LITHEVIEW USER CODE BEGIN one\n" +
            "keep();\n" +
            "// LITHEVIEW USER CODE END two\n";
        PreservedRegionMergeResult mismatch = PreservedRegionMerger.Merge(
            compilation.IntegrationUtf8,
            Encoding.UTF8.GetBytes(mismatched));
        Assert(
            !mismatch.IsValid && mismatch.Diagnostics.Any(item => item.Code == "LVFR1106"),
            "Mismatched user-code region IDs were accepted.");

        string existing = Encoding.UTF8.GetString(compilation.IntegrationUtf8!);
        const string orphan =
            "// LITHEVIEW USER CODE BEGIN orphan.region\n" +
            "keep();\n" +
            "// LITHEVIEW USER CODE END orphan.region\n";
        PreservedRegionMergeResult orphaned = PreservedRegionMerger.Merge(
            compilation.IntegrationUtf8,
            Encoding.UTF8.GetBytes(existing + orphan));
        Assert(!orphaned.IsValid, "Orphaned user-code regions were discarded.");
    }

    private static void EveryWidgetPropertyReachesXmlAndCpp(string catalogPath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        FormDocument form = CreateExhaustiveForm(catalog);
        FormXmlWriteResult xml = FormXmlWriter.Write(form, catalog);
        FormCppCompilationResult cpp = FormCppCompiler.Compile(form, catalog);
        AssertValid(xml);
        AssertValid(cpp);
        FormDocumentLoadResult imported = FormXmlImporter.Load(
            xml.Utf8!,
            catalog,
            FormImportMode.Strict);
        AssertValid(imported);
        Assert(
            imported.CanonicalXmlUtf8!.AsSpan().SequenceEqual(xml.Utf8),
            "The exhaustive property form did not reach the canonical XML fixed point.");

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            FormCppCompilationResult localized = FormCppCompiler.Compile(form, catalog);
            AssertValid(localized);
            Assert(
                localized.HeaderUtf8!.AsSpan().SequenceEqual(cpp.HeaderUtf8),
                "Generated C++ depends on the process culture.");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        FormComponent[] invalidComponents = [.. form.Components];
        int animationIndex = Array.FindIndex(
            invalidComponents,
            item => item.WidgetId == "lithe.widget.animated-image");
        FormPropertyValue[] invalidProperties = [.. invalidComponents[animationIndex].Properties];
        int repeatIndex = Array.FindIndex(invalidProperties, item => item.PropertyId == "repeat-index");
        invalidProperties[repeatIndex] = new FormUnsignedIntegerValue("repeat-index", 2U);
        invalidComponents[animationIndex] = invalidComponents[animationIndex] with
        {
            Properties = invalidProperties
        };
        FormResolutionResult invalid = FormDocumentResolver.Resolve(
            form with { Components = invalidComponents },
            catalog);
        Assert(
            !invalid.IsValid && invalid.Diagnostics.Any(item => item.Code == "LVFD1131"),
            "Out-of-range animation repeat indices were not rejected.");
    }

    private static void GeneratedFormFixtureIsFresh(
        string repositoryRoot,
        string catalogPath,
        string fixtureName,
        string generatedStem)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        string fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "designer-fixtures",
            fixtureName);
        string formPath = Path.Combine(fixtureRoot, "forms", "main.litheview.xml");
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(
            formPath,
            catalog,
            FormImportMode.Strict);
        AssertValid(form);
        Assert(
            File.ReadAllBytes(formPath).AsSpan().SequenceEqual(form.CanonicalXmlUtf8),
            "The checked form fixture is not canonical.");
        FormCppCompilationResult compilation = FormCppCompiler.Compile(form.Document!, catalog);
        AssertValid(compilation);
        AssertGeneratedFile(
            Path.Combine(fixtureRoot, "generated", $"{generatedStem}.hpp"),
            compilation.HeaderUtf8!);
        string integrationPath = Path.Combine(fixtureRoot, "generated", $"{generatedStem}.cpp");
        byte[] integration = File.ReadAllBytes(integrationPath);
        PreservedRegionMergeResult merged = PreservedRegionMerger.Merge(
            compilation.IntegrationUtf8!,
            integration);
        AssertValid(merged);
        Assert(
            integration.AsSpan().SequenceEqual(merged.Utf8),
            "Generated integration scaffolding is stale outside preserved regions.");
    }

    private static void FormSurfaceLockChangesWorkProduct(string catalogPath, string surfacePath)
    {
        WidgetCatalog catalog = LoadWidgetCatalog(catalogPath);
        DisplaySurfaceLoadResult surface = DisplaySurfaceProfileLoader.LoadFile(surfacePath);
        AssertValid(surface);
        FormResolutionResult resolution = FormDocumentResolver.Resolve(CreateTypedForm(), catalog);
        AssertValid(resolution);
        FormSurfaceValidationResult valid = FormSurfaceValidator.Validate(
            resolution.Document!,
            surface.Profile!);
        AssertValid(valid);

        FormComponent[] components = [.. resolution.Document!.Source.Components];
        FormPropertyValue[] properties = [.. components[0].Properties];
        int frameIndex = Array.FindIndex(properties, item => item.PropertyId == "frame");
        properties[frameIndex] = new FormRectangleValue("frame", new FormRectangle(0, 0, 320, 240));
        components[0] = components[0] with { Properties = properties };
        FormResolutionResult wrongResolution = FormDocumentResolver.Resolve(
            resolution.Document.Source with { Components = components },
            catalog);
        AssertValid(wrongResolution);
        FormSurfaceValidationResult wrong = FormSurfaceValidator.Validate(
            wrongResolution.Document!,
            surface.Profile!);
        Assert(
            !wrong.IsValid && wrong.Diagnostics.Any(item => item.Code == "LVFS1002"),
            "A root geometry that does not match the selected surface was accepted.");
    }

    private static FormDocument CreateExhaustiveForm(WidgetCatalog catalog)
    {
        FormResource[] resources =
        [
            new FormResource
            {
                Id = "resource.font.main",
                RuntimeName = "main-font",
                Symbol = "MainFont",
                Kind = FormResourceKind.Font
            },
            new FormResource
            {
                Id = "resource.bitmap.first",
                RuntimeName = "frame-first",
                Symbol = "FrameFirst",
                Kind = FormResourceKind.Bitmap
            },
            new FormResource
            {
                Id = "resource.bitmap.second",
                RuntimeName = "frame-second",
                Symbol = "FrameSecond",
                Kind = FormResourceKind.Bitmap
            }
        ];
        List<FormComponent> components = [];
        WidgetDefinition panel = catalog.Widgets.Single(item => item.Id == "lithe.widget.panel");
        components.Add(CreateExhaustiveComponent(panel, 0, null, catalog));
        // The exhaustive catalog exceeds one parent's 16-child runtime limit.
        // Use a second panel without changing which widget properties are covered.
        components.Add(CreateExhaustiveComponent(
            panel, 1, "component.exhaustive.0", catalog));
        int widgetIndex = 0;
        foreach (WidgetDefinition widget in catalog.Widgets.Where(item => item.Id != panel.Id))
        {
            bool underRoot = widgetIndex < 15;
            components.Add(CreateExhaustiveComponent(
                widget, widgetIndex + 2,
                underRoot ? "component.exhaustive.0" : "component.exhaustive.1", catalog) with
            {
                SiblingOrder = underRoot ? widgetIndex + 1 : widgetIndex - 15
            });
            ++widgetIndex;
        }
        return new FormDocument
        {
            SchemaVersion = 1,
            Id = "form.exhaustive",
            DisplayName = "Exhaustive Form",
            SurfaceProfileId = null,
            Components = [.. components],
            Actions =
            [
                new FormAction { Id = "action.sample", RuntimeId = 42, Symbol = "Sample" }
            ],
            Bindings =
            [
                new FormBinding
                {
                    Id = "binding.sample",
                    RuntimeName = "sample-binding",
                    Symbol = "SampleBinding",
                    Kind = FormBindingKind.MutableText,
                    CapacityBytes = 32
                }
            ],
            Resources = resources,
            ThemeColors = []
        };
    }

    private static FormComponent CreateExhaustiveComponent(
        WidgetDefinition widget,
        int componentIndex,
        string? parentComponentId,
        WidgetCatalog catalog)
    {
        string suffix = componentIndex.ToString(CultureInfo.InvariantCulture);
        FormPropertyValue[] properties = [.. widget.PropertyIds.Select(propertyId =>
            CreateSampleProperty(
                catalog.Properties.Single(item => item.Id == propertyId),
                componentIndex))];
        return new FormComponent
        {
            ComponentId = $"component.exhaustive.{suffix}",
            Name = $"Widget{suffix}",
            WidgetId = widget.Id,
            ParentComponentId = parentComponentId,
            SiblingOrder = 0,
            Properties = properties,
            HistoricalAttributes = []
        };
    }

    private static FormPropertyValue CreateSampleProperty(
        WidgetPropertyDefinition property,
        int componentIndex)
    {
        return property.Kind switch
        {
            WidgetPropertyKind.Boolean => new FormBooleanValue(property.Id, true),
            WidgetPropertyKind.SignedInteger => new FormSignedIntegerValue(property.Id, 1),
            WidgetPropertyKind.UnsignedInteger when property.Id == "runtime-id" =>
                new FormUnsignedIntegerValue(property.Id, (ulong)(componentIndex + 1)),
            WidgetPropertyKind.UnsignedInteger when property.Id == "text-alignment-flags" =>
                new FormUnsignedIntegerValue(property.Id, 13U),
            // One, or the property's minimum when that is larger (tab-height).
            WidgetPropertyKind.UnsignedInteger => new FormUnsignedIntegerValue(
                property.Id,
                (ulong)Math.Max(1L, property.Minimum ?? 1L)),
            WidgetPropertyKind.Duration => new FormUnsignedIntegerValue(property.Id, 40U),
            // The last value is non-default for every built-in enum: an explicit focus
            // override and a toggle button whose sample checked value is then valid.
            WidgetPropertyKind.Enum => new FormTextValue(property.Id, property.EnumValues[^1]),
            WidgetPropertyKind.Color => new FormColorValue(property.Id, 0x7f123456U),
            // Two items, so the sample selected-index of one names an existing tab.
            WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText when property.Id == "items" =>
                new FormTextValue(property.Id, "SAMPLE|ITEMS"),
            WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText =>
                new FormTextValue(property.Id, $"SAMPLE {property.Id}"),
            WidgetPropertyKind.Rectangle => new FormRectangleValue(
                property.Id,
                new FormRectangle(componentIndex, componentIndex, 40, 20)),
            WidgetPropertyKind.ActionId => new FormReferenceValue(
                property.Id,
                FormReferenceKind.Action,
                "action.sample"),
            WidgetPropertyKind.BindingId => new FormReferenceValue(
                property.Id,
                FormReferenceKind.Binding,
                "binding.sample"),
            WidgetPropertyKind.Font => new FormReferenceValue(
                property.Id,
                FormReferenceKind.Font,
                "resource.font.main"),
            WidgetPropertyKind.Bitmap => new FormReferenceValue(
                property.Id,
                FormReferenceKind.Bitmap,
                "resource.bitmap.first"),
            WidgetPropertyKind.AnimationFrames => new FormReferenceListValue(
                property.Id,
                FormReferenceKind.Bitmap,
                ["resource.bitmap.first", "resource.bitmap.second"]),
            _ => throw new InvalidOperationException($"No sample exists for property '{property.Id}'.")
        };
    }

    private static FormDocument CreateTypedForm()
    {
        return new FormDocument
        {
            SchemaVersion = 1,
            Id = "form.main",
            DisplayName = "Main Form",
            SurfaceProfileId = "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565",
            Components =
            [
                new FormComponent
                {
                    ComponentId = "component.main.root",
                    Name = "rootPanel",
                    WidgetId = "lithe.widget.panel",
                    ParentComponentId = null,
                    SiblingOrder = 0,
                    Properties =
                    [
                        new FormRectangleValue("frame", new FormRectangle(0, 0, 480, 272)),
                        new FormColorValue("background-color", "theme.surface")
                    ],
                    HistoricalAttributes = []
                },
                new FormComponent
                {
                    ComponentId = "component.main.open",
                    Name = "openButton",
                    WidgetId = "lithe.widget.button",
                    ParentComponentId = "component.main.root",
                    SiblingOrder = 0,
                    Properties =
                    [
                        new FormRectangleValue("frame", new FormRectangle(8, 8, 144, 36)),
                        new FormUnsignedIntegerValue("runtime-id", 41U),
                        new FormTextValue("text", "OPEN AND RUN")
                    ],
                    HistoricalAttributes = []
                },
                new FormComponent
                {
                    ComponentId = "component.main.name",
                    Name = "nameField",
                    WidgetId = "lithe.widget.text-field",
                    ParentComponentId = "component.main.root",
                    SiblingOrder = 1,
                    Properties =
                    [
                        new FormRectangleValue("frame", new FormRectangle(8, 52, 464, 32)),
                        new FormReferenceValue(
                            "binding",
                            FormReferenceKind.Binding,
                            "binding.device-name"),
                        new FormUnsignedIntegerValue("max-length", 23U),
                        new FormTextValue("placeholder", "DEVICE NAME")
                    ],
                    HistoricalAttributes = []
                },
                new FormComponent
                {
                    ComponentId = "component.main.softkeys",
                    Name = "softkeyBar",
                    WidgetId = "lithe.widget.softkey",
                    ParentComponentId = "component.main.root",
                    SiblingOrder = 2,
                    Properties =
                    [
                        new FormRectangleValue("frame", new FormRectangle(0, 240, 480, 32)),
                        new FormTextValue("left-text", "BACK"),
                        new FormTextValue("right-text", "OPEN"),
                        new FormReferenceValue("left-action", FormReferenceKind.Action, "action.back"),
                        new FormReferenceValue("right-action", FormReferenceKind.Action, "action.open")
                    ],
                    HistoricalAttributes = []
                }
            ],
            Actions =
            [
                new FormAction { Id = "action.back", RuntimeId = 100, Symbol = "Back" },
                new FormAction { Id = "action.open", RuntimeId = 101, Symbol = "Open" }
            ],
            Bindings =
            [
                new FormBinding
                {
                    Id = "binding.device-name",
                    RuntimeName = "device-name",
                    Symbol = "DeviceName",
                    Kind = FormBindingKind.MutableText,
                    CapacityBytes = 24
                }
            ],
            Resources = [],
            ThemeColors =
            [
                new FormThemeColor { Id = "theme.surface", EncodedColor = 0x00223344U }
            ]
        };
    }

    private static void DesignerEditTransactionsAreAtomic(string catalogPath)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(catalogPath);
        AssertValid(catalog);
        DesignerDocumentSession session = new(CreateTypedForm(), catalog.Catalog!);
        Assert(!session.IsDirty && !session.CanUndo, "A new edit session is unexpectedly dirty.");

        FormThemeColor[] maximumTheme =
        [
            new FormThemeColor { Id = "theme.surface", EncodedColor = 0x00223344U },
            .. Enumerable.Range(0, FormDocumentLimits.MaximumThemeColors - 1)
            .Select(index => new FormThemeColor
            {
                Id = $"theme.token-{index}",
                EncodedColor = (uint)index
            })
        ];
        FormEditResult themeEdit = session.SetThemeColors(maximumTheme);
        Assert(themeEdit.IsValid && themeEdit.Changed, "The bounded theme token set was rejected.");
        FormEditResult oversizedTheme = session.SetThemeColors(
            [.. maximumTheme, new FormThemeColor { Id = "theme.overflow", EncodedColor = 0U }]);
        Assert(
            !oversizedTheme.IsValid && oversizedTheme.Diagnostics.Any(item => item.Code == "LVFD1204"),
            "An oversized theme token set did not fail closed.");
        FormEditResult reservedTheme = session.SetThemeColors(
            [new FormThemeColor
            {
                Id = "theme.reserved",
                EncodedColor = FormDocumentLimits.UnspecifiedStyleStateColor
            }]);
        Assert(
            !reservedTheme.IsValid && reservedTheme.Diagnostics.Any(item => item.Code == "LVFD1204"),
            "The internal omitted-state sentinel was accepted as a theme color.");
        FormEditResult reservedProperty = session.SetProperty(
            "component.main.open",
            new FormColorValue(
                "pressed-background-color",
                FormDocumentLimits.UnspecifiedStyleStateColor));
        Assert(
            !reservedProperty.IsValid &&
            reservedProperty.Diagnostics.Any(item => item.Code == "LVFD1130"),
            "The internal omitted-state sentinel was accepted as a literal property color.");
        Assert(session.Undo(), "The bounded theme edit was not one undoable transaction.");

        FormResource font = new()
        {
            Id = "litheview.resource.ui-font",
            RuntimeName = "uiFont",
            Symbol = "UiFont",
            Kind = FormResourceKind.Font
        };
        FormEditResult addResource = session.AddResource(font);
        Assert(addResource.IsValid && addResource.Changed, "A valid logical font resource was not added.");
        FormEditResult duplicateResource = session.AddResource(font);
        Assert(
            !duplicateResource.IsValid &&
            duplicateResource.Diagnostics.Any(item => item.Code == "LVED1701"),
            "A duplicate logical font resource did not fail closed.");

        FormEditResult add = session.AddWidget("lithe.widget.label", "component.main.root");
        Assert(add.IsValid && add.Changed, "Schema-driven label placement failed.");
        Assert(session.Document.Components.Length == 5, "Label placement did not add one component.");
        string labelId = session.Document.Components.Single(item => item.WidgetId == "lithe.widget.label").ComponentId;
        Assert(session.IsDirty && session.CanUndo && !session.CanRedo, "Placement history state differs.");

        FormEditResult geometry = session.SetFrame(labelId, new FormRectangle(20, 30, 120, 24));
        Assert(geometry.IsValid && geometry.Changed, "Geometry edit failed.");
        Assert(session.Undo(), "Geometry transaction did not undo.");
        FormRectangle restored = session.Document.Components.Single(item => item.ComponentId == labelId)
            .Properties.OfType<FormRectangleValue>().Single(item => item.PropertyId == "frame").Value;
        Assert(restored != new FormRectangle(20, 30, 120, 24), "Undo retained edited geometry.");
        Assert(session.Redo(), "Geometry transaction did not redo.");

        session.MarkClean();
        Assert(!session.IsDirty, "MarkClean did not establish a save point.");
        FormEditResult text = session.SetText(labelId, "STATIC TEXT");
        Assert(text.IsValid && session.IsDirty, "Text edit did not dirty the document.");
        Assert(session.Undo() && !session.IsDirty, "Undo did not return to the save point.");
        Assert(session.Redo() && session.IsDirty, "Redo did not leave the save point.");

        FormEditResult addPanel = session.AddWidget("lithe.widget.panel", "component.main.root");
        Assert(addPanel.IsValid && addPanel.Changed, "Schema-driven panel placement failed.");
        string panelId = session.Document.Components
            .Where(item => item.WidgetId == "lithe.widget.panel" && item.ParentComponentId is not null)
            .Single().ComponentId;
        Assert(
            session.SetFrame(panelId, new FormRectangle(200, 100, 180, 120)).IsValid,
            "Panel setup geometry failed.");
        Assert(session.AddWidget("lithe.widget.label", panelId).IsValid, "Nested label placement failed.");
        FormComponent nestedLabel = session.Document.Components
            .Where(item => item.ParentComponentId == panelId)
            .Single();
        string nestedLabelId = nestedLabel.ComponentId;
        int beforeDuplicate = session.Document.Components.Length;
        FormEditResult duplicate = session.Duplicate(panelId);
        Assert(duplicate.IsValid && duplicate.Changed, "Subtree duplication failed.");
        Assert(
            session.Document.Components.Length == beforeDuplicate + 2,
            "Subtree duplication did not copy the complete subtree.");
        FormComponent duplicatePanel = session.Document.Components
            .Where(item => item.WidgetId == "lithe.widget.panel" && item.ParentComponentId is not null)
            .Single(item => item.ComponentId != panelId);
        FormComponent duplicateLabel = session.Document.Components
            .Single(item => item.ParentComponentId == duplicatePanel.ComponentId);
        Assert(
            duplicateLabel.ComponentId != nestedLabelId && duplicateLabel.Name != nestedLabel.Name,
            "Duplicated subtree identities were not regenerated.");
        Assert(session.Undo(), "Subtree duplication did not undo as one transaction.");
        Assert(
            session.Document.Components.Length == beforeDuplicate,
            "Undo retained part of a duplicated subtree.");
        Assert(session.Redo(), "Subtree duplication did not redo as one transaction.");

        FormEditResult duplicateRuntimeWidget = session.Duplicate("component.main.open");
        Assert(duplicateRuntimeWidget.IsValid, "A runtime-addressed widget could not be duplicated.");
        FormComponent duplicatedButton = session.Document.Components
            .Where(item => item.WidgetId == "lithe.widget.button")
            .Single(item => item.ComponentId != "component.main.open");
        ulong duplicatedRuntimeId = duplicatedButton.Properties
            .OfType<FormUnsignedIntegerValue>()
            .Single(item => item.PropertyId == "runtime-id").Value;
        Assert(duplicatedRuntimeId == 0U, "Duplication retained an assigned runtime ID.");
        FormEditResult assign = session.AssignAction(
            "component.main.open",
            "activate",
            "action.open");
        Assert(assign.IsValid && assign.Changed, "Existing form action assignment failed.");
        ulong assignedRuntimeId = session.Document.Components
            .Single(item => item.ComponentId == "component.main.open")
            .Properties.OfType<FormUnsignedIntegerValue>()
            .Single(item => item.PropertyId == "runtime-id").Value;
        Assert(assignedRuntimeId == 101U, "Runtime-backed action assignment used the wrong ID.");
        FormEditResult referenceAction = session.AssignAction(
            "component.main.softkeys",
            "left-activate",
            "action.open");
        Assert(referenceAction.IsValid && referenceAction.Changed, "Reference-backed action assignment failed.");
        FormReferenceValue assignedReference = session.Document.Components
            .Single(item => item.ComponentId == "component.main.softkeys")
            .Properties.OfType<FormReferenceValue>()
            .Single(item => item.PropertyId == "left-action");
        Assert(
            assignedReference.ReferenceId == "action.open",
            "Reference-backed action assignment used the wrong action.");
        FormAction customAction = new() { Id = "action.custom", RuntimeId = 102, Symbol = "Custom" };
        FormEditResult createAction = session.CreateAndAssignAction(
            duplicatedButton.ComponentId,
            "activate",
            customAction);
        Assert(createAction.IsValid && createAction.Changed, "Atomic action creation and assignment failed.");
        Assert(
            session.Document.Actions.Any(item => item.Id == customAction.Id),
            "Created action is absent from the document.");
        Assert(session.Undo(), "Create-and-assign action did not undo.");
        Assert(
            session.Document.Actions.All(item => item.Id != customAction.Id),
            "Undo retained an incompletely removed action.");

        FormEditResult order = session.ChangeZOrder(
            duplicatePanel.ComponentId,
            FormZOrderOperation.SendToBack);
        Assert(order.IsValid && order.Changed, "Z-order transaction failed.");
        Assert(
            session.Document.Components.Single(item => item.ComponentId == duplicatePanel.ComponentId)
                .SiblingOrder == 0,
            "Send-to-back did not assign the first sibling order.");

        FormRectangle nestedFrame = session.Document.Components
            .Single(item => item.ComponentId == nestedLabelId)
            .Properties.OfType<FormRectangleValue>()
            .Single(item => item.PropertyId == "frame").Value;
        FormEditResult cycle = session.Reparent(panelId, nestedLabelId);
        Assert(
            !cycle.IsValid && cycle.Diagnostics.Any(item => item.Code == "LVED1504"),
            "A hierarchy cycle did not fail closed.");
        FormEditResult reparent = session.Reparent(nestedLabelId, "component.main.root");
        Assert(reparent.IsValid && reparent.Changed, "Valid reparent transaction failed.");
        FormRectangle reparentedFrame = session.Document.Components
            .Single(item => item.ComponentId == nestedLabelId)
            .Properties.OfType<FormRectangleValue>()
            .Single(item => item.PropertyId == "frame").Value;
        Assert(
            reparentedFrame.X == nestedFrame.X + 200 && reparentedFrame.Y == nestedFrame.Y + 100,
            "Reparenting did not preserve the component's absolute location.");
        FormEditResult invalidParent = session.Reparent(panelId, nestedLabelId);
        Assert(
            !invalidParent.IsValid && invalidParent.Diagnostics.Any(item => item.Code == "LVED1505"),
            "An invalid non-container parent did not fail closed.");

        Dictionary<string, FormRectangle> frames = new(StringComparer.Ordinal)
        {
            [labelId] = new FormRectangle(24, 34, 120, 24),
            [nestedLabelId] = reparentedFrame with { X = reparentedFrame.X + 4 }
        };
        FormEditResult multiGeometry = session.SetFrames(frames);
        Assert(multiGeometry.IsValid && multiGeometry.Changed, "Multi-component geometry edit failed.");
        Assert(session.Undo(), "Multi-component geometry did not undo as one transaction.");

        FormEditResult rootDelete = session.Delete("component.main.root");
        Assert(!rootDelete.IsValid && !rootDelete.Changed, "The root component was deletable.");
        Assert(
            rootDelete.Diagnostics.Any(item => item.Code == "LVED1202"),
            "Root deletion did not use the stable failure diagnostic.");
    }

    private static void PreviewSurfaceFactoryMapsCheckedProfiles(string h750Path, string roundPath)
    {
        DisplaySurfaceLoadResult h750 = DisplaySurfaceProfileLoader.LoadFile(h750Path);
        DisplaySurfaceLoadResult round = DisplaySurfaceProfileLoader.LoadFile(roundPath);
        AssertValid(h750);
        AssertValid(round);

        PreviewSurface rectangle = PreviewSurfaceFactory.Create(h750.Profile!);
        PreviewSurface circle = PreviewSurfaceFactory.Create(round.Profile!);
        Assert(
            rectangle == new PreviewSurface(
                PreviewPixelFormat.Rgb565,
                PreviewShape.Rectangle,
                PreviewQuantization.Native,
                480,
                272,
                960),
            "H750 preview surface mapping changed.");
        Assert(
            circle == new PreviewSurface(
                PreviewPixelFormat.Rgb565,
                PreviewShape.Circle,
                PreviewQuantization.Native,
                240,
                240,
                480),
            "Round preview surface mapping changed.");
    }

    private static void PreviewProtocolRejectsHostileRecords()
    {
        byte[] valid = PreviewProtocol.EncodeHello(requestId: 7);
        byte[] invalidMagic = (byte[])valid.Clone();
        invalidMagic[sizeof(uint)] ^= 0xff;
        AssertThrows<PreviewProtocolException>(
            () => PreviewProtocol.DecodeRecord(invalidMagic.AsSpan(sizeof(uint))),
            "Protocol decoder accepted invalid magic.");

        byte[] inconsistentLength = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(
            inconsistentLength.AsSpan(sizeof(uint) + 24),
            value: 1);
        AssertThrows<PreviewProtocolException>(
            () => PreviewProtocol.DecodeRecord(inconsistentLength.AsSpan(sizeof(uint))),
            "Protocol decoder accepted an inconsistent payload length.");

        byte[] oversizedDamagePayload = new byte[PreviewProtocol.ResponseFixedPayloadBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(
            oversizedDamagePayload.AsSpan(12),
            checked((uint)PreviewLimits.MaximumDamageRegions + 1U));
        byte[] oversizedDamage = PreviewProtocol.EncodeRecord(
            PreviewProtocol.MessageType.Render,
            flags: 0,
            requestId: 9,
            oversizedDamagePayload);
        BinaryPrimitives.WriteUInt32LittleEndian(
            oversizedDamage.AsSpan(sizeof(uint) + 8),
            (uint)PreviewProtocol.MessageType.Render | PreviewProtocol.ResponseBit);
        PreviewProtocol.Record damageRecord = PreviewProtocol.DecodeRecord(
            oversizedDamage.AsSpan(sizeof(uint)));
        AssertThrows<PreviewProtocolException>(
            () => PreviewProtocol.DecodeRenderResponse(damageRecord, expectedRequestId: 9),
            "Protocol decoder accepted too many damage regions.");
    }

    // Linux and macOS workers return the frame after the render response instead of through a
    // named mapping; the decoder must take exactly the expected frame and nothing else.
    private static void PreviewProtocolCarriesInlineFrames()
    {
        PreviewSurface surface = new(PreviewPixelFormat.Rgb565, PreviewShape.Rectangle,
            PreviewQuantization.Native, Width: 2, Height: 2, StrideBytes: 4);
        byte[] request = PreviewProtocol.EncodeRender(
            requestId: 5, new PreviewRenderRequest(surface, new byte[] { 1 }), mappingName: null, pixelCapacity: 8);
        PreviewProtocol.Record encoded = PreviewProtocol.DecodeRecord(request.AsSpan(sizeof(uint)));
        Assert(
            BinaryPrimitives.ReadUInt32LittleEndian(encoded.Payload.AsSpan(36)) == 0 &&
                BinaryPrimitives.ReadUInt64LittleEndian(encoded.Payload.AsSpan(44)) == 8,
            "An inline render request must carry no mapping name and the frame capacity.");

        byte[] frame = [1, 2, 3, 4, 5, 6, 7, 8];
        PreviewProtocol.Record Response(ulong frameBytes, ReadOnlySpan<byte> pixels)
        {
            byte[] payload = new byte[PreviewProtocol.ResponseFixedPayloadBytes + pixels.Length];
            BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(16), frameBytes);
            pixels.CopyTo(payload.AsSpan(PreviewProtocol.ResponseFixedPayloadBytes));
            byte[] record = PreviewProtocol.EncodeRecord(PreviewProtocol.MessageType.Render, flags: 0, requestId: 5, payload);
            BinaryPrimitives.WriteUInt32LittleEndian(
                record.AsSpan(sizeof(uint) + 8),
                (uint)PreviewProtocol.MessageType.Render | PreviewProtocol.ResponseBit);
            return PreviewProtocol.DecodeRecord(record.AsSpan(sizeof(uint)));
        }
        PreviewProtocol.RenderResponse decoded =
            PreviewProtocol.DecodeRenderResponse(Response(8, frame), expectedRequestId: 5, inlineFrameBytes: 8);
        Assert(decoded.InlinePixels.AsSpan().SequenceEqual(frame), "The inline frame was not decoded.");
        AssertThrows<PreviewProtocolException>(
            () => PreviewProtocol.DecodeRenderResponse(Response(7, frame.AsSpan(0, 7)), 5, inlineFrameBytes: 8),
            "A short inline frame was accepted.");
        AssertThrows<PreviewProtocolException>(
            () => PreviewProtocol.DecodeRenderResponse(Response(8, frame), 5),
            "Trailing frame bytes were accepted by a shared-memory response.");
        Assert(
            PreviewProtocol.DecodeRenderResponse(Response(0, []), 5).InlinePixels.Length == 0,
            "A shared-memory response must carry no inline frame.");

        byte[] hello = new byte[PreviewProtocol.HelloPayloadBytes];
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"u8.CopyTo(hello.AsSpan(32));
        BinaryPrimitives.WriteUInt32LittleEndian(hello.AsSpan(28), PreviewProtocol.CapabilityInlinePixels);
        byte[] helloRecord = PreviewProtocol.EncodeRecord(PreviewProtocol.MessageType.Hello, flags: 0, requestId: 6, hello);
        BinaryPrimitives.WriteUInt32LittleEndian(
            helloRecord.AsSpan(sizeof(uint) + 8),
            (uint)PreviewProtocol.MessageType.Hello | PreviewProtocol.ResponseBit);
        Assert(
            PreviewProtocol.DecodeHello(PreviewProtocol.DecodeRecord(helloRecord.AsSpan(sizeof(uint))), 6).InlinePixels,
            "The inline-pixels capability bit was not decoded.");
    }

    private static void PreviewProtocolRejectsStaleResponses()
    {
        byte[] payload = new byte[PreviewProtocol.HelloPayloadBytes];
        byte[] encoded = PreviewProtocol.EncodeRecord(
            PreviewProtocol.MessageType.Hello,
            flags: 0,
            requestId: 41,
            payload);
        BinaryPrimitives.WriteUInt32LittleEndian(
            encoded.AsSpan(sizeof(uint) + 8),
            (uint)PreviewProtocol.MessageType.Hello | PreviewProtocol.ResponseBit);
        PreviewProtocol.Record record = PreviewProtocol.DecodeRecord(encoded.AsSpan(sizeof(uint)));
        AssertThrows<PreviewProtocolException>(
            () => PreviewProtocol.DecodeHello(record, expectedRequestId: 42),
            "Protocol decoder accepted a stale request ID.");
    }

    private static async Task NativePreviewWorkerRejectsUnknownMessagesAsync(string repositoryRoot)
    {
        string workerPath = PreviewWorkerPath(repositoryRoot);
        Assert(File.Exists(workerPath), $"Native preview worker is missing: {workerPath}");
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                Arguments = "--stdio",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        Assert(process.Start(), "Native preview worker did not start for protocol rejection testing.");
        try
        {
            const ulong unknownRequestId = 77;
            const uint unknownMessageType = 99;
            byte[] unknown = PreviewProtocol.EncodeRecord(
                (PreviewProtocol.MessageType)unknownMessageType,
                flags: 0,
                unknownRequestId,
                []);
            await process.StandardInput.BaseStream.WriteAsync(unknown).ConfigureAwait(false);
            await process.StandardInput.BaseStream.FlushAsync().ConfigureAwait(false);
            PreviewProtocol.Record rejected = await PreviewProtocol.ReadRecordAsync(
                process.StandardOutput.BaseStream,
                CancellationToken.None).ConfigureAwait(false);
            Assert(
                rejected.MessageType == (unknownMessageType | PreviewProtocol.ResponseBit) &&
                    rejected.RequestId == unknownRequestId &&
                    rejected.Payload.Length >= PreviewProtocol.ResponseFixedPayloadBytes &&
                    BinaryPrimitives.ReadUInt32LittleEndian(rejected.Payload) ==
                        (uint)PreviewProtocol.WorkerStatus.UnsupportedMessage,
                "Native preview worker did not reject an unknown message deterministically.");

            const ulong shutdownRequestId = 78;
            byte[] shutdown = PreviewProtocol.EncodeShutdown(shutdownRequestId);
            await process.StandardInput.BaseStream.WriteAsync(shutdown).ConfigureAwait(false);
            await process.StandardInput.BaseStream.FlushAsync().ConfigureAwait(false);
            PreviewProtocol.Record stopped = await PreviewProtocol.ReadRecordAsync(
                process.StandardOutput.BaseStream,
                CancellationToken.None).ConfigureAwait(false);
            Assert(
                stopped.MessageType == ((uint)PreviewProtocol.MessageType.Shutdown |
                                        PreviewProtocol.ResponseBit) &&
                    stopped.RequestId == shutdownRequestId &&
                    BinaryPrimitives.ReadUInt32LittleEndian(stopped.Payload) ==
                        (uint)PreviewProtocol.WorkerStatus.Ok,
                "Native preview worker did not acknowledge a bounded shutdown.");
            await process.WaitForExitAsync().ConfigureAwait(false);
            Assert(process.ExitCode == 0, "Native preview worker did not stop cleanly.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task NativePreviewWorkerRendersAndRestartsAsync(string repositoryRoot)
    {
        string workerPath = PreviewWorkerPath(repositoryRoot);
        Assert(File.Exists(workerPath), $"Native preview worker is missing: {workerPath}");
        string formPath = Path.Combine(
            repositoryRoot,
            "tests",
            "designer-fixtures",
            "document-compiler-smoke",
            "forms",
            "main.litheview.xml");
        byte[] snapshot = File.ReadAllBytes(formPath);
        byte[] originalFormHash = SHA256.HashData(snapshot);
        await using PreviewWorkerClient client = new(workerPath);
        PreviewWorkerCapabilities capabilities = await client.StartAsync().ConfigureAwait(false);
        Assert(capabilities.AbiVersion == PreviewLimits.AbiVersion, "Worker ABI negotiation differs.");
        Assert(capabilities.SchemaVersion == PreviewLimits.SchemaVersion, "Worker schema negotiation differs.");
        Assert(
            capabilities.WidgetCatalogSha256 == BuiltInWidgetCatalog.CatalogSha256,
            "Worker generated widget-catalog identity differs from the managed catalog.");

        Dictionary<PreviewPixelFormat, string> hashes = [];
        foreach (PreviewPixelFormat format in new[]
                 {
                     PreviewPixelFormat.Rgb565, PreviewPixelFormat.Rgb888,
                     PreviewPixelFormat.Xrgb8888, PreviewPixelFormat.Argb8888
                 })
        {
            int bytesPerPixel = format switch
            {
                PreviewPixelFormat.Rgb565 => 2,
                PreviewPixelFormat.Rgb888 => 3,
                PreviewPixelFormat.Xrgb8888 or PreviewPixelFormat.Argb8888 => 4,
                _ => throw new InvalidOperationException("Unexpected preview format.")
            };
            PreviewSurface surface = new(
                format,
                PreviewShape.Rectangle,
                PreviewQuantization.Native,
                Width: 480,
                Height: 272,
                StrideBytes: 480 * bytesPerPixel);
            PreviewInput[] inputs =
            [
                new(PreviewInputKind.Focus, Value: 1),
                new(PreviewInputKind.Key, (int)PreviewKey.Down),
                new(PreviewInputKind.Encoder, Value: -1),
                new(PreviewInputKind.PushButton, Value: 1),
                new(
                    PreviewInputKind.PointerEvent,
                    (int)PreviewPointerPhase.Pressed,
                    X: 12,
                    Y: 12,
                    TimestampMilliseconds: 10),
                new(
                    PreviewInputKind.PointerEvent,
                    (int)PreviewPointerPhase.Released,
                    X: 12,
                    Y: 12,
                    TimestampMilliseconds: 20),
                new(PreviewInputKind.ElapsedTime, Value: 16)
            ];
            PreviewRenderRequest request = new(surface, snapshot, inputs);
            PreviewFrame first = await client.RenderAsync(
                request,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            PreviewFrame second = await client.RenderAsync(
                request,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            PreviewFrame tiled = await client.RenderAsync(
                request with { RenderMode = PreviewRenderMode.Tiled },
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Assert(first.Status == PreviewStatus.Ok, $"{format} preview failed: {first.Diagnostic}");
            Assert(first.Pixels.Length == surface.RequiredFrameBytes(), $"{format} frame size differs.");
            Assert(first.DamageRegions.Count > 0, $"{format} preview produced no dirty region.");
            Assert(first.AcceptedInputCount == inputs.Length, $"{format} input count differs.");
            Assert(first.Pixels.AsSpan().SequenceEqual(second.Pixels), $"{format} preview is nondeterministic.");
            Assert(first.Pixels.AsSpan().SequenceEqual(tiled.Pixels),
                   $"{format} tiled preview differs from full-frame pixels.");
            hashes.Add(format, Convert.ToHexString(SHA256.HashData(first.Pixels)).ToLowerInvariant());
        }
        Dictionary<PreviewPixelFormat, string> expectedHashes = new()
        {
            [PreviewPixelFormat.Rgb565] =
                "ff0f216be37e1949d220421965a68910e6d7cb2fc488c4d8c12ddfbd8cae0ca7",
            [PreviewPixelFormat.Rgb888] =
                "c9b4043b1469c304d2789911625d05f6054204e77be08be5b15092aaddb57ad7",
            [PreviewPixelFormat.Xrgb8888] =
                "df1d498f2ff676e2c8f495566459c089731935f5c152aaf97a2b264e1966d100",
            [PreviewPixelFormat.Argb8888] =
                "df1d498f2ff676e2c8f495566459c089731935f5c152aaf97a2b264e1966d100"
        };
        Assert(
            hashes.All(item => expectedHashes[item.Key] == item.Value),
            "Real-renderer preview pixels differ from the checked per-format goldens.");

        Task<PreviewFrame> RenderDropDown(PreviewRenderRequest request) => client.RenderAsync(request, TimeSpan.FromSeconds(5)).AsTask();
        byte[] dropDownSnapshot = Encoding.UTF8.GetBytes(
            "<panel frame=\"0,0,160,160\" backgroundColor=\"0x00000000\">" +
            "<dropdown frame=\"10,10,100,24\" items=\"AUTO|MANUAL|OFF\" rowHeight=\"20\" textColor=\"0x00ffffff\"/>" +
            "<panel frame=\"0,40,160,100\" backgroundColor=\"0x000000ff\"/></panel>");
        PreviewInput[] openedInputs = [new(PreviewInputKind.Focus, 1), new(PreviewInputKind.Key, (int)PreviewKey.Select)];
        foreach (PreviewPixelFormat format in Enum.GetValues<PreviewPixelFormat>())
        {
            PreviewSurface surface = new(format, PreviewShape.Rectangle, PreviewQuantization.Native, 160, 160, 0);
            surface = surface with { StrideBytes = surface.RowBytes };
            PreviewFrame closed = await RenderDropDown(new(surface, dropDownSnapshot, [openedInputs[0]])).ConfigureAwait(false);
            PreviewRenderRequest openedRequest = new(surface, dropDownSnapshot, openedInputs);
            PreviewFrame opened = await RenderDropDown(openedRequest).ConfigureAwait(false);
            PreviewFrame tiled = await RenderDropDown(openedRequest with { RenderMode = PreviewRenderMode.Tiled }).ConfigureAwait(false);
            Assert(closed.Status == PreviewStatus.Ok && opened.Status == PreviewStatus.Ok && tiled.Status == PreviewStatus.Ok,
                $"{format} drop-down native preview failed.");
            Assert(!opened.Pixels.AsSpan().SequenceEqual(closed.Pixels), $"{format} popup did not render.");
            Assert(opened.Pixels.AsSpan().SequenceEqual(tiled.Pixels), $"{format} popup differs in tiled rendering.");
            PreviewFrame cancelled = await RenderDropDown(new(surface, dropDownSnapshot,
                [.. openedInputs, new(PreviewInputKind.Key, (int)PreviewKey.Down), new(PreviewInputKind.Key, (int)PreviewKey.Back)])).ConfigureAwait(false);
            Assert(cancelled.Status == PreviewStatus.Ok && cancelled.Pixels.AsSpan().SequenceEqual(closed.Pixels),
                $"{format} cancellation must restore the closed native frame.");
            PreviewFrame pointer = await RenderDropDown(new(surface, dropDownSnapshot,
                [.. openedInputs, new(PreviewInputKind.PointerEvent, (int)PreviewPointerPhase.Pressed, X: 20, Y: 60),
                    new(PreviewInputKind.PointerEvent, (int)PreviewPointerPhase.Released, X: 20, Y: 60)])).ConfigureAwait(false);
            PreviewFrame keyboard = await RenderDropDown(new(surface, dropDownSnapshot,
                [.. openedInputs, new(PreviewInputKind.Key, (int)PreviewKey.Down), new(PreviewInputKind.Key, (int)PreviewKey.Select)])).ConfigureAwait(false);
            Assert(pointer.Status == PreviewStatus.Ok && keyboard.Status == PreviewStatus.Ok &&
                pointer.Pixels.AsSpan().SequenceEqual(keyboard.Pixels) && !pointer.Pixels.AsSpan().SequenceEqual(closed.Pixels),
                $"{format} pointer and keyboard must commit the same row.");
        }

        byte[] roundSnapshot = Encoding.UTF8.GetBytes(
            "<panel frame=\"0,0,240,240\" backgroundColor=\"0x000000ff\">" +
            "<button frame=\"60,96,120,48\" text=\"ROUND\" canFocus=\"1\"/>" +
            "</panel>");
        PreviewSurface roundSurface = new(
            PreviewPixelFormat.Xrgb8888,
            PreviewShape.Circle,
            PreviewQuantization.Native,
            Width: 240,
            Height: 240,
            StrideBytes: 960);
        PreviewFrame round = await client.RenderAsync(
            new PreviewRenderRequest(roundSurface, roundSnapshot),
            timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert(round.Status == PreviewStatus.Ok, $"Round preview failed: {round.Diagnostic}");
        Assert(round.Pixels.AsSpan(0, 4).SequenceEqual(new byte[4]),
               "Round preview wrote outside the circular mask.");
        int roundCenter = (120 * roundSurface.StrideBytes) + (120 * 4);
        Assert(
            round.Pixels[roundCenter] != 0 || round.Pixels[roundCenter + 1] != 0 ||
                round.Pixels[roundCenter + 2] != 0 || round.Pixels[roundCenter + 3] != 0,
            "Round preview did not render inside the circular mask.");

        PreviewSurface redYellowSurface = new(
            PreviewPixelFormat.Rgb888,
            PreviewShape.Rectangle,
            PreviewQuantization.RedYellow2Bpp,
            Width: 480,
            Height: 272,
            StrideBytes: 1_440);
        PreviewFrame redYellow = await client.RenderAsync(
            new PreviewRenderRequest(redYellowSurface, snapshot),
            timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert(redYellow.Status == PreviewStatus.Ok, $"Red/yellow preview failed: {redYellow.Diagnostic}");
        bool physicalPaletteOnly = true;
        for (int offset = 0; offset < redYellow.Pixels.Length; offset += 3)
        {
            byte red = redYellow.Pixels[offset];
            byte green = redYellow.Pixels[offset + 1];
            byte blue = redYellow.Pixels[offset + 2];
            physicalPaletteOnly = physicalPaletteOnly && blue == 0 &&
                                  ((red == 0 && green == 0) ||
                                   (red == byte.MaxValue &&
                                    green is 0 or 128 or byte.MaxValue));
        }
        Assert(physicalPaletteOnly, "Red/yellow preview escaped the physical target palette.");

        PreviewSurface invalidSurface = new(
            PreviewPixelFormat.Rgb565,
            PreviewShape.Rectangle,
            PreviewQuantization.Native,
            Width: 480,
            Height: 272,
            StrideBytes: 960);
        PreviewFrame invalid = await client.RenderAsync(
            new PreviewRenderRequest(
                invalidSurface,
                Encoding.UTF8.GetBytes("<panel frame=\"0,0,480,272\"><unknown/></panel>")),
            timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert(
            invalid.Status == PreviewStatus.InvalidSnapshot && invalid.Pixels.Length == 0,
            "Invalid preview snapshot did not fail without pixels.");

        PreviewFrame schemaMismatch = await client.RenderAsync(
            new PreviewRenderRequest(invalidSurface, snapshot, SchemaVersion: 2),
            timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert(
            schemaMismatch.Status == PreviewStatus.SchemaMismatch &&
                schemaMismatch.Pixels.Length == 0,
            "A mismatched widget schema did not fail without pixels.");

        byte[] missingResourceSnapshot = Encoding.UTF8.GetBytes(
            "<panel frame=\"0,0,480,272\">" +
            "<image frame=\"8,8,16,16\" imageName=\"missing.bitmap\"/>" +
            "</panel>");
        PreviewFrame missingResource = await client.RenderAsync(
            new PreviewRenderRequest(invalidSurface, missingResourceSnapshot),
            timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert(
            missingResource.Status == PreviewStatus.MissingResource &&
                missingResource.Pixels.Length == 0,
            "A missing preview resource did not fail without pixels.");

        PreviewFrame unsupportedMode = await client.RenderAsync(
            new PreviewRenderRequest(
                invalidSurface,
                snapshot,
                RenderMode: (PreviewRenderMode)2),
            timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert(
            unsupportedMode.Status == PreviewStatus.InvalidArgument &&
                unsupportedMode.Pixels.Length == 0,
            "Unsupported preview request flags did not fail without pixels.");

        const int stressDimension = 4_096;
        PreviewSurface stressSurface = new(
            PreviewPixelFormat.Xrgb8888,
            PreviewShape.Rectangle,
            PreviewQuantization.Native,
            stressDimension,
            stressDimension,
            checked(stressDimension * 4));
        byte[] stressSnapshot = Encoding.UTF8.GetBytes(
            $"<panel frame=\"0,0,{stressDimension},{stressDimension}\" " +
            "backgroundColor=\"0x00112233\"/>");
        await AssertThrowsAsync<PreviewWorkerException>(
            () => client.RenderAsync(
                new PreviewRenderRequest(stressSurface, stressSnapshot),
                timeout: TimeSpan.FromMilliseconds(1)).AsTask(),
            "A preview deadline did not terminate the isolated worker.").ConfigureAwait(false);
        Assert(!client.IsRunning && client.LastFailedRequestId.HasValue,
               "Timed-out worker remained active or lost the failed request ID.");
        await client.StartAsync().ConfigureAwait(false);

        using (CancellationTokenSource cancellation = new())
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(1));
            await AssertThrowsAsync<PreviewWorkerException>(
                () => client.RenderAsync(
                    new PreviewRenderRequest(stressSurface, stressSnapshot),
                    timeout: TimeSpan.FromSeconds(5),
                    cancellation.Token).AsTask(),
                "Cancellation did not terminate the isolated worker.").ConfigureAwait(false);
        }
        Assert(!client.IsRunning, "Cancelled worker remained active.");
        await client.StartAsync().ConfigureAwait(false);

        int originalProcessId = client.WorkerProcessId ?? throw new InvalidOperationException(
            "Worker process ID is unavailable.");
        using (Process worker = Process.GetProcessById(originalProcessId))
        {
            worker.Kill(entireProcessTree: true);
            await worker.WaitForExitAsync().ConfigureAwait(false);
        }
        PreviewWorkerCapabilities restarted = await client.StartAsync().ConfigureAwait(false);
        Assert(
            restarted.AbiVersion == PreviewLimits.AbiVersion && client.RestartCount == 3,
            "A terminated worker did not negotiate a clean restart.");
        Assert(client.WorkerProcessId != originalProcessId, "Worker restart reused the terminated process.");

        byte[] finalFormHash = SHA256.HashData(File.ReadAllBytes(formPath));
        Assert(
            finalFormHash.AsSpan().SequenceEqual(originalFormHash),
            "Native worker changed its caller's project snapshot.");
    }

    // The windows-mingw-debug preset on Windows and host-debug elsewhere build the worker.
    private static string PreviewWorkerPath(string repositoryRoot) =>
        Environment.GetEnvironmentVariable("LITHEVIEW_PREVIEW_WORKER") ??
        (OperatingSystem.IsWindows()
            ? Path.Combine(repositoryRoot, "build", "windows-mingw-debug", "litheview_designer_preview_worker.exe")
            : Path.Combine(repositoryRoot, "build", "host-debug", "litheview_designer_preview_worker"));

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static WidgetCatalog LoadWidgetCatalog(string path)
    {
        WidgetCatalogLoadResult result = WidgetCatalogLoader.LoadFile(path);
        AssertValid(result);
        return result.Catalog!;
    }

    private static JsonElement ParseJson(string value)
    {
        using JsonDocument document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static void AssertCompilationEqual(
        WidgetSchemaCompilationResult expected,
        WidgetSchemaCompilationResult actual)
    {
        Assert(actual.CSharpUtf8!.AsSpan().SequenceEqual(expected.CSharpUtf8), "C# generation is not deterministic.");
        Assert(actual.CppUtf8!.AsSpan().SequenceEqual(expected.CppUtf8), "C++ generation is not deterministic.");
        Assert(
            actual.DocumentationUtf8!.AsSpan().SequenceEqual(expected.DocumentationUtf8),
            "Documentation generation is not deterministic.");
    }

    private static void AssertGeneratedFile(string path, ReadOnlySpan<byte> expected)
    {
        Assert(File.Exists(path), $"Generated artifact is missing: {path}");
        Assert(File.ReadAllBytes(path).AsSpan().SequenceEqual(expected), $"Generated artifact is stale: {path}");
    }

    private static string Hash(DisplaySurfaceProfile profile)
    {
        return DisplaySurfaceCanonicalizer.ComputeSha256(
            DisplaySurfaceCanonicalizer.Canonicalize(profile));
    }

    private static void AssertValid(DisplaySurfaceLoadResult result)
    {
        if (!result.IsValid)
        {
            string diagnostics = string.Join(
                "; ",
                result.Diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));
            throw new InvalidOperationException(diagnostics);
        }
    }

    private static void AssertValid(TargetProfileLoadResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(DesignerProjectLoadResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(DesignerProjectResolutionResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(DesignerWorkspaceLoadResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(WidgetCatalogLoadResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(WidgetSchemaCompilationResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(FormDocumentLoadResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(FormResolutionResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(FormXmlWriteResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(FormBudgetReport result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(FormCppCompilationResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(PreservedRegionMergeResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertValid(FormSurfaceValidationResult result)
    {
        AssertDiagnosticsValid(result.IsValid, result.Diagnostics);
    }

    private static void AssertDiagnosticsValid(
        bool isValid,
        IReadOnlyList<ProfileDiagnostic> diagnostics)
    {
        if (!isValid)
        {
            string text = string.Join(
                "; ",
                diagnostics.Select(item => $"{item.Code} {item.Path}: {item.Message}"));
            throw new InvalidOperationException(text);
        }
    }

    private static void AssertDiagnostic(DisplaySurfaceLoadResult result, string code)
    {
        Assert(!result.IsValid, $"Expected profile failure {code}.");
        Assert(result.Diagnostics.Any(item => item.Code == code), $"Expected diagnostic {code}.");
    }

    private static void AssertDiagnostic(DesignerProjectLoadResult result, string code)
    {
        Assert(!result.IsValid, $"Expected project-manifest failure {code}.");
        Assert(result.Diagnostics.Any(item => item.Code == code), $"Expected diagnostic {code}.");
    }

    private static void AssertDiagnostic(DesignerProjectResolutionResult result, string code)
    {
        Assert(!result.IsValid, $"Expected project-resolution failure {code}.");
        Assert(result.Diagnostics.Any(item => item.Code == code), $"Expected diagnostic {code}.");
    }

    private static void AssertDiagnostic(WidgetCatalogLoadResult result, string code)
    {
        Assert(!result.IsValid, $"Expected widget-catalog failure {code}.");
        Assert(result.Diagnostics.Any(item => item.Code == code), $"Expected diagnostic {code}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CMakeLists.txt")) &&
                Directory.Exists(Path.Combine(directory.FullName, "profiles")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Unable to locate the LitheView repository root.");
    }
}
