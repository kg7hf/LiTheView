// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using LitheView.Designer.Core.DisplayBuild;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Cli;

internal sealed record TargetCommandResult(
    string? ProfileSha256, string? EvidenceSha256, TargetFeasibilityResult Feasibility);

internal static class TargetCommands
{
    private const int MaximumEvidenceBytes = 1_048_576;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        Converters =
        {
            new EvidenceEnumConverter<TargetMetric>(),
            new EvidenceEnumConverter<TargetEvidenceKind>(),
            new EvidenceEnumConverter<ResourcePlacement>()
        }
    };

    internal static TargetCommandResult Evaluate(
        DisplayApplicationCompilationResult surface,
        ResolvedFormDocument form,
        DisplayDefinitionProfile definition,
        string targetPath,
        string evidencePath)
    {
        TargetProfileLoadResult target = TargetProfileLoader.LoadFile(targetPath);
        if (!target.IsValid || target.Profile is null)
        {
            return Failed(surface, target.Sha256, null, target.Diagnostics);
        }
        string? evidenceHash = null;
        try
        {
            TargetFeasibilityOptions evidence = new();
            if (evidencePath != "-")
            {
                using FileStream stream = File.OpenRead(evidencePath);
                if (stream.Length > MaximumEvidenceBytes)
                {
                    throw new JsonException("Target evidence exceeds the 1 MiB input limit.");
                }
                byte[] bytes = new byte[checked((int)stream.Length)];
                stream.ReadExactly(bytes);
                evidenceHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
                RejectDuplicates(document.RootElement);
                evidence = JsonSerializer.Deserialize<TargetFeasibilityOptions>(bytes, Options)
                    ?? throw new JsonException("Target evidence must be an object.");
            }
            return new(target.Sha256, evidenceHash,
                TargetFeasibilityEvaluator.Evaluate(surface, form, definition, target.Profile, evidence));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Failed(surface, target.Sha256, evidenceHash,
                [new("LVTF0001", ProfileDiagnosticSeverity.Error, evidencePath,
                    $"Target evidence could not be loaded: {exception.Message}")]);
        }
    }

    private static TargetCommandResult Failed(
        DisplayApplicationCompilationResult surface, string? targetHash, string? evidenceHash,
        IReadOnlyList<ProfileDiagnostic> diagnostics) => new(targetHash, evidenceHash, new()
        {
            SurfaceStatus = surface.SurfaceStatus,
            TargetStatus = ValidationStageStatus.Failed,
            EstimatedResourceRomBytes = surface.EstimatedResourceRomBytes,
            Constraints = [],
            Diagnostics = diagnostics
        });

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException($"Duplicate target-evidence property '{property.Name}'.");
                }
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicates(item);
            }
        }
    }

    // Evidence is an interchange contract: accept only the schema's exact names.
    private sealed class EvidenceEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly Dictionary<string, TEnum> sValues = Enum.GetValues<TEnum>()
            .ToDictionary(value => JsonNamingPolicy.CamelCase.ConvertName(value.ToString()), StringComparer.Ordinal);

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String ||
                !sValues.TryGetValue(reader.GetString()!, out TEnum value))
            {
                throw new JsonException($"Expected an exact camelCase {typeof(TEnum).Name} name.");
            }
            return value;
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(
                Enum.GetName(value) ?? throw new JsonException($"Undefined {typeof(TEnum).Name}.")));
    }
}
