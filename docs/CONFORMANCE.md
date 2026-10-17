# Conformance, regression, and budget gates

This document defines the automated baseline established by modernization task 1 and extended by the
pixel-format work in task 2, font-pipeline work in task 3, and bounded scene/input work in task 4. These
gates preserve extracted RGB565 behavior while checking generalized rendering, both
compile-time-selectable font backends, shaped viewports, gestures, transitions, native destination
packing, and platform-independent input.

## Drop-down control checkpoint (2026-10-17)

The drop-down increment passes clean full conformance and the critical static-analysis gate. Final
Debug, Release and Windows MinGW CTest each pass 18/18. The compiled drop-down consumer exercises
typed generated handlers, tentative selection, cancellation, synchronous reentrancy, disabled
ancestors, popup layering/hit testing, scrolling and incremental repaint equivalence.

The managed solution builds with zero warnings/errors. Core passes 99/99, Service 507 checks,
Windows App 18/18, extension units 74/74 and packaged VS Code 0.2.11 acceptance 24/24. Real native
worker tests cover drop-down full/tiled rendering, cancellation and pointer/key commit equivalence
in all seven preview formats. Installed/source asset-free consumers, all ten installed rendering
strategy consumers, four Cortex-M33 configurations and the Cortex-M7 cross-build pass. Clang-Tidy
covers 43 translation units; the finalized controller and test were also checked individually.
This is host/editor and cross-build evidence; no board or starter application was changed.

With Arm GCC 14.3.1, a fresh `c06b623` baseline measures 79,057 bytes of RGB565 aggregate archive
text. The drop-down and root popup composition increase it to 84,578 bytes (+5,521). The round
aggregate changes from 59,803 to 65,250 (+5,447). An explicit 5,600-byte feature allowance makes the
compact-font ceiling 85,200 bytes, generated-font ceiling 115,200 before animation allowance, and
round ceiling 68,600. Packed-only Core ceilings remain 68,000. These are archive regression ceilings,
not application target budgets or linked firmware measurements. The new control has its own object
file, so applications that do not use it need not link that object's code.

Arm object sizes are `DropDownView` 108 bytes (112-byte gate), `View` 80 and `StaticLayout<16>` 1,884.
The existing 1,900-byte layout ceiling remains fixed. Aggregate data/BSS remain 16/8 bytes and the
largest reported stack frame remains 840 bytes. Baseline source/build evidence is under
`build/dropdown-baseline-c06b623*`; current metrics are under `build/conformance/m33`; size-probe
assembly is `build/dropdown-size.s`. Validation logs are `build/dropdown-*` and packaged acceptance
is under `build/vscode-designer/integration-20261017T120204385`.

## Follow-up cleanup validation (2026-10-16)

The follow-up pass under `build/cleanup-pass2` passes Debug and Release CTest (15/15 each), installed
and asset-free consumers, all five Arm cross-builds, and the newly integrated ten installed rendering
strategy consumers. Generic row decoding is checked at 1, 7, 31 and 256 storage blocks with exact-byte
oracles and rejected-span guards. Fixed RGB565 standard/round Win32 builds pass; XRGB8888 standard/round
builds pass in conformance. An old RGB888 inspection setting fails with its XRGB8888 replacement.

Warnings-as-errors, Cppcheck (51 files) and critical Clang-Tidy (39 translation units) pass. The managed
solution builds without warnings/errors; Core passes 96/96, Service 494 checks, App 18/18 and target
feasibility 21/21. Target provenance and schema paths are now checked alongside canonical identities.
The U545 catalog ceilings match the live 78,000/63,000-byte gates; full-frame planning profiles and
companion fixtures explicitly declare synthetic limits. No physical board was accessed.

The previous packaged-extension and generated-display-consumer results below were not repeated in
this pass. The normal installed extension and all local starter application files remain unchanged.

## Preceding cleanup checkpoint (2026-10-16)

The prepared publication cleanup passed fresh validation. Host CTest is now 15/15 in both Debug and
Release after retiring two controller-specific suites; generic packed-pixel, rendering, input and
control coverage remains. Installed-package and asset-free consumers pass, as do all five Arm
cross-build configurations below. This is host and cross-build evidence, not physical-board acceptance.

The three packed-only Cortex-M33 Core archives measure 52,231 bytes (`R2_PACKED2X2`), 52,241 bytes
(`RRRR2222`) and 52,311 bytes (`RYRY2222`) of text, with zero archive data/BSS and a largest reported
stack frame of 840 bytes. Each uses a 68,000-byte text ceiling and a 4,096-byte stack-frame ceiling.
These archive measurements do not establish final application memory use.

Static analysis passes its warnings-as-errors build, Cppcheck over 51 files and critical Clang-Tidy
over 39 translation units. The managed solution builds with zero warnings/errors; Core passes 96/96,
Service passes 494 checks and App passes 18/18. VS Code passes 73/73 unit tests and 24/24 packaged
workflow checks. Three native generated-component consumers pass; six display-definition exports
replay deterministically, both negative configuration checks pass, and target feasibility passes
21 cases.

Logs are under `build/publication-cleanup/`. The first packaged VS Code run encountered an `EBUSY`
logical-input file lock; the same package passed in a fresh run. Both results are retained, and no
file-lock fix is claimed. Display-definition checks passed under `build/cleanup-dd` after a deeply
nested consumer build exceeded a Windows path-length limit. Historical measurements below describe
their named checkpoints; the packed-only Core measurements above supersede retired fixture totals.

## One-command local gate

From PowerShell, with the Arm GNU Toolchain on `PATH`, in `STM32_TOOLCHAIN_ROOT`, or supplied explicitly:

```powershell
.\tools\run-conformance.ps1 `
  -ArmToolchainRoot 'REPLACE_WITH_ARM_GNU_TOOLCHAIN_ROOT'
```

The command configures fresh CMake caches under `build/conformance`, then runs:

1. Debug and Release host builds with first-party warnings as errors;
2. host behavioral, golden, XML mutation, timing, footprint, and cast-audit tests;
3. installed-package consumer configuration, build, and execution;
4. five installed single-format packages, each tested with full-frame and tiled strategy consumers;
5. four separate Cortex-M33 destination configurations with archive/workspace gates; and
6. the Cortex-M7 hard-float portable-core/demo cross-build.

The cross-build matrix is fixed at configuration time. Build directories are under `build/conformance`:

| Directory | CPU / floating-point ABI | Destination writers | Checked products |
| --- | --- | --- | --- |
| `m33` | Cortex-M33 / soft | RGB565 | Standard and round probes with separate archive/workspace gates |
| `m33-r2` | Cortex-M33 / soft | R2_PACKED2X2 | Core-only archive, data/BSS and stack-frame gates |
| `m33-red-cell` | Cortex-M33 / soft | RRRR2222 | Core-only archive, data/BSS and stack-frame gates |
| `m33-red-yellow` | Cortex-M33 / soft | RYRY2222 | Core-only archive, data/BSS and stack-frame gates |
| `m7` | Cortex-M7 / hard, fpv5-d16 | ALL | Generic portable Core and demo compilation; no U545 probe or board runtime |

`-SkipCrossBuilds` is available for a host-only check, but that is not the complete conformance gate.
The script never probes, resets, flashes, or executes on a physical board.

The control-event increment (2026-10-09) measures 77,580 bytes in the Cortex-M33
RGB565 aggregate archives, compared with a freshly compiled `11e9578` baseline
of 74,942 bytes: 2,638 bytes for typed dispatch, control hooks, radio ownership and
edit semantics. The previous 75,000-byte ceiling had only 58 bytes of headroom.
A documented 3,000-byte feature allowance makes the compact-font regression
ceiling 78,000 bytes (108,000 for the generated-font variant, before any existing
animation allowance). The same Core allowance makes the round ceiling 63,000,
red/emitter and red/cell ceilings 68,000, and dual-color ceiling 88,000 bytes.
The R2 aggregate changes from 64,248 to 66,886 bytes, the same 2,638-byte increase.
Archive totals include unlinked code; they do not establish
final firmware placement or overwrite application target budgets. Baseline evidence
is retained in `build/control-events-baseline/sizes.txt`.

The bounded text-event increment (2026-10-17) measures 79,057 bytes against a fresh
`a2d9056` RGB565 Cortex-M33 aggregate of 77,553 bytes with the same GCC 14.3.1
configuration: an additional 1,504 bytes. An explicit 1,600-byte text-event allowance
changes the compact-font aggregate ceiling to 79,600 bytes (109,600 for generated
fonts before animation allowance). Other archive gates retain their existing
limits. These are archive regression ceilings, not changes to application budgets.
The baseline is retained under `build/text-events-baseline-build`; current metrics
are under `build/conformance/m33`.

Arm size probes measure `TextFieldView` at 104 -> 116 bytes and `StaticLayout<16>`
at 1,820 -> 1,884 bytes. The existing 1,900-byte layout ceiling stays fixed; a new
116-byte text-field gate prevents further growth. `View` stays at 80 bytes. Explicit
caller-owned draft and rollback buffers are additional application storage. Probe
assembly is retained in `build/text-size-{before,after}.s`.

The Cortex-M RAM ceilings remain unchanged, but the event API has a per-object
cost. An identical Arm GNU 14.3.1 size probe against `11e9578` and this increment
measures the following bytes:

| Object | Before | After |
| --- | ---: | ---: |
| `View` | 80 | 80 |
| `ViewController` | 28 | 32 |
| `ButtonView` | 108 | 112 |
| `CheckboxView` | 92 | 96 |
| `RadioButtonView` | 92 | 100 |
| `PickerView` | 104 | 108 |
| `WheelPickerView` | 108 | 112 |

The interaction guard uses existing view flag padding; only event controls carry
the temporary sink pointer. Radio controls also borrow their application-owned
group. Evidence is `build/control-events-ram-{before,after}.s`.
`StaticLayout<16>`, archive data/BSS and stack limits retain their existing gates.
Both conformance and static-analysis builds now use `--clean-first`
after fresh configuration: `--fresh` alone can leave old object files after removing
their header dependency information.

Run the safety-focused static-analysis gate separately:

```powershell
.\tools\run-static-analysis.ps1
```

The static-analysis command requires `cmake`, `cppcheck`, `clang-tidy`, and `git` on `PATH`; `git`
lists the tracked and untracked, non-ignored sources passed to Clang-Tidy. The script checks all four
prerequisites before configuring a build and reports the missing command.

That command performs a warnings-as-errors build, exhaustive Cppcheck warning/portability analysis,
and the risk-focused Clang-Tidy profile in `tools/clang-tidy-critical.yaml`. H750 adapter sources are
included in the I/O-independent Cppcheck pass; generated BSP and vendor sources are excluded.

Every modernization item also inventories the complete first-party `.clang-tidy` profile and the
project style guide. This broader inventory is a phase-to-phase ratchet rather than an all-or-nothing
merge gate: the item must correct a reviewed subset of lingering findings and document what remains.
The critical profile, warnings-as-errors builds, formatting, tests, and resource ceilings remain hard
zero-regression gates.

## Separate target-feasibility gate

Build the headless CLI with the SDK pinned under `tools/designer`, then run
`tools/check-target-feasibility.ps1 -CliAssembly <path-to-LitheView.Designer.Cli.dll>`.
The 2026-09-26 M5 checkpoint passes 21 cases in both Debug and Release, including constrained/sufficient
targets, exact native row costs, malformed/stale evidence, and preservation of an existing surface
export. A successful CLI exit reports no checked failure; target status remains unevaluated until all
required evidence is available. Caller-supplied evidence is distinct from the native gates above.

The full managed solutions build with zero warnings/errors; Core passes 69/69 and Windows UI/goldens
11/11 in each configuration. Six fresh installed consumers retain exact native replay hashes and pass
critical Clang-Tidy/Cppcheck across 12 translation units. See the M5 section of
[the implementation record](DISPLAY_MODEL_IMPLEMENTATION.md) for retained paths and measurement limits.

## Designer document/compiler parity

The Item 5 headless form compiler has a checked canonical XML and generated C++ smoke fixture under
`tests/designer-fixtures/document-compiler-smoke`. Its generated C++ is compiled as an independent archive in
host, U545/M33, and H750/M7 builds. Host tests enable the generated source XML, parse it through the
real `XmlLayoutParser`, compare all structural `ViewSpec` fields with the generated array, construct
both fixed-capacity layouts, and require byte-identical RGB565 frames. This proves equivalence for the
checked fixture; it is not a claim that arbitrary extension code has been executed or validated.

The managed contract suite also requires every built-in widget property to survive typed model,
canonical XML, and C++ generation; canonical XML to reach a fixed point; generated artifacts to be
fresh; surface locks to fail closed; logical resource metadata to round-trip; and developer
save-zone bodies to survive regeneration byte-for-byte. Strict import prohibits DTDs/external entities,
rejects unknown metadata, and enforces the 1 MiB document bound. Compatibility import is explicit and
reports every synthesized or discarded historical field.

The generated form archive is intentionally separate from each runtime/demo archive. It demonstrates
the compiler without consuming a product's flash unless the product elects to link that form.

## Designer resource identity and deployment separation

The Phase 5.6 resource contract has three independent checks. Manifest validation verifies the typed
contract and policy; artifact verification re-reads the source, generated payload, and license text
and requires their declared byte counts and SHA-256 values; project resolution selects exactly one
variant compatible with the selected display surface's painter format. Resolution rejects missing or
ambiguous display-compatible variants.

The resource and project manifests do not select a board, target, bus, RTOS, linker section, alignment,
internal flash, external QSPI, or host-file placement. The consuming application owns those deployment
choices. Optional target-profile and linker gates remain the authority for U545/H750 capacity after
export; verified payload size alone must not be upgraded to linker or hardware evidence.

The manifest and every selected resource variant contribute to the resolved project SHA-256. The
managed suite therefore requires equivalent inputs to resolve identically and verifies that a changed
source/output/license hash, converter option, painter-format mapping, ownership, or lifetime cannot
silently retain the prior project identity.

## Designer native-preview isolation and parity

The Item 5 native worker is a host-only executable linked to the real LitheView parser, bounded
layout, application/controller path, and full-frame/tiled renderers. Its versioned C ABI uses only
caller-owned storage. Its length-prefixed control records are bounded, and framebuffer pixels use a
designer-created namespace-checked shared-memory mapping. The worker receives no project paths and has
no network behavior.

The checked `preview render` CLI and VS Code task write a raw host frame atomically. This is renderer
and isolation evidence only; it does not build, flash, probe, or execute an embedded board. External
font/bitmap payload transport is intentionally deferred to the resource-studio phase, and missing
payloads fail closed in this baseline.

## Rendering goldens

The common 480 by 272 demo has checked RGB565 and XRGB8888 frame signatures for both font backends:

| Font backend | Scene | RGB565 CRC-32 | XRGB8888 CRC-32 |
| --- | --- | ---: | ---: |
| Compact 5 by 7 | Initial menu | `0xf88f6224` | `0x94b033a1` |
| Compact 5 by 7 | Menu after one focus-down action | `0x807cef56` | `0x25b32d3b` |
| Generated Sans | Initial menu | `0x8baac9e5` | `0x7955ec5c` |
| Generated Sans | Menu after one focus-down action | `0xb4159684` | `0x56343042` |

The CRC is computed in row-major memory order: low then high byte for RGB565, and blue, green, red,
`0xff` for XRGB8888. Tests require each configured backend and full-frame format to match both logical
scenes. The compact font is binary coverage, so every XRGB8888 pixel must quantize to its corresponding
RGB565 reference pixel. Generated Sans uses anti-aliased coverage; its RGB565 and XRGB8888 results have
format-specific goldens because blending and quantization do not commute. The four-row M33 RGB565 path
matches both compact-font RGB565 goldens. Arbitrary 73 by 11 tile tests compare RGB565 and XRGB8888
tiles with their full-frame buffers, including partial right and bottom tiles.

A golden may change only with a reviewed rendering-contract change. Record the visual reason and update
both the expected signature and relevant reference evidence; never refresh a golden merely to make a
failure disappear.

The 240 by 240 round example has RGB565 signatures for both configured font choices:

| Font backend | Scene | RGB565 CRC-32 |
| --- | --- | ---: |
| Compact 5 by 7 | Dashboard | `0x7596e85c` |
| Compact 5 by 7 | Gesture lab | `0x16d5a161` |
| Generated Sans | Dashboard | `0x786b049a` |
| Generated Sans | Gesture lab | `0xd1469b14` |

The stable scenes show a `MORE: BUILD INFO` cue. Branch, commit, variant, version, and build time remain
visible in the Build Info overlay but are deliberately excluded from these golden scenes so a branch or
dirty-state change cannot invalidate a rendering regression signature.

Tests require the complete circular full-frame output and arbitrary 73 by 11 tiles to be byte-for-byte
equal, including cleared corner tiles outside the shaped viewport. Halfway scene transitions must also
match between the full-frame and tiled paths. A transparent-overlay regression renders an intermediate
translated frame before completion, then requires the previous panel/control position to be restored
from the underlying opaque scene. The gesture-lab regression separately taps and drags its marker,
requires each recognized result to change the visible image, and compares the final arbitrary-tile
render byte-for-byte with the full framebuffer.

## XML robustness corpus

The host suite retains explicit round-trip, entity, strict-attribute, malformed-element, numeric-bound,
writer-capacity, animation-arena, and caller-storage-capacity tests. It also mutates every byte of a
known-valid layout with NUL, `<`, `"`, and `&`, parses every case twice, and requires deterministic,
bounded results. This deterministic corpus complements future coverage-guided fuzzing and runs without
network access or an unbounded allocation arena.

## Enforced resource budgets

The U545 Cortex-M33 cross-build generates GCC `.su` stack-usage records and
`litheview_embedded_metrics.json` reports. The current conservative per-profile archive ceilings are:

| Metric | Ceiling |
| --- | ---: |
| Standard XML/MVC profile aggregate text (compact font, alpha1 animation) | 85,200 bytes |
| Standard XML/MVC profile aggregate text (generated fonts, alpha1 animation) | 115,200 bytes |
| Standard XML/MVC indexed4 animation adjustment | +21,000 bytes |
| Round specialized profile aggregate text | 68,600 bytes |
| Aggregate initialized data | 256 bytes |
| Aggregate archive BSS | 256 bytes |
| Largest statically bounded function stack frame | 4,096 bytes |

The Core and displayless rendering probes are separate archives. A product links only its selected
scene, destination writer, and resource configuration. The 2026-10-17 compact-profile measurements are:

| M33 profile | Destination | Aggregate archive text |
| --- | --- | ---: |
| Standard | RGB565 | 84,578 bytes |
| Round | RGB565 | 65,250 bytes |

Both report 16 bytes initialized data, 8 bytes archive BSS, and an 840-byte maximum bounded function
frame. These Arm GNU 14.3.1 measurements supersede the older checkpoints for current size comparisons;
the implementation and audit records retain that history. The corresponding target catalogue profiles
and canonical identity locks match these archive ceilings. Application budgets remain independent.

The build fails if a ceiling is exceeded or GCC reports an unbounded dynamic stack frame. The standard
profile ceiling first increased from 60,000 to 66,000 after the scene/gesture core measured 65,097
bytes, then to 75,000 after the 96 by 72 alpha1 showcase resource made the profile measure 73,132
bytes. Selecting indexed4 adds a separate 21,000-byte allowance because its eight-frame payload is
20,736 bytes larger than alpha1; it does not relax the compact or generated-font alpha1 ceilings. The
specialized profiles retain separate ceilings. The control-event increment adds the same explicit
3,000-byte Core allowance to each profile; it does not combine them under one larger ceiling. The archive
text total is a conservative pre-link regression metric, not a claim
about the garbage-collected flash size of a particular firmware image. Likewise, the largest function
frame is not a whole-task call-tree stack proof. Exact linked flash/RAM, complete worst-case stack,
execution time, transfer time, cache, and DMA behavior remain target-integration measurements.

These archive ceilings intentionally apply to the resource-constrained U545 acceptance profiles, not
to the H750 examples. H750 RGB565 and XRGB8888 images are bounded by the board linker's real FLASH and
RAM regions. Higher-fidelity H750 demo assets may use the board's external QSPI flash when the internal
flash image no longer has suitable margin; that placement remains an H750 integration decision.
The fresh M4 standalone links use RGB565 FLASH 79,704 / RAM_D1 36,032 bytes and XRGB8888 FLASH 120,112 /
RAM_D1 36,192 bytes. Logs are `build/display-model-m4-h750-{rgb565,xrgb8888}-build.log`. These are
cross-build measurements only; the M4 images have not been programmed or executed on the board.

## Continuous integration

`.github/workflows/conformance.yml` defines one workflow with:

- Debug and Release host builds and package-consumer tests on Windows and Linux;
- offline Cortex-M33 and Cortex-M7 Arm GNU cross-builds with JSON budget metrics printed in the log;
  and
- compiler, Cppcheck, and critical Clang-Tidy gates.

The workflow is manual-only: pushes and pull requests do not trigger it. Its dispatch form selects
`host`, `arm`, `static-analysis`, or `all`, with `host` as the conservative default. Run only the scope
needed for the evidence being collected. Do not schedule it routinely, and do not assume a local runner
is free: the same matrix can contend with the other projects on a shared development PC.

All first-party host and cross-build warnings are treated as errors. GCC and Clang builds include
`-Wdouble-promotion`. Vendor/generated code is excluded from first-party analysis rather than rewritten
or broadly suppressed.

## STM32H750 application boundary

The generic Cortex-M7 gate is compilation evidence. It does not replace building and testing
the consuming H750 application, and a cross-build is not a physical-device result.

The board is shared. Hardware actions require the owner's current-session authorization and a
resolved probe identity. Automated library conformance commands do not access hardware. Record
build, programming verification and observed runtime behavior as separate evidence.
