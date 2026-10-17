# Resource-manifest smoke fixture

This fixture demonstrates the hardware-independent Phase 5.6A resource contract. It is intentionally
small and inspectable rather than a production image converter:

1. `assets/status-icon.svg` is the immutable source asset.
2. `resources/status-icon.resource.json` locks the source identity, provenance, CC0 license evidence,
   deterministic converter contract, generated output, compatible painter formats, ownership, and
   lifetime.
3. `generated/status-icon-alpha1.bin` is the verified generated payload.
4. `project.litheview.json` selects a display surface and declares the resource manifest. Resolution
   chooses exactly one variant compatible with that surface's painter format.

From the repository root:

```powershell
$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"

& $Dotnet run --project tools\designer\src\LitheView.Designer.Cli -c Release --no-build -- `
  resource validate `
    tests\designer-fixtures\resource-manifest-smoke\resources\status-icon.resource.json

& $Dotnet run --project tools\designer\src\LitheView.Designer.Cli -c Release --no-build -- `
  resource verify `
    tests\designer-fixtures\resource-manifest-smoke\resources\status-icon.resource.json `
    tests\designer-fixtures\resource-manifest-smoke

& $Dotnet run --project tools\designer\src\LitheView.Designer.Cli -c Release --no-build -- `
  project resolve `
    tests\designer-fixtures\resource-manifest-smoke\project.litheview.json `
    profiles\display-surfaces
```

The resource manifest does not decide whether a consuming application links the bytes, stores them in
internal flash, places them in external QSPI, loads them from a host file, or obtains them through a
custom provider. Those are downstream application/build decisions. The logical ownership and lifetime
fields tell a consumer how the resource reaches the LitheView `ResourceProvider` boundary without
embedding board or linker policy in the UI project.

For a real imported resource, retain the original source, record its actual provenance and license,
run a versioned deterministic converter, and update the output hash and byte count from the generated
file. Do not hand-edit a hash to make verification pass.
