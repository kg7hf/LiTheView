// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.IO;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Simulation;

// Legacy simulator manifest validation, also used when migrating to a UI component.
internal static class SimulatorFiles
{
    internal static SimulatorProjectManifest? LoadManifest(string directory)
    {
        string path = GeneratedFileAccess.Contained(directory, SimulatorJson.ManifestName);
        if (!File.Exists(path))
        {
            return null;
        }
        if (new FileInfo(path).Length > 1_048_576)
        {
            throw new ArgumentException("Simulation manifest exceeds its size limit.");
        }
        List<ProfileDiagnostic> diagnostics = [];
        SimulatorProjectManifest? manifest = DisplayDefinitionJson.Read<SimulatorProjectManifest>(
            File.ReadAllBytes(path), "LVSIM", diagnostics);
        if (diagnostics.Count != 0)
        {
            throw new InvalidDataException(string.Join("; ", diagnostics.Select(item => item.Message)));
        }
        if (manifest is null || manifest.SchemaVersion != 1 ||
            manifest.GenerationStatus is not ("complete" or "incomplete") ||
            manifest.SurfaceStatus != "passed" || manifest.TargetStatus != "unevaluated" ||
            manifest.DestinationFormat is not ("RGB565" or "RGB888" or "XRGB8888" or "ARGB8888") ||
            manifest.GeneratedFiles is null || manifest.GeneratedFiles.Count is 0 or > 1024 ||
            manifest.UserFiles is null || !manifest.UserFiles.SequenceEqual(["application.hpp"]) ||
            !ProfileRules.IsLowercaseSha256(manifest.FormSha256) ||
            !ProfileRules.IsLowercaseSha256(manifest.DisplayDefinitionSha256) ||
            !ProfileRules.IsLowercaseSha256(manifest.LogicalInputSha256))
        {
            throw new ArgumentException("The simulation manifest is invalid; export the project again.");
        }
        foreach ((string relative, string hash) in manifest.GeneratedFiles)
        {
            _ = GeneratedFileAccess.Contained(directory, relative);
            if (relative is "application.hpp" or SimulatorJson.ManifestName || !ProfileRules.IsLowercaseSha256(hash))
            {
                throw new ArgumentException("The simulation manifest has an invalid generated-file entry.");
            }
        }
        return manifest;
    }

    internal static void VerifyGeneratedFiles(string directory, SimulatorProjectManifest manifest)
    {
        foreach ((string relative, string hash) in manifest.GeneratedFiles)
        {
            string path = GeneratedFileAccess.Contained(directory, relative);
            if (!File.Exists(path) || GeneratedFileAccess.Hash(File.ReadAllBytes(path)) != hash)
            {
                throw new ArgumentException($"Generated file '{relative}' is missing or edited. " +
                    "Keep application changes in application.hpp; reconcile generated changes before exporting or building.");
            }
        }
    }
}
