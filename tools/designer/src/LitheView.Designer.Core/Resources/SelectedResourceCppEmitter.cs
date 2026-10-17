// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Resources;

public sealed record SelectedResourceSource(string Path, string Sha256);

public sealed record SelectedResourceCppEmissionResult
{
    public string? HeaderText { get; init; }

    public required IReadOnlyList<SelectedResourceSource> RequiredSources { get; init; }

    // Payload, palette, and font-record estimates from validated metadata;
    // generated UTF-8 source text and provider code size are not counted as ROM.
    public required long EstimatedRomBytes { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => HeaderText is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class SelectedResourceCppEmitter
{
    private static readonly HashSet<string> sCppKeywords = new(
        ("alignas alignof and and_eq asm auto bitand bitor bool break case catch char char8_t " +
         "char16_t char32_t class compl concept const consteval constexpr constinit const_cast " +
         "continue co_await co_return co_yield decltype default delete do double dynamic_cast else " +
         "enum explicit export extern false float for friend goto if inline int long mutable " +
         "namespace new noexcept not not_eq nullptr operator or or_eq private protected public " +
         "register reinterpret_cast requires return short signed sizeof static static_assert " +
         "static_cast struct switch template this thread_local throw true try typedef typeid " +
         "typename union unsigned using virtual void volatile wchar_t while xor xor_eq").Split(' '),
        StringComparer.Ordinal);

    private sealed record Binding(string ResourceId, string RuntimeName, string EntryPoint,
        ResourceKind Kind, int FrameCount);

    public static SelectedResourceCppEmissionResult Emit(
        ResourceSurfaceResolutionResult resolution,
        IReadOnlyDictionary<string, string> runtimeNames,
        string className = "SelectedResources")
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(runtimeNames);
        List<ProfileDiagnostic> diagnostics = [.. resolution.Diagnostics];
        List<Binding> bindings = [];
        List<SelectedResourceSource> sources = [];
        if (!FontConversionRequestValidator.IsCppIdentifier(className) ||
            className[0] is not (>= 'A' and <= 'Z'))
        {
            AddError(diagnostics, "LVRM3201", "A provider class name must be an upper-camel C++ identifier.");
        }
        HashSet<string> emittedNames = new(StringComparer.Ordinal);
        HashSet<string> entryPoints = new(StringComparer.Ordinal);
        HashSet<string> sourcePaths = new(StringComparer.Ordinal);
        long romBytes = 0;
        foreach ((string resourceId, string runtimeName) in runtimeNames.OrderBy(
            item => item.Key, StringComparer.Ordinal))
        {
            ResolvedResourceVariant[] matches = [.. resolution.Variants.Where(
                item => item.ResourceId == resourceId)];
            if (matches.Length != 1 || !ProfileRules.IsValidIdentifier(resourceId) ||
                !FormSymbolRules.IsRuntimeName(runtimeName))
            {
                AddError(diagnostics, "LVRM3202",
                    $"Binding '{resourceId}' requires exactly one selected resource and a valid runtime name.");
                continue;
            }
            ResolvedResourceVariant selected = matches[0];
            ResourceVariant variant = selected.Variant;
            if (!selected.OutputVerified || !ProfileRules.IsSafeProjectPath(variant.Output.Path) ||
                !variant.Output.Path.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                !ProfileRules.IsLowercaseSha256(variant.Output.Sha256) ||
                variant.Integration.Lifetime != ResourceLifetime.Static ||
                !sourcePaths.Add(variant.Output.Path))
            {
                AddError(diagnostics, "LVRM3203",
                    $"Resource '{resourceId}' requires a unique verified C++ source with static lifetime.");
                continue;
            }
            if (!variant.Converter.Options.TryGetValue("entryPoint", out string? entryPoint) ||
                !FontConversionRequestValidator.IsCppIdentifier(entryPoint) ||
                entryPoint[0] is not (>= 'a' and <= 'z') || sCppKeywords.Contains(entryPoint) ||
                !entryPoints.Add(entryPoint))
            {
                AddError(diagnostics, "LVRM3204",
                    $"Resource '{resourceId}' has no valid unique converter entry point.");
                continue;
            }
            long cost = PayloadCost(selected);
            if (cost < 0)
            {
                AddError(diagnostics, "LVRM3205",
                    $"Resource '{resourceId}' does not have a supported checked converter contract and payload metadata.");
                continue;
            }
            int frameCount = selected.Kind == ResourceKind.Animation ? variant.Output.FrameCount : 1;
            bool namesValid = true;
            for (int frame = 0; frame < frameCount; ++frame)
            {
                string name = selected.Kind == ResourceKind.Animation
                    ? runtimeName + "-f" + frame.ToString(CultureInfo.InvariantCulture)
                    : runtimeName;
                namesValid &= emittedNames.Add(name);
            }
            if (!namesValid)
            {
                AddError(diagnostics, "LVRM3206", $"Runtime names for resource '{resourceId}' collide.");
                continue;
            }
            try
            {
                romBytes = checked(romBytes + cost);
            }
            catch (OverflowException)
            {
                AddError(diagnostics, "LVRM3207", "Selected resource ROM estimates overflow the host measurement range.");
                continue;
            }
            bindings.Add(new Binding(resourceId, runtimeName, entryPoint, selected.Kind, frameCount));
            sources.Add(new SelectedResourceSource(variant.Output.Path, variant.Output.Sha256));
        }
        bool valid = diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
        return new SelectedResourceCppEmissionResult
        {
            HeaderText = valid ? WriteHeader(bindings, className) : null,
            RequiredSources = valid ? sources : [],
            EstimatedRomBytes = valid ? romBytes : 0,
            Diagnostics = diagnostics
        };
    }

    private static long PayloadCost(ResolvedResourceVariant selected)
    {
        ResourceVariant variant = selected.Variant;
        return selected.Kind switch
        {
            ResourceKind.Font when variant.Converter.Id == "litheview.converter.font.gdiplus" &&
                variant.Converter.Version == "2.0.0" && variant.Font is not null &&
                variant.Image is null && variant.Animation is null => variant.Font.EstimatedRomBytes,
            ResourceKind.Image or ResourceKind.Icon when
                variant.Converter.Id == "litheview.converter.image.gdiplus" &&
                variant.Converter.Version == "1.0.0" && variant.Image is not null &&
                variant.Font is null && variant.Animation is null => variant.Image.EstimatedRomBytes,
            ResourceKind.Animation when variant.Converter.Id == "litheview.converter.animation.gdiplus" &&
                variant.Converter.Version == "1.0.0" && variant.Animation is not null &&
                variant.Font is null && variant.Image is null && variant.Output.FrameCount is >= 2 and <= 256 =>
                variant.Animation.EstimatedRomBytes,
            _ => -1
        };
    }

    private static string WriteHeader(List<Binding> bindings, string className)
    {
        StringBuilder text = new();
        text.AppendLine("#pragma once");
        text.AppendLine("#include \"lithe/layout.hpp\"");
        text.AppendLine("#include <array>");
        text.AppendLine("#include <span>");
        text.AppendLine("#include <string_view>");
        text.AppendLine();
        text.AppendLine("// Compile only the hash-locked RequiredSources listed by the export result.");
        text.AppendLine("// Descriptors and payloads are borrowed from static converter output.");
        text.AppendLine("namespace lithe");
        text.AppendLine("{");
        foreach (Binding binding in bindings)
        {
            string type = binding.Kind switch
            {
                ResourceKind.Font => "const GeneratedFont&",
                ResourceKind.Animation => "std::span<const Bitmap>",
                _ => "const Bitmap&"
            };
            text.AppendLine(CultureInfo.InvariantCulture, $"{type} {binding.EntryPoint}() noexcept;");
        }
        text.AppendLine("}");
        text.AppendLine("namespace lithe::generated");
        text.AppendLine("{");
        text.AppendLine(CultureInfo.InvariantCulture, $"class {className} final : public ResourceProvider");
        text.AppendLine("{");
        text.AppendLine("public:");
        WriteLookup(text, "Bitmap", "bitmap", "kBitmaps");
        WriteLookup(text, "Font", "font", "kFonts");
        text.AppendLine("private:");
        text.AppendLine("    template <typename T> struct Binding");
        text.AppendLine("    {");
        text.AppendLine("        std::string_view resourceId;");
        text.AppendLine("        std::string_view runtimeName;");
        text.AppendLine("        const T* (*get)() noexcept;");
        text.AppendLine("    };");
        List<string> bitmaps = [];
        List<string> fonts = [];
        int getterIndex = 0;
        foreach (Binding binding in bindings)
        {
            for (int frame = 0; frame < binding.FrameCount; ++frame)
            {
                string getter = "getResource" + getterIndex.ToString(CultureInfo.InvariantCulture);
                string type = binding.Kind == ResourceKind.Font ? "Font" : "Bitmap";
                text.AppendLine(CultureInfo.InvariantCulture, $"    static const {type}* {getter}() noexcept");
                text.AppendLine("    {");
                string name = binding.RuntimeName;
                if (binding.Kind == ResourceKind.Animation)
                {
                    string index = frame.ToString(CultureInfo.InvariantCulture) + "U";
                    name += "-f" + frame.ToString(CultureInfo.InvariantCulture);
                    text.AppendLine(CultureInfo.InvariantCulture, $"        const auto frames = lithe::{binding.EntryPoint}();");
                    text.AppendLine(CultureInfo.InvariantCulture, $"        return frames.size() > {index} ? &frames[{index}] : nullptr;");
                }
                else
                {
                    text.AppendLine(CultureInfo.InvariantCulture, $"        return &lithe::{binding.EntryPoint}();");
                }
                text.AppendLine("    }");
                string row = $"        {{\"{binding.ResourceId}\", \"{EscapeLiteral(name)}\", &{getter}}},";
                (binding.Kind == ResourceKind.Font ? fonts : bitmaps).Add(row);
                ++getterIndex;
            }
        }
        WriteTable(text, "Bitmap", "kBitmaps", bitmaps);
        WriteTable(text, "Font", "kFonts", fonts);
        text.AppendLine("};");
        text.AppendLine("} // namespace lithe::generated");
        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void WriteLookup(StringBuilder text, string type, string method, string table)
    {
        text.AppendLine(CultureInfo.InvariantCulture, $"    const {type}* {method}(std::string_view name) const noexcept override");
        text.AppendLine("    {");
        text.AppendLine(CultureInfo.InvariantCulture, $"        for (const auto& entry : {table})");
        text.AppendLine("        {");
        text.AppendLine("            if (entry.runtimeName == name)");
        text.AppendLine("            {");
        text.AppendLine("                return entry.get();");
        text.AppendLine("            }");
        text.AppendLine("        }");
        text.AppendLine("        return nullptr;");
        text.AppendLine("    }");
    }

    private static void WriteTable(StringBuilder text, string type, string name, List<string> rows)
    {
        string count = rows.Count.ToString(CultureInfo.InvariantCulture);
        text.AppendLine(CultureInfo.InvariantCulture, $"    inline static constexpr std::array<Binding<{type}>, {count}U> {name}{{{{");
        foreach (string row in rows)
        {
            text.AppendLine(row);
        }
        text.AppendLine("    }};");
    }

    private static void AddError(List<ProfileDiagnostic> diagnostics, string code, string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, "$.resources", message));
    }

    private static string EscapeLiteral(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);
}
