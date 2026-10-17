# Document compiler smoke fixture

This fixture is a checked, canonical designer form used to verify the Item 5 document pipeline.
It deliberately includes hierarchy, theme resolution, action IDs, a mutable-text binding, and
generated integration regions. The form XML is the portable editable artifact; the files under
`generated/` are deterministic outputs and must stay fresh.

From the repository root, use the .NET 10 SDK selected by the VS Code designer tasks:

```powershell
$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form validate `
    tests/designer-fixtures/document-compiler-smoke/forms/main.litheview.xml `
    schema/widgets.json

& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form export-cpp `
    tests/designer-fixtures/document-compiler-smoke/forms/main.litheview.xml `
    schema/widgets.json `
    tests/designer-fixtures/document-compiler-smoke/generated/main_form.hpp `
    tests/designer-fixtures/document-compiler-smoke/generated/main_form.cpp
```

Only code between `LITHEVIEW USER CODE BEGIN/END` markers in the integration `.cpp` belongs to the
application developer. Regeneration preserves those bodies exactly. A malformed, duplicate,
nested, or orphaned region makes export fail and writes a `.candidate` file instead of replacing
the developer's integration file.
