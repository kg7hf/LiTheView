# Starter split validation

2026-10-18 UTC, private checkout. These are build and automated test results;
no hardware was flashed for the split.

| Check | Result |
| --- | --- |
| Blank starter, Windows GCC 14.2 / C++23 | Build passed; CTest 3/3 |
| Completed reference, Windows GCC 14.2 / C++23 | Build passed; CTest 4/4 |
| Blank starter, EFM32 GNU Arm 14.2 | 5,192 bytes flash; 64 bytes static RAM |
| Completed reference, EFM32 GNU Arm 14.2 | 7,740 bytes flash; 64 bytes static RAM |
| Stack reservation | 1,024 bytes in each hardware link; not a worst-case measurement |
| Designer solution | Build passed, zero warnings/errors |
| Core / Service tests after fixture relocation | 101/101 / 953 checks passed |
| Independent generated-component consumer | 2/2 tests passed |
| Cast audit | Passed; separate 10 Win32 / 1 sensor ceilings for each Tiny example |

The blank Windows tests run the actual application: FSM sequences, button/slider
and LCD contracts, and a hidden-window walkthrough of mouse and keyboard events.
They assert that the blank presenter keeps every segment off. The completed
example additionally tests generated segment encoding against independent data.

An isolated installed source-only VS Code extension, version 0.2.16, used a copy
of the real blank starter in an external application directory containing spaces.
The test built the blank application, imported the glass through New Project,
checked cancellation, created the project, bound the label/number/ring, previewed,
saved and generated, added a screen, and applied Configure to both screens.
A normal build then discovered the generated header and passed all four suites,
without an explicit second configure command. FSM source and task configuration
were unchanged by authoring. The normal user extension profile was not modified.

Evidence is retained locally under `build/tiny-gecko-split` in the library checkout,
including the installed-package identities and external-application report in
`integration-evidence.json`. Logs are not shipped as source dependencies.
Both SDK submodules resolve to Gecko SDK v4.4.6 commit
`59d2160fe448b13730a91c0f019a8b4c53c43eb2`.

The completed reference's `tiny_gecko.bin` SHA-256 is
`2088888b94743788ffe41e790beb45abc9b854c1a7afcee33d77dc1a2fd32e62`.
It matches the retained pre-split firmware exactly. Previous human Windows and
hardware acceptance is recorded in the [completed reference](../efm32tg-stk3300-menu-1-complete/VALIDATION.md); it is not a
new physical test of the blank starter. Existing STM32 starters were not edited.
