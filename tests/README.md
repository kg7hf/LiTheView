# Library verification

This directory is the library's automated verification suite. It is not a collection of application
templates. Use the [starter examples](../examples/README.md) to learn the development workflow.

| Tests | Purpose |
| --- | --- |
| `control_objects_test.cpp`, `control_events_test.cpp` | Unit tests for typed controls, ownership, callbacks, focus, selection and cancellation |
| `text_events_test.cpp`, `text_setpoint_test.cpp` | Bounded UTF-8 edits, validation, rollback, generated handlers and actual applied-value/label behavior; no new application starter |
| `drop_down_test.cpp` | Popup rendering/hit testing, bounded item scrolling, tentative selection and commit/cancel, XML round trips and compiled generated backend/label handlers |
| Storage, R2, native-cell and surface tests | Exact byte encoding, bounds, stride, clipping, layout, damage and input validation |
| `test_main.cpp`, `rendering/` | Rendering regression fixtures and comparisons of full-frame and tiled output |
| `designer-fixtures/` and generated-form tests | Designer documents, generated C++ and handwritten handler integration |
| `package_consumer/`, `product_consumer/`, `strategy_consumer/` | Compile/link/run a consuming application, including installed and asset-free packages |
| `display_definition_consumer/`, `native_component_consumer/` | Generated display definitions and components compiled against the selected native format |

Additional managed designer tests live under `tools/designer/tests`; extension unit and packaged
integration tests live under `tools/vscode`. Those exercise editing, saving, configuration, generation,
native preview and handler preservation. A passing host test does not establish physical board behavior.

`tools/test-package-notices.ps1` checks the real VSIX packager in an isolated synthetic checkout:
license bytes, upstream provenance links and attribution survive Windows bundle packaging, including
`-ForRedistribution`. Absent/changed notices and mismatched runtime DLLs reject packaging before an
existing output can be replaced.
Source-only publication works without prepared tools and rejects compiled files. It needs
PowerShell 7.2 or later and does not build or execute the designer or access hardware.

`tools/test-vscode-designer.ps1 -PackagedExtensionPath <source.vsix> -SourceOnlyPackage` exercises
the installed source-only extension with locally prepared tools and no service/worker path overrides.
Use an isolated VS Code installation/profile; the harness writes only temporary application fixtures.

The synthetic packed-display fixtures use invented geometry to exercise reusable storage contracts.
They contain no controller commands, device register maps or physical sensor drivers. Logical encoder
and button events are device-independent inputs; the wheel picker is an on-screen control.

Run the host suite after configuring and building the repository:

```powershell
cmake --preset windows-mingw-debug
cmake --build --preset windows-mingw-debug
ctest --preset windows-mingw-debug
```

`tools/run-conformance.ps1` additionally checks Debug/Release, installed packages, optional fonts,
asset-free Core, all ten installed full-frame/tiled strategy consumers and embedded cross-builds. `tools/run-static-analysis.ps1` checks warning and analysis
policies. Neither script flashes hardware.

`tools/check-render-strategies.ps1` is also available separately when matching single-format Core
packages are already installed. Supply `-Rgb565Prefix`, `-Xrgb8888Prefix`, `-R2Prefix`,
`-Rrrr2222Prefix` and `-Ryry2222Prefix`. It checks native byte oracles, equal full-frame/tiled output,
workspace costs and (when `nm` is available) unused-strategy symbol exclusion. The conformance runner
builds those packages and invokes the script automatically.
