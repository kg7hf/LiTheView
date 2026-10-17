# Native packed component consumer

This consumer compiles a **Generate UI** component from a version-3 native packed display project
against an installed `LitheView::Core` package that enables only the selected destination writer.
It needs no MCU, board or target profile.

`main.cpp` derives the application-owned form from the generated `Instance<>`, as
`form_handlers.hpp` does, and overrides `onButton1Clicked()`. It then:

- renders into native storage sized by the generated display configuration;
- compares every storage byte outside the button with an independent packing oracle;
- activates the button through logical select input, so the handwritten handler runs and
  calls `getButton1()`.

The `forms` directory holds the exact forms saved by the designer workflow test
("native display projects open, edit, save, preview and generate"). That test fails if they
drift. Each form has a red root panel, a half-red panel, a yellow panel whose edges split R2
storage bytes, and `button1`.

## Run

From the repository root on Linux, with Ninja, a C++23 compiler and the designer CLI built.
Use the matching generator and `.exe` paths on Windows MinGW.

```bash
cli="dotnet tools/designer/src/LitheView.Designer.Cli/bin/Release/net10.0/LitheView.Designer.Cli.dll"
out=build/native-components
for entry in synthetic-r2:R2_PACKED2X2 synthetic-red-cell:RRRR2222 synthetic-red-yellow:RYRY2222; do
  name=${entry%%:*}; format=${entry##*:}
  cmake -S . -B $out/core-$name -G Ninja -DCMAKE_BUILD_TYPE=Release \
    -DLITHEVIEW_DESTINATION_FORMAT=$format -DLITHEVIEW_BUILD_RENDERING_FIXTURES=OFF \
    -DLITHEVIEW_BUILD_TESTS=OFF -DLITHEVIEW_BUILD_WIN32=OFF -DLITHEVIEW_BUILD_STM32U545_PROBE=OFF \
    -DLITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF
  cmake --build $out/core-$name
  cmake --install $out/core-$name --prefix $PWD/$out/installed-$name
  $cli project create-native $out/projects/$name example.native-component.$name "Native component $name" \
    profiles/display-definitions/$name.json profiles/logical-inputs/encoder-select.json schema/widgets.json
  cp tests/native_component_consumer/forms/$name.litheview.xml $out/projects/$name/forms/main.litheview.xml
  $cli component export-project $out/projects/$name/project.litheview.json "$PWD"
  cmake -S tests/native_component_consumer -B $out/consumer-$name -G Ninja -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_PREFIX_PATH=$PWD/$out/installed-$name -DCOMPONENT_DIRECTORY=$PWD/$out/projects/$name/simulation
  cmake --build $out/consumer-$name
  ctest --test-dir $out/consumer-$name --output-on-failure -V
done
```

A project inside the checkout's `build` directory without a path sidecar exports its component to
`<project>/simulation`. Each run prints the checked byte count and `RESULT PASS`. The checked
bytes are 48,552 of 49,152 for R2 and 46,752 of 49,152 for the cell formats; the remainder is
covered by the button. Configuring a component against a package built for a different destination
fails to compile at the generated `supportsDestination` assertion.
