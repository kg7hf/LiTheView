// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Projects;

public sealed record DesignerTutorialStarterResult
{
    public FormDocument? Form { get; init; }
    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }
}

public static class DesignerTutorialStarter
{
    public static DesignerTutorialStarterResult CreateForm(FormDocument emptyForm,
        DisplayDefinitionProfile definition, LogicalInputConfigurationProfile input)
    {
        ArgumentNullException.ThrowIfNull(emptyForm);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(input);
        List<ProfileDiagnostic> diagnostics = [.. DisplayDefinitionValidator.Validate(definition),
            .. LogicalInputConfigurationLoader.Validate(input)];
        ProfileDiagnostic? requirements = FormSurfaceValidator.ValidateInputRequirements(input,
            new() { Activation = true, Navigation = true });
        if (requirements is not null)
        {
            diagnostics.Add(requirements);
        }
        SafeAreaProfile safe = definition.Geometry.SafeArea;
        if (safe.Width < 96 || safe.Height < 94 || emptyForm.Components.Length != 1)
        {
            diagnostics.Add(new("LVDP1101", ProfileDiagnosticSeverity.Error, "$.tutorialStarter",
                "The two-control tutorial needs one root and a safe area at least 96 by 94 logical units; create an empty form for smaller displays."));
        }
        if (diagnostics.Any(item => item.Severity == ProfileDiagnosticSeverity.Error))
        {
            return new() { Diagnostics = diagnostics };
        }
        int width = Math.Min(200, safe.Width - 16);
        int x = safe.X + (safe.Width - width) / 2;
        int y = safe.Y + (safe.Height - 78) / 2;
        string parent = emptyForm.Components[0].ComponentId;
        return new()
        {
            Form = emptyForm with
            {
                Components =
                [
                    emptyForm.Components[0],
                    Component("status", "statusLabel", "lithe.widget.label", 100U, "READY",
                        parent, 0, new(x, y, width, 18)),
                    Component("activate", "activateButton", "lithe.widget.button", 1U, "ACTIVATE",
                        parent, 1, new(x, y + 26, width, 22)),
                    Component("reset", "resetButton", "lithe.widget.button", 2U, "RESET",
                        parent, 2, new(x, y + 56, width, 22))
                ],
                Actions =
                [
                    new() { Id = "action.activate", RuntimeId = 1, Symbol = "Activate" },
                    new() { Id = "action.reset", RuntimeId = 2, Symbol = "Reset" }
                ]
            },
            Diagnostics = diagnostics
        };
    }

    // Written only while creating a new project. Export preserves this developer
    // owned hook; the simulator has no implicit widget-name action behavior.
    public static byte[] ApplicationSource => Encoding.UTF8.GetBytes("""
        #pragma once
        #include "runtime_types.hpp"

        namespace lithe::simulator
        {
        class ApplicationHooks final
        {
        public:
            void onReady(Views& views) noexcept
            {
                if (auto* status = views.find<lithe::LabelView>(100U))
                {
                    status->setText("READY");
                }
            }
            void onAction(Views& views, lithe::ViewId action) noexcept
            {
                if (action != 1U && action != 2U)
                {
                    return;
                }
                if (auto* status = views.find<lithe::LabelView>(100U))
                {
                    status->setText(action == 1U ? "ACTIVE" : "READY");
                }
            }
            void onBack([[maybe_unused]] Views& views) noexcept
            {
            }
        };
        } // namespace lithe::simulator

        """);

    private static FormComponent Component(string id, string name, string widget, ulong runtimeId,
        string text, string parent, int order, FormRectangle frame) => new()
    {
        ComponentId = "component.main." + id,
        Name = name,
        WidgetId = widget,
        ParentComponentId = parent,
        SiblingOrder = order,
        Properties =
        [
            new FormRectangleValue("frame", frame),
            new FormUnsignedIntegerValue("runtime-id", runtimeId),
            new FormTextValue("text", text),
            // Legacy form values are 0xTTBBGGRR, where T is inverse alpha.
            new FormColorValue("background-color", widget == "lithe.widget.button" ? 0x00483828U : 0xff000000U),
            new FormColorValue("foreground-color", 0x00ffffffU),
            new FormColorValue("accent-color", 0x00dca050U)
        ],
        HistoricalAttributes = []
    };
}
