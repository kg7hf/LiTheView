# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

#requires -Version 7.2
[CmdletBinding()]
param(
    [string] $Dotnet = 'dotnet',
    [string] $Code = 'code',
    [string] $FixtureProject = '',
    [string] $PackagedExtensionPath = '',
    [switch] $SourceOnlyPackage,
    [ValidateRange(30, 600)][int] $TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($SourceOnlyPackage -and !$PackagedExtensionPath) { throw '-SourceOnlyPackage requires -PackagedExtensionPath.' }
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnetCommand = (Get-Command $Dotnet -CommandType Application -ErrorAction Stop).Source
$codeCommand = Get-Command $Code -ErrorAction Stop | Select-Object -First 1 -ExpandProperty Source
$codeCli = ''
if ([IO.Path]::GetExtension($codeCommand) -eq '.cmd') {
    $cliMatch = [regex]::Match((Get-Content -LiteralPath $codeCommand -Raw), '"%~dp0\.\.\\([^"\r\n]+\\cli\.js)"')
    if ($cliMatch.Success) { $codeCli = [IO.Path]::GetFullPath((Join-Path (Split-Path $codeCommand -Parent) ('../' + $cliMatch.Groups[1].Value))) }
    $codeCommand = [IO.Path]::GetFullPath((Join-Path (Split-Path $codeCommand -Parent) '../Code.exe'))
}
$service = Join-Path $repository 'build/vscode-designer/service/LitheView.Designer.Service.dll'
$worker = Join-Path $repository 'build/windows-mingw-debug/litheview_designer_preview_worker.exe'
$requiredArtifacts = @($codeCommand)
if ($PackagedExtensionPath) {
    $PackagedExtensionPath = [IO.Path]::GetFullPath($PackagedExtensionPath)
    $requiredArtifacts += @($PackagedExtensionPath, $codeCli)
}
if (!$PackagedExtensionPath -or $SourceOnlyPackage) { $requiredArtifacts += @($service, $worker) }
foreach ($required in $requiredArtifacts) {
    if (!(Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required prepared artifact is missing: $required" }
}
$run = Join-Path $repository ('build/vscode-designer/integration-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff'))
$application = Join-Path $run 'application with spaces'
$project = Join-Path $application 'Tasks/UI'
$managed = Join-Path $run 'managed'
New-Item -ItemType Directory -Force -Path $project | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'examples/stm32h750b-dk-starter-1/board-front.png') -Destination $application
$cli = Join-Path $managed 'bin/LitheView.Designer.Cli/debug/LitheView.Designer.Cli.dll'
if ($FixtureProject) {
    $fixture = [IO.Path]::GetFullPath($FixtureProject)
    foreach ($name in @('project.litheview.json', 'forms', 'profiles', 'simulation')) {
        $inputPath = Join-Path $fixture $name
        if (Test-Path -LiteralPath $inputPath) { Copy-Item -LiteralPath $inputPath -Destination $project -Recurse }
    }
} else {
    $previousDotnetHome = $env:DOTNET_CLI_HOME
    $previousFirstTime = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
    $previousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
    Push-Location (Join-Path $repository 'tools/designer')
    try {
        $env:DOTNET_CLI_HOME = Join-Path $run 'dotnet-home'
        $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        & $dotnetCommand build src/LitheView.Designer.Cli/LitheView.Designer.Cli.csproj -c Debug `
            --artifacts-path $managed --disable-build-servers -m:1 -p:UseSharedCompilation=false `
            *> (Join-Path $run 'cli-build.log')
        if ($LASTEXITCODE -ne 0) { throw "CLI build failed; see $run/cli-build.log" }
    } finally {
        $env:DOTNET_CLI_HOME = $previousDotnetHome
        $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $previousFirstTime
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousTelemetry
        Pop-Location
    }
    & $dotnetCommand $cli simulator create-tutorial $project `
        (Join-Path $repository 'profiles/display-surfaces/gc9a01-1.28-round-240-rgb565.json') `
        (Join-Path $repository 'profiles/logical-inputs/pointer-keys-text.json') `
        (Join-Path $repository 'profiles/display-surfaces') (Join-Path $repository 'schema/widgets.json') --existing
    if ($LASTEXITCODE -ne 0) { throw 'Tutorial fixture creation failed.' }
}
$manifest = Join-Path $project 'project.litheview.json'
$projectLibrary = $repository
if ($PackagedExtensionPath -and !$SourceOnlyPackage) {
    # Copy only the library inputs used by project validation and generation.
    # This intentionally has no prepared tools, native DLLs, build or vendor tree.
    $projectLibrary = Join-Path $application 'Dependencies/LiTheView'
    New-Item -ItemType Directory -Force -Path (Join-Path $projectLibrary 'include/lithe'), (Join-Path $projectLibrary 'tools/designer') | Out-Null
    Copy-Item -LiteralPath (Join-Path $repository 'CMakeLists.txt') -Destination $projectLibrary
    Copy-Item -LiteralPath (Join-Path $repository 'include/lithe/application.hpp') -Destination (Join-Path $projectLibrary 'include/lithe')
    foreach ($name in @('schema', 'profiles')) { Copy-Item -LiteralPath (Join-Path $repository $name) -Destination $projectLibrary -Recurse }
    Copy-Item -LiteralPath (Join-Path $repository 'tools/designer/templates') -Destination (Join-Path $projectLibrary 'tools/designer') -Recurse
}
@{schemaVersion=1; applicationRoot='../..'; libraryRoot=$projectLibrary;
  componentDirectory='Tasks/UI/Generated'; buildDirectory='out/simulator'} | ConvertTo-Json |
  Set-Content -LiteralPath (Join-Path $project 'project.litheview.paths.json') -Encoding utf8
New-Item -ItemType Directory -Force -Path (Join-Path $application '.vscode') | Out-Null
$settings = @{'litheview.dotnetPath'=$dotnetCommand; 'litheview.libraryRoot'=$projectLibrary}
if (!$PackagedExtensionPath) { $settings['litheview.servicePath'] = $service; $settings['litheview.previewWorkerPath'] = $worker }
$settings |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $application '.vscode/settings.json') -Encoding utf8
# Retain an application-owned task and verify editing/generation leaves it untouched.
@'
param([string] $Project = '')
$ErrorActionPreference = 'Stop'
[IO.File]::AppendAllText((Join-Path $PSScriptRoot '../application-task-ran.txt'), "Application task ran: $Project`n")
'@ | Set-Content -LiteralPath (Join-Path $application '.vscode/application-smoke.ps1') -Encoding utf8
@{version='2.0.0'; tasks=@(@{label='Application integration smoke'; type='process';
    command=(Get-Command pwsh -CommandType Application | Select-Object -First 1 -ExpandProperty Source);
    args=@('-NoProfile', '-File', (Join-Path $application '.vscode/application-smoke.ps1'),
        '-Project', $manifest);
    problemMatcher=@(); presentation=@{reveal='never'; panel='dedicated'}})} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $application '.vscode/tasks.json') -Encoding utf8
# The private profile owns this bounded test process. Do not reuse or terminate
# any developer VS Code window; only this exact process tree may be stopped.
$developmentExtension = Join-Path $repository 'tools/vscode'
$originalPath = $env:PATH
$removedPathEntries = @()
if ($PackagedExtensionPath) {
    # Both package forms must launch without compiler runtime directories on PATH.
    $removedPathEntries = @($env:PATH -split ';' | Where-Object {
        $_ -and ((Test-Path -LiteralPath (Join-Path $_ 'libstdc++-6.dll') -PathType Leaf) -or
                 (Test-Path -LiteralPath (Join-Path $_ 'libwinpthread-1.dll') -PathType Leaf))
    })
    $env:PATH = ($env:PATH -split ';' | Where-Object { $_ -and $_ -notin $removedPathEntries }) -join ';'
    $developmentExtension = Join-Path $run 'integration-harness'
    New-Item -ItemType Directory -Force -Path $developmentExtension | Out-Null
    @{name='litheview-integration-harness'; publisher='litheview-tests'; version='0.0.1'; engines=@{vscode='^1.95.0'};
      main='./extension.js'; activationEvents=@('onStartupFinished')} | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $developmentExtension 'package.json') -Encoding utf8
    'exports.activate = () => {};' | Set-Content -LiteralPath (Join-Path $developmentExtension 'extension.js') -Encoding utf8
    @{package=$PackagedExtensionPath; sourceOnly=[bool]$SourceOnlyPackage; libraryRoot=$projectLibrary; removedPathEntries=$removedPathEntries} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $application 'packaged-test.json') -Encoding utf8
    $installArguments = @($codeCli, '--install-extension', $PackagedExtensionPath, '--force',
        '--user-data-dir', (Join-Path $run 'user-data'), '--extensions-dir', (Join-Path $run 'extensions'))
    $quotedInstallArguments = ($installArguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $previousElectron = $env:ELECTRON_RUN_AS_NODE
    try {
        $env:ELECTRON_RUN_AS_NODE = '1'
        $installProcess = Start-Process -FilePath $codeCommand -ArgumentList $quotedInstallArguments -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $run 'install.log') -RedirectStandardError (Join-Path $run 'install-error.log')
    } finally { $env:ELECTRON_RUN_AS_NODE = $previousElectron }
    if (!$installProcess.WaitForExit(60000)) {
        & taskkill.exe /PID $installProcess.Id /T /F *> (Join-Path $run 'install-timeout-cleanup.log')
        throw "Private VSIX installation timed out; see $run"
    }
    $installProcess.Refresh()
    if ($installProcess.ExitCode -ne 0) { throw "Private VSIX installation failed; see $run/install.log and install-error.log" }
}
$arguments = @('--new-window', '--disable-workspace-trust', '--skip-welcome', '--skip-release-notes',
    '--user-data-dir', (Join-Path $run 'user-data'), '--extensions-dir', (Join-Path $run 'extensions'),
    '--extensionDevelopmentPath', $developmentExtension,
    '--extensionTestsPath', (Join-Path $repository 'tools/vscode/integration/run.js'), $application, '--wait')
$quotedArguments = ($arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
try {
    $testProcess = Start-Process -FilePath $codeCommand -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $run 'vscode.log') -RedirectStandardError (Join-Path $run 'vscode-error.log')
} finally { $env:PATH = $originalPath }
if (!$testProcess.WaitForExit($TimeoutSeconds * 1000)) {
    & taskkill.exe /PID $testProcess.Id /T /F *> (Join-Path $run 'timeout-cleanup.log')
    throw "VS Code integration exceeded $TimeoutSeconds seconds; see $run"
}
$testProcess.Refresh()
if ($testProcess.ExitCode -ne 0) { throw "VS Code integration exited $($testProcess.ExitCode); see $run/vscode.log and vscode-error.log" }
$validation = Join-Path $application 'vscode-validation.json'
if (!(Test-Path -LiteralPath $validation)) { throw "VS Code did not publish a test result; see $run/vscode.log" }
$result = Get-Content -LiteralPath $validation -Raw | ConvertFrom-Json
if (!$result.passed) { throw "VS Code integration failed; see $validation" }
if ($PackagedExtensionPath -and !$SourceOnlyPackage) {
    $service = Join-Path $result.extensionPath 'runtime/service/LitheView.Designer.Service.dll'
    $worker = Join-Path $result.extensionPath 'runtime/native/litheview_designer_preview_worker.exe'
}
[ordered]@{run=$run; application=$application; manifest=$manifest;
    cli=$(if ($FixtureProject) { $null } else { $cli }); fixtureProject=$FixtureProject;
    packagedExtension=$PackagedExtensionPath; sourceOnlyPackage=[bool]$SourceOnlyPackage; libraryRoot=$projectLibrary; removedPathEntries=$removedPathEntries;
    packageSha256=$(if ($PackagedExtensionPath) { (Get-FileHash -LiteralPath $PackagedExtensionPath).Hash.ToLowerInvariant() } else { $null });
    serviceSha256=(Get-FileHash -LiteralPath $service).Hash.ToLowerInvariant();
    workerSha256=(Get-FileHash -LiteralPath $worker).Hash.ToLowerInvariant(); validation=$validation} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repository 'build/vscode-designer/latest-integration.json') -Encoding utf8
Write-Output "VS Code integration passed: $validation"
