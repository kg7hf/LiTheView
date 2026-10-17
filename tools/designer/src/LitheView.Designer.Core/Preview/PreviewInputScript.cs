// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace LitheView.Designer.Core.Preview;

// A bounded, text-only input sequence for headless preview. It names the same focus,
// encoder, push-button, key, pointer, and elapsed-time records that the native worker
// already accepts; parsing never executes code or reads files.
public static class PreviewInputScript
{
    public static bool TryParse(
        IReadOnlyList<string> specifications,
        out PreviewInput[] inputs,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(specifications);
        inputs = [];
        if (specifications.Count > PreviewLimits.MaximumInputs)
        {
            error = $"A preview accepts at most {PreviewLimits.MaximumInputs} inputs.";
            return false;
        }
        List<PreviewInput> parsed = [];
        foreach (string specification in specifications)
        {
            if (!TryParseOne(specification, out PreviewInput input))
            {
                error = $"Preview input '{specification}' is not recognized. Use focus, next, previous, " +
                    "select, key:<up|down|left|right|select|back>, press|move|release|cancel:<x>,<y>, " +
                    "or elapsed:<milliseconds>.";
                return false;
            }
            parsed.Add(input);
        }
        inputs = [.. parsed];
        error = string.Empty;
        return true;
    }

    private static bool TryParseOne(string specification, out PreviewInput input)
    {
        input = default;
        switch (specification)
        {
            case "focus":
                input = new PreviewInput(PreviewInputKind.Focus, 1);
                return true;
            case "next":
                input = new PreviewInput(PreviewInputKind.Encoder, 1);
                return true;
            case "previous":
                input = new PreviewInput(PreviewInputKind.Encoder, -1);
                return true;
            case "select":
                input = new PreviewInput(PreviewInputKind.PushButton, 1);
                return true;
            default:
                break;
        }

        int separator = specification.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return false;
        }
        string verb = specification[..separator];
        string argument = specification[(separator + 1)..];
        if (verb == "key")
        {
            PreviewKey? key = argument switch
            {
                "up" => PreviewKey.Up,
                "down" => PreviewKey.Down,
                "left" => PreviewKey.Left,
                "right" => PreviewKey.Right,
                "select" => PreviewKey.Select,
                "back" => PreviewKey.Back,
                _ => null
            };
            if (key is null)
            {
                return false;
            }
            input = new PreviewInput(PreviewInputKind.Key, (int)key.Value);
            return true;
        }
        if (verb == "elapsed")
        {
            if (!int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds))
            {
                return false;
            }
            input = new PreviewInput(PreviewInputKind.ElapsedTime, milliseconds);
            return true;
        }

        PreviewPointerPhase? phase = verb switch
        {
            "press" => PreviewPointerPhase.Pressed,
            "move" => PreviewPointerPhase.Moved,
            "release" => PreviewPointerPhase.Released,
            "cancel" => PreviewPointerPhase.Cancelled,
            _ => null
        };
        string[] coordinates = argument.Split(',');
        if (phase is null ||
            coordinates.Length != 2 ||
            !int.TryParse(coordinates[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(coordinates[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y))
        {
            return false;
        }
        input = new PreviewInput(PreviewInputKind.PointerEvent, (int)phase.Value, x, y);
        return true;
    }
}
