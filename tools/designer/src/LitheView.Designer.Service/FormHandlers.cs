// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service;

internal sealed partial class DesignerService
{
    // A read-only plan. VS Code owns the handwritten document edit and undo history.
    // Generation never publishes this file or rewrites a handler body.
    private object DescribeHandlers(Context context, int documentVersion)
    {
        DesignerWorkspace workspace = RequireForm(context);
        FormResolutionResult resolved = FormDocumentResolver.Resolve(workspace.Form, _catalog);
        if (!resolved.IsValid || resolved.Document is null)
        {
            throw new ServiceException("InvalidForm", Messages(resolved.Diagnostics));
        }
        ResolvedDesignerProjectPaths paths = DesignerProjectPathService.Load(context.ManifestPath, _libraryRoot);
        string handlerPath = DesignerProjectForms.HandlerPath(workspace, paths.ComponentDirectory);
        string directory = Path.GetDirectoryName(handlerPath)!;
        bool scoped = DesignerProjectForms.HasOwnDirectory(workspace, paths.ComponentDirectory);
        string include = Path.GetRelativePath(directory,
            Path.Combine(DesignerProjectForms.OutputDirectory(workspace, paths.ComponentDirectory), scoped ? "form.hpp" : "product_config.hpp")).Replace('\\', '/');
        if (include.Contains('"') || include.Contains('\n') || include.Contains('\r'))
        {
            throw new ServiceException("InvalidPath", "The generated include path cannot be represented in C++.");
        }
        return new
        {
            documentVersion,
            formId = workspace.Form.Id,
            handlerPath,
            className = scoped ? resolved.Document.CppSymbol + "Form" : "Form",
            baseType = scoped ? "lithe::generated::" + resolved.Document.CppSymbol : "lithe::simulator::Form",
            include,
            handlers = resolved.Document.Components.SelectMany(item =>
                ControlEventCatalog.For(item.Widget.RuntimeKind).Select(descriptor => new
                {
                    componentId = item.Source.ComponentId,
                    eventId = descriptor.Id,
                    parameterType = descriptor.ParameterType,
                    method = "on" + item.CppSymbol + descriptor.Suffix,
                    accessor = "get" + item.CppSymbol
                })).ToArray()
        };
    }
}
