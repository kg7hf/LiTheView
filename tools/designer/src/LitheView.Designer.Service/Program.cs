// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.Json;

namespace LitheView.Designer.Service;

internal static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        try
        {
            (string library, string? worker) = ParseArguments(arguments);
            Console.InputEncoding = new UTF8Encoding(false, true);
            Console.OutputEncoding = new UTF8Encoding(false);
            await using DesignerService service = new(library, worker);
            await RunAsync(Console.In, Console.Out, service).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
            UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
    }

    internal static async Task RunAsync(TextReader input, TextWriter output, DesignerService service)
    {
        char[] buffer = new char[4096];
        StringBuilder line = new();
        bool oversized = false;
        int count;
        while ((count = await input.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) != 0)
        {
            for (int index = 0; index < count; ++index)
            {
                char value = buffer[index];
                if (value == '\n')
                {
                    await WriteResponseAsync(line, oversized, output, service).ConfigureAwait(false);
                    line.Clear();
                    oversized = false;
                }
                else if (!oversized)
                {
                    if (line.Length == Protocol.MaximumLineCharacters)
                    {
                        oversized = true;
                        line.Clear();
                    }
                    else
                    {
                        line.Append(value);
                    }
                }
            }
        }
        if (line.Length != 0 || oversized)
        {
            await WriteResponseAsync(line, oversized, output, service).ConfigureAwait(false);
        }
    }

    private static async Task WriteResponseAsync(StringBuilder line, bool oversized,
        TextWriter output, DesignerService service)
    {
        ServiceResponse response = oversized
            ? new(0, false, Error: new("RequestTooLarge", "The request line exceeds the 8 MiB character limit."))
            : await service.HandleAsync(line.ToString()).ConfigureAwait(false);
        await output.WriteLineAsync(JsonSerializer.Serialize(response, Protocol.JsonOptions)).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private static (string Library, string? Worker) ParseArguments(string[] arguments)
    {
        string? library = null;
        string? worker = null;
        for (int index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 == arguments.Length)
            {
                throw new ArgumentException("Expected --library-root <absolute path> [--preview-worker <absolute path>].");
            }
            string value = arguments[index + 1];
            if (!Path.IsPathFullyQualified(value))
            {
                throw new ArgumentException("Service startup paths must be absolute.");
            }
            switch (arguments[index])
            {
                case "--library-root" when library is null: library = value; break;
                case "--preview-worker" when worker is null: worker = value; break;
                default: throw new ArgumentException("Unknown or repeated service argument: " + arguments[index]);
            }
        }
        return (library ?? throw new ArgumentException("--library-root is required."), worker);
    }
}
