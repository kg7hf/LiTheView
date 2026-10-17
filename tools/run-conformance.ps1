# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

[CmdletBinding()]
param(
    [string] $ArmToolchainRoot = $env:STM32_TOOLCHAIN_ROOT,
    [switch] $SkipCrossBuilds,
    [ValidateSet('MinGW Makefiles', 'Ninja')]
    [string] $Generator = $(if ($env:OS -eq 'Windows_NT') { 'MinGW Makefiles' } else { 'Ninja' })
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$buildRoot = Join-Path $repositoryRoot 'build/conformance'
$buildWin32 = if ($env:OS -eq 'Windows_NT') { 'ON' } else { 'OFF' }

function Invoke-CMakeStep {
    param(
        [Parameter(Mandatory)]
        [string] $Description,
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    Write-Host "==> $Description"
    & cmake @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

function Invoke-CTestStep {
    param(
        [Parameter(Mandatory)]
        [string] $Description,
        [Parameter(Mandatory)]
        [string] $BuildDirectory
    )

    Write-Host "==> $Description"
    & ctest --test-dir $BuildDirectory --output-on-failure
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

# --fresh discards CMake dependency metadata, but can leave old object files.
# Clean compiled outputs too so every gate checks this checkout's current headers.
Push-Location $repositoryRoot
try {
    foreach ($configuration in @('Debug', 'Release')) {
        $configurationName = $configuration.ToLowerInvariant()
        $hostBuild = Join-Path $buildRoot "host-$configurationName"
        Invoke-CMakeStep "Configure host $configuration" @(
            '--fresh',
            '-S', '.',
            '-B', $hostBuild,
            '-G', $Generator,
            "-DCMAKE_BUILD_TYPE=$configuration",
            '-DCMAKE_EXPORT_COMPILE_COMMANDS=ON',
            "-DLITHEVIEW_BUILD_WIN32=$buildWin32",
            '-DLITHEVIEW_BUILD_TESTS=ON',
            '-DLITHEVIEW_WARNINGS_AS_ERRORS=ON'
        )
        Invoke-CMakeStep "Build host $configuration" @(
            '--build', $hostBuild, '--clean-first', '--parallel'
        )
        Invoke-CTestStep "Test host $configuration" $hostBuild
    }

    $releaseBuild = Join-Path $buildRoot 'host-release'
    $installRoot = Join-Path $buildRoot 'installed'
    $consumerBuild = Join-Path $buildRoot 'package-consumer'
    Invoke-CMakeStep 'Install package' @(
        '--install', $releaseBuild, '--prefix', $installRoot
    )
    Invoke-CMakeStep 'Configure installed-package consumer' @(
        '--fresh',
        '-S', 'tests/package_consumer',
        '-B', $consumerBuild,
        '-G', $Generator,
        '-DCMAKE_BUILD_TYPE=Release',
        "-DCMAKE_PREFIX_PATH=$installRoot"
    )
    Invoke-CMakeStep 'Build installed-package consumer' @(
        '--build', $consumerBuild, '--clean-first', '--parallel'
    )
    Invoke-CTestStep 'Test installed-package consumer' $consumerBuild

    # Prove Core remains usable without any bundled resources or example code,
    # both as a source subdirectory and through a minimal installed package.
    $bareBuild = Join-Path $buildRoot 'core-only'
    $bareInstall = Join-Path $buildRoot 'core-only-installed'
    Invoke-CMakeStep 'Configure asset-free Core' @(
        '--fresh', '-S', '.', '-B', $bareBuild, '-G', $Generator,
        '-DCMAKE_BUILD_TYPE=Release', '-DLITHEVIEW_BUILD_RENDERING_FIXTURES=OFF',
        '-DLITHEVIEW_BUILD_TESTS=OFF', '-DLITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF',
        '-DLITHEVIEW_BUILD_COMPACT_FONT=OFF', '-DLITHEVIEW_BUILD_GENERATED_FONTS=OFF'
    )
    Invoke-CMakeStep 'Build asset-free Core' @('--build', $bareBuild, '--clean-first', '--parallel')
    Invoke-CMakeStep 'Install asset-free Core' @('--install', $bareBuild, '--prefix', $bareInstall)
    foreach ($integration in @('package', 'source')) {
        $bareConsumer = Join-Path $buildRoot "core-only-$integration-consumer"
        $selection = if ($integration -eq 'source') {
            "-DLITHEVIEW_SOURCE=$repositoryRoot"
        } else { "-DCMAKE_PREFIX_PATH=$bareInstall" }
        Invoke-CMakeStep "Configure asset-free $integration consumer" @(
            '--fresh', '-S', 'tests/package_consumer', '-B', $bareConsumer,
            '-G', $Generator, '-DCMAKE_BUILD_TYPE=Release', $selection)
        Invoke-CMakeStep "Build asset-free $integration consumer" @('--build', $bareConsumer, '--clean-first', '--parallel')
        Invoke-CTestStep "Test asset-free $integration consumer" $bareConsumer
    }

    # Separate installed packages prove that each strategy uses only its selected
    # writer. These consumers also check full-frame/tiled bytes and symbol omission.
    $strategyPrefixes = @{}
    foreach ($format in @('RGB565', 'XRGB8888', 'R2_PACKED2X2', 'RRRR2222', 'RYRY2222')) {
        $formatName = $format.ToLowerInvariant()
        $strategyCore = Join-Path $buildRoot "strategy-core-$formatName"
        $strategyPrefix = Join-Path $buildRoot "strategy-installed-$formatName"
        Invoke-CMakeStep "Configure $format strategy package" @(
            '--fresh', '-S', '.', '-B', $strategyCore, '-G', $Generator,
            '-DCMAKE_BUILD_TYPE=Release', "-DLITHEVIEW_DESTINATION_FORMAT=$format",
            '-DLITHEVIEW_BUILD_RENDERING_FIXTURES=OFF', '-DLITHEVIEW_BUILD_TESTS=OFF',
            '-DLITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF', '-DLITHEVIEW_BUILD_COMPACT_FONT=ON',
            '-DLITHEVIEW_BUILD_GENERATED_FONTS=OFF')
        Invoke-CMakeStep "Build $format strategy package" @('--build', $strategyCore, '--clean-first', '--parallel')
        Invoke-CMakeStep "Install $format strategy package" @('--install', $strategyCore, '--prefix', $strategyPrefix)
        $strategyPrefixes[$format] = $strategyPrefix
    }
    & (Join-Path $PSScriptRoot 'check-render-strategies.ps1') `
        -Rgb565Prefix $strategyPrefixes.RGB565 -Xrgb8888Prefix $strategyPrefixes.XRGB8888 `
        -R2Prefix $strategyPrefixes.R2_PACKED2X2 -Rrrr2222Prefix $strategyPrefixes.RRRR2222 `
        -Ryry2222Prefix $strategyPrefixes.RYRY2222 -Generator $Generator `
        -BuildRoot (Join-Path $buildRoot 'render-strategies')

    if (-not $SkipCrossBuilds) {
        $toolchainArgument = @()
        if (-not [string]::IsNullOrWhiteSpace($ArmToolchainRoot)) {
            $resolvedToolchainRoot = (Resolve-Path -LiteralPath $ArmToolchainRoot).Path.Replace('\', '/')
            $toolchainArgument = @("-DLITHEVIEW_ARM_TOOLCHAIN_ROOT=$resolvedToolchainRoot")
        }
        $toolchainFile = (Resolve-Path -LiteralPath 'cmake/arm-none-eabi-gcc.cmake').Path.Replace('\', '/')
        # MCU budgets apply to the selected product. Each native destination
        # has its own archive and workspace gate; the host suite stays universal.
        $crossProducts = @(
            @{ Name = 'm33'; Cpu = 'm33'; Format = 'RGB565' },
            @{ Name = 'm33-r2'; Cpu = 'm33'; Format = 'R2_PACKED2X2' },
            @{ Name = 'm33-red-cell'; Cpu = 'm33'; Format = 'RRRR2222' },
            @{ Name = 'm33-red-yellow'; Cpu = 'm33'; Format = 'RYRY2222' },
            @{ Name = 'm7'; Cpu = 'm7'; Format = 'ALL' }
        )
        foreach ($target in $crossProducts) {
            $crossBuild = Join-Path $buildRoot $target.Name
            $targetArguments = if ($target.Cpu -eq 'm33') {
                @(
                    '-DLITHEVIEW_ARM_CPU=cortex-m33',
                    '-DLITHEVIEW_ARM_FLOAT_ABI=soft',
                    '-DLITHEVIEW_ARM_FPU=',
                    '-DLITHEVIEW_BUILD_STM32U545_PROBE=ON'
                )
            } else {
                @(
                    '-DLITHEVIEW_ARM_CPU=cortex-m7',
                    '-DLITHEVIEW_ARM_FLOAT_ABI=hard',
                    '-DLITHEVIEW_ARM_FPU=fpv5-d16',
                    '-DLITHEVIEW_BUILD_STM32U545_PROBE=OFF'
                )
            }
            $configureArguments = @(
                '--fresh',
                '-S', '.',
                '-B', $crossBuild,
                '-G', $Generator,
                '-DCMAKE_BUILD_TYPE=MinSizeRel',
                '-DCMAKE_EXPORT_COMPILE_COMMANDS=ON',
                "-DCMAKE_TOOLCHAIN_FILE=$toolchainFile",
                '-DLITHEVIEW_DEMO_FONT_BACKEND=AUTO',
                "-DLITHEVIEW_DESTINATION_FORMAT=$($target.Format)",
                "-DLITHEVIEW_BUILD_RENDERING_FIXTURES=$(if ($target.Format -in @('RGB565', 'ALL')) { 'ON' } else { 'OFF' })",
                '-DLITHEVIEW_BUILD_WIN32=OFF',
                '-DLITHEVIEW_BUILD_TESTS=OFF'
            ) + $targetArguments + $toolchainArgument
            Invoke-CMakeStep "Configure $($target.Name)" $configureArguments
            Invoke-CMakeStep "Build and check $($target.Name) budgets" @(
                '--build', $crossBuild, '--clean-first', '--parallel'
            )
        }
    }
} finally {
    Pop-Location
}

Write-Host 'LitheView conformance gates passed.'
