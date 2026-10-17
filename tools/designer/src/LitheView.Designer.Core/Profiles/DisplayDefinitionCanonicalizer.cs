// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;

namespace LitheView.Designer.Core.Profiles;

public static class DisplayDefinitionCanonicalizer
{
    public static byte[] Canonicalize(DisplayDefinitionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        DisplayDefinitionProfile ordered = profile with
        {
            ResourceFormats = [.. profile.ResourceFormats.OrderBy(
                StrictCamelCaseEnumConverter<ResourceFormat>.ToJsonName, StringComparer.Ordinal)]
        };
        return JsonSerializer.SerializeToUtf8Bytes(ordered, DisplayDefinitionJson.Options);
    }
}

public static class LogicalInputConfigurationCanonicalizer
{
    public static byte[] Canonicalize(LogicalInputConfigurationProfile configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return JsonSerializer.SerializeToUtf8Bytes(configuration, DisplayDefinitionJson.Options);
    }
}
