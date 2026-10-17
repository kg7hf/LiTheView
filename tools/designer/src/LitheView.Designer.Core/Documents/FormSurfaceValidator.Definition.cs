// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public static partial class FormSurfaceValidator
{
    public static FormSurfaceValidationResult Validate(
        ResolvedFormDocument document,
        DisplayDefinitionProfile definition,
        LogicalInputConfigurationProfile input,
        FormSurfacePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(input);

        List<ProfileDiagnostic> diagnostics = [];
        if (document.Source.SurfaceProfileId != definition.Id)
        {
            AddError(diagnostics, "LVFS1001", "$.surfaceProfileId",
                "The form surface lock does not match the selected display definition.");
        }
        FormRectangle root = document.Components.Length == 0
            ? default : Frame(document.Components[0]);
        if (root != new FormRectangle(0, 0, definition.Geometry.Width, definition.Geometry.Height))
        {
            AddError(diagnostics, "LVFS1002", "$.components[0].frame",
                "The root frame must exactly cover the selected display's logical surface.");
        }
        policy ??= new();
        Dictionary<string, SurfacePlacementPolicy> overrides = new(StringComparer.Ordinal);
        HashSet<string> componentIds = document.Components.Select(item => item.Source.ComponentId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FormComponentPlacementPolicy component in policy.Components)
        {
            if (!componentIds.Contains(component.ComponentId) ||
                !overrides.TryAdd(component.ComponentId, component.Placement))
            {
                AddError(diagnostics, "LVSL3007", "$.surfacePolicy.components",
                    "Placement overrides must identify distinct components in this form.");
            }
        }
        SurfaceLayoutNode[] nodes = new SurfaceLayoutNode[document.Components.Length];
        for (int index = 0; index < document.Components.Length; ++index)
        {
            ResolvedFormComponent component = document.Components[index];
            SurfacePlacementPolicy placement = overrides.TryGetValue(component.Source.ComponentId,
                out SurfacePlacementPolicy specified) ? specified : DefaultPlacement(component, index);
            long contentHeight = component.Properties.Select(item => item.Value)
                .OfType<FormSignedIntegerValue>()
                .FirstOrDefault(item => item.PropertyId == "content-height")?.Value ?? 0;
            if (contentHeight is < int.MinValue or > int.MaxValue)
            {
                AddError(diagnostics, "LVSL3003", $"$.components[{index}].contentHeight",
                    "Content height must fit the logical coordinate range.");
                return new() { Diagnostics = diagnostics };
            }
            nodes[index] = new(component.ParentIndex, Frame(component), placement,
                component.Widget.RuntimeKind is "scroll" or "table", checked((int)contentHeight));
        }
        ProfileDiagnostic? layoutError = ValidateLayout(definition, nodes);
        if (layoutError is not null)
        {
            diagnostics.Add(layoutError);
        }
        ProfileDiagnostic? inputError = ValidateInputRequirements(input, policy.InputRequirements);
        if (inputError is not null)
        {
            diagnostics.Add(inputError);
        }
        return new() { Diagnostics = diagnostics };
    }

    private static FormRectangle Frame(ResolvedFormComponent component) =>
        component.Properties.Select(item => item.Value).OfType<FormRectangleValue>()
            .FirstOrDefault(item => item.PropertyId == "frame")?.Value ?? default;

    private static SurfacePlacementPolicy DefaultPlacement(ResolvedFormComponent component, int index)
    {
        if (index == 0)
        {
            return SurfacePlacementPolicy.ClipToViewport;
        }
        bool explicitFocus = component.Properties.Select(item => item.Value)
            .OfType<FormTextValue>().Any(item => item.PropertyId == "focus-policy" && item.Value == "true");
        return explicitFocus || component.Widget.RuntimeKind is
            "button" or "checkbox" or "radio" or "textField" or "picker" or "softkey" or "tabBar" or "tabView" or
            "wheelPicker" or "dropDown" or "scroll" or "table"
            ? SurfacePlacementPolicy.SafeArea : SurfacePlacementPolicy.ClipToViewport;
    }
}
