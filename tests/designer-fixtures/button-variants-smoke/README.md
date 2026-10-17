# Button variants fixture

This checked designer project demonstrates the Phase 5.7 button slice on the 480 by 272 H750
RGB565 surface. One form contains each button variant described in [BUTTONS.md](../../../docs/BUTTONS.md):

| Component | Variant | Runtime ID |
| --- | --- | ---: |
| `startButton` | Text momentary button with detail text `F1` | 10 |
| `homeButton` | Icon-only momentary button | 11 |
| `muteToggle` | Icon-and-text toggle, initially released | 13 |
| `alarmToggle` | Text toggle, initially latched | 12 |
| `serviceButton` | Disabled text button | none |

Every button uses theme tokens for its normal, focused, pressed, and disabled colors. The two
toggles also use `theme.selected` and `theme.on-selected`. The form XML is the editable artifact;
`generated/button_variants.hpp` and `generated/button_variants.cpp` are deterministic exports that
must stay fresh.

## Validate and export

From the repository root, using the .NET 10 SDK selected by the VS Code designer tasks:

```powershell
$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form validate `
    tests/designer-fixtures/button-variants-smoke/forms/main.litheview.xml `
    schema/widgets.json

& $Dotnet run --project tools/designer/src/LitheView.Designer.Cli -- `
    form export-cpp `
    tests/designer-fixtures/button-variants-smoke/forms/main.litheview.xml `
    schema/widgets.json `
    tests/designer-fixtures/button-variants-smoke/generated/button_variants.hpp `
    tests/designer-fixtures/button-variants-smoke/generated/button_variants.cpp
```

The designer's freshness test and the host equivalence test fail if the form and generated files
drift apart.

## Supplying the icon

The form declares one logical bitmap, `home-icon`. Like every LitheView resource, the application
owns its pixels and returns them from its `ResourceProvider`:

```cpp
class Resources final : public lithe::ResourceProvider
{
public:
    const lithe::Bitmap* bitmap(std::string_view name) const noexcept override
    {
        return name == "home-icon" ? &kHomeIcon : nullptr;
    }
};

// Form::Layout builds only the widget kinds this form uses.
lithe::generated::ButtonVariants::Layout layout{};
const lithe::LayoutResult built =
    layout.build(lithe::generated::ButtonVariants::views(), lithe::font5x7(), &resources);
```

A missing icon makes `build()` fail with `LayoutError::missingResource` at the button's index. In a
product, import the icon with **Resources > Import Icon**; the import adds the manifest, generated
C++, and the form resource. The host test supplies a 16 by 16 alpha1 house so that the alpha icon
is tinted with each button's resolved foreground.

## Input

With the controller active, `Key::down` or one encoder step moves focus through START, the home
icon, MUTE, and ALARM; SERVICE is disabled and skipped. `Key::select`, the encoder push, or a
pointer release inside a toggle flips it before `onAction()` receives runtime ID 12 or 13. Releasing
outside a pressed button cancels the activation.

This fixture is host and cross-build evidence only. It does not select a target, storage section,
or board, and no hardware is required.
