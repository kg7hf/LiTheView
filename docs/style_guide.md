# LitheView style guide

This document defines the house style for LitheView. It applies to the portable library, tests,
examples, tools, generated code owned by this project, and platform adapters unless a documented
external interface requires another convention.

LitheView follows the [C++ Core Guidelines](https://github.com/isocpp/CppCoreGuidelines) as its
general engineering standard and adds stricter rules for small, deterministic embedded systems.
Where the Core Guidelines offer an aesthetic default rather than a safety rule, this document is the
project-specific decision.

The words **must**, **should**, and **may** describe required, preferred, and optional practices.

## Priorities

Code is evaluated in this order:

1. Correctness and defined behavior.
2. Safety, bounded resource use, and explicit ownership.
3. Deterministic behavior suitable for embedded targets.
4. Clear interfaces and maintainability.
5. Measured performance, memory use, and power consumption.
6. Consistent presentation.

Style must never conceal a correctness or safety problem. Performance exceptions require evidence
from the affected target, not an assumption that lower-level code is faster.

## Copyright and license headers

First-party source, headers, scripts and build files must start with a short SPDX notice in the
language's comment syntax. Keep a required shebang first. For a new file:

```cpp
// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
```

Use the actual creation/publication years. The extracted library retains the project's 2012-2026
range; new designer and tooling files use 2026. Keep the full GPL text in the root `LICENSE`.
Do not add a GPL runtime exception or change the identifier to `GPL-3.0-or-later` without an explicit
license decision by the owner.

Preserve vendor and third-party attribution and their separate licenses. DejaVu-derived font tables
are not Paul R. Decker's original glyphs. Byte-for-byte generated form fixtures and hash-locked asset
data keep their generator output and existing repository/resource license records. The library's
own generated widget metadata includes the notice in its generator so regeneration retains it.

A developer's forms, handler bodies and imported assets do not become Paul R. Decker's work merely
because the designer processes them. Do not hardcode his copyright into general form or resource
exporters. Shipped first-party templates retain notices for the template code; those notices do not
claim ownership of a developer's additions.

## Language and portability

- LitheView targets ISO C++23.
- The portable library must not depend on an operating system, RTOS, filesystem, device driver,
  global clock, or global allocator.
- The portable library is built without exceptions and RTTI. APIs must remain useful under those
  constraints.
- Required language and library features must be supported by the selected Win32, Linux, Cortex-M7,
  and Cortex-M33 toolchains before they are admitted to the supported subset.
- Compiler extensions must not appear in portable code. A required extension belongs in a narrowly
  scoped adapter and must have a documented reason.
- New and substantially changed code must follow this guide. Existing extracted code that does not
  comply is migration debt, not precedent for new code.

The build baseline is C++23. A new C++23 facility may be used only after the selected host and embedded
toolchains are shown to support it under LitheView's no-exception, no-RTTI configuration.

## Naming

Names must communicate intent and use a single convention throughout first-party C++ code.

| Entity | Convention | Example |
| --- | --- | --- |
| Namespace | lowercase | `lithe` |
| Class, struct, enum, and concept | `PascalCase` | `ScrollView`, `PixelSink` |
| Type alias | `PascalCase` | `ViewId`, `FrameIndex` |
| Function and method | `lowerCamelCase` | `setScrollPosition()` |
| Local variable and parameter | `lowerCamelCase` | `scrollPosition` |
| Private or protected data member | `mLowerCamelCase` | `mScrollPosition` |
| Static data member | `sLowerCamelCase` | `sInstanceCount` |
| Constant | `kPascalCase` | `kMaxChildren` |
| Enumerator | `lowerCamelCase` | `PointerPhase::pressed` |
| Template parameter | descriptive `PascalCase` | `PixelType`, `Limits` |
| Source or header filename | `snake_case` | `scroll_view.cpp` |
| CMake target | `LitheView::PascalCase` | `LitheView::Core` |
| CMake variable or option | `LITHEVIEW_UPPER_SNAKE_CASE` | `LITHEVIEW_BUILD_TESTS` |

### Underscores

- An identifier must not begin or end with an underscore.
- Do not use forms such as `_value`, `value_`, or `m_value`.
- Double underscores are prohibited.
- Internal underscores are reserved for filenames, CMake names, and external interfaces whose
  spelling LitheView does not control.
- C++ identifiers use the camel-case conventions in the table above.

Leading underscores and some double-underscore forms are reserved to the implementation. LitheView
avoids all leading and trailing underscores as a simpler project-wide rule.

### Acronyms and abbreviations

- Treat an acronym as a word: `XmlParser`, `Rgb565Surface`, `lcdWidth`, and `parseXml()`.
- Use an abbreviation only when it is substantially clearer than its expansion in this domain.
- Keep the same spelling everywhere. Do not mix `ID`, `Id`, and `id`; use `ViewId` and `viewId`.
- A name's detail should be proportional to its scope. A tiny loop index may be `i`; persistent state
  needs a descriptive name.
- Do not encode the type in a name. Prefer `frameCount` to `u32FrameCount`.

### Files and compatibility formats

- Public headers will live under `include/lithe/` after the compatibility migration.
- The public namespace will be `lithe`.
- Historical XML element names, attributes, resource identifiers, vendor symbols, and wire-format
  fields retain their defined spelling. They are compatibility data, not C++ naming precedents.
- Public-name migrations must be staged when compatibility matters; a mechanical rename must not
  silently break an established package consumer.

## Preprocessor policy

LitheView-owned C++ design must not use preprocessor macros when the language can express the idea.
In particular, do not use macros for constants, functions, type selection, control flow, registration,
string construction, assertions, or first-party feature configuration.

Use the appropriate C++23 facility instead:

| Do not use a macro for | Use instead |
| --- | --- |
| Constant | `inline constexpr` typed value |
| Function-like operation | Function, lambda, or constrained function template |
| Set of choices | `enum class` |
| Compile-time branch | `if constexpr` |
| Compile-time validation | `static_assert`, concept, or `consteval` function |
| Array and its length | `std::array` or `std::span` |
| Configuration bundle | Typed configuration object or a focused template parameter |
| Conditional implementation | Separate translation unit, adapter, or build-selected source |
| Header inclusion guard | `#pragma once` in first-party headers |

For example:

```cpp
namespace lithe::config
{

inline constexpr std::size_t kMaxChildren {16};
inline constexpr std::size_t kMaxDamageRegions {8};

} // namespace lithe::config
```

Capacities that affect object layout may be represented by a typed limits object or a focused template.
Do not make a value a template parameter unless it provides a meaningful compile-time, storage, or
type-safety benefit. Unnecessary template configuration can increase code size and make embedded
diagnostics harder to understand.

### Unavoidable boundary macros

Some C and operating-system interfaces require macros as part of their contract. Examples include
STM32 HAL/BSP configuration symbols, compiler-provided target detection, and Windows header controls.
These are permitted only when all of the following are true:

1. The spelling or preprocessor behavior is required by an interface LitheView does not control.
2. The macro is confined to a platform adapter, vendor configuration header, or private build
   definition.
3. It does not appear in LitheView's portable public API.
4. It uses the exact external spelling, or a unique `LITHEVIEW_` name if LitheView must define it.
5. Its purpose and owner are apparent at the definition site.

Boundary macros form a quarantine zone, not an exception that permits macro-based library design.
CMake variables are not C++ preprocessor macros, but they still follow the naming convention above.

## Interfaces and ownership

- Make ownership explicit in the type or interface contract.
- Prefer values and references. Use a pointer when `nullptr` is a meaningful state or when required by
  an external interface.
- Do not use owning raw pointers.
- Use `std::span` for borrowed contiguous sequences and `std::string_view` for borrowed text when the
  lifetime contract is clear.
- Use `std::span<const std::byte>` for opaque immutable binary resources. Do not erase their bounds
  behind `const void*`.
- Do not use a function pointer plus `void*` context for an internal C++ callback. Bind behavior and
  object through a borrowed typed interface with `noexcept` methods, or a compile-time template.
  Do not replace that pair with an allocating `std::function` on the portable path.
- Confine unavoidable `void*` to a genuine C boundary such as an RTOS, HAL, Win32 callback, or frozen
  C ABI. Convert once in its small adapter and pass typed references or bounded spans behind it.
  A convenience helper inside that adapter should retain the type and bounds until the C call.
- A required, non-reseatable object relationship is a reference. A required relationship that must be
  reseated uses `std::reference_wrapper`; do not use a raw pointer merely to obtain assignment.
- A non-owning view must not outlive its backing storage. Document any lifetime that is not evident
  from the declaration.
- Avoid writable output parameters. Return a value or a small result structure when practical.
- Use strong types or scoped enumerations when primitive values could be confused.
- Keep public interfaces smaller and more stable than their implementations.
- Do not expose Win32, STM32, FreeRTOS, or application types through portable headers.
- Header files must be self-contained and include what they use.

## Memory and resource use

- The portable rendering and interaction path must not allocate dynamically.
- Storage must be caller-owned or have an explicit fixed capacity.
- Every capacity limit must have a named, typed value and a defined failure behavior.
- Capacity exhaustion and malformed input must fail closed without corrupting existing state.
- Avoid recursion unless its maximum depth is statically bounded and justified. XML and view-tree
  depth must have explicit ceilings.
- Use RAII for resources even where exceptions are disabled.
- Avoid non-constant global variables. Shared mutable state requires explicit ownership and
  synchronization.
- Large immutable graphics resources should be eligible for placement in ROM or target-specific
  memory sections without changing the portable interface.
- Do not trade deterministic memory use for convenience without an explicit architectural decision.

## Error handling

- Do not use exceptions in LitheView library or adapter code.
- Expected failure must be represented explicitly with a status, a result type, or `std::expected`
  when supported by every selected standard library.
- Do not ignore a `[[nodiscard]]` result without a documented reason.
- Assertions express programmer errors and invariants; they are not a substitute for validating XML,
  resources, touch coordinates, buffer sizes, or other external input.
- An error path must leave the object in a valid, documented state.
- Never continue after detecting memory corruption, an impossible geometry, or a violated invariant
  when safe behavior cannot be guaranteed.
- Platform applications own their watchdog, diagnostic, and safe-state policy. The portable library
  reports failures but does not invent product policy.

## Types, arithmetic, and conversions

- Use fixed-width integer types for serialized data, pixels, hardware-facing values, and quantities
  whose width is part of the contract.
- Use a natural semantic type for ordinary computation. Do not choose unsigned arithmetic merely to
  prohibit negative values.
- Avoid implicit narrowing, mixed-sign arithmetic, and unchecked multiplication used for buffer sizes.
- Enable `-Wdouble-promotion` in GCC and Clang builds so implicit promotion from `float` to `double`
  is diagnosed. This warning does not prohibit an intentional, explicit `double`; any such use needs
  target measurements and a documented reason.
- Make unit and coordinate-space changes explicit. Screen, local-view, framebuffer, and touch
  coordinates must not be confused.
- Prefer `std::array`, `std::span`, and range-based iteration to C arrays and pointer arithmetic.
- Treat every cast, including C++ named casts and `std::bit_cast`, as a boundary requiring proof.
  Trace the value from producer to consumer and make internal types agree instead of converting at
  successive layers. Approved casts must be external ABI, serialized/memory representation, or a
  range-checked wide-arithmetic-to-storage boundary. They carry a local `LITHEVIEW_CAST_BOUNDARY`
  justification and pass the automated cast audit. C-style casts are prohibited.
- Avoid `reinterpret_cast`. Isolate a required cast at an external hardware or operating-system
  boundary and explain the representation contract.
- Initialize every object at its declaration, normally with braces.
- Prefer immutable state. A value should be `const` or `constexpr` unless it must change.
- Do not use magic numbers. Name values that encode a protocol, color, timing, dimension, limit, or
  hardware behavior.

## Classes and functions

- A class must have a clear invariant and a single responsibility.
- Use `struct` for passive aggregates without hidden invariants; use `class` when invariants or
  encapsulation matter.
- Apply the rule of zero. Explicitly delete unsupported copy or move operations.
- Mark single-argument constructors `explicit` unless implicit conversion is intentionally part of the
  type's meaning.
- Mark overriding functions with `override`; use `final` where further derivation is not designed.
- Functions should perform one logical operation and remain short enough to understand locally.
- Declare a function `noexcept` when it cannot throw under its complete implementation contract.
- Prefer compile-time polymorphism for small closed policies and runtime interfaces where target or
  application substitution is a real requirement. Measure code-size effects rather than assuming one
  is cheaper.
- Keep constructors free of externally visible work. Starting tasks, opening files, or touching
  hardware belongs in explicit adapter operations.

Class declarations use this order:

1. Public types and constants.
2. Constructors, destructor, and assignment operations.
3. Public operations.
4. Protected operations.
5. Private types, operations, and data.

## Concurrency and real-time behavior

- LitheView owns no thread or task. The application decides where input, model updates, animation
  advancement, and rendering run.
- Shared mutable model data must have an explicit synchronization policy.
- Do not block, sleep, wait on I/O, or acquire an unbounded lock in rendering or input dispatch.
- Interrupt handlers must not call the view system directly. Convert interrupt activity into a bounded
  application event.
- Avoid hidden polling. Rendering should be driven by invalidation; animation should be driven by an
  application-owned clock.
- State timing and stack assumptions for code that runs in a real-time task.

## Rendering, performance, and power

- Invalidate the smallest correct region. A local visual change must not become a full-screen redraw
  without a correctness requirement.
- Avoid floating-point work in inner pixel loops unless target measurements support it.
- Prefer algorithms whose worst-case work is bounded by declared capacities.
- Keep color conversion and hardware acceleration behind display or painter boundaries.
- A fast path must produce the same observable rendering result as the portable reference path unless
  a documented quality mode says otherwise.
- Optimize only from measurements on representative hardware. Record flash, RAM, stack, frame time,
  transfer size, and wakeup implications where relevant.
- An idle interface must not redraw or wake periodically unless an active animation or product policy
  requires it.

## Formatting

- Use four spaces for indentation and no tabs.
- Use Allman braces. Put the opening brace on its own line for namespaces, types, functions, lambdas,
  and control-flow statements. Put `else` and `catch` on a new line after the preceding closing brace;
  keep the terminating `while` of a `do` statement with that closing brace.
- Braced initialization is not a scope brace and remains with the declaration or expression, as in
  `Rect frame {};` and `Point {x, y}`.
- Keep one statement and one declaration per line.
- Prefer lines no longer than 100 characters; exceed that limit only when breaking the line would be
  less readable.
- Put spaces around binary operators and after commas. Do not add spaces just inside parentheses.
- Use braces for control-flow bodies, including a single statement.
- Put `public`, `protected`, and `private` labels at class indentation and indent their contents once.
- Place pointer and reference symbols with the type: `View* view` and `const Rect& region`.
- Use `//` for short comments. Public API documentation may use the project's selected documentation
  comment format once one is standardized.
- Let the configured formatter decide mechanical layout; do not hand-format code against it.

Example:

```cpp
class ScrollView final : public View
{
public:
    explicit ScrollView(Rect frame = {}) noexcept;

    void setScrollPosition(Point position) noexcept;
    Point scrollPosition() const noexcept;

private:
    Point mScrollPosition {};
    Rect mViewport {};
    bool mDraggingScrollBar {false};
};
```

## Comments and documentation

- Prefer code that states what it does directly.
- Comments explain intent, invariants, units, ownership, hardware constraints, or a non-obvious reason.
  They must not merely translate the next statement into English.
- A safety, analysis, or linter suppression must be narrow and include a concrete justification.
- `TODO` comments must identify a bounded action and, when the project adopts issue tracking, its owner
  or issue number.
- Public behavior, lifetime requirements, capacity limits, thread-safety, and failure behavior belong
  in the API contract.
- Keep examples buildable. Documentation snippets that define supported usage should be compiled or
  tested where practical.

## Tool enforcement

The intended automated checks are:

- compiler warnings treated as errors for first-party code;
- `-Wdouble-promotion` on GCC and Clang first-party targets;
- `clang-format` for layout;
- `clang-tidy` with C++ Core Guidelines, bug-prone, performance, readability, and project naming
  checks;
- Cppcheck as an independent static-analysis pass;
- host unit tests, installed-package consumer tests, and embedded cross-builds;
- target-specific flash, RAM, stack, timing, and rendering validation where a claim depends on
  hardware.

Generated and vendor code should be excluded from first-party style diagnostics, not rewritten to look
like LitheView code. Adapter code remains first-party and is checked except for narrowly suppressed
external-interface requirements.

No new warning or analyzer suppression may be added without an adjacent explanation. The long-term
goal is zero unexplained first-party diagnostics, reached through risk-ordered modernization rather
than broad suppression.

## Review checklist

Before accepting a change, confirm that:

- names follow this guide and have no leading or trailing underscores;
- no replaceable first-party macro was introduced;
- ownership, lifetime, bounds, units, and failure behavior are explicit;
- all input and capacity boundaries are validated;
- portable headers remain platform-independent;
- rendering invalidation is no larger than correctness requires;
- memory, timing, or power claims are supported by measurements;
- relevant host tests and embedded builds pass;
- compatibility changes are documented and intentionally staged;
- any external-interface exception is isolated and justified.
