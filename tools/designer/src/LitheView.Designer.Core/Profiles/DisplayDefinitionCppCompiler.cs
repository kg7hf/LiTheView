// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Text.Json;

namespace LitheView.Designer.Core.Profiles;

public sealed record DisplayDefinitionCppCompilationResult
{
    public byte[]? HeaderUtf8 { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
    public bool IsValid => HeaderUtf8 is not null &&
        Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);
}

public static class DisplayDefinitionCppCompiler
{
    public static DisplayDefinitionCppCompilationResult Compile(
        DisplayDefinitionLoadResult definition,
        LogicalInputConfigurationProfile inputs,
        string symbol = "DisplayConfiguration")
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(inputs);
        List<ProfileDiagnostic> diagnostics = [.. definition.Diagnostics,
            .. LogicalInputConfigurationLoader.Validate(inputs)];
        if (!definition.IsValid || definition.Profile is null || definition.Sha256 is null)
        {
            DisplayDefinitionJson.Error(diagnostics, "LVDD4001", "$", "C++ export requires a validated display definition.");
        }
        else
        {
            diagnostics.AddRange(DisplayDefinitionValidator.Validate(definition.Profile));
            if (diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error) &&
                DisplaySurfaceCanonicalizer.ComputeSha256(
                    DisplayDefinitionCanonicalizer.Canonicalize(definition.Profile)) != definition.Sha256)
            {
                DisplayDefinitionJson.Error(diagnostics, "LVDD4004", "$", "Display definition changed after its hash was resolved.");
            }
        }
        if (definition.LegacySurface is { NativeDisplay.ColorModel: not NativeColorModel.Rgb })
        {
            DisplayDefinitionJson.Error(diagnostics, "LVDD4002", "$",
                "Legacy synthetic packed display RGB staging cannot be exported without its historical presentation mapping; select an explicit native definition.");
        }
        if (!IsSymbol(symbol))
        {
            DisplayDefinitionJson.Error(diagnostics, "LVDD4003", "$.symbol", "The generated type requires a safe C++ identifier.");
        }
        if (diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return new() { Diagnostics = diagnostics };
        }
        DisplayDefinitionProfile profile = definition.Profile!;
        DisplayDefinitionGeometry geometry = profile.Geometry;
        SafeAreaProfile safe = geometry.SafeArea;
        string shape = geometry.Shape == SurfaceShape.Circle ? "circle" : "rectangle";
        string inputHash = DisplaySurfaceCanonicalizer.ComputeSha256(
            LogicalInputConfigurationCanonicalizer.Canonicalize(inputs));
        string fields = string.Join("\n", profile.Fields.Select((field, index) =>
            FormattableString.Invariant($"    static_assert(kNative.fields[{index}U].channel == NativeChannel::{StrictCamelCaseEnumConverter<DisplayFieldChannel>.ToJsonName(field.Channel)} && kNative.fields[{index}U].bitOffset == {field.BitOffset}U && kNative.fields[{index}U].bitWidth == {field.BitWidth}U && kNative.fields[{index}U].position.x == {field.Position.X} && kNative.fields[{index}U].position.y == {field.Position.Y});")));
        string header = FormattableString.Invariant($$"""
            // Generated from a validated display definition; target feasibility is separate.
            #pragma once
            #include "lithe/display_definition.hpp"
            #include "lithe/render_config.hpp"
            #include <string_view>

            namespace lithe::generated
            {
            struct {{symbol}} final
            {
                static constexpr std::string_view kSourceSha256{ {{Quote(definition.Sha256!)}} };
                static constexpr std::string_view kInputSha256{ {{Quote(inputHash)}} };
                static constexpr DisplayDefinition kDefinition{
                    {{Quote(profile.Id)}}, { {{geometry.Width}}, {{geometry.Height}} }, ViewportShape::{{shape}},
                    { {{safe.X}}, {{safe.Y}}, {{safe.Width}}, {{safe.Height}} },
                    PixelFormat::{{FormatName(profile.Format)}}, DisplayAddressSpace::{{AddressName(profile.AddressSpace)}}
                };
                static constexpr LogicalInputConfiguration kInput{
                    {{Boolean(inputs.Pointer)}}, {{Boolean(inputs.Keys)}}, {{Boolean(inputs.Text)}}, {{Boolean(inputs.Encoder)}}, {{Boolean(inputs.Select)}}
                };
                static_assert(validateDisplayDefinition(kDefinition).has_value());
                static constexpr auto kViewport = *displayViewport(kDefinition);
                static constexpr PixelFormat kFormat{kDefinition.format};
                static_assert(supportsDestination(kFormat), "The library build must enable the selected destination format.");
                static constexpr auto kNative = *nativeDescriptor(kFormat);
                static_assert(kNative.storageBlock.width == {{profile.StorageBlock.Width}});
                static_assert(kNative.storageBlock.height == {{profile.StorageBlock.Height}});
                static_assert(kNative.storageBlock.bytes == {{profile.StorageBlock.Bytes}}U);
                static_assert(kNative.fieldCount == {{profile.Fields.Length}}U);
                static_assert(kNative.channelModel == NativeChannelModel::{{StrictCamelCaseEnumConverter<DisplayChannelModel>.ToJsonName(profile.ChannelModel)}});
                static_assert(kNative.storesDestinationAlpha == {{Boolean(profile.Composition.DestinationAlpha)}});
                static_assert(kNative.supportsSourceAlpha == {{Boolean(profile.Composition.SourceAlpha)}});
                static_assert(kNative.supportsSourceCoverage == {{Boolean(profile.Composition.Coverage)}});
                static_assert(kNative.supportsSourceOver == {{Boolean(profile.Composition.SourceOver)}});
            {{fields}}
            };
            } // namespace lithe::generated
            """);
        return new()
        {
            HeaderUtf8 = Encoding.UTF8.GetBytes(header.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n"),
            Diagnostics = diagnostics
        };
    }

    private static string FormatName(SurfacePixelFormat format) => format switch
    {
        SurfacePixelFormat.Rgb565 => "rgb565", SurfacePixelFormat.Rgb888 => "rgb888",
        SurfacePixelFormat.Xrgb8888 => "xrgb8888", SurfacePixelFormat.Argb8888 => "argb8888",
        SurfacePixelFormat.R2Packed2x2 => "r2Packed2x2", SurfacePixelFormat.Rrrr2222 => "rrrr2222",
        SurfacePixelFormat.Ryry2222 => "ryry2222",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static string AddressName(DisplayAddressSpace space) => space switch
    {
        DisplayAddressSpace.Pixels => "pixels", DisplayAddressSpace.Cells => "cells",
        DisplayAddressSpace.Emitters => "emitters", _ => throw new ArgumentOutOfRangeException(nameof(space))
    };

    private static string Boolean(bool value) => value ? "true" : "false";
    private static string Quote(string value) => JsonSerializer.Serialize(value);
    private static bool IsSymbol(string value) =>
        value.Length is > 0 and <= 128 && char.IsAsciiLetter(value[0]) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') &&
        !value.Contains("__", StringComparison.Ordinal) &&
        Documents.FormSymbolRules.ToCppIdentifier(value) == value;
}
