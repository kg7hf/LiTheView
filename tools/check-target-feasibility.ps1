# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $CliAssembly,
    [string] $Dotnet = 'dotnet',
    [string] $BuildRoot = (Join-Path $PSScriptRoot '../build/target-feasibility')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$assembly = (Resolve-Path -LiteralPath $CliAssembly).Path
$dotnetCommand = (Get-Command $Dotnet -ErrorAction Stop).Source
New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $BuildRoot).Path
[ordered]@{ Passed = $false; Status = 'incomplete' } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $outputRoot 'validation.json') -Encoding utf8
$fixtureRoot = Join-Path $repositoryRoot 'tests/rendering/display-model-generated'
$results = [Collections.Generic.List[object]]::new()

function Invoke-Report {
    param([string] $Name, [string[]] $Arguments, [int] $ExpectedExit = 0)
    $stdout = Join-Path $outputRoot "$Name.json"
    $stderr = Join-Path $outputRoot "$Name.stderr.log"
    & $dotnetCommand $assembly display @Arguments 1> $stdout 2> $stderr
    $code = $LASTEXITCODE
    if ($code -ne $ExpectedExit) { throw "$Name expected exit $ExpectedExit, got $code; see $stderr" }
    $report = Get-Content -LiteralPath $stdout -Raw | ConvertFrom-Json
    if ($report.surfaceStatus -ne 'passed') { throw "$Name changed surface validity" }
    $results.Add([ordered]@{ Name = $Name; ExitCode = $code; Surface = $report.surfaceStatus; Target = $report.targetStatus })
    return $report
}

function Assert-Constraint {
    param($Report, [string] $Id, [string] $Status, [Nullable[long]] $Used = $null)
    $match = @($Report.target.feasibility.constraints | Where-Object id -EQ $Id)
    if ($match.Count -ne 1 -or $match[0].status -ne $Status -or
        ($null -ne $Used -and $match[0].used -ne $Used)) { throw "Unexpected $Id constraint" }
}

function Get-ExportHashes {
    param([string] $Directory)
    return @(Get-ChildItem -LiteralPath $Directory -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = [IO.Path]::GetRelativePath($Directory, $_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
    }) | ConvertTo-Json -Depth 4 -Compress
}

$common = @(
    (Join-Path $fixtureRoot 'rk043fn48h-rgb565.litheview.xml'),
    (Join-Path $repositoryRoot 'schema/widgets.json'),
    (Join-Path $repositoryRoot 'profiles/display-definitions/rk043fn48h-rgb565.json'),
    (Join-Path $repositoryRoot 'profiles/logical-inputs/pointer-keys-text.json')
)
$resource = 'resources/gallop.resource.json'
$target = Join-Path $fixtureRoot 'targets/rgb565-sufficient.json'
$evidencePath = Join-Path $fixtureRoot 'targets/rgb565-buffer.evidence.json'
$exportPath = Join-Path $outputRoot 'export'
$baseline = Invoke-Report 'surface-only' (@('export-cpp') + $common + @($exportPath, $resource))
if ($baseline.targetStatus -ne 'unevaluated') { throw 'A target was inferred for export' }
$before = Get-ExportHashes $exportPath
$constrained = Invoke-Report 'constrained' (@('check-target') + $common + @(
    (Join-Path $fixtureRoot 'targets/rgb565-constrained.json'), $evidencePath, $resource)) 1
Assert-Constraint $constrained 'minimum-render-payload' 'failed' 261120
if ($constrained.targetStatus -ne 'failed') { throw 'Constrained target did not fail' }
$sufficient = Invoke-Report 'sufficient' (@('check-target') + $common + @($target, $evidencePath, $resource))
Assert-Constraint $sufficient 'minimum-render-payload' 'passed' 261120
Assert-Constraint $sufficient 'allocated-framebuffer' 'passed' 261120
Assert-Constraint $sufficient 'internal-flash' 'unevaluated'
Assert-Constraint $sufficient 'resource-linker-placement' 'unevaluated'
if ($sufficient.targetStatus -ne 'unevaluated') { throw 'Unmeasured target constraints were certified' }
$missing = Invoke-Report 'no-evidence' (@('check-target') + $common + @($target, '-', $resource))
Assert-Constraint $missing 'allocated-framebuffer' 'unevaluated'
$bound = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json -AsHashtable
$bound.expectedFormSha256 = $baseline.formSha256
$bound.expectedDisplayDefinitionSha256 = $baseline.displaySha256
$bound.expectedLogicalInputSha256 = $baseline.inputSha256
$boundPath = Join-Path $outputRoot 'bound.evidence.json'
$bound | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $boundPath -Encoding utf8
$null = Invoke-Report 'bound' (@('check-target') + $common + @($target, $boundPath, $resource))

$invalid = [ordered]@{
    'upper-kind' = '{"measurements":[{"metric":"framebufferAllocatedBytes","value":261120,"kind":"CALCULATED","source":"fixture"}]}'
    'upper-metric' = '{"measurements":[{"metric":"FramebufferAllocatedBytes","value":261120,"kind":"calculated","source":"fixture"}]}'
    'numeric-kind' = '{"measurements":[{"metric":"framebufferAllocatedBytes","value":261120,"kind":0,"source":"fixture"}]}'
    'combined-kind' = '{"measurements":[{"metric":"framebufferAllocatedBytes","value":261120,"kind":"calculated, estimated","source":"fixture"}]}'
    'unknown-property' = '{"schemaVersion":1,"unrecognized":true}'
    'duplicate-property' = '{"schemaVersion":1,"schemaVersion":1}'
    'null-measurements' = '{"measurements":null}'
    'null-entry' = '{"measurements":[null]}'
    'negative-value' = '{"measurements":[{"metric":"framebufferAllocatedBytes","value":-1,"kind":"calculated","source":"fixture"}]}'
    'overflow-value' = '{"measurements":[{"metric":"framebufferAllocatedBytes","value":9223372036854775808,"kind":"calculated","source":"fixture"}]}'
    'missing-source' = '{"measurements":[{"metric":"framebufferAllocatedBytes","value":261120,"kind":"calculated"}]}'
    'stale-form' = ('{"expectedFormSha256":"' + ('0' * 64) + '"}')
    'stale-definition' = ('{"expectedDisplayDefinitionSha256":"' + ('0' * 64) + '"}')
    'stale-input' = ('{"expectedLogicalInputSha256":"' + ('0' * 64) + '"}')
}
foreach ($case in $invalid.GetEnumerator()) {
    $path = Join-Path $outputRoot "$($case.Key).evidence.json"
    $case.Value | Set-Content -LiteralPath $path -Encoding utf8
    $report = Invoke-Report $case.Key (@('check-target') + $common + @($target, $path, $resource)) 1
    if ($report.targetStatus -ne 'failed') { throw "$($case.Key) did not fail the target stage" }
}

$native = Invoke-Report 'native-r2' @(
    'check-target', (Join-Path $fixtureRoot 'synthetic-r2.litheview.xml'),
    (Join-Path $repositoryRoot 'schema/widgets.json'),
    (Join-Path $repositoryRoot 'profiles/display-definitions/synthetic-r2.json'),
    (Join-Path $repositoryRoot 'profiles/logical-inputs/encoder-select.json'),
    (Join-Path $fixtureRoot 'targets/synthetic-r2-tiled.json'),
    (Join-Path $fixtureRoot 'targets/native-r2.evidence.json'), $resource
)
Assert-Constraint $native 'minimum-render-payload' 'passed' 256
Assert-Constraint $native 'allocated-tile-workspace' 'passed' 256
Assert-Constraint $native 'transfer-wire-bytes' 'passed' 49152
Assert-Constraint $native 'transfer-duration' 'unevaluated'

if ($before -cne (Get-ExportHashes $exportPath)) { throw 'Target checks mutated the existing surface export' }
$null = Invoke-Report 'surface-after-target' (@('export-cpp') + $common + @($exportPath, $resource))
if ($before -cne (Get-ExportHashes $exportPath)) { throw 'Surface regeneration changed after target checks' }
[ordered]@{
    Passed = $true; Cases = $results.ToArray(); ExportUnchanged = $true
    EvidenceScope = 'Caller-reported integration evidence; no linked-firmware or hardware measurement performed by this script.'
    CliAssemblySha256 = (Get-FileHash -LiteralPath $assembly).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'validation.json') -Encoding utf8
Write-Output "Target feasibility checks passed ($($results.Count) cases): $outputRoot"
exit 0
