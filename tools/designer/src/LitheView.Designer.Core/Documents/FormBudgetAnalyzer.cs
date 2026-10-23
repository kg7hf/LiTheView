// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Profiles;

namespace LitheView.Designer.Core.Documents;

public sealed record FormBudgetRequest
{
    public required TargetProfile Target { get; init; }

    public required long RequiredRenderWorkspaceBytes { get; init; }

    // Caller-supplied compiled archive text measurement, never C++ source size.
    public long? GeneratedArchiveBytes { get; init; }

    public long? GeneratedSourceBytes { get; init; }

    // Application controller stack usage cannot be derived from a form's view tree.
    public int? RequiredControllerDepth { get; init; }
}

public sealed record FormBudgetUse
{
    public required string Name { get; init; }

    public required long? Used { get; init; }

    public required long? Maximum { get; init; }

    public required string Units { get; init; }

    public required BudgetEnforcement? Enforcement { get; init; }

    public bool IsWithinLimit => Used.HasValue && (!Maximum.HasValue || Used.Value <= Maximum.Value);
}

public sealed record FormBudgetReport
{
    public required IReadOnlyList<FormBudgetUse> Uses { get; init; }

    public required IReadOnlyList<ProfileDiagnostic> Diagnostics { get; init; }

    public bool IsValid => Diagnostics.All(item => item.Severity != ProfileDiagnosticSeverity.Error);

    public bool HasUnknownUses => Uses.Any(item => !item.Used.HasValue);
}

public static class FormBudgetAnalyzer
{
    public static FormBudgetReport Analyze(
        ResolvedFormDocument document,
        FormBudgetRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Target);

        List<ProfileDiagnostic> diagnostics = [];
        List<FormBudgetUse> uses = [];
        TargetProfile target = request.Target;
        if (request.RequiredRenderWorkspaceBytes < 0 || request.GeneratedArchiveBytes < 0 ||
            request.GeneratedSourceBytes < 0 || request.RequiredControllerDepth < 0)
        {
            AddError(diagnostics, "LVFB1001", "$", "Budget inputs may not be negative.");
            return new FormBudgetReport { Uses = uses, Diagnostics = diagnostics };
        }
        int maximumChildren = MaximumChildren(document.Components);
        int maximumDepth = MaximumDepth(document.Components);
        int focusable = document.Components.Count(IsPotentiallyFocusable);
        AddCapacity(
            uses,
            diagnostics,
            "children-per-view",
            maximumChildren,
            target.Capacities.MaxChildrenPerView,
            "LVFB1101");
        AddCapacity(
            uses,
            diagnostics,
            "view-depth",
            maximumDepth,
            null,
            "LVFB1102");
        AddCapacity(
            uses,
            diagnostics,
            "controller-depth",
            request.RequiredControllerDepth,
            target.Capacities.MaxControllerDepth,
            "LVFB1102");
        if (!request.RequiredControllerDepth.HasValue)
        {
            AddWarning(diagnostics, "LVFB1302", "$",
                "Controller stack usage is unknown; view nesting is informational and does not measure it.");
        }
        AddCapacity(
            uses,
            diagnostics,
            "focusable-views",
            focusable,
            target.Capacities.MaxFocusableViews,
            "LVFB1103");

        long mutableTextBytes = 0;
        bool allBindingsKnown = true;
        foreach (FormBinding binding in document.Bindings.Values)
        {
            if (!binding.CapacityBytes.HasValue)
            {
                AddWarning(
                    diagnostics,
                    "LVFB1301",
                    $"$.bindings.{binding.Id}",
                    "Application-provided binding capacity is unknown; the mutable-text total is unevaluated.");
                allBindingsKnown = false;
                continue;
            }
            mutableTextBytes = CheckedAdd(mutableTextBytes, binding.CapacityBytes.Value, diagnostics);
        }

        AddBytes(uses, diagnostics, "generated-archive", request.GeneratedArchiveBytes, target.Budgets.ArchiveText);
        if (!request.GeneratedArchiveBytes.HasValue)
        {
            AddWarning(diagnostics, "LVFB1303", "$",
                "Compiled archive text is unmeasured; generated C++ source bytes cannot satisfy the archive gate.");
        }
        if (request.GeneratedSourceBytes.HasValue)
        {
            AddBytes(uses, diagnostics, "generated-source", request.GeneratedSourceBytes, null);
        }
        AddBytes(uses, diagnostics, "mutable-text", allBindingsKnown ? mutableTextBytes : null, null);
        if (!allBindingsKnown)
        {
            AddBytes(uses, diagnostics, "mutable-text-known-minimum", mutableTextBytes, null);
        }
        AddBytes(uses, diagnostics, "internal-ram", null, target.Budgets.InternalRam);
        AddWarning(diagnostics, "LVFB1304", "$",
            "Mutable-text capacity is one storage component, not complete target RAM usage. " +
            "Complete RAM remains unmeasured by this form analysis.");
        BudgetLimit? workspaceLimit = target.Runtime.RenderingMode == RenderingMode.Tiled
            ? target.Budgets.TileWorkspace
            : target.Budgets.Framebuffer;
        AddBytes(uses, diagnostics, "render-workspace", request.RequiredRenderWorkspaceBytes, workspaceLimit);
        return new FormBudgetReport { Uses = uses, Diagnostics = diagnostics };
    }

    private static int MaximumChildren(ResolvedFormComponent[] components)
    {
        return components
            .GroupBy(item => item.ParentIndex)
            .Where(item => item.Key.HasValue)
            .Select(item => item.Count())
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int MaximumDepth(ResolvedFormComponent[] components)
    {
        int maximum = 0;
        foreach (ResolvedFormComponent component in components)
        {
            int depth = 1;
            int? parent = component.ParentIndex;
            while (parent.HasValue)
            {
                ++depth;
                parent = components[parent.Value].ParentIndex;
            }
            maximum = Math.Max(maximum, depth);
        }
        return maximum;
    }

    private static bool IsPotentiallyFocusable(ResolvedFormComponent component)
    {
        FormTextValue? policy = component.Properties
            .Select(item => item.Value)
            .OfType<FormTextValue>()
            .SingleOrDefault(item => item.PropertyId == "focus-policy");
        if (policy?.Value == "false")
        {
            return false;
        }
        if (policy?.Value == "true")
        {
            return true;
        }
        // Match the portable widget constructor defaults. Elapsed-time or
        // pointer handlers alone do not make a widget keyboard-focusable.
        return component.Widget.RuntimeKind is
            "button" or "checkbox" or "radio" or "textField" or "picker" or "softkey" or "tabBar" or
            "tabView" or "wheelPicker" or "dropDown" or "list";
    }

    private static long CheckedAdd(
        long left,
        long right,
        List<ProfileDiagnostic> diagnostics)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            AddError(diagnostics, "LVFB1901", "$", "Budget arithmetic overflowed a signed 64-bit value.");
            return long.MaxValue;
        }
    }

    private static void AddCapacity(
        List<FormBudgetUse> uses,
        List<ProfileDiagnostic> diagnostics,
        string name,
        int? used,
        int? maximum,
        string diagnosticCode)
    {
        uses.Add(new FormBudgetUse
        {
            Name = name,
            Used = used,
            Maximum = maximum,
            Units = "count",
            Enforcement = maximum.HasValue ? BudgetEnforcement.CompileTimeAssertion : null
        });
        if (used > maximum)
        {
            AddError(diagnostics, diagnosticCode, "$", $"{name} uses {used} of {maximum} available entries.");
        }
    }

    private static void AddBytes(
        List<FormBudgetUse> uses,
        List<ProfileDiagnostic> diagnostics,
        string name,
        long? used,
        BudgetLimit? limit)
    {
        uses.Add(new FormBudgetUse
        {
            Name = name,
            Used = used,
            Maximum = limit?.MaximumBytes,
            Units = "bytes",
            Enforcement = limit?.Enforcement
        });
        if (limit is not null && used > limit.MaximumBytes)
        {
            AddError(
                diagnostics,
                "LVFB1401",
                "$",
                $"{name} uses {used} bytes and exceeds the {limit.MaximumBytes}-byte target limit.");
        }
    }

    private static void AddError(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Error, path, message));
    }

    private static void AddWarning(
        List<ProfileDiagnostic> diagnostics,
        string code,
        string path,
        string message)
    {
        diagnostics.Add(new ProfileDiagnostic(code, ProfileDiagnosticSeverity.Warning, path, message));
    }
}
