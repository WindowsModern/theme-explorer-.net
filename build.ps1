<#
.SYNOPSIS
    Builds Theme Explorer (WinForms + WPF + libraries) and optionally runs both
    headless self-tests.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -SelfTest
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) {
            return $found
        }
    }

    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'MSBuild was not found. Install Visual Studio 2015 or newer, or add msbuild.exe to PATH.'
}

function Invoke-SelfTest {
    param(
        [string] $Executable,
        [string] $OutputDirectory,
        [string] $Label
    )

    if (Test-Path $OutputDirectory) {
        Remove-Item $OutputDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

    # Both applications are WinExe, so they do not write to the console; they
    # write selftest.log and exit on their own.
    & $Executable --selftest "--out=$OutputDirectory"

    $logPath = Join-Path $OutputDirectory 'selftest.log'
    $deadline = (Get-Date).AddSeconds(120)
    while (-not (Test-Path $logPath) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }

    if (-not (Test-Path $logPath)) {
        throw "$Label self-test did not write $logPath."
    }

    $log = Get-Content $logPath -Encoding UTF8
    Write-Host "--- $Label self-test ---"
    $log | ForEach-Object { Write-Host $_ }

    if ($log -match 'ASSERT-FAIL|ERROR:') {
        throw "$Label self-test reported failures."
    }

    Write-Host "$Label self-test passed. Artifacts: $OutputDirectory"
}

$msbuild = Find-MSBuild
Write-Host "MSBuild: $msbuild"

& $msbuild (Join-Path $root 'ThemeExplorer.sln') /t:Rebuild `
    "/p:Configuration=$Configuration" '/p:Platform=Any CPU' /v:minimal

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$winForms = Join-Path $root "src\ThemeExplorer.App\bin\$Configuration\ThemeExplorer.exe"
$wpf = Join-Path $root "src\ThemeExplorer.Wpf\bin\$Configuration\ThemeExplorer.Wpf.exe"

Write-Host "Built: $winForms"
Write-Host "Built: $wpf"

if ($SelfTest) {
    Invoke-SelfTest -Executable $winForms `
        -OutputDirectory (Join-Path $root "artifacts\selftest-winforms-$Configuration") `
        -Label 'WinForms'

    Invoke-SelfTest -Executable $wpf `
        -OutputDirectory (Join-Path $root "artifacts\selftest-wpf-$Configuration") `
        -Label 'WPF'
}
