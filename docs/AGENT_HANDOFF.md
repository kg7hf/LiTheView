# LiTheView agent handoff

## Clean public history

This checkout belongs to the new public source history. Older checkpoint hashes
below refer to private development history and must not be fetched as dependencies.
Use the current `.gitmodules`, build presets and
[publication validation record](PUBLICATION_VALIDATION.md). The owner's
retained local exercise work is outside this public snapshot.

## Tiny Gecko publication scope and glass guide (2026-10-17, America/New_York)

Publication includes the blank Tiny Gecko starter, completed reference, segmented
New Project/Configure workflow, and compiler-matched GDB/CMake Tools launch fixes.
Both examples include UI/design/glass.segment-display.md beside the strict JSON:
an annotated custom indicator, field/cell/stroke explanations, A03 mapping, and
134 descriptive segment names. Import JSON and generated encoder are unchanged.
The stripped teaching sample passes the real CLI glass validator.

Final publication checks: extension 97/97, blank Windows 3/3, completed Windows
4/4, matching FSM/platform sources, valid local documentation links. Earlier
solution/Core/Service and packaged workflow evidence remains under
build/tiny-gecko-split. User confirmed both F5 and Run/Debug stop at main then
run the simulator. No new hardware flash. Publication excludes existing STM32
starter work, root board photographs, private hardware documents and local
article drafts. Protected starter hashes are in build/tiny-publication.


## Tiny Gecko full F5 pre-launch validation (2026-10-17, America/New_York)

The user confirmed the designer preview but reported F5 still did not start.
The earlier direct-adapter test omitted preLaunchTask and therefore was not
end-to-end F5 evidence. VS Code logged spawn ctest ENOENT while its CMake Tools
build used an absolute discovered CMake executable. Both Tiny examples now use
CMake Tools tasks with explicit win32-debug/efm32-debug presets instead of bare
cmake/ctest process tasks. Existing launch labels and dependencies remain.

An isolated VS Code extension-host run called debug.startDebugging with the actual
Tiny Gecko: Windows configuration, including preLaunchTask. CMake was removed
from that process PATH; CMake Tools had an explicit test-profile tool path.
Configure and build exited zero, GDB stopped at main (line 508), and Continue
reached line 525 after window.show(). Only the owned debug session was stopped.
Evidence: build/tiny-gecko-split/f5-integration/result.json and test.cjs.
This verifies the VS Code launch API and tasks, not the user's physical F5 key.
User subsequently confirmed both F5 and the green Run and Debug triangle work
in the existing editor: each stopped at main, then Continue ran the Windows
simulator. This closes the reported Windows launch failure. No hardware access,
commits or pushes; existing STM32 starter work unchanged.


## Tiny Gecko debugger and visual Configure (2026-10-17, America/New_York)

Both Tiny example launchers now stop at main and use the compiler-matched GDB
through ignored build/tool-links/mingw/bin/gdb.exe. CMake creates a junction to
the entire MinGW installation; linking only bin breaks this WinLibs GDB's Python
lookup. The previous manual gdb.exe prompt did not resolve in the user's VS Code
environment. The installed C++ debug adapter was exercised with the actual launch
configuration: stopped in main, continued past window.show(), then terminated only
the test process. Evidence: build/tiny-gecko-split/vscode-gdb-validation.json and
vscode-gdb-trace.json. No firmware changes or board access.

Designer extension 0.2.18 is installed in the normal VS Code profile. Reload Window
is needed for an already-open editor. Segmented Configure now shares New Project's
glass geometry/field rendering helper and shows named logical input capabilities.
Import validates through project.segmentGlass before replacing the preview. Failed
imports preserve the old glass, and Cancel remains available during import.
No raw JSON editor is presented. The FSM stays application-owned.

97 Node tests pass, including canvas drawing, import failure recovery, Apply and
Cancel. Installed-package blank-starter integration passes with the automatic GDB
setup and both pre/post-generation builds. Logs: visual-configure-tests.log and
visual-configure-integration.log under build/tiny-gecko-split. A standalone browser
render of Configure was inspected there; it lacks VS Code's injected theme values
and is not a screenshot of the user's editor. Existing STM32 starters untouched.
No commits or pushes. User confirmation of F5 and the updated Configure is pending.


## Segmented Configure recovery (2026-10-17, America/New_York)

The user's normal VS Code profile still had extension 0.2.13. Its pixel-only
Configure opened a partial tab for the new segmented service response and threw
on missing displayDefinition.geometry. The newer extension had only been tested
in an isolated profile. Source extension 0.2.17 is now installed in the normal
profile; the existing VS Code window still needs Developer: Reload Window.

Pixel and segmented setup values are now checked before creating the tab. Added
host/webview tests for routing, initialized input values, Apply, failed Apply,
Cancel, tab disposal and malformed responses. All 96 Node tests pass. Installed
0.2.17 integration passes the actual blank Tiny starter workflow, including
Configure Cancel, tab close, reopen and Apply. Evidence:
`build/tiny-gecko-split/configure-node-tests.log`, `configure-integration.log`,
`configure-integration-evidence.json` and `configure-validation.json`.
Normal-profile installation is verified, but user confirmation after reload is
pending. No starter source, firmware or FSM was changed by this fix. No push.


## Tiny Gecko guided starter and completed reference (2026-10-18 UTC)

`examples/efm32tg-stk3300-menu-1` starts with no designer project or generated
headers. `examples/efm32tg-stk3300-menu-1-complete` contains the finished reference.
The old `efm32tg-stk3300-menu` source location is retired; its SDK remains ignored
locally. Each new example has its own pinned SDK submodule. The FSM, device
adapters and platform sources are identical across the pair. A build-time
header-presence check enables the generated presenter on the next ordinary build;
there are no separate blank/UI presets or launchers.

The starter README walks New Project, glass import, three field bindings,
Generate UI, Windows checks and hardware configuration. Visual pattern authoring
remains planned. Packaged integration now copies the actual blank starter, not a
synthetic no-op main. It passes from blank through generation with the unchanged
build preset and without a manual second configure. Windows CTest: starter 3/3,
complete 4/4; both EFM32 links pass. Completed firmware is byte-identical to the
previously validated binary. No flash in this increment. Designer build has zero
warnings; Core 101/101, Service 953, generated consumer 2/2 and cast audit pass.
Evidence: `build/tiny-gecko-split`. Existing STM32 starter work remains untouched.
Changes are local and uncommitted; nothing was pushed for this split.


## Segmented New Project in VS Code (2026-10-18 UTC)

Implemented locally in plug-in/service 0.2.16. New Project offers pixel or segmented
displays, imports bounded validated glass, shows fixed geometry/field capacities,
and creates a blank v4 form with an application-selected name, logical input and
paths sidecar. Core creation stages the sidecar with the manifest and validates
paths before writes. Existing directories are rejected. The application FSM,
presenter and hardware adapters are not generated or modified.

Tutorial: examples/efm32tg-stk3300-menu-1-complete/docs/segmented-designer.md now starts with
LiTheView: Add LiTheView UI, not CLI creation. docs/SEGMENTED_DESIGNER_PLAN.md records
the next visual pattern-authoring stage: reference art and reusable digit/ring/icon
templates expanded into the current glass contract, with controller mapping kept
in application adapters. That visual pattern editor is planned, not implemented.

Validation: zero-warning solution build; Core 101/101, Service 953, App 18/18,
Node 93/93. The installed source-only VSIX passes the blank external application
workflow, then the unchanged Windows preset builds the generated simulator and
passes all four tests. Native worker and embedded code were unchanged. No flash.
Evidence: build/segmented-new-project and build/vscode-designer/latest-integration.json.
A sandbox temp-rename failure was resolved by running tests with normal permissions.
Preparation published the service but its runtime-copy step hit a DLL held by an
existing process; no developer process was terminated. The new service and existing
worker passed packaged integration. Normal-profile extension installation unchanged.
No commits or pushes for this increment; existing STM32 starters/private references
remain untouched.

## Generated Tiny Gecko runtime integration (2026-10-18 UTC)

The presenter now calls UI/Generated/segmented/home.hpp on both Windows and EFM32.
Application/menu.hpp is unchanged. The handwritten encoder is test-only in
Tests/reference_encoder.hpp; production glass.hpp retains mapping/geometry IDs.
CMake checks LF-normalized manifest/glass/input/form hashes embedded by project
export; stale input requires Generate UI. No .NET is needed for an ordinary build.

Actual contributor generation exposed a pre-existing path restriction hidden by
copied application fixtures. DesignerProjectPathService now permits explicit
outputs within a direct application child of library/examples containing its own
CMakeLists.txt and manifest. It still rejects library src/include, sibling examples
and protected inputs. Regression tests cover the boundaries. Updated service is
prepared; no extension API change or normal-profile extension reinstall was needed.

Windows 4/4, consumer 2/2, Core 101/101, Service 939, cast audit and zero-warning
solution build pass. Four stale-source fault injections and CRLF conversion pass.
Cross-build is 7,740 flash (+700), 64 static RAM, 1 KiB reserved stack. The image was
programmed/verified on the identified USB123456 Tiny Gecko, startup ready/frame/
calibration counters are 1, faults=0, LCD register rows match the independent
reference. Readback matches SHA2088888B94743788FFE41E790BEB45ABC9B854C1A7AFCEE33D77DC1A2FD32E62.
Startup stack touched400/1024. Hardware resumed; no retained debug connection.
Evidence: build/tiny-gecko-generated-20261018, including initial failed tests/logs.
Owner confirmed "Everything works on the hardware" against the generated-image
PLAY/zero/half-ring, PB0 play/pause, slider navigation, VOLUME adjustment and
cancel/save checklist. Generated hardware now has fresh physical acceptance.
The owner subsequently confirmed "The Windows simulator works too" for the same
generated presenter. Both Windows and hardware now have owner acceptance in
addition to the automated checks. Publication includes the segmented designer and
Tiny Gecko example; existing STM32 starters and private hardware documents are excluded.
See the updated example README, docs/segmented-designer.md and VALIDATION.md.

## Segmented project, native preview and Configure (2026-10-17)

This supersedes the earlier compiler-increment pending list. Implemented v4
segmented manifests with locked glass/input snapshots, multi-screen resolution
and export, field editing with editor-only preview values, Add screen, native
worker electrode encoding and final host expansion, and the VS Code Configure
workflow. Configure edits/imports glass JSON and input capabilities and relocks
all compatible screens together. It rejects stale or dirty sources. Export
preflights ownership and restores already-replaced headers if a later write fails;
it explicitly reports an incomplete rollback instead of claiming atomic success.

VS Code/service version 0.2.15. Prepared service/worker are under
build/vscode-designer/service and build/windows-mingw-debug. Source-only package:
build/segmented-designer/litheview-segmented-source.vsix. It was installed only
in an isolated test profile, not the owner's normal VS Code profile. Tutorial:
examples/efm32tg-stk3300-menu-1-complete/docs/segmented-designer.md; project: UI/designer.
Initial creation remains CLI (`segment create-project`); New Project and the
standalone desktop do not yet author new segmented projects. Polygon editing
is JSON-based; native preview is bounded to 1024 by 1024 and a work limit.

Verified: zero-warning solution build, Core 101/101, desktop App 18/18,
Service 939 checks, Node 91/91, C++ consumer/parser 2/2, cast audit, targeted preview-worker
Clang-Tidy and Cppcheck. Real installed-package VS Code exercise starts blank,
binds fields, previews, adds a screen, changes glass/input and generates both
headers while preserving application tasks. Evidence:
build/vscode-designer/integration-20261017T235412594/application with spaces/vscode-validation.json.
Service evidence: build/designer-service-tests/e7e7a859a0984fdea687ac5a90dee91e.
Logs: build/segmented-designer/final-* and preview-*.log. Native preview pixels
were inspected separately; no claim of a manual webview mouse walkthrough.

S4 remains pending: accepted Tiny Gecko runtime still uses handwritten encoding.
Do not claim generated firmware acceptance or new memory measurements. FSM,
original STM32 starters, public checkout and hardware untouched. No commit/push.
Private hardware PDFs and root photographs remain outside publication scope.

## Tiny Gecko Windows increment (2026-10-17, private checkout)

The owner requested the Windows-first segmented-menu implementation with PB0,
PB1 and the touch slider, preserving existing starters. New application:
`examples/efm32tg-stk3300-menu-1-complete`. It has a display-independent FSM, typed effects,
bounded slider translator, A03 character/digit mapping and Win32 segment drawing.
The new header-only `LitheView::Segments` target has no raster dependency.

GCC 14.2 warning-clean build, all three CTest suites, repository cast audit and a
separate installed-package consumer pass. The simulator's own render artifact was
visually inspected. The window was launched for the owner; a human walkthrough is
still pending. See the example VALIDATION.md for scope. No MCU was accessed, no
firmware fit claimed, and no segmented designer support was added. Decorative
LCD symbols, GPIO debounce and physical slider sampling remain open.

The original A03 manual was found at RS, with A03 schematics embedded on pages
21-30. At the owner's request, both the complete PDF and extracted schematics are
in `docs/private-hardware/efm32tg-stk3300-a03`, **private repository only**. Do not
copy that directory to LiTheView-public or a package. Its README records hashes.
The initial plan's SEG pin transcription was corrected against the actual sheet;
glass and MCU common numbering are reversed. Existing starters and the public
checkout were not modified. These changes have not been committed or pushed.

## Publication scope (2026-10-17)

The owner authorized publication of the validated designer, menu examples and tutorial
updates to public LiTheView and private LiTheView-full. The original H750 starter pair
and unrelated article drafts remain excluded. Only the two menu examples advance their
LiTheView dependency pins to the accompanying validated public commit.

The hardware follow-up found an H750 touch-I2C timeout that stops its UI task and ongoing
L4R9 BSP errors with reduced touch responsiveness. These remain documented, deferred
example limitations at the owner's request; publication does not claim they are fixed.
The host/editor checks and cross-build results below remain separate from these hardware
limitations. Earlier statements about uncommitted work describe their checkpoint only.

## Current direction

Work in the current LiTheView checkout; resolve its root with `git rev-parse --show-toplevel`.
The maintained developer path is the H750 starter pair under
`examples/`. One application uses the same platform interfaces in Windows and STM32 builds; the
designer supplements VS Code with form editing, Configure, Generate UI and typed handler hooks.
Application CMake owns builds and the application's launchers own debugging.

Read [DISPLAY_MODEL_PLAN.md](DISPLAY_MODEL_PLAN.md), [CONTROL_OBJECTS.md](CONTROL_OBJECTS.md),
[SHARED_APPLICATION_PLAN.md](SHARED_APPLICATION_PLAN.md) and
[LIBRARY_BOUNDARY_CLEANUP.md](LIBRARY_BOUNDARY_CLEANUP.md) before changing these contracts.

## Review corrections and blank-project acceptance (2026-10-17)

Designer 0.2.14 resolves the review's three P1 findings and five P2 findings.
Add screen rechecks the editor version immediately before its workspace edit, accepts
UTF-8 BOMs, and keeps valid screens visible when another registered form is missing.
Generated resource namespaces cannot collide with the Foo/FooAssets form pair, and
mixed legacy/scoped exports share one compiled source for identical native assets.
Each menu form owns its own resource provider. All five H750 handwritten handlers
pass the actual service binding plan used by Generate UI.

The round tutorial now maps designer-created component identities when installing
Playback, explicitly sets Play/Stop keyboard order, and uses compile-time page types
rather than internal feature macros. Its two platform presets remain unchanged.
The application FSM still owns navigation; the designer does not generate a task per form.

Validation is recorded in [MENU_REVIEW_VALIDATION.md](MENU_REVIEW_VALIDATION.md).
The installed source VSIX passes its general acceptance run and a fresh application
exercise from blank through all five menu screens. Both platform presets build at each
lesson. Final Windows CTest passes 7/7 for each menu, Core passes 18/18, designer Core
100/100, Service 492 checks, App 18/18 and extension units 88/88. The compiled multi-form
consumer links and runs. Critical static analysis passes Core (43 translation units)
and both Windows menu applications (11 each). These are host/editor and cross-build
results; no board was flashed or tested in this review-fix pass.

All 331 protected starter baseline files are byte-identical. The original starter pair,
private checkout and unrelated article drafts are untouched. Changes remain local and
uncommitted; nothing has been pushed. The tested extension is installed only in isolated
validation profiles. Menu dependency pins have not advanced: do that with a validated
library commit during the later publication step, not by pointing at unpublished history.

## Earlier Screens navigator checkpoint (2026-10-17)

Local work in the public development checkout adds Designer 0.2.12's Screens section.
It discovers manifest-listed forms sharing the current project's applicationRoot,
without assuming Tasks/UI or a particular middleware layout. It skips library/submodule
roots, generated/build directories, symlink traversal and unrelated application roots;
walks and manifest sizes are bounded. Refresh screens discovers added/renamed forms.
Navigation revalidates membership and uses normal persistent VS Code custom-editor tabs,
preserving dirty documents and undo. It runs no build, changes no FSM and does not bypass
workspace trust. Metadata discovery can work while the native service is unavailable.

The completed H750 example now names its five source forms Home.litheview.xml,
Settings.litheview.xml, Storage.litheview.xml, Volume.litheview.xml and Load.litheview.xml
in their existing per-page folders. Manifests and README match. Generate UI for all five
passes with byte-unchanged handwritten handlers and unchanged generated C++ output.
The L4 tutorial teaches Home's rename; page creation uses descriptive names and handler
connection reads the manifest rather than assuming main.litheview.xml. The original
starter pair and all 331 preservation-baseline file hashes remain unchanged.

Validation: 81/81 extension tests; actual source-VSIX integration passes, including a
second native-preview screen, unsaved edit preservation, return and undo; all five H750
forms inspect/render/export; the regenerated H750 host builds and passes 7/7. L4 helper
creation/connection with renamed forms passes in an isolated fixture. No MCU changes or
hardware operations. Evidence is under build/vscode-designer/screens-* and
integration-20261017T181157004/application with spaces/vscode-validation.json.
The source VSIX 0.2.12 is installed in the user's default VS Code profile. Reload the
existing window to load it; trust remains the user's decision. These changes are not
yet committed or mirrored into the private checkout. The unrelated article drafts stay local.

## Menu examples (2026-10-17)

The current menu work is in the public development checkout, in two independent applications:

- `examples/stm32h750b-dk-menu-1-complete`: completed Home (Play/Pause, Stop, Record, Settings), Settings (Volume/Storage/Back), Storage (Save/Load/Back).
- `examples/stm32l4r9i-disco-menu-1`: blank step-by-step round-display tutorial; same behavior at the end. No separate L4 completed companion, per owner direction.

Both compile the same FreeRTOS application on Windows and STM32. Display/input drivers are
application-owned; the library has no STM32 or RTOS dependency. L4 uses the full 390 x 390
bounding-square canvas with circular engine clipping, native RGB888 tiles and the recovered
revision-C DSI/GFXMMU adapter. No STemWin is linked. Tutorial page creation copies locked
profiles; handler hookup preserves named generated callbacks and application-owned FSM state.

The existing starter-1 and starter-1-complete are preserved (174 private and 155 public
baseline file hashes unchanged). The unrelated DEVTO article remains untouched/untracked.
The owner suggested a future workspace + standalone example repositories + library repository
layout; no repository migration was performed in this pass.

Read each new example's VALIDATION.md before claiming hardware completion. Both final
firmwares are programmed and host tests pass, but L4 idle touch reads show HAL I2C arbitration
errors; retained GDB evidence isolates the FT3X67 status-read path. On 2026-10-17 the owner
confirmed both boards navigate correctly for SETTINGS -> STORAGE -> BACK -> BACK, including
the requested L4R9 joystick directions and center check. Physical navigation is confirmed;
the idle arbitration errors remain unexplained. Keep failed evidence and do not silently
change bus timing.
The guided L4 source folder has been returned to blank. Its verified exercise forms are in
that application's ignored `build/completed-tutorial-validation/Tasks/`; exact flashed ELFs
are in root `build/menu-validation/20261017-113756-nested/`. Its current build output is blank.
Both ST-LINK/GDB launch backends were tested through download/reset/break-at-main/detach;
the programmer junction preserves the installation root so its device database resolves.

Volume is now implemented (2026-10-17): H750 has a fourth persistent form; L4R9 README
step 10 adds it after the three-page lesson with the same presets. The application-owned
value starts at 50%, steps by 5%, clamps at 0/100 and survives Back/reopen. Horizontal
keys adjust only on Volume; vertical keys move focus and center activates. H750 hardware
uses touch; the L4R9 has the physical joystick. No audio backend or persistent setting.

Windows tests pass 4/4 on both completed applications; L4's pre-Volume stage passes 3/3
and its restored blank stage passes 1/1. Both final MCU builds and re-established blank
L4 MCU build pass. Both boards were backed up, programmed and verified with Volume;
startup reads show ready=1, assertion=0 and advancing task heartbeats. On 2026-10-17 the
owner confirmed both Volume builds work correctly: +/- updates percentage and shading,
Back/reopen retains the value, and L4R9 joystick left/right adjusts while up/down and
center control focus/activation. This closes the physical Volume interaction check;
the idle touch-bus and transparent-sibling redraw issues remain open. Exact loaded ELFs/hashes/logs are in
`build/menu-validation/volume-20261017/hardware-run/`. The L4 exercise sources are in
its ignored `build/volume-tutorial-validation/Tasks/`; its source and build output are
blank again. The prior evidence above is preserved. No dependency pins changed.

A core redraw issue was exposed: a transparent label dirties a bounded root region that
can clip away an independently dirty progress sibling and clear its dirty flag. The Volume
lesson explicitly gives the changing label an opaque background matching the panel;
its native progress-pixel tests pass. The core transparent-sibling case remains open,
with a minimal reproduction at `build/menu-validation/volume-20261017/transparent-sibling-repro.md`.
Existing starter work is unchanged against all 331 files inventoried for this pass.

Playback is now implemented (2026-10-17): Home has a custom PlayPauseButton and separate
StopButton, with compile-time 16x16 alpha1 triangle/pause/stop masks (96 bytes of mask data).
Application/playback.hpp owns the three-state model; handwritten Home callbacks mutate
it and refresh controls. Generated named widget overrides select the subclasses with
typed accessors and no casts. Base ButtonView retains input/focus/cancellation behavior.
No library changes, RGB staging assets or runtime symbol scaling were needed.

H750 is the completed reference. L4 README step 11 teaches layout, custom types and
handwritten handlers; Lessons/Playback contains the advanced reference. The designer
shows the base button, not execution of application subclasses. Windows runtime renders
are in each example's docs/playback-*.png. Both completed Windows applications pass 5/5;
the L4 preceding Volume stage passes 4/4 and restored blank passes 1/1 plus its ARM build.
Both completed ARM builds pass (H750 flash 73,120 bytes, L4 79,276 bytes).
Both boards were backed up, programmed and verified; startup health is ready=1/assertion=0
with advancing task ticks. On 2026-10-17 the owner confirmed both boards work correctly:
Play/Pause symbols, captions and status change, Stop resets to STOPPED, Settings/Back
retains state, and L4R9 joystick-center activates the focused control. Physical playback
acceptance is complete; the previously recorded idle I2C and sibling redraw issues remain open.
Exact loaded firmware, hashes and logs: build/menu-validation/playback-20261017/hardware-run/.
L4 completed exercise: build/playback-tutorial-validation/Tasks/ inside that application;
published source and current build output are blank again. All 331 original starter
file hashes remain unchanged. No commits or pushes were made in this pass.

On 2026-10-17 the owner also confirmed the H750 Windows hands-on checklist passes:
mouse/keyboard, menus, Volume, playback and retained state. See its VALIDATION.md for
the exact executable hash; this is separate from automatic tests and physical boards.

Load is implemented (2026-10-17): application-owned SelectionList derives ControlView,
uses a generated named Panel override and a borrowed typed ListEvents observer. It has
selection vs activation, bounded Up/Down scrolling, explicit touch arrow targets,
Left/Right focus traversal and center/Enter activation. No radio controls, filesystem,
heap-per-row allocation or new library widget catalog entry. Eight fixed entries occupy
three visible rows. Designer preview shows the base Panel; native application renders
show the actual list. L4 README step 12 teaches this and step 13 teaches the board photo.

The owner confirmed the H750 Win32 walkthrough, completed L4R9 simulator/photo, and both
physical boards with the initial three-entry list. Expanded eight-entry scrolling is now
programmed/verified on both boards, with ready=1/assertion=0. On 2026-10-17 the owner confirmed scrolling works correctly on both boards;
physical scrolling acceptance is complete. Both completed Windows applications pass 7/7, including touch
photo mapping, list scrolling/cancellation and hidden-entry activation. ARM FLASH is
79,844 bytes (H750) and 85,988 bytes (L4); native framebuffer/workspaces are unchanged.
Evidence and exact images: root build/menu-validation/load-20261017/scroll-hardware-run/.
The preceding accepted firmware is retained in the sibling hardware-run/ directory.
The original radio prototype logs/renders are historical, superseded by final docs PNGs.

Both completed simulators use board photographs. H750 reuses the existing starter image
and sidecar; the blue B1 hotspot activates the focused control. L4 uses the owner-supplied
STM32L4R9AI-DISCO.png as board-front.png, clips the display/hit test to the circle and maps
the pictured joystick center. Its printed photo revision is not the hardware configuration.
Photo decoding stays in Windows. Completed executables plus matching images are retained
in root build/menu-validation/load-20261017/scroll-completed-{h750,l4r9}/.
The guided L4 source/build is blank again (1/1 Windows test and ARM build pass), while the
board remains on completed firmware. Its completed exercise sources are in the application's
build/load-tutorial-validation/Tasks/. All 331 original starter hashes remain unchanged.
The owner authorized publishing the menu examples and tutorials after physical scrolling
acceptance. No library/vendor dependency pins changed during validation. Existing I2C and redraw issues
remain open. Next work can assess promoting the demonstrated list contract into the core;
TableView currently remains a ScrollView subclass, not a selectable multi-column table.

Use H750 as the completed reference and extend the guided round-display L4R9 lessons in
small steps. Existing starter-1 work stays intact. These lessons do not imply real audio or
storage operations; the application owns those services and their platform adapters.

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
(1-255 entries), index identity and typed selection/edit notifications. Selection is tentative while
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

## 2026-10-17: One UI project and Add screen (local, not yet committed)

This supersedes the earlier per-page-project menu layout. Work is in the public
checkout. No private checkout source, original starter source or hardware was changed.

- H750 menu: one `Tasks/UI/project.litheview.json`, shared profiles, five XML forms in
  `Tasks/UI/forms`, outputs `Tasks/UI/Generated/{form}`, handwritten `handlers/<Screen>.hpp`.
  Duplicate page projects/profiles removed. The existing single UI task/FSM remains.
- Core exporter supports independent form identities, output paths, namespaces, CMake
  target names and shared native-resource source paths. The first legacy `form.main`
  retains flat output when new screens are added; canonical manifest ordering cannot
  relocate it. New layouts use `{form}` from their first screen.
- VS Code 0.2.13 Add screen applies a blank form plus manifest registration through an
  editor-owned WorkspaceEdit with source/conflict/path checks. Shared settings remain
  in one project. Generate UI exports the selected screen; CLI and desktop Generate UI
  export every registered screen. Whole-project export is not atomic: earlier successful
  components remain if a later screen fails. Handwritten handlers are never generated over.
- L4R9 tutorial now uses Add screen and one UI project. Command-line helper edits the
  same files. All handler/playback lessons and CMake detection use `Tasks/UI`.
  Published Tasks is blank again (kernel_memory.cpp only). Completed exercise retained
  under `examples/stm32l4r9i-disco-menu-1/build/multi-form-tutorial-validation`, including
  Tasks/UI, simulator, photo and cross-built ELF; older validation exercises are untouched.
- Validation: designer solution 0 warnings/errors; Core 100/100 (including shared asset
  CMake execution); service 515 checks; extension 82 tests; packaged VS Code integration
  passed at `build/vscode-designer/integration-20261017T190530626/application with spaces/vscode-validation.json`.
  That run creates a screen, previews it, adds a button, creates its distinct typed
  handler, generates, rejects a duplicate name, and preserves another form's unsaved edit/undo.
  H750 completed Win32 7/7 + STM32 cross-build; L4R9 completed Win32 7/7 + STM32 cross-build;
  restored L4R9 blank Win32 1/1 + STM32 cross-build. All five H750 product headers also
  compile together. No board was flashed or newly physically validated in this pass.
- Evidence: `build/multi-form-validation/`. Backup of previous menu sources in
  `build/multi-form-validation/menu-before/`. Original starter baseline checked: 331 files,
  zero changes. Unrelated article drafts remain untracked and untouched.
- Source-only VSIX 0.2.13 installed. Matching prepared service artifacts copied into
  both menu library dependencies' ignored build directories; only H750 designer service
  processes holding old DLLs were stopped. Reload VS Code to activate the extension.
- Publication remains pending: commit/push the library/designer change, advance only
  the menu examples' library pins to that published revision, and mirror the intended
  changes to the private repository. Current local runtime tests compile with existing
  pinned library C++ headers; prepared service artifacts are local, not a substitute for
  advancing those pins when publishing. Do not change the original starter pair.

## Hardware follow-up after review fixes (2026-10-17)

User requested flashing both boards. Both programmed/verified; H750 touch I2C timeout asserted at app_main.cpp:25 after 3792 heartbeats. Reset recovered execution, not root cause. Round L4R9 runs but BSP errors accumulate (4749 in final sample). The owner subsequently authorized publication with these example limitations documented and investigation deferred. Exact ELFs, backups, hashes and debugger trace: build/menu-validation/review-flash-20261017-165615. See MENU_REVIEW_VALIDATION.md. Original starters untouched.

## Tiny Gecko hardware increment (2026-10-17)

The owner accepted the Windows simulator, then authorized the basic hardware menu.
The existing FSM/presenter/glass encoder are unchanged. New Platform/EFM32 contains
bare-metal startup, 32 KiB/4 KiB linker budgets, GPIO/LCD/LESENSE adapters and a
foreground event loop. SDK v4.4.6 is a real example-owned submodule; no SDK enters
LiTheView core. Four host suites pass; Cortex-M3 firmware builds at 6,936 bytes
flash and 64 bytes static RAM plus 1 KiB reserved stack. Startup/idle watermark
observed 384 bytes. See the example VALIDATION.md for limits and exact binary hash.

J-Link USB selection 123456 (onboard firmware prints 440001204) identified the
EFM32TG840F32. Original flash saved privately, final image programmed/verified,
ready=1, frame=1, calibrated=1, zero scan faults. Board resumed and probe released.
The owner subsequently confirmed "Display and controls work" against the basic
menu checklist. Windows and physical-board acceptance are both recorded.
SEGGER tools came from the installed STM32CubeIDE bundle; no standalone install
was needed. Keep this distinction in the setup documentation.
Evidence: build/tiny-gecko-hardware-20261017. No STM32 touched; original starter
pair unchanged. No commit/push. Git submodule add staged only its gitlink and
.gitmodules automatically. Vendor PDFs and original flash remain private.

Next: the fixed-symbol advanced lesson. The
supplied EFM32-tiny-gecko.png remains untouched at root for a later Windows skin.
The Win32 rendering was deliberately left unchanged during this hardware port.

## Tiny Gecko volume-ring lesson (2026-10-17)

Added named eight-electrode volume ring, nearest-eighth scale, presenter binding
(saved volume normally, draft in editor), shared atomic presentation encoding,
and matching Windows polygons. FSM and device/LCD adapters unchanged. Tutorial:
examples/efm32tg-stk3300-menu-1-complete/docs/volume-ring.md. It explicitly explains that the
current raster designer cannot yet author segmented forms and identifies the
future field/binding/generation responsibilities without claiming that work done.

Four host suites and cast audit passed. Cortex-M3: 7,040 bytes flash, 64 bytes
static RAM, 1 KiB reserved stack; startup/idle watermark 400 bytes. Same Tiny Gecko
programmed/verified; no scan faults. Owner says ring worked on hardware, then
confirmed "The ring works in Windows too" after reopening the simulator. Both
platforms now have user acceptance for the ring.
Evidence and exact hash: build/tiny-gecko-ring-20261017 and example VALIDATION.md.
No commit/push. Original STM32 starters and user root photographs untouched.

## Segmented designer plan (2026-10-17)

See SEGMENTED_DESIGNER_PLAN.md for the next implementation stages. The foundation
is individually addressable on/off segments, with named icons, character fields
and ordered groups layered over them. The ring is a value binding over eight
pieces, not an animation. Animation remains a possible later application-timed
behavior. Glass definition, form bindings and hardware mapping are separate.

Plan: typed segmented display/document contract, bounded generation, native
preview capability, Configure and multi-form plug-in workflow, then a blank-project
Tiny Gecko tutorial and generated-code hardware acceptance. The FSM stays
application-owned and unchanged. No segmented designer implementation was added
in this documentation pass. Original STM32 starters remain untouched.

## Segmented designer compiler increment (2026-10-17)

Started S0/S1 in the private checkout. Core/Segments now loads strict version-1
glass JSON and segmented-form XML, validates hash-locked bindings and emits typed
C++ presentation encoders. CLI: `segment glass-hash`, `segment check`,
`segment export-cpp`. Definitions support individual/multi-electrode Boolean
icons, ordered scaled groups, ASCII character cells and unsigned decimal fields.
No raster profile, SDK, MCU selection, task or FSM is generated.

Tiny Gecko's definitions are in examples/efm32tg-stk3300-menu-1-complete/UI/design; the
accepted runtime still uses UI/glass.hpp and its handwritten presenter/FSM.
Synthetic glass and individual-piece fixtures are in tests/designer-fixtures/
segmented-icons. Reproduction and contract details: tests/segmented_designer_consumer/
README.md. Generated output stays in ignored build/segmented-designer/generated.

Validation: complete designer solution builds with zero warnings/errors; Core
101/101 passes. Sandbox initially blocked six existing PowerShell/temp-directory
tests; rerunning with appropriate access passed all of them. Generated MinGW
consumer passes the independent ring oracle, all numeric values, supported glyphs,
65,536 FSM histories and the synthetic Boolean/group/piece cases. Tiny Gecko
rebuilt in a separate Windows output directory and all four suites passed. Final
generated consumer also compiles warning-clean for Cortex-M3 with -Os.

The mixed 160-bit/17-bit consumer exposed GCC identical-code-folding array-bounds
diagnostics in SegmentFrame::set. A bounded std::span byte view avoids attaching
another instantiation's array type to the inlined access. No warning or optimizer
setting was weakened. A private old-access reproduction passes with -fno-ipa-icf;
production passes with folding enabled. Rebuilt handwritten hardware binary is
byte-identical to the accepted ring firmware (7,040 flash, 64 static RAM, same
957E90...0295 SHA-256). No board access or flash in this increment.

Pending: shared display-kind/project-manifest integration, preview-worker support,
Configure and segmented editing in VS Code, multi-form project export and switching
the example to generated code. Do not claim segmented plug-in support yet or install
this as a finished authoring workflow. No commit/push; original STM32 starters and
user root photographs untouched. Evidence: build/segmented-designer/.
