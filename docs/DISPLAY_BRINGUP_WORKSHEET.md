# Display and input bring-up worksheet

Copy this into the new product's documentation. Complete the simulation contract first; the MCU and
board sections can remain undecided. Follow [the tutorial](NEW_HARDWARE_TUTORIAL.md) for each gate.

## Product identity and sources

- Product/configuration name:
- Panel and controller part numbers/revisions:
- Display datasheet revision and relevant pages/figures:
- Touch controller / encoder / button part numbers and source references:
- Confirmed facts, unresolved assumptions, and how each will be checked:

## Display contract for simulation

- Logical width and height:
- Logical unit (pixel, emitter, cell):
- Shape, visible area, safe area, and orientation:
- Native destination format and build selection:
- Storage block width, height, and bytes:
- Field/channel bit positions, level meaning, and byte order:
- Row/page stride and origin/alignment constraints:
- Two independently specified coordinate/color-to-byte examples:
- Existing format / driver-only port, or new format implementation required:
- Host decoder and literal byte test locations:

## Input contract for simulation

- Input choice (touch, encoder with/without push, discrete buttons, keyboard):
- Application requirements (navigation, activation, text entry):
- `LogicalInputConfiguration` values:
- Touch coordinate mapping, contact ID and pressed/moved/released/cancelled semantics:
- Encoder direction/detents to navigation; push to activation:
- Key/button roles, repeat behavior, and text requirements:
- PC controls that produce those logical events:
- Replay cases, including outside release, lost capture, and repeated activation:

## Designer project and C++ ownership

- Selected checked/custom editing surface and exact native format:
- Project directory and `project.litheview.json` identity:
- Independent display-definition and logical-input snapshot IDs/hashes:
- Wizard logical input (Touch + Keys / Touch only / Encoder + Select / Output only):
- Tutorial starter selected, or reason for an empty form:
- `simulation/application.hpp` owner and source-control location:
- Tutorial behavior: ACTIVATE action 1, RESET action 2, `statusLabel` view 100:
- Generated-file ownership accepted; custom behavior kept outside generated files:
- Application CMake toolchains and debug tools configured in the IDE:
- Application workspace, Windows/embedded CMake presets and F5 launchers:
- If using a packed/native format unsupported by this exporter, handwritten adapter/example path:

## Simulation acceptance

- **Generate UI** result and `simulation/simulation.json`:
- Application CMake build and Windows F5 result and actual executable path:
- Application-hook edit survived export and changed the running C++ behavior:
- Canvas preview checked separately from application execution:
- Product, host adapter, and resource source paths:
- Fixed Core build/install prefix and matching application build command:
- Native bytes are rendered first and decoded only for host presentation:
- Expected diagnostic pattern and response to each available input:
- Native frame bytes / tile bytes / host-only presentation bytes:
- Full-frame/tiled comparison, literal bytes, rejected-transfer cleanup and retry:
- Layout/surface/input validation results:
- Debug/Release commands, source revision, captures and test evidence:
- `simulation/build/debug/build.log` and `build-result.json`, or matching Release/manual evidence:
- Open surface issues to resolve before proceeding:

## Hardware integration, completed after simulation

- Board/MCU/revision, SDK/toolchain, firmware target, and linker regions:
- Display bus, pins, reset/power/enable sequence, clocks, commands and addressing:
- Input bus/pins, raw coordinate calibration, interrupts, debounce and delivery context:
- Native-to-wire serialization, including byte/channel order and offsets:
- Full-frame or tile strategy; buffer owner, memory region, alignment and reuse point:
- Synchronous completion or explicit DMA completion/cache ownership:
- Raw pattern result, corners/levels verified, logic-analyzer capture and source identity:
- Portable application result, fault handling, cancellation and retry evidence:
- Actual linked flash, RAM by region, task/ISR stacks, resource bytes, latency and bus traffic:
- Target limits, measured pass/fail, and missing measurements:
- Reproduction/build/debug/flash instructions and remaining hardware qualification:

Do not convert a target budget failure into a surface compatibility failure. Keep both records so the
same working simulation can be evaluated against another MCU, storage strategy, or hardware revision.
