// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using LitheView.Designer.Core.IO;

namespace LitheView.Designer.Core.Segments;

public sealed record SegmentCompilation(byte[] HeaderUtf8, string GlassSha256, string FormSha256, string OwnershipLine);

public static class SegmentCppCompiler
{
    public static SegmentCompilation Compile(ReadOnlyMemory<byte> glassUtf8, ReadOnlyMemory<byte> formUtf8)
    {
        SegmentGlass glass = SegmentDocuments.ReadGlass(glassUtf8);
        SegmentForm form = SegmentDocuments.ReadForm(formUtf8, glass);
        string glassHash = SegmentDocuments.GlassHash(glass);
        string formHash = GeneratedFileAccess.Hash(formUtf8.Span);
        string owner = $"// LiTheView segmented output; owner: {glass.Id}/{form.Id}\n";
        StringBuilder cpp = new(owner);
        cpp.AppendLine(CultureInfo.InvariantCulture, $"// Glass SHA-256: {glassHash}");
        cpp.AppendLine(CultureInfo.InvariantCulture, $"// Form source SHA-256: {formHash}");
        cpp.AppendLine("// Generated presentation only. Application state and device behavior remain handwritten.");
        cpp.AppendLine("#pragma once\n#include <lithe/segment_frame.hpp>\n#include <array>\n#include <cstdint>\n#include <optional>\n#include <string_view>");
        cpp.AppendLine(CultureInfo.InvariantCulture, $"namespace lithe_generated::screen_{form.Id}\n{{");
        cpp.AppendLine(CultureInfo.InvariantCulture, $"using Frame = lithe::SegmentFrame<{N(glass.SegmentCount)}>;\nstruct Values\n{{");
        foreach (SegmentBinding binding in form.Bindings)
        {
            string type = glass.Icons.Any(f => f.Id == binding.Field) ? "bool" :
                glass.Texts.Any(f => f.Id == binding.Field) ? "std::string_view" : "std::uint32_t";
            cpp.AppendLine(CultureInfo.InvariantCulture, $"    {type} field_{binding.Input}{{}};");
        }
        cpp.AppendLine("};");
        foreach (SegmentBinding binding in form.Bindings)
        {
            SegmentText? text = glass.Texts.FirstOrDefault(f => f.Id == binding.Field);
            SegmentNumber? number = glass.Numbers.FirstOrDefault(f => f.Id == binding.Field);
            if (text is not null) EmitGlyphWriter(cpp, binding.Input, text.Cells, text.Glyphs);
            if (number is not null) EmitGlyphWriter(cpp, binding.Input, number.Cells, number.Glyphs);
        }
        cpp.AppendLine("[[nodiscard]] inline constexpr std::optional<Frame> encode(const Values& values) noexcept\n{\n    Frame frame{};");
        if (form.Bindings.Length == 0) cpp.AppendLine("    (void)values;");
        foreach (SegmentBinding binding in form.Bindings)
        {
            string name = binding.Input;
            string value = $"values.field_{name}";
            if (glass.Icons.FirstOrDefault(f => f.Id == binding.Field) is { } icon)
            {
                foreach (int id in icon.Segments) cpp.AppendLine(CultureInfo.InvariantCulture, $"    if (!frame.set({N(id)}U, {value})) return {{}};");
            }
            else if (glass.Groups.FirstOrDefault(f => f.Id == binding.Field) is { } group)
            {
                cpp.AppendLine(CultureInfo.InvariantCulture, $"    if ({value} > {N(group.Maximum)}U) return {{}};");
                // Widen before multiplication. The range, count and half-up policy are explicit.
                cpp.AppendLine(CultureInfo.InvariantCulture, $"    const auto count_{name} = (std::uint64_t{{{value}}} * {N(group.Segments.Length)}U + {N(group.Maximum / 2)}U) / {N(group.Maximum)}U;");
                for (int i = 0; i < group.Segments.Length; ++i)
                    cpp.AppendLine(CultureInfo.InvariantCulture, $"    if (!frame.set({N(group.Segments[i])}U, {N(i)}U < count_{name})) return {{}};");
            }
            else if (glass.Texts.FirstOrDefault(f => f.Id == binding.Field) is { } text)
            {
                cpp.AppendLine(CultureInfo.InvariantCulture, $"    if ({value}.size() > {N(text.Cells.Length)}U) return {{}};");
                cpp.AppendLine(CultureInfo.InvariantCulture, $"    for (std::size_t i = 0; i < {value}.size(); ++i)\n        if (!write_{name}(frame, i, {value}[i])) return {{}};");
            }
            else if (glass.Numbers.FirstOrDefault(f => f.Id == binding.Field) is { } number)
            {
                uint maximum = 1;
                for (int i = 0; i < number.Cells.Length; ++i) maximum *= 10;
                cpp.AppendLine(CultureInfo.InvariantCulture, $"    if ({value} >= {maximum.ToString(CultureInfo.InvariantCulture)}U) return {{}};");
                cpp.AppendLine(CultureInfo.InvariantCulture, $"    auto rest_{name} = {value};\n    for (std::size_t remaining = {N(number.Cells.Length)}U; remaining > 0; --remaining)\n    {{");
                // A fixed table avoids an integral-to-char cast and keeps ASCII independent of locale.
                cpp.AppendLine(CultureInfo.InvariantCulture, $"        constexpr std::string_view digits{{\"0123456789\"}};\n        if (!write_{name}(frame, remaining - 1U, digits[rest_{name} % 10U])) return {{}};\n        rest_{name} /= 10U;\n        if (rest_{name} == 0) break;\n    }}");
            }
        }
        cpp.AppendLine("    return frame;\n}\n}");
        return new(Encoding.UTF8.GetBytes(cpp.ToString().Replace("\r\n", "\n", StringComparison.Ordinal)), glassHash, formHash, owner);
    }

    // Single output, same-directory replace. Never overwrite a handwritten file or another form.
    public static void Export(ReadOnlyMemory<byte> glassUtf8, ReadOnlyMemory<byte> formUtf8, string outputHeader)
    {
        SegmentCompilation result = Compile(glassUtf8, formUtf8);
        string full = Path.GetFullPath(outputHeader);
        if (!string.Equals(Path.GetExtension(full), ".hpp", StringComparison.Ordinal))
            throw new ArgumentException("Segment output must be a .hpp file.", nameof(outputHeader));
        GeneratedFileAccess.RejectReparsePath(full);
        GeneratedFileAccess.RejectFileAncestors(full);
        if (File.Exists(full))
        {
            using StreamReader reader = File.OpenText(full);
            if (reader.ReadLine() != result.OwnershipLine.TrimEnd('\n'))
                throw new IOException("Refusing to overwrite an unowned file or another form's output.");
        }
        AtomicFileWriter.WriteAllBytes(full, result.HeaderUtf8);
    }

    private static void EmitGlyphWriter(StringBuilder cpp, string name, int[][] cells, SegmentGlyph[] glyphs)
    {
        // Namespace-scope immutable tables do not consume the embedded call stack.
        cpp.AppendLine(CultureInfo.InvariantCulture, $"inline constexpr std::array<std::array<std::size_t, {N(cells[0].Length)}>, {N(cells.Length)}> cells_{name}{{{{");
        foreach (int[] cell in cells) cpp.AppendLine("        {{" + string.Join(", ", cell.Select(id => N(id) + "U")) + "}},");
        cpp.AppendLine("}};");
        cpp.AppendLine(CultureInfo.InvariantCulture, $"inline constexpr bool write_{name}(Frame& frame, std::size_t position, char character) noexcept\n{{\n    if (position >= {N(cells.Length)}U) return false;\n    switch (character)\n    {{");
        foreach (SegmentGlyph glyph in glyphs.OrderBy(g => g.Character, StringComparer.Ordinal))
        {
            cpp.AppendLine(CultureInfo.InvariantCulture, $"    case {N(glyph.Character[0])}:");
            foreach (int stroke in glyph.Strokes) cpp.AppendLine(CultureInfo.InvariantCulture, $"        if (!frame.set(cells_{name}[position][{N(stroke)}U])) return false;");
            cpp.AppendLine("        return true;");
        }
        cpp.AppendLine("    default: return false;\n    }\n}");
    }

    private static string N(int number) => number.ToString(CultureInfo.InvariantCulture);
}
