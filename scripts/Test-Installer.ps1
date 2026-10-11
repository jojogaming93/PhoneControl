# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

param([string]$Installer)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Installer) { $Installer = Join-Path $projectRoot 'artifacts\releases\PhoneControl-Setup-1.0.7-win-x64.exe' }
$Installer = [IO.Path]::GetFullPath($Installer)
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{CF1762E2-784A-49DA-AF70-A6753131BE40}_is1'
if (Test-Path -LiteralPath $key) { throw 'PhoneControl is already installed. Use a disposable Windows account/VM for installer lifecycle tests.' }
$testRoot = Join-Path $projectRoot ('artifacts\installer-test-' + [Guid]::NewGuid().ToString('N'))
$target = [IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
# The uninstaller will remove this tree: verify its resolved boundary first.
$boundary = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $target.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe installer test target.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$profile = Join-Path $env:LOCALAPPDATA 'PhoneControl\settings.json'
$originalProfileHash = if (Test-Path -LiteralPath $profile) { (Get-FileHash -LiteralPath $profile).Hash } else { $null }
function Assert-ProfilePreserved {
    $current = if (Test-Path -LiteralPath $profile) { (Get-FileHash -LiteralPath $profile).Hash } else { $null }
    if ($current -ne $originalProfileHash) { throw 'Installer changed the saved user profile.' }
}
function Install-TestPayload([string]$LogName) {
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOLAUNCH','/NOSHORTCUTS',('/DIR="' + $target + '"'),('/LOG="' + (Join-Path $testRoot $LogName) + '"'))
    $process = Start-Process -FilePath $Installer -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer exit code: $($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'unins000.exe'))) { throw 'Uninstaller was not registered.' }
    if ((Get-ItemProperty -LiteralPath $key).InstallLocation.TrimEnd('\') -ne $target) { throw 'Unexpected installation registration.' }
    Assert-ProfilePreserved
}
try {
    Install-TestPayload 'install.log'
    $savedPath = $env:PATH
    try {
        # A restricted PATH proves bundled tools are used; this is not a clean VM.
        $env:PATH = "$env:SystemRoot\System32;$env:SystemRoot"
        $test = Start-Process -FilePath (Join-Path $target 'PhoneControl.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
        if ($test.ExitCode -ne 0) { throw 'Installed application self-tests failed.' }
        & (Join-Path $target 'tools\scrcpy\adb.exe') version | Set-Content (Join-Path $testRoot 'adb-version.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Installed ADB failed.' }
        & (Join-Path $target 'tools\scrcpy\scrcpy.exe') --version | Set-Content (Join-Path $testRoot 'scrcpy-version.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Installed scrcpy failed.' }
    } finally { $env:PATH = $savedPath }
    Copy-Item -LiteralPath (Join-Path $target 'self-test-results.txt') -Destination $testRoot
    # Repair must replace corrupted files, retain settings, and keep one entry.
    [IO.File]::WriteAllText((Join-Path $target 'tools\phonecontrol-control.jar'), 'repair probe')
    $toolNotice = Join-Path $target 'tools\scrcpy\LICENSE.txt'
    $toolNoticeHash = (Get-FileHash -LiteralPath $toolNotice).Hash
    [IO.File]::WriteAllText($toolNotice, 'tool hash repair probe')
    Install-TestPayload 'repair.log'
    $expected = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'assets\phonecontrol-control.jar')).Hash
    if ((Get-FileHash -LiteralPath (Join-Path $target 'tools\phonecontrol-control.jar')).Hash -ne $expected) { throw 'Repair did not restore the control server.' }
    if ((Get-FileHash -LiteralPath $toolNotice).Hash -ne $toolNoticeHash) { throw 'Tool hash guard skipped a corrupted bundled tool.' }
    Assert-ProfilePreserved
    'PASS install, restricted-PATH runtime/tools, self-tests, repair and profile preservation' | Set-Content (Join-Path $testRoot 'installer-results.txt')
} finally {
    $uninstaller = Join-Path $target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $testRoot 'uninstall.log') + '"')) -WindowStyle Hidden -PassThru -Wait
        if ($uninstall.ExitCode -ne 0) { throw "Uninstall failed: $($uninstall.ExitCode)" }
        if (Test-Path -LiteralPath $key) { throw 'Uninstall registration remains.' }
        if (Test-Path -LiteralPath (Join-Path $target 'PhoneControl.exe')) { throw 'Uninstall left the app executable behind.' }
        Assert-ProfilePreserved
        'PASS uninstall and profile preservation' | Add-Content (Join-Path $testRoot 'installer-results.txt')
    }
}
Write-Output "Installer lifecycle passed. Evidence: $testRoot"
