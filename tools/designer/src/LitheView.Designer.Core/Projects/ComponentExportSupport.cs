// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.IO;

namespace LitheView.Designer.Core.Projects;

internal static class ComponentExportSupport
{
    internal static byte[] ProductConfiguration(string symbol, SurfaceInputRequirements requirements)
    {
        static string Boolean(bool value) => value ? "true" : "false";
        string text = $$"""
            // Portable UI aliases; the consuming application owns behavior and devices.
            #pragma once
            #include "display.hpp"
            #include "form.hpp"
            #include "resources.hpp"
            #include "lithe/surface_layout.hpp"
            #include <array>

            namespace lithe::simulator
            {
            using Configuration = lithe::generated::DisplayConfiguration;
            using Form = lithe::generated::{{symbol}};
            using SelectedResources = lithe::generated::SelectedResources;
            inline constexpr lithe::SurfaceInputRequirements kInputRequirements{
                {{Boolean(requirements.Activation)}}, {{Boolean(requirements.Navigation)}}, {{Boolean(requirements.Text)}}};
            inline constexpr std::array<lithe::SurfacePlacementPolicy, 0U> kPlacement{};
            } // namespace lithe::simulator
            """;
        return Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }

    internal static void GuardOutputLocation(DesignerWorkspace workspace, string directory)
    {
        GeneratedFileAccess.RejectReparsePath(directory);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace.ProjectDirectory));
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (root.Equals(directory, comparison) || root.StartsWith(directory + Path.DirectorySeparatorChar, comparison))
        {
            throw new ArgumentException("The output directory must not replace the source project or its parents.");
        }
        foreach (string protectedPath in new[] { "forms", "profiles", "resources" })
        {
            string path = Path.Combine(root, protectedPath);
            if (directory.Equals(path, comparison) || directory.StartsWith(path + Path.DirectorySeparatorChar, comparison))
            {
                throw new ArgumentException("Choose a separate output directory, outside source forms, profiles and resources.");
            }
        }
    }

}
