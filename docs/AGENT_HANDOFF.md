# LiTheView agent handoff

## Clean public history

This checkout belongs to the new public source history. Older checkpoint hashes
below refer to private development history and must not be fetched as dependencies.
Use the current `.gitmodules`, build presets and publication record. The owner's
retained local exercise work is outside this public snapshot.

## Current direction

Work in the current LiTheView checkout; resolve its root with `git rev-parse --show-toplevel`.
The maintained developer path is the H750 starter pair under
`examples/`. One application uses the same platform interfaces in Windows and STM32 builds; the
designer supplements VS Code with form editing, Configure, Generate UI and typed handler hooks.
Application CMake owns builds and the application's launchers own debugging.

Read [DISPLAY_MODEL_PLAN.md](DISPLAY_MODEL_PLAN.md), [CONTROL_OBJECTS.md](CONTROL_OBJECTS.md),
[SHARED_APPLICATION_PLAN.md](SHARED_APPLICATION_PLAN.md) and
[LIBRARY_BOUNDARY_CLEANUP.md](LIBRARY_BOUNDARY_CLEANUP.md) before changing these contracts.

## Starter publication pass (2026-10-17)

The owner authorized inclusion of the previously held-local button work. The guided
starter remains blank in Git, with its complete platform/lesson support included.
Its existing `Tasks/UI/` remains on disk and is now ignored as developer exercise
output. The completed sibling includes the generated button/counter UI and photo
hit-test configuration. No history reset, public visibility change or hardware
operation was performed.

- Both examples pin LiTheView `5e83237afb75ed4bbb8c532ed650e694efbba4d8`. Vendor pins are unchanged.
- CMake Tools, terminal builds and the two existing F5 launchers use common local
  tool discovery. Absolute installation paths and junctions stay in ignored `build/`.
  Explicit overrides fail if invalid; x64 GCC is rejected for the i686 RTOS port.
  Probe serials no longer default to the author's board. The unused Cube Clang
  toolchain file was removed; the supported presets use GCC.
- First-party application/tool files carry Paul R. Decker / GPL-3.0-only notices.
  Each example has a third-party inventory link. ST source headers remain intact;
  this pass does not invent a permissive grant for the unresolved CubeMX templates.
- The isolated progression passes blank Windows/STM32 builds, then generated UI
  builds using the same presets without explicit reconfiguration. Windows CTest is
  2/2 before and after. ON/OFF interaction, photo push-button counter, debounce,
  hold/release/cancel and coordinate transforms pass. Partial UI output, overlapping
  hit regions, an unmapped button and changed photo hash are rejected.
- The progression also builds using a compiler path containing spaces and rejects
  a missing explicit compiler. Separate checks reject an x64 compiler and an
  invalid explicit Arm debugger. Local junction paths resolve GDB, ST-LINK server
  and CubeProgrammer without hardcoded version strings.
- The actual completed starter separately passes fresh Windows configure/build and
  CTest 2/2, both interaction modes, and a fresh STM32 cross-build. Flash: 61,192 B;
  DTCM: 14,576 B; framebuffer SDRAM: 522,240 B. Windows GDB stops at WinMain,
  continues the RTOS self-test and exits normally; Arm GDB reads the ELF's main
  symbol. This is debugger-path evidence, not a new VS Code UI or physical-board test.

Evidence: `build/starter-progression/ed227388d36e40e2a8ad95e9655f42ba/` and
`build/starter-publication-20261017/`. Test-only vendor junctions reused initialized
checkouts at the pinned commits; this was not an anonymous fresh GitHub clone.
The initial local recovery bundle/ZIP remains under
`.local-retained/publication-preflight-20261017T122141Z/`.

Before a public cutover, resolve or replace the remaining CubeMX template grant
for linked firmware distribution, replace the self-submodule pins with a reviewed
bootstrap commit in the new history, and test anonymous initialization from that
history. The private remote and its old history remain unchanged by this pass.

## Public-release preparation audit (2026-10-17)

The owner asked for a clean/good checkpoint before resetting history and publishing. No history,
visibility, branch, PR or release mutation was performed. See [PUBLIC_RELEASE_READINESS.md](PUBLIC_RELEASE_READINESS.md)
for the cutover prerequisites and the distinction between the validated package and held-local starters.

A clean committed-source export passes Release CTest 18/18 and installed consumers 4/4 without
vendor submodules or untracked files; the managed solution builds with zero warnings/errors, Core
tests pass 99/99 against the export-built worker, extension units pass 74/74, and the source-only
VSIX packages. Gitleaks 8.30.1 reports no findings in the export or 126 locally reachable commits.
All 13 upstream notice hashes match; package attribution positive/negative tests pass.
Private evidence is under `build/public-release-audit`. The remote remains private, with 14 non-main
branches, 13 closed PRs and 16 historical workflow runs. No tags, releases, open PRs/standalone issues
or retained Actions artifacts were found. Those old PR references are not removed by resetting main.

Recommended cutover: a new repository with clean history, preserving the old repository privately.
This needs an explicit destination decision before remote changes. Starter self-submodule pins must
be bootstrapped into the new history; the committed physical-button lesson/source mismatch and fixed
tool paths still require a separately scoped application pass. No local starter changes were staged.

The audit fixes the manual workflow's nonexistent Cortex-M7 U545 metric read, clarifies README control
coverage and starter limitations, and removes remaining prose names for a retired product overlay.
No GitHub workflow was dispatched. A verified all-local-refs bundle and byte-verified ZIP of 174
held-local files are in the timestamped ignored `.local-retained/publication-preflight-*` backup;
use its `manifest.json` and `bundle-verify.txt` to identify the completed backup. The earlier partial
backup has no manifest; its patch step encountered vendor-submodule sandbox permissions before the
successful rerun excluded submodule traversal. Refresh backups at the actual cutover.

## Drop-down control and designer handlers (2026-10-17)

The owner requested the drop-down control and its designer handlers, deferred examples, then asked
for a good pause point. This is that completed control increment. Both local H750 starter trees and
`tools/test-h750-starter-progression.ps1` remain excluded from publication. The 173-file protected
starter baseline is unchanged. No hardware was accessed and no Git history reset was performed.

`DropDownView` is an extensible `ControlView` with immutable caller-owned, pipe-delimited items
(1â€“255 entries), index identity and typed selection/edit notifications. Selection is tentative while
open; commit accepts it, cancellation restores the initial index, and application refresh is silent.
It shares the synchronous, guarded control-hook-before-form-handler contract. Pointer/touch and
keyboard input use the same model. A bounded five-row popup opens above or below its anchor; pointer
drag scrolls without committing. Root overlay painting/hit testing preserves sibling order and native
surface clipping. Invalid direct models remain inert rather than truncating identities.

The designer palette, properties, native preview, XML round trip, generated named accessors and all
three handlers are connected in version 0.2.11. Generated drop-down forms require control-event
contract version 3. Existing text and other forms retain their previous minimum versions. Handler
bodies survive save/reopen/regeneration. See [CONTROL_OBJECTS.md](CONTROL_OBJECTS.md) for usage and
`tests/drop_down_test.cpp` for a compiled generated-form consumer that applies a selection to a
backend and label. No new application starter was created.

Validation:

- Clean full conformance passed: Debug/Release, installed and asset-free consumers, ten installed
  format/strategy consumers, four Cortex-M33 configurations and Cortex-M7. Final Debug, Release and
  Windows MinGW CTest each pass 18/18 after the last controller guard/test change.
- Managed solution: zero warnings/errors; Core 99/99, Service 507 checks, Windows App 18/18.
  The final Core run uses the finalized MinGW worker and checks full/tiled popup output, cancellation,
  and equivalent pointer/key commits for all seven native preview formats.
- Extension unit tests: 74/74. Final packaged VSIX: 24/24 workflow checks, including drop-down
  insertion and all three typed handlers. Evidence:
  `build/vscode-designer/integration-20261017T120204385/application with spaces/vscode-validation.json`.
  The normal user extension/profile was not changed. The package is
  `build/vscode-designer/litheview-designer-0.2.11.vsix`.
- Critical Cppcheck/Clang-Tidy passed (43 translation units); the final controller and regression
  files also passed a targeted Clang-Tidy run. Formatting and whitespace checks pass.
- Separate target-feasibility checks pass 21/21 under `build/dropdown-target-feasibility`.
- Fresh `c06b623` Arm baseline: RGB565 aggregate text 79,057 -> 84,578 bytes (+5,521); round
  aggregate 59,803 -> 65,250 (+5,447). CMake records a 5,600-byte archive allowance; its two regression
  target profiles and canonical identity locks are synchronized. Application budgets stay unchanged.
  `DropDownView` is 108 bytes on Arm; `View` remains 80 and
  `StaticLayout<16>` remains 1,884. Existing layout/data/BSS/stack ceilings still pass.

Logs are `build/dropdown-*`; final gate logs include `dropdown-conformance-final.log`,
`dropdown-static-final.log`, `dropdown-static-final-files.log`, `dropdown-core-final.log`,
`dropdown-{host-debug,host-release}-final-tests.log`, `dropdown-ctest-final.log` and
`dropdown-vscode-integration-final.log`. Retain the earlier failed logs as development evidence:
an initial static-analysis pass raced an edited header, and a later regression fixture directly
cleared focus without restoring the controller-owned flag. Final frozen/targeted runs pass.

Pause here. Table data/selection contracts remain the next unimplemented control work; focused
control starters are deferred until requested. Hardware and end-user visual acceptance are separate
from the host, packaged-editor and cross-build evidence above.

## Bounded text control events (2026-10-17)

The owner requested text-field events and setpoint behavior, then explicitly deferred creating a new
application example. This increment implements the controls, designer discovery and regression
consumers only. The tentative `examples/text-field-starter` scaffolding and its submodule registration
were removed. Both H750 starter trees and the progression script remain excluded from the commit;
all 173 files in the expanded baseline are unchanged. No history reset or hardware access occurred.

`TextFieldView` now derives from `ControlView` and permits specialization. `TextChange` and `TextEdit`
carry borrowed synchronous payloads; control hooks precede generated form handlers. Explicit caller
rollback storage enters through the final constructor span or `ResourceProvider::textEditBuffer`.
An empty span retains immediate editing. Select validates before commit, Back/focus loss restores
then cancels, and silent `setText` replaces an active draft. Re-entrant mutation cannot invalidate
borrowed callback text. See [CONTROL_OBJECTS.md](CONTROL_OBJECTS.md) for exact ownership and limits.

Designer 0.2.10 exposes all three text events. Palette insertion creates an explicit 32-byte binding;
duplicating a text field creates a distinct binding. These declarations do not allocate application
storage. Undo/redo and regeneration preserve the component/binding transaction and handwritten
handler bodies. New text forms require control-event contract version 2; existing non-text forms
retain version 1 requirements. The document-compiler fixture is regenerated.

Validation for this increment:

- Clean conformance: Debug and Release each 17/17; installed/source asset-free consumers; all ten
  installed format/strategy consumers; four Cortex-M33 configurations and Cortex-M7 cross-build.
- Managed solution: zero warnings/errors; Core 98/98, Service 499 checks, Windows App 18/18.
- Extension unit tests: 74/74. Packaged 0.2.10 acceptance: 24/24 workflow checks, including
  text-field palette insertion and all three typed handlers through save/reopen/regeneration.
  Evidence: `build/vscode-designer/integration-20261017T044933072/application with spaces/vscode-validation.json`.
  The user's installed extension/profile was not changed; the new VSIX is under `build/vscode-designer`.
- Full critical Cppcheck/Clang-Tidy gate passed. Final changed implementation/tests were checked
  again after compacting the edit state. Sandbox restrictions required the existing PowerShell asset
  tests and Arm tools to run outside the sandbox; those runs passed.
- `a2d9056` was freshly cross-built for the flash baseline. RGB565 aggregate archives increase by
  1,504 bytes (77,553 -> 79,057); the explicit 1,600-byte allowance is recorded in CMake and conformance
  docs. Text fields are 116 bytes on Arm; the existing 1,900-byte layout RAM ceiling still passes.

Logs are `build/text-{conformance,static-analysis,static-final-files,core-tests,service-tests,app-tests,
  extension-tests,vscode-integration}.log`; size probes are `build/text-size-{before,after}.s`.
The setpoint regression is `tests/text_setpoint_test.cpp`; there is no physical keyboard/touch
acceptance claim. Create a focused control starter later when requested. Drop-down/table data and
selection contracts remain the next unimplemented control work.

## Publication cleanup (2026-10-16)

The preceding library/designer publication was merged into main. The owner then requested removal
of obsolete applications and device-specific material from the published package and its history,
while retaining local source. Source and history backups are under the ignored `.local-retained/`
directory; inspect the local cleanup records before any history operation. The owner chose to prepare
the cleanup and reset history later, then authorized committing and pushing the library/designer
cleanup and licensing changes. This publication is an ordinary continuation of main from
`bef6fca9129a81fc3014cb2b620a15603494115e`, with no history reset, branch deletion or starter
submodule repinning. Verify the current local and remote heads before further publication.

Preserve the 60 held-local starter/progression files in
`build/publication-scope/local-files-before.json`. They are not part of the publication scope.
Do not bulk-stage, reset or replace those application files. The expanded record in
`build/license-packaging/starter-before.json` verifies all 173 protected starter files; older null
entries in the 60-path record mean there was no baseline file, not a file hash. Both starter trees
and `tools/test-h750-starter-progression.ps1` are excluded from this publication.

The follow-up audit also removed the unused fixed-geometry Win32 native adapter, its dormant build
branches and the unreferenced simulation wrapper. Packed-row checks use synthetic widths of 1, 7,
31 and 256 storage blocks. Shared generated-file checks live in `IO/GeneratedFileAccess`; legacy
simulator generation remains a tested CLI maintenance facility, including safe migration support.
Target profiles and companion budget fixtures now cite current sources or explicitly synthetic
limits. Their canonical locks were refreshed. The Win32 fixture option uses `XRGB8888` rather than
the ambiguous old `RGB888` value. All 173 starter files captured before this pass are unchanged.

The root launchers now cover library checks and designer preparation; open a starter folder for its
Win32/STM32 presets and launchers. Synthetic packed-display fixtures replace hardware-specific
rendering and controller assumptions. See [tests/README.md](../tests/README.md).

## Tool and path portability audit

The follow-up audit removes assumed per-user .NET paths from root tasks/launchers and
machine-specific checkout/archive paths from shared docs. Root maintenance commands now use
`dotnet` on PATH; managed debug launchers name the built DLL; `-Dotnet` remains available to scripts. This machine's system PATH selected SDK
9.0.304, so validation selected the already installed 10.0.400 host in the test process only.
No global environment or editor settings were changed. The build environment guide records the
SDK pin, tool ownership and the remaining protected-starter MSYS2/debugger path issues.

Core built in a relocated source tree with spaces and no example/vendor submodules. The source
consumer passed 3/3; after moving both source and installed prefix, the installed consumer also
passed 3/3. Exported CMake files contain no original checkout/prefix paths. The checked Core
compile commands contain no FreeRTOS, STM32 HAL or CMSIS dependency. Root managed task execution
passed with zero build warnings/errors and valid CLI output; extension unit tests passed 73/73.

The animation wrapper discovers Windows PowerShell through PATH rather than assuming it resides
beside PowerShell 7. Empty optional palette arguments are omitted across the native command boundary.
RGB565 and Indexed4 conversion passed under both shells with identical generated C++ per format.
Evidence and preservation hashes are under `build/cleanup-paths`. No hardware, history, installed
extension or protected starter files were changed. Interactive F5 was not exercised in this audit.

## Current library/designer behavior

The owner selected GPL-3.0-only for LiTheView-owned material and the copyright name Paul R. Decker.
He confirmed ownership of the original code and 2012 compact glyphs. See `LICENSE`, `NOTICE.md`,
`THIRD_PARTY_NOTICES.md` and `licenses/README.md`. The horse fixture records the project license;
its pixels and retained generation provenance are unchanged. Third-party terms remain separate.
Upstream DejaVu and MinGW/GCC notices now have local verbatim copies and a source/hash catalog;
CMake installs the notices and VSIX packaging verifies notice and DLL hashes before writing output.
The owner explicitly chose upstream compiler attribution and links, without mirroring or rebuilding
WinLibs/GCC/MinGW source. The README and packaged notices link the release and source repositories.
The prior unconditional binary redistribution block has been removed; notice/DLL identity checks
remain. `-ForRedistribution` records intent, not a legal certification. The runtime manifest preserves
upstream provenance limits without a source-approval Boolean. Do not restore a compiler-source hosting
or rebuild requirement. Including DLLs remains distinct from merely using an installed compiler.

`tools/package-vscode-designer.ps1 -SourceOnly` remains an optional platform-neutral VSIX with no
runtime binaries. It uses the existing locally prepared service/worker fallback; the application
CMake/debug workflow is unchanged. Source packaging needs no prepared tools/compiler. No VSIX was
uploaded to a marketplace or GitHub release.

The file-level Cube review is in `licenses/stm32-examples.md`, with full upstream dependency and
Cube package notices in `licenses/`. It distinguishes verified CMSIS/HAL/MIT/BSD portions from
unresolved CubeMX scaffolding and newer Picolibc system-call template grants. Those vendor files
are excluded from the root GPL default. Do not claim starter firmware redistribution is cleared;
the owner's local application files were deliberately not changed.

Typed controls use synchronous bounded callbacks, with no hidden queue, allocation or opaque context.
Toggle and checkbox changes, application-owned radio groups, picker selection and wheel commit/cancel
are implemented. Custom controls override typed hooks; generated forms expose named handlers.
One shared event catalog drives generated C++, service inspection and VS Code handler creation.
Handwritten bodies survive save/reopen/regeneration. Text entry, dropdown, table and named tab events
remain future work. New typed forms must diagnose an older Core dependency at compile time.

Display compatibility remains separate from target feasibility. Native preview renders native bytes
before final host decode. Asset work should encode prepared content into native storage without adding
a general image editor or runtime framebuffer conversion. Generated-font support and optional Sans
resources exist; the starters explicitly use the basic compact font.

## Per-file attribution (2026-10-17)

First-party library, designer, tests, scripts and build sources now carry SPDX copyright/license
headers naming Paul R. Decker and GPL-3.0-only. Extracted library files use 2012-2026; new tooling
uses 2026. The widget catalog compiler emits the same notice into its C++ and C# metadata.
The style guide records this convention and the generated-output ownership boundary. Do not stamp
Paul's ownership onto application-specific forms or imported assets. DejaVu tables, generated form
and resource fixtures, and all protected local starter work were left unchanged.

Validation for this attribution pass is retained under `build/copyright-notices/`: a byte audit
confirmed 343 notice-only source/build edits, apart from the explicit widget-header emission and
compact-font notice normalization. All 120 excluded tracked files and 173 protected starter hashes
are unchanged. The designer solution builds with zero warnings/errors; Core passes 96/96,
including generated-artifact freshness, extension tests pass 73/73 and the clean native Debug build
passes CTest 15/15. The first restricted Core run hit sandbox file/PowerShell permissions; the same
built tests passed outside that sandbox. No board or installed extension was accessed.

## Validation

Licensing/package follow-up evidence is in `build/license-packaging/`:

- Isolated packager checks pass for verbatim notices, required first-party license, runtime hashes,
  missing/changed notices, bundled packaging with redistribution intent, upstream provenance and
  preservation of existing outputs.
- Source-only packaging passes without prepared tools and rejects compiled files in source folders.
- Installed source-only VSIX passes all 24 real editor workflows using local tools without service
  or worker overrides (`build/vscode-designer/integration-20261017T034325830`). No application tasks ran.
- VS Code unit tests pass 73/73; installed Core consumer 3/3 and generated-font consumer 4/4.
- Designer Core was rebuilt with zero warnings/errors and passed 96/96 in the preceding license pass.
- All 173 protected starter hashes match. No hardware access or history rewrite was performed.

Fresh follow-up validation passed; evidence is under `build/cleanup-pass2/`:

- Conformance: Debug and Release 15/15, installed/asset-free consumers, all ten single-format
  full-frame/tiled consumers and all five Arm cross-build configurations with budget gates.
- Static analysis: warnings-as-errors, Cppcheck (51 files) and critical Clang-Tidy (39 translation units).
- Managed: zero build warnings/errors; Core 96/96, Service 494 checks and App 18/18.
- Fixed RGB565 Win32 standard/round builds; XRGB8888 builds in conformance; old RGB888 cache
  rejected with an explicit reconfiguration instruction; target feasibility 21/21.
- Catalog and companion target schemas/provenance paths resolve; all held-local starter hashes match.

The preceding cleanup evidence under `build/publication-cleanup/` additionally records:

- Conformance: 15/15 host tests in both Debug and Release, installed and asset-free package consumers,
  and all five Arm cross-build configurations with their resource gates.
- Static analysis: warnings-as-errors build, Cppcheck (51 files) and Clang-Tidy (39 translation units).
- Managed solution: zero build warnings/errors; Core 96/96, Service 494 checks and App 18/18.
- VS Code: 73/73 unit tests and 24/24 packaged workflow checks with no application task executions.
- Three native generated-component consumers, six deterministic display-definition exports/replays,
  both negative configuration checks and 21 target-feasibility cases.

The first packaged run hit an `EBUSY` file lock while saving logical input. A fresh run using the same
source and package passed all 24 checks; no file-lock fix is claimed. Both runs are retained under
`build/vscode-designer/integration-20261017T014823874` and
`build/vscode-designer/integration-20261017T015215362`. Display-definition consumer builds initially
exceeded a Windows path-length limit; the same checks passed in the shorter `build/cleanup-dd` path.
The normal installed extension was not updated. See [CONFORMANCE.md](CONFORMANCE.md) for gate scope.
Extension unit/packaged workflows and generated display consumers were not repeated in the follow-up
pass; their earlier results above remain separate from the fresh managed/conformance results.

Use a `dotnet` on PATH that satisfies `tools/designer/global.json`; scripts also accept `-Dotnet`.
See [BUILD_ENVIRONMENT.md](BUILD_ENVIRONMENT.md) for tool discovery and local overrides.
Windows native tools are MinGW; embedded cross-builds use the installed STM32
Arm toolchain. Fresh conformance and analysis runs use `--clean-first`. Do not edit source during
Clang-Tidy. Packaged VS Code tests can use the verified isolated editor under
`build/validation-tools/vscode-1.140.0`; do not interfere with the user's normal editor updater.

No hardware access is needed for this cleanup. Host checks, cross-builds and physical acceptance
must remain separately reported.
