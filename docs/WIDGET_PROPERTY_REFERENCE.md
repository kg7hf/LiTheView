# Widget property reference

This file is generated from `schema/widgets.json`; do not edit it by hand.

- Catalog: `litheview.widgets.builtin`
- Schema version: `1`
- Compatibility version: `1`
- Canonical SHA-256: `7bba9e94b158a2e9795e3c3da7e353d512fd7458ce86f1c22c9e31d757b6cc5f`

## Animated Image

Caller-clocked sequence of immutable bitmap resources with bounded frame storage.

- ID: `lithe.widget.animated-image`
- Runtime kind/type: `animatedImage` / `lithe::AnimatedImageView`
- XML: `<image>` / `LCDIMAGEVIEW`; selected by `animation-frames`
- Default geometry: `96 by 72` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `animation-frames` | `animationFrames` | `animationImage*` | `null` | - | yes | `spec.animationFrameOffset` |
| `animation-interval` | `duration` | `animationInterval` | `100` | 1 to 65535 | no | `spec.animationIntervalMs` |
| `autoplay` | `boolean` | `autoplay` | `true` | - | no | `spec.animationAutoplay` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `repeat-index` | `unsignedInteger` | `repeatIndex` | `0` | 0 to 65535 | no | `spec.animationRepeatIndex` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `animation-frames`: Required ordered bitmap-name sequence emitted as consecutive animationImageN attributes.
- `animation-interval`: Caller-clocked interval between animation frames.
- `autoplay`: Starts animation when the view is built; elapsed time remains caller-owned.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `repeat-index`: Frame index selected after the last frame, enabling an intro followed by a loop.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `running` - Running
- `visible` - Visible

### Inputs

- `elapsedTime`

### Resources

- `animationFrames` through `animation-frames` (required)

## Button

Focusable button with text, an optional icon, and momentary (non-latching) or toggle (latching) behavior.

- ID: `lithe.widget.button`
- Runtime kind/type: `button` / `lithe::ButtonView`
- XML: `<button>` / `LCDBUTTONVIEW`
- Default geometry: `120 by 36` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `button-behavior` | `enum` | `behavior` | `"momentary"` | - | no | `spec.buttonBehavior` |
| `checked` | `boolean` | `checked` | `false` | - | no | `spec.checked` |
| `detail` | `boundedText` | `detail` | `""` | - | no | `spec.detail` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `icon` | `bitmap` | `icon` | `null` | - | no | `spec.resource` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected-background-color` | `color` | `selectedBackgroundColor` | `null` | - | no | `state.selectedBackground` |
| `selected-foreground-color` | `color` | `selectedForegroundColor` | `null` | - | no | `state.selectedForeground` |
| `text` | `boundedText` | `text` | `""` | - | no | `spec.text` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `button-behavior`: Momentary buttons are non-latching and report one action for each completed activation. Toggle buttons latch the selected state, flip it before reporting each activation, and draw a latch indicator.
- `checked`: Initial Boolean state of a checkbox or toggle button; a momentary button cannot be checked.
- `detail`: Secondary borrowed UTF-8 text displayed by widgets that provide a detail line.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `icon`: Optional borrowed bitmap drawn before the text, or centered when the button has no text or detail. Alpha1 and alpha8 icons are tinted with the resolved state foreground.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected-background-color`: Bounded selected-state background token for a latched toggle button, checked checkbox, or selected radio button. Omitted keeps the normal background; disabled, pressed, and focused states take precedence. Named theme colors may be selected instead of a literal.
- `selected-foreground-color`: Bounded selected-state text, icon, and stroke token. Omitted keeps the normal foreground. Named theme colors may be selected instead of a literal.
- `text`: Borrowed UTF-8 display text whose storage must outlive the built layout.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

- `activate` (activate) - Activate; source `runtime-id`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `pressed` - Pressed
- `selected` - Selected
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `bitmap` through `icon` (optional)
- `font` through `font` (optional)

## Checkbox

Focusable Boolean choice that toggles when activated.

- ID: `lithe.widget.checkbox`
- Runtime kind/type: `checkbox` / `lithe::CheckboxView`
- XML: `<checkbox>` / `LCDCHECKBOXVIEW`
- Default geometry: `140 by 28` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `checked` | `boolean` | `checked` | `false` | - | no | `spec.checked` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected-background-color` | `color` | `selectedBackgroundColor` | `null` | - | no | `state.selectedBackground` |
| `selected-foreground-color` | `color` | `selectedForegroundColor` | `null` | - | no | `state.selectedForeground` |
| `text` | `boundedText` | `text` | `""` | - | no | `spec.text` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `checked`: Initial Boolean state of a checkbox or toggle button; a momentary button cannot be checked.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected-background-color`: Bounded selected-state background token for a latched toggle button, checked checkbox, or selected radio button. Omitted keeps the normal background; disabled, pressed, and focused states take precedence. Named theme colors may be selected instead of a literal.
- `selected-foreground-color`: Bounded selected-state text, icon, and stroke token. Omitted keeps the normal foreground. Named theme colors may be selected instead of a literal.
- `text`: Borrowed UTF-8 display text whose storage must outlive the built layout.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

- `checked-changed` (checked-changed) - Checked Changed; source `runtime-id`

### Bindings

None.

### States

- `checked` - Checked
- `disabled` - Disabled
- `focused` - Focused
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)

## Drop-down

Compact selector with up to 255 immutable pipe-delimited items. Select opens or commits; arrows navigate; Back, focus loss or an outside press cancels. Tap a row to commit; drag the popup to scroll. At most five rows are shown, below or above the control within the root surface.

- ID: `lithe.widget.drop-down`
- Runtime kind/type: `dropDown` / `lithe::DropDownView`
- XML: `<dropdown>` / `LCDDROPDOWNVIEW`
- Default geometry: `160 by 28` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `items` | `boundedText` | `items` | `""` | - | no | `spec.text` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `row-height` | `unsignedInteger` | `rowHeight` | `28` | 12 to 255 | no | `spec.rowHeight` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected-background-color` | `color` | `selectedBackgroundColor` | `null` | - | no | `state.selectedBackground` |
| `selected-foreground-color` | `color` | `selectedForegroundColor` | `null` | - | no | `state.selectedForeground` |
| `selected-index` | `unsignedInteger` | `selectedIndex` | `0` | 0 to 255 | no | `spec.selectedIndex` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |
| `wrap` | `boolean` | `wrap` | `true` | - | no | `spec.wraps` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `items`: Pipe-delimited picker items or tab titles retained in caller-owned immutable storage.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `row-height`: Height of a wheel or drop-down list row in pixels.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected-background-color`: Bounded selected-state background token for a latched toggle button, checked checkbox, or selected radio button. Omitted keeps the normal background; disabled, pressed, and focused states take precedence. Named theme colors may be selected instead of a literal.
- `selected-foreground-color`: Bounded selected-state text, icon, and stroke token. Omitted keeps the normal foreground. Named theme colors may be selected instead of a literal.
- `selected-index`: Initial picker item or tab; the runtime clamps a picker value, and tabs require an existing tab.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.
- `wrap`: Allows left/right selection to wrap between the first and last items or tabs.

### Actions

- `selection-changed` (selection-changed) - Selection Changed; source `runtime-id`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `pressed` - Pressed
- `selected` - Selected
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)

## Icon

Bitmap centered within the assigned frame.

- ID: `lithe.widget.icon`
- Runtime kind/type: `icon` / `lithe::IconView`
- XML: `<icon>` / `LCDICONVIEW`
- Default geometry: `32 by 32` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `bitmap` | `bitmap` | `imageName` | `null` | - | yes | `spec.resource` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `bitmap`: Required bitmap resource name resolved by ResourceProvider during bounded layout construction.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `visible` - Visible

### Inputs

None.

### Resources

- `bitmap` through `bitmap` (required)

## Image

Bitmap rendered at its native size from a caller-owned immutable resource.

- ID: `lithe.widget.image`
- Runtime kind/type: `image` / `lithe::ImageView`
- XML: `<image>` / `LCDIMAGEVIEW`; selected by `bitmap`
- Default geometry: `64 by 64` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `bitmap` | `bitmap` | `imageName` | `null` | - | yes | `spec.resource` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `bitmap`: Required bitmap resource name resolved by ResourceProvider during bounded layout construction.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `visible` - Visible

### Inputs

None.

### Resources

- `bitmap` through `bitmap` (required)

## Label

Static borrowed UTF-8 text rendered with the selected or default font.

- ID: `lithe.widget.label`
- Runtime kind/type: `label` / `lithe::LabelView`
- XML: `<label>` / `LCDLABELVIEW`
- Default geometry: `120 by 24` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text` | `boundedText` | `text` | `""` | - | no | `spec.text` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text`: Borrowed UTF-8 display text whose storage must outlive the built layout.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `visible` - Visible

### Inputs

None.

### Resources

- `font` through `font` (optional)

## Line

Horizontal or vertical rule selected from the frame aspect ratio.

- ID: `lithe.widget.line`
- Runtime kind/type: `line` / `lithe::LineView`
- XML: `<line>` / `LCDLINEVIEW`
- Default geometry: `120 by 1` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `visible` - Visible

### Inputs

None.

### Resources

None.

## Panel

General fixed-capacity container and the canonical root of a designed form.

- ID: `lithe.widget.panel`
- Runtime kind/type: `panel` / `lithe::View`
- XML: `<panel>` / `LCDVIEW`
- Default geometry: `240 by 160` pixels
- Child policy: `many`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `visible` - Visible

### Inputs

None.

### Resources

None.

## Picker

Focus-driven delimiter-backed choice picker without dynamic allocation.

- ID: `lithe.widget.picker`
- Runtime kind/type: `picker` / `lithe::PickerView`
- XML: `<picker>` / `LCDPICKERVIEW`
- Default geometry: `140 by 40` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `items` | `boundedText` | `items` | `""` | - | no | `spec.text` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected-index` | `unsignedInteger` | `selectedIndex` | `0` | 0 to 255 | no | `spec.selectedIndex` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |
| `wrap` | `boolean` | `wrap` | `true` | - | no | `spec.wraps` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `items`: Pipe-delimited picker items or tab titles retained in caller-owned immutable storage.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected-index`: Initial picker item or tab; the runtime clamps a picker value, and tabs require an existing tab.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.
- `wrap`: Allows left/right selection to wrap between the first and last items or tabs.

### Actions

- `selection-changed` (selection-changed) - Selection Changed; source `runtime-id`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)

## Progress

Bounded zero-through-one-hundred progress indicator.

- ID: `lithe.widget.progress`
- Runtime kind/type: `progress` / `lithe::ProgressView`
- XML: `<progress>` / `LCDPROGRESSVIEW`
- Default geometry: `120 by 16` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `value` | `signedInteger` | `value` | `0` | 0 to 100 | no | `spec.value` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `value`: Initial progress value clamped to the inclusive range zero through one hundred.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `visible` - Visible

### Inputs

None.

### Resources

None.

## Radio Button

Focusable radio-style Boolean choice; grouping policy remains application-owned.

- ID: `lithe.widget.radio`
- Runtime kind/type: `radio` / `lithe::RadioButtonView`
- XML: `<radio>` / `LCDRADIOBUTTONVIEW`
- Default geometry: `140 by 28` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected` | `boolean` | `selected` | `false` | - | no | `spec.checked` |
| `selected-background-color` | `color` | `selectedBackgroundColor` | `null` | - | no | `state.selectedBackground` |
| `selected-foreground-color` | `color` | `selectedForegroundColor` | `null` | - | no | `state.selectedForeground` |
| `text` | `boundedText` | `text` | `""` | - | no | `spec.text` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected`: Initial radio-button selection state.
- `selected-background-color`: Bounded selected-state background token for a latched toggle button, checked checkbox, or selected radio button. Omitted keeps the normal background; disabled, pressed, and focused states take precedence. Named theme colors may be selected instead of a literal.
- `selected-foreground-color`: Bounded selected-state text, icon, and stroke token. Omitted keeps the normal foreground. Named theme colors may be selected instead of a literal.
- `text`: Borrowed UTF-8 display text whose storage must outlive the built layout.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

- `selected` (selected) - Selected; source `runtime-id`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `selected` - Selected
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)

## Scroll Panel

Vertically translated child container with bounded key and pointer scrolling.

- ID: `lithe.widget.scroll`
- Runtime kind/type: `scroll` / `lithe::ScrollView`
- XML: `<scroll>` / `LCDSCROLLVIEW`
- Default geometry: `160 by 120` pixels
- Child policy: `many`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `content-height` | `signedInteger` | `contentHeight` | `0` | 0 to 2147483647 | no | `spec.contentHeight` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `scroll-offset` | `signedInteger` | `scrollOffset` | `0` | 0 to 2147483647 | no | `spec.scrollOffset` |
| `scroll-step` | `signedInteger` | `scrollStep` | `32` | 1 to 2147483647 | no | `spec.scrollStep` |
| `shows-indicator` | `boolean` | `showsIndicator` | `true` | - | no | `spec.showsIndicator` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `content-height`: Scrollable content extent; zero allows the runtime to fit content from children.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `scroll-offset`: Initial nonnegative vertical content offset.
- `scroll-step`: Positive vertical distance moved by one key-driven scroll operation.
- `shows-indicator`: Enables the compact draggable scroll indicator.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `dragging` - Dragging Indicator
- `focused` - Focused
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

None.

## Softkey Bar

Two-region softkey bar with independent left and right action IDs.

- ID: `lithe.widget.softkey`
- Runtime kind/type: `softkey` / `lithe::SoftkeyView`
- XML: `<softkey>` / `LCDSOFTKEYVIEW`
- Default geometry: `240 by 36` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `left-action` | `actionId` | `leftAction` | `0` | 0 to 65535 | no | `spec.leftAction` |
| `left-text` | `boundedText` | `left_text` | `""` | - | no | `spec.text` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `right-action` | `actionId` | `rightAction` | `0` | 0 to 65535 | no | `spec.rightAction` |
| `right-text` | `boundedText` | `right_text` | `""` | - | no | `spec.detail` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `left-action`: Action ID emitted when the left softkey is selected.
- `left-text`: Borrowed label for the left softkey region.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `right-action`: Action ID emitted when the right softkey is selected.
- `right-text`: Borrowed label for the right softkey region.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

- `left-activate` (left-activate) - Left Activate; source `left-action`
- `right-activate` (right-activate) - Right Activate; source `right-action`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `left-selected` - Left Selected
- `right-selected` - Right Selected
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)

## Tab Bar

Equal-width strip of up to sixteen tabs. Left/right, select, or a pointer release on a tab changes the selection and reports the action.

- ID: `lithe.widget.tab-bar`
- Runtime kind/type: `tabBar` / `lithe::TabBarView`
- XML: `<tabbar>` / `LCDTABBARVIEW`
- Default geometry: `240 by 32` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `items` | `boundedText` | `items` | `""` | - | no | `spec.text` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected-background-color` | `color` | `selectedBackgroundColor` | `null` | - | no | `state.selectedBackground` |
| `selected-foreground-color` | `color` | `selectedForegroundColor` | `null` | - | no | `state.selectedForeground` |
| `selected-index` | `unsignedInteger` | `selectedIndex` | `0` | 0 to 255 | no | `spec.selectedIndex` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |
| `wrap` | `boolean` | `wrap` | `true` | - | no | `spec.wraps` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `items`: Pipe-delimited picker items or tab titles retained in caller-owned immutable storage.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected-background-color`: Bounded selected-state background token for a latched toggle button, checked checkbox, or selected radio button. Omitted keeps the normal background; disabled, pressed, and focused states take precedence. Named theme colors may be selected instead of a literal.
- `selected-foreground-color`: Bounded selected-state text, icon, and stroke token. Omitted keeps the normal foreground. Named theme colors may be selected instead of a literal.
- `selected-index`: Initial picker item or tab; the runtime clamps a picker value, and tabs require an existing tab.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.
- `wrap`: Allows left/right selection to wrap between the first and last items or tabs.

### Actions

- `selection-changed` (selection-changed) - Selection Changed; source `runtime-id`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `pressed` - Pressed
- `selected` - Selected
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)

## Tab View

Tab strip above one container page per tab; only the selected tab's page is visible and reachable by focus.

- ID: `lithe.widget.tab-view`
- Runtime kind/type: `tabView` / `lithe::TabView`
- XML: `<tabview>` / `LCDTABVIEW`
- Default geometry: `240 by 160` pixels
- Child policy: `many`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `items` | `boundedText` | `items` | `""` | - | no | `spec.text` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected-background-color` | `color` | `selectedBackgroundColor` | `null` | - | no | `state.selectedBackground` |
| `selected-foreground-color` | `color` | `selectedForegroundColor` | `null` | - | no | `state.selectedForeground` |
| `selected-index` | `unsignedInteger` | `selectedIndex` | `0` | 0 to 255 | no | `spec.selectedIndex` |
| `tab-height` | `unsignedInteger` | `tabHeight` | `32` | 8 to 255 | no | `spec.tabHeight` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |
| `wrap` | `boolean` | `wrap` | `true` | - | no | `spec.wraps` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `items`: Pipe-delimited picker items or tab titles retained in caller-owned immutable storage.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected-background-color`: Bounded selected-state background token for a latched toggle button, checked checkbox, or selected radio button. Omitted keeps the normal background; disabled, pressed, and focused states take precedence. Named theme colors may be selected instead of a literal.
- `selected-foreground-color`: Bounded selected-state text, icon, and stroke token. Omitted keeps the normal foreground. Named theme colors may be selected instead of a literal.
- `selected-index`: Initial picker item or tab; the runtime clamps a picker value, and tabs require an existing tab.
- `tab-height`: Height of the tab strip; pages use tab-view coordinates and start at or below it.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.
- `wrap`: Allows left/right selection to wrap between the first and last items or tabs.

### Actions

- `selection-changed` (selection-changed) - Selection Changed; source `runtime-id`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `pressed` - Pressed
- `selected` - Selected
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)

## Table

Scrollable fixed-child container using the same bounded behavior as Scroll Panel.

- ID: `lithe.widget.table`
- Runtime kind/type: `table` / `lithe::TableView`
- XML: `<table>` / `LCDTABLEVIEW`
- Default geometry: `200 by 120` pixels
- Child policy: `many`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `content-height` | `signedInteger` | `contentHeight` | `0` | 0 to 2147483647 | no | `spec.contentHeight` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `scroll-offset` | `signedInteger` | `scrollOffset` | `0` | 0 to 2147483647 | no | `spec.scrollOffset` |
| `scroll-step` | `signedInteger` | `scrollStep` | `32` | 1 to 2147483647 | no | `spec.scrollStep` |
| `shows-indicator` | `boolean` | `showsIndicator` | `true` | - | no | `spec.showsIndicator` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `content-height`: Scrollable content extent; zero allows the runtime to fit content from children.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `scroll-offset`: Initial nonnegative vertical content offset.
- `scroll-step`: Positive vertical distance moved by one key-driven scroll operation.
- `shows-indicator`: Enables the compact draggable scroll indicator.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

None.

### States

- `disabled` - Disabled
- `dragging` - Dragging Indicator
- `focused` - Focused
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

None.

## Text Field

Bounded UTF-8 editor over an application-owned mutable text buffer.

- ID: `lithe.widget.text-field`
- Runtime kind/type: `textField` / `lithe::TextFieldView`
- XML: `<textfield>` / `LCDTEXTFIELDVIEW`
- Default geometry: `160 by 32` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `binding` | `bindingId` | `binding` | `null` | - | yes | `spec.resource` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `max-length` | `unsignedInteger` | `maxLength` | `null` | 1 to 65535 | no | `spec.maxLength` |
| `placeholder` | `boundedText` | `placeholder` | `""` | - | no | `spec.detail` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `text-alignment-flags` | `unsignedInteger` | `textAlignmentFlags` | `12` | 0 to 15 | no | `spec.style.textAlign` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `binding`: Required application-owned mutable text-buffer binding resolved by ResourceProvider.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `max-length`: Optional UTF-8 byte limit, additionally clamped to the caller-owned buffer capacity minus one.
- `placeholder`: Borrowed text displayed while the bound text buffer is empty.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `text-alignment-flags`: Legacy four-bit horizontal and vertical text-alignment encoding.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.

### Actions

None.

### Bindings

- `text` - Text; `binding`, twoWay

### States

- `disabled` - Disabled
- `empty` - Empty
- `focused` - Focused
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`
- `textInput`

### Resources

- `font` through `font` (optional)
- `mutableTextBuffer` through `binding` (required)

## Wheel Picker

Vertical scroll wheel of up to 255 items with the selection centered in a band. Select or encoder push starts and ends editing; while editing, up/down or the encoder turn it and back restores the starting value. A drag scrolls and snaps; a tap selects the row under it.

- ID: `lithe.widget.wheel-picker`
- Runtime kind/type: `wheelPicker` / `lithe::WheelPickerView`
- XML: `<wheelpicker>` / `LCDWHEELPICKERVIEW`
- Default geometry: `120 by 140` pixels
- Child policy: `none`

| Property | Kind | XML | Default | Range | Required | C++ member |
| --- | --- | --- | --- | --- | --- | --- |
| `accent-color` | `color` | `accentColor` | `"#2D6CDFFF"` | - | no | `spec.style.accent` |
| `background-color` | `color` | `backgroundColor` | `"#00000000"` | - | no | `spec.style.background` |
| `border-color` | `color` | `borderColor` | `"#00000000"` | - | no | `spec.style.border` |
| `border-width` | `unsignedInteger` | `borderWidth` | `0` | 0 to 16 | no | `spec.style.borderWidth` |
| `disabled-background-color` | `color` | `disabledBackgroundColor` | `"#00DCD4D0"` | - | no | `state.disabledBackground` |
| `disabled-foreground-color` | `color` | `disabledForegroundColor` | `"#00786F6A"` | - | no | `state.disabledForeground` |
| `enabled` | `boolean` | `enabled` | `true` | - | no | `spec.enabled` |
| `focus-policy` | `enum` | `canFocus` | `"auto"` | - | no | `spec.focusable` |
| `focused-background-color` | `color` | `focusedBackgroundColor` | `"#00DF6C2D"` | - | no | `state.focusedBackground` |
| `focused-foreground-color` | `color` | `focusedForegroundColor` | `"#00FFFFFF"` | - | no | `state.focusedForeground` |
| `font` | `font` | `font` | `""` | - | no | `spec.font` |
| `foreground-color` | `color` | `textColor` | `"#FFFFFFFF"` | - | no | `spec.style.foreground` |
| `frame` | `rectangle` | `frame` | `[0, 0, 1, 1]` | - | yes | `spec.frame` |
| `items` | `boundedText` | `items` | `""` | - | no | `spec.text` |
| `pressed-background-color` | `color` | `pressedBackgroundColor` | `"#00B35724"` | - | no | `state.pressedBackground` |
| `pressed-foreground-color` | `color` | `pressedForegroundColor` | `"#00FFFFFF"` | - | no | `state.pressedForeground` |
| `row-height` | `unsignedInteger` | `rowHeight` | `28` | 12 to 255 | no | `spec.rowHeight` |
| `runtime-id` | `unsignedInteger` | `runtimeID` | `0` | 0 to 65535 | no | `spec.id` |
| `selected-background-color` | `color` | `selectedBackgroundColor` | `null` | - | no | `state.selectedBackground` |
| `selected-foreground-color` | `color` | `selectedForegroundColor` | `null` | - | no | `state.selectedForeground` |
| `selected-index` | `unsignedInteger` | `selectedIndex` | `0` | 0 to 255 | no | `spec.selectedIndex` |
| `text-scale` | `unsignedInteger` | `textScale` | `1` | 1 to 8 | no | `spec.style.textScale` |
| `visible` | `boolean` | `hide` | `true` | - | no | `spec.visible` |
| `wrap` | `boolean` | `wrap` | `true` | - | no | `spec.wraps` |

### Property details

- `accent-color`: RGBA accent color used for selection, progress, focus, and active state.
- `background-color`: RGBA background color; a transparent value leaves the existing surface visible.
- `border-color`: RGBA border color; transparent disables a visible border even when width is nonzero.
- `border-width`: Border width in target pixels; zero disables border drawing.
- `disabled-background-color`: Bounded disabled-state background token. Named theme colors may be selected instead of a literal.
- `disabled-foreground-color`: Bounded disabled-state text and stroke token. Named theme colors may be selected instead of a literal.
- `enabled`: Initial interaction state; disabled views remain renderable but cannot receive focus.
- `focus-policy`: Auto uses the widget default; true or false emits an explicit canFocus override.
- `focused-background-color`: Bounded focused-state background token. Named theme colors may be selected instead of a literal.
- `focused-foreground-color`: Bounded focused-state text and stroke token. Named theme colors may be selected instead of a literal.
- `font`: Optional font resource name; an empty value selects the caller-provided default font.
- `foreground-color`: RGBA foreground color used for text and primary strokes.
- `frame`: Integer x, y, width, and height relative to the parent; width and height are positive.
- `items`: Pipe-delimited picker items or tab titles retained in caller-owned immutable storage.
- `pressed-background-color`: Bounded pointer-pressed background token. Named theme colors may be selected instead of a literal.
- `pressed-foreground-color`: Bounded pointer-pressed text and stroke token. Named theme colors may be selected instead of a literal.
- `row-height`: Height of a wheel or drop-down list row in pixels.
- `runtime-id`: Optional 16-bit runtime view and default action identifier; zero means unassigned.
- `selected-background-color`: Bounded selected-state background token for a latched toggle button, checked checkbox, or selected radio button. Omitted keeps the normal background; disabled, pressed, and focused states take precedence. Named theme colors may be selected instead of a literal.
- `selected-foreground-color`: Bounded selected-state text, icon, and stroke token. Omitted keeps the normal foreground. Named theme colors may be selected instead of a literal.
- `selected-index`: Initial picker item or tab; the runtime clamps a picker value, and tabs require an existing tab.
- `text-scale`: Integer nearest-neighbor scale applied by the selected font backend.
- `visible`: Initial visibility. XML stores the inverse historical hide flag.
- `wrap`: Allows left/right selection to wrap between the first and last items or tabs.

### Actions

- `selection-changed` (selection-changed) - Selection Changed; source `runtime-id`

### Bindings

None.

### States

- `disabled` - Disabled
- `focused` - Focused
- `pressed` - Pressed
- `selected` - Selected
- `visible` - Visible

### Inputs

- `keys`
- `pointerInput`

### Resources

- `font` through `font` (optional)
