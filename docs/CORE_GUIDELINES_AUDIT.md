# C++ Core Guidelines safety audit

LitheView applies the C++ Core Guidelines as a risk-control system, not as an undifferentiated warning
count. The first enforcement gate covers findings that can corrupt memory, invoke undefined behavior,
break object lifetimes, escape fixed storage, or silently ignore an external failure. Style,
maintainability, and performance findings remain important, but they do not displace this gate.

## Critical and severe gate

The checked first-party scope is `include`, `src`, `examples`, and `tests`. Generated STM32 and vendor
code is outside the scope. The gate combines:

- `-Wall`, `-Wextra`, `-Wpedantic`, `-Wconversion`, `-Wsign-conversion`,
  `-Wdouble-promotion`, and `-Wold-style-cast`;
- Clang static analyzer core, C++, dead-code, and security checks;
- high-risk `bugprone` lifetime, optional, narrowing, loop, memory, and size checks;
- Core Guidelines initialization, ownership, bounds-decay, pointer-arithmetic, cast, slicing,
  special-member, and virtual-destructor checks;
- Cppcheck warning, portability, and performance analysis;
- host tests plus Cortex-M33 and Cortex-M7 cross-builds.

The current critical/severe Clang profile completes with no first-party diagnostics. The following
confirmed defects were corrected during the audit:

1. `Surface::valid()` and tile sinks now prove a nonzero stride immediately before division.
2. Rectangle edges, translations, painter math, scroll ratios, progress widths, and host coordinate
   scaling use checked or saturating arithmetic instead of signed-overflow behavior.
3. Bitmap stride and tiled-renderer pixel-count multiplication fail closed on `size_t` overflow.
4. `View::addSubview()` rejects attaching an ancestor as a child, preventing recursive tree cycles.
5. XML optional-parent access is structurally guarded, and custom freestanding number parsing rejects
   values that do not fit their destination type.
6. Fixed heterogeneous storage explicitly deletes copy/move operations and destroys only the active,
   discriminator-selected union member.

## Deliberate embedded-profile decisions

Three broad diagnostics need project-specific interpretation:

- Required collaborators are stored as references. They are non-null, non-reseatable lifetime
  dependencies; nullable relationships remain pointers. This is also the package's pointer-to-reference
  modernization rule.
- Dynamic indexes into fixed `std::array` storage are permitted only after a local capacity or count
  proof. `std::array::at()` is not substituted because the core deliberately builds without exceptions;
  spans are used at public contiguous-storage boundaries.
- `StaticLayout` uses a private tagged union in accordance with Core Guidelines C.180/C.181. Every
  construction, access, and destruction is selected by the private discriminator. A trial
  `std::variant` implementation increased the STM32H750 debug image by 31,996 bytes, so it was rejected
  for the small-target profile.

These are bounded design decisions, not blanket permission for nullable references, unchecked array
access, or naked unions elsewhere in the codebase.

## Whole-tree standards audit, 2026-08-30

The item-3 final audit checked all 55 first-party C/C++ source, header, and configured-header template
files against `.clang-format`; all pass without reformatting. A fresh warnings-as-errors host build,
including `-Wdouble-promotion`, passed with 16 reviewed cast-bearing lines. Exhaustive Cppcheck covered
24 translation units, and the critical/severe Clang-Tidy profile covered 17 translation units; neither
emitted a first-party diagnostic.

The audit removed a dead store in ellipsis planning, a redundant stored display reference, redundant
member initializers, static-member calls through instances, nested conditional expressions, a
discouraged `do`/`while`, and an implicit multiplication-width issue in a test. Saturating subtraction
parameters now use the mathematical names `minuend` and `subtrahend`, making call-site ordering
explicit. Generated font tables and the CMake substitution template use narrow, documented formatter
boundaries; their surrounding generated C++ remains mechanically auditable and deterministic.

Text searches found no tabs, dynamic-allocation call sites, exception/RTTI tokens, or TODO/FIXME
markers in first-party C/C++. The only `double` token is prose explaining 2x font scaling, not a type or
operation. `delete` tokens are explicit deleted special members that enforce ownership constraints.

The broader advisory `.clang-tidy` profile is not treated as a false zero. Its remaining reports are
concentrated in cognitive complexity in layout construction, demo dispatch, text layout, tiled
rendering, and XML parsing/writing, plus magic-number readability in protocol encodings, color values,
goldens, and test vectors. The iterator in `font5x7.cpp` deliberately retains `auto` because forcing a
pointer type would regress MSVC iterator portability. These advisory findings remain the risk-ordered
continuation described below.

## Item-4 whole-tree audit, 2026-08-30

The scene/gesture, customer-example, and generated-animation pass expanded the first-party scope to 75
C/C++ source/header files plus the configured C++ header template. All 76 now pass the repository
Allman `.clang-format` configuration. The audit found and corrected one include-grouping miss in the
template rather than excluding it from the gate.
Debug and Release warnings-as-errors builds pass for compact RGB565 and generated-font XRGB8888,
including `-Wdouble-promotion`; the explicit-cast audit remains at 17 reviewed lines.

The safety-focused script checked 34 translation units with Cppcheck and 27 with the critical/severe
Clang-Tidy profile. Both completed without a first-party diagnostic. Text scans found zero tabs,
TODO/FIXME markers, allocation APIs, or C++ `double` declarations/operations. The only `double` token
is prose in the compact-font implementation explaining geometric 2x scaling; allocation-like matches
are comments/test descriptions that explicitly document allocation-free behavior.

The audit also removed unnecessary C-style casts from the first-party H750 BSP configuration, renamed
the internal FreeRTOS assertion callback to house camel case, and documented the three deliberately
stable snake-case C-linkage debugger variables as a probe ABI. A dedicated project naming pass over all
27 portable/U545/test translation units completed without a naming diagnostic.

The complete advisory profile was inventoried as the item-gate modernization ratchet. Item 4 retires
the shared representation/color/font/default-configuration constant findings by introducing one
typed internal constants header and replacing repeated byte masks, channel maxima, channel shifts,
RGB565 fields, alpha1 packing values, palette capacity, gesture defaults, and transition timing
literals. The compact font now names its width, height, stride, advance, and line height. Its one
qualified-`auto` exception remains adjacent to the lookup and explains the MSVC iterator-portability
constraint. These changes shrink findings across every translation unit that consumes the common
headers, rather than merely moving demo-local numbers around.

The normalized pre-ratchet inventory contained 950 distinct first-party numeric-readability sites,
10 cognitive-complexity sites, five static-member candidates, two nested-conditional sites, and the
one portable-iterator qualification site. This item retires 61 shared numeric sites and documents the
iterator exception, leaving a measured starting bundle of 889 numeric sites plus the 17 structural
readability sites for later item gates. Vendor diagnostics and transient parser errors from an
inventory run concurrent with header edits are not counted.

The remaining advisory bundle is still concentrated in graphics-demo geometry and test vectors plus
cognitive complexity in the fixed-capacity layout/parser/writer and text-rendering paths. Those are
visible inputs to later item gates, not silently suppressed safety findings and not a requirement to
turn item 4 into an unrelated XML/layout rewrite.

Three unambiguous redundant member initializers were removed. `Bitmap::palette` deliberately retains
an explicitly empty trailing aggregate initializer: removing it caused six
`-Wmissing-field-initializers` compiler errors at supported partial aggregate call sites, so the narrow
advisory suppression records the stronger compiler contract.

Transparent overlays are redrawn with every visible underlying layer when a transition completes or
is cancelled. This bounded full-viewport cleanup prevents stale pixels from an intermediate translated
position without allocating a second framebuffer. A host regression renders the intermediate frame
before completion and verifies that the abandoned position is restored from the opaque scene.

## Item-5 phase-5.1 whole-tree audit, 2026-08-31

The display/target/project-contract phase reran the complete first-party gate even though the new
designer code is isolated C# tooling. All 75 C/C++ source/header files plus the configured C++ header
template pass the Allman Clang-Format check. The warnings-as-errors build retains
`-Wdouble-promotion`, the cast audit remains at 17 reviewed lines, exhaustive Cppcheck covers 34
translation units, and the critical/severe Clang-Tidy profile covers 27 translation units without a
first-party diagnostic.

For a directly comparable phase-5.1 advisory measurement, duplicate header reports from those 27
configured translation units were normalized by file, line, column, and check. The pre-ratchet result
contained 54 unique sites: 48 magic-number readability sites, five stateless member candidates, and one
known high-complexity fixed-capacity layout constructor. This narrower configured-translation-unit
measurement supplements rather than replaces the broader 889-site numeric inventory recorded at the
Item 4 gate.

Phase 5.1 retires 20 of those 54 sites. Repeated standard-demo layout, animation-frame, mutable-text,
device-name, and default-value capacities now have typed class constants; the round gesture marker has
a named initial position; and the five input methods that intentionally ignore all object state are
static. The same normalized profile therefore leaves 34 sites: 33 intentional/readability constants
and the existing layout-constructor complexity site. Protocol fields, resource ceilings, geometry
fixtures, and rendering test vectors remain visible rather than being disguised as generic constants.

The audit also exposed that round-display golden images contained the generated branch and commit text,
so the same source rendered a different CRC on every branch or dirty-state change. Dynamic version,
variant, branch, commit, and timestamp data remain in the Build Info overlay; the stable dashboard and
gesture-lab scenes now show a static `MORE: BUILD INFO` cue. Compact and Generated Sans full-frame and
tiled paths use reviewed branch-independent signatures, and CRC failures report actual and expected
values.

## Item-5 phase-5.2 whole-tree audit, 2026-08-31

The widget-schema compiler phase again reruns the whole first-party gate. All 78 C/C++ source/header
files plus the configured C++ header template pass the repository Allman formatter. Fresh
warnings-as-errors compilation retains `-Wdouble-promotion`; the cast audit remains at 17 reviewed
lines. Exhaustive Cppcheck covers 35 translation units and the critical/severe Clang-Tidy profile
covers 28 translation units without a first-party diagnostic. Text scans find no tabs, TODO/FIXME
markers, allocation call sites, or C++ `double` declarations or operations. The sole `double` token
continues to be prose explaining geometric 2x compact-font scaling.

The strict widget catalog covers all 15 runtime view kinds and 37 shared property definitions. Its
deterministic compiler generates immutable C# descriptors, allocation-free C++ metadata, and the
property reference from one canonical input. Generated bulk tables have narrow paired formatter and
numeric-readability boundaries; their complete sizes, slices, schema versions, and catalog identity are
compiled and checked in a dedicated test translation unit. This keeps the metadata auditable while
preventing the already large regression-test translation unit from exhausting Clang-Tidy memory. The
full Roslyn information-level style gate passes, Debug and Release .NET builds report zero warnings,
and all 29 contract/freshness tests pass in each configuration.

## Item-gate continuation

Each subsequent item gate reruns the full inventory and retires another risk-ordered subset of
cognitive complexity, intentional-constant extraction, API ergonomics, rendering performance, and
readability findings. The remaining count must ratchet down without weakening the no-heap,
no-exception, fixed-capacity, or freestanding contracts.

## Item-5 phase-5.3 whole-tree audit, 2026-08-31

The headless document-compiler phase reran the complete first-party gate. All 82 checked C/C++ source,
header, and configured-template files pass the repository Allman formatter. Generated form payloads use
the same narrow, paired formatter and numeric-readability boundary as the checked widget/font tables;
the generated declarations, accessors, integration shell, and preserved-region markers remain normally
formatted and compiled. The four Win32 RGB565/RGB888 Debug/Release profiles pass 2 of 2 tests with
warnings as errors and `-Wdouble-promotion`; the cast audit remains at 17 reviewed lines.

Exhaustive Cppcheck covers 36 translation units and the critical/severe Clang-Tidy profile covers 29
translation units without a first-party diagnostic. The added parity unit compiles generated C++,
strictly parses its canonical XML through the real runtime, compares typed `ViewSpec` fields, builds
both layouts, and requires identical full-frame RGB565 pixels. Text scans find no tabs, TODO/FIXME
markers, allocation API calls, or C++ `double` declarations or operations. The sole `double` token is
still compact-font prose; allocation-like matches are comments and explicitly deleted ownership
operations.

The broad advisory profile was normalized by file, line, column, and check over the same 29 configured
units. Phase 5.2 ended at 937 sites. Phase 5.3 leaves 908: 895 numeric-readability sites, 11 existing
cognitive-complexity sites, and two existing nested-condition sites. It retires scattered standard-demo
action/view IDs and adjustment bounds, gives the generated parity test semantic indices and capacities,
removes redundant generated `inline` specifiers, emits house-style enum constants, documents the
16-bit `ViewId` action ABI, and shares named animation-interval and scroll-step defaults between
`ViewSpec` and their runtime widgets. No suppression hides the remaining geometry, protocol, golden,
or test-vector debt.

The package-free .NET 10 solution passes 41 of 41 contract/freshness tests in Debug and Release with
zero build warnings, and the Roslyn information-level formatting/style gate is clean. Strict form
import is DTD/entity-disabled and size-bounded, rejects unknown metadata, and canonicalizes to a fixed
point. Logical resource identity, runtime name, symbol, and kind round-trip. Generated integration
save-zone bodies are
preserved byte-for-byte; malformed, nested, mismatched, duplicate, missing, and orphaned markers fail
without overwriting the developer file.

## Item-5 phase-5.4 whole-tree audit, 2026-08-31

The isolated-preview phase again ran the complete first-party gate. All 87 tracked C/C++ source and
header files plus the configured demo header template pass the repository Allman formatter. The four
Win32 RGB565/XRGB8888 Debug/Release profiles each pass 3 of 3 tests with warnings as errors and
`-Wdouble-promotion`; the new test is the native ABI/full-frame/tiled suite. The original core cast
allowance remains 17 reviewed lines. The separately measured C ABI, wire, Win32-handle, and typed-core
translation boundary decreased from 75 to 74 cast-bearing lines.

Exhaustive Cppcheck covers 39 translation units and the critical/severe Clang-Tidy profile covers 32
without a first-party diagnostic. The critical pass initially found five raw-pointer indexing sites in
the C ABI transaction. They now use bounded spans. The broad pass initially exposed 170 phase-owned
advisories: raw wire offsets, C ABI naming/layout recommendations, fixed-storage ownership warnings,
unsafe type bridges, duplicated branches, and boundary complexity. Wire offsets are named; byte/string
bridges use spans or `bit_cast`; branch duplicates are removed; and fixed storage is explicitly
bounded. The public C header narrowly records that macros, C enumerator spelling, snake-case fields,
and C enum widths are ABI requirements. The two visible preview/worker transaction state machines have
targeted complexity records because their fail-closed validation and dispatch order is itself part of
the reviewable boundary. These records do not disable the critical profile.

The phase also retires 12 inherited numeric-readability sites by naming compact icon geometry/tint,
build-information label IDs, decimal formatting base, and the maximum adjustment value. Normalizing
the same broad profile by file, line, column, and check therefore reduces the prior 908 sites to 896:
883 `readability-magic-numbers`, 11 existing cognitive-complexity sites, and two existing nested
conditional sites. None of the new preview implementation files contributes a remaining advisory;
their only repeated report was the already recorded fixed-capacity `StaticLayout::build` header site.

## Item-5 phase-5.5 managed walking-skeleton gate, 2026-08-31

This completed walking-skeleton phase changes only managed tooling, checked VS Code configuration, and
documentation; no embedded or first-party C/C++ source changes relative to the complete Phase 5.4
gate at `e29fe12`. The .NET 10 solution adds the Windows Forms host and package-free Windows UI smoke
executable while retaining warnings-as-errors, nullable analysis, recommended analyzers,
deterministic output, and enforced code style.

The cross-platform core suite passes 50 of 50 in Debug and Release. New coverage requires staged
project creation to avoid existing-directory overwrite, canonical save/export to reach a fixed point,
project-local custom
surface snapshots to resolve by their locked hash, the surface-to-preview mapping to remain exact, and
placement/property/action/duplicate/reparent/order/delete transactions to preserve undo/redo
save-point semantics. The Windows suite passes 5 of 5 in Debug and Release and checks the required
classic RAD work areas, display-first wizard engineering detail, accessible control identities,
stable keyboard commands, grid/snap defaults, schema-backed property edits, unsupported-field hiding,
and stable action IDs. The information-level Roslyn gate is clean. A hidden Release startup smoke
opens the checked document-compiler project and remains live until its exact test process is
terminated. No hosted runner, network package, embedded build, flash, or board access is involved.

The first-party C++ source/template scope is byte-identical to the complete Phase 5.4 gate at
`e29fe12`; the managed-only phase neither changes nor relabels the 896-site C++ advisory inventory.
The full project-wide C++ ratchet remains mandatory at the Item 5 gate and whenever a later phase
changes first-party C++.

## Item-5 phase-5.6A whole-tree audit, 2026-08-31

The resource-manifest foundation is managed build-time tooling, schema, fixtures, and
documentation, but this checkpoint still reruns the complete first-party C++ gate. All 88 tracked
C/C++ source, header, and configured-template files pass the repository Allman formatter. A fresh
warnings-as-errors build retains `-Wdouble-promotion`; the cast audit remains at 17 core and 74
preview-boundary lines. Exhaustive Cppcheck covers 39 translation units and the critical/severe
Clang-Tidy profile covers 32 without a first-party diagnostic. The three host tests pass. Text scans
find no tabs or TODO/FIXME markers; the sole `double` token remains the compact-font scaling prose.
Allocation-like matches remain comments and deliberately deleted ownership operations.

This phase retires five inherited `readability-magic-numbers` sites in `memory_budget.cpp`. The
32-bit Cortex-M address width, `View`, `ViewSpec`, `PickerView`, and 16-view `StaticLayout` RAM ceilings
are now semantic constants while the checked sizes and static-assert behavior remain unchanged. The
focused broad profile confirms that those five reports are gone and that the only remaining report in
the translation unit is the previously recorded `StaticLayout::build` cognitive-complexity site.
Under the same normalized inventory model, the inherited C++ advisory bundle therefore decreases from
896 to 891 sites without a suppression or weakened limit.

The package-free .NET 10 solution builds with zero warnings in Debug and Release. The Core suite
passes 56 of 56 and the Windows UI suite passes 5 of 5 in both configurations; the Roslyn
information-level gate is clean. Resource manifest validation, canonicalization fixed point, exact
source/output/license artifact verification, surface-compatible variant resolution, and stable
hardware-independent project identity pass. No hosted runner, network package, embedded flash, probe,
or physical board is involved.

## Item-5 phase-5.6B whole-tree audit, 2026-09-01

The font-resource slice changes managed build-time tooling, the offline PowerShell converter, strict
JSON contracts, checked metrics, tests, and documentation. Its generated regular/bold C++ tables are
byte-identical to Phase 5.6A. The item gate nevertheless reruns the complete first-party native scope:
all 88 tracked C/C++ source, header, and configured-template files pass Clang-Format; warnings-as-errors
retain `-Wdouble-promotion`; the cast audit remains 17 core and 74 preview-boundary lines; exhaustive
Cppcheck covers 39 translation units; and the critical Clang-Tidy profile covers 32 without a
first-party diagnostic. Debug and Release host tests pass 3 of 3, installed compact/generated-font
consumers pass 2 of 2, and the M33/U545 and M7 offline archive gates pass with the 872-byte maximum
stack-frame ceiling.

The Phase 5.6A ratchet already retired five inherited C++ advisory sites on this branch. Phase 5.6B
adds no first-party C++ source delta, suppression, or relaxed limit, so the normalized C++ advisory
inventory remains 891 rather than manufacturing a line-number-only count change. The managed side is
held to its equivalent complete recommended-analyzer and information-level Roslyn gates. During this
slice those gates caught and removed two collection-construction style findings, while the real
headless alpha1 smoke exposed and removed the converter's dependency on the host-specific
`Get-FileHash` cmdlet. Repertoire input is now capped before expansion, conversion cancellation kills
the child process tree, and strict metrics validate every face, integration, checksum, payload, and
line-metric field.

Debug and Release .NET 10 builds complete with zero warnings and errors. The Core suite passes 59 of
59 and the Windows UI suite passes 6 of 6 in both configurations; the Roslyn information-level gate
is clean. A real alpha1 CLI conversion produces 95 glyphs and a 3,615-byte measured immutable table.
The checked regular/bold alpha8 faces reproduce their existing FNV-1a checksums and 9,201/10,318-byte
payloads while upgrading to strict v2 metadata. No hosted runner, network operation, flash, probe, or
physical board is involved.

## Item-5 phase-5.6C whole-tree audit, 2026-09-01

The raster image/icon slice changes managed build-time tooling, the offline PowerShell converter,
strict resource metadata/schema, the checked resource fixture, tests, and documentation. It adds no
tracked first-party C++ translation unit or runtime decoder. The phase gate nevertheless reruns the
complete native scope: all 88 tracked C/C++ source, header, and configured-template files pass
Clang-Format; warnings-as-errors retain `-Wdouble-promotion`; the cast audit remains 17 core and 74
preview-boundary lines; exhaustive Cppcheck covers 39 translation units; and the critical Clang-Tidy
profile covers 32 without a first-party diagnostic.

Debug and Release .NET 10 builds complete with zero warnings and errors. The Core suite passes 60 of
60 and the Windows UI suite passes 8 of 8 in both configurations. The information-level Roslyn gate
found and removed one unnecessary CLI initialization before passing clean. Strict JSON parsing,
resource validation/verification, and project resolution produce resource SHA-256
`86d0b7df58111786ef3bad1d3e46514d4c7ba00a8e587a1d715fd1f2e2184a93` and resolved project SHA-256
`f0ef81eb5a6a024b49ba40a38e149fd3261ffd4847ff9aef7bf60357951277dd`.

A real two-pass 32-by-24 indexed4 golden converts the checked raster to 384 pixel bytes plus 40 palette
bytes. It reproduces source SHA-256, three FNV-1a checksums, generated C++, and metrics byte for byte;
the generated C++ passes the project formatter and a C++23 warnings-as-errors compile with
`-Wdouble-promotion`. No hosted runner, network operation, flash, probe, or physical board is involved.

## Item-5 phase-5.6D whole-tree audit, 2026-09-01

The animation-resource slice changes managed build-time tooling, the offline PowerShell converter,
strict resource metadata/schema, tests, and documentation. It adds no tracked embedded runtime or
first-party C++ translation unit. The phase gate nevertheless reruns the complete native scope: all
88 tracked C/C++ source, header, and configured-template files pass the Allman Clang-Format check;
warnings-as-errors retain `-Wdouble-promotion`; the cast audit remains 17 core and 74 preview-boundary
lines; exhaustive Cppcheck covers 39 translation units; and the critical Clang-Tidy profile covers 32
without a first-party diagnostic. The normalized inherited advisory inventory therefore remains 891
after Phase 5.6A's five-site ratchet rather than reporting a line-number-only change for a managed
resource slice.

Debug and Release .NET 10 builds complete with zero warnings and errors. The Core suite passes 61 of
61 and the Windows UI/golden suite passes 10 of 10 in both configurations; the information-level
Roslyn formatter/analyzer gate is clean. Strict resource validation, verification, and project
resolution retain resource SHA-256
`86d0b7df58111786ef3bad1d3e46514d4c7ba00a8e587a1d715fd1f2e2184a93` and resolved-project SHA-256
`f0ef81eb5a6a024b49ba40a38e149fd3261ffd4847ff9aef7bf60357951277dd`.

A real two-pass animation golden extracts eight 96-by-72 indexed4 frames from the checked sprite
sheet. It reproduces 27,648 pixel bytes, one 40-byte shared palette, source SHA-256, all three FNV-1a
checksums, generated C++, and metrics byte for byte. The generated translation unit passes
Clang-Format and strict C++23 compilation with warnings as errors, `-Wdouble-promotion`, exceptions
disabled, and RTTI disabled. No hosted runner, network operation, flash, probe, or physical board is
involved.

## Display-model M4 bounded traversal and typed presentation, 2026-09-26

The M4 guidelines pass removes avoidable recursion from `ViewController::collectFocusable`. It follows
the caller-owned parent/child links iteratively, skips hidden/disabled branches, and stops once the
bounded focus list is full. Traversal uses constant stack space regardless of view nesting. Focus
retention and event dispatch also verify ancestor eligibility. No heap-backed work list, exception,
or RTTI mechanism was introduced.

Win32 presentation now exposes `std::span<const Xrgb8888Pixel>` throughout the C++ display adapter and
uses `.data()` only at `StretchDIBits`, whose C API accepts the untyped buffer. The window callback's
context recovery remains a real Win32 C boundary with its existing audited cast marker. This follows
the embedded philosophy's type-preservation rule: `void*` is not a generic substitute for typed
borrowed interfaces, and a C callback does not justify type erasure inside the portable application.

The retained checkpoint is `build/display-model-m4-static/validation.json`. The unchanged critical
Clang-Tidy policy passes 62 configured entries with warnings as errors. Exhaustive warning/portability
Cppcheck and unsuppressed include-resolution checks pass 112 entries: 62 host and 50 first-party H750
configurations. Formatting passes 13 changed files, and the cast audit remains at 17 core and 74
preview-boundary cast-bearing lines. Expected format-only changes were rechecked; no unexpected input
change was found. No critical or severe first-party finding remains in this configured scope.

The complete advisory profile was also run on 14 changed-file/configuration entries: seven portable
units and seven fixed Win32 main variants. It retains seven unique findings: four readability
suggestions in `surface_layout.hpp` (two precedence-parentheses suggestions, member initialization,
and cognitive complexity 41 against threshold 25), plus three intentional invalid-enum rejection
tests. This changed-file inventory is not comparable to the older whole-tree count of 891 and does
not replace it with a claimed zero. Independent consumer configurations and unconfigured integration
sources are explicitly listed as exclusions in the checkpoint rather than analyzed with guessed flags.

Full native conformance passes Debug/Release 15/15 and installed-package 3/3. Five separate M33 profile
gates retain their ceilings and report an 800-byte maximum bounded function frame; this is not a
whole-call-tree stack proof. The generic M7 and both H750 standalone cross-builds also pass. The M4
managed checkpoint passes 67/67 Core tests with zero build warnings/errors. No hardware was accessed
for this audit, and M2 physical emitter-order verification remains open.

The M5 pass uses typed evidence metrics/units and explicit unknown results, removes generated-source
size as a substitute for archive measurements, and binds compiled form capacity facts instead of
trusting a later reinterpretation of widget metadata. The independent consumer uses the checked
`buildSurfaceLayout` adapter. Six configurations pass critical Clang-Tidy/Cppcheck across 12 translation
units (`build/display-model-m5-consumer-static/validation.json`). Changed managed files pass the
information-level Roslyn analyzer check; strict Debug/Release solutions and Core 69/69 plus UI 11/11
pass. These are scoped improvements, with the existing advisory inventory retained for later passes.

## Item-5 phase-5.7 slice-2 button gate, 2026-10-01

The button slice adds no heap use, exception, RTTI, macro, `void*`, or cast. The new state is a
spare `View` flag; the icon is a nullable borrowed lookup result, the only pointer-shaped member
added, and `ButtonView::setIcon(const Bitmap&&)` is deleted so a temporary cannot be borrowed.
`setSelected()` reports a rejected momentary selection by returning `false`; the layout builder
calls it only for toggles instead of discarding the result through a cast. Button geometry constants
are named, and a 32-bit `static_assert` keeps `ButtonView` within the largest existing layout member.

Two pieces of existing debt were retired in the touched code. The XML parser's six copied
state-color branches became one `stateColorMember()`/`applyStateColor()` helper pair, lowering
`applyAttribute()`'s length and branch count while adding the two selected-color attributes.
Checkbox and radio buttons now share the `View` selected bit instead of duplicating a Boolean,
which also shrinks each by 4 bytes on a 32-bit target.

The gate also exposed a generator defect outside this slice's runtime code. GCC 13 constant
evaluation zero-fills the omitted members of a value-initialized array element written through a
reference after skipped elements, so generated state-style tables could silently replace omitted
colors with explicit transparent black. The generator now builds each entry locally and copies it;
the generated-form test compares every state color, which catches the defect on the host compiler.

Evidence, from a Linux cloud container: warnings-as-errors host build and CTest 15/15; the cast
audit is unchanged at 18 core, 74 preview, and 1 simulator-boundary cast lines; exhaustive
warning/portability Cppcheck 2.13 is clean over 60 files; clang-format 18 is clean for every changed
C++ file; and the critical Clang-Tidy profile is clean for 46 of the script's 47 translation units
when Clang 18 is given `-D__cpp_concepts=202002L` so GCC 13's libstdc++ exposes `std::expected`. The
47th, the unchanged Win32-only `designer_preview/worker_main.cpp`, cannot parse on Linux without
`windows.h`; it must be analyzed on Windows.
Without that definition, parse errors cascade into spurious `init-variables` reports in untouched
files; those are a tool-pairing artifact, not findings. The broader advisory inventory was not rerun
here; it remains for the owner's Windows static-analysis gate.

## Item-5 phase-5.7 slice-3 tab gate, 2026-10-01

The tab slice adds no heap use, exception, RTTI, macro, `void*`, or cast. Tab count, selection, and
the pressed tab are `std::uint8_t`; values entering them from `std::size_t` APIs pass through the
existing range-checked `detail::assignIfRepresentable()` after clamping to `kMaximumTabs`, and tab
geometry is computed in `std::int32_t` from the promoted byte values, so no narrowing conversion is
spelled. Titles are a borrowed `std::string_view`; only the count is parsed, and a title is located
on demand without storing offsets. The strict XML title counter is a loop rather than a
`std::count()` result cast. Geometry and limits are named constants (`kDefaultTabHeightPixels`,
`kMaximumTabs`, `kTabIndicatorThickness`), and 32-bit `static_assert`s keep both widgets within the
largest existing layout member.

Key-driven tab changes needed a way to report an action without a controller dependency in the
widget. The chosen mechanism is one spare `View` bit set by the widget and consumed by
`ViewController::handleKey()` immediately after the widget handled that key; the tab bar assigns
the bit on every key, so a stale request cannot produce a later action. No virtual function or
per-view storage was added for it, and other widgets keep their existing behavior.

`TabView` reuses the `View` hierarchy instead of a page table: pages are its direct children and
`syncPages()` sets their visibility, so drawing, hit testing, focus traversal, damage, and the
controller's hidden-focus release need no new code paths. `StaticLayout::build()` now calls one
`finishContainer()` post-pass for scroll fitting and page synchronization.

Evidence, from a Linux cloud container: warnings-as-errors host build and CTest 15/15; the cast
audit is unchanged at 18 core, 74 preview, and 1 simulator-boundary cast lines; exhaustive
warning/portability Cppcheck 2.13 is clean; clang-format 18 is clean for every changed C++ file;
and the critical Clang-Tidy profile (Clang 18 with `-D__cpp_concepts=202002L`) is clean for every
changed translation unit. The Win32-only worker and the advisory inventory remain for the owner's
Windows static-analysis gate.

## Item-5 phase-5.7 slice-4 wheel picker gate, 2026-10-02

The wheel picker adds no heap use, exception, RTTI, macro, `void*`, or cast. Item count, selection,
the value saved when editing starts, and the row height are `std::uint8_t`; values entering them
from `std::size_t` APIs pass through `detail::assignIfRepresentable()` after clamping to
`kMaximumItems`, and a `static_assert` ties `kMaximumItems` to the byte range. Row geometry is
computed in `std::int32_t` from the promoted byte values, so no narrowing conversion is spelled.
Items are a borrowed `std::string_view`, counted and located on demand by the pipe-delimited helpers
that the tab bar now shares, so no offsets are stored. Constants are named (`kMaximumWheelItems`,
`kDefaultWheelRowHeightPixels`, the tap slop, fade alphas, and edit-bar geometry), and a 32-bit
`static_assert` keeps the widget within the largest existing layout member.

Pointer travel is computed with the existing saturating subtraction and then bounded before any
other arithmetic: a non-wrapping wheel clamps the offset to its first and last items, and a wrapping
wheel keeps the sign-preserving remainder of one full cycle, which draws and snaps identically. The
offset therefore stays below 255 rows of 255 pixels for any reported coordinate, rounding cannot
overflow, and drawing visits only the rows that intersect the frame (at most frame height divided by
row height plus two), independent of how far a contact travelled. A host test drags thousands of
cycles and checks the snap.

Ending editing on focus loss needed a notification that the widget could not get before. The chosen
mechanism is a protected `virtual void focusChanged() noexcept` on `View`, called only when the
focused flag actually changes (including through `setEnabled(false)` and `setFocusable(false)`); it
adds no per-view storage, and the default does nothing. Reporting a drag that lifts outside the wheel
reuses the existing one-bit action request: the controller consumes it on every focusable release and
reports only when it was set, so widgets that never set it behave exactly as before.

Evidence, from a Linux cloud container: warnings-as-errors host build and CTest 15/15; the cast
audit is unchanged at 18 core, 74 preview, and 1 simulator-boundary cast lines; exhaustive
warning/portability Cppcheck 2.13 is clean; clang-format 18 is clean for every changed C++ file;
and the critical Clang-Tidy profile (Clang 18 with `-D__cpp_concepts=202002L`) is clean for every
changed translation unit. The Win32-only worker and the advisory inventory remain for the owner's
Windows static-analysis gate.

## Item-5 widget-set layouts, 2026-10-02

`StaticLayout<Capacity, Widgets = AllWidgets>` adds a compile-time `WidgetSet<ViewKind...>` so a
product links only the widgets its layouts build. The change adds no heap use, exception, RTTI,
macro, `void*`, or cast. With a restricted set, `build()` rejects a kind outside it with the
existing `LayoutError::invalidKind` before touching its resources (for `AllWidgets` that check is
compiled out and the switch's `default` still rejects invalid values), and every `build()` case is an
`if constexpr` branch, so a discarded kind's constructor, vtable, and members are never referenced.
The four per-kind `switch` statements over the tagged union (two `viewPointer()` overloads,
`destroyAt()`, and `finishContainer()`) became one `visitStored()` fold over the stored view types
that visits only kinds in the set; the private union, `kindFor()`, and `storageAddress()` are
unchanged, the union still has a member for every kind (so `sizeof` is unchanged), and the
`static_assert` in `visitStoredAs()` keeps the visited type list in step with `AllWidgets`. The
default `AllWidgets` keeps every existing `StaticLayout<N>` behaving exactly as before.

Evidence, from a Linux cloud container: warnings-as-errors host build and CTest 15/15 (new tests
cover a restricted set, rejection order, rebuild after rejection, the default set, and each
generated form's declared set); the exported simulator project builds with `Form::Layout` against
an installed RGB565 Core and its smoke test passes; the cast audit is unchanged; Cppcheck and the
critical Clang-Tidy profile are clean for the changed translation units.

## Item-5 portable preview worker, 2026-10-02

The native preview worker (`src/designer_preview/worker_main.cpp`) now builds on Linux and macOS.
The Windows code is unchanged apart from naming its stdio handle type `Stream`; a stand-in
`windows.h` syntax check found no new diagnostics in that branch. The POSIX branch adds no cast,
macro definition, heap use, or exception: it selects code with the compiler-defined `_WIN32`, reads
and writes framed records with `std::fread`/`std::fwrite` (whose `std::size_t` results need no
conversion) and flushes each record, and renders into a fixed `std::array` bounded by the maximum
surface at four bytes per pixel. That buffer sits in zero-initialized static storage, so untouched
pages cost no memory; it is zeroed up to the requested capacity before each render so a frame never
inherits the previous one, matching the fresh mapping Windows receives. The inline frame is
appended with `std::as_bytes()`. The preview-boundary cast count stays exactly 74.

Evidence, from a Linux cloud container: warnings-as-errors build of the worker in the existing host
tree and the new `host-debug` preset, CTest 15/15 in both, cast audit 18/74/1, clang-format 18,
critical Clang-Tidy, and Cppcheck clean for the worker; the managed worker tests reproduce the
Windows frame hashes. The Windows branch still needs the owner's MinGW build.
