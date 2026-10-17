# Designer service protocol, version 1

This process reuses `LitheView.Designer.Core` for the VS Code designer. It has no
Windows Forms dependency and does not build firmware, run a simulator, select a
device, or rewrite existing source forms. Project creation publishes new source
files only after validation. The host IDE owns documents, undo/redo, saves,
tasks, diagnostics, and debugger integration.

```powershell
$repository = (Resolve-Path .).Path # Run from the LiTheView checkout root.
$worker = (Resolve-Path build/windows-mingw-debug/litheview_designer_preview_worker.exe).Path
dotnet tools/designer/src/LitheView.Designer.Service/bin/Debug/net10.0/LitheView.Designer.Service.dll `
  --library-root $repository --preview-worker $worker
```

Both startup paths must be absolute. The preview worker is optional; inspect,
edit, and export work without it. The service checks its compiled widget-catalog
identity against the library, and checks the native worker's catalog on render.
On Windows the worker writes each frame into a designer-owned named mapping; on Linux and
macOS, which have no named shared memory, it returns the frame inline after its render response
(the `host-debug` CMake preset builds it there).

Stdin and stdout carry UTF-8 newline-delimited JSON. Stdout is protocol-only;
startup failures go to stderr and return a nonzero exit code. Each request gets
one response before the next request is processed. Closing stdin disposes the
native preview worker and ends the service; a final unterminated line is handled
before exit. The host must terminate the process tree if it forcibly stops an
unresponsive service.

Request envelope (the absolute paths below are illustrative protocol data, not installation requirements):

```json
{
  "protocolVersion": 1,
  "id": 12,
  "method": "inspect",
  "params": {
    "manifestPath": "D:/app/Tasks/UI/project.litheview.json",
    "formPath": "D:/app/Tasks/UI/forms/main.litheview.xml",
    "xml": "<panel ... />",
    "documentVersion": 7
  }
}
```

Response envelope:

```json
{"protocolVersion":1,"id":12,"ok":true,"result":{}}
{"protocolVersion":1,"id":12,"ok":false,"error":{"code":"InvalidForm","message":"..."}}
```

An unparseable or oversized request has response ID zero. Request IDs must be
nonnegative JavaScript-safe integers. Unknown fields and methods are rejected.
The hello request uses only `params: {"protocolVersion":1}` and returns the
protocol version, service version, widget catalog hash, and available methods.

`project.options` returns available RGB display profiles and logical input choices.
`project.create` accepts `params.project` with absolute `applicationRoot` and
`projectDirectory`, `displayName`, `projectId`, `logicalInput`, application-relative
`componentDirectory` and `buildDirectory`, and exactly one of `surfaceId` or a
complete `customSurface` profile. It validates paths and snapshots through the
shared core before publishing new source files and a blank root panel. The response
returns `manifestPath`, `formPath`, resolved paths and diagnostics. Creation does not
export code; the developer edits, saves and explicitly requests `export`. Existing
source files are never replaced, and export retains its separate ownership checks.
These project methods do not require an existing document. The remaining methods
require the current form XML and document version. The
form's absolute path must occur in the supplied manifest; it cannot select an
arbitrary file. Project context is resolved without reading on-disk form XML, so
an unsaved correction remains usable even if the disk copy is invalid. Manifest,
locked display/input snapshots, and resources are still read from the project.

| Method | Result and behavior |
|---|---|
| `inspect` | `documentVersion`, `components`, `widgets`, `properties`, `choices`, `surface`, `diagnostics`. Invalid XML returns empty component and choice lists plus diagnostics. |
| `edit` | Same view model plus canonical `xml` and optional `selectedComponentId`; never writes the form. |
| `render` | `documentVersion`, `width`, `height`, `rgbaBase64`, native `status`, `diagnostics`. The frame is rendered by the C++ worker in the selected native format. RGBA conversion is only for presentation. |
| `export` | `documentVersion`, `outputDirectory`, `buildDirectory`, `applicationRoot`, `libraryRoot`, generated `manifestPath`, `diagnostics`. Requires exact text equality with the saved form and honors the project paths sidecar. |
| `project.input` | `documentVersion`, `currentInput`, `edits`. A read-only input edit proposal for version-2/3 projects; the VS Code host applies and saves it. |
| `project.setup` | `documentVersion`, `snapshot`, `documents`, `edits`, `displaySurface` (RGB only), `displayDefinition`, `input`. Read current configuration or propose display/input edits. |

Service 0.2.7 advertises `project.setup` for the **Configure** screen. Without
`params.setup`, it returns the current canonical profiles, a configuration snapshot
token, and the complete source-document read set (`{path,before}`). Submit
`setup: {snapshot, customSurface?, customNativeDisplay?, input?}` to propose edits.
`customSurface` uses the RGB surface schema; `customNativeDisplay` uses the same
typed geometry/format/reference request as creation. `input` contains the five
Boolean capabilities: `pointer`, `keys`, `text`, `encoder`, `select`.

The service validates saved locks and the original snapshot before proposing any
changes. Profile identities remain stable. Unchanged settings yield no edits;
input-only changes leave display files intact. The read set includes unchanged
profiles so concurrent external changes cannot be overwritten by an older dialog.
The host checks contained paths, disk text and clean editor versions, applies one
workspace edit and saves profiles before the manifest. A partial save leaves
recoverable documents and generation rejects inconsistent locks.

Forms and handlers are never rewritten by Configure. A changed extent requires
the developer to resize the root and adjust layout before generation. RGB and
native packed projects retain their manifest family; v1 migration, hardware
configuration and target budgeting remain separate. `project.input` remains for
older clients; the extension exposes only Configure.

Service 0.2.6 advertises `project.input`. With no `params.logicalInput`, the result
identifies the current standard input choice (omitted for custom capabilities).
With `outputOnly`, `touch`, `touchKeys` or `encoderSelect`, it proposes two edits:
`{path, before, after}` for the input snapshot and manifest with its new canonical
hash. Choosing the current capabilities returns an empty edit list. Existing locks
must validate; this operation does not repair tampered profiles or migrate v1.
The host checks expected paths, unchanged document contents/versions and clean
buffers before one workspace edit, saves the pair, and requires Generate UI before
an application rebuild. Partial saves leave recoverable editor documents; the hash
checks reject inconsistent disk state. No forms, handlers, display definitions,
generated outputs, application tasks or drivers are changed by the proposal.

Each component has `id`, `name`, `widgetId`, optional `parentId`, parent-relative
`frame: {x,y,width,height}`, optional `text`, and `properties`: the explicitly
authored `{id, value}` pairs other than the frame. An absent property uses its
schema default. Values use their editor shapes: Booleans, integers, enum/text
strings, colors as a `theme.*` token or legacy `#aabbggrr` hex (low byte red,
high byte inverted alpha), reference IDs, and arrays for animation frames.
`previewHidden` is true for a tab-view page other than the selected one and for
everything inside it; the runtime hides those components, so a host should not
hit-test them.
`actions` lists `{widgetActionId, actionId}` for each action the widget reports;
`actionId` is omitted when unassigned (null fields are omitted throughout).

Each widget has `id`, `name`, palette `group`, `help`, the ordered property IDs
it accepts, `allowedParentIds`, `container` (whether it accepts children), and
`actions` (`id`, `name`, and the `source` property that carries the runtime ID).
Action choices also carry `runtimeId` and `symbol`. Each entry in `properties` describes one catalog property: `id`, `name`,
camel-case `kind`, editor `category`, `defaultValue`, optional `minimum`, `maximum`,
and `units`, `required`, `enumValues`, `documentation`, and `editable`. Frame and
animation-frame lists are not editable through the generic property operation.
`choices` lists the form's declared `themeColor` (with its legacy `color`), `bitmap`,
`font`, `action`, and `binding` entries by `id`, display `name`, and `kind`. Surface format names are
`Rgb565`, `Rgb888`, `Xrgb8888`, or `Argb8888`; shape is `Rectangle`, `Square`, or
`Circle`. Diagnostics carry `code`, lowercase `severity`, `message`, and a model
`path`. A successful native render has status `Ok`. Native render rejection has
an empty pixel payload and explanatory diagnostics; transport/startup failure is
an error response and leaves editing available.

Edit requests add one `operation` object to params:

- `{ "kind": "add", "parentId": "...", "widgetId": "lithe.widget.button" }`
- `{ "kind": "frame", "componentId": "...", "frame": { "x": 12, "y": 16, "width": 90, "height": 32 } }`
- `{ "kind": "text", "componentId": "...", "text": "Start" }`
- `{ "kind": "delete", "componentId": "..." }`
- `{ "kind": "property", "componentId": "...", "propertyId": "button-behavior", "value": "toggle" }`
- `{ "kind": "reset", "componentId": "...", "propertyId": "checked" }`
- `{ "kind": "rename", "componentId": "...", "name": "startButton" }`
- `{ "kind": "duplicate", "componentIds": ["...", "..."] }` (or `componentId`)
- `{ "kind": "delete", "componentIds": ["...", "..."] }` (or `componentId`)
- `{ "kind": "order", "componentId": "...", "order": "bringToFront" }` (`bringForward`, `sendBackward`, `sendToBack`)
- `{ "kind": "reparent", "componentId": "...", "parentId": "..." }`
- `{ "kind": "frames", "frames": { "id": { "x": 0, "y": 0, "width": 10, "height": 10 } } }`
- `{ "kind": "themeColor", "tokenId": "theme.brand", "color": "#00df6c2d" }` (adds or recolors)
- `{ "kind": "renameThemeColor", "tokenId": "theme.brand", "newTokenId": "theme.primary" }`
- `{ "kind": "deleteThemeColor", "tokenId": "theme.brand" }`
- `{ "kind": "action", "componentId": "...", "widgetActionId": "activate", "actionId": "action.start" }`
  (`actionId` null clears; `"newAction": { "id", "runtimeId", "symbol" }` creates and assigns)

An `add` operation may also supply `frame` for the new component's initial
parent-relative position and size. Omission uses the catalog default size centered
in the parent, except that a tab-view child fills the content area below the
strip. A new tab bar or tab view starts with the titles `TAB 1|TAB 2`, and a new
wheel picker with `ITEM 1|ITEM 2|ITEM 3|ITEM 4|ITEM 5`. Invalid
initial geometry rejects the entire edit; no partial addition is published.

A `property` value must match the property kind in the shapes above; a wrong JSON
type, malformed color, undeclared token or reference, out-of-range number, or a
property the widget does not accept returns `InvalidEdit` with the shared
validator's diagnostic. Frame and animation-frame properties return
`InvalidOperation`. `reset` removes the authored value so the schema default
applies; resetting a required property is rejected. Shared editing rules still
apply, so changing a button's `button-behavior` to `momentary` clears `checked` in
the same edit, while checking a momentary button fails with `LVFD1132`. Structural,
theme, and action edits likewise call the shared session: a duplicate name fails
with `LVFD1102`, deleting a token still in use with `LVED1802`, and a button action
that reuses another component's runtime ID with `LVFD1121`. An `add` or `duplicate`
selects the new component; a multi-component `delete` or `frames` edit returns no
selection so the host keeps its own. These fields are additive within protocol
version 1.

Every edit parses the supplied XML anew and uses the existing editing rules. The
result retains the *source* document version: the host must reject stale results
and apply accepted XML through one normal IDE document edit. It must inspect the
new document version after that edit. The service retains no independent undo
history. Generation retains the existing exporter checks for conflicts and
preserves handwritten `application.hpp` and unrelated neighboring files.

Limits: form XML is at most 1 MiB UTF-8; one request line is at most 8 MiB
characters (including JSON escapes); RGBA is at most 16 MiB before base64. Native
startup and rendering each have a five-second deadline. Oversized lines are
discarded through their newline, after which subsequent requests remain usable.
No MCU budget is selected or evaluated by this service.

Run the retained integration checks from the repository root:

```powershell
dotnet build tools/designer/tests/LitheView.Designer.Service.Tests/LitheView.Designer.Service.Tests.csproj --disable-build-servers -m:1
$repository = (Resolve-Path .).Path
$worker = (Resolve-Path build/windows-mingw-debug/litheview_designer_preview_worker.exe).Path
dotnet tools/designer/tests/LitheView.Designer.Service.Tests/bin/Debug/net10.0/LitheView.Designer.Service.Tests.dll `
  $repository $worker
```

Omit the last argument for the protocol/edit/export/presentation tests without a
native worker. Evidence is written under `build/designer-service-tests/`.
