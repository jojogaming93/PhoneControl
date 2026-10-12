# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

param([string]$Installer, [Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$identity = & (Join-Path $PSScriptRoot 'Get-ReleaseIdentity.ps1') -Version $Version
if (-not $Installer) { $Installer = Join-Path $projectRoot "artifacts\releases\PhoneControl-Setup-$Version-win-x64.exe" }
$Installer = [IO.Path]::GetFullPath($Installer)
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$($identity.AppId)_is1"
$otherId = if ($identity.IsPreview) { '{CF1762E2-784A-49DA-AF70-A6753131BE40}' } else { '{0F3B2360-8C37-4C30-8FA5-2A697FDC8836}' }
$otherKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$otherId`_is1"
$otherRegistration = if (Test-Path -LiteralPath $otherKey) { Get-ItemProperty -LiteralPath $otherKey | ConvertTo-Json -Depth 5 -Compress } else { $null }
if (Test-Path -LiteralPath $key) { throw 'PhoneControl is already installed. Use a disposable Windows account/VM for installer lifecycle tests.' }
$testRoot = Join-Path $projectRoot ('artifacts\installer-test-' + [Guid]::NewGuid().ToString('N'))
$target = [IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
# The uninstaller will remove this tree: verify its resolved boundary first.
$boundary = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $target.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe installer test target.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$profiles = @('PhoneControl','PhoneControl Preview') | ForEach-Object {
    $path = Join-Path $env:LOCALAPPDATA "$_\settings.json"
    $created = -not (Test-Path -LiteralPath $path)
    if ($created) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
        @{ AutoConnect=$false; Profile=@{ Name="Retained $_ mapping"; Sensitivity=2.7 } } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding utf8
    }
    [pscustomobject]@{ Path=$path; Hash=(Get-FileHash -LiteralPath $path).Hash; Created=$created }
}
function Assert-ProfilePreserved {
    foreach ($profile in $profiles) {
        $current = if (Test-Path -LiteralPath $profile.Path) { (Get-FileHash -LiteralPath $profile.Path).Hash } else { $null }
        if ($current -ne $profile.Hash) { throw 'Installer changed Stable or Preview settings.' }
    }
    $currentRegistration = if (Test-Path -LiteralPath $otherKey) { Get-ItemProperty -LiteralPath $otherKey | ConvertTo-Json -Depth 5 -Compress } else { $null }
    if ($currentRegistration -ne $otherRegistration) { throw 'Installer changed the other channel registration.' }
}
function Install-TestPayload([string]$LogName) {
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOLAUNCH','/NOSHORTCUTS',('/DIR="' + $target + '"'),('/LOG="' + (Join-Path $testRoot $LogName) + '"'))
    $process = Start-Process -FilePath $Installer -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer exit code: $($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'unins000.exe'))) { throw 'Uninstaller was not registered.' }
    if ((Get-ItemProperty -LiteralPath $key).InstallLocation.TrimEnd('\') -ne $target) { throw 'Unexpected installation registration.' }
    Assert-ProfilePreserved
    $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $target 'PhoneControl.dll'))
    if (($metadata.ProductVersion -split '\+')[0] -ne $Version -or $metadata.FileVersion -ne $identity.NumericVersion) { throw 'Installed application version metadata does not match the release.' }
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
    # Remove only synthetic profiles this invocation created, after checking their hash.
    foreach ($profile in $profiles | Where-Object Created) {
        if ((Test-Path -LiteralPath $profile.Path) -and (Get-FileHash -LiteralPath $profile.Path).Hash -eq $profile.Hash) {
            Remove-Item -LiteralPath $profile.Path
        }
    }
}
Write-Output "Installer lifecycle passed. Evidence: $testRoot"
