# Internal rendering fixtures

These fixtures retain useful checks from the retired showcase applications. They are library
maintenance tools, not the recommended application architecture. For application development,
start with [the H750 starter](../../examples/stm32h750b-dk-starter-1/README.md).

| Directory | Retained purpose |
| --- | --- |
| `common` | Deterministic scenes and resources for widget, damage, text, animation and frame checks |
| `rgb565`, `xrgb8888` | Independent native-format configurations and installed-package consumers |
| `win32` | Optional visual inspection of the same scenes on a host display |
| `stm32u545re-q` | Displayless strip rendering, transport seams and Cortex-M33 archive/stack probes |
| `display-model-generated` | Display compilation, native resource and separate target-budget fixtures |
| `display-bringup` | Handwritten display/input integration, failure-path and deterministic frame checks |
| `assets` | Source artwork and provenance for the retained regression payloads |

`LITHEVIEW_BUILD_RENDERING_FIXTURES` controls fixture libraries. With portable tests enabled,
`LITHEVIEW_BUILD_RENDERING_TESTS` adds scene/probe regressions. `LITHEVIEW_BUILD_WIN32` enables
optional visual inspection tools. These replace the old `LITHEVIEW_BUILD_EXAMPLES` and
`LITHEVIEW_BUILD_EXAMPLE_TESTS` options; they do not build or configure the starter applications.
A consuming application normally leaves all of these off.

Run the host/package and Arm checks from the repository root with `tools/run-conformance.ps1`.
Pass `-SkipCrossBuilds` for host-only checks, or `-ArmToolchainRoot` for the installed Arm tools.
`tools/run-display-bringup.ps1` exercises the standalone handwritten integration consumer.
Root VS Code launchers labelled **Regression** are maintenance tools. The example applications
own their own Windows/H750 F5 configurations.

No fixture here owns a FreeRTOS kernel, STM32 HAL or Cube package. The old H750 executable and
product-specific overlay have been removed. Existing golden frames and budget thresholds are retained;
new display and feature teaching belongs in starter lessons rather than in these old scenes.

Target fixtures are described in [profiles/targets](../../profiles/targets/README.md). The U545
probe profiles cite their current sources and gates. Synthetic full-frame fixtures exercise declared
limits without claiming board measurements. Use application-owned definitions and evidence for firmware.
