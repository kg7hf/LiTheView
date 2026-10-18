// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Xml;
using LitheView.Designer.Core.Segments;

namespace LitheView.Designer.Cli;

internal static class SegmentCommands
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length == 5 && arguments[1] == "create-project")
        {
            try { Console.WriteLine(SegmentProject.Create(arguments[4], ReadBounded(arguments[2]), ReadBounded(arguments[3]))); return 0; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { Console.Error.WriteLine(error.Message); return 1; }
        }
        if (arguments.Length == 4 && arguments[1] == "export-project")
        {
            try { Console.WriteLine(SegmentProject.Export(arguments[2], arguments[3])); return 0; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or XmlException) { Console.Error.WriteLine(error.Message); return 1; }
        }
        bool export = arguments.Length == 5 && arguments[1] == "export-cpp";
        bool check = arguments.Length == 4 && arguments[1] == "check";
        bool hash = arguments.Length == 3 && arguments[1] == "glass-hash";
        if (!export && !check && !hash)
        {
            Console.Error.WriteLine("segment create-project <glass.json> <input.json> <new-directory>\nsegment export-project <project.json> <library-root>\nsegment glass-hash <glass.json>\nsegment check <glass.json> <form.xml>\nsegment export-cpp <glass.json> <form.xml> <output.hpp>");
            return 2;
        }
        try
        {
            byte[] glass = ReadBounded(arguments[2]);
            if (hash)
            {
                Console.WriteLine(SegmentDocuments.GlassHash(SegmentDocuments.ReadGlass(glass)));
                return 0;
            }
            byte[] form = ReadBounded(arguments[3]);
            SegmentCompilation compiled = SegmentCppCompiler.Compile(glass, form);
            if (export) SegmentCppCompiler.Export(glass, form, arguments[4]);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                displayKind = "segmented", displayStatus = "passed", targetStatus = "unevaluated",
                glassSha256 = compiled.GlassSha256, formSha256 = compiled.FormSha256,
                exportStatus = export ? "complete" : "notRequested"
            }));
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or XmlException)
        {
            Console.Error.WriteLine($"LVSG0001 error: {error.Message}");
            return 1;
        }
    }

    private static byte[] ReadBounded(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > SegmentDocuments.MaximumBytes) throw new InvalidDataException("Segment document is too large.");
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
