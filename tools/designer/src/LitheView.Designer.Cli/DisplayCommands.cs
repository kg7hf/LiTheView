// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.DisplayBuild;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Resources;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.Cli;

internal static class DisplayCommands
{
    private static readonly JsonSerializerOptions ReportOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static int Run(string[] arguments)
    {
        bool export = arguments.Length >= 7 && arguments[1] == "export-cpp";
        bool targetCheck = arguments.Length >= 8 && arguments[1] == "check-target";
        if (!export && !targetCheck && !(arguments.Length >= 6 && arguments[1] == "check"))
        {
            Console.Error.WriteLine(
                "display check <form.xml> <widgets.json> <display.json> <input.json> [resource-manifest ...]");
            Console.Error.WriteLine(
                "display export-cpp <form.xml> <widgets.json> <display.json> <input.json> <output-directory> [resource-manifest ...]");
            Console.Error.WriteLine(
                "display check-target <form.xml> <widgets.json> <display.json> <input.json> <target.json> <evidence.json|-> [resource-manifest ...]");
            Console.Error.WriteLine("Resource-manifest paths are relative to the form's directory.");
            return 2;
        }

        try
        {
            return RunValidated(arguments, export, targetCheck);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"LVDB0001 error $: {exception.Message}");
            return 1;
        }
    }

    private static int RunValidated(string[] arguments, bool export, bool targetCheck)
    {
        WidgetCatalogLoadResult catalog = WidgetCatalogLoader.LoadFile(arguments[3]);
        DisplayDefinitionLoadResult definition = DisplayDefinitionProfileLoader.LoadFile(arguments[4]);
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.LoadFile(arguments[5]);
        WriteDiagnostics(catalog.Diagnostics);
        if (!catalog.IsValid || catalog.Catalog is null)
        {
            WriteDiagnostics(definition.Diagnostics);
            WriteDiagnostics(input.Diagnostics);
            return 1;
        }
        FormDocumentLoadResult form = FormXmlImporter.LoadFile(
            arguments[2], catalog.Catalog, FormImportMode.Strict);
        WriteDiagnostics(form.Diagnostics);
        if (!form.IsValid || form.Document is null)
        {
            WriteDiagnostics(definition.Diagnostics);
            WriteDiagnostics(input.Diagnostics);
            return 1;
        }
        string projectRoot = Path.GetDirectoryName(Path.GetFullPath(arguments[2]))!;
        ProjectResourceSetLoadResult resources = ProjectResourceSetLoader.Load(
            arguments.Skip(targetCheck ? 8 : export ? 7 : 6), projectRoot);
        DisplayApplicationCompilationResult compiled = DisplayApplicationCompiler.Compile(
            form.Document, catalog.Catalog, definition, input, resources);
        WriteDiagnostics(compiled.Diagnostics);

        TargetCommandResult? target = null;
        if (targetCheck && compiled.IsValid && definition.Profile is not null)
        {
            FormResolutionResult resolved = FormDocumentResolver.Resolve(form.Document, catalog.Catalog);
            if (resolved.Document is not null)
            {
                target = TargetCommands.Evaluate(compiled, resolved.Document, definition.Profile,
                    arguments[6], arguments[7]);
                WriteDiagnostics(target.Feasibility.Diagnostics);
            }
        }

        string surfaceStatus = compiled.SurfaceStatus == ValidationStageStatus.Passed ? "passed" : "failed";
        var report = new
        {
            SchemaVersion = 1,
            ExportStatus = !compiled.IsValid ? "notProduced" : export ? "complete" : "notRequested",
            SurfaceStatus = surfaceStatus,
            TargetStatus = target?.Feasibility.TargetStatus.ToString().ToLowerInvariant() ?? "unevaluated",
            Target = target,
            DisplayId = definition.Profile?.Id,
            DisplaySha256 = definition.Sha256,
            InputId = input.Configuration?.Id,
            InputSha256 = input.Sha256,
            compiled.FormSha256,
            DestinationFormat = definition.Profile?.Format.ToString(),
            ResourceRom = new { Bytes = compiled.EstimatedResourceRomBytes, Evidence = "estimated" },
            Resources = compiled.Resources.Select(item => new
            {
                item.ResourceId,
                item.ResourceManifestSha256,
                VariantId = item.Variant.Id,
                Format = item.Variant.Output.Format.ToString(),
                item.Variant.Output.Sha256,
                SourceFile = $"assets/{item.Variant.Output.Sha256}.cpp"
            }).ToArray(),
            Diagnostics = compiled.Diagnostics
        };
        byte[] reportBytes = JsonSerializer.SerializeToUtf8Bytes(report, ReportOptions);
        if (!compiled.IsValid || target?.Feasibility.IsValid == false ||
            target?.Feasibility.TargetStatus == ValidationStageStatus.Failed)
        {
            Console.WriteLine(Encoding.UTF8.GetString(reportBytes));
            return 1;
        }
        if (!export)
        {
            Console.WriteLine(Encoding.UTF8.GetString(reportBytes));
            return 0;
        }

        // Resolve and hash every selected source before writing any generated file.
        // Generated paths depend on content hashes, not the checkout's absolute path.
        SortedDictionary<string, byte[]> files = new(StringComparer.Ordinal);
        foreach ((string path, byte[] contents) in compiled.Files)
        {
            files.Add(path, contents);
        }
        StringBuilder cmake = new("# Generated selected sources; include after creating the consumer target.\nset(LITHEVIEW_SELECTED_RESOURCE_SOURCES\n");
        foreach (ResolvedResourceVariant resource in compiled.Resources)
        {
            string sourcePath = Path.GetFullPath(Path.Combine(projectRoot, resource.Variant.Output.Path));
            string rootPrefix = Path.TrimEndingDirectorySeparator(projectRoot) + Path.DirectorySeparatorChar;
            if (!sourcePath.StartsWith(rootPrefix,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new ArgumentException("A selected resource source leaves the form directory.");
            }
            byte[] contents = File.ReadAllBytes(sourcePath);
            string hash = Convert.ToHexStringLower(SHA256.HashData(contents));
            if (hash != resource.Variant.Output.Sha256 || contents.LongLength != resource.Variant.Output.ByteCount)
            {
                Console.Error.WriteLine($"LVDB0002 error {sourcePath}: Selected resource changed after validation.");
                return 1;
            }
            string generatedPath = $"assets/{hash}.cpp";
            if (files.TryAdd(generatedPath, contents))
            {
                cmake.Append("    \"${CMAKE_CURRENT_LIST_DIR}/").Append(generatedPath).Append("\"\n");
            }
        }
        cmake.Append(")\n");
        files.Add("resources.cmake", Encoding.UTF8.GetBytes(cmake.ToString()));
        string outputRoot = Path.GetFullPath(arguments[6]);
        // Invalidate a prior completion marker before replacing generated files.
        // A write failure then leaves an explicitly incomplete generation.
        AtomicFileWriter.WriteAllBytes(Path.Combine(outputRoot, "report.json"),
            JsonSerializer.SerializeToUtf8Bytes(report with { ExportStatus = "incomplete" }, ReportOptions));
        foreach ((string path, byte[] contents) in files)
        {
            AtomicFileWriter.WriteAllBytes(Path.Combine(outputRoot, path), contents);
        }
        // The report is written last; a nonzero CLI exit never certifies a completed export.
        AtomicFileWriter.WriteAllBytes(Path.Combine(outputRoot, "report.json"), reportBytes);
        Console.WriteLine(Encoding.UTF8.GetString(reportBytes));
        return 0;
    }

    private static void WriteDiagnostics(IEnumerable<ProfileDiagnostic> diagnostics)
    {
        foreach (ProfileDiagnostic diagnostic in diagnostics)
        {
            Console.Error.WriteLine(
                $"{diagnostic.Code} {diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Path}: {diagnostic.Message}");
        }
    }
}
