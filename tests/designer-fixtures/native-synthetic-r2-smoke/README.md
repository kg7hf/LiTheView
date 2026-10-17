# Native synthetic packed display red smoke fixture

This is the checked version-3 native display project. Its manifest locks
`profiles/display-definition.json` (`r2Packed2x2`, 512 by 384 emitters, 2 by 2 one-byte storage
blocks) and `profiles/logical-input.json` (encoder and select). It has no RGB editing surface,
display-surface snapshot or MCU target. Compare it with `synthetic-red-staging-smoke`, the legacy
version-1 project, which keeps its RGB565 staging interpretation.

The files were produced from the repository root by:

```powershell
$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
  project create-native tests/designer-fixtures/native-synthetic-r2-smoke `
    litheview.project.native-synthetic-r2-smoke "Native synthetic packed display Red Smoke" `
    profiles/display-definitions/synthetic-r2.json profiles/logical-inputs/encoder-select.json `
    schema/widgets.json
```

`project resolve <project.json> <surface-catalog-directory>` reports the pinned
`resolved-project-sha256` checked by the designer Core tests. The desktop and VS Code designers open,
edit, natively preview and generate it like any other project.
