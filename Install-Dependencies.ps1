# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

$ErrorActionPreference = 'Stop'
$toolDirectory = Join-Path $PSScriptRoot 'tools\scrcpy'
if (Test-Path -LiteralPath (Join-Path $toolDirectory 'scrcpy.exe')) { Write-Output 'Local scrcpy already installed.'; exit }
$release = Invoke-RestMethod 'https://api.github.com/repos/Genymobile/scrcpy/releases/tags/v4.1'
$asset = $release.assets | Where-Object name -eq 'scrcpy-win64-v4.1.zip' | Select-Object -First 1
if (-not $asset -or -not $asset.digest -or -not $asset.digest.StartsWith('sha256:')) { throw 'Release asset or SHA-256 digest missing.' }
$archive = Join-Path ([IO.Path]::GetTempPath()) ('PhoneControl-scrcpy-' + [Guid]::NewGuid().ToString('N') + '.zip')
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive
$actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $asset.digest.Substring(7).ToLowerInvariant()) { throw 'scrcpy download checksum mismatch.' }
$extractRoot = Join-Path $PSScriptRoot 'tools'
New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath $extractRoot -Force
Rename-Item -LiteralPath (Join-Path $extractRoot 'scrcpy-win64-v4.1') -NewName 'scrcpy'
Remove-Item -LiteralPath $archive
Write-Output "scrcpy 4.1 installed in $toolDirectory"
