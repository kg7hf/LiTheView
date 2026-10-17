# Menu designer review validation

This records the 2026-10-17 correction pass in the public development checkout.
Changes remain local. No commit, push, private-repository sync or hardware action was made.

## Findings and checks

| Finding | Correction | Evidence |
| --- | --- | --- |
| P1: Add screen could overwrite a concurrent manifest edit | Recheck document version, dirty state and text immediately before submitting the workspace edit | Regression tests inject both dirty edits and edits saved while the disk read is pending |
| P1: menu handlers rejected Generate UI | Restore form ownership markers and resolve actual component IDs in the playback helper | Actual service binding plans pass for all five H750 forms; installed plug-in regenerates all round forms after each handler lesson |
| P1: cast gate failed on menu sources | Separate reviewed application cast budgets; exclude dependency/build trees; retain range/ABI proofs | Core 13, preview 69, simulator 1, original starter 9, completed starter 9, H750 menu 11, round menu 15 |
| P2: Foo/FooAssets generated names collided | Put scoped asset declarations under `lithe::generated::assets::<Form>` and prefix generated CMake aliases | Compiled consumer includes both forms |
| P2: mixed flat/scoped resources linked twice | Canonical CMake source path per content hash for both layouts | Consumer links two forms using the same native bitmap payload |
| P2: menu forms used the wrong resource provider | Form-specific providers owned alongside persistent forms | Consumer rejects an empty provider and accepts each selected provider; both applications build and run tests |
| P2: BOM manifests rejected Add screen | Normalize the UTF-8 BOM along with line endings | BOM regression test |
| P2: missing first form hid other screens | Isolate errors per form and include the active manifest before bounded discovery | Missing-form and ignored-folder regressions |

The broader pass also corrected stale handler documentation, added explicit application
static-analysis coverage, completed ownership special members, and added required braces.
The H750 completed application no longer has lesson feature switches. The round application
selects concrete page types at configure time and uses `if constexpr`; it has no internal
`MENU_HAS_*` macros. Hardware/SDK macros remain at their platform boundaries.

## Blank application through the installed plug-in

The retained successful run is:
`build/vscode-designer/integration-20261017T200517008/application with spaces/`.
Its `vscode-validation.json` records 24 successful checkpoints. The test starts with no
UI manifest, forms or generated output. SDK/middleware dependencies are already initialized,
matching the tutorial's prerequisites; this is not an Internet dependency-download test.

The installed 0.2.14 source VSIX performs the custom-circle wizard, normal custom-editor
edits, Save, Add screen, native preview and Generate UI. The walkthrough covers:

1. Blank application and both existing platform presets.
2. Custom 390 x 390 RGB888 circular display and full square canvas.
3. Home controls, Settings and Storage; application-owned handlers and FSM.
4. Volume controls and joystick behavior.
5. Play/Pause and Stop overrides, including explicit keyboard focus order.
6. Load's eight-entry selectable/scrolled list and final five-screen regeneration.

Windows and STM32 configure/build at every stage. The final Windows tests pass 7/7.
The designer starts no application tasks; the test driver explicitly runs application
CMake. The installed general extension acceptance run also passes at
`build/vscode-designer/integration-20261017T195258291/`.

The fresh walkthrough found and corrected two additional problems: the playback helper
used PowerShell's reserved HOME variable, and the lesson omitted arranging Stop after Play
in component/focus order. Failed runs are retained, rather than relabelled as successful.
This is automated extension-host/custom-editor validation, not a manual mouse walkthrough.

## Gates

| Gate | Result |
| --- | --- |
| Designer solution | 0 warnings, 0 errors |
| Designer Core | 100/100 |
| Service | 492 checks |
| Windows App | 18/18 |
| VS Code unit tests | 88/88 |
| Core CTest | 18/18 |
| Generated multi-form consumer | build/link/run passed |
| H750 Windows application | 7/7 |
| Round Windows application from blank tutorial | 7/7 |
| H750 STM32 cross-build | passed; 80,496 bytes in FLASH |
| Round STM32 final cross-build | passed; 87,092 bytes in FLASH |
| Critical Core static analysis | passed, 43 translation units |
| Application static analysis | passed, 11 translation units per Windows application |
| Protected original starter sources | 331 files checked, zero changes |

Detailed logs are under `build/adversarial-review-20261017/`; the tutorial retains separate
configure/build/CTest logs per stage. Application analysis retains its exact source list
and tool logs under each build's `application-analysis/`. Clang's suppressed system-header
warning totals are not first-party diagnostics; the critical first-party profile is fatal.

Reproduce the generated consumer after running Designer.Core.Tests:

```powershell
$generated = Get-Content build/multi-form-tests/latest-consumer.txt -Raw
cmake -S tests/multi_form_consumer -B build/multi-form-consumer -G Ninja "-DGENERATED_ROOT=$generated"
cmake --build build/multi-form-consumer
ctest --test-dir build/multi-form-consumer --output-on-failure
```

## Boundaries

The FSM and navigation events belong to the application, and Core has no STM32 or FreeRTOS
dependency. The menu examples are an extensible application pattern, not a universal menu
engine. UI mutation remains serialized on its owner; an ISR/backend must deliver bounded
application events. An end-to-end ISR notification/wakeup example is still future work.

The host/editor review pass did not access hardware. Cross-builds do not renew earlier hardware
acceptance. The original H750 starter pair and unrelated article drafts are preserved.
Menu submodule pins and the user's normal installed extension were not changed. Updating
pins to a validated library commit and mirroring/publishing are later steps; nothing here
points examples at an unpublished remote commit.

## Review-fix hardware flash check (2026-10-17)

Both menu images were programmed, verified and reset using explicitly selected probes.
Previous internal flash contents and the exact new ELFs are retained locally under
`build/menu-validation/review-flash-20261017-165615/`. No original starter sources were changed.

- H750 probe `003000483137510C33333639`, device `0x450`; ELF SHA-256 `4C91199D69BB960A3D1AEF5749E8BF8FDED53E77C1BFF7E285598004D1BAE38C`.
  The first run reached ready=1 and frames=1, then stopped at heartbeat=3792 with
  failureLine=25 in Application/app_main.cpp. Debugger capture confirmed ui::update()
  failed and hi2c4.ErrorCode=0x20 (HAL_I2C_ERROR_TIMEOUT), state READY. The input adapter
  currently propagates this read failure to the task's fatal assertion. After retaining
  that evidence and resetting, ready=1, heartbeat=767, frames=13, failureLine=0.
  This recovery does not resolve the timeout or fatal runtime-input policy.
- L4R9 probe `0676FF504955857567024748`, device `0x470`; ELF SHA-256 `38F43A93FC4E807D509D669DEB3A5091FB40CEF00DA6A0AD2748E188E9D7BF09`.
  Image came from the completed blank-project plug-in tutorial in
  `build/vscode-designer/integration-20261017T200517008/application with spaces`.
  Heartbeats advanced 1854 -> 1911 -> 7634, ready=1, frames=1, failureLine=0.
  BSP errors initially 0 subsequently reached 4749. This confirms the previously
  documented hardware error counter remains active; this run did not identify its
  individual error causes. Continued task execution is not clean touch-bus acceptance.

Programming/readback verification passed on both. Hardware health acceptance remains open.
No new visual/touch walkthrough has been claimed. No commit or push was performed.

## Publication decision (2026-10-17)

The owner authorized publishing the validated designer and menu/tutorial changes to both
repositories, with the touch/bus issues documented for later investigation. This is not
acceptance of production input reliability. Only menu dependency pins advance; the
original H750 starter pair and unrelated local work remain excluded.

## Menu dependency publication check (2026-10-17)

Both menu examples pin the public library/designer commit
`fc21b83d07219f47ec8ac71f4af141aa3435dc3e`. The original starter pair keeps its existing pins.
With that dependency, H750 Windows CTest passes 7/7 and STM32 cross-build passes;
the blank L4R9 Windows test passes 1/1 and its STM32 cross-build passes.

A fresh installed source VSIX 0.2.14 tutorial run passes all 24 checkpoints: blank project,
custom round display, panel, navigation, Volume, playback and Load; both platform builds
at each stage and final Windows tests 7/7. Evidence:
`build/vscode-designer/integration-20261017T212529256/application with spaces/vscode-validation.json`.
The first publication rerun selected the system .NET 9 installation and was rejected;
rerunning with the verified user-local .NET 10 executable passed. An outer shell check
subsequently reported a stale native exit code despite the runner's successful result;
the retained runner output and validation JSON confirm completion. These execution logs
are under `build/publication-menu-20261017/`. All 331 protected starter files still match.
Hardware was not accessed again; the documented touch limitations remain deferred.
