# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

param([string]$Version = '1.0.7')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $projectRoot 'tools\inno\ISCC.exe'
$compilerHash = 'D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A'
if (-not (Test-Path -LiteralPath $compiler)) {
    $deps = Join-Path $projectRoot 'artifacts\installer-deps'
    New-Item -ItemType Directory -Force -Path $deps | Out-Null
    $bootstrap = Join-Path $deps 'innosetup-7.1.0-x64.exe'
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $bootstrap
    if ((Get-FileHash -LiteralPath $bootstrap).Hash -ne '0362A383ED217D4C4239B5933866DD96D3EB2102737DA92F80F6057A4B40DF2F') { throw 'Inno Setup bootstrap checksum mismatch.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $bootstrap
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B\.V\.') { throw 'Inno Setup publisher verification failed.' }
    $install = Start-Process -FilePath $bootstrap -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',('/DIR="' + (Split-Path -Parent $compiler) + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($install.ExitCode -ne 0) { throw "Inno Setup compiler installation failed: $($install.ExitCode)" }
}
if ((Get-FileHash -LiteralPath $compiler).Hash -ne $compilerHash) { throw 'Unexpected Inno Setup compiler. Review and pin a new version before releasing.' }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'tools\scrcpy\scrcpy.exe'))) { & (Join-Path $projectRoot 'Install-Dependencies.ps1') }
# Isolated staging: never overwrite a portable app somebody is using.
$staging = Join-Path $projectRoot ('artifacts\installer-staging\' + $Version + '-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'Publish.ps1') -OutputDirectory $staging -Version $Version -Obfuscate
$test = Start-Process -FilePath (Join-Path $staging 'PhoneControl.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
if ($test.ExitCode -ne 0) { throw "PhoneControl self-tests failed. See $staging\self-test-results.txt" }
$required = @('PhoneControl.exe','PhoneControl.dll','PhoneControl.runtimeconfig.json','coreclr.dll','hostfxr.dll','Microsoft.UI.Xaml.dll','Microsoft.WindowsAppRuntime.dll','PhoneControl.pri','tools\phonecontrol-control.jar','tools\scrcpy\scrcpy.exe','tools\scrcpy\adb.exe','tools\scrcpy\AdbWinApi.dll','tools\scrcpy\AdbWinUsbApi.dll','tools\scrcpy\scrcpy-server','tools\scrcpy\LICENSE.txt','docs\index.html','LICENSE','THIRD-PARTY-NOTICES.md','SOURCE.md','source\PhoneControl-source.zip')
foreach ($relative in $required) { if (-not (Test-Path -LiteralPath (Join-Path $staging $relative))) { throw "Release payload missing $relative" } }
$release = Join-Path $projectRoot 'artifacts\releases'
New-Item -ItemType Directory -Force -Path $release | Out-Null
$sourceArchive = Join-Path $release "PhoneControl-$Version-source.zip"
Copy-Item -LiteralPath (Join-Path $staging 'source\PhoneControl-source.zip') -Destination $sourceArchive -Force
# ADB's shared server may still have the installed executable/DLLs open after
# PhoneControl exits. Skip byte-identical tools instead of replacing those files.
# Changed/missing/corrupt tools still go through normal installation/repair.
$toolEntries = "$staging.scrcpy-files.iss"
$toolLines = Get-ChildItem -LiteralPath (Join-Path $staging 'tools\scrcpy') -File | ForEach-Object {
    $toolHash = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()
    'Source: "{0}"; DestDir: "{{app}}\tools\scrcpy"; Flags: ignoreversion; Check: ShouldUpdateTool(''{1}'', ''{2}'')' -f $_.FullName, $_.Name.Replace("'", "''"), $toolHash
}
[IO.File]::WriteAllLines($toolEntries, [string[]]$toolLines)
& $compiler ("/DPayloadDir=$staging") ("/DReleaseDir=$release") ("/DToolEntries=$toolEntries") ("/DAppVersion=$Version") (Join-Path $projectRoot 'installer\PhoneControl.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$payloadInstaller = Join-Path $release "PhoneControl-Payload-$Version-win-x64.exe"
$installer = Join-Path $release "PhoneControl-Setup-$Version-win-x64.exe"
& (Join-Path $PSScriptRoot 'Build-Bootstrap.ps1') -Payload $payloadInstaller -Output $installer -Version $Version
$hash = (Get-FileHash -LiteralPath $installer).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installer.sha256", "$hash  $([IO.Path]::GetFileName($installer))`n")
$payload = Get-ChildItem -LiteralPath $staging -Recurse -File | Where-Object { $_.Extension -ne '.pdb' -and $_.Name -ne 'self-test-results.txt' } | ForEach-Object {
    [ordered]@{ path = $_.FullName.Substring($staging.Length + 1).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant(); bytes = $_.Length }
}
$commit = & git -C $projectRoot rev-parse --verify HEAD 2>$null
if ($LASTEXITCODE -ne 0) { $commit = $null }
$dirty = [bool](& git -C $projectRoot status --porcelain)
[ordered]@{ version=$Version; license='AGPL-3.0-only'; copyrightOwner='jojogaming93'; builtUtc=[DateTime]::UtcNow.ToString('o'); sourceCommit=$commit; sourceDirty=$dirty; sourceArchive=[IO.Path]::GetFileName($sourceArchive); sourceArchiveSha256=(Get-FileHash -LiteralPath $sourceArchive).Hash.ToLowerInvariant(); architecture='x64'; minimumWindowsBuild=26100; obfuscated=$true; installerSha256=$hash; payload=@($payload) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $release "PhoneControl-$Version-manifest.json") -Encoding UTF8
Write-Output "Installer: $installer"
