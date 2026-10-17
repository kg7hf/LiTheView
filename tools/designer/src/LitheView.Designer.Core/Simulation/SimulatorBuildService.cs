// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Simulation;

// Host tool orchestration only. Generated applications link installed C++ Core
// and do not call this service or depend on the managed designer at runtime.
public static class SimulatorBuildService
{
    public static async Task<SimulatorBuildResult> BuildAsync(
        string exportDirectory,
        string repositoryRoot,
        string configuration,
        Action<string> output,
        CancellationToken cancellationToken,
        string? buildDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);
        List<ProfileDiagnostic> diagnostics = [];
        try
        {
            if (configuration is not ("Debug" or "Release"))
            {
                throw new ArgumentException("Choose Debug or Release for the simulator build.");
            }
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Build and Run currently provides a Windows simulator. " +
                    "Other hosts can build the exported headless CMake check against an installed Core package.");
            }
            string project = Path.GetFullPath(exportDirectory);
            string repository = Path.GetFullPath(repositoryRoot);
            GeneratedFileAccess.RejectReparsePath(project);
            bool hadSuccessfulBuild = InvalidateOwnedReport(project, configuration, buildDirectory);
            SimulatorProjectManifest manifest = SimulatorFiles.LoadManifest(project) ??
                throw new ArgumentException("Export a simulation project before building it.");
            if (manifest.GenerationStatus != "complete")
            {
                throw new ArgumentException("Simulation generation is incomplete. Complete a successful export before building.");
            }
            SimulatorFiles.VerifyGeneratedFiles(project, manifest);
            string application = GeneratedFileAccess.Contained(project, "application.hpp");
            string applicationHash = GeneratedFileAccess.Hash(File.ReadAllBytes(application));
            if (!File.Exists(Path.Combine(repository, "CMakeLists.txt")) ||
                !File.Exists(Path.Combine(repository, "include", "lithe", "application.hpp")))
            {
                throw new ArgumentException("The selected library directory is not a LitheView checkout.");
            }
            string buildRoot = SimulatorBuildPaths.Resolve(project, buildDirectory);
            string build = GeneratedFileAccess.Contained(buildRoot, configuration.ToLowerInvariant());
            ClaimBuildDirectory(project, repository, build);
            string report = GeneratedFileAccess.Contained(build, "build-result.json");
            bool cleanCore = !(hadSuccessfulBuild || HasSuccessfulBuild(report));
            AtomicFileWriter.WriteAllBytes(report,
                JsonSerializer.SerializeToUtf8Bytes(new { passed = false, status = "incomplete" }, SimulatorJson.Options));
            string core = GeneratedFileAccess.Contained(build, "core");
            string installed = GeneratedFileAccess.Contained(build, "installed");
            string host = GeneratedFileAccess.Contained(build, "host");
            string logPath = GeneratedFileAccess.Contained(build, "build.log");
            using StreamWriter log = new(logPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            object logGate = new();
            void WriteLine(string line)
            {
                lock (logGate)
                {
                    log.WriteLine(line);
                    log.Flush();
                    output(line);
                }
            }
            WriteLine($"Building {configuration} {manifest.DestinationFormat} simulation; no hardware target selected.");
            await RunAsync("cmake", ["-S", repository, "-B", core, "-G", "MinGW Makefiles",
                $"-DCMAKE_BUILD_TYPE={configuration}", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",
                $"-DLITHEVIEW_DESTINATION_FORMAT={manifest.DestinationFormat}",
                "-DLITHEVIEW_BUILD_RENDERING_FIXTURES=OFF", "-DLITHEVIEW_BUILD_TESTS=OFF",
                "-DLITHEVIEW_BUILD_WIN32=OFF", "-DLITHEVIEW_BUILD_STM32U545_PROBE=OFF",
                "-DLITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF"], project, WriteLine, cancellationToken).ConfigureAwait(false);
            string[] coreBuildArguments = cleanCore
                ? ["--build", core, "--parallel", "2", "--clean-first"]
                : ["--build", core, "--parallel", "2"];
            await RunAsync("cmake", coreBuildArguments, project, WriteLine, cancellationToken).ConfigureAwait(false);
            await RunAsync("cmake", ["--install", core, "--prefix", installed], project, WriteLine, cancellationToken).ConfigureAwait(false);
            await RunAsync("cmake", ["-S", project, "-B", host, "-G", "MinGW Makefiles",
                $"-DCMAKE_BUILD_TYPE={configuration}", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",
                $"-DCMAKE_PREFIX_PATH={installed}"], project, WriteLine, cancellationToken).ConfigureAwait(false);
            // A cancelled compiler can leave an object newer than a subsequent
            // application edit. Never accept host objects from an earlier run.
            await RunAsync("cmake", ["--build", host, "--parallel", "2", "--clean-first"], project, WriteLine, cancellationToken).ConfigureAwait(false);
            await RunAsync("ctest", ["--test-dir", host, "--output-on-failure"], project, WriteLine, cancellationToken).ConfigureAwait(false);
            // An external editor may modify the project during a build. Do not
            // launch a binary for inputs different from the ones we accepted.
            SimulatorFiles.VerifyGeneratedFiles(project, manifest);
            SimulatorProjectManifest? after = SimulatorFiles.LoadManifest(project);
            if (after?.GenerationStatus != "complete" ||
                !JsonSerializer.SerializeToUtf8Bytes(after, SimulatorJson.Options).AsSpan().SequenceEqual(
                    JsonSerializer.SerializeToUtf8Bytes(manifest, SimulatorJson.Options)) ||
                GeneratedFileAccess.Hash(File.ReadAllBytes(application)) != applicationHash)
            {
                throw new InvalidDataException("Simulation inputs changed during the build; export and build again.");
            }
            string executable = GeneratedFileAccess.Contained(host, "litheview_generated_simulator.exe");
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException("The build did not produce the expected simulator executable.", executable);
            }
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFileWriter.WriteAllBytes(report, JsonSerializer.SerializeToUtf8Bytes(new
            {
                passed = true,
                configuration,
                manifest.DestinationFormat,
                manifest.FormSha256,
                manifest.DisplayDefinitionSha256,
                manifest.LogicalInputSha256,
                applicationSha256 = applicationHash,
                executable,
                executableSha256 = GeneratedFileAccess.Hash(File.ReadAllBytes(executable)),
                coreArchiveSha256 = GeneratedFileAccess.Hash(File.ReadAllBytes(Path.Combine(installed, "lib", "liblitheview_core.a"))),
                renderConfigurationSha256 = GeneratedFileAccess.Hash(File.ReadAllBytes(Path.Combine(installed, "include", "lithe", "render_config.hpp"))),
                scope = "Host compilation and CTest passed. GUI execution and hardware feasibility are separate."
            }, SimulatorJson.Options));
            WriteLine($"Simulation build and checks passed: {executable}");
            return new() { ExecutablePath = executable, Diagnostics = diagnostics };
        }
        catch (OperationCanceledException)
        {
            diagnostics.Add(SimulatorJson.Error("LVSIM2002", "$.build", "Simulation build cancelled; no executable was launched."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or
            JsonException or InvalidOperationException or InvalidDataException or Win32Exception or PlatformNotSupportedException)
        {
            diagnostics.Add(SimulatorJson.Error("LVSIM2001", "$.build", exception.Message));
        }
        return new() { Diagnostics = diagnostics };
    }

    private static void ClaimBuildDirectory(string project, string repository, string build)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string library = Path.TrimEndingDirectorySeparator(repository);
        string libraryBuild = Path.Combine(library, "build") + Path.DirectorySeparatorChar;
        if (library.Equals(build, comparison) || library.StartsWith(build + Path.DirectorySeparatorChar, comparison) ||
            build.StartsWith(library + Path.DirectorySeparatorChar, comparison) && !build.StartsWith(libraryBuild, comparison))
        {
            throw new ArgumentException("Simulator build output must be outside library sources (or under the library's build directory).");
        }
        string owner = GeneratedFileAccess.Contained(build, "build-owner.json");
        byte[] identity = BuildIdentity(project, build);
        if (File.Exists(owner))
        {
            if (!File.ReadAllBytes(owner).AsSpan().SequenceEqual(identity))
            {
                throw new ArgumentException("This build directory belongs to a different UI component. Choose a separate simulator build directory.");
            }
            return;
        }
        // Adopt an older designer build only in its original component/build
        // location. Arbitrary existing application build trees are never adopted.
        string legacyParent = Path.Combine(project, "build");
        bool legacyBuild = string.Equals(Path.GetDirectoryName(build), legacyParent, comparison) &&
            File.Exists(Path.Combine(build, "build-result.json"));
        if (!legacyBuild && Directory.Exists(build) && Directory.EnumerateFileSystemEntries(build).Any())
        {
            throw new ArgumentException("The simulator build directory already contains unowned files. Choose an empty build directory.");
        }
        AtomicFileWriter.WriteAllBytes(owner, identity);
    }

    private static byte[] BuildIdentity(string project, string build) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = 1,
        component = Path.GetRelativePath(build, project).Replace('\\', '/')
    }, SimulatorJson.Options);

    private static bool InvalidateOwnedReport(string project, string configuration, string? buildDirectory)
    {
        // A preflight failure must not leave a previous successful result for
        // this attempt. Never create/adopt a directory merely to mark failure.
        string build = GeneratedFileAccess.Contained(SimulatorBuildPaths.Resolve(project, buildDirectory), configuration.ToLowerInvariant());
        string owner = GeneratedFileAccess.Contained(build, "build-owner.json");
        if (File.Exists(owner) && File.ReadAllBytes(owner).AsSpan().SequenceEqual(BuildIdentity(project, build)))
        {
            string report = GeneratedFileAccess.Contained(build, "build-result.json");
            bool passed = HasSuccessfulBuild(report);
            AtomicFileWriter.WriteAllBytes(report,
                JsonSerializer.SerializeToUtf8Bytes(new { passed = false, status = "incomplete" }, SimulatorJson.Options));
            return passed;
        }
        return false;
    }

    private static bool HasSuccessfulBuild(string report)
    {
        if (!File.Exists(report))
        {
            return false;
        }
        try
        {
            using JsonDocument previous = JsonDocument.Parse(File.ReadAllBytes(report));
            return previous.RootElement.ValueKind == JsonValueKind.Object &&
                previous.RootElement.TryGetProperty("passed", out JsonElement passed) &&
                passed.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            // A damaged result marker cannot establish reusable build state.
            return false;
        }
    }

    private static async Task RunAsync(string program, string[] arguments, string directory,
        Action<string> output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfo start = new(program)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        output($"> {program} {string.Join(' ', arguments)}");
        using Process process = new() { StartInfo = start };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start {program}.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"Cannot run {program}. Put CMake, CTest, MinGW GCC and mingw32-make " +
                "on the designer's PATH, then restart it. " + exception.Message, exception);
        }
        using CancellationTokenSource streamCancellation = new();
        Task stdout = ReadLinesAsync(process.StandardOutput, output, streamCancellation.Token);
        Task stderr = ReadLinesAsync(process.StandardError, output, streamCancellation.Token);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                // Wait for root termination and inherited output handles to
                // close. Root exit alone does not prove descendant completion.
                await Task.WhenAll(process.WaitForExitAsync(CancellationToken.None), stdout, stderr)
                    .WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                throw new InvalidOperationException("The cancelled build did not finish closing its process and output streams within five seconds. " +
                    "No executable was accepted; check the build log and remaining compiler processes.", exception);
            }
            finally
            {
                // On timeout, stop readers without another unbounded wait. Any
                // eventual disposal fault is observed; cancelled readers cannot
                // deliver later output into the disposed build log.
                await streamCancellation.CancelAsync().ConfigureAwait(false);
                ObserveReaderFailure(stdout);
                ObserveReaderFailure(stderr);
            }
            throw;
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{program} failed with exit code {process.ExitCode}. " +
                "See the simulation build log; an existing executable was not accepted.");
        }
    }

    private static void ObserveReaderFailure(Task reader)
    {
        _ = reader.ContinueWith(static completed => { _ = completed.Exception; },
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task ReadLinesAsync(StreamReader reader, Action<string> output, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output(line);
        }
    }
}
