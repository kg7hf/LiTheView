# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param(
    [string] $CompilerBin = '',
    [string] $OutputPath = '',
    [string] $RuntimeManifest = '',
    [switch] $SourceOnly,
    [switch] $ForRedistribution
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $repository 'tools/vscode'
$preparedRoot = Join-Path $repository 'build/vscode-designer'
$catalogHash = (Get-FileHash -LiteralPath (Join-Path $repository 'schema/widgets.json')).Hash.ToLowerInvariant()
if ($SourceOnly -and ($CompilerBin -or $RuntimeManifest)) {
    throw 'Source-only packages do not accept native runtime inputs.'
}
$prepared = $null
if (!$SourceOnly) {
    $prepared = Get-Content -LiteralPath (Join-Path $preparedRoot 'prepared.json') -Raw | ConvertFrom-Json
    if ($prepared.widgetCatalogSha256 -ne $catalogHash) {
        throw 'Prepared tools use a different widget catalog. Run tools/prepare-vscode-designer.ps1 again.'
    }
}
$manifest = Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
$packageSuffix = if ($SourceOnly) { '-source' } else { '' }
if (!$OutputPath) { $OutputPath = Join-Path $preparedRoot "$($manifest.name)-$($manifest.version)$packageSuffix.vsix" }
$OutputPath = [IO.Path]::GetFullPath($OutputPath, $repository)
if (!$SourceOnly -and !$CompilerBin) {
    $cache = Get-Content -LiteralPath (Join-Path $repository 'build/windows-mingw-debug/CMakeCache.txt')
    $compiler = $cache | Where-Object { $_ -match '^CMAKE_CXX_COMPILER:FILEPATH=' } | Select-Object -First 1
    if (!$compiler) { throw 'Cannot resolve the native worker compiler from CMakeCache.txt. Supply -CompilerBin.' }
    $CompilerBin = Split-Path -Parent ($compiler -replace '^CMAKE_CXX_COMPILER:FILEPATH=', '')
}

# Check notices and binary provenance before creating or replacing an output package.
$licenseRoot = Join-Path $repository 'licenses'
$noticeCatalog = Get-Content -LiteralPath (Join-Path $licenseRoot 'upstream-files.json') -Raw | ConvertFrom-Json
if ($noticeCatalog.schemaVersion -ne 1) { throw 'Unsupported upstream notice catalog version.' }
$noticeNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($notice in $noticeCatalog.files) {
    if ([IO.Path]::GetFileName($notice.file) -cne $notice.file -or !$noticeNames.Add($notice.file)) {
        throw 'Upstream notice names must be unique filenames within licenses/.'
    }
    $noticePath = Join-Path $licenseRoot $notice.file
    if (!(Test-Path -LiteralPath $noticePath -PathType Leaf) -or
        (Get-FileHash -LiteralPath $noticePath).Hash -ine $notice.sha256) {
        throw "Missing or changed upstream notice: $($notice.file)"
    }
}
$runtime = $null
$runtimePaths = [ordered]@{}
if (!$SourceOnly) {
    if (!$RuntimeManifest) { $RuntimeManifest = Join-Path $licenseRoot 'windows-runtime.json' }
    $RuntimeManifest = [IO.Path]::GetFullPath($RuntimeManifest, $repository)
    $runtime = Get-Content -LiteralPath $RuntimeManifest -Raw | ConvertFrom-Json
    if ($runtime.schemaVersion -ne 1) { throw 'Unsupported native runtime manifest version.' }
    foreach ($requiredNotice in $runtime.noticeFiles) {
        if (!$noticeNames.Contains($requiredNotice)) { throw "Unrecorded runtime notice: $requiredNotice" }
    }
    if (@($runtime.noticeFiles).Count -eq 0) { throw 'Runtime manifest has no license notices.' }
    $expectedNames = @('libgcc_s_seh-1.dll', 'libstdc++-6.dll', 'libwinpthread-1.dll')
    foreach ($binary in $runtime.binaries) {
        if ($binary.name -cnotin $expectedNames -or $runtimePaths.Contains($binary.name)) {
            throw "Unexpected or duplicate native runtime: $($binary.name)"
        }
        $path = Join-Path $CompilerBin $binary.name
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing native runtime dependency: $path" }
        if ((Get-FileHash -LiteralPath $path).Hash -ine $binary.sha256) {
            throw "Unreviewed native runtime: $($binary.name). Supply the matching -RuntimeManifest with notices and source provenance."
        }
        $runtimePaths[$binary.name] = $path
    }
    if ($runtimePaths.Count -ne $expectedNames.Count) { throw 'Native runtime manifest is incomplete.' }
}
$projectLicense = Join-Path $repository 'LICENSE'
if (!(Test-Path -LiteralPath $projectLicense -PathType Leaf)) {
    throw 'Missing first-party LICENSE; every package must include it.'
}
if ($ForRedistribution) {
    if ($manifest.license -eq 'UNLICENSED') {
        throw 'Redistribution requires an owner-selected first-party LICENSE.'
    }
}
$files = [ordered]@{}
foreach ($name in @('package.json', 'extension.js', 'README.md')) {
    $files["extension/$name"] = Join-Path $source $name
}
$files['extension/NOTICE.md'] = Join-Path $repository 'NOTICE.md'
$files['extension/THIRD_PARTY_NOTICES.md'] = Join-Path $repository 'THIRD_PARTY_NOTICES.md'
$files['extension/LICENSE'] = $projectLicense
foreach ($file in Get-ChildItem -LiteralPath $licenseRoot -File) {
    $files["extension/licenses/$($file.Name)"] = $file.FullName
}
# An override is packaged under the same stable name referenced by the notices.
if (!$SourceOnly) { $files['extension/licenses/windows-runtime.json'] = $RuntimeManifest }
foreach ($directory in @('lib', 'media')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $source $directory) -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/')
        $files["extension/$relative"] = $file.FullName
    }
}
if (!$SourceOnly) {
    foreach ($file in Get-ChildItem -LiteralPath (Split-Path -Parent $prepared.service) -File) {
        if ($file.Extension -in @('.dll', '.json')) { $files["extension/runtime/service/$($file.Name)"] = $file.FullName }
    }
    $files['extension/runtime/native/litheview_designer_preview_worker.exe'] = $prepared.worker
    # The matching compiler's runtime dependencies travel with this Windows bundle.
    foreach ($entry in $runtimePaths.GetEnumerator()) {
        $files["extension/runtime/native/$($entry.Key)"] = $entry.Value
    }
}
foreach ($file in $files.Values) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing package input: $file" }
    if ($SourceOnly -and [IO.Path]::GetExtension($file) -in @('.dll', '.exe')) {
        throw "Compiled binaries are not allowed in a source-only package: $file"
    }
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$packageName = [Security.SecurityElement]::Escape($manifest.name)
$packageVersion = [Security.SecurityElement]::Escape($manifest.version)
$publisher = [Security.SecurityElement]::Escape($manifest.publisher)
$displayName = [Security.SecurityElement]::Escape($manifest.displayName)
$engine = [Security.SecurityElement]::Escape($manifest.engines.vscode)
$targetPlatform = if ($SourceOnly) { '' } else { ' TargetPlatform="win32-x64"' }
$licenseAsset = if ($files.Contains('extension/LICENSE')) {
    '<Asset Type="Microsoft.VisualStudio.Services.Content.License" Path="extension/LICENSE" Addressable="true"/>'
} else { '' }
$packageXml = @"
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata>
    <Identity Language="en-US" Id="$packageName" Version="$packageVersion" Publisher="$publisher"$targetPlatform />
    <DisplayName>$displayName</DisplayName>
    <Description xml:space="preserve">Application-owned LiTheView form editing inside VS Code.</Description>
    <Tags>litheview,embedded,designer</Tags><Categories>Other</Categories>
    <Properties><Property Id="Microsoft.VisualStudio.Code.Engine" Value="$engine"/><Property Id="Microsoft.VisualStudio.Code.ExtensionKind" Value="workspace"/></Properties>
  </Metadata>
  <Installation><InstallationTarget Id="Microsoft.VisualStudio.Code"/></Installation>
  <Dependencies/>
  <Assets><Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true"/><Asset Type="Microsoft.VisualStudio.Services.Content.Details" Path="extension/README.md" Addressable="true"/>$licenseAsset</Assets>
</PackageManifest>
"@
$contentTypes = '<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="json" ContentType="application/json"/><Default Extension="js" ContentType="application/javascript"/><Default Extension="css" ContentType="text/css"/><Default Extension="md" ContentType="text/markdown"/><Default Extension="txt" ContentType="text/plain"/><Default Extension="dll" ContentType="application/octet-stream"/><Default Extension="exe" ContentType="application/octet-stream"/><Default Extension="vsixmanifest" ContentType="text/xml"/></Types>'
if ($files.Contains('extension/LICENSE')) {
    $contentTypes = $contentTypes.Replace('</Types>', '<Override PartName="/extension/LICENSE" ContentType="text/plain"/></Types>')
}
Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
try {
    foreach ($entry in @{ 'extension.vsixmanifest' = $packageXml; '[Content_Types].xml' = $contentTypes }.GetEnumerator()) {
        $writer = [IO.StreamWriter]::new($archive.CreateEntry($entry.Key).Open(), [Text.UTF8Encoding]::new($false))
        try { $writer.Write($entry.Value) } finally { $writer.Dispose() }
    }
    foreach ($entry in $files.GetEnumerator()) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.Value, $entry.Key) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
[ordered]@{
    package = $OutputPath
    sha256 = (Get-FileHash -LiteralPath $OutputPath).Hash.ToLowerInvariant()
    widgetCatalogSha256 = $catalogHash
    packageKind = $(if ($SourceOnly) { 'source-only' } else { 'windows-bundle' })
    distributionIntent = $(if ($ForRedistribution) { 'redistribution' } else { 'local-development' })
    runtimeManifestSha256 = $(if (!$SourceOnly) { (Get-FileHash -LiteralPath $RuntimeManifest).Hash.ToLowerInvariant() } else { $null })
    inputs = @($files.GetEnumerator() | ForEach-Object {
        [ordered]@{ entry = $_.Key; sha256 = (Get-FileHash -LiteralPath $_.Value).Hash.ToLowerInvariant() }
    })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$OutputPath.json" -Encoding utf8
Write-Output "Packaged extension$packageSuffix`: $OutputPath"
