# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
#requires -Version 7.2
[CmdletBinding()]
param([ValidateSet('Win32', 'STM32')][string]$Platform = 'Win32', [switch]$RequireDebugger)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. "$PSScriptRoot/tool-helpers.ps1"
$application = Split-Path $PSScriptRoot -Parent
$links = Join-Path $application 'build/tool-links'
New-Item -ItemType Directory -Path $links -Force | Out-Null

function Resolve-Bin([string]$Name, [string]$Override, [string[]]$Candidates) {
    $explicit = if ($Override) { Join-Path $Override $Name } else { '' }
    Split-Path (Resolve-Tool $Name $explicit $Candidates) -Parent
}
function Set-ToolLink([string]$Name, [string]$Directory) {
    # Only replace our own directory junction, never a real directory or its contents.
    $path = Join-Path $links $Name
    $existing = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.LinkType -ne 'Junction') { throw "Expected a tool junction at $path; inspect it manually." }
        if ([IO.Path]::GetFullPath($existing.Target) -eq [IO.Path]::GetFullPath($Directory)) { return }
        [IO.Directory]::Delete($path)
    }
    New-Item -ItemType Junction -Path $path -Target $Directory | Out-Null
}
function Resolve-DebugLink([string]$Name, [string]$Executable, [string]$Override, [string[]]$Candidates) {
    try {
        $directory = Resolve-Bin $Executable $Override $Candidates
    } catch {
        # Explicit overrides are never silently ignored, even during build-only setup.
        if ($Override -or $RequireDebugger) { throw }
        Write-Warning "Debugger tool unavailable ($Executable). Build is still supported. Run configure-tools.ps1 -Platform $Platform -RequireDebugger before F5."
        return
    }
    # CubeProgrammer resolves its device database relative to bin/.. . Preserve
    # the installation tree, not just a bin junction whose parent is tool-links.
    if ($Name -eq 'programmer') {
        $directory = Split-Path $directory -Parent
        if (!(Test-Path -LiteralPath (Join-Path $directory 'bin/STM32_Programmer_CLI.exe'))) {
            throw 'Expected CubeProgrammer installation with bin/ and its adjacent device database.'
        }
    }
    Set-ToolLink $Name $directory
}
$ninja = Resolve-Tool 'ninja.exe' $env:STARTER_NINJA @(
    "$env:LOCALAPPDATA/stm32cube/bundles/ninja/*/ninja.exe",
    "$env:LOCALAPPDATA/stm32cube/bundles/ninja/*/bin/ninja.exe",
    "$env:USERPROFILE/.mcuxpressotools/ninja-*/ninja.exe")
if ($Platform -eq 'Win32') {
    if ($env:STARTER_MINGW_BIN) {
        $compilerBin = Resolve-Bin 'gcc.exe' $env:STARTER_MINGW_BIN @()
    } else {
        # A 64-bit GCC earlier on PATH is not an eligible compiler for this RTOS port.
        $candidates = @((Get-Command gcc.exe -All -ErrorAction SilentlyContinue | ForEach-Object Source))
        $candidates += "$env:SystemDrive/msys64/mingw32/bin/gcc.exe"
        $compilerBin = ''
        foreach ($candidate in $candidates) {
            if (!(Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
            $machine = & $candidate -dumpmachine
            if ($LASTEXITCODE -eq 0 -and $machine -match '^i[3-6]86-.*mingw32$') {
                $compilerBin = Split-Path $candidate
                break
            }
        }
        if (!$compilerBin) { throw 'Install an i686 MinGW-w64 toolchain and set STARTER_MINGW_BIN to its bin directory.' }
    }
    $gcc = Join-Path $compilerBin 'gcc.exe'
    $machine = & $gcc -dumpmachine
    if ($LASTEXITCODE -ne 0 -or $machine -notmatch '^i[3-6]86-.*mingw32$') {
        throw "The Windows RTOS port requires i686 MinGW-w64, not '$machine': $gcc"
    }
    $cxx = Join-Path $compilerBin 'g++.exe'
    Set-ToolLink 'mingw' $compilerBin
    if (!(Test-Path -LiteralPath (Join-Path $compilerBin 'gdb.exe')) -and $RequireDebugger) {
        throw 'Install the matching i686 GDB in STARTER_MINGW_BIN.'
    }
} else {
    $compilerBin = Resolve-Bin 'arm-none-eabi-gcc.exe' $env:STARTER_ARM_BIN @(
        "$env:LOCALAPPDATA/stm32cube/bundles/gnu-tools-for-stm32/*/bin/arm-none-eabi-gcc.exe")
    $gcc = Join-Path $compilerBin 'arm-none-eabi-gcc.exe'
    $cxx = Join-Path $compilerBin 'arm-none-eabi-g++.exe'
    $machine = & $gcc -dumpmachine
    if ($LASTEXITCODE -ne 0 -or $machine -ne 'arm-none-eabi') {
        throw "Expected an arm-none-eabi compiler, not '$machine': $gcc"
    }
    foreach ($tool in @('arm-none-eabi-objcopy.exe', 'arm-none-eabi-size.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $compilerBin $tool))) { throw "Missing $tool in $compilerBin" }
    }
    Resolve-DebugLink 'arm-gdb' 'arm-none-eabi-gdb.exe' $env:STARTER_ARM_GDB_BIN @(
        "$env:LOCALAPPDATA/stm32cube/bundles/gnu-gdb-for-stm32/*/bin/arm-none-eabi-gdb.exe",
        "$compilerBin/arm-none-eabi-gdb.exe")
    Resolve-DebugLink 'stlink' 'ST-LINK_gdbserver.exe' $env:STARTER_STLINK_BIN @(
        "$env:LOCALAPPDATA/stm32cube/bundles/stlink-gdbserver/*/bin/ST-LINK_gdbserver.exe")
    Resolve-DebugLink 'programmer' 'STM32_Programmer_CLI.exe' $env:STARTER_PROGRAMMER_BIN @(
        "$env:LOCALAPPDATA/stm32cube/bundles/programmer/*/bin/STM32_Programmer_CLI.exe")
}
if (!(Test-Path -LiteralPath $cxx -PathType Leaf)) { throw "Missing C++ compiler: $cxx" }
# Bracket quoting preserves spaces and literal CMake metacharacters in local paths.
$values = [ordered]@{ STARTER_CC = $gcc; STARTER_CXX = $cxx; STARTER_COMPILER_BIN = $compilerBin; STARTER_NINJA = $ninja }
$lines = foreach ($entry in $values.GetEnumerator()) {
    $value = $entry.Value.Replace('\', '/')
    if ($value.Contains(']==]') -or $value.Contains(';')) { throw "Unsupported tool path: $value" }
    "set($($entry.Key) [==[$value]==])"
}
$output = Join-Path $application "build/tool-paths-$Platform.cmake"
[IO.File]::WriteAllLines($output, $lines)
Write-Output "Selected $Platform compiler: $gcc"
