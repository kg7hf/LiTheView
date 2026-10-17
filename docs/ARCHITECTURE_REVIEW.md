# LTE architecture review and decisions for LiTheView

Reviewed 2026-09-27. This is a source-based architectural assessment of the supplied
LTE archive, not a reconstruction of its historical qualification or field behavior.
The Win32 and LPC1850 paths were inspected; the owner's recalled STM32 and EM32
deployments were not independently checked. The archived project remains unchanged.

Reference: the owner-supplied private LTE archive. Its machine-specific location is not a
build dependency; all source references below are relative to that archive.

## Assessment

LTE's core portability decision is sound: build the real application and its RTOS
tasks for the PC, with device/CPU implementations selected by the build. This is a
useful architecture today. Its task-local heaps, MPU integration and receiver-owned
message copies form a deliberate isolation model: bounded dynamic messaging within
a lightweight protected-memory system. Its filesystem also preserves the embedded
middleware on the host, replacing the storage device beneath it. Those are strengths
to retain. Product-service and host-presentation coupling still deserve improvement
without discarding the isolation and middleware boundaries.

Correction after a second source pass: the initial review treated the current-task
allocator as if messages could be freed through an arbitrary task's heap. That was
an incomplete analysis. The message path explicitly selects the recipient's heap
for construction and destruction, within an MPU-aware task framework. The findings
below supersede that criticism.

The language revision is not the deciding factor. Explicit dependencies, RAII and
clear ownership were already possible in C++11. C++23 gives us better tools for
expressing contracts; it does not choose the contracts or enforce every lifetime,
capacity and synchronization assumption for us.

## Evidence and consequences

The following file paths are relative to the reference root. Line numbers refer to
the inspected archive, not another historical revision.

| Finding | Concrete evidence | Decision for the new system |
| --- | --- | --- |
| The PC really runs the application and RTOS | `Platform/Win32/WinMain.cpp:241` initializes uC/OS, constructs the shared `ExecutiveTask` and starts the scheduler. `Platform/LPC1850/main.cpp:95` starts the same executive. The two platform manifests compile shared application, power and input tasks. | Retain one source list for the product/model/presenter/task implementations and select platform sources in each build. |
| Product services leak into reusable-looking GUI code | `Frameworks/LCDViewGUI/LCDViewApplication/LCDViewApplication.h:14` includes task and rules-engine context. `Frameworks/LCDViewGUI/LCDViews/LCDBatteryIconView.cpp:22` and `:47` reach through application/task/executive to a battery algorithm. `Frameworks/LCDViewGUI/LCDViews/LCDDateView.cpp:50` reaches into product settings. | A presenter supplies view data and routes typed actions. The graphics library must not know the executive, settings store, sensor model or RTOS. |
| Central startup also becomes global service lookup | `Tasks/Executive/ExecutiveTask.h:136` onward exposes numerous subsystem getters and `:260` exposes `instance()`. | A composition owner can construct the system, but consumers receive only their required dependencies. Avoid passing a universal system/executive reference to every object. |
| Task-private allocation and receiver-owned messages support isolation | `Services/API/CTask/CTask.cpp:139` creates an mspace within each task's memory and `:151` creates its protection process. `:304` clones into the receiver's `m_heap` inside `SP_DO_PRIVIL`; `Frameworks/Messaging/Message.h:159` uses the heap-selecting placement-new overload. The receiver calls `InPlaceDelete(m_heap, p_msg)` at `CTask.cpp:403`; `Services/API/Memory/TaskMemory.h:190` destroys the object and explicitly frees through that heap. | Preserve task/domain ownership and trusted cross-domain copying. A typed owner does not grant another task access to protected memory. Keep memory source, permitted allocation phase and protection domain as separate decisions. |
| Failed message enqueue has a fatal-error contract | `Services/API/CTask/CTask.cpp:308` posts the recipient-owned clone; `:330` routes a failed handoff through the critical-error handler without locally reclaiming the unqueued clone. | Specify whether a failed send terminates the system or returns to continued operation. A recoverable send must reclaim recipient storage through an authorized path. No leak or runtime failure is established without the handler's termination semantics. |
| Filesystem middleware remains shared down to the storage-device boundary | `Platform/Win32/Simulation.vcxproj:788` and `Platform/LPC1850/EP3/LTM.ewp:7976` include the same uC/FS sources. Shared `HAL/FS/FSDriver.cpp:357` calls QSPIFI. `Platform/Win32/drivers/qspifi/qspifi.cpp:192` opens `SimQSPIFI.bin`; the embedded implementation invokes flash programming at `Platform/LPC1850/drivers/qspifi/qspifi.cpp:311`. | Keep the selected filesystem and its relevant configuration common. Substitute a file-backed device below it so filesystem, settings and database paths execute on the host. |
| Host presentation crosses scheduler and product boundaries | `Platform/Win32/WinMain.cpp:307` manipulates task/ISR state around the window procedure; `:499` reaches into the application during paint. `Platform/Win32/drivers/LCDController/LCDController.cpp:37` holds scheduler/critical protection across allocation, conversion and GDI output, and allocates a presentation buffer each update. | Put required simulated-interrupt integration inside the RTOS/device port. Windows presentation receives a stable frame through a bounded handoff and does not traverse product view controllers. Define frame ownership and avoid per-frame allocation. |
| Some simulated devices exercise only a successful nominal case | `Platform/Win32/drivers/adc/CADC.cpp:37` returns 950/success; `:48` onward supplies fixed successful temperature and battery values. `Platform/Win32/drivers/serial/CSerial.cpp:42` opens successfully while read/write implementations are stubs. `Platform/Win32/drivers/clkpwr/ClkPwr.cpp:92` exits the process for deep power-down. | Make simulation capability explicit. Unsupported operations report it; supported devices accept scripted values, failures, stale data and delayed completion. Same source is necessary but not sufficient evidence for fault handling. |

There is also a concrete source-level bounds concern in the message history path:
`Services/API/CTask/CTask.cpp:292` checks `source_cnt > OS_MAX_TASKS` before the
indexed write at `:298`, while `Frameworks/Messaging/Message.h:125` declares an
array with `OS_MAX_TASKS` elements. Equality is not rejected by that guard. This was
not reproduced at runtime, and it is not a verdict on the whole project. It illustrates
why capacity assertions and boundary tests matter even in an evolved shipped system.

## Task heaps, protected handoff and the MPU

The ordinary task-local `new/delete` implementation is part of a larger ownership
contract. `Services/API/Memory/TaskMemory.h:10` documents separate heaps/stacks and
the MPU rationale. The inspected message path is:

1. The sender retains its original message.
2. `CTask::postMessage` performs a privileged clone into the destination task's heap.
3. The queue receives the destination-owned clone.
4. The destination processes, destroys and releases that clone through its own heap.

This is a controlled copy between memory domains, not arbitrary sharing of one heap
object between tasks. `SP_DO_PRIVIL` saves/changes/restores privilege through the
port macros; it does not select the destination by changing `OSTCBCur`. Separate
startup/shutdown code does set task identity for lifecycle allocation. Those paths
must be assessed as lifecycle mechanisms, not conflated with the message handoff.

The embedded configuration enables `OS_SP_EN` at
`Services/COTS/Micrium/Software/uC-MPU/cfg/sp_cfg.h:56`.
`Services/COTS/Micrium/Software/uCOS-II/Ports/ARM-Cortex-M3/IAR/os_cpu_c.c:317`
loads the incoming task's protection process through `SP_ProcSwitch`; the latter
programs the region table in
`Services/COTS/Micrium/Software/uC-MPU/ports/ARM-Cortex-M3/LPC1850/sp_cpu.c:329`.
`CTask.cpp:172` also assigns explicit shared regions for system, filesystem and
database storage. Private and deliberately shared memory coexist.

Scope the protection claim precisely: this archive's LPC1850 `sp_cpu.h:74` at the
same MPU port path defines its default 4 GB region as `MPU_ATTR_AP_PRW_URO`
(privileged read/write, user read-only). The examined mapping supports task-local
write protection, but does not establish blanket read exclusion from other tasks'
heaps. The Win32 configuration disables hardware MPU support at `sp_cfg.h:53`.
Neither source inspection nor a host run substitutes for target protection-fault
tests, and this snapshot does not establish every shipped revision's permissions.

For new code, retain domain ownership and the trusted handoff. Express domain and
destination identity with types and keep privilege changes inside the application
RTOS/platform boundary. A static receiver-owned pool can obey the same isolation
contract as a private heap. Validate payload lifetime, failed-send cleanup and
permitted destruction context. LTE already uses a noncopyable RAII privilege guard
in `Services/API/utility/ScopedPrivilegedMode.h`; RAII is not a new invention of C++23.

## Shared filesystem over a simulated device

Both manifests select the shared uC/FS core, FAT implementation and uC/OS integration,
with `Services/COTS/Micrium/Software/uC-FS/Cfg/fs_cfg.h` as the shared configuration.
`Services/API/Settings/SettingsBase.cpp:80` and
`Services/API/RecordDB/RecordDB.cpp:112` use the same filesystem APIs. On Windows,
the QSPIFI adapter translates device addresses to offsets in `SimQSPIFI.bin`, reads
and writes the image, and reopens it across normal runs. Application files still
pass through uC/FS; Windows file I/O supplies the simulated device beneath it.

This is a useful model for the tutorial's persistence extension. Keep the product
storage middleware common and model its device contract, with reproducible images
and injected errors. Match the abstraction actually exposed: LTE's embedded write
uses an erase-and-program operation, so the host adapter need not pretend that its
write API is a raw flash-programming primitive.

The inspected image implementation does not establish flash timing, wear or
power-loss recovery fidelity. Host I/O/flush results are not checked before success
is returned, and a simulated write delay is commented out. Those are candidates
for future simulator improvements, without diminishing the shared filesystem design.

## Tighten our proposed architecture

1. **Separate model behavior from task execution.** The product model accepts typed
   events, device results and explicit time. Production RTOS tasks invoke it; a
   deterministic headless runner invokes the same implementation. Preserve the
   same-kernel interactive simulator as an additional integration mode.
2. **Specify device behavior, not just method signatures.** State whether calls block,
   how pending work completes, how cancellation retires borrowed buffers, who may call
   each method, and what happens on unavailable/stale data or a full queue. Shared
   headers with alternate implementations work when representations fit. Where host
   and MCU state differ, platform-owned concrete objects can implement small typed
   contracts; do not force OS/HAL handles into a common header, `void*` or singleton.
3. **Make dependency boundaries compile-time facts.** Targets for model, presenter,
   tasks and each platform have deliberate include/link dependencies. Model/UI cannot
   see HAL or Windows headers. Tasks may depend on the chosen common RTOS API while
   the model and `LitheView::Core` remain independent. A small standalone consumer
   should fail immediately if a forbidden dependency appears.
4. **Keep scope bounded.** LiTheView owns graphics and UI tooling. The example
   application owns its kernel, devices, control policy, memory owners and build.
   Preserve the three requested memory policies, but implement them after proving
   one static end-to-end path. Do not create a universal HAL, RTOS wrapper or container
   framework as a prerequisite for the example.
5. **Keep the designer replaceable.** Generated files and developer-owned code have
   distinct ownership. Builds and tests work through normal application targets
   without the editor. The VS Code extension remains an editor and task integration.

The revised sequence is recorded in [SHARED_APPLICATION_PLAN.md](SHARED_APPLICATION_PLAN.md).
The earlier first step bundled RTOS bring-up, the application, and all memory backends;
that was too broad. A small deterministic product scenario and a parallel RTOS-port
feasibility check give earlier evidence with fewer simultaneous unknowns.

## Confidence requires three kinds of execution

| Execution | What it can establish |
| --- | --- |
| Deterministic headless scenarios | Exact state/output transitions, timeouts, limits, errors and repeatable event traces without wall-clock sleeps |
| Interactive host application with the real kernel/tasks | UI integration, actual task and queue paths, device contracts, lifecycle and selected memory backend under the host port |
| Embedded execution and measurements | Physical I/O, interrupt/DMA/cache integration, stack/RAM margins, power states and actual timing |

The FreeRTOS Windows example explicitly limits real-time expectations; matching
kernel/task source does not reproduce MCU scheduling or timing. See its
[official example source](https://github.com/FreeRTOS/FreeRTOS/blob/main/FreeRTOS/Demo/WIN32-MSVC/main.c).

Use the coding article for ownership, typed boundaries and bounded execution.
Apply those ideas with precise C++ contracts: `std::span` carries a size but does
not prove arbitrary indexing, copying or lifetime safe. `[[nodiscard]]` helps
diagnose discarded results but does not prove recovery is correct. The
[C++ Core Guidelines](https://isocpp.github.io/CppCoreGuidelines/CppCoreGuidelines)
and [span element-access contract](https://eel.is/c++draft/span.elem) support keeping
explicit invariants, compiler checks and scenario tests alongside the language tools.
