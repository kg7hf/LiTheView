// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service;

internal sealed partial class DesignerService
{
    private sealed record InputFileEdit(string Path, string Before, string After);

    // Read-only proposal: VS Code owns the paired document edit and save lifecycle.
    // No display definition, form, handwritten handler or generated artifact is rewritten here.
    private static object ConfigureInput(Context context, RequestParameters parameters)
    {
        DesignerWorkspace workspace = RequireForm(context);
        if (workspace.Manifest.SchemaVersion is not (2 or 3))
        {
            throw new ServiceException("InputMigrationRequired",
                "Configure Input requires a version-2 or version-3 project with an independent logical-input snapshot.");
        }
        string inputPath = Path.Combine(workspace.ProjectDirectory, DesignerProductResolver.InputRelativePath);
        string manifestBefore = ReadInputDocument(context.ManifestPath);
        string inputBefore = ReadInputDocument(inputPath);
        DesignerProjectLoadResult manifest = DesignerProjectLoader.Load(Encoding.UTF8.GetBytes(manifestBefore));
        LogicalInputConfigurationLoadResult input = LogicalInputConfigurationLoader.Load(Encoding.UTF8.GetBytes(inputBefore));
        if (!manifest.IsValid || manifest.Sha256 != workspace.ResolvedProject.ManifestSha256 ||
            !input.IsValid || input.Sha256 != context.Display.InputSha256)
        {
            throw new ServiceException("ProjectChanged", "The project input changed while it was read. Retry Configure Input.");
        }
        string[] choices = ["outputOnly", "touch", "touchKeys", "encoderSelect"];
        string? current = choices.FirstOrDefault(choice =>
        {
            LogicalInputConfigurationProfile candidate = SelectInput(choice);
            return candidate with { Id = input.Configuration!.Id } == input.Configuration;
        });
        List<InputFileEdit> edits = [];
        if (parameters.LogicalInput is { } choice)
        {
            LogicalInputConfigurationProfile selected = SelectInput(choice);
            if (choice != current)
            {
                byte[] bytes = LogicalInputConfigurationCanonicalizer.Canonicalize(selected);
                DesignerProjectManifest updated = workspace.Manifest with
                {
                    LogicalInput = new() { Id = selected.Id, Sha256 = DisplaySurfaceCanonicalizer.ComputeSha256(bytes) }
                };
                // Snapshot first; a partial save leaves a hash mismatch and export fails closed.
                edits.Add(new(inputPath, inputBefore, Encoding.UTF8.GetString(bytes)));
                edits.Add(new(context.ManifestPath, manifestBefore,
                    Encoding.UTF8.GetString(DesignerProjectCanonicalizer.Canonicalize(updated))));
            }
        }
        return new { documentVersion = parameters.DocumentVersion, currentInput = current, edits };
    }

    private static string ReadInputDocument(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > Protocol.MaximumXmlBytes)
        {
            throw new ServiceException("InvalidProject", "The input configuration document exceeds 1 MiB.");
        }
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
