# Extraction boundary

The extraction preserves the original system's portable ideas while replacing its product and platform
coupling with narrow interfaces.

| Historical area | LitheView area | Decision |
| --- | --- | --- |
| `Frameworks/LCDViewGUI/LCDViewGraphics` | `Surface`, `DamageTracker`, `Painter` | Preserve RGB565 framebuffer drawing and dirty presentation behind a caller-owned display. |
| `Frameworks/LCDViewGUI/LCDViews` | Generic view, label, button, progress, image/icon/animation, line, scroll/table, picker, and softkey widgets | Preserve the retained relative-frame tree, focus/scroll behavior, and frame-sequence animations with bounded storage. |
| `Frameworks/LCDViewGUI/LCDViewController` | `ViewController`, `ControllerDelegate`, `Application` | Preserve focus/action routing and stacked navigation; modernize pointer input with capture and lifecycle events. |
| `Frameworks/LCDViewGUI/LCDResources` | `XmlLayoutParser`, `XmlLayoutWriter`, `ResourceProvider` | Preserve XML as a core capability, but accept caller-provided memory and generated resource tables. |
| `Feature/Resources/XML` | legacy-compatible XML object dialect | Demonstrate the existing `className`, `frame`, `runtimeID`, color, text, and focus conventions. |
| `HAL/LCDController` | application-defined `Display` or `TileSink` | Replace hardware knowledge with a writable RGB565 surface/dirty presentation or a constrained tile transfer. |
| Win32 simulator | `tests/rendering/win32` | Keep the platform at the edge: Win32 window/input/presentation only. |
| Embedded OS/HAL | H750 starter and tutorial applications | Each consuming application owns its pinned RTOS and STM32 drivers. The initial showcase application is retired. |
| Small Cortex-M bring-up | `tests/rendering/stm32u545re-q` | Cross-build the real demo and render it in strips on NUCLEO-U545RE-Q without inventing display hardware. |

## Deliberately excluded

- `Feature/Settings` and all device/domain settings controllers.
- Filesystem paths, language directories, caches, settings stores, and persistence.
- The LiTheView message/task framework, timers, rules-engine context, error system, and manufacturer settings.
- Product-specific widgets such as dose, battery, tagging, and date/time views.
- LPC1850 and Win32 LCD driver implementations.
- Historical resource binary formats and generated product data.

These exclusions are ownership boundaries, not lost extension points. An application can implement model
persistence, event delivery, generated resources, scheduling, or specialized widgets around the library.

## Architectural contract

```text
caller model/settings             caller XML bytes + generated resources
          |                                      |
          v                                      v
 ControllerDelegate <--- ViewController <--- XmlLayoutParser / StaticLayout
          |                    |                 |
          +------------------> View tree ------> Painter
                                                   |
                                                   v
                                        caller-owned RGB565 Display
```

The arrows end at interfaces owned by the caller. LitheView never opens a file, starts a thread,
reads a setting, or initializes a board peripheral.

## Compatibility intent

The parser accepts a substantial generic subset of the original XML dialect, including the original
`animationImageN` sequence form, but not every historical product class. It
ignores unknown attributes so product metadata does not prevent a layout from loading, while unknown
element and class names fail closed. This makes migration incremental: introduce generic widgets first,
then add explicitly designed portable widget types where real layouts require them.
