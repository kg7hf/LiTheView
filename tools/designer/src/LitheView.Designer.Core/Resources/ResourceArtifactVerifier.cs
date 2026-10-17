// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public sealed record VerifiedResourceArtifact(string Path, long ByteCount, string Sha256);

public sealed record ResourceArtifactVerificationResult
{
    public required IReadOnlyList<VerifiedResourceArtifact> Artifacts { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class ResourceArtifactVerifier
{
    public static ResourceArtifactVerificationResult Verify(
        ResourceManifest manifest,
        string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        List<ProfileDiagnostic> diagnostics = [];
        List<VerifiedResourceArtifact> artifacts = [];
        VerifyArtifact(
            projectRoot,
            manifest.Source.Path,
            manifest.Source.ByteCount,
            manifest.Source.Sha256,
            "$.source",
            diagnostics,
            artifacts);

        ResourceLicense? license = manifest.Source.Provenance?.License;
        if (license?.LicenseTextPath is not null && license.LicenseTextSha256 is not null)
        {
            VerifyArtifact(
                projectRoot,
                license.LicenseTextPath,
                expectedByteCount: null,
                license.LicenseTextSha256,
                "$.source.provenance.license",
                diagnostics,
                artifacts);
        }

        foreach (ResourceVariant variant in manifest.Variants)
        {
            VerifyArtifact(
                projectRoot,
                variant.Output.Path,
                variant.Output.ByteCount,
                variant.Output.Sha256,
                $"$.variants[{variant.Id}].output",
                diagnostics,
                artifacts);
        }

        return new ResourceArtifactVerificationResult
        {
            Artifacts = artifacts,
            Diagnostics = diagnostics
        };
    }

    private static void VerifyArtifact(
        string projectRoot,
        string relativePath,
        long? expectedByteCount,
        string expectedSha256,
        string diagnosticPath,
        List<ProfileDiagnostic> diagnostics,
        List<VerifiedResourceArtifact> artifacts)
    {
        if (!TryResolveContainedPath(projectRoot, relativePath, out string path))
        {
            AddError(
                diagnostics,
                "LVRM2001",
                diagnosticPath,
                $"Artifact path '{relativePath}' resolves outside the project root.");
            return;
        }

        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
            long actualByteCount = stream.Length;
            string actualSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            artifacts.Add(new VerifiedResourceArtifact(relativePath, actualByteCount, actualSha256));
            if (expectedByteCount is not null && actualByteCount != expectedByteCount)
            {
                AddError(
                    diagnostics,
                    "LVRM2002",
                    $"{diagnosticPath}.byteCount",
                    $"Artifact '{relativePath}' contains {actualByteCount} bytes; " +
                        $"the manifest locks {expectedByteCount} bytes.");
            }
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    "LVRM2003",
                    $"{diagnosticPath}.sha256",
                    $"Artifact '{relativePath}' does not match its locked SHA-256 hash.");
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            AddError(
                diagnostics,
                "LVRM2004",
                diagnosticPath,
                $"Unable to read artifact '{relativePath}': {exception.Message}");
        }
    }

    private static bool TryResolveContainedPath(
        string projectRoot,
        string relativePath,
        out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (!ProfileRules.IsSafeProjectPath(relativePath))
        {
            return false;
        }

        try
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string nativeRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, nativeRelativePath));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            resolvedPath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }
}
