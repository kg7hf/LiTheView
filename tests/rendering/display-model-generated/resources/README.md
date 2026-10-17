# Native resource fixture

`gallop.resource.json` selects one static crop of the existing LitheView gallop artwork. The fixture
copy at `../assets/gallop_source.png` is byte-identical to `tests/rendering/assets/animations/gallop_source.png` in the
repository, with SHA-256 `71656c0af13f37460e70d9d674867ea6787fef02767ee7c461089d9926d47344`.
The original generation provenance and prompt are retained in `tests/rendering/assets/animations/README.md`.

Both variants sample the same source rectangle `(0, 0, 384, 512)` from the 1536x1024 PNG and scale it
to 96x72 with nearest-neighbor sampling. The R2 image is opaque: the existing converter composites
the black source artwork against a white matte, then quantizes red to four levels. The alpha1 image
retains coverage at threshold128 and uses a red tint. These are different source representations;
alpha1 remains tintable, while R2 contains its final red intensities.

| Variant | Logical extent | Storage stride | Storage rows | Payload / estimated ROM | Payload SHA-256 |
| --- | --- | --- | --- | --- | --- |
| R2 | 96x72 | 48 bytes | 36 | 1728 bytes | `c65342db72675a19996b3a7aa0173531fe8072663faa85d972a6f6907a1f8caa` |
| Alpha1 | 96x72 | 12 bytes | 72 | 864 bytes | `727b79f083903d2154f9f20154f92973ee27fe293bd7e409d73e313a73587dfb` |

There are no palette bytes. The converter metrics describe payload costs; the generated C++ file
length is a source-artifact measurement and is not the ROM estimate. Target linker placement and
final firmware size remain later deployment checks.

The R2 variant declares compatibility with the three native display formats. The alpha1 variant
declares compatibility with the RGB formats. Both decoders are portable; these declarations choose
one product resource variant without a runtime format switch. The display definition can impose a
further source-format allowlist. Resource ID and the example's runtime lookup name are `gallop-icon`.

Run `resources/verify-native-packing.ps1` from any working directory to check both payloads against
the retained PNG. Its R2 oracle uses independent level thresholds and positional emitter weights
TL4/TR64/BL1/BR16. The managed tests also invoke the real converter with an independent 4x4 BMP whose
four packed cells must each be `0x72`.

To regenerate from the repository root, call `tools/convert-image.ps1` with these shared options:

```powershell
$fixture = 'tests/rendering/display-model-generated'
$shared = @{
    InputImage = "$fixture/assets/gallop_source.png"
    Kind = 'Icon'; CropX = 0; CropY = 0; CropWidth = 384; CropHeight = 512
    OutputWidth = 96; OutputHeight = 72; ScaleMode = 'NearestNeighbor'
    AlphaMode = 'Preserve'; AlphaThreshold = 128; Dither = 'None'
    TintArgb = 'ffff0000'; MatteArgb = 'ffffffff'
}
./tools/convert-image.ps1 @shared -Format R2Packed2x2 -Tintable False `
    -Symbol GallopR2 -EntryPoint gallopR2 `
    -OutputSource "$fixture/generated/resources/gallop-r2.cpp" `
    -OutputManifest "$fixture/generated/resources/gallop-r2.metrics.json" `
    -OutputPixels "$fixture/generated/resources/gallop-r2.bin"
./tools/convert-image.ps1 @shared -Format Alpha1 -Tintable True `
    -Symbol GallopAlpha1 -EntryPoint gallopAlpha1 `
    -OutputSource "$fixture/generated/resources/gallop-alpha1.cpp" `
    -OutputManifest "$fixture/generated/resources/gallop-alpha1.metrics.json" `
    -OutputPixels "$fixture/generated/resources/gallop-alpha1.bin"
```

Paul R. Decker has offered the project's rights in the retained artwork under GPL-3.0-only.
The fixture records that SPDX identifier and redistribution approval, and treats missing license
metadata as an error. The complete license accompanies the repository and installed packages.
The original pixels and generation provenance are unchanged.
