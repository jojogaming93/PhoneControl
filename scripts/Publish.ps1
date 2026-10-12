# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

param([switch]$DesktopShortcut, [string]$OutputDirectory, [string]$Version = '1.0.7', [switch]$Obfuscate)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& python (Join-Path $PSScriptRoot 'Check-PublicSource.py')
if ($LASTEXITCODE -ne 0) { throw 'Public source privacy check failed.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts\PhoneControl' }
$outputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch.' }
& dotnet publish (Join-Path $projectRoot 'src\PhoneControl\PhoneControl.csproj') -c Release -o $outputDirectory "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
$scrcpyDirectory = Join-Path $projectRoot 'tools\scrcpy'
if (-not (Test-Path -LiteralPath (Join-Path $scrcpyDirectory 'scrcpy.exe'))) { throw 'Run Install-Dependencies.ps1 first.' }
$scrcpyVersion = & (Join-Path $scrcpyDirectory 'scrcpy.exe') --version
if ($LASTEXITCODE -ne 0 -or ($scrcpyVersion -join "`n") -notmatch '(?m)^scrcpy 4\.1(?:\s|$)') { throw 'The release requires scrcpy 4.1.' }
$toolDestination = Join-Path $outputDirectory 'tools\scrcpy'
New-Item -ItemType Directory -Force -Path $toolDestination | Out-Null
Get-ChildItem -LiteralPath $scrcpyDirectory -File | Copy-Item -Destination $toolDestination -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'LICENSE'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md'), (Join-Path $projectRoot 'VALIDATION.md') -Destination $outputDirectory -Force
$docsDestination = Join-Path $outputDirectory 'docs'
New-Item -ItemType Directory -Force -Path $docsDestination | Out-Null
# Copy only audited source files, never ignored captures or local scratch files.
$docFiles = & git -C $projectRoot -c core.quotePath=false ls-files --cached --others --exclude-standard -- docs
foreach ($relative in $docFiles) {
    $docSource = Join-Path $projectRoot $relative
    if (-not (Test-Path -LiteralPath $docSource -PathType Leaf)) { continue }
    $docTarget = Join-Path $outputDirectory $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $docTarget) | Out-Null
    Copy-Item -LiteralPath $docSource -Destination $docTarget -Force
}
# Remove the retired chapter when reusing a portable output directory.
$retiredChapter = Join-Path $docsDestination 'signing-application.md'
if (Test-Path -LiteralPath $retiredChapter) { Remove-Item -LiteralPath $retiredChapter -Force }
$serverNotices = Join-Path $outputDirectory 'tools\control-server-source'
New-Item -ItemType Directory -Force -Path $serverNotices | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'server\PointersState.java'), (Join-Path $projectRoot 'server\README.md'), (Join-Path $projectRoot 'server\LICENSE.scrcpy') -Destination $serverNotices -Force
if ($Obfuscate) { & (Join-Path $PSScriptRoot 'Obfuscate.ps1') -InputDirectory $outputDirectory }
& (Join-Path $PSScriptRoot 'Build-SourceArchive.ps1') -OutputFile (Join-Path $outputDirectory 'source\PhoneControl-source.zip') -Version $Version
if ($DesktopShortcut) {
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'PhoneControl.lnk'
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $outputDirectory 'PhoneControl.exe'
    $shortcut.WorkingDirectory = $outputDirectory
    $shortcut.Description = 'Free USB Android mirroring and game control mappings'
    $shortcut.Save()
}
Write-Output "PhoneControl: $(Join-Path $outputDirectory 'PhoneControl.exe')"
