# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

function Resolve-Tool([string]$Name, [string]$Override, [string[]]$Candidates) {
    if ($Override) {
        if (!(Test-Path -LiteralPath $Override -PathType Leaf)) { throw "Missing tool override: $Override" }
        return (Resolve-Path -LiteralPath $Override).Path
    }
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($candidate in $Candidates) {
        $files = @(Get-ChildItem -Path $candidate -File -ErrorAction SilentlyContinue | Sort-Object { [regex]::Replace($_.FullName, '[0-9]+', { param($match) $match.Value.PadLeft(12, '0') }) } -Descending)
        if ($files.Count) { return $files[0].FullName }
    }
    throw "Cannot find $Name. Install the documented prerequisite or set its STARTER_* override."
}
function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed with exit code $LASTEXITCODE" }
}
