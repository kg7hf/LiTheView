# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixture = Join-Path $repository "build/package-notice-tests/$([Guid]::NewGuid().ToString('N'))"
$shell = (Get-Process -Id $PID).Path
New-Item -ItemType Directory -Force -Path $fixture | Out-Null

# A small isolated checkout exercises the real packager without changing production
# notices, the prepared designer, installed tools or the application examples.
foreach ($directory in @('tools/vscode/lib', 'tools/vscode/media', 'schema',
    'build/vscode-designer/service', 'runtime')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $fixture $directory) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $repository 'licenses') -Destination $fixture -Recurse
foreach ($relative in @('tools/package-vscode-designer.ps1', 'tools/vscode/package.json',
    'tools/vscode/README.md', 'tools/vscode/extension.js', 'schema/widgets.json',
    'LICENSE', 'NOTICE.md', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repository $relative) -Destination (Join-Path $fixture $relative)
}
$worker = Join-Path $fixture 'build/vscode-designer/worker.exe'
$service = Join-Path $fixture 'build/vscode-designer/service/service.dll'
[IO.File]::WriteAllBytes($worker, [byte[]]@(1, 2, 3))
[IO.File]::WriteAllBytes($service, [byte[]]@(4, 5, 6))
@{
    worker = $worker
    service = $service
    widgetCatalogSha256 = (Get-FileHash (Join-Path $fixture 'schema/widgets.json')).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content (Join-Path $fixture 'build/vscode-designer/prepared.json')
$runtimePath = Join-Path $fixture 'licenses/windows-runtime.json'
$runtime = Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json
foreach ($binary in $runtime.binaries) {
    $file = Join-Path $fixture "runtime/$($binary.name)"
    [IO.File]::WriteAllBytes($file, [Text.Encoding]::UTF8.GetBytes("fixture:$($binary.name)"))
    $binary.sha256 = (Get-FileHash $file).Hash.ToLowerInvariant()
}
$runtime | ConvertTo-Json -Depth 10 | Set-Content $runtimePath
$output = Join-Path $fixture 'test.vsix'
$packager = Join-Path $fixture 'tools/package-vscode-designer.ps1'
$runtimeBin = Join-Path $fixture 'runtime'

function Invoke-PackageCheck([string] $Name, [string] $ExpectedFailure = '', [string[]] $Extra = @()) {
    $arguments = @('-NoProfile', '-File', $packager, '-OutputPath', $output)
    if ('-SourceOnly' -notin $Extra) { $arguments += @('-CompilerBin', $runtimeBin) }
    $messages = & $shell @arguments @Extra 2>&1
    $exitCode = $LASTEXITCODE
    $messages | Set-Content (Join-Path $fixture "$Name.log")
    if (!$ExpectedFailure) {
        if ($exitCode -ne 0) { throw "Package success case failed: $messages" }
    } elseif ($exitCode -eq 0 -or ($messages -join "`n") -notlike "*$ExpectedFailure*") {
        throw "Expected '$ExpectedFailure' for $Name, exit=$exitCode : $messages"
    }
}

Invoke-PackageCheck 'success'
Invoke-PackageCheck 'bundled-redistribution' '' @('-ForRedistribution')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($output)
try {
    $catalog = Get-Content (Join-Path $fixture 'licenses/upstream-files.json') -Raw | ConvertFrom-Json
    foreach ($notice in $catalog.files) {
        $entry = $archive.GetEntry("extension/licenses/$($notice.file)")
        if (!$entry) { throw "Missing packaged notice $($notice.file)" }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
        finally { $stream.Dispose() }
        if ($hash -cne $notice.sha256) { throw "Packaged notice bytes changed: $($notice.file)" }
    }
    foreach ($name in @('extension/LICENSE', 'extension/NOTICE.md', 'extension/THIRD_PARTY_NOTICES.md',
        'extension/licenses/windows-runtime.json')) {
        if (!$archive.GetEntry($name)) { throw "Missing packaged attribution: $name" }
    }
    $stream = $archive.GetEntry('extension/licenses/windows-runtime.json').Open()
    try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
    finally { $stream.Dispose() }
    if ($hash -ine (Get-FileHash $runtimePath).Hash) { throw 'Packaged runtime provenance changed.' }
    foreach ($binary in $runtime.binaries) {
        $entry = $archive.GetEntry("extension/runtime/native/$($binary.name)")
        if (!$entry) { throw "Missing bundled runtime: $($binary.name)" }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
        if ($hash -ine $binary.sha256) { throw "Packaged runtime bytes changed: $($binary.name)" }
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('extension.vsixmanifest').Open())
    try { $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if (!$xml.Contains('Microsoft.VisualStudio.Services.Content.License')) { throw 'VSIX license asset missing.' }
} finally { $archive.Dispose() }
$sidecar = Get-Content "$output.json" -Raw | ConvertFrom-Json
if ($sidecar.packageKind -ne 'windows-bundle' -or $sidecar.distributionIntent -ne 'redistribution' -or
    $sidecar.runtimeManifestSha256 -ine (Get-FileHash $runtimePath).Hash) {
    throw 'Bundled package metadata misrepresents the included runtime.'
}
$originalHash = (Get-FileHash $output).Hash
$projectLicensePath = Join-Path $fixture 'LICENSE'
$projectLicenseBytes = [IO.File]::ReadAllBytes($projectLicensePath)
try {
    Remove-Item -LiteralPath $projectLicensePath
    Invoke-PackageCheck 'missing-project-license' 'Missing first-party LICENSE'
} finally { [IO.File]::WriteAllBytes($projectLicensePath, $projectLicenseBytes) }
$noticePath = Join-Path $fixture 'licenses/DejaVu-LICENSE.txt'
$noticeBytes = [IO.File]::ReadAllBytes($noticePath)
try {
    [IO.File]::WriteAllText($noticePath, 'damaged notice')
    Invoke-PackageCheck 'changed-notice' 'Missing or changed upstream notice'
    Remove-Item -LiteralPath $noticePath
    Invoke-PackageCheck 'missing-notice' 'Missing or changed upstream notice'
} finally { [IO.File]::WriteAllBytes($noticePath, $noticeBytes) }
$binaryPath = Join-Path $runtimeBin 'libgcc_s_seh-1.dll'
[IO.File]::WriteAllText($binaryPath, 'different compiler runtime')
Invoke-PackageCheck 'changed-runtime' 'Unreviewed native runtime'
if ((Get-FileHash $output).Hash -ne $originalHash) { throw 'Failed preflight replaced the existing package.' }

# Optional source-only packaging uses readable extension source, no native/runtime binaries and no
# prepared tools or compiler installation. Existing extension fallback uses the
# developer's locally prepared service/worker.
Remove-Item -LiteralPath (Join-Path $fixture 'build/vscode-designer/prepared.json')
Invoke-PackageCheck 'source-publication' '' @('-SourceOnly', '-ForRedistribution')
$archive = [IO.Compression.ZipFile]::OpenRead($output)
try {
    if (@($archive.Entries | Where-Object { $_.FullName -like 'extension/runtime/*' -or $_.FullName -match '\.(dll|exe)$' }).Count) {
        throw 'Source package contains compiled runtime files.'
    }
    foreach ($name in @('extension/extension.js', 'extension/package.json', 'extension/LICENSE')) {
        if (!$archive.GetEntry($name)) { throw "Missing source package entry: $name" }
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('extension.vsixmanifest').Open())
    try { $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($xml.Contains('TargetPlatform=')) { throw 'Source package must not be tied to Windows.' }
} finally { $archive.Dispose() }
$sidecar = Get-Content "$output.json" -Raw | ConvertFrom-Json
if ($sidecar.packageKind -ne 'source-only' -or $sidecar.distributionIntent -ne 'redistribution' -or $null -ne $sidecar.runtimeManifestSha256) {
    throw 'Source package metadata misrepresents the included runtime.'
}
Invoke-PackageCheck 'conflicting-source-input' 'Source-only packages do not accept native runtime inputs' @('-SourceOnly', '-CompilerBin', $runtimeBin)
$sourceHash = (Get-FileHash $output).Hash
[IO.File]::WriteAllBytes((Join-Path $fixture 'tools/vscode/lib/unexpected.dll'), [byte[]]@(7, 8, 9))
Invoke-PackageCheck 'binary-in-source-tree' 'Compiled binaries are not allowed' @('-SourceOnly', '-ForRedistribution')
if ((Get-FileHash $output).Hash -ne $sourceHash) { throw 'Rejected source input replaced the existing package.' }
Write-Output "PASS: Windows bundle notices and provenance, byte preservation, license asset, redistribution intent, source-only packaging without prepared tools, runtime mismatch and output preservation. Evidence: $fixture"
exit 0
