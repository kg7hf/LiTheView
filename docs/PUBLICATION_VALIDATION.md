# Clean-history publication validation

Validated on Windows on October 17, 2026, from a fresh GitHub clone with recursive
submodules initialized from their upstream repositories. No local dependency
reference directories or junctions were used for that clone.

## Source identity

- Library/designer bootstrap: `8ca00c0ba4f80ea4e7e73b72c07a329ed48092b6`.
- Starter integration tested: `17e1b31f89882ead8cbf8d0d5d5fa2d1f87d3b9a`.
- Both starters pin LiTheView to the bootstrap commit. That commit contains no
  examples or Git submodules, so recursive initialization terminates without
  pulling earlier development history.
- Vendor submodule pins are unchanged. The guided starter has no generated UI;
  the completed starter contains the finished form and button/counter workflow.
- The subsequent publication-record commit changes documentation only.

## Results

| Gate | Result |
| --- | --- |
| Full C++ conformance | Passed: Debug 18/18, Release 18/18, installed consumer 4/4, asset-free installed and source consumers 3/3 each |
| Render strategies | Ten installed native-format/strategy consumers passed |
| Embedded core feasibility | Four Cortex-M33 configurations and Cortex-M7 cross-build/budget gates passed |
| Critical static analysis | Passed: Cppcheck and Clang-Tidy critical profiles |
| Designer Release solution | Built with 0 warnings and 0 errors |
| Designer Core / App | 99/99 and 18/18 passed |
| Designer Service | 507 checks passed, including native preview worker integration |
| VS Code extension unit tests | 74/74 passed |
| Installed source-only VSIX | 24/24 acceptance checks passed in isolated VS Code |
| Packaging attribution | Positive and negative notice/provenance tests passed; source-only redistribution package passed |
| Guided starter | Windows build and CTest 2/2; STM32 Debug cross-build passed |
| Completed starter | Windows build and CTest 2/2; STM32 Debug cross-build passed |
| Guided progression | Blank to generated UI with the same presets; CTest 2/2 before and after generation; paths containing spaces passed |
| Interaction | Mouse to named handler to ON/OFF label, plus physical-button simulation counter/hold/cancel checks passed |
| Debugger tooling | Discovery passed for both platforms in both starters; Windows GDB stopped at WinMain and exited the RTOS self-test normally; Arm GDB resolved main in the ELF |
| Source and history scans | Gitleaks 8.30.1 found no secrets in either the clean history or tracked source snapshot |
| Source audit | No broken local Markdown links, personal absolute paths, retired-device names, case collisions, notice hash mismatches or shared commits with private history |

The starter progression also rejected partial generated output, overlapping or
unmapped hit regions, changed photo hashes, missing compilers, incompatible x64
compilers and invalid debugger overrides.

| STM32 Debug application | Flash | DTCM | SDRAM |
| --- | ---: | ---: | ---: |
| Guided blank starter | 27,444 B | 13,256 B | 0 B |
| Completed starter | 61,236 B | 14,576 B | 522,240 B |

The validation used .NET SDK 10.0.400, host GCC 14.2, starter i686 GCC 16.1.0,
Arm GNU GCC 14.3.1 and VS Code 1.140.0. Installed source-only extension testing
used the locally prepared service and native preview worker. It did not replace
the developer's installed extension.

No board was probed, reset or flashed during this publication pass. The application
F5 interface was not exercised again; the underlying debugger executables and
launch paths were checked as described above. Cross-builds are not new hardware
acceptance evidence. Static-analysis acceptance uses the repository's configured
critical profiles, not a claim that every possible compiler diagnostic is absent.

## Reproduce

Install the tools described in [BUILD_ENVIRONMENT.md](BUILD_ENVIRONMENT.md), then
initialize dependencies and run the gates from the checkout root:

```powershell
git submodule update --init --recursive
./tools/run-conformance.ps1 -Generator Ninja
./tools/run-static-analysis.ps1 -Generator Ninja
./tools/prepare-vscode-designer.ps1
node --test tools/vscode/tests/*.test.js
./tools/test-package-notices.ps1
./tools/package-vscode-designer.ps1 -SourceOnly -ForRedistribution
./tools/test-vscode-designer.ps1 -PackagedExtensionPath build/vscode-designer/litheview-designer-0.2.11-source.vsix -SourceOnlyPackage
./tools/test-h750-starter-progression.ps1
```

If the selected SDK, compiler or editor is not on PATH, use the explicit tool
parameters documented in the scripts and build guide. The .NET SDK must satisfy
`tools/designer/global.json`. See [CONFORMANCE.md](CONFORMANCE.md) for the managed
suites and detailed gate scope, and each starter README for its normal application
build and debug commands. Temporary validation output belongs under `build/` and
is not part of the source publication.

## Public access verification

After publication, an anonymous clone and all recursive submodules initialized
successfully with credential helpers and authentication headers disabled. The
isolated check also disabled the user's Git configuration; its deeply nested
Windows checkout required `-c core.longpaths=true` for FreeRTOS dependencies.
The initial path-length failure was retained alongside the successful rerun.

Anonymous GitHub API checks confirmed the public repository, only the `main`
branch, no inherited pull requests or workflow runs, and no access to the separate
private full-history repository or its former commit through this repository.
