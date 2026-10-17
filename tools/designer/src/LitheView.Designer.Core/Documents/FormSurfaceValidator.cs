// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public static partial class FormSurfaceValidator
{
    public static FormSurfaceValidationResult Validate(
        ResolvedFormDocument document,
        DisplaySurfaceProfile surface)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(surface);

        List<ProfileDiagnostic> diagnostics = [];
        if (document.Source.SurfaceProfileId != surface.Id)
        {
            AddError(
                diagnostics,
                "LVFS1001",
                "$.surfaceProfileId",
                "The form surface lock does not match the selected display surface.");
        }
        FormRectangleValue? rootFrame = document.Components[0].Properties
            .Select(item => item.Value)
            .OfType<FormRectangleValue>()
            .SingleOrDefault(item => item.PropertyId == "frame");
        if (rootFrame is null ||
            rootFrame.Value != new FormRectangle(0, 0, surface.PixelSize.Width, surface.PixelSize.Height))
        {
            AddError(
                diagnostics,
                "LVFS1002",
                "$.components[0].frame",
                "The root frame must exactly cover the selected display's logical pixel surface.");
        }
        return new FormSurfaceValidationResult { Diagnostics = diagnostics };
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
